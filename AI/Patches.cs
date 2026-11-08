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
using Interplanetary.Weapons.Trajectories;
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
                if (Plugin.IsSmart(me)) StrategyPatches.SetAimWindow(__instance, Plugin.AimingWindowSeconds.Value);
                Plugin.WriteDecision(new Json()
                    .Str("e", "init").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName).Int("id", me.PlayerID).Int("lvl", me.AILevel)
                    .Bool("active", Plugin.Active).Bool("smart", Plugin.IsSmart(me)).Num("acc", AccuracyPlan.ForLevel(Plugin.Accuracy, me.AILevel)) // v0.17.1: smart false = the original AI (acc unused)
                    .Bool("shadows", GameSessionManager.CurrentGameSettings != null && GameSessionManager.CurrentGameSettings.SeenBuildingsShadows) // Sol v0.15.2 #7
                    .Num("nativeAcc", __instance.Difficulty != null ? __instance.Difficulty.Accuracy : -1f)
                    .Str("build", Plugin.BuildId).Raw("config", Plugin.NonDefaultConfigJson()).ToString()); // v0.20 (council I1a)
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

        // v0.17.1: does this targeting module / goal belong to a Smarter AI (Plugin.IsSmart)?
        private static bool SmartModule(AIWeaponTargeting targeting)
        {
            try
            {
                AICore core = targeting != null ? (AICore)s_moduleCore.GetValue(targeting) : null;
                return core != null && Plugin.IsSmart(core.PlayerData);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool SmartGoal(AIWeaponTargetingGoal goal)
        {
            try
            {
                AICore core = goal != null ? (AICore)s_goalCore.GetValue(goal) : null;
                return core != null && Plugin.IsSmart(core.PlayerData);
            }
            catch (Exception)
            {
                return false;
            }
        }

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
                if (weapons > 0 && core != null && Plugin.IsSmart(core.PlayerData))
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
            if (!SmartModule(__instance)) return; // v0.17.1: an original AI's targeting stays native
            ApplyProcessorBudget(__instance);
            if (!Plugin.Active || !Plugin.SmartTargeting.Value) return;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                if (core == null || !Plugin.IsSmart(core.PlayerData)) return;
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
                if (core == null || !Plugin.IsSmart(core.PlayerData) || weapons == null || weapons.Count == 0) return;
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
                }
            }
            // v0.16 (Sol v0.16 F2, Pavol's P2): cities we see NOW, not the surviving-city total the overlay shows behind the
            // population gate (v0.15 read GetRemainingCities().Count there: 5 at T22 while 2 were targetable).
            int seenNow = 0;
            foreach (BaseBuilding city in enemy.BuildingHandler.GetRemainingCities()) if (IsVisibleIntact(me, city)) seenNow++;
            view.VisibleCities = seenNow;
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
                if (!Plugin.IsSmart(me)) return; // v0.17.1: an original AI keeps its native aim

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
                // trajectory, whichever class (T22's 0.827 was the 11/2 mix, not who hit). EnforceClassQuota (at firing) holds
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

        private static bool IsGuidedTrajectory(AITrajectoryWeapon w)
        {
            return w != null && w.TargetableWeapon != null && IsGuided(w.TargetableWeapon.Weapon);
        }

        // v0.16 (Sol v0.16 F1/F7): strike candidates a precision weapon could take on this enemy NOW: intact cities and
        // weapon buildings in the Visible state. Shadow records do not count (decision-record-r2 1.10).
        internal static int StrikeCandidateCount(PlayerData me, PlayerData enemy)
        {
            if (me == null || enemy == null) return 0;
            int n = 0;
            foreach (BaseBuilding b in enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.City)) if (IsVisibleIntact(me, b)) n++;
            foreach (BaseBuilding b in enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.Weapon)) if (IsVisibleIntact(me, b)) n++;
            return n;
        }

        // v0.16 (Sol v0.16 re-check #3): the planet the selected trajectory actually hits, not the assigned enemy. A hit uses
        // the primary trajectory's target or the stored secondary trajectory's planet; a planned miss uses its miss
        // trajectory's planet (null = misses every planet).
        private static CelestialBody Destination(AITrajectoryWeapon w, bool asMiss)
        {
            if (w == null) return null;
            if (asMiss)
            {
                AIStoredTrajectory miss = w.MissTrajectory;
                return miss != null && miss.TrajectoryResults != null ? miss.TrajectoryResults.HitCelestialBody : null;
            }
            if (w.HasFoundPrimaryTargetTrajectory) return w.TargetEnemy;
            AIStoredTrajectory hit = w.HitTrajectory;
            return hit != null && hit.TrajectoryResults != null ? hit.TrajectoryResults.HitCelestialBody : null;
        }

        // Visible strike candidates at the weapon's destination: -1 = not an enemy planet (or unresolved).
        private static int CandidatesFor(PlayerData me, AITrajectoryWeapon w, Dictionary<int, int> cache, bool asMiss = false)
        {
            CelestialBody dest = Destination(w, asMiss);
            if (dest == null || dest.Player == null || dest.Player == me) return -1;
            int id = dest.Player.PlayerID, n;
            if (!cache.TryGetValue(id, out n))
            {
                n = StrikeCandidateCount(me, dest.Player);
                cache[id] = n;
            }
            return n;
        }

        // v0.16 (Sol v0.16 F7): a guided weapon whose target enemy offers no visible strike candidate would land on a random
        // point (match 16: 45 such strikes). It is withheld; its firing energy stays unspent for defense rolls.
        private static int WithholdBlindPrecision(PlayerData me, List<AITrajectoryWeapon> hitting, List<AITrajectoryWeapon> missing,
            Dictionary<int, int> cache)
        {
            int withheld = 0;
            // A hit with an unresolved destination is withheld (fail closed); a miss only when it would land on a blind enemy.
            if (hitting != null) withheld += hitting.RemoveAll(w => IsGuidedTrajectory(w) && CandidatesFor(me, w, cache) <= 0);
            if (missing != null) withheld += missing.RemoveAll(w => IsGuidedTrajectory(w) && CandidatesFor(me, w, cache, true) == 0);
            return withheld;
        }

        // v0.16 (Sol v0.16 F8): enforce the per-class planned hits on the final firing lists. Native selection fills the
        // combined Hits count from either class; here a class above its quota moves its extra hits to (verified) misses,
        // and a class below it is filled only from its own weapons with a trajectory.
        private static void EnforceClassQuota(int[] quota, List<AITrajectoryWeapon> all, List<AITrajectoryWeapon> hitting,
            List<AITrajectoryWeapon> missing, CelestialBody ownPlanet, HashSet<AITrajectoryWeapon> excluded)
        {
            if (quota == null || hitting == null || all == null) return;
            for (int c = 0; c < 2; c++)
            {
                bool guidedClass = c == 0;
                List<AITrajectoryWeapon> classHits = hitting.FindAll(w => IsGuidedTrajectory(w) == guidedClass);
                for (int i = classHits.Count - 1; i >= quota[c] && i >= 0; i--)
                {
                    AITrajectoryWeapon extra = classHits[i];
                    hitting.Remove(extra);
                    if (missing != null && Plugin.FireMisses.Value && !missing.Contains(extra) && IsSafeNearMiss(extra, ownPlanet)) missing.Add(extra);
                }
                int need = quota[c] - Math.Min(classHits.Count, quota[c]);
                for (int pass = 0; pass < 2 && need > 0; pass++)
                {
                    foreach (AITrajectoryWeapon w in all)
                    {
                        if (need <= 0) break;
                        if (w == null || IsGuidedTrajectory(w) != guidedClass || hitting.Contains(w) || (excluded != null && excluded.Contains(w))) continue;
                        bool ok = pass == 0 ? w.HasFoundPrimaryTargetTrajectory : w.HasFoundSecondaryTargetTrajectory;
                        if (!ok) continue;
                        if (missing != null) missing.Remove(w);
                        hitting.Add(w);
                        need--;
                    }
                }
            }
        }

        // v0.16 (Sol v0.16 F7/F8 logging): what is committed to fire this turn, by class and target, with live firing cost.
        // Written at the firing point after withholding and quotas, before the abort check (an abort writes its own record).
        private static void LogFiring(PlayerData me, int[] quota, List<AITrajectoryWeapon> hitting, List<AITrajectoryWeapon> missing,
            int withheld, Dictionary<int, int> cache)
        {
            try
            {
                int gh = 0, uh = 0, gm = 0, um = 0, gE = 0, uE = 0;
                Dictionary<string, int> firingByTarget = new Dictionary<string, int>(), candidatesByTarget = new Dictionary<string, int>();
                List<AITrajectoryWeapon> fired = new List<AITrajectoryWeapon>();
                if (hitting != null) fired.AddRange(hitting);
                if (missing != null) fired.AddRange(missing);
                foreach (AITrajectoryWeapon w in fired)
                {
                    bool g = IsGuidedTrajectory(w);
                    bool hit = hitting != null && hitting.Contains(w);
                    if (g) { if (hit) gh++; else gm++; } else { if (hit) uh++; else um++; }
                    WeaponGameplayData wd = w.TargetableWeapon != null ? w.TargetableWeapon.Weapon : null;
                    int cost = wd != null && wd.Building != null && wd.Building.Stats != null && wd.Building.Stats.Weapons != null ? wd.Building.Stats.Weapons.EnergyFiringCost : 0;
                    if (g) gE += cost; else uE += cost;
                    CelestialBody dest = Destination(w, !hit);
                    string name = dest != null && dest.Player != null ? dest.Player.UserNickName : "none";
                    int sum;
                    firingByTarget.TryGetValue(name, out sum);
                    firingByTarget[name] = sum + cost;
                    candidatesByTarget[name] = CandidatesFor(me, w, cache, !hit);
                }
                Plugin.WriteDecision(new Json().Str("e", "fire-plan").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName)
                    .Int("guidedQuota", quota != null ? quota[0] : -1).Int("unguidedQuota", quota != null ? quota[1] : -1)
                    .Int("guidedHits", gh).Int("unguidedHits", uh).Int("guidedMisses", gm).Int("unguidedMisses", um)
                    .Int("guidedFiringE", gE).Int("unguidedFiringE", uE).Int("withheldBlindPrecision", withheld)
                    .Map("firingEBy", firingByTarget).Map("candidatesBy", candidatesByTarget).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("fire-log", e);
            }
        }

        // v0.16 (Sol v0.16 re-check #3/#4): the payment boundary. PlayerWeaponHandler.CompleteFirstPhaseTargeting checks the
        // firing cost, marks the weapon targeted and spends. For an AI guided weapon whose completed trajectory lands on an
        // enemy planet with no visible strike candidate, it is refused exactly as native refuses an unaffordable shot
        // (IsFirstPhaseTargeted = false, nothing spent). Every successful AI payment is logged ("fire-paid").
        private static readonly FieldInfo s_handlerPlayer = AccessTools.Field(typeof(PlayerWeaponHandler), "m_playerData");
        private static readonly FieldInfo s_handlerResults = AccessTools.Field(typeof(PlayerWeaponHandler), "m_targetingResults");

        private static bool CompleteFirstPhasePrefix(PlayerWeaponHandler __instance, WeaponGameplayData weapon, TrajectoryCalculationResults trajectory,
            ref PlayerWeaponTargetingResult __result, out int __state)
        {
            __state = -1;
            if (!Plugin.Active) return true;
            PlayerData me = null;
            string reason = null, destName = "";
            // v0.16 (Sol re-check round 3): applicability first, outside the fail-closed region. Any error here allows the
            // shot (native behaviour); only an established AI guided shot can be refused on a guard error.
            try
            {
                me = s_handlerPlayer != null ? s_handlerPlayer.GetValue(__instance) as PlayerData : null;
                if (me == null || !Plugin.IsSmart(me)) return true; // the human's (or an original AI's) handler: untouched
                if (!Plugin.BlindArtillery.Value || !IsGuided(weapon)) { __state = SafeEnergy(me); return true; }
            }
            catch (Exception e)
            {
                ReportOnce("fire-guard-scope", e);
                return true;
            }
            __state = SafeEnergy(me); // the paid-ledger snapshot cannot trigger a refusal
            try
            {
                CelestialBody dest = trajectory != null ? trajectory.HitCelestialBody : null;
                if (dest == null || dest.Player == null || dest.Player == me) return true; // not an enemy planet: no second phase
                destName = dest.Player.UserNickName;
                if (StrikeCandidateCount(me, dest.Player) > 0) return true;
                reason = "no-visible-candidate";
            }
            catch (Exception e)
            {
                ReportOnce("fire-guard", e);
                reason = "guard-error"; // an established AI guided shot we could not check: fail closed
            }
            // Refusal is authoritative once decided: native's own refusal state (no payment), whatever the bookkeeping does.
            __state = -2;
            try
            {
                Dictionary<WeaponGameplayData, PlayerWeaponTargetingResult> results =
                    s_handlerResults != null ? s_handlerResults.GetValue(__instance) as Dictionary<WeaponGameplayData, PlayerWeaponTargetingResult> : null;
                PlayerWeaponTargetingResult r = null;
                if (results != null && !results.TryGetValue(weapon, out r)) { r = new PlayerWeaponTargetingResult(weapon); results.Add(weapon, r); }
                if (r != null) r.IsFirstPhaseTargeted = false;
                __result = r;
            }
            catch (Exception e)
            {
                ReportOnce("fire-refuse", e);
            }
            try
            {
                Plugin.WriteDecision(new Json().Str("e", "fire-refused").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName)
                    .Str("wid", weapon != null && weapon.WeaponData != null ? weapon.WeaponData.id.Value : "").Str("destination", destName)
                    .Str("reason", reason).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("fire-refused-log", e);
            }
            return false;
        }

        private static int SafeEnergy(PlayerData me)
        {
            try { return me.ResourceHandler.Energy; }
            catch (Exception e) { ReportOnce("fire-paid-snapshot", e); return -1; }
        }

        private static void CompleteFirstPhasePostfix(PlayerWeaponHandler __instance, WeaponGameplayData weapon, TrajectoryCalculationResults trajectory,
            PlayerWeaponTargetingResult __result, int __state)
        {
            if (__state < 0 || __result == null || !__result.IsFirstPhaseTargeted) return;
            try
            {
                PlayerData me = s_handlerPlayer.GetValue(__instance) as PlayerData;
                CelestialBody dest = trajectory != null ? trajectory.HitCelestialBody : null;
                bool enemyDest = dest != null && dest.Player != null && dest.Player != me;
                Plugin.WriteDecision(new Json().Str("e", "fire-paid").Int("t", GameManager.CurrentTurn).Str("ai", me.UserNickName)
                    .Str("wid", weapon.WeaponData != null ? weapon.WeaponData.id.Value : "").Bool("guided", IsGuided(weapon))
                    // v0.20 (council I1a): "own" was also any non-player body; split it into own (our planet) and neutral.
                    .Str("destination", enemyDest ? dest.Player.UserNickName : dest == null ? "none" : dest.Player == me ? "own" : "neutral")
                    .Int("candidates", enemyDest ? StrikeCandidateCount(me, dest.Player) : -1)
                    .Int("energyPaid", Math.Max(0, __state - me.ResourceHandler.Energy)).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("fire-paid-log", e);
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
            if (__state && !SmartGoal(__instance)) __state = false; // v0.17.1: the postfix skips an original AI too
            if (!__state || !Plugin.Active) return;
            try
            {
                // v0.10 hold fire: nothing in the firing lists, nothing fired, no firing energy charged (the reserved
                // energy went to construction this turn).
                AICore holder = (AICore)s_goalCore.GetValue(__instance);
                // v0.19 (Terra v0.19b #1): the goal runs inside AIWeaponTargeting.EndTurn, the last module; construction's
                // EndTurn has already run. A turn held only for energy may still fire here, before the hold is read.
                if (holder != null) StrategyPatches.LateFreeFire(holder);
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
            // v0.16 (Sol v0.16 F7, F8): withhold blind precision weapons, enforce the per-class hits, log what is committed.
            try
            {
                AICore shooter = (AICore)s_goalCore.GetValue(__instance);
                List<AITrajectoryWeapon> hitList = (List<AITrajectoryWeapon>)s_goalHitting.GetValue(__instance);
                List<AITrajectoryWeapon> missList = (List<AITrajectoryWeapon>)s_goalMissing.GetValue(__instance);
                List<AITrajectoryWeapon> allList = s_goalWeapons != null ? s_goalWeapons.GetValue(__instance) as List<AITrajectoryWeapon> : null;
                if (shooter != null && hitList != null)
                {
                    PlayerData me = shooter.PlayerData;
                    Dictionary<int, int> cache = new Dictionary<int, int>();
                    int withheld = Plugin.BlindArtillery.Value ? WithholdBlindPrecision(me, hitList, missList, cache) : 0;
                    HashSet<AITrajectoryWeapon> excluded = new HashSet<AITrajectoryWeapon>();
                    HashSet<WeaponGameplayData> registered = RegisteredWeapons(me);
                    if (allList != null)
                    {
                        foreach (AITrajectoryWeapon w in allList)
                        {
                            WeaponGameplayData wd = w != null && w.TargetableWeapon != null ? w.TargetableWeapon.Weapon : null;
                            bool blindGuided = Plugin.BlindArtillery.Value && IsGuidedTrajectory(w) && CandidatesFor(me, w, cache) <= 0;
                            if (wd == null || !registered.Contains(wd) || !IsOperational(me, wd) || blindGuided) excluded.Add(w);
                        }
                    }
                    int[] quota;
                    if (!Plugin.AccuracyOverride.Value || !s_classQuota.TryGetValue(__instance, out quota) || quota[2] != GameManager.CurrentTurn) quota = null;
                    if (quota != null) EnforceClassQuota(quota, allList, hitList, missList, shooter.Planet, excluded);
                    LogFiring(me, quota, hitList, missList, withheld, cache);
                }
            }
            catch (Exception e)
            {
                ReportOnce("class-quota", e);
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
            if (!SmartModule(__instance)) return true; // v0.17.1: an original AI picks its own second-phase target
            // v0.16 (Sol v0.16 re-check #1): fail closed. Native selection accepts Shadow cities, so no path below returns to it.
            bool queued = false;
            AICore failCore = null;
            PlayerData me = null, enemy = null;
            List<BaseBuilding> buildings = new List<BaseBuilding>();
            List<StrikeCandidate> candidates = new List<StrikeCandidate>();
            Dictionary<string, float> ledger = null;
            int pick = -1;
            float damage = 0f;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                failCore = core;
                enemy = targetPlanet != null ? targetPlanet.Player : null;
                if (core == null || enemy == null) return false; // nothing information-free to aim at: no second phase
                me = core.PlayerData;
                ledger = VolleyLedger(me.PlayerID, GameManager.CurrentTurn);
                AddStrikeCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.City), true, ledger, buildings, candidates);
                AddStrikeCandidates(me, enemy.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.Weapon), false, ledger, buildings, candidates);
                bool preferWeapon = !StrategyPatches.PrecisionStrike(me.PlayerID) && Plugin.Rng.NextDouble() < Plugin.WeaponBuildingShare.Value;
                pick = VolleyPlanner.Pick(candidates, Plugin.VolleyOverkill.Value, preferWeapon);
                if (pick < 0)
                {
                    // v0.16 (Sol v0.16 F1): native fallback picks among non-Invisible cities (Shadow included). Use the
                    // information-free fallback instead: a random point on the planet's surface.
                    core.Actions.SecondPhaseTargetWeapon(weapon, targetPlanet, UnityEngine.Random.onUnitSphere * targetPlanet.Radius);
                    queued = true;
                    LogStrike(me, enemy, weapon, null, null, 0f, 0, "random-surface");
                    return false;
                }
                damage = candidates[pick].IsCity ? weapon.DamageModule.PopulationDamage : weapon.DamageModule.BuildingDamage;
                core.Actions.SecondPhaseTargetWeapon(weapon, targetPlanet, buildings[pick].transform.localPosition);
                queued = true;
            }
            catch (Exception e)
            {
                ReportOnce("strike", e);
                // v0.16: never the native selector. If nothing was queued yet, the information-free random point.
                if (!queued && failCore != null && targetPlanet != null)
                {
                    try { failCore.Actions.SecondPhaseTargetWeapon(weapon, targetPlanet, UnityEngine.Random.onUnitSphere * targetPlanet.Radius); }
                    catch (Exception inner) { ReportOnce("strike-fallback", inner); }
                }
                return false;
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
                // v0.16 (Sol v0.16 F1, decision-record-r2 1.10): a Shadow city is no longer a candidate; its frozen record
                // and coordinates may not rescue targeting once it leaves sight.
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
