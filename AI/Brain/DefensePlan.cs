using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // Pick the defense that covers the most visible enemy firepower not yet covered by our own defenses.
    // Interception chances from docs/ruleset/DefensesData.csv (defends_against / defense_probabilities).
    public static class DefensePlanner
    {
        public static readonly Dictionary<string, Dictionary<string, float>> Counters = new Dictionary<string, Dictionary<string, float>>
        {
            { "bui_KineticDefense", new Dictionary<string, float> { { "wea_Railgun", 0.8f }, { "wea_Coilcannon", 0.8f } } },
            { "bui_IonDefense", new Dictionary<string, float> { { "wea_Missile", 0.8f }, { "wea_MIRV", 0.8f }, { "wea_NuclearMissile", 0.8f } } },
            { "bui_ShieldDefense", new Dictionary<string, float>
                {
                    { "wea_Laser", 1f }, { "wea_Railgun", 0.15f }, { "wea_Coilcannon", 0.15f },
                    { "wea_Missile", 0.15f }, { "wea_MIRV", 0.15f }, { "wea_NuclearMissile", 0.15f },
                }
            },
        };

        // enemyWeapons: visible enemy weapon buildings by weapon id. ownDefenses: our intact defenses by building id.
        // candidates: defense buildings we can build now. Returns null when nothing is threatening us.
        public static string Choose(Dictionary<string, int> enemyWeapons, Dictionary<string, int> ownDefenses, ICollection<string> candidates)
        {
            string best = null;
            float bestScore = 0f;
            foreach (string defense in candidates)
            {
                Dictionary<string, float> counters;
                if (!Counters.TryGetValue(defense, out counters)) continue;
                float threat = 0f;
                foreach (KeyValuePair<string, float> pair in counters)
                {
                    int n;
                    if (enemyWeapons.TryGetValue(pair.Key, out n)) threat += n * pair.Value;
                }
                int own;
                ownDefenses.TryGetValue(defense, out own);
                float score = threat / (1 + own);
                if (score > bestScore)
                {
                    best = defense;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
