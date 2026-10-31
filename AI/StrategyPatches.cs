using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.GameEngine;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;
using Interplanetary.Weapons;
using InterplanetaryAI.Brain;

namespace InterplanetaryAI
{
    // v0.10 game adapter for Brain/Strategy. Decides once per AI turn inside EconomyPatches.CoreBeginTurnPostfix,
    // after the defense ledger roll and the enemy defense memory update and before the superweapon savings:
    //  - Targets: every weapon is pointed at the committed enemy (falling back to one it can reach).
    //  - Hold: the firing energy the game reserved (AIResources.BeginTurn) goes back to Resources and the
    //    construction queue at once, and AiPatches empties the firing lists, so nothing fires or is charged.
    // Hold needs the director (it spends the energy) and the firing hook (it stops the shots); without either the
    // AI fires as v0.9 would, at the committed target. No decision (strategy off or failed) = v0.9 behaviour.
    internal static class StrategyPatches
    {
        private sealed class State
        {
            public int Turn = -1;
            public int LastStrikeTurn = -1000;
            public int HoldTurns;
            public int ReleasedEnergy;
            public bool Reported;               // this turn's firing has been counted
            public int LastCommitId = -1;       // the enemy the weapons were pointed at last turn
            public bool Aborted;                // this turn's strike was called off at firing time
            public StrategyDecision Decision;
            public readonly HashSet<int> PlanetsSeen = new HashSet<int>(); // enemies whose buildings our intel has seen
        }

        private static readonly Dictionary<int, State> s_states = new Dictionary<int, State>();
        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();
        private static readonly FieldInfo s_coreTargeting = AccessTools.Field(typeof(AICore), "m_weaponTargeting");
        // v0.12: AICore is ready once m_timeOut seconds have passed since its BeginTurn and the human has ended the turn (or
        // every weapon found a trajectory). The trajectory search runs only until then: 20 s in the game.
        private static readonly FieldInfo s_coreTimeOut = AccessTools.Field(typeof(AICore), "m_timeOut");
        internal const float NativeAimWindow = 20f;

        internal static void SetAimWindow(AICore core, float seconds)
        {
            try
            {
                if (s_coreTimeOut != null && core != null) s_coreTimeOut.SetValue(core, Math.Max(NativeAimWindow, Math.Min(120f, seconds)));
            }
            catch (Exception e)
            {
                ReportOnce("aim-window", e);
            }
        }

        internal static void Reset()
        {
            s_states.Clear();
        }

        private static bool Enabled
        {
            get { return Plugin.Active && Plugin.StrategyEnabled.Value && Plugin.DirectorEnabled.Value; }
        }

        // Hold fire is only possible when the hook that empties the firing lists is installed and its fields resolved.
        internal static bool HoldAvailable
        {
            get { return Plugin.HoldHookPatched && AiPatches.HoldFieldsResolved; }
        }

        // The AI's decision for its current turn, or null. Latched for the whole turn: it is not keyed on
        // GameManager.CurrentTurn (which may move before the weapons fire) nor on live config toggles (the energy may
        // already be spent). Cleared at the start of the AI's next turn.
        internal static StrategyDecision Latest(int playerId)
        {
            State s;
            return s_states.TryGetValue(playerId, out s) ? s.Decision : null;
        }

        internal static bool HoldFire(int playerId)
        {
            StrategyDecision d = Latest(playerId);
            return d != null && !d.Fire;
        }

        internal static int ReleasedEnergy(int playerId)
        {
            State s;
            return s_states.TryGetValue(playerId, out s) && s.Decision != null ? s.ReleasedEnergy : 0;
        }

        internal static void ClearDecision(int playerId)
        {
            State s;
            // A turn start seen twice keeps the decision already made for that turn (BeginTurn will not redo it).
            if (s_states.TryGetValue(playerId, out s) && s.Turn != GameManager.CurrentTurn)
            {
                s.Decision = null;
                s.ReleasedEnergy = 0;
            }
        }

