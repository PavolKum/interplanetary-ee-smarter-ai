using System;
using System.Collections.Generic;
using InterplanetaryAI.Brain;

namespace InterplanetaryAI.Tests
{
    internal static class Program
    {
        private static int s_failures;
        private static int s_checks;

        private static int Main()
        {
            RunawayLeaderScoresHighest();
            HumanBiasMultipliesHumanScore();
            GrudgeAndFinishRaiseScore();
            FocusShareSendsMostWeaponsAtTop();
            UnreachableTopIsReroutedToNextBest();
            AllUnreachableKeepsPlan();
            NoVisibleCitiesGetsNoFinishBonus();
            AccuracyCarryKeepsLongRunRate();
            AccuracyExtremes();
            AccuracyLevelLookupClamps();
            GrudgeDecaysAndIgnoresSelfDamage();
            ReachabilityExpiresAndClearsOnSuccess();
            StrikePicksWeakestCityOrWeapon();
            SavingsConvergeOnGoal();
            SavingsNeverExceedStockOrGoal();
            GrowthPhaseRules();
            DefenseBonusWhenHit();
            CategoryPickFollowsWeights();
            ResearchPlanSkipsDoneAndQueued();
            DefenseCountersVisibleThreat();
            DirectorPrefersYieldUpgrades();
            DirectorBuildsTheShortResource();
            DirectorRespectsBudgetAndSlots();
            DirectorWarAndDefenseRules();
            DefenseMemoryAndBlindChoice();
            LedgerAndObservedRules();
            WeaponChoiceAvoidsTargetDefenses();
            PlanetMaterialRunsOut();
            BlindAiSpreadsFire();
            UnknownEnemyIsNotIgnored();
            DefenseReserveRules();
            EndgameBankRules();

            Console.WriteLine(s_checks + " checks, " + s_failures + " failed");
            return s_failures == 0 ? 0 : 1;
        }

        // Simulates turns: income arrives, the reserve is set, everything unreserved is spent.
        private static void SavingsConvergeOnGoal()
        {
            int stock = 0, reserve = 0, turnsToGoal = -1;
            for (int turn = 1; turn <= 20; turn++)
            {
                stock = reserve + 1000;  // last turn's reserve survives, 1000 material income
                if (Savings.CanBuy(9999, stock, 0, 2000)) { turnsToGoal = turn; break; }
                reserve = Savings.NextReserve(reserve, stock, 2000, 0.5f);
            }
            Check(turnsToGoal > 0 && turnsToGoal <= 4, "1000/turn income at share 0.5 should afford a 2000 goal within 4 turns; got " + turnsToGoal);
        }

        private static void SavingsNeverExceedStockOrGoal()
        {
            Check(Savings.NextReserve(0, 500, 2000, 1f) == 500, "reserve cannot exceed the stock");
            Check(Savings.NextReserve(1900, 5000, 2000, 0.5f) == 2000, "reserve caps at the goal cost");
            Check(Savings.NextReserve(1500, 800, 2000, 0.5f) == 800, "if the stock fell below the old reserve, reserve what is there");
            Check(Savings.NextReserve(300, 1000, 0, 0.5f) == 0, "no goal, no reserve");
        }

        private static void GrowthPhaseRules()
        {
            BuildWeights w = new BuildWeights();
            Check(BuildPlanner.IsGrowthPhase(new BuildSituation { Turn = 5, OwnEconomy = 9000, MaxEnemyEconomy = 1000 }, w), "early turns are always growth");
            Check(!BuildPlanner.IsGrowthPhase(new BuildSituation { Turn = 30, OwnEconomy = 3000, MaxEnemyEconomy = 13000 }, w), "trailing a booming leader after the opening must NOT stay in growth (keeps arming)");
            Check(!BuildPlanner.IsGrowthPhase(new BuildSituation { Turn = 30, OwnEconomy = 13000, MaxEnemyEconomy = 12000 }, w), "caught up after the opening means pressure");
        }

        private static void DefenseBonusWhenHit()
        {
            BuildWeights w = new BuildWeights();
            float calm = BuildPlanner.Weights(new BuildSituation { Turn = 30, OwnEconomy = 1, MaxEnemyEconomy = 1 }, w)[(int)BuildCategory.Defense];
            float hit = BuildPlanner.Weights(new BuildSituation { Turn = 30, OwnEconomy = 1, MaxEnemyEconomy = 1, RecentDamage = 500 }, w)[(int)BuildCategory.Defense];
            Check(Math.Abs(hit - calm - w.DefenseBonusWhenHit) < 1e-4, "taking damage adds the defense bonus");
            Check(w.Pressure[(int)BuildCategory.Defense] == 1f, "the bonus must not mutate the configured weights");
        }

        private static void CategoryPickFollowsWeights()
        {
            int[] counts = new int[4];
            Random rng = new Random(7);
            float[] weights = { 0f, 3f, 1f, 0f };
            for (int i = 0; i < 4000; i++) counts[(int)BuildPlanner.Pick(weights, rng)]++;
            Check(counts[0] == 0 && counts[3] == 0, "zero-weight categories are never picked");
            Check(counts[1] > 2700 && counts[1] < 3300, "energy at weight 3 of 4 should be ~3000 of 4000; got " + counts[1]);
            Check(BuildPlanner.Parse("1,2,x,4", new float[] { 9, 9, 9, 9 })[0] == 9f, "a malformed weight list keeps the fallback");
        }

