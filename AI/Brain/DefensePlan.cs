using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // Pick the defense that covers the most known enemy firepower not yet covered by our own defenses.
    // Interception chances: the Enhanced table below until the adapter loads the active ruleset's data
    // (defends_against / defense_probabilities); Classic differs (Kinetic also stops Missiles, no Ion).
    public static class DefensePlanner
    {
        public static Dictionary<string, Dictionary<string, float>> Counters = EnhancedCounters();

        public static Dictionary<string, Dictionary<string, float>> EnhancedCounters()
        {
            return new Dictionary<string, Dictionary<string, float>>
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
        }

        // Replace the table (from the ruleset). An empty table keeps the current one.
        public static void SetCounters(Dictionary<string, Dictionary<string, float>> counters)
        {
            if (counters != null && counters.Count > 0) Counters = counters;
        }

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

        // v0.11.1 (Terra v0.11 #3): a known enemy weapon type our defenses barely stop gets its counter even when the total
        // defense count is at its target (v0.10.1: 14 human Lasers, no Shield ever built; the total cap blocked any new type).
        // Wanted per weapon: min(cap, ceil(share x count)) defenses stopping it at >= minChance. Returns the buildable defense
        // that stops the most under-countered weapon best, or null.
        public static string CounterDeficit(IDictionary<string, int> enemyWeapons, IDictionary<string, int> ownDefenses,
            ICollection<string> buildable, float share = 0.25f, int cap = 6, float minChance = 0.5f)
        {
            string best = null;
            int bestGap = 0;
            foreach (KeyValuePair<string, int> weapon in enemyWeapons)
            {
                if (weapon.Value <= 0) continue;
                int wanted = Math.Min(cap, (int)Math.Ceiling(share * weapon.Value - 1e-4f));
                int have = 0;
                string pick = null;
                float pickChance = 0f;
                foreach (KeyValuePair<string, Dictionary<string, float>> defense in Counters)
                {
                    float p;
                    if (!defense.Value.TryGetValue(weapon.Key, out p) || p < minChance) continue;
                    int own;
                    if (ownDefenses.TryGetValue(defense.Key, out own)) have += own;
                    if (buildable.Contains(defense.Key) && p > pickChance) { pick = defense.Key; pickChance = p; }
                }
                int gap = wanted - have;
                if (pick != null && gap > bestGap)
                {
                    best = pick;
                    bestGap = gap;
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

        // v0.13.1 (Sol v0.13 next move 2): weapons that hit us count as known even when their buildings stay hidden. v0.12.1:
        // the human's Lasers hit from T29 and no Shield was built, because counters looked only at visible weapon buildings.
        // Estimate: impacts / impactsPerWeapon (rounded up), remembered with the same decay as seen weapons; known[type] is
        // raised to it, never lowered.
        public static void RememberIncoming(Dictionary<string, float> memory, IDictionary<string, int> impactsByWeapon, float impactsPerWeapon,
            float decay, Dictionary<string, int> known)
        {
            foreach (string type in new List<string>(memory.Keys)) memory[type] *= decay;
            if (impactsByWeapon != null)
            {
                foreach (KeyValuePair<string, int> hit in impactsByWeapon)
                {
                    if (hit.Value <= 0 || hit.Key == null || hit.Key == "?") continue;
                    float estimate = (float)Math.Ceiling(hit.Value / Math.Max(1f, impactsPerWeapon));
                    float old;
                    memory.TryGetValue(hit.Key, out old);
                    memory[hit.Key] = Math.Max(old, estimate);
                }
            }
            foreach (KeyValuePair<string, float> pair in memory)
            {
                int n = (int)Math.Round(pair.Value);
                int have;
                known.TryGetValue(pair.Key, out have);
                if (n > have) known[pair.Key] = n;
            }
        }

        // v0.5: remember enemy weapons once seen. Counterintel hides weapons it does not destroy: the 31 Oct
        // human went dark at T21 and built 24 Railguns the AI no longer counted. Per enemy and type, keep the
        // larger of what is visible now and the memory decayed by `decay` per turn; forget enemies no longer
        // alive (elimination is public). `known` gets the per-type sum over living enemies, rounded.
        public static void Remember(Dictionary<string, float> memory, Dictionary<int, Dictionary<string, int>> visibleByEnemy,
            ICollection<int> aliveEnemies, float decay, Dictionary<string, int> known)
        {
            List<string> keys = new List<string>(memory.Keys);
            foreach (KeyValuePair<int, Dictionary<string, int>> enemy in visibleByEnemy)
            {
                foreach (string type in enemy.Value.Keys)
                {
                    string key = enemy.Key + "|" + type;
                    if (!memory.ContainsKey(key)) keys.Add(key);
                }
            }
            Dictionary<string, float> sums = new Dictionary<string, float>();
            foreach (string key in keys)
            {
                int bar = key.IndexOf('|');
                int enemyId = int.Parse(key.Substring(0, bar));
                string type = key.Substring(bar + 1);
                if (!aliveEnemies.Contains(enemyId))
                {
                    memory.Remove(key);
                    continue;
                }
                float old;
                memory.TryGetValue(key, out old);
                int now = 0;
                Dictionary<string, int> visible;
                if (visibleByEnemy.TryGetValue(enemyId, out visible)) visible.TryGetValue(type, out now);
                float value = System.Math.Max(now, old * decay);
                memory[key] = value;
                float sum;
                sums.TryGetValue(type, out sum);
                sums[type] = sum + value;
            }
            known.Clear();
            foreach (KeyValuePair<string, float> pair in sums)
            {
                int rounded = (int)System.Math.Round(pair.Value);
                if (rounded > 0) known[pair.Key] = rounded;
            }
        }
    }
}
