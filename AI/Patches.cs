using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Trajectories;
using Interplanetary.GameEngine;
using Interplanetary.GameSession;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;
using Interplanetary.Weapons;
using InterplanetaryAI.Brain;

namespace InterplanetaryAI
{
    // Harmony hooks. Each one reads game state into the plain Brain types, asks the Brain, and writes
    // the answer back. Any exception leaves the native behaviour in place and is logged once per hook.
    internal static class AiPatches
    {
        private static readonly FieldInfo s_moduleCore = AccessTools.Field(typeof(AIModule), "m_core");
        private static readonly FieldInfo s_targetableWeapons = AccessTools.Field(typeof(AIWeaponTargeting), "m_targetableWeapons");
        private static readonly FieldInfo s_goalCore = AccessTools.Field(typeof(AIWeaponTargetingGoal), "m_core");
        private static readonly FieldInfo s_goalWeapons = AccessTools.Field(typeof(AIWeaponTargetingGoal), "m_weapons");
        private static readonly FieldInfo s_goalHitting = AccessTools.Field(typeof(AIWeaponTargetingGoal), "m_hittingWeapons");
        private static readonly FieldInfo s_goalMissing = AccessTools.Field(typeof(AIWeaponTargetingGoal), "m_missingWeapons");
        private static readonly FieldInfo s_acceptableMiss = AccessTools.Field(typeof(AITrajectoryWeapon), "m_acceptableMissTrajectory");

        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();

        // ---- match lifecycle -------------------------------------------------------------------

        // Every player (human too) gets an AICore at match start or load; only AI players ever run it
        // (PlayerManager.Update -> UpdateAI is gated on AIPlayer). Memory is reset on every start or load:
        // the turn value at this point is not proven fresh, and the memory is short-lived anyway.
        private static void CoreInitializePostfix(AICore __instance)
        {
            Plugin.ResetMatchMemory();
            try
            {
                PlayerData me = __instance.PlayerData;
                if (!me.AIPlayer) return;
                // v0.12: the long aiming window by default; with the strategy on, hold turns go back to the game's 20 s.
                if (Plugin.Active) StrategyPatches.SetAimWindow(__instance, Plugin.AimingWindowSeconds.Value);
                Plugin.WriteDecision(new Json()
                    .Str("e", "init").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName).Int("id", me.PlayerID).Int("lvl", me.AILevel)
                    .Bool("active", Plugin.Active).Num("acc", AccuracyPlan.ForLevel(Plugin.Accuracy, me.AILevel))
                    .Num("nativeAcc", __instance.Difficulty != null ? __instance.Difficulty.Accuracy : -1f).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("init", e);
            }
        }

        // ---- who to shoot ----------------------------------------------------------------------

        private static void TargetingBeginTurnPostfix(AIWeaponTargeting __instance)
        {
            if (!Plugin.Active || !Plugin.SmartTargeting.Value) return;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                if (core == null || !core.PlayerData.AIPlayer) return;
                // Backstop for the Initialize reset: the init-time turn value is not proven fresh, but the first targeting
                // of a new match or an earlier save always shows the turn going backwards. Runs here, before this turn's
                // economy and strategy state are built (EconomyPatches.CoreBeginTurnPostfix), never after.
                int turn = GameManager.CurrentTurn;
                if (turn < Plugin.LastTurnSeen) Plugin.ResetMatchMemory();
                Plugin.LastTurnSeen = turn;
            }
            catch (Exception e)
            {
                ReportOnce("targeting-reset", e);
            }
            // v0.10: with the strategy on, targets are assigned once it has decided (StrategyPatches.BeginTurn, later in
            // the same AICore.BeginTurn), so a strike commits every weapon to one enemy.
            if (Plugin.StrategyEnabled.Value && Plugin.DirectorEnabled.Value) return;
            AssignTargets(__instance, -1);
        }