        private static void DefenseCountersVisibleThreat()
        {
            string[] all = { "bui_KineticDefense", "bui_IonDefense", "bui_ShieldDefense" };
            Dictionary<string, int> none = new Dictionary<string, int>();
            Dictionary<string, int> railguns = new Dictionary<string, int> { { "wea_Railgun", 10 } };
            Check(DefensePlanner.Choose(railguns, none, all) == "bui_KineticDefense", "railguns call for kinetic defense");
            Dictionary<string, int> missiles = new Dictionary<string, int> { { "wea_MIRV", 4 }, { "wea_NuclearMissile", 1 } };
            Check(DefensePlanner.Choose(missiles, none, all) == "bui_IonDefense", "MIRVs and a nuke call for ion defense");
            Dictionary<string, int> mixed = new Dictionary<string, int> { { "wea_Railgun", 6 }, { "wea_Missile", 4 } };
            Dictionary<string, int> haveKinetic = new Dictionary<string, int> { { "bui_KineticDefense", 3 } };
            Check(DefensePlanner.Choose(mixed, haveKinetic, all) == "bui_IonDefense", "with railguns already covered, the uncovered missiles win");
            Check(DefensePlanner.Choose(none, none, all) == null, "no visible threat gives null (native choice)");
            Check(DefensePlanner.Choose(railguns, none, new[] { "bui_IonDefense" }) == null, "a defense that counters nothing visible is not forced");
        }

        private static void ResearchPlanSkipsDoneAndQueued()
        {
            List<string> plan = ResearchPlan.Parse(" tec_A, tec_B ,tec_C,tec_A,");
            Check(plan.Count == 3 && plan[1] == "tec_B", "parse trims, drops blanks and duplicates");
            HashSet<string> done = new HashSet<string> { "tec_A" };
            HashSet<string> queued = new HashSet<string> { "tec_B" };
            Check(ResearchPlan.NextTarget(plan, done.Contains, queued.Contains) == "tec_C", "next target skips researched and queued techs");
            done.Add("tec_C");
            Check(ResearchPlan.NextTarget(plan, done.Contains, id => false) == "tec_B", "a dequeued tech comes back");
            done.Add("tec_B");
            Check(ResearchPlan.NextTarget(plan, done.Contains, queued.Contains) == null, "finished plan returns null (native research resumes)");
        }

        // Costs and yields from docs/ruleset (BuildingsData, UpgradesData, ProjectsData).
        private static EconOption Solar() { return new EconOption { Id = "bui_SolarPlant", Kind = OptionKind.Plant, Energy = 250, Material = 400, EnergyGain = 100 }; }
        private static EconOption Mine() { return new EconOption { Id = "bui_Mine", Kind = OptionKind.Producer, Energy = 300, Material = 300, MaterialGain = 100, NeedsSlot = true }; }
        private static EconOption SolarYield1() { return new EconOption { Id = "upg_SolarPEnergyYield1", Kind = OptionKind.Upgrade, Energy = 50, Material = 200, EnergyGain = 75 }; }
        private static EconOption MineYield1() { return new EconOption { Id = "upg_MineMaterialYield1", Kind = OptionKind.Upgrade, Energy = 50, Material = 150, MaterialGain = 75 }; }

        private static EconomyBalance Balance(float energyIncome, float materialIncome, int energy, int material)
        {
            return new EconomyBalance { EnergyIncome = energyIncome, MaterialIncome = materialIncome, TargetRatio = 0.6f, EnergyBudget = energy, MaterialBudget = material };
        }

        private static void DirectorPrefersYieldUpgrades()
        {
            List<EconOption> options = new List<EconOption> { Solar(), Mine(), SolarYield1() };
            EconOption pick = EconomyDirector.Best(options, Balance(1000, 2000, 1500, 3000), 5);
            Check(pick != null && pick.Id == "upg_SolarPEnergyYield1", "energy short: the solar yield upgrade beats a new plant");
            options.Add(MineYield1());
            pick = EconomyDirector.Best(options, Balance(2000, 1000, 1500, 3000), 5);
            Check(pick != null && pick.Id == "upg_MineMaterialYield1", "material short: the mine yield upgrade beats a new mine");
        }

        private static void DirectorBuildsTheShortResource()
        {
            List<EconOption> options = new List<EconOption> { Solar(), Mine() };
            EconOption pick = EconomyDirector.Best(options, Balance(2000, 1000, 1500, 3000), 5);
            Check(pick != null && pick.Id == "bui_Mine", "energy 2000 vs material 1000 per turn (target 0.6): build a mine");
            pick = EconomyDirector.Best(options, Balance(500, 2000, 1500, 3000), 5);
            Check(pick != null && pick.Id == "bui_SolarPlant", "energy 500 vs material 2000 per turn: build a plant");
            EconomyBalance push = Balance(2000, 1000, 1500, 3000);
            push.EnergyPush = true;
            pick = EconomyDirector.Best(options, push, 5);
            Check(pick != null && pick.Id == "bui_SolarPlant", "energy push (superweapon waiting on energy) builds power");
            EconomyBalance upkeep = Balance(1000, 1000, 1500, 3000);
            upkeep.WeaponUpkeep = 600;
            pick = EconomyDirector.Best(options, upkeep, 5);
            Check(pick != null && pick.Id == "bui_SolarPlant", "weapon upkeep counts against energy income");
        }

        private static void DirectorRespectsBudgetAndSlots()
        {
            List<EconOption> options = new List<EconOption> { Solar(), Mine() };
            EconOption pick = EconomyDirector.Best(options, Balance(2000, 1000, 1500, 3000), 0);
            Check(pick != null && pick.Id == "bui_SolarPlant", "no free power slot: the mine is skipped, the plant (which adds slots) is not");
            pick = EconomyDirector.Best(options, Balance(2000, 1000, 280, 3000), 5);
            Check(pick != null && pick.Id == "bui_SolarPlant", "energy 280: the 300-energy mine is unaffordable");
            pick = EconomyDirector.Best(options, Balance(2000, 1000, 200, 3000), 5);
            Check(pick == null, "nothing affordable gives null (construction ends for the turn)");
            EconOption useless = new EconOption { Id = "x", Kind = OptionKind.Upgrade, Energy = 50, Material = 100 };
            Check(EconomyDirector.Best(new List<EconOption> { useless }, Balance(1000, 1000, 1500, 3000), 5) == null, "an option with no gain is never bought");
        }

