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
        Counterintel = 4,
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
        // firepower, energy, material, defense, counterintel
        public float[] Growth = { 1.5f, 3.5f, 3f, 0.7f, 0.3f };
        public float[] Pressure = { 2.5f, 2.5f, 2f, 1f, 0.8f };
        public int GrowthUntilTurn = 15;
        public float DefenseBonusWhenHit = 1.5f;
    }

    public static class BuildPlanner
    {
        // Growth is the opening only. "Grow while behind the leader" was tried and rejected: against a human
        // who out-booms the AIs it keeps them building economy all game and never punishing the boom.
        public static bool IsGrowthPhase(BuildSituation s, BuildWeights w)
        {
            return s.Turn < w.GrowthUntilTurn;
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
            if (parts.Length != 5) return fallback;
            float[] values = new float[5];
            for (int i = 0; i < 5; i++)
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

        // v0.11: techs to research before the plan because a known enemy weapon has no counter-defense we can build.
        // v0.10.1: 14 human Lasers from T32; the Shield tech (Ultrasonic Projection) was not in the plan at all.
        // knownWeapons: enemy weapon id -> count; counters: defense id -> (weapon id -> stop chance);
        // defenseTechs: defense id -> unlocking tech. A weapon counts as covered when a defense stopping it at >= minChance
        // is buildable now.
        public static List<string> CounterTechs(IDictionary<string, int> knownWeapons, Dictionary<string, Dictionary<string, float>> counters,
            IDictionary<string, string> defenseTechs, Func<string, bool> isBuildable, float minChance = 0.5f)
        {
            List<string> techs = new List<string>();
            foreach (KeyValuePair<string, int> weapon in knownWeapons)
            {
                if (weapon.Value <= 0) continue;
                bool covered = false;
                string bestTech = null;
                float bestChance = 0f;
                foreach (KeyValuePair<string, Dictionary<string, float>> defense in counters)
                {
                    float p;
                    if (!defense.Value.TryGetValue(weapon.Key, out p) || p < minChance) continue;
                    if (isBuildable(defense.Key)) { covered = true; break; }
                    string tech;
                    if (defenseTechs.TryGetValue(defense.Key, out tech) && p > bestChance) { bestTech = tech; bestChance = p; }
                }
                if (!covered && bestTech != null && !techs.Contains(bestTech)) techs.Add(bestTech);
            }
            return techs;
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
