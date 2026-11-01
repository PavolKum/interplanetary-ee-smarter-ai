using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // v0.13 repair. The native AI never repairs (AIActions.RepairBuilding is an empty stub with no caller), so damage on AI
    // buildings only accumulates, while a human repairs between strikes (v0.12.1: 11 AI strikes, the human never lost a
    // building). A repair restores full HP for a flat cost (repair_cost_energy 100 + repair_cost_material 100-1000).
    public sealed class RepairCandidate
    {
        public int Priority;     // lower first: 0 weapons, 1 defenses, 2 power, 3 intel, 4 mines, 5 other
        public float HpShare;    // current / max HP
        public int Energy;
        public int Material;
        public object Payload;   // adapter data (the building)
    }

    public static class RepairPlanner
    {
        // Damaged at or below maxHpShare, by priority then most damaged; as many as the budgets and maxCount allow.
        // A candidate that does not fit is skipped (a cheaper one later in the order may still fit).
        public static List<RepairCandidate> Pick(IList<RepairCandidate> candidates, int energyBudget, int materialBudget, int maxCount, float maxHpShare)
        {
            List<RepairCandidate> eligible = new List<RepairCandidate>();
            foreach (RepairCandidate c in candidates)
            {
                if (c != null && c.HpShare < 1f && c.HpShare <= maxHpShare) eligible.Add(c);
            }
            eligible.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.HpShare.CompareTo(b.HpShare));
            List<RepairCandidate> picked = new List<RepairCandidate>();
            int energy = Math.Max(0, energyBudget), material = Math.Max(0, materialBudget);
            foreach (RepairCandidate c in eligible)
            {
                if (picked.Count >= maxCount) break;
                if (c.Energy > energy || c.Material > material) continue;
                energy -= c.Energy;
                material -= c.Material;
                picked.Add(c);
            }
            return picked;
        }
    }
}