        private static void DirectorWarAndDefenseRules()
        {
            Check(!EconomyDirector.WarPhase(10, 15, 100f, 600f), "turn 10, little damage: still booming");
            Check(EconomyDirector.WarPhase(10, 15, 700f, 600f), "heavy damage starts retaliation early");
            Check(EconomyDirector.WarPhase(15, 15, 0f, 600f), "war from WarFromTurn");
            Check(EconomyDirector.DesiredDefenses(10, 1000f, 0.4f, 500f, 40, false, 0, 0.15f) == 6, "10 known weapons x 0.4 + 1000 damage / 500 = 6 defenses");
            Check(EconomyDirector.DesiredDefenses(200, 0f, 0.4f, 500f, 40, true, 100, 0.15f) == 40, "defenses are capped");
            Check(EconomyDirector.DesiredDefenses(0, 0f, 0.4f, 500f, 40, false, 140, 0.15f) == 0, "no threat before war, no defenses");

            // v0.5 floor: 31 Oct 1v1, T21 Fey had 140 non-city buildings, saw 0 weapons, took no damage, wanted 0.
            Check(EconomyDirector.DesiredDefenses(0, 0f, 0.4f, 500f, 60, true, 140, 0.15f) == 21, "at war, blind: floor 0.15 x 140 buildings = 21");
            Check(EconomyDirector.DesiredDefenses(4, 0f, 0.4f, 500f, 60, true, 10, 0.15f) == 2, "small base: floor 2 beats 4 x 0.4 = 1.6 -> 2");
            Check(EconomyDirector.DesiredDefenses(100, 0f, 0.4f, 500f, 60, true, 20, 0.15f) == 40, "threat above the floor still counts");
            Check(EconomyDirector.DesiredDefenses(0, 0f, 0.4f, 500f, 60, true, 140, 0f) == 0, "floor share 0 = v0.4 behaviour");

            // v0.5 catch-up: 0 owned, 21 wanted, 3 turns -> 7 a turn; on target -> base rate.
            Check(EconomyDirector.DefensesPerTurnCap(21, 0, 3, 3, 113, false) == 7, "21 behind over 3 turns = 7 a turn");
            Check(EconomyDirector.DefensesPerTurnCap(40, 38, 3, 3, 113, false) == 3, "nearly there: base rate 3");
            Check(EconomyDirector.DefensesPerTurnCap(40, 0, 3, 0, 200, false) == 40, "catch-up 0 turns: the whole gap");
            // Terra v0.5 #4: a small planet keeps room for weapons and growth unless under attack.
            Check(EconomyDirector.DefensesPerTurnCap(60, 0, 3, 3, 8, false) == 4, "60 behind, 8 free slots, no damage: half the slots = 4");
            Check(EconomyDirector.DefensesPerTurnCap(60, 0, 3, 3, 8, true) == 20, "under attack: full catch-up 20");
            Check(EconomyDirector.DefensesPerTurnCap(60, 0, 3, 3, 2, false) == 3, "never below the base rate");
        }