        // From AiPatches.GoalTargetWeaponsPostfix: what actually went into the firing lists this turn. The strike
        // history follows real shots, not the plan (a strike whose trajectories all failed does not use the pulse).
        internal static void ReportFired(int playerId, int fired)
        {
            State s;
            if (!s_states.TryGetValue(playerId, out s) || s.Decision == null || s.Reported) return;
            s.Reported = true;
            if (fired > 0)
            {
                s.LastStrikeTurn = s.Turn;
                s.HoldTurns = 0;
            }
            else if (s.Decision.War && s.Decision.Shots > 0)
            {
                s.HoldTurns++;
            }
        }

        internal static void BeginTurn(AICore core, EconomyPatches.AiEconomyState economy, float recentDamage, int turn)
        {
            if (!Enabled) return;
            PlayerData me = core.PlayerData;
            State state;
            if (!s_states.TryGetValue(me.PlayerID, out state))
            {
                state = new State();
                s_states[me.PlayerID] = state;
            }
            if (state.Turn == turn) return; // decided already (a turn start seen twice)
            if (turn < state.Turn) { state.LastStrikeTurn = -1000; state.HoldTurns = 0; state.LastCommitId = -1; } // an earlier save
            if (turn != state.Turn + 1) state.LastCommitId = -1; // aim memory only from the turn just before
            state.Turn = turn;
            state.Decision = null;
            state.ReleasedEnergy = 0;
            state.Reported = false;
            state.Aborted = false;

            StrategyDecision decision;
            try
            {
                bool war = EconomyDirector.WarPhase(turn, Plugin.WarFromTurn.Value, recentDamage, Plugin.WarDamageTrigger.Value);
                List<CelestialBody> planets = core.EnemyPlanets;
                List<EnemyView> views = new List<EnemyView>();
                foreach (CelestialBody planet in planets) views.Add(AiPatches.BuildEnemyView(me, planet.Player, turn));
                TargetPlanner.Score(views, Plugin.Planner);

                // Projectiles per weapon id, overall and per enemy (weapons our reachability memory says cannot reach an
                // enemy are left out of its salvo). A projectile's fragments are intercepted one by one.
                Dictionary<string, float> salvo = new Dictionary<string, float>();
                Dictionary<int, Dictionary<string, float>> perEnemy = new Dictionary<int, Dictionary<string, float>>();
                foreach (EnemyView view in views) perEnemy[view.PlayerId] = new Dictionary<string, float>();
                int weapons = 0;
                HashSet<int> unreachable = new HashSet<int>();
                foreach (WeaponGameplayData weapon in me.WeaponHandler.Weapons)
                {
                    if (weapon == null || weapon.IsLaser || weapon.WeaponData == null || weapon.Building == null) continue;
                    if (weapon.Building.CurrentState != BaseBuilding.BuildingState.Normal || !weapon.Building.Power.HasPower) continue;
                    weapons++;
                    string id = weapon.WeaponData.id.Value;
                    // Live counts, upgrades included (Terra v0.11 #2): ShootingModule / SurfaceModule are what the action phase fires.
                    int liveProjectiles = weapon.ShootingModule != null ? weapon.ShootingModule.ProjectileAmount : 0;
                    int liveFragments = weapon.SurfaceModule != null ? weapon.SurfaceModule.SplitFragments : 0;
                    if (liveProjectiles < 1) liveProjectiles = weapon.WeaponData.projectile_amount.IsValueSet ? weapon.WeaponData.projectile_amount.Value : 1;
                    if (liveFragments < 1) liveFragments = weapon.WeaponData.fragment_amount.IsValueSet ? weapon.WeaponData.fragment_amount.Value : 1;
                    float projectiles = Math.Max(1, liveProjectiles) * Math.Max(1, liveFragments);
                    Add(salvo, id, projectiles);
                    unreachable.Clear();
                    Plugin.Reach.FillUnreachable(AiPatches.WeaponKey(weapon), turn, unreachable);
                    foreach (EnemyView view in views)
                    {
                        if (!unreachable.Contains(view.PlayerId)) Add(perEnemy[view.PlayerId], id, projectiles);
                    }
                }

                float accuracy = Plugin.AccuracyOverride.Value ? AccuracyPlan.ForLevel(Plugin.Accuracy, me.AILevel)
                    : core.Difficulty != null ? core.Difficulty.Accuracy : 0.75f;
                List<SalvoTarget> targets = new List<SalvoTarget>();
                for (int i = 0; i < views.Count; i++)
                {
                    EnemyView view = views[i];
                    PlayerData enemy = planets[i].Player;
                    if (!state.PlanetsSeen.Contains(view.PlayerId))
                    {
                        foreach (BaseBuilding b in enemy.BuildingHandler.Buildings)
                        {
                            if (b != null && !b.IsCity && AiPatches.IsVisibleIntact(me, b)) { state.PlanetsSeen.Add(view.PlayerId); break; }
                        }
                    }
                    Dictionary<string, int> known;
                    economy.KnownDefenses.TryGetValue(view.PlayerId, out known);
                    targets.Add(new SalvoTarget
                    {
                        PlayerId = view.PlayerId,
                        Name = view.Name,
                        Score = view.Score,
                        Defenses = known != null ? new Dictionary<string, int>(known) : new Dictionary<string, int>(),
                        DefensesSeen = state.PlanetsSeen.Contains(view.PlayerId),
                        EnergyKnown = view.EnergyKnown,
                        EnergyProduction = view.EnergyProduction,
                        Shots = perEnemy[view.PlayerId],
                        Observed = Plugin.Ledger.ObservedAll(me.PlayerID, view.PlayerId, 1),
                    });
                }
                decision = Strategy.Decide(turn, war, salvo, accuracy, targets, DefensePlanner.Counters, state.LastStrikeTurn, state.HoldTurns,
                    weapons, Plugin.StrategyCfg, state.LastCommitId);
                if (!decision.Fire && !HoldAvailable)
                {
                    decision.Fire = true; // cannot stop the shots: do not take their energy either
                    decision.Reason += "/hold-unavailable";
                }
                state.Decision = decision;
            }
            catch (Exception e)
            {
                ReportOnce("strategy", e);
                state.Decision = null;
                AssignTargets(core, -1); // v0.9 split targeting
                return;
            }

            int aimedBefore = state.LastCommitId;
            state.LastCommitId = decision.CommitTargetId;
            if (!decision.Fire) Release(core, state);
            // v0.12: a strike turn gets the long aiming window; a hold turn keeps the game's 20 s (nothing will fire).
            SetAimWindow(core, decision.Fire ? Plugin.AimingWindowSeconds.Value : NativeAimWindow);
            // Point the weapons. Native targeting already ran in AICore.BeginTurn; trajectory search starts in Update.
            AssignTargets(core, decision.CommitTargetId);
            Log(me, turn, decision, state, aimedBefore);
        }

