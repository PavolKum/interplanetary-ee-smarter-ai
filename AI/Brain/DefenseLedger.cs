using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // What a player observes about shots reaching a planet: as the defender (its own defense rolls, energy spent,
    // uncovered impacts) and as the shooter (how many of its shots at a planet were intercepted, per weapon).
    // Both are events the game shows the player involved. v0.5 could not tell if its 21 Kinetic Defenses were
    // stopping anything; the 31 Oct 1v1 logs recorded the human's shots but no interception outcomes.
    //
    // Counting is per action phase: Roll(player) is called at that player's turn start and turns the shots since
    // its previous turn start (the action phase in between) into "last turn" figures.
    public sealed class DefenderStats
    {
        public int Impacts;       // shots reaching this planet
        public int Uncovered;     // no defense in range: nothing could be rolled
        public int Intercepted;
        public int ProbabilityFails;
        public int NoEnergy;      // a defense in range could not roll: not enough energy
        public int Disconnected;  // a defense in range had no power
        public int EnergySpent;
    }

    public sealed class DefenseLedger
    {
        private readonly Dictionary<int, DefenderStats> m_pendingDefender = new Dictionary<int, DefenderStats>();
        private readonly Dictionary<int, DefenderStats> m_lastDefender = new Dictionary<int, DefenderStats>();
        // shooter -> weapon -> [impacts, intercepted]
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

        public void Record(int defenderId, int shooterId, string weaponId, bool intercepted, int probabilityFails, int noEnergy, int disconnected,
            int energySpent, bool covered)
        {
            DefenderStats d;
            if (!m_pendingDefender.TryGetValue(defenderId, out d))
            {
                d = new DefenderStats();
                m_pendingDefender[defenderId] = d;
            }
            d.Impacts++;
            if (!covered) d.Uncovered++;
            if (intercepted) d.Intercepted++;
            d.ProbabilityFails += probabilityFails;
            d.NoEnergy += noEnergy;
            d.Disconnected += disconnected;
            d.EnergySpent += energySpent;

            Dictionary<string, int[]> weapons;
            if (!m_pendingShooter.TryGetValue(shooterId, out weapons))
            {
                weapons = new Dictionary<string, int[]>();
                m_pendingShooter[shooterId] = weapons;
            }
            int[] counts;
            if (!weapons.TryGetValue(weaponId ?? "?", out counts))
            {
                counts = new int[2];
                weapons[weaponId ?? "?"] = counts;
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

        // Share of this shooter's recent shots of one weapon that got through (not intercepted) over the history
        // window; false when fewer than minImpacts were seen (too little evidence).
        public bool ObservedPass(int shooterId, string weaponId, int minImpacts, out float pass)
        {
            pass = 1f;
            List<Dictionary<string, int[]>> history;
            if (!m_shooterHistory.TryGetValue(shooterId, out history)) return false;
            int impacts = 0, intercepted = 0;
            foreach (Dictionary<string, int[]> turn in history)
            {
                int[] counts;
                if (turn.TryGetValue(weaponId, out counts))
                {
                    impacts += counts[0];
                    intercepted += counts[1];
                }
            }
            if (impacts < minImpacts) return false;
            pass = 1f - intercepted / (float)impacts;
            return true;
        }

        // Observed pass for every weapon id with enough evidence.
        public Dictionary<string, float> ObservedPassAll(int shooterId, int minImpacts)
        {
            Dictionary<string, float> result = new Dictionary<string, float>();
            List<Dictionary<string, int[]>> history;
            if (!m_shooterHistory.TryGetValue(shooterId, out history)) return result;
            HashSet<string> ids = new HashSet<string>();
            foreach (Dictionary<string, int[]> turn in history) foreach (string id in turn.Keys) ids.Add(id);
            foreach (string id in ids)
            {
                float pass;
                if (ObservedPass(shooterId, id, minImpacts, out pass)) result[id] = pass;
            }
            return result;
        }
    }
}