        private static void LedgerAndObservedRules()
        {
            // The ledger turns one action phase into "last turn" figures at the player's next turn start.
            var ledger = new DefenseLedger(2);
            ledger.Record(1, 2, "wea_Railgun", true, 0, 0, 0, 100, true);
            ledger.Record(1, 2, "wea_Railgun", false, 1, 1, 0, 100, true);
            ledger.Record(1, 2, "wea_Railgun", false, 0, 0, 0, 0, false);
            Check(ledger.LastDefender(1).Impacts == 0, "before the defender's next turn start: nothing reported yet");
            ledger.Roll(1);
            DefenderStats last = ledger.LastDefender(1);
            Check(last.Impacts == 3 && last.Intercepted == 1 && last.NoEligibleDefense == 1 && last.NoResource == 1 && last.EnergySpent == 200,
                "defender: 3 impacts, 1 intercepted, 1 with no eligible defense, 1 unpaid roll, 200 energy");
            ledger.Roll(1);
            Check(ledger.LastDefender(1).Impacts == 0, "the next action phase starts empty");

            float pass;
            Check(!ledger.ObservedPass(2, 1, "wea_Railgun", 3, out pass), "shooter history is empty until the shooter's own turn start");
            ledger.Roll(2);
            Check(ledger.ObservedPass(2, 1, "wea_Railgun", 3, out pass) && Math.Abs(pass - 2f / 3f) < 1e-4f, "shooter: 1 of 3 shots intercepted -> pass 0.67");
            Check(!ledger.ObservedPass(2, 1, "wea_Railgun", 4, out pass), "too few samples: no verdict");
            Check(!ledger.ObservedPass(2, 1, "wea_Missile", 1, out pass), "weapon never fired: no verdict");
            for (int i = 0; i < 10; i++) ledger.Record(1, 2, "wea_Railgun", true, 0, 0, 0, 100, true);
            ledger.Roll(2);
            Check(ledger.ObservedPass(2, 1, "wea_Railgun", 3, out pass) && Math.Abs(pass - 2f / 13f) < 1e-4f, "two turns in the window: 11 of 13 intercepted -> 2 of 13 get through = pass 0.15");
            ledger.Roll(2);
            ledger.Roll(2);
            Check(!ledger.ObservedPass(2, 1, "wea_Railgun", 1, out pass), "old turns leave the window (history 2)");

            // Terra v0.6 #2: samples are kept per defender; shots at oneself say nothing about an opponent.
            var two = new DefenseLedger(3);
            for (int i = 0; i < 12; i++) two.Record(5, 2, "wea_Railgun", false, 0, 0, 0, 0, false);   // weak AI: everything lands
            for (int i = 0; i < 12; i++) two.Record(1, 2, "wea_Railgun", true, 0, 0, 0, 100, true);   // the human: everything stopped
            for (int i = 0; i < 12; i++) two.Record(2, 2, "wea_Railgun", true, 0, 0, 0, 100, true);   // own planet
            two.Roll(2);
            Check(two.ObservedPass(2, 5, "wea_Railgun", 12, out pass) && Math.Abs(pass - 1f) < 1e-4f, "vs the weak AI: pass 1.0");
            Check(two.ObservedPass(2, 1, "wea_Railgun", 12, out pass) && Math.Abs(pass) < 1e-4f, "vs the human: pass 0.0, not pooled with the weak AI");
            Check(!two.ObservedPass(2, 2, "wea_Railgun", 1, out pass), "shots at oneself are not shooter samples");
            Check(two.ObservedAll(2, 1, 12).Count == 1 && two.ObservedAll(2, 7, 1).Count == 0, "ObservedAll lists only that defender's weapons");

            // Observed need (Terra v0.6 #4): unpaid rolls count at most once per impact that had an eligible defense.
            Check(EconomyDirector.ObservedDefenseNeed(3000, 10, 40, 100f, 1.25f) == 5000f, "(3,000 spent + 10 unpaid x 100) x 1.25");
            Check(EconomyDirector.ObservedDefenseNeed(1700, 29, 12, 100f, 1.25f) == 3625f, "29 unpaid rolls over 12 eligible impacts: counted as 12 (T33 X)");
            Check(EconomyDirector.ObservedDefenseNeed(0, 20, 0, 100f, 1.25f) == 0f, "no eligible impacts: nothing was needed");

            // Reserve (v0.6) and its recovery cap (Terra v0.6 #1).
            Check(EconomyDirector.DefenseReserve(21, 0, 1, 0f, 150f, 125f, 0.3f, 6840f, 1f) == 2052, "no observation: the planned figure, capped at 30% income");
            Check(EconomyDirector.DefenseReserve(21, 0, 1, 0f, 150f, 125f, 0.3f, 6840f, 1f, 6125f, 0.8f) == 5472, "observed 6,125 is capped at 80% of income = 5,472");
            Check(EconomyDirector.DefenseReserve(21, 0, 1, 0f, 150f, 125f, 0.3f, 6840f, 1f, 6125f, 0f) == 2052, "hard cap share 0 = v0.5 behaviour");
            Check(EconomyDirector.DefenseReserve(21, 0, 1, 0f, 150f, 125f, 0.3f, 20000f, 1f, 1000f, 0.8f) == 3150, "small observed need: the planned floor stays");
            Check(EconomyDirector.DefenseReserve(0, 0, 1, 0f, 150f, 125f, 0.3f, 6840f, 1f, 0f, 0.8f) == 0, "no defenses and no observation: nothing to fund");
            Check(EconomyDirector.DefenseReserve(0, 0, 1, 0f, 150f, 125f, 0.3f, 6840f, 1f, 750f, 0.8f) == 750, "a city barrier alone: observed spending still funds a reserve (Terra v0.6 #5)");
            // Turing T33: income 2,800, usable energy 1,000, reserve wanted 1,800. Construction keeps 20% of income.
            Check(EconomyDirector.CapReserveForRecovery(1800, 1000, 2800f, 0.2f) == 440, "usable 1,000 - recovery floor 560 = reserve 440");
            Check(EconomyDirector.CapReserveForRecovery(300, 1000, 2800f, 0.2f) == 300, "a reserve below the room is untouched");
            Check(EconomyDirector.CapReserveForRecovery(1800, 400, 2800f, 0.2f) == 0, "usable energy already under the floor: no reserve at all");
            Check(EconomyDirector.CapReserveForRecovery(1800, 1000, 2800f, 0f) == 1000, "recovery share 0 = v0.6.0 behaviour (reserve capped by usable energy)");

            // Observed pass beats the estimate from remembered defenses; a few shots only nudge it (Terra v0.6 #6).
            var kinetic16 = new Dictionary<string, int> { { "bui_KineticDefense", 16 } };
            var counters = DefensePlanner.EnhancedCounters();
            Check(Math.Abs(EconomyDirector.PassChance("wea_Railgun", kinetic16, counters, 10f) - 0.2f) < 1e-4f, "16 Kinetic: estimated Railgun pass 0.2");
            var observed = new Dictionary<string, float> { { "wea_Railgun", 0.9f } };
            Check(Math.Abs(EconomyDirector.PassChance("wea_Railgun", kinetic16, counters, 10f, observed) - 0.9f) < 1e-4f, "observed pass 0.9 overrides the estimate");
            Check(Math.Abs(EconomyDirector.PassChance("wea_Missile", kinetic16, counters, 10f, observed) - 1f) < 1e-4f, "no observation for Missile: the estimate (Kinetic does not stop it) = 1");
            observed["wea_Railgun"] = 0f;
            Check(Math.Abs(EconomyDirector.PassChance("wea_Railgun", new Dictionary<string, int>(), counters, 10f, observed) - 0.05f) < 1e-4f, "observed pass floors at 0.05");
            Check(Math.Abs(EconomyDirector.BlendedPass(12, 12, 0.2f, 6f) - 0.2f * 6f / 18f) < 1e-4f, "12 of 12 intercepted, prior 0.2, weight 6: pass 0.067");
            Check(Math.Abs(EconomyDirector.BlendedPass(2, 2, 0.8f, 6f) - 0.8f * 6f / 8f) < 1e-4f, "2 of 2 intercepted barely moves a prior of 0.8 (0.6)");
            Check(Math.Abs(EconomyDirector.BlendedPass(100, 0, 0.2f, 6f) - (100f + 1.2f) / 106f) < 1e-4f, "100 shots, none intercepted: pass near 1");
            Check(Math.Abs(EconomyDirector.BlendedPass(12, 6, 0.5f, 0f) - 0.5f) < 1e-4f, "weight 0 = the raw observed rate");

            var options = new List<WeaponStats>
            {
                new WeaponStats { BuildingId = "bui_Railgun", WeaponId = "wea_Railgun", BuildEnergy = 800, BuildMaterial = 1000, FiringEnergy = 100, Damage = 100f },
                new WeaponStats { BuildingId = "bui_Missile", WeaponId = "wea_Missile", BuildEnergy = 800, BuildMaterial = 1000, FiringEnergy = 100, Damage = 80f },
            };
            var rng = new Random(7);
            int railgunPicks = 0;
            for (int i = 0; i < 200; i++)
            {
                if (EconomyDirector.PickWeapon(options, new Dictionary<string, int>(), counters, 10f, 4f, 0.6f, rng).WeaponId == "wea_Railgun") railgunPicks++;
            }
            Check(railgunPicks > 60, "no observation, empty target defenses: Railguns still picked often (" + railgunPicks + " of 200)");
            var railgunStopped = new Dictionary<string, float> { { "wea_Railgun", 0.1f } };
            int after = 0;
            for (int i = 0; i < 200; i++)
            {
                if (EconomyDirector.PickWeapon(options, new Dictionary<string, int>(), counters, 10f, 4f, 0.6f, rng, railgunStopped).WeaponId == "wea_Railgun") after++;
            }
            Check(after == 0, "Railguns observed 90% intercepted: Missiles only (" + after + " Railgun picks of 200)");
        }

