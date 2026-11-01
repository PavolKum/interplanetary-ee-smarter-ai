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
                    .Bool("shadows", GameSessionManager.CurrentGameSettings != null && GameSessionManager.CurrentGameSettings.SeenBuildingsShadows) // Sol v0.15.2 #7
                    .Num("nativeAcc", __instance.Difficulty != null ? __instance.Difficulty.Accuracy : -1f).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("init", e);
            }
        }

        // ---- who to shoot ----------------------------------------------------------------------

        // v0.14: the trajectory search budget. AIWeaponTargeting.BeginTurn gives every AI weapon clamp(1200 / all AI weapons,
        // 30, 100) search steps per frame (1200 = AICore's processor resources over all AIs). v0.13.1 1v1: 11-17 weapons
        // (100 steps each) aimed 11/11 and 17/17; 26 weapons (46 each) aimed 6/26 and 9/26 on the two strike turns.
        // AiProcessorResources replaces the 1200; the per-weapon clamp stays. 0 = the game's budget.
        private static readonly FieldInfo s_trajectoryCalculator = AccessTools.Field(typeof(AIWeaponTargeting), "m_trajectoryCalculator");

        private static void ApplyProcessorBudget(AIWeaponTargeting targeting)
        {
            int total = Plugin.AiProcessorResources.Value;
            if (total <= 0) return;
            try
            {
                int ais = Math.Max(1, PlayerManager.GetAiPlayerAmount());
                int weapons = PlayerManager.GetTotalAIWeaponAmount();
                targeting.ProcessorResourcesThisFrame = Math.Max(1, total / ais);
                int perWeapon = AimBudget.StepsPerWeapon(total, weapons);
                Interplanetary.Weapons.Trajectories.TrajectoryCalculator calc =
                    s_trajectoryCalculator != null ? s_trajectoryCalculator.GetValue(targeting) as Interplanetary.Weapons.Trajectories.TrajectoryCalculator : null;
                if (calc != null) calc.ReallocateDesignatedStepAmount(perWeapon);
                // Sol v0.14 #6: log the denominator and the allowance actually given (once per AI turn, when weapons exist).
                AICore core = (AICore)s_moduleCore.GetValue(targeting);
                if (weapons > 0 && core != null && core.PlayerData.AIPlayer)
                {
                    Plugin.WriteDecision(new Json().Str("e", "aim-budget").Int("t", GameManager.CurrentTurn).Str("ai", core.PlayerData.UserNickName)
                        .Int("total", total).Int("aiWeapons", weapons).Int("steps", perWeapon).Int("nativeSteps", AimBudget.StepsPerWeapon(1200, weapons))
                        .Bool("applied", calc != null).ToString());
                }
            }
            catch (Exception e)
            {
                ReportOnce("processor-budget", e);
            }
        }

        private static void TargetingBeginTurnPostfix(AIWeaponTargeting __instance)
        {
            if (Plugin.Active) ApplyProcessorBudget(__instance);
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

        // v0.15: guided = has second-phase targeting (lands where placed: Missile, Nuclear Missile). MIRV has no second
        // phase in this engine (Sol v0.15), so it keeps the level rate. One classifier for firing and strategy (Sol v0.15 #4).
        internal static bool IsGuided(WeaponGameplayData w)
        {
            return w != null && w.TargetingModule != null && w.TargetingModule.HasTargetingPhase(WeaponTargetingPhase.SecondPhaseTargeting);
        }

        // v0.15.3 (Sol v0.15.2 #1): the player's current weapon registry (destroyed weapons leave it; PlayerWeaponHandler).
        internal static HashSet<WeaponGameplayData> RegisteredWeapons(PlayerData me)
        {
            HashSet<WeaponGameplayData> set = new HashSet<WeaponGameplayData>();
            if (me != null && me.WeaponHandler != null && me.WeaponHandler.Weapons != null)
            {
                foreach (WeaponGameplayData w in me.WeaponHandler.Weapons) if (w != null) set.Add(w);
            }
            return set;
        }

        // Registered weapon whose building is ours, Normal and powered.
        internal static bool IsOperational(PlayerData me, WeaponGameplayData w)
        {
            return w != null && w.Building != null && w.Building.OwnerID == me.PlayerID && w.Building.CurrentState == BaseBuilding.BuildingState.Normal
                && w.Building.Power.HasPower;
        }

        // v0.15.1 (Sol v0.15 #7): a new match or a loaded save must not reuse last match's volley reservations.
        internal static void ResetCombatMemory()
        {
            s_volley.Clear();
            s_classQuota.Clear();
            s_quotaLogged.Clear();
        }

        // ---- how many shots are planned to hit -------------------------------------------------

        private static void GoalBeginTurnPostfix(AIWeaponTargetingGoal __instance, List<AITargetableWeapon> weapons)
        {
            if (!Plugin.Active || !Plugin.AccuracyOverride.Value) return;
            try
            {
                int total = __instance.Hits + __instance.Misses;
                if (total == 0) return;
                AICore core = (AICore)s_goalCore.GetValue(__instance);
                PlayerData me = core.PlayerData;
                if (!me.AIPlayer) return;

                float levelAccuracy = AccuracyPlan.ForLevel(Plugin.Accuracy, me.AILevel);
                // v0.15: live weapons only (the targeting list keeps destroyed entries, Sol + Fable v0.14.2 audits), guided
                // ones at GuidedAccuracy (Pavol's measured Missile rate), unguided at the level rate.
                // v0.15.1 (Sol v0.15 #1): counted from the BeginTurn argument, whose Weapon is set in its constructor. The
                // trajectory weapon's TargetableWeapon back-reference is only set after this postfix (AIWeaponTargeting.cs:
                // 110-122), so v0.15.0 skipped every newly built weapon and fell back to the level rate.
                if (weapons == null) return;
                // v0.15.3 (Sol v0.15.2 #1): only weapons in the player's registry count. The targetable list keeps destroyed
                // weapons, and a pooled building component can come back Normal and powered under a stale weapon object
                // (v0.15.1 T32: 27 registered, 33 counted). Stale entries also leave the goal's list before hit selection,
                // so they cannot take hit slots or be fired (native completion does not check the registry).
                HashSet<WeaponGameplayData> registered = RegisteredWeapons(me);
                List<AITrajectoryWeapon> goalList = s_goalWeapons != null ? s_goalWeapons.GetValue(__instance) as List<AITrajectoryWeapon> : null;
                int guided = 0, unguided = 0, stale = 0;
                foreach (AITargetableWeapon tw in weapons)
                {
                    WeaponGameplayData w = tw != null ? tw.Weapon : null;
                    if (w == null || !registered.Contains(w))
                    {
                        if (tw != null && goalList != null && tw.TrajectoryWeapon != null && goalList.Remove(tw.TrajectoryWeapon)) stale++;
                        continue;
                    }
                    if (!IsOperational(me, w)) continue;
                    if (IsGuided(w)) guided++;
                    else unguided++;
                }
                int live = guided + unguided;
                if (live <= 0) return; // nothing operational: leave the native counts alone
                float accuracy = Plugin.GuidedAccuracy.Value > 0f
                    ? AccuracyPlan.MixedRate(guided, Plugin.GuidedAccuracy.Value, unguided, levelAccuracy) : levelAccuracy;
                int nativeHits = __instance.Hits;
                int slots = Math.Min(total, live);
                // v0.16 item 4 (council: per-class quotas): planned hits per class, each with its own carry, so the 0.85
                // guided rate goes to guided weapons. v0.15 set one mixed count and native took the first weapons with a
                // trajectory, whichever class (T22's 0.827 was the 11/2 mix, not who hit). DetermineShootingPrefix puts
                // the quota weapons first.
                float guidedRate = Plugin.GuidedAccuracy.Value > 0f ? Plugin.GuidedAccuracy.Value : levelAccuracy;
                float carry, guidedCarry;
                Plugin.AccuracyCarry.TryGetValue(me.PlayerID, out carry);
                Plugin.GuidedAccuracyCarry.TryGetValue(me.PlayerID, out guidedCarry);
                int guidedHits = AccuracyPlan.PlannedHits(guided, guidedRate, ref guidedCarry);
                int unguidedHits = AccuracyPlan.PlannedHits(unguided, levelAccuracy, ref carry);
                Plugin.AccuracyCarry[me.PlayerID] = carry;
                Plugin.GuidedAccuracyCarry[me.PlayerID] = guidedCarry;
                int hits = Math.Min(slots, guidedHits + unguidedHits);
                s_classQuota[__instance] = new[] { guidedHits, unguidedHits, GameManager.CurrentTurn };

                __instance.Hits = hits;
                __instance.Misses = slots - hits; // dead entries cannot fill a miss slot (Sol v0.15 #1)

                Plugin.WriteDecision(new Json()
                    .Str("e", "accuracy").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName).Int("lvl", me.AILevel)
                    .Num("acc", accuracy).Int("shots", total).Int("live", live).Int("guided", guided).Int("stale", stale).Int("nativeHits", nativeHits).Int("hits", hits)
                    .Int("guidedQuota", guidedHits).Int("unguidedQuota", unguidedHits).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("accuracy", e);
            }
        }

        // v0.16 item 4: goal -> {guided hits, unguided hits, turn} planned in GoalBeginTurnPostfix.
        private static readonly Dictionary<AIWeaponTargetingGoal, int[]> s_classQuota = new Dictionary<AIWeaponTargetingGoal, int[]>();

        // v0.16 item 4: native DetermineShootingWeapons gives the Hits slots to the first weapons in its list that found a
        // primary trajectory. Put up to the guided quota of guided weapons and the unguided quota of unguided weapons with a
        // primary trajectory first; native then fills any shortfall from the rest, as before.
        private static void DetermineShootingPrefix(AIWeaponTargetingGoal __instance)
        {
            if (!Plugin.Active || !Plugin.AccuracyOverride.Value) return;
            try
            {
                int[] quota;
                if (!s_classQuota.TryGetValue(__instance, out quota) || quota[2] != GameManager.CurrentTurn) return;
                List<AITrajectoryWeapon> list = s_goalWeapons != null ? s_goalWeapons.GetValue(__instance) as List<AITrajectoryWeapon> : null;
                if (list == null || list.Count == 0) return;
                List<AITrajectoryWeapon> front = new List<AITrajectoryWeapon>(), rest = new List<AITrajectoryWeapon>();
                int g = 0, u = 0;
                foreach (AITrajectoryWeapon w in list)
                {
                    bool isGuided = w != null && w.TargetableWeapon != null && IsGuided(w.TargetableWeapon.Weapon);
                    bool ready = w != null && w.HasFoundPrimaryTargetTrajectory;
                    if (ready && isGuided && g < quota[0]) { front.Add(w); g++; }
                    else if (ready && !isGuided && u < quota[1]) { front.Add(w); u++; }
                    else rest.Add(w);
                }
                list.Clear();
                list.AddRange(front);
                list.AddRange(rest);
                AICore core = (AICore)s_goalCore.GetValue(__instance);
                if (core != null && core.PlayerData != null && s_quotaLogged.Add(core.PlayerData.PlayerID + ":" + GameManager.CurrentTurn))
                {
                    Plugin.WriteDecision(new Json().Str("e", "hit-quota").Int("t", GameManager.CurrentTurn).Str("ai", core.PlayerData.UserNickName)
                        .Int("guidedQuota", quota[0]).Int("unguidedQuota", quota[1]).Int("guidedReady", g).Int("unguidedReady", u)
                        .Int("hits", __instance.Hits).ToString());
                }
            }
            catch (Exception e)
            {
                ReportOnce("hit-quota", e);
            }
        }

        private static readonly HashSet<string> s_quotaLogged = new HashSet<string>();

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
                HashSet<WeaponGameplayData> registered = RegisteredWeapons(core.PlayerData); // v0.15.3 (Sol v0.15.2 #1)
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
                    if (data == null || !registered.Contains(data) || !IsOperational(core.PlayerData, data)) continue;
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

        // v0.15 (Sol + Fable v0.14.2 audits): candidates are enemy cities we see now (live population) or saw before
        // (Shadow: the frozen observation, never hidden live values; cities never move), plus visible weapon buildings.
        // A per-volley ledger spreads the shots (VolleyPlanner). With no candidate the native choice runs; it also falls
        // back to a random point on the planet when it knows nothing (AIWeaponTargeting.cs:483-497), so that is logged.
        private static readonly Dictionary<int, KeyValuePair<int, Dictionary<string, float>>> s_volley = new Dictionary<int, KeyValuePair<int, Dictionary<string, float>>>();

        private static Dictionary<string, float> VolleyLedger(int playerId, int turn)
        {
            KeyValuePair<int, Dictionary<string, float>> entry;
            if (!s_volley.TryGetValue(playerId, out entry) || entry.Key != turn)
            {
                entry = new KeyValuePair<int, Dictionary<string, float>>(turn, new Dictionary<string, float>());
                s_volley[playerId] = entry;
            }
            return entry.Value;
        }

        private static bool SecondPhasePrefix(AIWeaponTargeting __instance, WeaponGameplayData weapon, CelestialBody targetPlanet)
        {
            if (!Plugin.Active || !Plugin.SmartStrikes.Value) return true;
            PlayerData me = null, enemy = null;
            List<BaseBuilding> buildings = new List<BaseBuilding>();
            List<StrikeCandidate> candidates = new List<StrikeCandidate>();
            Dictionary<string, float> ledger = null;
            int pick = -1;
            float damage = 0f;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                enemy = targetPlanet != null ? targetPlanet.Player : null;
                if (core == null || enemy == null) return true;
                me = core.PlayerData;
                ledger = VolleyLedger(me.PlayerID, GameManager.CurrentTurn);
                AddStrikeCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.City), true, ledger, buildings, candidates);
                AddStrikeCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.Weapon), false, ledger, buildings, candidates);
                bool preferWeapon = Plugin.Rng.NextDouble() < Plugin.WeaponBuildingShare.Value;
                pick = VolleyPlanner.Pick(candidates, Plugin.VolleyOverkill.Value, preferWeapon);
                if (pick < 0)
                {
                    LogStrike(me, enemy, weapon, null, null, 0f, 0, "native-no-candidates");
                    return true;
                }
                damage = candidates[pick].IsCity ? weapon.DamageModule.PopulationDamage : weapon.DamageModule.BuildingDamage;
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
                string key = buildings[pick].ObjectId.ToString();
                float before;
                ledger.TryGetValue(key, out before);
                ledger[key] = before + damage;
                LogStrike(me, enemy, weapon, buildings[pick], candidates[pick], damage, candidates.Count, "");
            }
            catch (Exception e)
            {
                ReportOnce("strike-log", e);
            }
            return false;
        }

        private static void AddStrikeCandidates(PlayerData me, List<BaseBuilding> source, bool isCity, Dictionary<string, float> ledger,
            List<BaseBuilding> buildings, List<StrikeCandidate> candidates)
        {
            if (source == null) return;
            foreach (BaseBuilding building in source)
            {
                if (building == null) continue;
                BuildingIntelVisibility vis = me.IntelHandler.GetBuildingVisibility(building);
                float durability;
                bool shadow = false;
                if (vis == BuildingIntelVisibility.Visible)
                {
                    if (building.CurrentState != BaseBuilding.BuildingState.Normal || building.Stats == null) continue;
                    durability = isCity ? building.Stats.CurrentPopulation : building.Stats.CurrentHP;
                }
                else if (vis == BuildingIntelVisibility.Shadow && isCity)
                {
                    ShadowBuildingFrozenState frozen = me.IntelHandler.IntelMemory != null ? me.IntelHandler.IntelMemory.GetShadowBuildingState(building) : null;
                    if (frozen == null || frozen.BuildingState != BaseBuilding.BuildingState.Normal) continue;
                    durability = frozen.HP;
                    shadow = true;
                }
                else continue;
                float allocated;
                ledger.TryGetValue(building.ObjectId.ToString(), out allocated);
                buildings.Add(building);
                candidates.Add(new StrikeCandidate { IsCity = isCity, Shadow = shadow, Durability = durability, Allocated = allocated });
            }
        }

        private static void LogStrike(PlayerData me, PlayerData enemy, WeaponGameplayData weapon, BaseBuilding target, StrikeCandidate c, float damage, int count, string fallback)
        {
            try
            {
                Json j = new Json().Str("e", "strike").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName).Str("target", enemy.UserNickName)
                    .Int("candidates", count)
                    .Str("weapon", weapon != null ? WeaponKey(weapon) : "").Str("wid", weapon != null && weapon.WeaponData != null ? weapon.WeaponData.id.Value : ""); // Sol v0.15 #10
                if (target != null && c != null)
                {
                    j.Str("building", c.IsCity ? "city" : "weapon").Str("id", target.ObjectId.ToString()).Str("seen", c.Shadow ? "shadow" : "visible")
                        .Int("durability", (int)c.Durability).Int("allocatedBefore", (int)c.Allocated).Int("damage", (int)damage);
                }
                else
                {
                    j.Str("building", "none").Str("fallback", fallback);
                }
                Plugin.WriteDecision(j.ToString());
            }
            catch (Exception e)
            {
                ReportOnce("strike-log", e);
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