        // commitPlayerId >= 0: every weapon at that enemy (each falling back to one it can reach); -1: the v0.9 split plan.
        internal static void AssignTargets(AIWeaponTargeting __instance, int commitPlayerId)
        {
            PlayerData logMe = null;
            List<EnemyView> logEnemies = null;
            int[] countByEnemy = null;
            int logTurn = 0, logWeapons = 0, rerouted = 0;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                List<AITargetableWeapon> weapons = (List<AITargetableWeapon>)s_targetableWeapons.GetValue(__instance);
                if (core == null || !core.PlayerData.AIPlayer || weapons == null || weapons.Count == 0) return;
                List<CelestialBody> planets = core.EnemyPlanets;
                if (planets == null || planets.Count == 0) return;

                int turn = GameManager.CurrentTurn;
                PlayerData me = core.PlayerData;
                List<EnemyView> enemies = new List<EnemyView>();
                foreach (CelestialBody planet in planets)
                {
                    enemies.Add(BuildEnemyView(me, planet.Player, turn));
                }
                TargetPlanner.Score(enemies, Plugin.Planner);

                List<AITrajectoryWeapon> gravityWeapons = new List<AITrajectoryWeapon>();
                List<WeaponSlot> slots = new List<WeaponSlot>();
                foreach (AITargetableWeapon weapon in weapons)
                {
                    if (!(weapon.TrajectoryWeapon is AIGravityTrajectoryWeapon)) continue;
                    WeaponSlot slot = new WeaponSlot { WeaponKey = WeaponKey(weapon.Weapon) };
                    Plugin.Reach.FillUnreachable(slot.WeaponKey, turn, slot.UnreachableTargetIds);
                    slots.Add(slot);
                    gravityWeapons.Add(weapon.TrajectoryWeapon);
                }
                if (slots.Count == 0) return;

                int commitIndex = -1;
                for (int i = 0; i < enemies.Count; i++) if (commitPlayerId >= 0 && enemies[i].PlayerId == commitPlayerId) commitIndex = i;
                // The whole plan is computed above; only plain assignments from here on.
                int[] plan = TargetPlanner.Assign(enemies, slots, Plugin.Planner, Plugin.Rng, commitIndex);
                countByEnemy = new int[enemies.Count];
                for (int i = 0; i < plan.Length; i++)
                {
                    if (plan[i] < 0) continue;
                    gravityWeapons[i].TargetEnemy = planets[plan[i]];
                    countByEnemy[plan[i]]++;
                    if (slots[i].UnreachableTargetIds.Count > 0) rerouted++;
                }
                logMe = me;
                logEnemies = enemies;
                logTurn = turn;
                logWeapons = slots.Count;
            }
            catch (Exception e)
            {
                ReportOnce("targeting", e);
                return;
            }