        private static Dictionary<int, Dictionary<string, int>> Seen(int enemy, string weapon, int count)
        {
            var seen = new Dictionary<string, int>();
            if (weapon != null) seen[weapon] = count;
            return new Dictionary<int, Dictionary<string, int>> { { enemy, seen } };
        }

        private static void DefenseMemoryAndBlindChoice()
        {
            // 31 Oct 1v1: Fey saw Railguns, then counterintel hid them; v0.4.1 forgot them and built Ion.
            var memory = new Dictionary<string, float>();
            var known = new Dictionary<string, int>();
            var human = new List<int> { 0 };
            DefensePlanner.Remember(memory, Seen(0, "wea_Railgun", 13), human, 0.95f, known);
            Check(known.ContainsKey("wea_Railgun") && known["wea_Railgun"] == 13, "seen now: 13 Railguns known");
            DefensePlanner.Remember(memory, Seen(0, null, 0), human, 0.95f, known);
            Check(known.ContainsKey("wea_Railgun") && known["wea_Railgun"] == 12, "hidden next turn: remembered 13 x 0.95 = 12");

            // Remembered Railguns pick Kinetic, not Ion: the memory output drives the choice.
            var candidates = new List<string> { "bui_KineticDefense", "bui_IonDefense" };
            var owned = new Dictionary<string, int> { { "bui_KineticDefense", 9 } };
            Check(DefensePlanner.Choose(known, owned, candidates) == "bui_KineticDefense",
                "remembered Railguns: Kinetic even with 9 owned and 0 Ion");

            var both = Seen(0, "wea_Railgun", 36);
            both[0]["wea_Missile"] = 2;
            DefensePlanner.Remember(memory, both, human, 0.95f, known);
            Check(known["wea_Railgun"] == 36 && known["wea_Missile"] == 2, "seen again: the larger count wins, new types added");
            for (int i = 0; i < 100; i++) DefensePlanner.Remember(memory, Seen(0, null, 0), human, 0.95f, known);
            Check(!known.ContainsKey("wea_Missile"), "memory fades: unseen for 100 turns, gone");
            var forget = new Dictionary<string, float>();
            DefensePlanner.Remember(forget, Seen(0, "wea_Railgun", 13), human, 0f, known);
            DefensePlanner.Remember(forget, Seen(0, null, 0), human, 0f, known);
            Check(known.Count == 0, "decay 0 = v0.4 behaviour: forget at once");

            // Terra v0.5 #3: per enemy. A's 20 hidden while B's 20 appear = 19 + 20, not 20.
            var multi = new Dictionary<string, float>();
            var ab = new List<int> { 1, 2 };
            DefensePlanner.Remember(multi, Seen(1, "wea_Railgun", 20), ab, 0.95f, known);
            DefensePlanner.Remember(multi, Seen(2, "wea_Railgun", 20), ab, 0.95f, known);
            Check(known["wea_Railgun"] == 39, "A hidden (20 x 0.95) + B seen (20) = 39");
            DefensePlanner.Remember(multi, Seen(2, "wea_Railgun", 20), new List<int> { 2 }, 0.95f, known);
            Check(known["wea_Railgun"] == 20, "A eliminated: its weapons are forgotten");

            // Nothing known: Kinetic 2:1 over Ion (v0.4.1 spread by fewest owned -> Ion).
            Check(DefensePlanner.ChooseBlind(new Dictionary<string, int>(), candidates) == "bui_KineticDefense", "blind, none owned: Kinetic");
            Check(DefensePlanner.ChooseBlind(new Dictionary<string, int> { { "bui_KineticDefense", 1 } }, candidates) == "bui_KineticDefense",
                "blind, 1 Kinetic 0 Ion: 2/2 vs 1/1, tie keeps Kinetic (first)");
            Check(DefensePlanner.ChooseBlind(new Dictionary<string, int> { { "bui_KineticDefense", 2 } }, candidates) == "bui_IonDefense",
                "blind, 2 Kinetic 0 Ion: Ion's turn");
            Check(DefensePlanner.ChooseBlind(new Dictionary<string, int>(), new List<string> { "bui_IonDefense" }) == "bui_IonDefense",
                "blind, Kinetic unavailable: Ion");
        }

