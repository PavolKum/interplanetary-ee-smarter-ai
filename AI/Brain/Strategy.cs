using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // v0.10 strategy: when to fire and at whom. Facts behind it (v0.9.1 playtest, Sol strategy review):
    //  - Unfired weapons cost nothing, but the native AI reserves every weapon's firing energy before it builds
    //    (AIResources.BeginTurn). From T18 Guts spent ~35% of its energy income on firing into defenses that
    //    intercepted ~90%, and ended every turn with 15-37k material it could not use.
    //  - The human built 11 -> 51 Railguns from T22 without firing (T23-27), then fired every other turn
    //    (T28 100 shots, T30 109) and invested on the turns between.
    //  - A defense in range intercepts ~80-90%, and every roll costs the defender 100-200 energy: a salvo larger
    //    than the rolls the target can pay for gets through (saturation).
    // So: hold fire (and give the reserved energy to construction) until a salvo at one committed target is
    // expected to get enough shots through; then strike, and pulse.

    public sealed class SalvoTarget
    {
        public int PlayerId;
        public string Name;
        public float Score;                        // TargetPlanner score (leader / grudge / finish)
        public Dictionary<string, int> Defenses;   // defense building id -> count, as seen or remembered
        public bool EnergyKnown;                   // our intel reads its energy production
        public float EnergyProduction;
        public int ObservedImpacts;                // our recent shots that reached it (DefenseLedger window)
        public int ObservedIntercepted;

        // outputs
        public float Passing;      // expected shots through its defenses
        public float Eligible;     // shots expected to meet a defense in range
        public float Rolls;        // defense rolls it can pay for (float.MaxValue when unknown)
        public float Value;        // Score x Passing
    }

    public sealed class StrategySettings
    {
        public float CoverHalf = 12f;          // defenses at which half of the shots meet one in range
        public float MaxCover = 0.9f;
        public float RollEnergyShare = 0.5f;   // share of its energy income a defender has left for rolls
        public float RollCost = 150f;          // energy per defense roll
        public float MinPassingShots = 12f;    // strike when at least this many shots are expected through...
        public float MinPassShare = 0.45f;     // ...and at least this share of the salvo
        public float BrokenPassShare = 0.7f;   // defenses this broken: strike again without waiting for the pulse
        public int StrikeEvery = 2;            // turns between strikes (2 = every other turn, as the human)
        public int MaxHoldTurns = 5;           // at war with weapons, never hold longer than this
        public int MinWeapons = 6;             // fewer weapons than this: keep building, do not fire
        public int ObservedMinImpacts = 10;    // our own shots at a target before they correct the coverage guess
        public float ObservedPriorWeight = 20f; // impacts' worth of weight on the modelled coverage
        public float DefaultChance = 0.8f;     // intercept chance assumed for defenses we have not seen
    }

    public sealed class StrategyDecision
    {
        public bool Fire;
        public int CommitTargetId = -1;
        public string Reason = "";
        public int Shots;
        public float Passing;
        public float PassShare;
        public List<SalvoTarget> Targets = new List<SalvoTarget>();
    }

    public static class Strategy
    {
        // Expected shots through one target's defenses. counters: defense id -> (weapon id -> intercept chance).
        public static void EstimateSalvo(IDictionary<string, int> shotsByWeapon, SalvoTarget target,
            Dictionary<string, Dictionary<string, float>> counters, StrategySettings s)
        {
            float total = 0f, eligible = 0f, weightedChance = 0f;
            foreach (KeyValuePair<string, int> shots in shotsByWeapon)
            {
                if (shots.Value <= 0) continue;
                total += shots.Value;
                int defenses = 0;
                float chance = 0f;
                if (target.Defenses != null)
                {
                    foreach (KeyValuePair<string, int> defense in target.Defenses)
                    {
                        Dictionary<string, float> stops;
                        float p;
                        if (defense.Value <= 0 || !counters.TryGetValue(defense.Key, out stops) || !stops.TryGetValue(shots.Key, out p) || p <= 0f) continue;
                        defenses += defense.Value;
                        chance = Math.Max(chance, p);
                    }
                }
                if (defenses == 0) continue;
                float cover = Math.Min(s.MaxCover, defenses / (defenses + Math.Max(1e-3f, s.CoverHalf)));
                float e = shots.Value * cover;
                eligible += e;
                weightedChance += e * chance;
            }
            float averageChance = eligible > 0f ? weightedChance / eligible : 0f;
            // Our own shots at it correct the guessed coverage (defenses we never saw, better placement than assumed).
            // Past salvos that saturated its rolls understate coverage; the prior weight keeps a few shots from deciding.
            if (target.ObservedImpacts >= Math.Max(1, s.ObservedMinImpacts) && total > 0f)
            {
                float n = target.ObservedImpacts;
                float chance = averageChance > 0f ? averageChance : s.DefaultChance;
                float observedCover = Math.Min(s.MaxCover, target.ObservedIntercepted / n / Math.Max(1e-3f, chance));
                float modelCover = eligible / total;
                float cover = (s.ObservedPriorWeight * modelCover + n * observedCover) / (s.ObservedPriorWeight + n);
                eligible = cover * total;
                averageChance = chance;
            }
            target.Rolls = target.EnergyKnown ? Math.Max(0f, target.EnergyProduction) * s.RollEnergyShare / Math.Max(1f, s.RollCost) : float.MaxValue;
            float intercepted = Math.Min(target.Rolls, eligible) * averageChance;
            target.Eligible = eligible;
            target.Passing = Math.Max(0f, total - intercepted);
            target.Value = target.Score * target.Passing;
        }

        // lastStrikeTurn: last turn we fired (-1000 never); holdTurns: consecutive war turns held with weapons.
        public static StrategyDecision Decide(int turn, bool war, IDictionary<string, int> shotsByWeapon, IList<SalvoTarget> targets,
            Dictionary<string, Dictionary<string, float>> counters, int lastStrikeTurn, int holdTurns, StrategySettings s)
        {
            StrategyDecision d = new StrategyDecision();
            foreach (int n in shotsByWeapon.Values) d.Shots += Math.Max(0, n);
            SalvoTarget best = null;
            foreach (SalvoTarget t in targets)
            {
                EstimateSalvo(shotsByWeapon, t, counters, s);
                d.Targets.Add(t);
                if (best == null || t.Value > best.Value) best = t;
            }
            if (best == null || d.Shots == 0)
            {
                d.Reason = "no-weapons";
                return d;
            }
            d.CommitTargetId = best.PlayerId;
            d.Passing = best.Passing;
            d.PassShare = best.Passing / d.Shots;
            if (!war)
            {
                d.Reason = "peace";
                return d;
            }
            if (d.Shots < s.MinWeapons)
            {
                d.Reason = "building";
                return d;
            }
            bool saturated = d.Passing >= s.MinPassingShots && d.PassShare >= s.MinPassShare;
            bool pulseReady = turn - lastStrikeTurn >= Math.Max(1, s.StrikeEvery);
            bool broken = d.PassShare >= s.BrokenPassShare && d.Passing >= s.MinPassingShots;
            if (saturated && (pulseReady || broken))
            {
                d.Fire = true;
                d.Reason = pulseReady ? "saturated" : "broken";
            }
            else if (holdTurns >= s.MaxHoldTurns && pulseReady)
            {
                d.Fire = true;
                d.Reason = "max-hold";
            }
            else
            {
                d.Reason = saturated ? "pulse-wait" : "below-saturation";
            }
            return d;
        }
    }
}
