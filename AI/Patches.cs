using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Trajectories;
using Interplanetary.GameEngine;
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

        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();

        // ---- match lifecycle -------------------------------------------------------------------

        private static void CoreInitializePostfix(AICore __instance)
        {
            Plugin.ResetMatchMemory();
            try
            {
                PlayerData me = __instance.PlayerData;
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
                List<AITargetableWeapon> weapons = (List<AITargetableWeapon>)s_targetableWeapons.GetValue(__instance);
                if (core == null || weapons == null || weapons.Count == 0) return;
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

                int[] plan = TargetPlanner.Assign(enemies, slots, Plugin.Planner, Plugin.Rng);
                int[] countByEnemy = new int[enemies.Count];
                int rerouted = 0;
                for (int i = 0; i < plan.Length; i++)
                {
                    if (plan[i] < 0) continue;
                    gravityWeapons[i].TargetEnemy = planets[plan[i]];
                    countByEnemy[plan[i]]++;
                    if (slots[i].UnreachableTargetIds.Count > 0) rerouted++;
                }

                LogTargeting(me, turn, enemies, countByEnemy, slots.Count, rerouted);
            }
            catch (Exception e)
            {
                ReportOnce("targeting", e);
            }
        }

        private static EnemyView BuildEnemyView(PlayerData me, PlayerData enemy, int turn)
        {
            EnemyView view = new EnemyView
            {
                PlayerId = enemy.PlayerID,
                Name = enemy.UserNickName,
                IsHuman = !enemy.AIPlayer,
            };
            foreach (BaseBuilding building in enemy.BuildingHandler.Buildings)
            {
                if (!IsVisibleIntact(me, building)) continue;
                BuildingStatHolder stats = building.Stats;
                if (stats == null) continue;
                view.Economy += Math.Max(0, stats.EnergyGeneration) + Math.Max(0, stats.MaterialGeneration) + Math.Max(0, stats.ScienceGeneration);
                if (building.IsCity)
                {
                    view.CityHp += Math.Max(0, stats.CurrentHP);
                    view.VisibleCities++;
                }
            }
            view.Grudge = Plugin.Grudges.Get(me.PlayerID, enemy.PlayerID, turn);
            return view;
        }

        // Same filter the native AI uses for its target lists: intact, and not hidden from our intel.
        private static bool IsVisibleIntact(PlayerData me, BaseBuilding building)
        {
            if (building == null || building.CurrentState != BaseBuilding.BuildingState.Normal) return false;
            return me.IntelHandler.GetBuildingVisibility(building) != BuildingIntelVisibility.Invisible;
        }

        private static string WeaponKey(WeaponGameplayData weapon)
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

        private static void GoalTargetWeaponsPrefix(AIWeaponTargetingGoal __instance, out bool __state)
        {
            __state = !__instance.AreWeaponsTargeted;
        }

        private static void GoalTargetWeaponsPostfix(AIWeaponTargetingGoal __instance, bool __state)
        {
            if (!__state || !Plugin.Enabled.Value) return;
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
                foreach (AITrajectoryWeapon weapon in weapons)
                {
                    WeaponGameplayData data = weapon.TargetableWeapon != null ? weapon.TargetableWeapon.Weapon : null;
                    if (data == null || data.Building == null || !data.Building.Power.HasPower) continue;
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
                        Plugin.Reach.Report(WeaponKey(data), weapon.TargetEnemy.Player.PlayerID, turn, weapon.HasFoundPrimaryTargetTrajectory);
                    }
                }

                Plugin.WriteDecision(new Json()
                    .Str("e", "fire").Int("t", turn).Str("ai", core.PlayerData.UserNickName)
                    .Int("weapons", weapons.Count).Int("powered", powered).Int("primaryFound", primary).Int("secondaryOnly", secondary)
                    .Int("plannedHits", __instance.Hits).Int("plannedMisses", __instance.Misses)
                    .Int("firingHits", hitting != null ? hitting.Count : -1).Int("firingMisses", missing != null ? missing.Count : -1)
                    .Map("assigned", assignedByTarget).Map("primaryByTarget", primaryByTarget).ToString());
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
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                PlayerData enemy = targetPlanet != null ? targetPlanet.Player : null;
                if (core == null || enemy == null) return true;
                PlayerData me = core.PlayerData;

                List<BaseBuilding> buildings = new List<BaseBuilding>();
                List<BuildingView> views = new List<BuildingView>();
                AddCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.City), true, buildings, views);
                AddCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.Weapon), false, buildings, views);

                int pick = StrikePlanner.Choose(views, Plugin.WeaponBuildingShare.Value, Plugin.Rng);
                if (pick < 0) return true; // nothing visible: native random surface point

                core.Actions.SecondPhaseTargetWeapon(weapon, targetPlanet, buildings[pick].transform.localPosition);
                Plugin.WriteDecision(new Json()
                    .Str("e", "strike").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName).Str("target", enemy.UserNickName)
                    .Str("building", views[pick].IsCity ? "city" : "weapon").Int("hp", views[pick].CurrentHp).Int("maxHp", views[pick].MaxHp)
                    .Int("candidates", views.Count).ToString());
                return false;
            }
            catch (Exception e)
            {
                ReportOnce("strike", e);
                return true;
            }
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
                    CurrentHp = building.Stats.CurrentHP,
                    MaxHp = building.Stats.MaxHP,
                    Population = isCity ? building.Stats.CurrentPopulation : 0,
                });
            }
        }

        // ---- grudges: who damaged whom ----------------------------------------------------------

        private static void DealDamagePrefix(BaseBuilding building, int damage, int sourcePlayerID)
        {
            if (!Plugin.Enabled.Value || building == null || sourcePlayerID < 0) return;
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
                    .Num("score", e.Score).Num("economy", e.Economy).Num("cityHp", e.CityHp).Int("cities", e.VisibleCities)
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