        private static void WeaponChoiceAvoidsTargetDefenses()
        {
            // Damage proxy as the adapter computes it: (building + population damage) x fragments x area x homing.
            List<WeaponStats> weapons = new List<WeaponStats>
            {
                new WeaponStats { BuildingId = "bui_Railgun", WeaponId = "wea_Railgun", BuildEnergy = 200, BuildMaterial = 200, FiringEnergy = 100, Damage = 250f * 1.5f },
                new WeaponStats { BuildingId = "bui_Coilcannon", WeaponId = "wea_Coilcannon", BuildEnergy = 250, BuildMaterial = 400, FiringEnergy = 200, Damage = 400f * 3 * 1.1f },
                new WeaponStats { BuildingId = "bui_Missile", WeaponId = "wea_Missile", BuildEnergy = 250, BuildMaterial = 450, FiringEnergy = 300, Damage = 350f * 0.867f * 1.5f },
            };
            Random rng = new Random(1);
            Dictionary<string, int> open = new Dictionary<string, int>();
            Check(EconomyDirector.PickWeapon(weapons, open, DefensePlanner.Counters, 10f, 4f, 1f, rng).BuildingId == "bui_Coilcannon", "undefended target: the coilcannon (most damage per cost)");
            Dictionary<string, int> kinetic = new Dictionary<string, int> { { "bui_KineticDefense", 32 } };
            Check(EconomyDirector.PickWeapon(weapons, kinetic, DefensePlanner.Counters, 10f, 4f, 1f, rng).BuildingId == "bui_Missile", "32 kinetic defenses (the human's T26 count): missiles");
            float pass = EconomyDirector.PassChance("wea_Railgun", new Dictionary<string, int> { { "bui_KineticDefense", 5 }, { "bui_ShieldDefense", 10 } }, DefensePlanner.Counters, 10f);
            Check(Math.Abs(pass - (1f - 0.5f * 0.8f) * (1f - 0.15f)) < 1e-4f, "pass chance multiplies half kinetic cover and full shield cover");
            int coil = 0;
            for (int i = 0; i < 400; i++)
            {
                if (EconomyDirector.PickWeapon(weapons, open, DefensePlanner.Counters, 10f, 4f, 0.3f, rng).BuildingId == "bui_Coilcannon") coil++;
            }
            Check(coil > 200 && coil < 400, "a wide band mixes in weaker weapons, the best still most often (" + coil + "/400)");
        }

        private static void PlanetMaterialRunsOut()
        {
            Check(EconomyDirector.PlanetMaterialFactor(100000, 1000f, 10) == 1f, "100 turns of planet material left: full value");
            Check(Math.Abs(EconomyDirector.PlanetMaterialFactor(5000, 1000f, 10) - 0.5f) < 1e-4f, "5 of 10 horizon turns left: half value");
            Check(EconomyDirector.PlanetMaterialFactor(0, 1000f, 10) == 0f, "depleted planet: mines are worthless");
        }

        // 31 Oct v0.3.1: every enemy unknown, one AI hit us. v0.3 scored it 0.7 and everyone else 0.1 (7x).
        private static void BlindAiSpreadsFire()
        {
            List<EnemyView> enemies = new List<EnemyView>
            {
                new EnemyView { PlayerId = 0, IsHuman = true },
                new EnemyView { PlayerId = 2, Grudge = 5000 },
                new EnemyView { PlayerId = 3 },
            };
            TargetPlanner.Score(enemies, new PlannerSettings { GrudgeWeight = 0.35f });
            float ratio = enemies[1].Score / enemies[0].Score;
            Check(ratio > 1f && ratio < 1.6f, "blind: the attacker scores higher but not overwhelmingly (" + ratio.ToString("0.00") + "x)");
            Check(Math.Abs(enemies[0].Score - enemies[2].Score) < 1e-5f, "blind: enemies we know nothing about score alike");

            // What matters is where the weapons go (Terra v0.4 finding 3): not 70% at the one who hit us.
            List<WeaponSlot> weapons = new List<WeaponSlot>();
            for (int i = 0; i < 1000; i++) weapons.Add(new WeaponSlot { WeaponKey = "w" + i });
            int[] assigned = TargetPlanner.Assign(enemies, weapons, new PlannerSettings { GrudgeWeight = 0.35f }, new Random(7));
            int[] counts = new int[3];
            foreach (int a in assigned) counts[a]++;
            Check(counts[1] < 600, "blind: the attacker gets under 60% of the weapons (" + counts[1] + "/1000)");
            Check(counts[0] >= 200 && counts[2] >= 200, "blind: every other enemy still draws fire (" + counts[0] + ", " + counts[2] + ")");
        }

        private static void UnknownEnemyIsNotIgnored()
        {
            List<EnemyView> enemies = new List<EnemyView>
            {
                new EnemyView { EconomyKnown = true, PlayerId = 1, Economy = 5000 },
                new EnemyView { EconomyKnown = true, PlayerId = 2, Economy = 1000 },
                new EnemyView { PlayerId = 0, IsHuman = true },
            };
            TargetPlanner.Score(enemies, new PlannerSettings());
            Check(enemies[2].Score > enemies[1].Score, "an enemy we cannot read outranks a known weak one");
            Check(enemies[2].Score < enemies[0].Score, "and ranks below the known leader");
            enemies[2].Economy = 99999f; // stale value on an enemy we can no longer read
            TargetPlanner.Score(enemies, new PlannerSettings());
            Check(Math.Abs(enemies[0].LeaderTerm - 1f) < 1e-5f, "an unreadable enemy's number never sets the leader scale");
        }

