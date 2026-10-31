using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // v0.10 strategy: when to fire and at whom. Facts behind it (v0.9.1 playtest, Sol strategy review):
    //  - Unfired weapons cost nothing, but the native AI reserves every weapon's firing energy before it builds
    //    (AIResources.BeginTurn). From T18 Guts spent ~35% of its energy income on firing into defenses that
    //    intercepted most of it, and ended every turn with 15-37k material it could not use.
    //  - The human built 11 -> 51 Railguns from T22 without firing (T23-27), then fired every other turn
    //    (T28 100 shots, T30 109) and invested on the turns between.
    //  - A defense in range intercepts at its chance; the game tries further defenses in range until one succeeds
    //    (PlayerDefenseHandler.IsHitIntercepted), and every roll costs the defender energy: a salvo larger than the
    //    rolls the target can pay for gets through (saturation).
    // So: hold fire (and give the reserved energy to construction) until a salvo at one committed target is
    // expected to get enough shots through; then strike, and pulse.

    public sealed class SalvoTarget
    {
        public int PlayerId;
        public string Name;
        public float Score;                        // TargetPlanner score (leader / grudge / finish)
        public Dictionary<string, int> Defenses;   // defense building id -> count, as seen or remembered
        public bool DefensesSeen;                  // our intel sees (or saw) its buildings: no remembered defense = none
        public bool EnergyKnown;                   // our intel reads its energy production
        public float EnergyProduction;
        public Dictionary<string, float> Shots;    // weapon id -> projectiles that can reach it (null: the salvo's)
        public Dictionary<string, int[]> Observed; // weapon id -> our recent [impacts, intercepted] at it

        // outputs
        public float Arriving;     // projectiles expected to arrive (after accuracy)
        public float Passing;      // expected through its defenses
        public float Eligible;     // expected to meet a defense in range
        public float Rolls;        // defense rolls it can pay for (float.MaxValue when unknown)
        public float Value;        // Score x Passing
    }

    public sealed class StrategySettings
    {
        public float CoverHalf = 12f;          // defenses at which half of the shots meet one in range
        public float MaxCover = 0.9f;
        public float OverlapDefenses = 20f;    // each this many defenses adds one more defense in range of an impact
        public float RollEnergyShare = 0.5f;   // share of its energy income a defender has left for rolls
        public float RollCost = 150f;          // energy per defense roll
        public float UnseenCover = 0.6f;       // coverage assumed for a planet whose buildings we never saw
        public float MinPassingShots = 12f;    // strike when at least this many shots are expected through...
        public float MinPassShare = 0.45f;     // ...and at least this share of the salvo
        public float BrokenPassShare = 0.7f;   // defenses this broken: strike again without waiting for the pulse
        public int StrikeEvery = 2;            // turns between strikes (2 = every other turn, as the human)
        public int MaxHoldTurns = 5;           // at war with weapons, never hold longer than this
        public int MinWeapons = 6;             // fewer weapons than this: keep building (until MaxHoldTurns)
        public int ObservedMinImpacts = 10;    // our own shots of a weapon at a target before they correct its coverage
        public float ObservedPriorWeight = 20f; // impacts' worth of weight on the modelled coverage
        public float DefaultChance = 0.8f;     // intercept chance assumed for defenses we have not seen
    }

    public sealed class StrategyDecision
    {
        public bool Fire;
        public bool War;
        public int CommitTargetId = -1;
        public string Reason = "";
        public int Shots;          // weapons ready to fire
        public float Arriving;     // at the committed target
        public float Passing;
        public float PassShare;
        public List<SalvoTarget> Targets = new List<SalvoTarget>();
    }

    public static class Strategy
    {
        // Expected shots through one target's defenses. counters: defense id -> (weapon id -> intercept chance).
        // accuracy: share of shots planned to hit (AccuracyPlan); misses do not reach the target.
        public static void EstimateSalvo(IDictionary<string, float> salvo, SalvoTarget target, float accuracy,
            Dictionary<string, Dictionary<string, float>> counters, StrategySettings s)
        {
            IDictionary<string, float> shotsByWeapon = target.Shots ?? salvo;
            float arriving = 0f, eligible = 0f, intercepts = 0f, rollsNeeded = 0f;
            foreach (KeyValuePair<string, float> shots in shotsByWeapon)
            {
                if (shots.Value <= 0f) continue;
                float n = shots.Value * Math.Max(0f, Math.Min(1f, accuracy));
                arriving += n;
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
                float cover;
                float tries; // defenses in range of one impact; the game rolls them in turn until one succeeds
                if (defenses > 0)
                {
                    cover = Math.Min(s.MaxCover, defenses / (defenses + Math.Max(1e-3f, s.CoverHalf)));
                    tries = 1f + defenses / Math.Max(1f, s.OverlapDefenses);
                }
                else if (!target.DefensesSeen)
                {
                    cover = s.UnseenCover; // never saw the planet: unknown is not absent
                    chance = s.DefaultChance;
                    tries = 1f;
                }
                else
                {
                    cover = 0f;
                    chance = s.DefaultChance;
                    tries = 1f;
                }
                float stopChance = 1f - (float)Math.Pow(1f - chance, tries);
                // Our own shots of this weapon at it correct the guessed coverage (defenses we never saw, better placement
                // than assumed). Salvos that saturated its rolls understate it; the prior weight keeps a few shots from deciding.
                int[] seen;
                if (target.Observed != null && target.Observed.TryGetValue(shots.Key, out seen) && seen != null && seen.Length >= 2
                    && seen[0] >= Math.Max(1, s.ObservedMinImpacts))
                {
                    if (stopChance <= 0f) { chance = s.DefaultChance; tries = 1f; stopChance = chance; }
                    float observedCover = Math.Min(s.MaxCover, seen[1] / (float)seen[0] / stopChance);
                    cover = (s.ObservedPriorWeight * cover + seen[0] * observedCover) / (s.ObservedPriorWeight + seen[0]);
                }
                float e = n * cover;
                eligible += e;
                intercepts += e * stopChance;
                // Expected rolls per eligible impact: tries until the first success, at most `tries`.
                rollsNeeded += e * (chance > 0f ? stopChance / chance : 1f);
            }
            target.Rolls = target.EnergyKnown ? Math.Max(0f, target.EnergyProduction) * s.RollEnergyShare / Math.Max(1f, s.RollCost) : float.MaxValue;
            float paid = rollsNeeded <= 0f ? 1f : Math.Min(1f, target.Rolls / rollsNeeded);
            target.Arriving = arriving;
            target.Eligible = eligible;
            target.Passing = Math.Max(0f, arriving - intercepts * paid);
            target.Value = target.Score * target.Passing;
        }

        // lastStrikeTurn: last turn we fired (-1000 never); holdTurns: consecutive war turns held with weapons.
        public static StrategyDecision Decide(int turn, bool war, IDictionary<string, float> salvo, float accuracy, IList<SalvoTarget> targets,
            Dictionary<string, Dictionary<string, float>> counters, int lastStrikeTurn, int holdTurns, int weapons, StrategySettings s)
        {
            StrategyDecision d = new StrategyDecision { War = war, Shots = weapons };
            SalvoTarget best = null;
            foreach (SalvoTarget t in targets)
            {
                EstimateSalvo(salvo, t, accuracy, counters, s);
                d.Targets.Add(t);
                if (best == null || t.Value > best.Value) best = t;
            }
            if (best == null || weapons <= 0)
            {
                d.Reason = "no-weapons";
                return d;
            }
            d.CommitTargetId = best.PlayerId;
            d.Arriving = best.Arriving;
            d.Passing = best.Passing;
            d.PassShare = best.Arriving > 0f ? best.Passing / best.Arriving : 0f;
            if (!war)
            {
                d.Reason = "peace";
                return d;
            }
            bool pulseReady = turn - lastStrikeTurn >= Math.Max(1, s.StrikeEvery);
            bool saturated = d.Passing >= s.MinPassingShots && d.PassShare >= s.MinPassShare;
            bool broken = d.PassShare >= s.BrokenPassShare && d.Passing >= s.MinPassingShots;
            if (weapons >= s.MinWeapons && saturated && (pulseReady || broken))
            {
                d.Fire = true;
                d.Reason = pulseReady ? "saturated" : "broken";
            }
            else if (holdTurns >= s.MaxHoldTurns && pulseReady && d.Passing > 0f)
            {
                d.Fire = true; // never sit on weapons forever, small arsenals included
                d.Reason = "max-hold";
            }
            else
            {
                d.Reason = weapons < s.MinWeapons ? "building" : saturated ? "pulse-wait" : "below-saturation";
            }
            return d;
        }
    }
}
