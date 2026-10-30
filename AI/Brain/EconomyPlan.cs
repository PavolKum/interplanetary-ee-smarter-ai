using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // Saving toward one expensive purchase (a superweapon). The native AI drops any need larger than
    // 1.65x its stock (AIConstructionModule.handleUnaffordableBuilding), so it never buys one.
    public static class Savings
    {
        // Each turn the reserve keeps everything already saved and adds a share of the new stock, so it
        // converges on the goal instead of stalling below it (a flat share of stock would cap at share/(1-share) x income).
        public static int NextReserve(int previousReserve, int stock, int goalCost, float saveShare)
        {
            if (goalCost <= 0 || stock <= 0) return 0;
            saveShare = Math.Max(0f, Math.Min(1f, saveShare));
            int kept = Math.Min(previousReserve, stock);
            int added = (int)Math.Round((stock - kept) * saveShare);
            return Math.Min(goalCost, Math.Min(stock, kept + added));
        }

        public static bool CanBuy(int energy, int material, int energyCost, int materialCost)
        {
            return energy >= energyCost && material >= materialCost;
        }
    }

    public enum BuildCategory
    {
        Firepower = 0,
        Energy = 1,
        Material = 2,
        Defense = 3,
    }

    public sealed class BuildSituation
    {
        public int Turn;
        public float OwnEconomy;      // own energy + material production per turn
        public float MaxEnemyEconomy; // strongest visible enemy economy (same measure)
        public float RecentDamage;    // decayed damage taken from all enemies
    }

    public sealed class BuildWeights
    {
        public float[] Growth = { 1.5f, 3.5f, 3f, 0.7f };   // firepower, energy, material, defense
        public float[] Pressure = { 3f, 2f, 1.5f, 1f };
        public int GrowthUntilTurn = 15;
        public float BehindRatio = 0.9f;                     // growth while own economy < ratio x strongest enemy
        public float DefenseBonusWhenHit = 1.5f;
    }

    public static class BuildPlanner
    {
        public static bool IsGrowthPhase(BuildSituation s, BuildWeights w)
        {
            return s.Turn < w.GrowthUntilTurn || s.OwnEconomy < w.BehindRatio * s.MaxEnemyEconomy;
        }

        public static float[] Weights(BuildSituation s, BuildWeights w)
        {
            float[] source = IsGrowthPhase(s, w) ? w.Growth : w.Pressure;
            float[] weights = (float[])source.Clone();
            if (s.RecentDamage > 0f) weights[(int)BuildCategory.Defense] += w.DefenseBonusWhenHit;
            return weights;
        }

        public static BuildCategory Pick(float[] weights, Random rng)
        {
            float total = 0f;
            foreach (float weight in weights) total += Math.Max(0f, weight);
            float roll = (float)rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= Math.Max(0f, weights[i]);
                if (roll <= 0f) return (BuildCategory)i;
            }
            return (BuildCategory)(weights.Length - 1);
        }

        public static float[] Parse(string text, float[] fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            string[] parts = text.Split(',');
            if (parts.Length != 4) return fallback;
            float[] values = new float[4];
            for (int i = 0; i < 4; i++)
            {
                float v;
                if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v < 0f) return fallback;
                values[i] = v;
            }
            return values;
        }
    }

    public static class ResearchPlan
    {
        // First planned technology that is neither researched nor already queued.
        public static string NextTarget(IList<string> plan, Func<string, bool> isDone, Func<string, bool> isQueued)
        {
            foreach (string tech in plan)
            {
                if (!isDone(tech) && !isQueued(tech)) return tech;
            }
            return null;
        }

        public static List<string> Parse(string text)
        {
            List<string> plan = new List<string>();
            if (string.IsNullOrEmpty(text)) return plan;
            foreach (string part in text.Split(','))
            {
                string id = part.Trim();
                if (id.Length > 0 && !plan.Contains(id)) plan.Add(id);
            }
            return plan;
        }
    }
}
