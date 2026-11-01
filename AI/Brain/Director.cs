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
        public bool SavesReserve; // gain is planet material saved (refinery), not production: never counted as income
        // v0.16 item 1 (council E1, B-Amendment 2): next-turn production change when it differs from the investment
        // utility above. A temporary project's utility is its gain x duration/10 (a common ten-turn horizon); its
        // production next turn is the full gain. NaN = same as EnergyGain / MaterialGain.
        public float IncomeEnergy = float.NaN;
        public float IncomeMaterial = float.NaN;

        public float NextTurnEnergy { get { return float.IsNaN(IncomeEnergy) ? EnergyGain : IncomeEnergy; } }
        public float NextTurnMaterial { get { return float.IsNaN(IncomeMaterial) ? MaterialGain : IncomeMaterial; } }
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
        public float ScienceWeight = 1f; // science per turn valued against energy/material per turn
        public bool MaterialGlut;     // v0.10: more material banked than energy can turn into anything
        public float GlutMaterialWeight = 0.25f;
        public bool EnergyGlut;       // v0.11: last turn ended with much of its energy unspent (material or space bound)
        public float GlutEnergyWeight = 0.25f; // energy cost factor in energy glut
        // v0.12: energy gains are worth this much in energy glut or energy saturation (v0.11.1: ~180 Floating Plants per AI
        // bought with the last of the planet's material while energy went unspent).
        public float GlutEnergyGainWeight = 0f;
        // v0.12: material per turn the planet can still supply to the horizon turn (planet material left / turns left);
        // -1 = unlimited planet. Planet drain is max(production, planet cost) (PlayerResourceHandler), so yield multipliers
        // only spend the fixed planet pool faster.
        public float SustainableMaterialIncome = -1f;
        public float EnergySaturation = 1.5f; // energy income above this multiple of the wanted energy is saturated
        public int MaterialReserve;           // v0.12: material the economy may not spend (planet about to run dry)
        public int SlotsLeft = 1;             // v0.14.1: free power slots left this turn (SlotStarved)
        public int PlantsQueued;              // v0.14.1: plants queued this turn
        public bool Opening;                  // v0.14.1: the first turns (SlotStarved applies)
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
            float wantedEnergy = WantedEnergy(b);
            if (b.EnergyGlut)
            {
                // v0.12: more energy is worthless when last turn's already went unspent. v0.11.1 ranked material glut first and
                // turned the last material into Floating Plants. (Energy saturation is a filter in Best, Sol v0.12 #5.)
                energyWeight = b.GlutEnergyGainWeight;
                materialWeight = b.MaterialGlut ? b.GlutMaterialWeight : 1f;
            }
            else if (b.MaterialGlut)
            {
                // v0.10: v0.9.1 Guts banked 15-37k material from T19 while energy-bound, and still preferred material
                // gains (energy income sat just above 0.6 x material income + upkeep).
                energyWeight = 1f;
                materialWeight = b.GlutMaterialWeight;
            }
            else if (b.EnergyPush || b.EnergyIncome < wantedEnergy)
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

        // Energy income wanted: TargetRatio x material income (capped by what the planet can sustain) + weapon upkeep.
        // On a dry planet (no material income) the bank spread to the horizon is what energy can turn into anything.
        public static float WantedEnergy(EconomyBalance b)
        {
            float material = b.MaterialIncome;
            if (b.SustainableMaterialIncome >= 0f)
                material = b.MaterialIncome <= 0f ? b.SustainableMaterialIncome : Math.Min(b.MaterialIncome, b.SustainableMaterialIncome);
            return b.TargetRatio * material + b.WeaponUpkeep;
        }

        // v0.12: energy is saturated when the planet, not the mines, limits material (the remaining pool and bank cannot
        // sustain the current extraction to the horizon, or the planet is dry) and energy income is above EnergySaturation x
        // the wanted energy. Sol v0.12 #1: the dry state (income 0, sustainable from the bank) counts.
        public static bool EnergySaturated(EconomyBalance b)
        {
            if (b.EnergySaturation <= 0f || b.SustainableMaterialIncome < 0f) return false;
            bool planetBinds = b.MaterialIncome <= 0f || b.SustainableMaterialIncome < b.MaterialIncome;
            return planetBinds && b.EnergyIncome >= b.EnergySaturation * Math.Max(1f, WantedEnergy(b));
        }

        // v0.12: an option whose only gain is energy (a power plant, an energy yield upgrade). Projects are kept out of the
        // saturation filter (Reactor Trials: temporary, cheap, Sol v0.12 #5).
        public static bool IsEnergyOnly(EconOption o)
        {
            return o.Kind != OptionKind.Project && o.EnergyGain > 0f && o.MaterialGain <= 0f && o.ScienceGain <= 0f;
        }

        // v0.13: an option whose only gain is extracted material (a mine, a material yield upgrade or project). In material
        // glut it only drains the fixed planet pool faster: v0.12.1 Clowrus queued 14-30 Mines a turn T21-T26 while its stock
        // grew 38.6k -> 96.2k unused, and still ran its planet dry at T28. Planet-saving options (refinery) are kept.
        public static bool IsExtractionOnly(EconOption o)
        {
            return !o.SavesReserve && o.MaterialGain > 0f && o.EnergyGain <= 0f && o.ScienceGain <= 0f;
        }

        // v0.12: material per turn the planet and the bank can supply until horizonTurn (at least minTurns ahead).
        // -1 = unlimited planet.
        public static float SustainableMaterialIncome(int planetMaterial, int stock, bool limited, int turn, int horizonTurn, int minTurns)
        {
            if (!limited) return -1f;
            return (Math.Max(0, planetMaterial) + Math.Max(0, stock)) / (float)Math.Max(Math.Max(1, minTurns), horizonTurn - turn);
        }

        // v0.12: material the economy keeps back once the planet runs dry within warningTurns (for defenses, weapons and
        // rebuilding after it does). v0.11.1: ~30 material at construction from T28, nothing could be rebuilt.
        public static int EconomyReserve(int planetMaterial, float drainPerTurn, bool limited, int warningTurns, int reserve)
        {
            if (!limited || reserve <= 0) return 0;
            bool soon = planetMaterial <= 0 || (drainPerTurn > 0f && planetMaterial / drainPerTurn <= warningTurns);
            return soon ? reserve : 0;
        }

        // Energy is only "expensive" when this turn's energy budget is the tighter one.
        public static float EnergyCostFactor(EconomyBalance b)
        {
            if (b.EnergyGlut && !b.MaterialGlut) return b.GlutEnergyWeight;
            return b.MaterialBudget >= 1.3f * b.EnergyBudget ? 1f : 0.3f;
        }

        // v0.11: energy is in glut when last turn ended with at least this share of its budget, and minimum, unspent.
        public static bool IsEnergyGlut(int lastEnergyLeft, int lastEnergyBudget, float share, int minimum)
        {
            return lastEnergyLeft >= minimum && lastEnergyLeft >= share * Math.Max(1, lastEnergyBudget);
        }

        public static float Score(EconOption o, float energyWeight, float materialWeight, float energyCostFactor, float scienceWeight = 1f,
            float materialCostFactor = 1f)
        {
            float value = energyWeight * o.EnergyGain + materialWeight * o.MaterialGain + scienceWeight * o.ScienceGain;
            float cost = materialCostFactor * o.Material + energyCostFactor * o.Energy;
            return value / Math.Max(1f, cost);
        }

        // v0.14.1: a plant bought only for its power slots. With no free slot nothing that needs one can be queued, and a queued
        // plant's connectors are not usable this turn (placement searches see existing buildings; the plant is built at turn
        // end, AIActions.EndTurn; Sol v0.14 #1). v0.13.1 Guts at T1 (0 free slots, energy not short) bought 3 Solar Plants
        // with 1,200 of 1,350 material, then left 750-975 energy unspent T2-T6. So in the opening: with no slot left, energy
        // not short and a plant already queued this turn, no more plants; the material waits for next turn's Mines. Later
        // turns keep buying plants for slots (weapons and defenses need them too).
        public static bool SlotStarved(EconomyBalance b)
        {
            return b.Opening && b.SlotsLeft <= 0 && b.PlantsQueued >= 1 && b.EnergyIncome >= WantedEnergy(b);
        }

        // v0.10: material is in glut when the banked stock is several turns of what this turn's energy can spend.
        public static bool IsMaterialGlut(int materialStock, int energyBudget, float ratio, int minimum)
        {
            return materialStock >= minimum && materialStock >= ratio * Math.Max(1, energyBudget);
        }

        // The best affordable option with a positive score, or null.
        public static EconOption Best(IList<EconOption> options, EconomyBalance b, int slotsLeft)
        {
            float wE, wM;
            Weights(b, out wE, out wM);
            float eFactor = EnergyCostFactor(b);
            float mFactor = b.MaterialGlut ? b.GlutMaterialWeight : 1f;
            EconOption best = null;
            float bestScore = 0f;
            int materialBudget = b.MaterialBudget - Math.Max(0, b.MaterialReserve);
            bool saturated = EnergySaturated(b);
            foreach (EconOption o in options)
            {
                if (o.Energy > b.EnergyBudget || o.Material > materialBudget) continue;
                if (saturated && IsEnergyOnly(o)) continue;
                if (b.MaterialGlut && IsExtractionOnly(o)) continue;
                if (o.Kind == OptionKind.Plant && SlotStarved(b)) continue;
                if (o.NeedsSlot && slotsLeft <= 0) continue;
                float score = Score(o, wE, wM, eFactor, b.ScienceWeight, mFactor);
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

        // Defenses wanted: a share of the known enemy weapons plus a response to damage taken, and at war never
        // fewer than floorShare x our own non-city buildings (v0.5). v0.4.1 waited for visible weapons or damage:
        // 0 defenses until T23 with a 277-building economy; the human who won had 19 Kinetic before any weapon.
        public static int DesiredDefenses(int knownEnemyWeapons, float recentDamage, float perWeapon, float damagePerDefense, int cap,
            bool war, int ownNonCityBuildings, float floorShare)
        {
            float wanted = perWeapon * knownEnemyWeapons + (damagePerDefense > 0f ? recentDamage / damagePerDefense : 0f);
            if (war) wanted = Math.Max(wanted, (float)Math.Ceiling(floorShare * ownNonCityBuildings - 1e-3f));
            return Math.Min(cap, (int)Math.Round(wanted));
        }

        // v0.8: defenses that stop Nuclear Missiles (Ion in Enhanced) wanted over our cities. 31 Oct v0.7.3: the human
        // built no conventional weapon, then 9-17 Nuclear Missiles from T32, and took both AIs' cities (5 -> 1) while
        // their economies stood. Once a nuke is seen or lands: perCitySeen per city; before that, from priorTurn on,
        // perCityPrior per city (Thermonuclear Warheads arrive around T27-32 in every match so far).
        public static int AntiNukeDefenses(int cities, bool nukeSeen, int turn, int priorTurn, int perCitySeen, int perCityPrior)
        {
            if (cities <= 0) return 0;
            return cities * AntiNukePerCity(nukeSeen, turn, priorTurn, perCitySeen, perCityPrior);
        }

        // v0.8.2 safety cap: never more anti-nuke defenses than capMultiplier x perCity x cities in total, whatever the
        // per-city count says (a geometry or counting error must not turn into runaway building again).
        public static int AntiNukeMissing(int deficit, int owned, int cities, int perCity, float capMultiplier)
        {
            int cap = (int)Math.Ceiling(Math.Max(0f, capMultiplier) * Math.Max(0, perCity) * Math.Max(0, cities));
            if (owned >= cap) return 0;
            return Math.Max(0, Math.Min(deficit, cap - owned));
        }

        // v0.15 intel sprint (Sol + Fable v0.14.2 audits): cities never move and a city seen once stays on the map as a Shadow
        // that guided strikes can use; v0.14.1 Bolide built its first Telescopes at T16, after the human's first counterintel
        // hub (T13), and never saw a city. While an enemy city has never been seen and turn <= untilTurn, intel may take up
        // to sprintPerTurn buildings a turn until sprintBuildings stand; otherwise the normal per-turn limit.
        // v0.15.1 (Sol v0.15 #5): the sprint's extra buildings wait while material is below materialReserve (one Telescope
        // plus one core material project), so the sprint cannot starve the opening's compounding investments.
        public static int IntelPerTurnLimit(int unseenCities, int turn, int untilTurn, int intelBuildings, int sprintBuildings,
            int sprintPerTurn, int normalPerTurn, float material = float.MaxValue, float materialReserve = 0f)
        {
            bool sprint = unseenCities > 0 && turn <= untilTurn && intelBuildings < sprintBuildings && material >= materialReserve;
            return sprint ? Math.Max(normalPerTurn, sprintPerTurn) : normalPerTurn;
        }

        // v0.15.2: intel wanted only to find unseen cities (not below the native minimum, not blind) waits while energy is below
        // `reserve` (one Telescope + one power plant), so the economy step after it still gets a plant. v0.15.1 Thistle: intel
        // (step 3) ran before the economy (step 6) and its 8 Telescopes at T6-T8 left no energy for plants; energy fell
        // 925 -> 785 while v0.14.1 Bolide rose to 1425. The v0.15.1 material guard never bound (material was in glut).
        // v0.15.3 (Sol v0.15.2 #3): which intel purchases the energy guard applies to. Native BelowMinimum is exempt; intel
        // needed because an enemy is blind is guarded from the second building of the turn (v0.15.1 T6-T7 were blind and
        // bypassed the v0.15.2 guard); intel wanted only for unseen cities is guarded from the first.
        public static bool IntelGuarded(bool belowMinimum, bool needed, int boughtThisTurn)
        {
            return !needed || (!belowMinimum && boughtThisTurn > 0);
        }

        public static bool IntelEnergyAllowed(bool unseenOnly, float energy, float reserve)
        {
            return !unseenOnly || energy >= reserve;
        }

        // Anti-nuke defenses wanted over each city.
        public static int AntiNukePerCity(bool nukeSeen, int turn, int priorTurn, int perCitySeen, int perCityPrior)
        {
            if (nukeSeen) return Math.Max(0, perCitySeen);
            return priorTurn > 0 && turn >= priorTurn ? Math.Max(0, perCityPrior) : 0;
        }

        // Defenses queued per turn: the base rate, or faster to close a gap within catchUpTurns. v0.4.1 built
        // 3/turn while losing 40-90 buildings a turn. Without damage taken the catch-up uses at most half the
        // free power slots, so a small planet keeps room for weapons and growth; under attack it may use all.
        public static int DefensesPerTurnCap(int desired, int own, int basePerTurn, int catchUpTurns, int freeSlots, bool underAttack)
        {
            int deficit = Math.Max(0, desired - own);
            int catchUp = catchUpTurns > 0 ? (deficit + catchUpTurns - 1) / catchUpTurns : deficit;
            if (!underAttack) catchUp = Math.Min(catchUp, Math.Max(0, freeSlots) / 2);
            return Math.Max(basePerTurn, catchUp);
        }

        // Chance a weapon gets through the target's defenses. coverageFull = defenses of one type that cover
        // the whole planet. stop = interception chance by defense id for this weapon.
        public static float PassChance(string weaponId, Dictionary<string, int> targetDefenses, Dictionary<string, Dictionary<string, float>> counters, float coverageFull,
            Dictionary<string, float> observedPass = null)
        {
            // v0.6: what our own shots of this weapon actually met over the last turns beats the estimate from
            // the defenses we think the target has (the 31 Oct 1v1 AI kept firing Railguns into a Kinetic wall).
            float seen;
            if (observedPass != null && observedPass.TryGetValue(weaponId, out seen)) return Math.Max(0.05f, Math.Min(1f, seen));
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

        // v0.13.1 (Sol v0.13 #4): an unobserved weapon inherits the observed pass of a weapon the same defenses stop
        // (at >= minChance): Railgun interception is evidence about Coilcannons (both stopped by Kinetic), not about Missiles.
        // Without it an untested type counts as passing completely and displaces the real counter.
        public static void ShareObservedPass(Dictionary<string, float> observedPass, IEnumerable<string> weaponIds,
            Dictionary<string, Dictionary<string, float>> counters, float minChance)
        {
            if (observedPass == null || observedPass.Count == 0) return;
            List<KeyValuePair<string, float>> seen = new List<KeyValuePair<string, float>>(observedPass);
            foreach (string weapon in weaponIds)
            {
                if (observedPass.ContainsKey(weapon)) continue;
                string stoppers = Stoppers(weapon, counters, minChance);
                if (stoppers.Length == 0) continue;
                float shared = -1f;
                foreach (KeyValuePair<string, float> s in seen)
                {
                    if (Stoppers(s.Key, counters, minChance) == stoppers) shared = shared < 0f ? s.Value : Math.Min(shared, s.Value);
                }
                if (shared >= 0f) observedPass[weapon] = shared;
            }
        }

        private static string Stoppers(string weapon, Dictionary<string, Dictionary<string, float>> counters, float minChance)
        {
            List<string> ids = new List<string>();
            foreach (KeyValuePair<string, Dictionary<string, float>> defense in counters)
            {
                float p;
                if (defense.Value.TryGetValue(weapon, out p) && p >= minChance) ids.Add(defense.Key);
            }
            ids.Sort(StringComparer.Ordinal);
            return string.Join(",", ids.ToArray());
        }

        // perSlot (v0.13): with a cap on aimed weapons the cap, not the cost, binds - rank by damage through per weapon.
        // v0.12.1: 30 Railguns per AI (pass 0.41-0.55 observed) into a human with Kinetic only and no Ion Defense.
        public static float WeaponScore(WeaponStats w, Dictionary<string, int> targetDefenses, Dictionary<string, Dictionary<string, float>> counters, float coverageFull, float firingCostTurns,
            Dictionary<string, float> observedPass = null, bool perSlot = false)
        {
            float pass = PassChance(w.WeaponId, targetDefenses, counters, coverageFull, observedPass);
            if (perSlot) return w.Damage * pass;
            float cost = w.BuildMaterial + 0.5f * w.BuildEnergy + firingCostTurns * w.FiringEnergy;
            return w.Damage * pass / Math.Max(1f, cost);
        }

        // Human-like variety: pick among the weapons scoring at least `band` of the best, weighted by score squared.
        public static WeaponStats PickWeapon(IList<WeaponStats> weapons, Dictionary<string, int> targetDefenses, Dictionary<string, Dictionary<string, float>> counters,
            float coverageFull, float firingCostTurns, float band, Random rng, Dictionary<string, float> observedPass = null, bool perSlot = false)
        {
            if (weapons.Count == 0) return null;
            float[] scores = new float[weapons.Count];
            float best = 0f;
            for (int i = 0; i < weapons.Count; i++)
            {
                scores[i] = WeaponScore(weapons[i], targetDefenses, counters, coverageFull, firingCostTurns, observedPass, perSlot);
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

        // Energy kept unspent for defense rolls (each roll costs the defender 100-200 energy from what is
        // left that turn; at 0 the defense fails). Expected incoming shots: our share of the visible enemy
        // weapons plus the hits behind recent damage. Capped as a share of income.
        // v0.5: never fewer than minRollsPerDefense rolls per own defense, so defenses built on the floor
        // (no known weapons, no damage yet) can still answer the first unseen volley.
        // v0.6: also what the last action phase actually cost our defenses (energy spent plus rolls that could
        // not be paid), with a margin, up to hardCapShare of income. 31 Oct 1v1: the human fired 49-96 shots a turn
        // at planet; the capped estimate held 2.0-3.7k. The planned figure keeps its own (lower) cap.
        public static int DefenseReserve(int ownDefenses, int visibleEnemyWeapons, int enemiesAlive, float recentDamage,
            float energyPerRoll, float damagePerHit, float capShare, float energyIncome, float minRollsPerDefense,
            float observedNeed = 0f, float hardCapShare = 0f)
        {
            // Observed spending can fund a reserve with no defense buildings: city barriers also roll (250 energy each).
            if (ownDefenses <= 0 && observedNeed <= 0f) return 0;
            float planned = 0f;
            if (ownDefenses > 0)
            {
                float shots = visibleEnemyWeapons / (float)Math.Max(1, enemiesAlive) + (damagePerHit > 0f ? recentDamage / damagePerHit : 0f);
                shots = Math.Max(shots, minRollsPerDefense * ownDefenses);
                planned = Math.Min(shots * energyPerRoll, capShare * energyIncome);
            }
            float seen = Math.Min(Math.Max(0f, observedNeed), Math.Max(0f, hardCapShare) * energyIncome);
            return (int)Math.Max(0f, Math.Max(planned, seen));
        }

        // Energy our defenses may have needed last action phase: spent on rolls, plus the rolls that could not be
        // paid (energy OR material, and every overlapping defense reports one for the same impact), so unpaid rolls
        // are counted at most once per impact that had an eligible defense. A bounded estimate, not a measurement.
        public static float ObservedDefenseNeed(int energySpent, int unpaidRolls, int impactsWithEligibleDefense, float energyPerRoll, float margin)
        {
            int unpaid = Math.Max(0, Math.Min(unpaidRolls, impactsWithEligibleDefense));
            return Math.Max(0f, (energySpent + unpaid * energyPerRoll) * margin);
        }

        // The defense reserve may not eat the budget that lets the AI recover: construction keeps at least
        // recoveryShare of income (31 Oct v0.6: Turing T33 and X T36 had 0 energy to build with and queued nothing).
        public static int CapReserveForRecovery(int reserve, int energyAvailable, float energyIncome, float recoveryShare)
        {
            float room = energyAvailable - Math.Max(0f, recoveryShare) * energyIncome;
            return (int)Math.Max(0f, Math.Min(reserve, room));
        }

        // Observed pass blended with the estimate from remembered defenses: a few shots barely move the prior.
        public static float BlendedPass(int impacts, int intercepted, float prior, float priorWeight)
        {
            float w = Math.Max(0f, priorWeight);
            return ((impacts - intercepted) + prior * w) / Math.Max(1f, impacts + w);
        }

        // Material to keep banked when the planet will run dry soon: enough for the superweapons we plan.
        // The 31 Oct v0.3.1 AIs held 53-120k material when their planets ran out at turns 25-28, spent it on
        // Railguns and plants, and had 72 left when the Nuclear Missile unlocked at turn 32.
        public static int EndgameBank(int planetMaterial, float planetDrainPerTurn, int turnsWarning, int bankTarget, int stock)
        {
            if (bankTarget <= 0) return 0;
            bool soon = planetMaterial <= 0 || (planetDrainPerTurn > 0f && planetMaterial / planetDrainPerTurn <= turnsWarning);
            return soon ? Math.Min(stock, bankTarget) : 0;
        }

        // Value of material income when the planet is running out: mines stop yielding at zero planet material.
        public static float PlanetMaterialFactor(int planetMaterial, float planetMaterialUsePerTurn, int horizonTurns)
        {
            if (planetMaterial <= 0) return 0f; // v0.12 (Sol #1): a dry planet yields nothing, whatever the multipliers
            if (planetMaterialUsePerTurn <= 0f) return 1f;
            float turnsLeft = planetMaterial / planetMaterialUsePerTurn;
            return Math.Max(0f, Math.Min(1f, turnsLeft / Math.Max(1, horizonTurns)));
        }
    }
}