        // v0.11, at firing time (AiPatches.GoalTargetWeaponsPrefix, after the firing lists are rebuilt): call the strike off
        // when too few weapons found a trajectory onto the committed target. The firing energy then stays for defense rolls,
        // and the turn counts as held (no pulse used). v0.10.1 T30: 11 of 48 weapons reached a newly committed target.
        internal static bool ShouldAbort(PlayerData me, int hitsOnCommit)
        {
            State s;
            if (!s_states.TryGetValue(me.PlayerID, out s) || s.Decision == null || !s.Decision.Fire) return false;
            // v0.12: fire what can aim; only a strike with almost nothing aimed is called off (Strategy.ShouldAbort).
            if (!Strategy.ShouldAbort(hitsOnCommit, s.Decision.Shots, s.Decision.Reason, Plugin.StrategyCfg)) return false;
            float needed = Strategy.AbortThreshold(s.Decision.Shots, Plugin.StrategyCfg);
            s.Aborted = true;
            Plugin.WriteDecision(new Json().Str("e", "abort").Int("t", s.Turn).Str("ai", me.UserNickName)
                .Int("hitsOnCommit", hitsOnCommit).Num("needed", needed).Int("shots", s.Decision.Shots).ToString());
            return true;
        }

        internal static int CommitTarget(int playerId)
        {
            StrategyDecision d = Latest(playerId);
            return d != null ? d.CommitTargetId : -1;
        }