        private static void DefenseReserveRules()
        {
            Check(EconomyDirector.DefenseReserve(0, 50, 2, 5000f, 150f, 125f, 0.3f, 20000f, 1f) == 0, "no defenses: nothing to fund");
            Check(EconomyDirector.DefenseReserve(10, 40, 4, 1250f, 150f, 125f, 0.3f, 20000f, 1f) == 3000, "40 weapons over 4 enemies + 10 hits = 20 shots x 150");
            Check(EconomyDirector.DefenseReserve(10, 400, 1, 0f, 150f, 125f, 0.3f, 20000f, 1f) == 6000, "capped at 30% of income");
            // Terra v0.5 #1: floor defenses with no known weapons and no damage still get one roll each.
            Check(EconomyDirector.DefenseReserve(21, 0, 1, 0f, 150f, 125f, 0.3f, 20000f, 1f) == 3150, "21 floor defenses, blind, undamaged: 21 x 150");
            Check(EconomyDirector.DefenseReserve(21, 0, 1, 0f, 150f, 125f, 0.3f, 20000f, 0f) == 0, "min rolls 0 = v0.4 behaviour");
        }

        private static void EndgameBankRules()
        {
            Check(EconomyDirector.EndgameBank(300000, 10000f, 8, 6000, 50000) == 0, "30 turns of planet left: no bank yet");
            Check(EconomyDirector.EndgameBank(60000, 10000f, 8, 6000, 50000) == 6000, "6 turns left: bank the target");
            Check(EconomyDirector.EndgameBank(0, 10000f, 8, 6000, 2500) == 2500, "depleted: bank what is left");
            Check(EconomyDirector.EndgameBank(60000, 10000f, 8, 0, 50000) == 0, "no target, no bank");
        }

        private static void Check(bool condition, string what)
        {
            s_checks++;
            if (!condition)
            {
                s_failures++;
                Console.WriteLine("FAIL: " + what);
            }
        }

        private static List<EnemyView> ThreeEnemies()
        {
            return new List<EnemyView>
            {
                new EnemyView { EconomyKnown = true, PlayerId = 0, Name = "You", IsHuman = true, Economy = 13000, CityPopulation = 9000, VisibleCities = 5 },
                new EnemyView { EconomyKnown = true, PlayerId = 2, Name = "AI 02", Economy = 1100, CityPopulation = 1500, VisibleCities = 2 },
                new EnemyView { EconomyKnown = true, PlayerId = 3, Name = "AI 03", Economy = 3100, CityPopulation = 8000, VisibleCities = 4 },
            };
        }

        private static void RunawayLeaderScoresHighest()
        {
            List<EnemyView> enemies = ThreeEnemies();
            TargetPlanner.Score(enemies, new PlannerSettings());
            Check(enemies[0].Score > enemies[1].Score && enemies[0].Score > enemies[2].Score,
                "economy leader (T30 shape: 13k vs 1-3k) should score highest; got " + enemies[0].Score + " / " + enemies[1].Score + " / " + enemies[2].Score);
        }

        private static void HumanBiasMultipliesHumanScore()
        {
            List<EnemyView> neutral = ThreeEnemies();
            TargetPlanner.Score(neutral, new PlannerSettings());
            List<EnemyView> biased = ThreeEnemies();
            TargetPlanner.Score(biased, new PlannerSettings { HumanBias = 2f });
            Check(Math.Abs(biased[0].Score - 2f * neutral[0].Score) < 1e-4, "HumanBias 2 should double the human's score");
            Check(Math.Abs(biased[2].Score - neutral[2].Score) < 1e-4, "HumanBias must not touch AI enemies");
        }

        private static void GrudgeAndFinishRaiseScore()
        {
            List<EnemyView> enemies = new List<EnemyView>
            {
                new EnemyView { EconomyKnown = true, PlayerId = 1, Economy = 1000, CityPopulation = 5000, VisibleCities = 3 },
                new EnemyView { EconomyKnown = true, PlayerId = 2, Economy = 1000, CityPopulation = 5000, VisibleCities = 3, Grudge = 800 },
                new EnemyView { EconomyKnown = true, PlayerId = 3, Economy = 1000, CityPopulation = 500, VisibleCities = 1 },
            };
            TargetPlanner.Score(enemies, new PlannerSettings());
            Check(enemies[1].Score > enemies[0].Score, "the enemy that hurt us should outscore an equal one that did not");
            Check(enemies[2].Score > enemies[0].Score, "the enemy with little city HP left should outscore an equal healthy one");
        }

        private static List<WeaponSlot> Weapons(int n)
        {
            List<WeaponSlot> slots = new List<WeaponSlot>();
            for (int i = 0; i < n; i++) slots.Add(new WeaponSlot { WeaponKey = "w" + i });
            return slots;
        }

        private static void FocusShareSendsMostWeaponsAtTop()
        {
            List<EnemyView> enemies = ThreeEnemies();
            PlannerSettings settings = new PlannerSettings { FocusShare = 0.7f };
            TargetPlanner.Score(enemies, settings);
            int[] plan = TargetPlanner.Assign(enemies, Weapons(10), settings, new Random(1));
            int atTop = 0;
            foreach (int target in plan) if (target == 0) atTop++;
            Check(atTop >= 7, "FocusShare 0.7 of 10 weapons should put at least 7 on the leader; got " + atTop);
            for (int i = 0; i < 7; i++) Check(plan[i] == 0, "focus weapon " + i + " should target the leader");
        }

        private static void UnreachableTopIsReroutedToNextBest()
        {
            List<EnemyView> enemies = ThreeEnemies();
            PlannerSettings settings = new PlannerSettings { FocusShare = 1f };
            TargetPlanner.Score(enemies, settings);
            List<WeaponSlot> weapons = Weapons(2);
            weapons[0].UnreachableTargetIds.Add(0); // cannot reach the human
            int[] plan = TargetPlanner.Assign(enemies, weapons, settings, new Random(1));
            int secondBest = enemies[1].Score > enemies[2].Score ? 1 : 2;
            Check(plan[0] == secondBest, "weapon that cannot reach the leader should go to the next best enemy; got " + plan[0]);
            Check(plan[1] == 0, "a weapon with no reach problem should still go at the leader");
        }

