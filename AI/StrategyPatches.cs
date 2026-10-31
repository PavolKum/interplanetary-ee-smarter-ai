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
    // v0.10 game adapter for Brain/Strategy: decides once per AI turn (after the defense ledger and the enemy
    // defense memory are updated in EconomyPatches.CoreBeginTurnPostfix) whether to fire, and at whom.
    //  - Targets: every weapon is pointed at the committed enemy (falling back to one it can reach).
    //  - Hold: DirectorPatches gives the firing energy the game reserved to construction, and AiPatches empties
    //    the firing lists, so nothing fires and nothing is charged.
    // No decision (strategy off, or it failed) means v0.9 behaviour: fire every turn, split targets.
    internal static class StrategyPatches
    {
        private sealed class State
        {
            public int Turn = -1;
            public int LastStrikeTurn = -1000;
            public int HoldTurns;
            public StrategyDecision Decision;
        }

        private static readonly Dictionary<int, State> s_states = new Dictionary<int, State>();
        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();
        private static readonly FieldInfo s_coreTargeting = AccessTools.Field(typeof(AICore), "m_weaponTargeting");

        internal static void Reset()
        {
            s_states.Clear();
        }

        // The AI's latest decision (made at the start of its current turn), or null: behave as v0.9. Read by turn start
        // (construction) and at firing (AIWeaponTargeting.EndTurn); both must see the same one, so it is not keyed on
        // GameManager.CurrentTurn, which may already have moved on when the weapons fire.
        internal static StrategyDecision Latest(int playerId)
        {
            if (!Plugin.Active || !Plugin.StrategyEnabled.Value) return null;
            State s;
            return s_states.TryGetValue(playerId, out s) ? s.Decision : null;
        }

        internal static void ClearDecision(int playerId)
        {
            State s;
            // A turn start seen twice keeps the decision already made for that turn (BeginTurn will not redo it).
            if (s_states.TryGetValue(playerId, out s) && s.Turn != GameManager.CurrentTurn) s.Decision = null;
        }

        internal static bool HoldFire(int playerId)
        {
            StrategyDecision d = Latest(playerId);
            return d != null && !d.Fire;
        }

        internal static void BeginTurn(AICore core, EconomyPatches.AiEconomyState economy, float recentDamage, int turn)
        {
            if (!Plugin.Active || !Plugin.StrategyEnabled.Value) return;
            PlayerData me = core.PlayerData;
            State state;
            if (!s_states.TryGetValue(me.PlayerID, out state))
            {
                state = new State();
                s_states[me.PlayerID] = state;
            }
            if (state.Turn == turn) return; // decided already (a turn start seen twice)
            if (turn < state.Turn) { state.LastStrikeTurn = -1000; state.HoldTurns = 0; } // an earlier save
            state.Turn = turn;
            state.Decision = null;

            StrategyDecision decision;
            List<CelestialBody> planets = core.EnemyPlanets;
            try
            {
                bool war = EconomyDirector.WarPhase(turn, Plugin.WarFromTurn.Value, recentDamage, Plugin.WarDamageTrigger.Value);
                Dictionary<string, int> shots = new Dictionary<string, int>();
                foreach (WeaponGameplayData weapon in me.WeaponHandler.Weapons)
                {
                    if (weapon == null || weapon.IsLaser || weapon.WeaponData == null || weapon.Building == null) continue;
                    if (weapon.Building.CurrentState != BaseBuilding.BuildingState.Normal || !weapon.Building.Power.HasPower) continue;
                    string id = weapon.WeaponData.id.Value;
                    int n;
                    shots.TryGetValue(id, out n);
                    shots[id] = n + 1;
                }

                List<EnemyView> views = new List<EnemyView>();
                foreach (CelestialBody planet in planets) views.Add(AiPatches.BuildEnemyView(me, planet.Player, turn));
                TargetPlanner.Score(views, Plugin.Planner);
                List<SalvoTarget> targets = new List<SalvoTarget>();
                foreach (EnemyView view in views)
                {
                    Dictionary<string, int> known;
                    economy.KnownDefenses.TryGetValue(view.PlayerId, out known);
                    int impacts = 0, intercepted = 0;
                    foreach (int[] counts in Plugin.Ledger.ObservedAll(me.PlayerID, view.PlayerId, 1).Values)
                    {
                        impacts += counts[0];
                        intercepted += counts[1];
                    }
                    targets.Add(new SalvoTarget
                    {
                        PlayerId = view.PlayerId,
                        Name = view.Name,
                        Score = view.Score,
                        Defenses = known != null ? new Dictionary<string, int>(known) : new Dictionary<string, int>(),
                        EnergyKnown = view.EnergyKnown,
                        EnergyProduction = view.EnergyProduction,
                        ObservedImpacts = impacts,
                        ObservedIntercepted = intercepted,
                    });
                }
                decision = Strategy.Decide(turn, war, shots, targets, DefensePlanner.Counters, state.LastStrikeTurn, state.HoldTurns, Plugin.StrategyCfg);
                if (decision.Fire)
                {
                    state.LastStrikeTurn = turn;
                    state.HoldTurns = 0;
                }
                else if (war && decision.Shots >= Plugin.StrategyCfg.MinWeapons)
                {
                    state.HoldTurns++;
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

            // Point the weapons. Native targeting already ran in AICore.BeginTurn; trajectory search starts in Update.
            AssignTargets(core, decision.CommitTargetId);
            Log(me, turn, decision, state);
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

        private static void Log(PlayerData me, int turn, StrategyDecision d, State state)
        {
            try
            {
                Dictionary<string, int> passing = new Dictionary<string, int>();
                Dictionary<string, int> eligible = new Dictionary<string, int>();
                Dictionary<string, int> rolls = new Dictionary<string, int>();
                Dictionary<string, int> defenses = new Dictionary<string, int>();
                Dictionary<string, int> score = new Dictionary<string, int>();
                string commit = "";
                foreach (SalvoTarget t in d.Targets)
                {
                    string name = t.Name ?? t.PlayerId.ToString();
                    passing[name] = (int)Math.Round(t.Passing);
                    eligible[name] = (int)Math.Round(t.Eligible);
                    rolls[name] = t.Rolls >= float.MaxValue / 2 ? -1 : (int)Math.Round(t.Rolls);
                    int n = 0;
                    foreach (int c in t.Defenses.Values) n += c;
                    defenses[name] = n;
                    score[name] = (int)Math.Round(t.Score * 1000f);
                    if (t.PlayerId == d.CommitTargetId) commit = name;
                }
                Plugin.WriteDecision(new Json()
                    .Str("e", "strategy").Int("t", turn).Str("ai", me.UserNickName)
                    .Bool("fire", d.Fire).Str("reason", d.Reason).Int("shots", d.Shots).Num("passing", d.Passing).Num("passShare", d.PassShare)
                    .Str("commit", commit).Int("lastStrike", state.LastStrikeTurn).Int("holdTurns", state.HoldTurns)
                    .Map("passingBy", passing).Map("eligibleBy", eligible).Map("rollsBy", rolls).Map("defensesBy", defenses).Map("scoreBy", score)
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
