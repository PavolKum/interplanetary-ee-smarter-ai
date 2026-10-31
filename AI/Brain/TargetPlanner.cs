using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // What one AI can see of one enemy this turn. Built by the game adapter only from what the same
    // seat's intel screen shows (aggregate numbers gated by intel vs the enemy's counterintel), so the
    // brain never uses information a human in the same seat would lack.
    public sealed class EnemyView
    {
        public int PlayerId;
        public string Name;
        public bool IsHuman;
        public float Economy;        // energy + material (+ science) production per turn, when our intel shows it
        public bool EconomyKnown;    // false = our intel cannot see this enemy's production numbers
        public float CityPopulation; // total city population when our intel shows it; a city's durability (ruined at 0)
        public int VisibleCities;    // 0 = nothing known, so no finish-off bonus
        public float Grudge;         // decayed damage this enemy dealt to us

        // outputs, filled by Score()
        public float Score;
        public float LeaderTerm;
        public float GrudgeTerm;
        public float FinishTerm;
    }

    public sealed class WeaponSlot
    {
        public string WeaponKey;
        public int PreviousTargetId = -1;
        public readonly HashSet<int> UnreachableTargetIds = new HashSet<int>();
    }

    public sealed class PlannerSettings
    {
        public float FocusShare = 0.7f;   // share of weapons sent at the top-scored enemy
        public float BaseWeight = 0.1f;   // keeps every enemy pickable
        public float LeaderWeight = 1.0f; // squared economy share, so a runaway leader dominates
        public float GrudgeWeight = 0.6f;
        public float FinishWeight = 0.5f; // prefers enemies with little visible city population left
        // Economy share assumed for an enemy our intel cannot read. Unknown is not weak: a blind AI
        // spreads its fire instead of locking onto whoever hit it (31 Oct v0.3.1: all four AIs were blind
        // and grudges alone turned them on each other).
        public float UnknownEconomyShare = 0.8f;
        // Focus fire scales with how clearly the top enemy leads: none below FocusRatioLow x the second score,
        // full FocusShare from FocusRatioHigh x. A narrow lead (a grudge among enemies we cannot see) spreads fire
        // instead of starting a vendetta; a runaway leader still draws the focus.
        public float FocusRatioLow = 1.5f;
        public float FocusRatioHigh = 2.5f;
        public float HumanBias = 1.0f;    // multiplier on human enemies; 1 = no favouritism
    }

    public static class TargetPlanner
    {
        public static void Score(IList<EnemyView> enemies, PlannerSettings settings)
        {
            float maxEconomy = 0f;
            float maxGrudge = 0f;
            float maxCityPopulation = 0f;
            foreach (EnemyView enemy in enemies)
            {
                if (enemy.EconomyKnown) maxEconomy = Math.Max(maxEconomy, enemy.Economy);
                maxGrudge = Math.Max(maxGrudge, enemy.Grudge);
                maxCityPopulation = Math.Max(maxCityPopulation, enemy.CityPopulation);
            }

            foreach (EnemyView enemy in enemies)
            {
                float economyShare = !enemy.EconomyKnown ? settings.UnknownEconomyShare
                    : maxEconomy > 0f ? enemy.Economy / maxEconomy : 0f;
                float grudgeShare = maxGrudge > 0f ? enemy.Grudge / maxGrudge : 0f;
                // An enemy with no visible city is unknown, not weak: no finish-off bonus.
                float populationShare = enemy.VisibleCities > 0 && maxCityPopulation > 0f ? enemy.CityPopulation / maxCityPopulation : 1f;

                enemy.LeaderTerm = settings.LeaderWeight * economyShare * economyShare;
                enemy.GrudgeTerm = settings.GrudgeWeight * grudgeShare;
                enemy.FinishTerm = settings.FinishWeight * (1f - populationShare);

                float score = settings.BaseWeight + enemy.LeaderTerm + enemy.GrudgeTerm + enemy.FinishTerm;
                if (enemy.IsHuman)
                {
                    score *= settings.HumanBias;
                }
                enemy.Score = Math.Max(score, 0.0001f);
            }
        }

        // Returns, for each weapon, an index into enemies. The first FocusShare of weapons go at the
        // top-scored enemy; the rest draw at random weighted by score. A weapon that failed to find
        // any trajectory onto its target recently is moved to the best enemy it can still reach.
        public static int[] Assign(IList<EnemyView> enemies, IList<WeaponSlot> weapons, PlannerSettings settings, Random rng)
        {
            int[] result = new int[weapons.Count];
            if (enemies.Count == 0)
            {
                for (int i = 0; i < result.Length; i++) result[i] = -1;
                return result;
            }

            List<int> byScore = new List<int>();
            for (int i = 0; i < enemies.Count; i++) byScore.Add(i);
            byScore.Sort((a, b) => enemies[b].Score.CompareTo(enemies[a].Score));
            int top = byScore[0];

            float topScore = enemies[top].Score;
            float secondScore = byScore.Count > 1 ? enemies[byScore[1]].Score : 0f;
            float ratio = secondScore > 0f ? topScore / secondScore : float.MaxValue;
            float span = Math.Max(1e-3f, settings.FocusRatioHigh - settings.FocusRatioLow);
            float focus = settings.FocusShare * Math.Max(0f, Math.Min(1f, (ratio - settings.FocusRatioLow) / span));
            int focusCount = (int)Math.Round(focus * weapons.Count, MidpointRounding.AwayFromZero);
            for (int w = 0; w < weapons.Count; w++)
            {
                int wanted = w < focusCount ? top : WeightedPick(enemies, rng);
                result[w] = ReachableOrBest(wanted, byScore, enemies, weapons[w]);
            }
            return result;
        }

        private static int WeightedPick(IList<EnemyView> enemies, Random rng)
        {
            float total = 0f;
            foreach (EnemyView enemy in enemies) total += enemy.Score;
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < enemies.Count; i++)
            {
                roll -= enemies[i].Score;
                if (roll <= 0f) return i;
            }
            return enemies.Count - 1;
        }

        private static int ReachableOrBest(int wanted, List<int> byScore, IList<EnemyView> enemies, WeaponSlot weapon)
        {
            if (!weapon.UnreachableTargetIds.Contains(enemies[wanted].PlayerId)) return wanted;
            foreach (int candidate in byScore)
            {
                if (!weapon.UnreachableTargetIds.Contains(enemies[candidate].PlayerId)) return candidate;
            }
            return wanted; // nothing known reachable: keep the plan, the search may still succeed
        }
    }
}
