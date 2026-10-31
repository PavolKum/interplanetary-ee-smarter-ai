using System;
using System.Collections.Generic;

namespace InterplanetaryAI.Brain
{
    // v0.3 economy director: the pure decision rules. The game adapter (DirectorPatches) turns the AI's
    // state into options; these functions pick one. Facts behind the rules (31 Oct 2026 playtest + ruleset):
    //  - Energy resets every turn (PlayerResourceHandler.TurnStartInit), material accumulates. Energy is the
    //    per-turn build throughput; material is the stock.
    //  - Yield upgrades (x1.75, then x2: 3.5x together) cost 50 energy and 150-250 material and need no power
    //    slot. They are the best investment in the game, and the native AI almost never buys them.
    //  - The human who won kept energy income at ~0.5-0.65x material income while booming, built no
    //    weapons until ~turn 25, and countered visible weapons with matching defenses.

    public enum OptionKind
    {
        Upgrade,
        Project,
        Plant,     // energy building that distributes power (gives power slots)
        Producer,  // material building (needs a power slot)
    }

    public sealed class EconOption
    {
        public string Id;
        public OptionKind Kind;
        public int Energy;
        public int Material;
        public float EnergyGain;
        public float MaterialGain;
        public float ScienceGain;
        public bool NeedsSlot;
        public object Payload;  // adapter data (BuildingData, UpgradeData, AIProjectEntity)
        public object Target;   // adapter data (the building an upgrade goes on)

        public override string ToString()
        {
            return Kind + ":" + Id;
        }
    }

    public sealed class EconomyBalance
    {
        public float EnergyIncome;    // per turn, including this turn's queued gains
        public float MaterialIncome;  // per turn, including this turn's queued gains
        public float WeaponUpkeep;    // energy reserved each turn for weapon firing
        public float TargetRatio = 0.6f;
        public int EnergyBudget;      // usable energy left this turn
        public int MaterialBudget;    // usable material left this turn
        public bool EnergyPush;       // e.g. superweapon material saved, energy short
    }

    public sealed class WeaponStats
    {
        public string BuildingId;
        public string WeaponId;
        public int BuildEnergy;
        public int BuildMaterial;
        public int FiringEnergy;
        public float Damage;  // (building + population damage) x fragments x projectiles
    }

    public static class EconomyDirector
    {
        // Which resource is short against the target ratio decides the weights.
        public static void Weights(EconomyBalance b, out float energyWeight, out float materialWeight)
        {
            float wantedEnergy = b.TargetRatio * b.MaterialIncome + b.WeaponUpkeep;
            if (b.EnergyPush || b.EnergyIncome < wantedEnergy)
            {
                energyWeight = 1f;
                materialWeight = 0.5f;
            }
            else
            {
                energyWeight = 0.5f;
                materialWeight = 1f;
            }
        }

        // Energy is only "expensive" when this turn's energy budget is the tighter one.
        public static float EnergyCostFactor(EconomyBalance b)
        {
            return b.MaterialBudget >= 1.3f * b.EnergyBudget ? 1f : 0.3f;
        }

        public static float Score(EconOption o, float energyWeight, float materialWeight, float energyCostFactor)
        {
            float value = energyWeight * o.EnergyGain + materialWeight * o.MaterialGain + 0.5f * o.ScienceGain;
            float cost = o.Material + energyCostFactor * o.Energy;
            return value / Math.Max(1f, cost);
        }

        // The best affordable option with a positive score, or null.
        public static EconOption Best(IList<EconOption> options, EconomyBalance b, int slotsLeft)
        {
            float wE, wM;
            Weights(b, out wE, out wM);
            float eFactor = EnergyCostFactor(b);
            EconOption best = null;
            float bestScore = 0f;
            foreach (EconOption o in options)
            {
                if (o.Energy > b.EnergyBudget || o.Material > b.MaterialBudget) continue;
                if (o.NeedsSlot && slotsLeft <= 0) continue;
                float score = Score(o, wE, wM, eFactor);
                if (score > bestScore)
                {
                    best = o;
                    bestScore = score;
                }
            }
            return best;
        }

        public static bool WarPhase(int turn, int warFromTurn, float recentDamage, float damageTrigger)
        {
            return turn >= warFromTurn || recentDamage >= damageTrigger;
        }

        // Defenses wanted: a share of the visible enemy weapons plus a response to damage taken.
        public static int DesiredDefenses(int visibleEnemyWeapons, float recentDamage, float perWeapon, float damagePerDefense, int cap)
        {
            float wanted = perWeapon * visibleEnemyWeapons + (damagePerDefense > 0f ? recentDamage / damagePerDefense : 0f);
            return Math.Min(cap, (int)Math.Round(wanted));
        }

        // Chance a weapon gets through the target's defenses. coverageFull = defenses of one type that cover
        // the whole planet. stop = interception chance by defense id for this weapon.
        public static float PassChance(string weaponId, Dictionary<string, int> targetDefenses, Dictionary<string, Dictionary<string, float>> counters, float coverageFull)
        {
            float pass = 1f;
            foreach (KeyValuePair<string, int> pair in targetDefenses)
            {
                Dictionary<string, float> stops;
                float stop;
                if (!counters.TryGetValue(pair.Key, out stops) || !stops.TryGetValue(weaponId, out stop)) continue;
                float coverage = Math.Min(1f, pair.Value / Math.Max(1f, coverageFull));
                pass *= 1f - coverage * stop;
            }
            return pass;
        }

        public static float WeaponScore(WeaponStats w, Dictionary<string, int> targetDefenses, Dictionary<string, Dictionary<string, float>> counters, float coverageFull, float firingCostTurns)
        {
            float pass = PassChance(w.WeaponId, targetDefenses, counters, coverageFull);
            float cost = w.BuildMaterial + 0.5f * w.BuildEnergy + firingCostTurns * w.FiringEnergy;
            return w.Damage * pass / Math.Max(1f, cost);
        }

        // Human-like variety: pick among the weapons scoring at least `band` of the best, weighted by score squared.
        public static WeaponStats PickWeapon(IList<WeaponStats> weapons, Dictionary<string, int> targetDefenses, Dictionary<string, Dictionary<string, float>> counters,
            float coverageFull, float firingCostTurns, float band, Random rng)
        {
            if (weapons.Count == 0) return null;
            float[] scores = new float[weapons.Count];
            float best = 0f;
            for (int i = 0; i < weapons.Count; i++)
            {
                scores[i] = WeaponScore(weapons[i], targetDefenses, counters, coverageFull, firingCostTurns);
                best = Math.Max(best, scores[i]);
            }
            if (best <= 0f) return null;
            float total = 0f;
            for (int i = 0; i < scores.Length; i++)
            {
                scores[i] = scores[i] >= band * best ? scores[i] * scores[i] : 0f;
                total += scores[i];
            }
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < scores.Length; i++)
            {
                roll -= scores[i];
                if (roll <= 0 && scores[i] > 0f) return weapons[i];
            }
            for (int i = scores.Length - 1; i >= 0; i--)
            {
                if (scores[i] > 0f) return weapons[i];
            }
            return null;
        }

        // Value of material income when the planet is running out: mines stop yielding at zero planet material.
        public static float PlanetMaterialFactor(int planetMaterial, float planetMaterialUsePerTurn, int horizonTurns)
        {
            if (planetMaterialUsePerTurn <= 0f) return 1f;
            float turnsLeft = planetMaterial / planetMaterialUsePerTurn;
            return Math.Max(0f, Math.Min(1f, turnsLeft / Math.Max(1, horizonTurns)));
        }
    }
}
