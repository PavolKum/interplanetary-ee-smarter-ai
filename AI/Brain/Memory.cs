using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // Who hurt whom, decayed per turn. The native AI has no such memory (its "vengeful threat" is a stub).
    public sealed class GrudgeLedger
    {
        private sealed class Entry
        {
            public float Value;
            public int Turn;
        }

        private readonly Dictionary<long, Entry> m_entries = new Dictionary<long, Entry>();
        private readonly float m_decayPerTurn;

        public GrudgeLedger(float decayPerTurn)
        {
            m_decayPerTurn = decayPerTurn;
        }

        public void Record(int victimId, int shooterId, int turn, int damage)
        {
            if (damage <= 0 || victimId == shooterId || shooterId < 0) return;
            long key = Key(victimId, shooterId);
            Entry entry;
            if (!m_entries.TryGetValue(key, out entry))
            {
                entry = new Entry { Value = 0f, Turn = turn };
                m_entries[key] = entry;
            }
            entry.Value = Decayed(entry, turn) + damage;
            entry.Turn = turn;
        }

        public float Get(int victimId, int shooterId, int turn)
        {
            Entry entry;
            if (!m_entries.TryGetValue(Key(victimId, shooterId), out entry)) return 0f;
            return Decayed(entry, turn);
        }

        public void Clear()
        {
            m_entries.Clear();
        }

        private float Decayed(Entry entry, int turn)
        {
            int age = Math.Max(0, turn - entry.Turn);
            return entry.Value * (float)Math.Pow(m_decayPerTurn, age);
        }

        private static long Key(int victimId, int shooterId)
        {
            return ((long)victimId << 32) | (uint)shooterId;
        }
    }

    // Per weapon: which targets it recently failed to find any trajectory onto. Planets orbit slowly,
    // so a target out of reach this turn is usually out of reach next turn too.
    public sealed class ReachabilityMemory
    {
        private readonly Dictionary<string, Dictionary<int, int>> m_failedTurnByWeapon = new Dictionary<string, Dictionary<int, int>>();
        private readonly int m_memoryTurns;

        public ReachabilityMemory(int memoryTurns)
        {
            m_memoryTurns = memoryTurns;
        }

        public void Report(string weaponKey, int targetId, int turn, bool foundTrajectory)
        {
            Dictionary<int, int> failed;
            if (!m_failedTurnByWeapon.TryGetValue(weaponKey, out failed))
            {
                failed = new Dictionary<int, int>();
                m_failedTurnByWeapon[weaponKey] = failed;
            }
            if (foundTrajectory) failed.Remove(targetId);
            else failed[targetId] = turn;
        }

        public void FillUnreachable(string weaponKey, int turn, HashSet<int> into)
        {
            into.Clear();
            Dictionary<int, int> failed;
            if (!m_failedTurnByWeapon.TryGetValue(weaponKey, out failed)) return;
            foreach (KeyValuePair<int, int> pair in failed)
            {
                if (turn - pair.Value <= m_memoryTurns) into.Add(pair.Key);
            }
        }

        public void Clear()
        {
            m_failedTurnByWeapon.Clear();
        }
    }

    public static class AccuracyPlan
    {
        // Native AIs pre-roll a share of deliberate misses per turn (accuracy 0.4 / 0.75 / 0.9 / 1.0 by
        // difficulty). This re-splits the same shot count at the configured accuracy; carry keeps the
        // long-run rate exact without randomness.
        public static int PlannedHits(int totalShots, float accuracy, ref float carry)
        {
            if (totalShots <= 0) return 0;
            accuracy = Math.Max(0f, Math.Min(1f, accuracy));
            float exact = totalShots * accuracy + carry;
            int hits = (int)Math.Floor(exact + 1e-4f);
            if (hits > totalShots) hits = totalShots;
            carry = exact - hits;
            if (carry < 0f) carry = 0f;
            return hits;
        }

        public static float ForLevel(float[] byLevel, int level)
        {
            if (byLevel == null || byLevel.Length == 0) return 1f;
            if (level < 0) level = 0;
            if (level >= byLevel.Length) level = byLevel.Length - 1;
            return byLevel[level];
        }
    }

    public sealed class BuildingView
    {
        public bool IsCity;
        public int CurrentHp;
        public int MaxHp;
        public int Population;
    }

    public static class StrikePlanner
    {
        // Players are defeated when their last city falls (PlayerManager.CheckPlayers), so guided
        // second-phase strikes go for the city closest to destruction; a share go for weapon buildings.
        public static int Choose(IList<BuildingView> candidates, float weaponBuildingShare, Random rng)
        {
            int weakestCity = -1;
            int weakestWeapon = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                BuildingView b = candidates[i];
                if (b.CurrentHp <= 0) continue;
                if (b.IsCity)
                {
                    if (weakestCity < 0 || b.CurrentHp < candidates[weakestCity].CurrentHp) weakestCity = i;
                }
                else if (weakestWeapon < 0 || b.CurrentHp < candidates[weakestWeapon].CurrentHp)
                {
                    weakestWeapon = i;
                }
            }
            if (weakestWeapon >= 0 && (weakestCity < 0 || rng.NextDouble() < weaponBuildingShare)) return weakestWeapon;
            return weakestCity;
        }
    }
}