        private static void AllUnreachableKeepsPlan()
        {
            List<EnemyView> enemies = ThreeEnemies();
            PlannerSettings settings = new PlannerSettings { FocusShare = 1f };
            TargetPlanner.Score(enemies, settings);
            List<WeaponSlot> weapons = Weapons(1);
            weapons[0].UnreachableTargetIds.Add(0);
            weapons[0].UnreachableTargetIds.Add(2);
            weapons[0].UnreachableTargetIds.Add(3);
            int[] plan = TargetPlanner.Assign(enemies, weapons, settings, new Random(1));
            Check(plan[0] == 0, "with every target marked unreachable, keep the leader (search may still succeed)");

            weapons[0].UnreachableTargetIds.Remove(2); // now exactly one enemy (AI 02, index 1) is reachable
            plan = TargetPlanner.Assign(enemies, weapons, settings, new Random(1));
            Check(plan[0] == 1, "with only one reachable enemy, the weapon goes there even if it scores lowest; got " + plan[0]);
        }

        private static void NoVisibleCitiesGetsNoFinishBonus()
        {
            List<EnemyView> enemies = new List<EnemyView>
            {
                new EnemyView { EconomyKnown = true, PlayerId = 1, Economy = 1000, CityPopulation = 5000, VisibleCities = 3 },
                new EnemyView { EconomyKnown = true, PlayerId = 2, Economy = 1000, CityPopulation = 0, VisibleCities = 0 },
            };
            TargetPlanner.Score(enemies, new PlannerSettings());
            Check(enemies[1].FinishTerm == 0f, "an enemy with no visible city is unknown, not weak; finish term was " + enemies[1].FinishTerm);
        }

        private static void AccuracyCarryKeepsLongRunRate()
        {
            float carry = 0f;
            int hits = 0;
            for (int turn = 0; turn < 100; turn++) hits += AccuracyPlan.PlannedHits(3, 0.95f, ref carry);
            Check(hits >= 284 && hits <= 285, "300 shots at 0.95 should plan 284-285 hits; got " + hits);
        }

        private static void AccuracyExtremes()
        {
            float carry = 0f;
            Check(AccuracyPlan.PlannedHits(7, 1f, ref carry) == 7, "accuracy 1 plans every shot as a hit");
            carry = 0f;
            Check(AccuracyPlan.PlannedHits(7, 0f, ref carry) == 0, "accuracy 0 plans no hits");
            carry = 0f;
            Check(AccuracyPlan.PlannedHits(0, 1f, ref carry) == 0, "no shots, no hits");
        }

        private static void AccuracyLevelLookupClamps()
        {
            float[] byLevel = { 0.6f, 0.95f, 1f, 1f };
            Check(AccuracyPlan.ForLevel(byLevel, 1) == 0.95f, "level 1 reads the second entry");
            Check(AccuracyPlan.ForLevel(byLevel, 9) == 1f, "a level past the list uses the last entry");
            Check(AccuracyPlan.ForLevel(byLevel, -1) == 0.6f, "a negative level uses the first entry");
        }

        private static void GrudgeDecaysAndIgnoresSelfDamage()
        {
            GrudgeLedger ledger = new GrudgeLedger(0.5f);
            ledger.Record(victimId: 1, shooterId: 2, turn: 1, damage: 100);
            Check(Math.Abs(ledger.Get(1, 2, 3) - 25f) < 1e-3, "100 damage at decay 0.5 should be 25 two turns later; got " + ledger.Get(1, 2, 3));
            ledger.Record(1, 2, 3, 10);
            Check(Math.Abs(ledger.Get(1, 2, 3) - 35f) < 1e-3, "new damage adds to the decayed total");
            ledger.Record(1, 1, 3, 500);
            ledger.Record(1, -1, 3, 500);
            Check(ledger.Get(1, 1, 3) == 0f && ledger.Get(1, -1, 3) == 0f, "self-damage and unknown shooters are ignored");
            Check(ledger.Get(2, 1, 3) == 0f, "grudges are directional");
        }

        private static void ReachabilityExpiresAndClearsOnSuccess()
        {
            ReachabilityMemory memory = new ReachabilityMemory(2);
            HashSet<int> unreachable = new HashSet<int>();
            memory.Report("w", targetId: 5, turn: 10, foundTrajectory: false);
            memory.FillUnreachable("w", 12, unreachable);
            Check(unreachable.Contains(5), "a miss 2 turns ago is still remembered");
            memory.FillUnreachable("w", 13, unreachable);
            Check(!unreachable.Contains(5), "a miss 3 turns ago is forgotten with memory 2");
            memory.Report("w", 5, 13, false);
            memory.Report("w", 5, 14, true);
            memory.FillUnreachable("w", 14, unreachable);
            Check(!unreachable.Contains(5), "finding a trajectory clears the mark");
        }

        private static void StrikePicksWeakestCityOrWeapon()
        {
            List<BuildingView> candidates = new List<BuildingView>
            {
                new BuildingView { IsCity = true, CurrentHp = 4000, MaxHp = 4000 },
                new BuildingView { IsCity = true, CurrentHp = 900, MaxHp = 3000 },
                new BuildingView { IsCity = false, CurrentHp = 300, MaxHp = 1000 },
                new BuildingView { IsCity = false, CurrentHp = 100, MaxHp = 1000 },
            };
            Check(StrikePlanner.Choose(candidates, 0f, new Random(1)) == 1, "share 0 picks the weakest city");
            Check(StrikePlanner.Choose(candidates, 1f, new Random(1)) == 3, "share 1 picks the weakest weapon building");
            Check(StrikePlanner.Choose(new List<BuildingView>(), 0.5f, new Random(1)) == -1, "no candidates gives -1 (native fallback)");
            List<BuildingView> weaponsOnly = new List<BuildingView> { new BuildingView { IsCity = false, CurrentHp = 50 } };
            Check(StrikePlanner.Choose(weaponsOnly, 0f, new Random(1)) == 0, "with no visible city, strike a weapon building");
        }
    }
}