            try
            {
                if (logMe != null) LogTargeting(logMe, logTurn, logEnemies, countByEnemy, logWeapons, rerouted);
            }
            catch (Exception e)
            {
                ReportOnce("targeting-log", e);
            }
        }

        internal static EnemyView BuildEnemyView(PlayerData me, PlayerData enemy, int turn)
        {
            EnemyView view = new EnemyView
            {
                PlayerId = enemy.PlayerID,
                Name = enemy.UserNickName,
                IsHuman = !enemy.AIPlayer,
            };
            // Exactly what this seat's intel overlay shows (IntelOverlayIntelThermometerComponent.cs:313-373): the
            // enemy's round-start totals, each shown when our round-start intel >= its stealth x their counterintel,
            // all shown when the Intelligence setting is off. v0.3 summed live stats of visible buildings instead,
            // which needs far more intel (city stealth 30-45 x counterintel) and reads per-building values.
            FrozenIntelState theirs = enemy.IntelHandler.FrozenIntelState;
            FrozenIntelState ours = me.IntelHandler.FrozenIntelState;
            if (theirs != null && ours != null)
            {
                bool intelOff = GameSessionManager.CurrentGameSettings.Intelligence != 1;
                int intel = ours.Intel;
                if (intelOff || intel >= theirs.EnergyStealth)
                {
                    view.EnergyKnown = true;
                    view.EnergyProduction = Math.Max(0, theirs.EnergyProduction);
                }
                if (intelOff || (intel >= theirs.EnergyStealth && intel >= theirs.MaterialStealth))
                {
                    view.EconomyKnown = true;
                    view.Economy = Math.Max(0, theirs.EnergyProduction) + Math.Max(0, theirs.MaterialProduction);
                }
                if (intelOff || intel >= theirs.PopulationStealth)
                {
                    // Population is a city's durability; the overlay shows the total and the city count together.
                    view.CityPopulation = Math.Max(0, theirs.TotalPopulation);
                    view.VisibleCities = enemy.BuildingHandler.GetRemainingCities().Count; // as the overlay counts
                }
            }
            view.Grudge = Plugin.Grudges.Get(me.PlayerID, enemy.PlayerID, turn);
            return view;
        }

        // Live values are read only for buildings our intel currently sees. Shadow buildings show frozen
        // last-known state, so reading their live stats would be hidden information; they are skipped.
        internal static bool IsVisibleIntact(PlayerData me, BaseBuilding building)
        {
            if (building == null || building.CurrentState != BaseBuilding.BuildingState.Normal) return false;
            return me.IntelHandler.GetBuildingVisibility(building) == BuildingIntelVisibility.Visible;
        }

        // v0.10: hold fire empties these lists; without them the strategy never holds (StrategyPatches.HoldAvailable).
        internal static bool HoldFieldsResolved
        {
            get { return s_goalCore != null && s_goalHitting != null && s_goalMissing != null; }
        }

        internal static string WeaponKey(WeaponGameplayData weapon)
        {
            return weapon.Building != null ? weapon.Building.ObjectId.ToString() : "weapon@" + weapon.GetHashCode();
        }

        // ---- how many shots are planned to hit -------------------------------------------------

        private static void GoalBeginTurnPostfix(AIWeaponTargetingGoal __instance)
        {
            if (!Plugin.Active || !Plugin.AccuracyOverride.Value) return;
            try
            {
                int total = __instance.Hits + __instance.Misses;
                if (total == 0) return;
                AICore core = (AICore)s_goalCore.GetValue(__instance);
                PlayerData me = core.PlayerData;
                if (!me.AIPlayer) return;

                float accuracy = AccuracyPlan.ForLevel(Plugin.Accuracy, me.AILevel);
                float carry;
                Plugin.AccuracyCarry.TryGetValue(me.PlayerID, out carry);
                int nativeHits = __instance.Hits;
                int hits = AccuracyPlan.PlannedHits(total, accuracy, ref carry);
                Plugin.AccuracyCarry[me.PlayerID] = carry;

                __instance.Hits = hits;
                __instance.Misses = total - hits;

                Plugin.WriteDecision(new Json()
                    .Str("e", "accuracy").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName).Int("lvl", me.AILevel)
                    .Num("acc", accuracy).Int("shots", total).Int("nativeHits", nativeHits).Int("hits", hits).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("accuracy", e);
            }
        }

        // ---- what actually got fired; feeds reachability memory ---------------------------------

        // Native bug: the hitting/missing lists are only rebuilt when a weapon registers a primary hit
        // (AITrajectoryWeapon.RegisterPrimaryTargetTrajectory) and are never cleared at turn start. A weapon
        // that only found a secondary hit or a miss is never picked, and last turn's entries return no shot.
        // Rebuilding the lists from this turn's results right before firing fixes both.
        // A planned "miss" can be a trajectory onto the AI's own planet (AIWeaponTargeting.handleFinishedAccurateTrajectory
        // classes an own-planet impact as a miss). Only verified near-misses are kept: an acceptable miss whose stored
        // result did not hit our planet. Unacceptable misses are dropped because their stored shot is the latest one
        // tried, not the stored closest result (AITrajectoryWeapon.RegisterUnacceptableMissTrajectory), so it cannot be checked.
        private static void GoalTargetWeaponsPrefix(AIWeaponTargetingGoal __instance, out bool __state)
        {
            __state = !__instance.AreWeaponsTargeted;
            if (!__state || !Plugin.Active) return;
            try
            {
                // v0.10 hold fire: nothing in the firing lists, nothing fired, no firing energy charged (the reserved
                // energy went to construction this turn).
                AICore holder = (AICore)s_goalCore.GetValue(__instance);
                if (holder != null && StrategyPatches.HoldFire(holder.PlayerData.PlayerID))
                {
                    List<AITrajectoryWeapon> holdHitting = (List<AITrajectoryWeapon>)s_goalHitting.GetValue(__instance);
                    List<AITrajectoryWeapon> holdMissing = (List<AITrajectoryWeapon>)s_goalMissing.GetValue(__instance);
                    if (holdHitting != null) holdHitting.Clear();
                    if (holdMissing != null) holdMissing.Clear();
                    return;
                }
            }
            catch (Exception e)
            {
                ReportOnce("hold-fire", e);
            }
            try
            {
                if (Plugin.RefreshFiringList.Value) __instance.DetermineShootingWeapons();
                List<AITrajectoryWeapon> missing = (List<AITrajectoryWeapon>)s_goalMissing.GetValue(__instance);
                if (missing != null && !Plugin.FireMisses.Value)
                {
                    missing.Clear();
                }
                else if (missing != null)
                {
                    AICore core = (AICore)s_goalCore.GetValue(__instance);
                    missing.RemoveAll(weapon => !IsSafeNearMiss(weapon, core.Planet));
                }
            }
            catch (Exception e)
            {
                ReportOnce("refresh-firing-list", e);
            }
            // v0.11: a strike whose weapons mostly found no trajectory onto the committed target is called off; its firing
            // energy stays for defense rolls.
            try
            {
                AICore striker = (AICore)s_goalCore.GetValue(__instance);
                if (striker == null || !Plugin.StrategyEnabled.Value) return;
                int commit = StrategyPatches.CommitTarget(striker.PlayerData.PlayerID);
                if (commit < 0) return;
                List<AITrajectoryWeapon> hitting = (List<AITrajectoryWeapon>)s_goalHitting.GetValue(__instance);
                int onCommit = 0;
                if (hitting != null)
                {
                    foreach (AITrajectoryWeapon w in hitting)
                    {
                        if (w.HasFoundPrimaryTargetTrajectory && w.TargetEnemy != null && w.TargetEnemy.Player != null && w.TargetEnemy.Player.PlayerID == commit) onCommit++;
                    }
                }
                if (StrategyPatches.ShouldAbort(striker.PlayerData, onCommit))
                {
                    List<AITrajectoryWeapon> missing = (List<AITrajectoryWeapon>)s_goalMissing.GetValue(__instance);
                    if (hitting != null) hitting.Clear();
                    if (missing != null) missing.Clear();
                }
            }
            catch (Exception e)
            {
                ReportOnce("strike-abort", e);
            }
        }

        private static bool IsSafeNearMiss(AITrajectoryWeapon weapon, CelestialBody ownPlanet)
        {
            if (!weapon.HasFoundAcceptableMissTrajectory) return false;
            AIStoredTrajectory stored = (AIStoredTrajectory)s_acceptableMiss.GetValue(weapon);
            if (stored == null || stored.TrajectoryResults == null) return false;
            return stored.TrajectoryResults.HitCelestialBody != ownPlanet;
        }

        private static void GoalTargetWeaponsPostfix(AIWeaponTargetingGoal __instance, bool __state)
        {
            if (!__state || !Plugin.Active) return;
            try
            {
                AICore core = (AICore)s_goalCore.GetValue(__instance);
                List<AITrajectoryWeapon> weapons = (List<AITrajectoryWeapon>)s_goalWeapons.GetValue(__instance);
                List<AITrajectoryWeapon> hitting = (List<AITrajectoryWeapon>)s_goalHitting.GetValue(__instance);
                List<AITrajectoryWeapon> missing = (List<AITrajectoryWeapon>)s_goalMissing.GetValue(__instance);
                if (core == null || weapons == null) return;

                int turn = GameManager.CurrentTurn;
                int powered = 0, primary = 0, secondary = 0;
                Dictionary<string, int> primaryByTarget = new Dictionary<string, int>();
                Dictionary<string, int> assignedByTarget = new Dictionary<string, int>();
                Dictionary<int, int> assignedById = new Dictionary<int, int>(), primaryById = new Dictionary<int, int>();
                List<string> reachKeys = new List<string>();
                List<int> reachTargets = new List<int>();
                List<bool> reachFound = new List<bool>();
                foreach (AITrajectoryWeapon weapon in weapons)
                {
                    WeaponGameplayData data = weapon.TargetableWeapon != null ? weapon.TargetableWeapon.Weapon : null;
                    // v0.12: live weapons only (the targetable list keeps destroyed weapons: v0.11.1 logged 53-54 to the end).
                    if (data == null || data.Building == null || data.Building.CurrentState != BaseBuilding.BuildingState.Normal
                        || !data.Building.Power.HasPower) continue;
                    powered++;
                    string targetName = weapon.TargetEnemy != null && weapon.TargetEnemy.Player != null ? weapon.TargetEnemy.Player.UserNickName : "?";
                    Increment(assignedByTarget, targetName);
                    if (weapon.HasFoundPrimaryTargetTrajectory)
                    {
                        primary++;
                        Increment(primaryByTarget, targetName);
                    }
                    else if (weapon.HasFoundSecondaryTargetTrajectory)
                    {
                        secondary++;
                    }
                    if (weapon.TargetEnemy != null && weapon.TargetEnemy.Player != null)
                    {
                        int targetId = weapon.TargetEnemy.Player.PlayerID;
                        reachKeys.Add(WeaponKey(data));
                        reachTargets.Add(targetId);
                        reachFound.Add(weapon.HasFoundPrimaryTargetTrajectory);
                        int n;
                        assignedById.TryGetValue(targetId, out n);
                        assignedById[targetId] = n + 1;
                        if (weapon.HasFoundPrimaryTargetTrajectory)
                        {
                            primaryById.TryGetValue(targetId, out n);
                            primaryById[targetId] = n + 1;
                        }
                    }
                }
                // v0.12: failures count as "out of reach" only where most weapons at that target found a trajectory.
                int unreachableMarked = Plugin.Reach.ReportTurn(reachKeys, reachTargets, reachFound, turn, Plugin.ReachFailMinShare.Value);
                // v0.10: strike history follows what was actually put up for firing (0 on a hold turn).
                StrategyPatches.ReportFired(core.PlayerData.PlayerID, (hitting != null ? hitting.Count : 0) + (missing != null ? missing.Count : 0),
                    assignedById, primaryById);

                Plugin.WriteDecision(new Json()
                    .Str("e", "fire").Int("t", turn).Str("ai", core.PlayerData.UserNickName)
                    .Int("retainedEntries", weapons.Count).Int("powered", powered).Int("primaryFound", primary).Int("secondaryOnly", secondary)
                    .Int("plannedHits", __instance.Hits).Int("plannedMisses", __instance.Misses)
                    .Int("firingHits", hitting != null ? hitting.Count : -1).Int("firingMisses", missing != null ? missing.Count : -1)
                    .Map("assigned", assignedByTarget).Map("primaryByTarget", primaryByTarget).Int("unreachableMarked", unreachableMarked).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("fire-summary", e);
            }
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            int n;
            counts.TryGetValue(key, out n);
            counts[key] = n + 1;
        }

        // ---- guided strikes: which building on the target planet --------------------------------

        private static bool SecondPhasePrefix(AIWeaponTargeting __instance, WeaponGameplayData weapon, CelestialBody targetPlanet)
        {
            if (!Plugin.Active || !Plugin.SmartStrikes.Value) return true;
            PlayerData me, enemy;
            List<BuildingView> views = new List<BuildingView>();
            int pick;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                enemy = targetPlanet != null ? targetPlanet.Player : null;
                if (core == null || enemy == null) return true;
                me = core.PlayerData;

                List<BaseBuilding> buildings = new List<BaseBuilding>();
                AddCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.City), true, buildings, views);
                AddCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.Weapon), false, buildings, views);

                pick = StrikePlanner.Choose(views, Plugin.WeaponBuildingShare.Value, Plugin.Rng);
                if (pick < 0) return true; // nothing currently visible: native choice (it also aims at shadow intel)

                core.Actions.SecondPhaseTargetWeapon(weapon, targetPlanet, buildings[pick].transform.localPosition);
            }
            catch (Exception e)
            {
                ReportOnce("strike", e); // no action was queued, so the native choice runs
                return true;
            }

            // The strike is queued: never fall through to native from here (it would queue a second one).
            try
            {
                Plugin.WriteDecision(new Json()
                    .Str("e", "strike").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName).Str("target", enemy.UserNickName)
                    .Str("building", views[pick].IsCity ? "city" : "weapon").Int("durability", views[pick].CurrentHp).Int("maxDurability", views[pick].MaxHp)
                    .Int("candidates", views.Count).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("strike-log", e);
            }
            return false;
        }

        private static void AddCandidates(PlayerData me, List<BaseBuilding> source, bool isCity, List<BaseBuilding> buildings, List<BuildingView> views)
        {
            if (source == null) return;
            foreach (BaseBuilding building in source)
            {
                if (!IsVisibleIntact(me, building) || building.Stats == null) continue;
                buildings.Add(building);
                views.Add(new BuildingView
                {
                    IsCity = isCity,
                    CurrentHp = isCity ? building.Stats.CurrentPopulation : building.Stats.CurrentHP,
                    MaxHp = isCity ? building.Stats.MaxPopulation : building.Stats.MaxHP,
                });
            }
        }

        // ---- grudges: who damaged whom ----------------------------------------------------------

        private static void DealDamagePrefix(BaseBuilding building, int damage, int sourcePlayerID)
        {
            if (!Plugin.Active || building == null || sourcePlayerID < 0) return;
            try
            {
                int victimId = building.Owner != null ? building.Owner.PlayerID : building.OwnerID;
                Plugin.Grudges.Record(victimId, sourcePlayerID, GameManager.CurrentTurn, damage);
            }
            catch (Exception e)
            {
                ReportOnce("grudge", e);
            }
        }

        // ---- logging ----------------------------------------------------------------------------

        private static void LogTargeting(PlayerData me, int turn, List<EnemyView> enemies, int[] countByEnemy, int weaponCount, int rerouted)
        {
            StringBuilder line = new StringBuilder();
            line.Append("T").Append(turn).Append(' ').Append(me.UserNickName).Append(": ").Append(weaponCount).Append(" weapons ->");
            Json json = new Json().Str("e", "target").Int("t", turn).Str("ai", me.UserNickName).Int("lvl", me.AILevel)
                .Int("weapons", weaponCount).Int("rerouted", rerouted);
            StringBuilder enemyArray = new StringBuilder("[");
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyView e = enemies[i];
                line.Append(' ').Append(e.Name).Append(" x").Append(countByEnemy[i])
                    .Append(" (").Append(e.Score.ToString("0.00", CultureInfo.InvariantCulture)).Append(')');
                if (i > 0) enemyArray.Append(',');
                enemyArray.Append(new Json().Str("name", e.Name).Int("id", e.PlayerId).Bool("human", e.IsHuman).Int("assigned", countByEnemy[i])
                    .Num("score", e.Score).Bool("known", e.EconomyKnown).Num("economy", e.Economy).Num("cityPop", e.CityPopulation).Int("cities", e.VisibleCities)
                    .Num("grudge", e.Grudge).Num("leader", e.LeaderTerm).Num("grudgeTerm", e.GrudgeTerm).Num("finish", e.FinishTerm).ToString());
            }
            enemyArray.Append(']');
            Plugin.Log.LogInfo(line.ToString());
            Plugin.WriteDecision(json.Raw("enemies", enemyArray.ToString()).ToString());
        }

        private static void ReportOnce(string hook, Exception e)
        {
            if (s_reportedErrors.Add(hook))
            {
                Plugin.Log.LogError("Hook '" + hook + "' failed; native behaviour used for it (reported once): " + e);
            }
        }
    }

    // Minimal JSON object writer; net35 has no serializer we can rely on.
    internal sealed class Json
    {
        private readonly StringBuilder m_sb = new StringBuilder("{");
        private bool m_first = true;

        public Json Str(string key, string value) { return Key(key).Quote(value); }
        public Json Int(string key, int value) { Key(key); m_sb.Append(value.ToString(CultureInfo.InvariantCulture)); return this; }
        public Json Num(string key, float value) { Key(key); m_sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture)); return this; }
        public Json Bool(string key, bool value) { Key(key); m_sb.Append(value ? "true" : "false"); return this; }
        public Json Raw(string key, string rawJson) { Key(key); m_sb.Append(rawJson); return this; }

        public Json Map(string key, Dictionary<string, int> values)
        {
            Json inner = new Json();
            foreach (KeyValuePair<string, int> pair in values) inner.Int(pair.Key, pair.Value);
            return Raw(key, inner.ToString());
        }

        public override string ToString() { return m_sb.ToString() + "}"; }

        private Json Key(string key)
        {
            if (!m_first) m_sb.Append(',');
            m_first = false;
            Quote(key);
            m_sb.Append(':');
            return this;
        }

        private Json Quote(string value)
        {
            m_sb.Append('"');
            foreach (char c in value ?? "")
            {
                if (c == '"' || c == '\\') m_sb.Append('\\').Append(c);
                else if (c < ' ') m_sb.Append(' ');
                else m_sb.Append(c);
            }
            m_sb.Append('"');
            return this;
        }
    }
}
