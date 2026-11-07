using System;

namespace InterplanetaryAI.Brain
{
    // v0.9: weapon upgrades. The AI never bought one (31 Oct, six matches): the director only priced economy upgrades.
    // An AI at war is limited by firing energy (weapon upkeep is capped at a share of income), so a weapon is worth its
    // damage per firing energy. Population damage is weighted up: cities at 0 population decide the game.
    public struct WeaponProfile
    {
        public float BuildingDamage, PopulationDamage, Aoe, FiringEnergy;
        public int Fragments, Projectiles;
        public bool LockOn;
    }

    public struct UpgradeEffect
    {
        public float BuildingDamageMul, PopulationDamageMul, AoeMul, FiringMul;
        public int FragmentAdd, ProjectileAdd;

        public static UpgradeEffect None()
        {
            return new UpgradeEffect { BuildingDamageMul = 1f, PopulationDamageMul = 1f, AoeMul = 1f, FiringMul = 1f };
        }
    }

    public static class WeaponUpgrades
    {
        // Damage per firing energy, same damage proxy as the weapon pick (DirectorPatches.TryWeapon).
        public static float Efficiency(WeaponProfile w, float populationWeight)
        {
            float damage = (w.BuildingDamage + populationWeight * w.PopulationDamage)
                * Math.Max(1, w.Fragments) * Math.Max(1, w.Projectiles)
                * (0.5f + w.Aoe / 6f) * (w.LockOn ? 1.5f : 1f);
            return damage / Math.Max(1f, w.FiringEnergy);
        }

        public static WeaponProfile Apply(WeaponProfile w, UpgradeEffect u)
        {
            return new WeaponProfile
            {
                BuildingDamage = w.BuildingDamage * u.BuildingDamageMul,
                PopulationDamage = w.PopulationDamage * u.PopulationDamageMul,
                Aoe = w.Aoe * u.AoeMul,
                FiringEnergy = w.FiringEnergy * u.FiringMul,
                Fragments = Math.Max(1, Math.Max(1, w.Fragments) + u.FragmentAdd),
                Projectiles = Math.Max(1, Math.Max(1, w.Projectiles) + u.ProjectileAdd),
                LockOn = w.LockOn,
            };
        }

        // Damage per shot after / before (ignores firing cost): below 1 means the upgrade loses damage right away and only
        // pays if the freed firing energy buys more weapons (Terra v0.9: EnergyCostDown cuts damage to 0.8).
        public static float ImmediateDamageRatio(WeaponProfile current, UpgradeEffect u, float populationWeight)
        {
            WeaponProfile after = Apply(current, u);
            float before = Efficiency(current, populationWeight) * Math.Max(1f, current.FiringEnergy);
            if (before <= 0f) return 1f;
            return Efficiency(after, populationWeight) * Math.Max(1f, after.FiringEnergy) / before;
        }

        // Relative gain in damage per firing energy (0.6 = +60%).
        public static float Gain(WeaponProfile current, UpgradeEffect u, float populationWeight)
        {
            float before = Efficiency(current, populationWeight);
            if (before <= 0f) return 0f;
            return Efficiency(Apply(current, u), populationWeight) / before - 1f;
        }
    }

    public static class ScienceValue
    {
        // v0.9: science is worth more while the research plan is behind schedule. AI science sat at 0.18 per
        // population (no multiplier at all) against the human's 2-4x; it never bought a science project because
        // science was valued at 1.5 energy. Required pace = remaining plan science / turns left to targetTurn (at least 5);
        // weight = base x clamp(required / current, 1, maxMultiplier).
        public static float Weight(float baseWeight, float remainingPlanScience, float sciencePerTurn, int turn, int targetTurn, float maxMultiplier)
        {
            if (remainingPlanScience <= 0f) return baseWeight;
            int turnsLeft = Math.Max(5, targetTurn - turn);
            float required = remainingPlanScience / turnsLeft;
            float ratio = required / Math.Max(1f, sciencePerTurn);
            return baseWeight * Math.Max(1f, Math.Min(Math.Max(1f, maxMultiplier), ratio));
        }
    }

    // v0.14: trajectory search steps per frame for each AI weapon, as AIWeaponTargeting.BeginTurn computes them from the
    // total processor budget (the game: 1200 over all AIs): clamp(total / all AI weapons, 30, 100).
    public static class AimBudget
    {
        public const int MinSteps = 30, MaxSteps = 100;

        public static int StepsPerWeapon(int totalBudget, int totalAiWeapons)
        {
            if (totalAiWeapons <= 0) return MinSteps;
            int steps = (int)Math.Round(totalBudget / (double)totalAiWeapons); // Mathf.RoundToInt: half to even
            return Math.Max(MinSteps, Math.Min(MaxSteps, steps));
        }
    }

    // v0.18 (match 21, docs/playtests/2026-11-07-v0.17.1-match21.md): on several firing turns only 4-13 of ~30 weapons found
    // a trajectory onto the human. The search runs until the AI's window (AimingWindowSeconds) has passed and the human
    // has ended the turn; a quick human turn (40-60 s late in the match) cut it short. The window is pushed out a little at a
    // time while the search is still finding trajectories, up to a hard cap; weapons that cannot reach (wrong side of the
    // planet) stop finding anything, so the stall test ends the wait instead of holding the human for nothing.
    public static class AimWindow
    {
        // The AI's timeout after this frame: unchanged, or pushed `step` seconds past the timer.
        // timer: seconds since the AI's turn began; timeout: the current window; lastProgress: timer when the count of
        // weapons with a trajectory last rose; found / live: weapons with a trajectory / weapons searching.
        public static float Extend(float timer, float timeout, float lastProgress, int found, int live, float targetShare,
            float stallSeconds, float maxWindow, float step)
        {
            if (live <= 0 || maxWindow <= timeout) return timeout;
            if (timer < timeout - Math.Max(0f, step)) return timeout;            // not near the end yet
            if (timer >= maxWindow) return timeout;                                // hard cap
            if (found >= (int)Math.Ceiling(Clamp01(targetShare) * live)) return timeout; // enough aimed
            if (timer - lastProgress >= Math.Max(0f, stallSeconds)) return timeout; // stall budget spent (often geometry; not proof)
            return Math.Min(maxWindow, timer + Math.Max(0.5f, step));
        }

        private static float Clamp01(float v)
        {
            return Math.Max(0f, Math.Min(1f, v));
        }
    }
}
