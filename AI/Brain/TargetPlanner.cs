using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // What one AI can see of one enemy this turn. Built by the game adapter from intel-visible
    // buildings only, so the brain never uses information a human in the same seat would lack.
    public sealed class EnemyView
    {
        public int PlayerId;
        public string Name;
        public bool IsHuman;
        public float Economy;        // visible energy + material + science generation per turn
        public float CityHp;         // visible remaining city HP
        public int VisibleCities;
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
        public float FinishWeight = 0.5f; // prefers enemies with little city HP left
        public float HumanBias = 1.0f;    // multiplier on human enemies; 1 = no favouritism
    }

    public static class TargetPlanner
    {
        public static void Score(IList<EnemyView> enemies, PlannerSettings settings)
        {
            float maxEconomy = 0f;
            float maxGrudge = 0f;
            float maxCityHp = 0f;
            foreach (EnemyView enemy in enemies)
            {
                maxEconomy = Math.Max(maxEconomy, enemy.Economy);
                maxGrudge = Math.Max(maxGrudge, enemy.Grudge);
                maxCityHp = Math.Max(maxCityHp, enemy.CityHp);
            }

            foreach (EnemyView enemy in enemies)
            {
                float economyShare = maxEconomy > 0f ? enemy.Economy / maxEconomy : 0f;
                float grudgeShare = maxGrudge > 0f ? enemy.Grudge / maxGrudge : 0f;
                float hpShare = maxCityHp > 0f ? enemy.CityHp / maxCityHp : 1f;

                enemy.LeaderTerm = settings.LeaderWeight * economyShare * economyShare;
                enemy.GrudgeTerm = settings.GrudgeWeight * grudgeShare;
                enemy.FinishTerm = settings.FinishWeight * (1f - hpShare);

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

            int focusCount = (int)Math.Round(settings.FocusShare * weapons.Count, MidpointRounding.AwayFromZero);
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
