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

        // v0.5: no known threat but we still want defenses (the floor, or damage from weapons we cannot see).
        // Railguns are the first and most common weapon, so Kinetic leads 2:1 over Ion; Shield last.
        // v0.4.1 spread by fewest owned and built Ion against a Railgun attack (31 Oct 1v1, T27-30).
        public static readonly Dictionary<string, float> BlindWeights = new Dictionary<string, float>
        {
            { "bui_KineticDefense", 2f }, { "bui_IonDefense", 1f }, { "bui_ShieldDefense", 0.5f },
        };

        public static string ChooseBlind(Dictionary<string, int> ownDefenses, ICollection<string> candidates)
        {
            string best = null;
            float bestScore = 0f;
            foreach (string defense in candidates)
            {
                float weight;
                if (!BlindWeights.TryGetValue(defense, out weight)) continue;
                int own;
                ownDefenses.TryGetValue(defense, out own);
                float score = weight / (1 + own);
                if (score > bestScore)
                {
                    best = defense;
                    bestScore = score;
                }
            }
            return best;
        }

        // v0.5: remember enemy weapons once seen. Counterintel hides weapons it does not destroy: the 31 Oct
        // human went dark at T21 and built 24 Railguns the AI no longer counted. Each type keeps the larger of
        // what is visible now and the memory decayed by `decay` per turn. `known` gets the rounded result.
        public static void Remember(Dictionary<string, float> memory, Dictionary<string, int> visible, float decay, Dictionary<string, int> known)
        {
            List<string> types = new List<string>(memory.Keys);
            foreach (string type in visible.Keys)
            {
                if (!memory.ContainsKey(type)) types.Add(type);
            }
            known.Clear();
            foreach (string type in types)
            {
                float old;
                memory.TryGetValue(type, out old);
                int now;
                visible.TryGetValue(type, out now);
                float value = System.Math.Max(now, old * decay);
                memory[type] = value;
                int rounded = (int)System.Math.Round(value);
                if (rounded > 0) known[type] = rounded;
            }
        }
    }
}
