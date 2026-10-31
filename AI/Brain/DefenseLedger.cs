using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // What a player observes about shots reaching a planet: as the defender (its own defense rolls, energy spent,
    // impacts no matching defense could answer) and as the shooter (how many of its shots at one particular
    // opponent were intercepted, per weapon). v0.5 could not tell if its 21 Kinetic Defenses were stopping anything;
    // the 31 Oct 1v1 logs recorded the human's shots but no interception outcomes.
    //
    // Counting is per action phase: Roll(player) is called at that player's turn start and turns the shots since
    // its previous turn start (the action phase in between) into "last turn" figures.
    public sealed class DefenderStats
    {
        public int Impacts;            // shots reaching this planet
        public int NoEligibleDefense;  // no normal, matching defense in range: nothing could be rolled
        public int NearCity;           // impact within a short distance of one of our cities
        public int NoDefenseNearby;    // impact with no defense of any kind close by (the planet is simply not covered there)
        public int Intercepted;
        public int ProbabilityFails;
        public int NoResource;         // a defense in range could not roll: not enough energy OR material
        public int Disconnected;       // a defense in range had no power
        public int EnergySpent;
        public readonly Dictionary<string, int> ImpactsByWeapon = new Dictionary<string, int>(); // what hit us (shown in playback)
    }

    public sealed class DefenseLedger
    {
        private readonly Dictionary<int, DefenderStats> m_pendingDefender = new Dictionary<int, DefenderStats>();
        private readonly Dictionary<int, DefenderStats> m_lastDefender = new Dictionary<int, DefenderStats>();
        // shooter -> "defenderId|weapon" -> [impacts, intercepted]
        private readonly Dictionary<int, Dictionary<string, int[]>> m_pendingShooter = new Dictionary<int, Dictionary<string, int[]>>();
        private readonly Dictionary<int, List<Dictionary<string, int[]>>> m_shooterHistory = new Dictionary<int, List<Dictionary<string, int[]>>>();
        private readonly int m_historyTurns;

        public DefenseLedger(int historyTurns)
        {
            m_historyTurns = Math.Max(1, historyTurns);
        }

        public void Clear()
        {
            m_pendingDefender.Clear();
            m_lastDefender.Clear();
            m_pendingShooter.Clear();
            m_shooterHistory.Clear();
        }

        public void Record(int defenderId, int shooterId, string weaponId, bool intercepted, int probabilityFails, int noResource, int disconnected,
            int energySpent, bool eligibleDefenseInRange, bool nearCity = false, bool anyDefenseNearby = true)
        {
            DefenderStats d;
            if (!m_pendingDefender.TryGetValue(defenderId, out d))
            {
                d = new DefenderStats();
                m_pendingDefender[defenderId] = d;
            }
            d.Impacts++;
            int byWeapon;
            d.ImpactsByWeapon.TryGetValue(weaponId ?? "?", out byWeapon);
            d.ImpactsByWeapon[weaponId ?? "?"] = byWeapon + 1;
            if (!eligibleDefenseInRange) d.NoEligibleDefense++;
            if (nearCity) d.NearCity++;
            if (!anyDefenseNearby) d.NoDefenseNearby++;
            if (intercepted) d.Intercepted++;
            d.ProbabilityFails += probabilityFails;
            d.NoResource += noResource;
            d.Disconnected += disconnected;
            d.EnergySpent += energySpent;

            if (defenderId == shooterId) return; // shots at one's own planet say nothing about an opponent's defenses
            Dictionary<string, int[]> weapons;
            if (!m_pendingShooter.TryGetValue(shooterId, out weapons))
            {
                weapons = new Dictionary<string, int[]>();
                m_pendingShooter[shooterId] = weapons;
            }
            string key = defenderId + "|" + (weaponId ?? "?");
            int[] counts;
            if (!weapons.TryGetValue(key, out counts))
            {
                counts = new int[2];
                weapons[key] = counts;
            }
            counts[0]++;
            if (intercepted) counts[1]++;
        }

        // Call at the start of this player's turn.
        public void Roll(int playerId)
        {
            DefenderStats d;
            if (m_pendingDefender.TryGetValue(playerId, out d)) m_lastDefender[playerId] = d;
            else m_lastDefender[playerId] = new DefenderStats();
            m_pendingDefender[playerId] = new DefenderStats();

            Dictionary<string, int[]> weapons;
            if (!m_pendingShooter.TryGetValue(playerId, out weapons)) weapons = new Dictionary<string, int[]>();
            List<Dictionary<string, int[]>> history;
            if (!m_shooterHistory.TryGetValue(playerId, out history))
            {
                history = new List<Dictionary<string, int[]>>();
                m_shooterHistory[playerId] = history;
            }
            history.Add(weapons);
            while (history.Count > m_historyTurns) history.RemoveAt(0);
            m_pendingShooter[playerId] = new Dictionary<string, int[]>();
        }

        public DefenderStats LastDefender(int playerId)
        {
            DefenderStats d;
            return m_lastDefender.TryGetValue(playerId, out d) ? d : new DefenderStats();
        }

        // This shooter's recent shots of one weapon at one defender over the history window: [impacts, intercepted].
        public int[] ObservedCounts(int shooterId, int defenderId, string weaponId)
        {
            int[] total = new int[2];
            List<Dictionary<string, int[]>> history;
            if (!m_shooterHistory.TryGetValue(shooterId, out history)) return total;
            string key = defenderId + "|" + weaponId;
            foreach (Dictionary<string, int[]> turn in history)
            {
                int[] counts;
                if (turn.TryGetValue(key, out counts))
                {
                    total[0] += counts[0];
                    total[1] += counts[1];
                }
            }
            return total;
        }

        // Share of the shots that got through, false when fewer than minImpacts were seen (too little evidence).
        public bool ObservedPass(int shooterId, int defenderId, string weaponId, int minImpacts, out float pass)
        {
            pass = 1f;
            int[] counts = ObservedCounts(shooterId, defenderId, weaponId);
            if (counts[0] < Math.Max(1, minImpacts)) return false;
            pass = 1f - counts[1] / (float)counts[0];
            return true;
        }

        // Every weapon id this shooter has used on this defender within the window, with its counts.
        public Dictionary<string, int[]> ObservedAll(int shooterId, int defenderId, int minImpacts)
        {
            Dictionary<string, int[]> result = new Dictionary<string, int[]>();
            List<Dictionary<string, int[]>> history;
            if (!m_shooterHistory.TryGetValue(shooterId, out history)) return result;
            string prefix = defenderId + "|";
            HashSet<string> weapons = new HashSet<string>();
            foreach (Dictionary<string, int[]> turn in history)
            {
                foreach (string key in turn.Keys)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal)) weapons.Add(key.Substring(prefix.Length));
                }
            }
            foreach (string weapon in weapons)
            {
                int[] counts = ObservedCounts(shooterId, defenderId, weapon);
                if (counts[0] >= Math.Max(1, minImpacts)) result[weapon] = counts;
            }
            return result;
        }
    }
}