        // Nothing fires this turn, so the firing cost the game reserved is free for construction.
        private static void Release(AICore core, State state)
        {
            int energyBack = Math.Max(0, core.Resources.EnergyUpkeep);
            int materialBack = Math.Max(0, core.Resources.MaterialUpkeep);
            core.Resources.Energy += energyBack;
            core.Resources.Material += materialBack;
            core.Construction.ConstructionQueue.AllottedEnergy += energyBack;
            core.Construction.ConstructionQueue.AllottedMaterial += materialBack;
            state.ReleasedEnergy = energyBack;
        }

        private static void Add(Dictionary<string, float> counts, string key, float n)
        {
            float v;
            counts.TryGetValue(key, out v);
            counts[key] = v + n;
        }

        private static void AssignTargets(AICore core, int commitPlayerId)
        {
            try
            {
                AIWeaponTargeting targeting = s_coreTargeting != null ? s_coreTargeting.GetValue(core) as AIWeaponTargeting : null;
                if (targeting != null) AiPatches.AssignTargets(targeting, commitPlayerId);
            }
            catch (Exception e)
            {
                ReportOnce("strategy-targets", e);
            }
        }

        private static void Log(PlayerData me, int turn, StrategyDecision d, State state, int aimedBefore)
        {
            try
            {
                // Calibration: our own shots at each target over the ledger window, [impacts, intercepted] summed over weapons.
                Dictionary<string, int> observedImpacts = new Dictionary<string, int>();
                Dictionary<string, int> observedIntercepted = new Dictionary<string, int>();
                Dictionary<string, int> passing = new Dictionary<string, int>();
                Dictionary<string, int> arriving = new Dictionary<string, int>();
                Dictionary<string, int> eligible = new Dictionary<string, int>();
                Dictionary<string, int> rolls = new Dictionary<string, int>();
                Dictionary<string, int> defenses = new Dictionary<string, int>();
                Dictionary<string, int> score = new Dictionary<string, int>();
                string commit = "";
                foreach (SalvoTarget t in d.Targets)
                {
                    string name = t.Name ?? t.PlayerId.ToString();
                    passing[name] = (int)Math.Round(t.Passing);
                    arriving[name] = (int)Math.Round(t.Arriving);
                    eligible[name] = (int)Math.Round(t.Eligible);
                    rolls[name] = t.Rolls >= float.MaxValue / 2 ? -1 : (int)Math.Round(t.Rolls);
                    int n = 0;
                    foreach (int c in t.Defenses.Values) n += c;
                    defenses[name] = t.DefensesSeen ? n : -1;
                    score[name] = (int)Math.Round(t.Score * 1000f);
                    if (t.PlayerId == d.CommitTargetId) commit = name;
                    int imp = 0, inter = 0;
                    if (t.Observed != null) foreach (int[] c in t.Observed.Values) { imp += c[0]; inter += c[1]; }
                    observedImpacts[name] = imp;
                    observedIntercepted[name] = inter;
                }
                Plugin.WriteDecision(new Json()
                    .Str("e", "strategy").Int("t", turn).Str("ai", me.UserNickName).Bool("war", d.War)
                    .Bool("fire", d.Fire).Str("reason", d.Reason).Int("shots", d.Shots).Num("arriving", d.Arriving).Num("passing", d.Passing)
                    .Num("passShare", d.PassShare).Str("commit", commit).Int("lastStrike", state.LastStrikeTurn).Int("holdTurns", state.HoldTurns)
                    .Int("releasedEnergy", state.ReleasedEnergy)
                    .Map("passingBy", passing).Map("arrivingBy", arriving).Map("eligibleBy", eligible).Map("rollsBy", rolls)
                    .Map("defensesBy", defenses).Map("scoreBy", score)
                    .Int("aimedAt", aimedBefore).Map("observedImpactsBy", observedImpacts).Map("observedInterceptedBy", observedIntercepted)
                    .ToString());
            }
            catch (Exception e)
            {
                ReportOnce("strategy-log", e);
            }
        }

        private static void ReportOnce(string hook, Exception e)
        {
            if (s_reportedErrors.Add(hook)) Plugin.Log.LogError("Strategy hook '" + hook + "' failed (v0.9 firing used): " + e);
        }
    }
}
