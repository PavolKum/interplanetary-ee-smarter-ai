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
            CoverageRules();
            OffenseAndScienceRules();
            WeaponChoiceAvoidsTargetDefenses();
            PlanetMaterialRunsOut();
            BlindAiSpreadsFire();
            UnknownEnemyIsNotIgnored();
            DefenseReserveRules();
            EndgameBankRules();
            SalvoEstimateRules();
            StrategyFireRules();
            StrategyCommitsToBestValue();
            CommittedAssignmentRules();
            MaterialGlutRules();
            V011Rules();
            V012Rules();
            V013Rules();
            V014Rules();
            V015Rules();
            V0151Rules();
            V0152Rules();
            V016Rules();
            V0161Rules();
            V0162Rules();
            V017Rules();
            V018Rules();

            Console.WriteLine(s_checks + " checks, " + s_failures + " failed");
            return s_failures == 0 ? 0 : 1;
        }

        // ---- v0.10 strategy -------------------------------------------------------------------------

        private static Dictionary<string, Dictionary<string, float>> EnhancedCounters()
        {
            return new Dictionary<string, Dictionary<string, float>>
            {
                { "bui_KineticDefense", new Dictionary<string, float> { { "wea_Railgun", 0.8f }, { "wea_Coilcannon", 0.8f } } },
                { "bui_IonDefense", new Dictionary<string, float> { { "wea_Missile", 0.8f }, { "wea_NuclearMissile", 0.8f } } },
            };
        }

        private static SalvoTarget Target(int id, float score, int kinetic, bool energyKnown, float energy)
        {
            return new SalvoTarget
            {
                PlayerId = id, Name = "p" + id, Score = score, EnergyKnown = energyKnown, EnergyProduction = energy, DefensesSeen = true,
                Defenses = new Dictionary<string, int> { { "bui_KineticDefense", kinetic } },
            };
        }

        private static Dictionary<string, float> Railguns(float n)
        {
            return new Dictionary<string, float> { { "wea_Railgun", n } };
        }

        // The model by hand: k defenses of chance p, CoverHalf h, OverlapDefenses o; shots n; rolls r.
        private static float ExpectedPassing(float n, int k, float p, float h, float o, float rolls, float maxCover = 0.9f)
        {
            float cover = Math.Min(maxCover, k / (k + h));
            float tries = 1f + k / o;
            float stop = 1f - (float)Math.Pow(1f - p, tries);
            float eligible = n * cover;
            float needed = eligible * stop / p;
            float paid = Math.Min(1f, rolls / needed);
            return n - eligible * stop * paid;
        }

        private static void SalvoEstimateRules()
        {
            StrategySettings s = new StrategySettings { OverlapDefenses = 20f }; // CoverHalf 12, roll share 0.5 at 150
            var counters = EnhancedCounters();

            // v0.9.1 T22 shape: 35 Railguns at the human's 19 Kinetic, 6,000 energy -> 20 rolls, which bind.
            SalvoTarget human = Target(0, 1f, 19, true, 6000f);
            Strategy.EstimateSalvo(Railguns(35), human, 1f, counters, s);
            Check(Math.Abs(human.Rolls - 20f) < 1e-3, "6000 energy x 0.5 / 150 = 20 rolls; got " + human.Rolls);
            Check(Math.Abs(human.Eligible - 35f * 19f / 31f) < 1e-3, "eligible = shots x cover; got " + human.Eligible);
            Check(Math.Abs(human.Passing - ExpectedPassing(35, 19, 0.8f, 12, 20, 20)) < 1e-3, "rolls bind; got " + human.Passing);
            Check(Math.Abs(human.Passing - (35f - 20f * 0.8f)) < 0.05, "when rolls bind, each paid roll stops p of a shot: ~19 through; got " + human.Passing);

            // Unknown energy: every roll paid; several defenses in range try in turn (1 - 0.2^1.95 ~ 0.957 stop).
            SalvoTarget blind = Target(0, 1f, 19, false, 0f);
            Strategy.EstimateSalvo(Railguns(35), blind, 1f, counters, s);
            Check(Math.Abs(blind.Passing - ExpectedPassing(35, 19, 0.8f, 12, 20, float.MaxValue)) < 1e-3, "unknown energy: only coverage limits; got " + blind.Passing);
            Check(blind.Passing < 35f - 35f * 19f / 31f * 0.8f - 1f, "overlapping defenses stop more than one 80% roll would");

            // Saturation: doubling the salvo against the same rolls more than doubles what gets through.
            SalvoTarget small = Target(0, 1f, 19, true, 6000f), big = Target(0, 1f, 19, true, 6000f);
            Strategy.EstimateSalvo(Railguns(20), small, 1f, counters, s);
            Strategy.EstimateSalvo(Railguns(40), big, 1f, counters, s);
            Check(big.Passing > 2f * small.Passing, "a salvo past the target's rolls saturates; 20 -> " + small.Passing + ", 40 -> " + big.Passing);

            // Defenses that do not stop the weapon do nothing; a planet we have seen with no defenses lets all through;
            // a planet we never saw is not assumed open.
            SalvoTarget ionOnly = new SalvoTarget { PlayerId = 1, Score = 1f, DefensesSeen = true, Defenses = new Dictionary<string, int> { { "bui_IonDefense", 30 } } };
            Strategy.EstimateSalvo(Railguns(10), ionOnly, 1f, counters, s);
            Check(Math.Abs(ionOnly.Passing - 10f) < 1e-3, "Ion defenses do not stop Railguns; got " + ionOnly.Passing);
            SalvoTarget open = new SalvoTarget { PlayerId = 1, Score = 1f, DefensesSeen = true };
            Strategy.EstimateSalvo(Railguns(30), open, 1f, counters, s);
            Check(Math.Abs(open.Passing - 30f) < 1e-3, "seen and undefended: all through; got " + open.Passing);
            SalvoTarget unseen = new SalvoTarget { PlayerId = 1, Score = 1f, DefensesSeen = false };
            Strategy.EstimateSalvo(Railguns(30), unseen, 1f, counters, s);
            Check(Math.Abs(unseen.Passing - (30f - 30f * s.UnseenCover * s.DefaultChance)) < 1e-3, "never seen: UnseenCover at DefaultChance; got " + unseen.Passing);

            // Accuracy: planned misses never arrive. Per-target shots: weapons that cannot reach it are not in its salvo.
            SalvoTarget half = new SalvoTarget { PlayerId = 1, Score = 1f, DefensesSeen = true };
            Strategy.EstimateSalvo(Railguns(30), half, 0.5f, counters, s);
            Check(Math.Abs(half.Arriving - 15f) < 1e-3 && Math.Abs(half.Passing - 15f) < 1e-3, "accuracy 0.5 halves what arrives; got " + half.Arriving);
            SalvoTarget reach = new SalvoTarget { PlayerId = 1, Score = 1f, DefensesSeen = true, Shots = Railguns(10) };
            Strategy.EstimateSalvo(Railguns(30), reach, 1f, counters, s);
            Check(Math.Abs(reach.Arriving - 10f) < 1e-3, "the target's own reachable salvo is used, not the whole arsenal; got " + reach.Arriving);

            // Our observed shots correct coverage per weapon: 40 Railgun impacts, 36 intercepted, at 5 Kinetic.
            SalvoTarget seen = Target(0, 1f, 5, false, 0f);
            seen.Observed = new Dictionary<string, int[]> { { "wea_Railgun", new[] { 40, 36 } } };
            SalvoTarget model = Target(0, 1f, 5, false, 0f);
            Strategy.EstimateSalvo(Railguns(30), seen, 1f, counters, s);
            Strategy.EstimateSalvo(Railguns(30), model, 1f, counters, s);
            float stop5 = 1f - (float)Math.Pow(0.2, 1f + 5f / 20f);
            float blended = (20f * (5f / 17f) + 40f * Math.Min(0.9f, 0.9f / stop5)) / 60f;
            Check(Math.Abs(seen.Passing - (30f - 30f * blended * stop5)) < 1e-3, "observed interceptions blend into coverage; got " + seen.Passing);
            Check(seen.Passing < model.Passing, "a target that intercepted most of our shots is harder than its visible defenses suggest");
            SalvoTarget other = Target(0, 1f, 5, false, 0f);
            other.Observed = new Dictionary<string, int[]> { { "wea_Missile", new[] { 40, 36 } } };
            Strategy.EstimateSalvo(Railguns(30), other, 1f, counters, s);
            Check(Math.Abs(other.Passing - model.Passing) < 1e-3, "missile observations do not change a Railgun estimate");
            SalvoTarget few = Target(0, 1f, 5, false, 0f);
            few.Observed = new Dictionary<string, int[]> { { "wea_Railgun", new[] { 5, 5 } } };
            Strategy.EstimateSalvo(Railguns(30), few, 1f, counters, s);
            Check(Math.Abs(few.Passing - model.Passing) < 1e-3, "fewer than ObservedMinImpacts shots do not move the estimate");
        }

        private static StrategyDecision DecideRail(int turn, bool war, int railguns, List<SalvoTarget> targets, int lastStrike, int hold, StrategySettings s,
            int lastCommit = 0)
        {
            return Strategy.Decide(turn, war, Railguns(railguns), 1f, targets, EnhancedCounters(), lastStrike, hold, railguns, s, lastCommit);
        }

        private static void StrategyFireRules()
        {
            StrategySettings s = new StrategySettings { OverlapDefenses = 20f };
            Func<int, List<SalvoTarget>> human = kinetic => new List<SalvoTarget> { Target(0, 1f, kinetic, true, 6000f) };

            StrategyDecision none = DecideRail(25, true, 0, human(19), -1000, 0, s);
            Check(!none.Fire && none.Reason == "no-weapons", "no weapons, no fire; got " + none.Reason);
            StrategyDecision peace = DecideRail(10, false, 40, human(0), -1000, 0, s);
            Check(!peace.Fire && peace.Reason == "peace", "before war, no fire even into an open planet; got " + peace.Reason);
            StrategyDecision few = DecideRail(25, true, 5, human(0), -1000, 0, s);
            Check(!few.Fire && few.Reason == "building", "under MinWeapons, keep building; got " + few.Reason);
            StrategyDecision fewLong = DecideRail(25, true, 5, human(0), -1000, s.MaxHoldTurns, s);
            Check(fewLong.Fire && fewLong.Reason == "max-hold", "a small arsenal held MaxHoldTurns fires too; got " + fewLong.Reason);

            // 20 Railguns at 19 Kinetic, 6k energy: ~8 through (< 12): hold. 35: ~19 through, 54%: strike.
            StrategyDecision small = DecideRail(25, true, 20, human(19), -1000, 0, s);
            Check(!small.Fire && small.Reason == "below-saturation", "a salvo the defenses absorb is held; got " + small.Reason + " " + small.Passing);
            StrategyDecision big = DecideRail(25, true, 35, human(19), -1000, 0, s);
            Check(big.Fire && big.Reason == "saturated", "a saturating salvo strikes; got " + big.Reason + " " + big.Passing);
            Check(big.CommitTargetId == 0, "commits to the only enemy");

            // Pulse: struck last turn -> wait, unless the defenses are broken (>= 70% through).
            StrategyDecision wait = DecideRail(26, true, 35, human(19), 25, 0, s);
            Check(!wait.Fire && wait.Reason == "pulse-wait", "struck last turn: invest this turn; got " + wait.Reason);
            StrategyDecision again = DecideRail(27, true, 35, human(19), 25, 0, s);
            Check(again.Fire, "two turns after a strike, strike again");
            StrategyDecision broken = DecideRail(26, true, 35, human(3), 25, 0, s);
            Check(broken.Fire && broken.Reason == "broken", "broken defenses: strike again at once; got " + broken.Reason + " " + broken.PassShare);

            // Never hold forever.
            StrategyDecision stuck = DecideRail(30, true, 20, human(40), -1000, s.MaxHoldTurns, s);
            Check(stuck.Fire && stuck.Reason == "max-hold", "after MaxHoldTurns held, fire anyway; got " + stuck.Reason);
            StrategyDecision notYet = DecideRail(30, true, 20, human(40), -1000, s.MaxHoldTurns - 1, s);
            Check(!notYet.Fire, "one turn short of MaxHoldTurns still holds");
        }

        private static void StrategyCommitsToBestValue()
        {
            StrategySettings s = new StrategySettings();
            // The leader (score 1.0) behind 40 Kinetic vs a weaker rival (0.6) with 2: the open target is worth more.
            List<SalvoTarget> targets = new List<SalvoTarget> { Target(0, 1f, 40, false, 0f), Target(2, 0.6f, 2, false, 0f) };
            StrategyDecision d = DecideRail(25, true, 30, targets, -1000, 0, s, -1);
            Check(d.CommitTargetId == 2, "commit where score x expected shots through is highest; got " + d.CommitTargetId);
            // Same defenses: the higher score wins.
            targets = new List<SalvoTarget> { Target(0, 1f, 10, false, 0f), Target(2, 0.6f, 10, false, 0f) };
            d = DecideRail(25, true, 30, targets, -1000, 0, s, -1);
            Check(d.CommitTargetId == 0, "equal defenses: the leader; got " + d.CommitTargetId);
            // The leader is out of reach for most weapons: the reachable salvo decides.
            SalvoTarget farLeader = Target(0, 1f, 10, false, 0f);
            farLeader.Shots = Railguns(3);
            targets = new List<SalvoTarget> { farLeader, Target(2, 0.6f, 10, false, 0f) };
            d = DecideRail(25, true, 30, targets, -1000, 0, s, -1);
            Check(d.CommitTargetId == 2, "a leader only 3 weapons can reach is not the commit; got " + d.CommitTargetId);
        }

        private static void CommittedAssignmentRules()
        {
            List<EnemyView> enemies = ThreeEnemies();
            PlannerSettings settings = new PlannerSettings { FocusShare = 0.7f };
            TargetPlanner.Score(enemies, settings);
            int[] plan = TargetPlanner.Assign(enemies, Weapons(10), settings, new Random(1), 2);
            bool all = true;
            foreach (int t in plan) all &= t == 2;
            Check(all, "a committed strike sends every weapon at the committed enemy, not the top-scored one");
            List<WeaponSlot> weapons = Weapons(2);
            weapons[0].UnreachableTargetIds.Add(enemies[2].PlayerId);
            plan = TargetPlanner.Assign(enemies, weapons, settings, new Random(1), 2);
            int bestOther = 0;
            for (int i = 1; i < enemies.Count; i++) if (i != 2 && enemies[i].Score > enemies[bestOther].Score) bestOther = i;
            Check(plan[0] == bestOther && plan[1] == 2, "a weapon that cannot reach the committed enemy falls back to the best other; got " + plan[0] + "," + plan[1]);
            plan = TargetPlanner.Assign(enemies, Weapons(10), new PlannerSettings { FocusShare = 0.7f }, new Random(1), -1);
            int atTop = 0;
            foreach (int t in plan) if (t == 0) atTop++;
            Check(atTop >= 7, "no commit keeps the v0.9 split plan");
        }

        // ---- v0.11 ---------------------------------------------------------------------------------

        private static void V011Rules()
        {
            StrategySettings s = new StrategySettings();
            var counters = EnhancedCounters();

            // v0.10.1 T26 shape: the human softer (33 through) but scored lower (0.5) than the other AI (19 through, 1.0).
            SalvoTarget human = new SalvoTarget { PlayerId = 0, Name = "human", Score = 0.5f, DefensesSeen = true, Shots = Railguns(33) };
            SalvoTarget rival = new SalvoTarget { PlayerId = 2, Name = "rival", Score = 1f, DefensesSeen = true, Shots = Railguns(19) };
            StrategyDecision d = Strategy.Decide(26, true, Railguns(40), 1f, new List<SalvoTarget> { human, rival }, counters, -1000, 0, 40, s, -1);
            Check(d.CommitTargetId == 0, "commit by shots through: the softer human, not the higher-scored rival; got " + d.CommitTargetId);
            StrategySettings v010 = new StrategySettings { LeaderInfluence = 1000f };
            d = Strategy.Decide(26, true, Railguns(40), 1f, new List<SalvoTarget> { human, rival }, counters, -1000, 0, 40, v010, -1);
            Check(d.CommitTargetId == 2, "a huge leader influence brings back the v0.10 pick (the check can fail)");

            // Stickiness: last turn's target stays unless another is worth CommitStickiness x more.
            SalvoTarget a = new SalvoTarget { PlayerId = 0, Score = 1f, DefensesSeen = true, Shots = Railguns(20) };
            SalvoTarget b = new SalvoTarget { PlayerId = 2, Score = 1f, DefensesSeen = true, Shots = Railguns(23) };
            d = Strategy.Decide(26, true, Railguns(40), 1f, new List<SalvoTarget> { a, b }, counters, -1000, 0, 40, s, 0);
            Check(d.CommitTargetId == 0, "23 vs 20 through (< 1.25x): stay on last turn's target; got " + d.CommitTargetId);
            b.Shots = Railguns(30);
            d = Strategy.Decide(26, true, Railguns(40), 1f, new List<SalvoTarget> { a, b }, counters, -1000, 0, 40, s, 0);
            Check(d.CommitTargetId == 2, "30 vs 20 through: switch; got " + d.CommitTargetId);

            // v0.11.1: no aiming turn (trajectory searches restart every turn); a new target is struck at once.
            SalvoTarget open = new SalvoTarget { PlayerId = 0, Score = 1f, DefensesSeen = true };
            d = Strategy.Decide(26, true, Railguns(30), 1f, new List<SalvoTarget> { open }, counters, -1000, 0, 30, s, -1);
            Check(d.Fire && d.Reason == "saturated", "a new target saturated: strike this turn; got " + d.Reason);

            // Big salvo: 40 through at 44% fires (v0.10.1 Clowrus T29 held).
            SalvoTarget wall = new SalvoTarget { PlayerId = 0, Score = 1f, DefensesSeen = true, Defenses = new Dictionary<string, int> { { "bui_KineticDefense", 120 } } };
            d = Strategy.Decide(29, true, Railguns(100), 1f, new List<SalvoTarget> { wall }, counters, -1000, 0, 100, s, 0);
            Check(d.PassShare < s.MinPassShare && d.Passing >= s.BigSalvoShots, "setup: big salvo under the share bar; got " + d.Passing + " at " + d.PassShare);
            Check(d.Fire, "a big salvo fires under the share bar; got " + d.Reason);

            // Overlap off by default: an eligible impact is stopped at the defense's chance (measured 0.72).
            SalvoTarget t19 = Target(0, 1f, 19, false, 0f);
            Strategy.EstimateSalvo(Railguns(35), t19, 1f, counters, s);
            Check(Math.Abs(t19.Passing - ExpectedPassing(35, 19, 0.8f, 12, 1000, float.MaxValue)) < 1e-3 && t19.Passing > 35f - 35f * 19f / 31f * 0.81f,
                "default overlap ~ one roll; got " + t19.Passing);

            // Counter-defense at the cap (Terra v0.11 #3): 14 Lasers, 26 Kinetic + 5 Ion, nothing stops Lasers -> Shield.
            var enemy = new Dictionary<string, int> { { "wea_Laser", 14 }, { "wea_Railgun", 37 } };
            var all = new List<string> { "bui_KineticDefense", "bui_IonDefense", "bui_ShieldDefense" };
            var ownKI = new Dictionary<string, int> { { "bui_KineticDefense", 26 }, { "bui_IonDefense", 5 } };
            Check(DefensePlanner.CounterDeficit(enemy, ownKI, all) == "bui_ShieldDefense", "Lasers with no Shield: build a Shield");
            var ownS = new Dictionary<string, int>(ownKI); ownS["bui_ShieldDefense"] = 4;
            Check(DefensePlanner.CounterDeficit(enemy, ownS, all) == null, "4 Shields for 14 Lasers (0.25 share): enough");
            Check(DefensePlanner.CounterDeficit(enemy, ownKI, new List<string> { "bui_KineticDefense", "bui_IonDefense" }) == null, "no Shield buildable: nothing (Kinetic does not stop Lasers)");
            var fewK = new Dictionary<string, int> { { "bui_KineticDefense", 2 }, { "bui_ShieldDefense", 30 } };
            Check(DefensePlanner.CounterDeficit(new Dictionary<string, int> { { "wea_Railgun", 37 } }, fewK, all) == "bui_KineticDefense",
                "37 Railguns vs 2 Kinetic: Kinetic (30 Shields at 0.15 do not count)");
            var sixK = new Dictionary<string, int> { { "bui_KineticDefense", 6 } };
            Check(DefensePlanner.CounterDeficit(new Dictionary<string, int> { { "wea_Railgun", 200 } }, sixK, all) == null, "capped at 6 per weapon type");

            // Energy glut.
            Check(EconomyDirector.IsEnergyGlut(11545, 19845, 0.3f, 5000), "v0.10.1 Clowrus T21 (11.5k of 19.8k unspent) is energy glut");
            Check(!EconomyDirector.IsEnergyGlut(4000, 6000, 0.3f, 5000), "under the minimum is not");
            Check(!EconomyDirector.IsEnergyGlut(6000, 30000, 0.3f, 5000), "20% unspent is not");
            EconOption energy = new EconOption { Id = "e", Kind = OptionKind.Plant, Energy = 300, Material = 300, EnergyGain = 1000f };
            EconOption material = new EconOption { Id = "m", Kind = OptionKind.Producer, Energy = 300, Material = 300, MaterialGain = 700f };
            List<EconOption> options = new List<EconOption> { energy, material };
            EconomyBalance bal = new EconomyBalance { EnergyIncome = 20000, MaterialIncome = 9000, TargetRatio = 0.6f, EnergyBudget = 19000, MaterialBudget = 14000, EnergyPush = true };
            Check(EconomyDirector.Best(options, bal, 10) == energy, "setup: energy preferred without glut");
            bal.EnergyGlut = true;
            Check(EconomyDirector.Best(options, bal, 10) == material, "energy glut: material gains win");
            Check(Math.Abs(EconomyDirector.EnergyCostFactor(bal) - bal.GlutEnergyWeight) < 1e-6, "energy glut: energy cost at GlutEnergyWeight");
            bal.MaterialGlut = true;
            // v0.12 (was: energy wins): v0.11.1 turned the last planet material into ~180 Floating Plants with energy unspent.
            // v0.13: and extraction is not bought in material glut either (it only drains the planet faster).
            Check(EconomyDirector.Best(options, bal, 10) == null, "both gluts: energy gains worthless, extraction filtered: nothing");

            // Counter research: Lasers known, no Shield buildable -> Shield tech first.
            var countersWithShield = new Dictionary<string, Dictionary<string, float>>(counters);
            countersWithShield["bui_ShieldDefense"] = new Dictionary<string, float> { { "wea_Laser", 1f }, { "wea_Railgun", 0.15f } };
            var techs = new Dictionary<string, string> { { "bui_KineticDefense", "tec_MaglevServos" }, { "bui_IonDefense", "tec_PlasmaChannelInduction" }, { "bui_ShieldDefense", "tec_UltrasonicProjection" } };
            var lasers = new Dictionary<string, int> { { "wea_Laser", 14 }, { "wea_Railgun", 37 } };
            List<string> urgent = ResearchPlan.CounterTechs(lasers, countersWithShield, techs, id => id == "bui_KineticDefense");
            Check(urgent.Count == 1 && urgent[0] == "tec_UltrasonicProjection", "Lasers with no Shield: research Ultrasonic Projection; got " + string.Join(",", urgent.ToArray()));
            urgent = ResearchPlan.CounterTechs(lasers, countersWithShield, techs, id => true);
            Check(urgent.Count == 0, "everything buildable: nothing urgent");
            urgent = ResearchPlan.CounterTechs(new Dictionary<string, int> { { "wea_Railgun", 5 } }, countersWithShield, techs, id => false);
            Check(urgent.Count == 1 && urgent[0] == "tec_MaglevServos", "Railguns: Kinetic (0.8), not Shield (0.15); got " + string.Join(",", urgent.ToArray()));
            urgent = ResearchPlan.CounterTechs(new Dictionary<string, int> { { "wea_Railgun", 5 } }, countersWithShield, techs, id => id == "bui_ShieldDefense");
            Check(urgent.Count == 1 && urgent[0] == "tec_MaglevServos", "a Shield (0.15 vs Railguns) does not count as a Railgun counter; got " + string.Join(",", urgent.ToArray()));
            urgent = ResearchPlan.CounterTechs(new Dictionary<string, int> { { "wea_Laser", 0 } }, countersWithShield, techs, id => false);
            Check(urgent.Count == 0, "a weapon no longer known (count 0) asks for nothing");
        }

        // ---- v0.13: repair, no extraction in material glut, weapons ranked per slot -------------------
        private static void V015Rules()
        {
            // Volley allocation: weakest city first, move on once overkill x durability is reserved, weapons on the share draw.
            var c1 = new StrikeCandidate { IsCity = true, Durability = 600 };
            var c2 = new StrikeCandidate { IsCity = true, Durability = 300, Shadow = true };
            var w1 = new StrikeCandidate { IsCity = false, Durability = 330 };
            var list = new List<StrikeCandidate> { c1, c2, w1 };
            Check(VolleyPlanner.Pick(list, 1.25f, false) == 1, "weakest city first (shadow 300 before 600)");
            Check(VolleyPlanner.Pick(list, 1.25f, true) == 2, "the weapon share draw picks the weapon");
            c2.Allocated = 300;
            Check(VolleyPlanner.Pick(list, 1.25f, false) == 1, "300 reserved on 300 x 1.25: still open");
            c2.Allocated = 450;
            Check(VolleyPlanner.Pick(list, 1.25f, false) == 0, "450 reserved >= 375: next city");
            c1.Allocated = 800;
            Check(VolleyPlanner.Pick(list, 1.25f, false) == 0, "all cities full: the least over-reserved (450-375=75 vs 800-750=50 -> city 600)");
            Check(VolleyPlanner.Pick(new List<StrikeCandidate>(), 1.25f, false) == -1, "no candidates: -1 (native)");
            Check(VolleyPlanner.Pick(new List<StrikeCandidate> { new StrikeCandidate { IsCity = true, Durability = 0 } }, 1.25f, false) == -1, "a dead city is no target");
            // v0.14.1 T22: 8 Missiles (200) at 11 visible weapons of 330 HP all went at one; now at most 3 per weapon.
            var weapons = new List<StrikeCandidate>();
            for (int i = 0; i < 11; i++) weapons.Add(new StrikeCandidate { IsCity = false, Durability = 330 });
            var hitsOn = new int[11];
            for (int shot = 0; shot < 8; shot++)
            {
                int k = VolleyPlanner.Pick(weapons, 1.25f, true);
                weapons[k].Allocated += 200; hitsOn[k]++;
            }
            int maxOn = 0, distinct = 0;
            foreach (int h in hitsOn) { maxOn = Math.Max(maxOn, h); if (h > 0) distinct++; }
            Check(maxOn <= 3 && distinct >= 3, "8 shots spread over >= 3 weapons, <= 3 each; got max " + maxOn + ", distinct " + distinct);

            // Accuracy: guided at their own rate, unguided at the level rate, over live weapons.
            Check(Math.Abs(AccuracyPlan.MixedRate(20, 0.85f, 0, 0.7f) - 0.85f) < 1e-4, "all guided: 0.85");
            Check(Math.Abs(AccuracyPlan.MixedRate(10, 0.85f, 10, 0.7f) - 0.775f) < 1e-4, "half and half: 0.775");
            Check(Math.Abs(AccuracyPlan.MixedRate(0, 0.85f, 0, 0.7f) - 0.7f) < 1e-4, "no live weapons: the level rate");

            // Anti-missile cap counts defenses that protect a city.
            var cities = new List<Point> { new Point(0, 0, 0) };
            var cityR = new List<float> { 1.9f };
            var ions = new List<Point> { new Point(3, 0, 0), new Point(5, 0, 0), new Point(0, 4.3f, 0) };
            var ionR = new List<float> { 5.3f, 5.3f, 5.3f };
            Check(DefenseCoverage.ProtectingCount(cities, cityR, ions, ionR) == 2, "Ions within 5.3 - 0.95 = 4.35 of the centre protect: 2 of 3");

            // Intel sprint.
            Check(EconomyDirector.IntelPerTurnLimit(5, 6, 14, 2, 6, 3, 2) == 3, "unseen cities, T6, 2 of 6: 3 a turn");
            Check(EconomyDirector.IntelPerTurnLimit(5, 6, 14, 6, 6, 3, 2) == 2, "6 standing: back to the normal limit");
            Check(EconomyDirector.IntelPerTurnLimit(5, 15, 14, 2, 6, 3, 2) == 2, "after the sprint turn: normal");
            Check(EconomyDirector.IntelPerTurnLimit(0, 6, 14, 2, 6, 3, 2) == 2, "every city seen: normal");
        }

        private static void V0151Rules()
        {
            // Sol v0.15 #2: a city-cover placement must close a city deficit (reach = 5.3 - 0.95 = 4.35 from a city centre).
            var targets = new List<Point> { new Point(0, 0, 0), new Point(20, 0, 0), new Point(40, 0, 0) };
            var loss = new List<float> { 0.95f, 0.95f, 0f };
            var isCity = new List<bool> { true, true, false };
            int[] counts = { 2, 0, 0 };
            Check(!DefenseCoverage.ClosesCityDeficit(new Point(3, 0, 0), targets, loss, isCity, counts, 5.3f, 2), "over a city already at 2: refused");
            Check(DefenseCoverage.ClosesCityDeficit(new Point(23, 0, 0), targets, loss, isCity, counts, 5.3f, 2), "over the exposed city: accepted");
            Check(!DefenseCoverage.ClosesCityDeficit(new Point(40, 1, 0), targets, loss, isCity, counts, 5.3f, 2), "over a producer cluster: refused");
            Check(!DefenseCoverage.ClosesCityDeficit(new Point(25, 0, 0), targets, loss, isCity, counts, 5.3f, 2), "5 from the exposed city (reach 4.35): refused");
            counts[1] = 1;
            Check(DefenseCoverage.ClosesCityDeficit(new Point(23, 0, 0), targets, loss, isCity, counts, 5.3f, 2), "a city at 1 of 2 still takes one");

            // The measurement: fewest qualifying defenses over any city.
            var cities = new List<Point> { new Point(0, 0, 0), new Point(20, 0, 0) };
            var cityR = new List<float> { 1.9f, 1.9f };
            var ions = new List<Point> { new Point(3, 0, 0), new Point(0, 3, 0), new Point(23, 0, 0) };
            var ionR = new List<float> { 5.3f, 5.3f, 5.3f };
            Check(DefenseCoverage.MinCityCover(cities, cityR, ions, ionR) == 1, "min cover of 2 and 1 is 1");
            Check(DefenseCoverage.MinCityCover(cities, cityR, new List<Point>(), new List<float>()) == 0, "no Ions: 0");
            Check(DefenseCoverage.MinCityCover(new List<Point>(), new List<float>(), ions, ionR) == -1, "no cities: -1");

            // Sol v0.15 #4: the strategy estimate uses the firing rates per weapon.
            StrategySettings s = new StrategySettings { OverlapDefenses = 20f };
            var counters = EnhancedCounters();
            var missiles = new Dictionary<string, float> { { "wea_Missile", 20f } };
            var guidedRate = new Dictionary<string, float> { { "wea_Missile", 0.85f } };
            SalvoTarget level = Target(0, 1f, 0, true, 6000f);
            Strategy.EstimateSalvo(missiles, level, 0.7f, counters, s);
            SalvoTarget guided = Target(0, 1f, 0, true, 6000f);
            Strategy.EstimateSalvo(missiles, guided, 0.7f, counters, s, guidedRate);
            Check(Math.Abs(level.Arriving - 14f) < 1e-3 && Math.Abs(guided.Arriving - 17f) < 1e-3,
                "20 Missiles: 14 arrive at the level rate, 17 at the guided rate; got " + level.Arriving + ", " + guided.Arriving);
            SalvoTarget rails = Target(0, 1f, 0, true, 6000f);
            Strategy.EstimateSalvo(Railguns(20), rails, 0.7f, counters, s, guidedRate);
            Check(Math.Abs(rails.Arriving - 14f) < 1e-3, "Railguns keep the level rate; got " + rails.Arriving);
            StrategyDecision viaDecide = Strategy.Decide(20, true, missiles, 0.7f, new List<SalvoTarget> { Target(0, 1f, 0, true, 6000f) }, counters, -1000, 0, 20, s,
                -1, -1, false, guidedRate);
            Check(Math.Abs(viaDecide.Targets[0].Arriving - 17f) < 1e-3, "Decide passes the per-weapon rates on; got " + viaDecide.Targets[0].Arriving);

            // Sol v0.15 #5: the sprint waits while material is short.
            Check(EconomyDirector.IntelPerTurnLimit(5, 6, 14, 2, 6, 3, 2, 600f, 650f) == 2, "material 600 < 650 reserve: normal limit");
            Check(EconomyDirector.IntelPerTurnLimit(5, 6, 14, 2, 6, 3, 2, 900f, 650f) == 3, "material 900: sprint");
        }

        private static void V0152Rules()
        {
            StrategySettings s = new StrategySettings { OverlapDefenses = 20f };
            var counters = EnhancedCounters();
            // Return fire (v0.15.1 Thistle T19-T23 and T34-T37: 64-82% through, held for MinPassingShots 12 while hit).
            Check(Strategy.ReturnFire(8, 4.5f, 0.8f, true, s), "under attack, 8 weapons, 82% through, 4.5 shots: return fire");
            Check(!Strategy.ReturnFire(8, 4.5f, 0.8f, false, s), "not under attack: hold for saturation as before");
            Check(!Strategy.ReturnFire(8, 4.5f, 0.5f, true, s), "50% through (defenses are the limit): keep holding");
            Check(!Strategy.ReturnFire(5, 4.5f, 0.8f, true, s), "fewer than MinWeapons: keep building");
            Check(!Strategy.ReturnFire(8, 3f, 0.8f, true, s), "fewer than 4 shots through: no");
            Check(!Strategy.ReturnFire(8, 4.5f, 0.8f, true, new StrategySettings { ReturnFirePassShare = 0f }), "share 0 = off");
            Check(Strategy.ShouldAbort(0, 20, "return-fire", s), "v0.15.3: return fire with nothing aimed is called off (Sol v0.15.2 #6)");

            // Through Decide: 13 Railguns at 4 Kinetic, below MinPassingShots, fires only under attack.
            Func<bool, bool, StrategyDecision> decide = (grudge, hit) => Strategy.Decide(20, true, Railguns(13), 0.7f,
                new List<SalvoTarget> { Target(0, 1f, 4, true, 6000f) }, counters, 18, 1, 13, s, 0, 13, grudge, null, hit);
            StrategyDecision held = decide(false, false), fired = decide(true, true), grudgeOnly = decide(true, false);
            Check(!grudgeOnly.Fire, "v0.15.3: a decaying grudge without impacts last phase does not return fire (Sol v0.15.2 #2); got " + grudgeOnly.Reason);
            Check(!held.Fire && held.Reason == "below-saturation", "13 Railguns, not attacked: below-saturation; got " + held.Reason + " " + held.Passing);
            Check(fired.Fire && fired.Reason == "return-fire", "13 Railguns, attacked: return-fire; got " + fired.Reason + " share " + fired.PassShare);

            // Intel energy guard (v0.15.1 Thistle: 8 Telescopes at T6-T8, no plant).
            Check(!EconomyDirector.IntelEnergyAllowed(true, 400f, 450f), "unseen-only intel waits below the 450 reserve");
            Check(EconomyDirector.IntelEnergyAllowed(true, 500f, 450f), "unseen-only intel with 500 energy: allowed");
            Check(EconomyDirector.IntelEnergyAllowed(false, 100f, 450f), "intel needed (blind / below minimum): never held back");

            // v0.15.3 intel guard: native minimum exempt; blind or unseen-only guarded from the second building of a turn.
            Check(!EconomyDirector.IntelGuarded(true, true, 3), "native BelowMinimum: never guarded");
            Check(!EconomyDirector.IntelGuarded(false, true, 0), "blind, first intel building this turn: free");
            Check(EconomyDirector.IntelGuarded(false, true, 1), "blind, second building: guarded");
            Check(EconomyDirector.IntelGuarded(false, false, 0), "unseen cities only: guarded from the first");

            // City-cover ring: Ion 5.3, footprint 1.1, city 1.9 -> 3.1 .. 4.25.
            float inner, outer;
            DefenseCoverage.CityCoverRing(5.3f, 1.1f, 1.9f, 0.1f, out inner, out outer);
            Check(Math.Abs(inner - 3.1f) < 1e-4 && Math.Abs(outer - 4.25f) < 1e-4, "ring 3.1 .. 4.25; got " + inner + " .. " + outer);
            var ringCity = new List<Point> { new Point(0, 0, 0) };
            var ringLoss = new List<float> { 0.95f };
            var ringIsCity = new List<bool> { true };
            Check(DefenseCoverage.ClosesCityDeficit(new Point(outer, 0, 0), ringCity, ringLoss, ringIsCity, new[] { 0 }, 5.3f, 1), "the ring's outer edge still closes the deficit");
        }

        private static void V014Rules()
        {
            StrategySettings s = new StrategySettings { OverlapDefenses = 20f };
            // Attrition: the arsenal shrank by >= max(2, 15%) since last turn (v0.13.1 Guts: 26 -> 17 -> 11 -> 10 held).
            Check(Strategy.Attrition(17, 26, true, s), "26 -> 17 live weapons is attrition");
            Check(Strategy.Attrition(22, 26, true, s), "26 -> 22 (4 >= 3.9) is attrition");
            Check(!Strategy.Attrition(23, 26, true, s), "26 -> 23 (3 < 3.9) is not");
            Check(!Strategy.Attrition(9, 10, true, s), "10 -> 9 (1 < AttritionMinLoss 2) is not");
            Check(!Strategy.Attrition(4, 5, true, s), "5 -> 4: 20% but 1 < AttritionMinLoss 2 is not");
            Check(!Strategy.Attrition(17, -1, true, s), "unknown last turn is not");
            Check(!Strategy.Attrition(17, 26, false, s), "26 -> 17 without an attack (lost plants, a sale) is not");
            Check(!Strategy.Attrition(30, 26, true, s), "a growing arsenal is not");
            Check(!Strategy.Attrition(17, 26, true, new StrategySettings { AttritionLossShare = 0f }), "share 0 = off");
            Func<int, List<SalvoTarget>> human = kinetic => new List<SalvoTarget> { Target(0, 1f, kinetic, true, 6000f) };
            StrategyDecision held = Strategy.Decide(25, true, Railguns(17), 1f, human(19), EnhancedCounters(), -1000, 1, 17, s, 0, 17, true);
            Check(!held.Fire && held.Reason == "below-saturation", "17 weapons, steady arsenal: hold; got " + held.Reason);
            StrategyDecision shrinking = Strategy.Decide(25, true, Railguns(17), 1f, human(19), EnhancedCounters(), -1000, 1, 17, s, 0, 26, true);
            Check(shrinking.Fire && shrinking.Reason == "attrition", "17 weapons, 26 last turn: fire them; got " + shrinking.Reason);
            StrategyDecision quiet = Strategy.Decide(25, true, Railguns(17), 1f, human(19), EnhancedCounters(), -1000, 1, 17, s, 0, 26, false);
            Check(!quiet.Fire, "26 -> 17 with no attack: hold; got " + quiet.Reason);
            StrategyDecision pulse = Strategy.Decide(25, true, Railguns(17), 1f, human(19), EnhancedCounters(), 24, 0, 17, s, 0, 26, true);
            Check(pulse.Fire && pulse.Reason == "attrition", "attrition fires even right after a strike; got " + pulse.Reason);
            StrategyDecision peace = Strategy.Decide(15, false, Railguns(17), 1f, human(19), EnhancedCounters(), -1000, 0, 17, s, 0, 26, true);
            Check(!peace.Fire, "no attrition fire before war");
            Check(!Strategy.ShouldAbort(1, 17, "attrition", s), "an attrition strike is not called off");
            Check(Strategy.ShouldAbort(1, 17, "saturated", s), "a saturated strike with 1 aimed weapon still is");

            // Aim budget: clamp(total / all AI weapons, 30, 100) steps per weapon per frame (AIWeaponTargeting.BeginTurn).
            Check(AimBudget.StepsPerWeapon(1200, 26) == 46, "1v1, 26 weapons: 46 steps; got " + AimBudget.StepsPerWeapon(1200, 26));
            Check(AimBudget.StepsPerWeapon(1200, 11) == 100, "11 weapons: clamped to 100");
            Check(AimBudget.StepsPerWeapon(1200, 60) == 30, "60 weapons: floor 30");
            Check(AimBudget.StepsPerWeapon(2600, 26) == 100, "a 2600 budget gives 26 weapons the full 100");
            Check(AimBudget.StepsPerWeapon(1200, 0) == 30, "no weapons: the floor");

            // Placement backoff across turns (v0.13.1: Offshore Mines 6 abandoned at T14, 13 of 14 at T18).
            var none = new Dictionary<string, int>();
            var t14 = PlacementBackoff.Next(none, new Dictionary<string, int> { { "off", 6 } }, new Dictionary<string, int> { { "off", 6 } }, 14, 5);
            Check(PlacementBackoff.Throttled(t14, "off", 18), "6 abandoned at T14: still throttled at T18");
            Check(!PlacementBackoff.Throttled(t14, "off", 20), "released after 5 turns");
            Check(!PlacementBackoff.Throttled(t14, "mine", 15), "other types untouched");
            var t15 = PlacementBackoff.Next(t14, none, none, 15, 5);
            Check(PlacementBackoff.Throttled(t15, "off", 18), "a turn without trying keeps the throttle");
            var placed = PlacementBackoff.Next(t14, none, new Dictionary<string, int> { { "off", 2 } }, 15, 5);
            Check(!PlacementBackoff.Throttled(placed, "off", 16), "two placed with no failure: released");
            var partial = PlacementBackoff.Next(t14, new Dictionary<string, int> { { "off", 3 } }, new Dictionary<string, int> { { "off", 4 } }, 16, 5);
            Check(PlacementBackoff.Throttled(partial, "off", 21), "1 of 4 placed, 3 failed: throttle renewed to T21");
            var small = PlacementBackoff.Next(none, new Dictionary<string, int> { { "off", 2 } }, new Dictionary<string, int> { { "off", 2 } }, 14, 5);
            Check(!PlacementBackoff.Throttled(small, "off", 15), "2 failures do not start a backoff");
            Check(PlacementBackoff.Next(none, new Dictionary<string, int> { { "off", 6 } }, none, 14, 0).Count == 0, "turns 0 = off");

            // v0.21 (council I9): anchors on the building's own medium. Match 28 shape: a land solar plant whose anchor pool
            // is mostly offshore mines (water-only) and one land building.
            var pool = new List<bool> { true, true, false, true };
            Check(string.Join(",", AnchorMedium.Preferred(true, false, pool)) == "2", "land-only building: only the land anchor; got " + string.Join(",", AnchorMedium.Preferred(true, false, pool)));
            Check(string.Join(",", AnchorMedium.Preferred(false, true, pool)) == "0,1,3", "water-only building: only the water anchors");
            Check(string.Join(",", AnchorMedium.Preferred(true, true, pool)) == "0,1,2,3", "a building for both media keeps every anchor");
            var allWater = new List<bool> { true, true };
            Check(string.Join(",", AnchorMedium.Preferred(true, false, allWater)) == "0,1", "no land anchor: every anchor is kept (the game's behaviour, coastal spots)");
            Check(AnchorMedium.Preferred(true, false, new List<bool>()).Count == 0, "no anchors: none");

            // Opening: with no free slot (a queued plant's connectors are unusable this turn), one plant, then the material waits.
            EconOption mineT1 = new EconOption { Id = "mine", Kind = OptionKind.Producer, Energy = 300, Material = 300, MaterialGain = 100f, NeedsSlot = true };
            EconOption solarT1 = new EconOption { Id = "solar", Kind = OptionKind.Plant, Energy = 250, Material = 400, EnergyGain = 100f };
            EconOption trialsT1 = new EconOption { Id = "proj", Kind = OptionKind.Project, Energy = 150, Material = 350, MaterialGain = 200f };
            // v0.13.1 Guts T1 after one Solar Plant: 850 energy and 200 material income, 500 energy and 950 material left, 0 slots.
            EconomyBalance t1 = new EconomyBalance { EnergyIncome = 850, MaterialIncome = 200, EnergyBudget = 500, MaterialBudget = 950,
                SlotsLeft = 0, PlantsQueued = 1, Opening = true };
            Check(EconomyDirector.Best(new List<EconOption> { mineT1, solarT1 }, t1, 0) == null, "T1, one plant queued, no slot: no second plant");
            Check(EconomyDirector.Best(new List<EconOption> { mineT1, solarT1, trialsT1 }, t1, 0) == trialsT1, "a project still fits");
            t1.PlantsQueued = 0;
            Check(EconomyDirector.Best(new List<EconOption> { mineT1, solarT1 }, t1, 0) == solarT1, "no plant yet: the first plant");
            t1.PlantsQueued = 1; t1.Opening = false;
            Check(EconomyDirector.Best(new List<EconOption> { mineT1, solarT1 }, t1, 0) == solarT1, "after the opening, plants for slots continue");
            t1.Opening = true; t1.SlotsLeft = 2;
            Check(EconomyDirector.Best(new List<EconOption> { mineT1, solarT1 }, t1, 2) == mineT1, "with free slots: the Mine");
            Check(EconomyDirector.Best(new List<EconOption> { solarT1 }, t1, 2) == solarT1, "with free slots, a plant is not blocked");
            EconomyBalance shortE = new EconomyBalance { EnergyIncome = 100, MaterialIncome = 2000, EnergyBudget = 500, MaterialBudget = 950,
                SlotsLeft = 0, PlantsQueued = 1, Opening = true };
            Check(EconomyDirector.Best(new List<EconOption> { solarT1 }, shortE, 0) == solarT1, "energy short: plants continue");

            // Defense floor 0.25 (v0.13.1 T21: 140 non-city buildings, blind): 35 wanted, not 21.
            Check(EconomyDirector.DesiredDefenses(0, 0f, 0.4f, 500f, 60, true, 140, 0.25f) == 35, "floor 0.25 x 140 = 35");
        }

        private static void V013Rules()
        {
            // Repair: weapons, then defenses, then power; most damaged first; within budgets and count.
            RepairCandidate mineHit = new RepairCandidate { Priority = 4, HpShare = 0.2f, Energy = 100, Material = 150 };
            RepairCandidate railHit = new RepairCandidate { Priority = 0, HpShare = 0.5f, Energy = 100, Material = 100 };
            RepairCandidate railBad = new RepairCandidate { Priority = 0, HpShare = 0.3f, Energy = 100, Material = 100 };
            RepairCandidate kinetic = new RepairCandidate { Priority = 1, HpShare = 0.6f, Energy = 100, Material = 100 };
            RepairCandidate scratched = new RepairCandidate { Priority = 0, HpShare = 0.9f, Energy = 100, Material = 100 };
            RepairCandidate whole = new RepairCandidate { Priority = 0, HpShare = 1f, Energy = 100, Material = 100 };
            var all = new List<RepairCandidate> { mineHit, railHit, railBad, kinetic, scratched, whole };
            List<RepairCandidate> got = RepairPlanner.Pick(all, 10000, 10000, 40, 0.8f);
            Check(got.Count == 4 && got[0] == railBad && got[1] == railHit && got[2] == kinetic && got[3] == mineHit,
                "order: weapons (most damaged first), defense, mine; 90% and full HP skipped; got " + got.Count);
            got = RepairPlanner.Pick(all, 250, 10000, 40, 0.8f);
            Check(got.Count == 2 && got[0] == railBad && got[1] == railHit, "energy for two repairs: the two weapons");
            got = RepairPlanner.Pick(all, 10000, 10000, 1, 0.8f);
            Check(got.Count == 1 && got[0] == railBad, "maxCount 1: the most damaged weapon");
            RepairCandidate pricey = new RepairCandidate { Priority = 0, HpShare = 0.1f, Energy = 100, Material = 1000 };
            got = RepairPlanner.Pick(new List<RepairCandidate> { pricey, kinetic }, 10000, 500, 40, 0.8f);
            Check(got.Count == 1 && got[0] == kinetic, "one that does not fit is skipped, a cheaper one still fits");
            got = RepairPlanner.Pick(all, 10000, 10000, 40, 0.95f);
            Check(got.Contains(scratched), "threshold 0.95 includes the 90% one");

            // Material glut: no extraction (mines, material upgrades/projects); plants, Trials and refineries stay.
            EconOption mine = new EconOption { Id = "mine", Kind = OptionKind.Producer, Energy = 300, Material = 300, MaterialGain = 175f, NeedsSlot = true };
            EconOption matUp = new EconOption { Id = "mup", Kind = OptionKind.Upgrade, Energy = 50, Material = 200, MaterialGain = 120f };
            EconOption plant = new EconOption { Id = "np", Kind = OptionKind.Plant, Energy = 250, Material = 450, EnergyGain = 200f };
            EconOption refinery = new EconOption { Id = "ref", Kind = OptionKind.Upgrade, Energy = 50, Material = 200, MaterialGain = 60f, SavesReserve = true };
            EconOption trials = new EconOption { Id = "trials", Kind = OptionKind.Project, Energy = 150, Material = 300, EnergyGain = 3000f, MaterialGain = -500f };
            // v0.12.1 Clowrus T23: 21.2k energy, 26.8k material income, 57.3k stock, material glut.
            EconomyBalance glut = new EconomyBalance { EnergyIncome = 21246, MaterialIncome = 26813, TargetRatio = 0.6f, WeaponUpkeep = 3000,
                EnergyBudget = 20000, MaterialBudget = 57340, MaterialGlut = true };
            Check(EconomyDirector.Best(new List<EconOption> { mine }, glut, 10) == null, "material glut: no Mine");
            Check(EconomyDirector.Best(new List<EconOption> { matUp }, glut, 10) == null, "material glut: no material yield upgrade");
            Check(EconomyDirector.Best(new List<EconOption> { mine, plant }, glut, 10) == plant, "material glut: the plant");
            Check(EconomyDirector.Best(new List<EconOption> { refinery }, glut, 10) == refinery, "material glut: a planet-saving refinery stays");
            Check(EconomyDirector.Best(new List<EconOption> { trials }, glut, 10) == trials, "material glut: Reactor Trials stays");
            EconOption mixed = new EconOption { Id = "mix", Kind = OptionKind.Project, Energy = 150, Material = 300, EnergyGain = 500f, MaterialGain = 500f };
            Check(EconomyDirector.Best(new List<EconOption> { mixed }, glut, 10) == mixed, "material glut: an option that also gains energy stays");
            glut.MaterialGlut = false;
            glut.EnergyIncome = 5000;
            Check(EconomyDirector.Best(new List<EconOption> { mine }, glut, 10) == mine, "setup: without glut the Mine is bought");

            // Weapons per slot: v0.12.1 observed Railgun pass ~0.45 at a human with Kinetic only.
            WeaponStats rail = new WeaponStats { BuildingId = "bui_Railgun", WeaponId = "wea_Railgun", BuildEnergy = 200, BuildMaterial = 200, FiringEnergy = 100, Damage = 375f };
            WeaponStats missile = new WeaponStats { BuildingId = "bui_Missile", WeaponId = "wea_Missile", BuildEnergy = 250, BuildMaterial = 450, FiringEnergy = 300, Damage = 455f };
            var observed = new Dictionary<string, float> { { "wea_Railgun", 0.45f } };
            var none = new Dictionary<string, int>();
            // Unseen defenses, no shots yet: by cost the cheap Railgun wins; per slot the Missile's damage does.
            float rCost = EconomyDirector.WeaponScore(rail, none, EnhancedCounters(), 10f, 4f);
            float mCost = EconomyDirector.WeaponScore(missile, none, EnhancedCounters(), 10f, 4f);
            Check(rCost > mCost, "setup: by cost, nothing observed, the Railgun wins (" + rCost + " vs " + mCost + ")");
            Check(EconomyDirector.WeaponScore(missile, none, EnhancedCounters(), 10f, 4f, null, true) > EconomyDirector.WeaponScore(rail, none, EnhancedCounters(), 10f, 4f, null, true),
                "per slot, nothing observed: the Missile");
            float rSlot = EconomyDirector.WeaponScore(rail, none, EnhancedCounters(), 10f, 4f, observed, true);
            float mSlot = EconomyDirector.WeaponScore(missile, none, EnhancedCounters(), 10f, 4f, observed, true);
            Check(mSlot > rSlot && Math.Abs(rSlot - 375f * 0.45f) < 0.01f, "per slot: Missile (455 through) beats Railgun (169 through)");
            WeaponStats only = EconomyDirector.PickWeapon(new List<WeaponStats> { rail, missile }, none, EnhancedCounters(), 10f, 4f, 0.6f, new Random(1), observed, true);
            Check(only == missile, "per slot with band 0.6: only the Missile qualifies (169 < 0.6 x 455)");

            // v0.13.1 (Sol #4): Railgun interception is evidence about Coilcannons (both stopped by Kinetic), not Missiles.
            var shared = new Dictionary<string, float> { { "wea_Railgun", 0.45f } };
            EconomyDirector.ShareObservedPass(shared, new List<string> { "wea_Railgun", "wea_Coilcannon", "wea_Missile", "wea_Laser" }, EnhancedCounters(), 0.5f);
            Check(shared.ContainsKey("wea_Coilcannon") && Math.Abs(shared["wea_Coilcannon"] - 0.45f) < 1e-6, "Coilcannon inherits the Railgun's 0.45");
            Check(!shared.ContainsKey("wea_Missile"), "Missile (stopped by Ion) does not");
            Check(!shared.ContainsKey("wea_Laser"), "a weapon nothing in the table stops does not");
            var both = new Dictionary<string, float> { { "wea_Railgun", 0.45f }, { "wea_Coilcannon", 0.9f } };
            EconomyDirector.ShareObservedPass(both, new List<string> { "wea_Railgun", "wea_Coilcannon" }, EnhancedCounters(), 0.5f);
            Check(Math.Abs(both["wea_Coilcannon"] - 0.9f) < 1e-6, "an observed value is never overwritten");
            var lowStop = new Dictionary<string, Dictionary<string, float>> {
                { "bui_KineticDefense", new Dictionary<string, float> { { "wea_Railgun", 0.8f }, { "wea_Coilcannon", 0.3f } } } };
            var weak = new Dictionary<string, float> { { "wea_Railgun", 0.45f } };
            EconomyDirector.ShareObservedPass(weak, new List<string> { "wea_Coilcannon" }, lowStop, 0.5f);
            Check(!weak.ContainsKey("wea_Coilcannon"), "a defense below minChance does not make two weapons alike");
            var unstopped = new Dictionary<string, float> { { "wea_Laser", 0.3f } };
            EconomyDirector.ShareObservedPass(unstopped, new List<string> { "wea_Laser", "wea_SolarLaser" }, EnhancedCounters(), 0.5f);
            Check(!unstopped.ContainsKey("wea_SolarLaser"), "two weapons nothing in the table stops are not alike");
            WeaponStats coil = new WeaponStats { BuildingId = "bui_Coilcannon", WeaponId = "wea_Coilcannon", BuildEnergy = 250, BuildMaterial = 400, FiringEnergy = 200, Damage = 1320f };
            float cSlot = EconomyDirector.WeaponScore(coil, none, EnhancedCounters(), 10f, 4f, shared, true);
            Check(Math.Abs(cSlot - 1320f * 0.45f) < 0.01f && 455f >= 0.6f * cSlot, "with shared evidence the Missile (455) stays in the band of the Coilcannon (594)");

            // v0.13.1 (Sol #1): energy spent on more energy counts as unspent. v0.11.1 Beam T24: 6.7k of 27.9k left.
            Check(!EconomyDirector.IsEnergyGlut(6670, 27920, 0.3f, 5000), "setup: 6.7k left alone is not glut (24%)");
            Check(EconomyDirector.IsEnergyGlut(6670 + 7500, 27920, 0.3f, 5000), "6.7k left + 7.5k spent on 30 Floating Plants: glut");

            // v0.13.1: weapons that hit us count as known, decayed, never lowering what we saw.
            var incoming = new Dictionary<string, float>();
            var known = new Dictionary<string, int> { { "wea_Railgun", 30 } };
            DefensePlanner.RememberIncoming(incoming, new Dictionary<string, int> { { "wea_Laser", 14 }, { "wea_Railgun", 10 }, { "?", 5 } }, 2f, 0.95f, known);
            Check(known["wea_Laser"] == 7, "14 Laser impacts at 2 per weapon: 7 Lasers known; got " + (known.ContainsKey("wea_Laser") ? known["wea_Laser"] : -1));
            Check(known["wea_Railgun"] == 30, "seen 30 Railguns stay 30 (10 impacts do not lower it)");
            Check(!known.ContainsKey("?"), "unknown weapon ids are ignored");
            var later = new Dictionary<string, int>();
            DefensePlanner.RememberIncoming(incoming, new Dictionary<string, int>(), 2f, 0.95f, later);
            Check(later.ContainsKey("wea_Laser") && later["wea_Laser"] == 7, "a quiet turn (the human pulses): still remembered (7 x 0.95)");
            DefensePlanner.RememberIncoming(incoming, null, 2f, 0.5f, later);
            Check(later["wea_Laser"] == 7, "known is never lowered by the memory");
            var lighter = new Dictionary<string, float> { { "wea_Laser", 7f } };
            var lighterKnown = new Dictionary<string, int>();
            DefensePlanner.RememberIncoming(lighter, new Dictionary<string, int> { { "wea_Laser", 2 } }, 2f, 0.95f, lighterKnown);
            Check(lighterKnown["wea_Laser"] == 7, "a lighter turn (2 impacts) does not wipe the memory of 7");
            var fresh = new Dictionary<string, int>();
            DefensePlanner.RememberIncoming(incoming, null, 2f, 0.5f, fresh);
            Check(fresh.ContainsKey("wea_Laser") && fresh["wea_Laser"] == 2, "decay applies each turn (7 x 0.95 x 0.5 x 0.5 = 1.7)");
            var counterKnown = new Dictionary<string, int>();
            DefensePlanner.RememberIncoming(new Dictionary<string, float>(), new Dictionary<string, int> { { "wea_Laser", 28 } }, 2f, 0.95f, counterKnown);
            Check(DefensePlanner.CounterDeficit(counterKnown, new Dictionary<string, int>(), new List<string> { "bui_ShieldDefense", "bui_KineticDefense" }) == "bui_ShieldDefense",
                "v0.12.1: 28 Laser impacts and no Laser building ever seen -> a Shield is wanted");
        }

        // ---- v0.12: material budget, energy saturation, fire what can aim, reachability ----------------
        private static void V012Rules()
        {
            // Sustainable material: (planet + bank) / turns to the horizon (at least minTurns); -1 when unlimited.
            Check(Math.Abs(EconomyDirector.SustainableMaterialIncome(124334, 0, true, 24, 45, 3) - 124334f / 21f) < 1f, "Beam T24: 124k over 21 turns");
            Check(Math.Abs(EconomyDirector.SustainableMaterialIncome(100000, 26000, true, 24, 45, 3) - 6000f) < 1f, "the bank counts");
            Check(Math.Abs(EconomyDirector.SustainableMaterialIncome(30000, 0, true, 44, 45, 3) - 10000f) < 1f, "near the horizon: at least minTurns");
            Check(EconomyDirector.SustainableMaterialIncome(0, 0, true, 30, 45, 3) == 0f, "dry planet, no bank: 0");
            Check(EconomyDirector.SustainableMaterialIncome(50000, 0, false, 30, 45, 3) < 0f, "unlimited planet: -1");
            Check(EconomyDirector.PlanetMaterialFactor(0, 0f, 10) == 0f, "dry planet: extraction gains worth nothing (Sol #1)");

            // Energy saturation, v0.11.1 Beam numbers (Floating Plant wave T22-27).
            EconomyBalance t18 = new EconomyBalance { EnergyIncome = 6792, MaterialIncome = 10884, TargetRatio = 0.6f,
                SustainableMaterialIncome = EconomyDirector.SustainableMaterialIncome(276774, 17282, true, 18, 45, 3) };
            Check(!EconomyDirector.EnergySaturated(t18), "T18 (6.8k energy, planet not yet binding): not saturated");
            EconomyBalance t22 = new EconomyBalance { EnergyIncome = 20118, MaterialIncome = 16193, TargetRatio = 0.6f, WeaponUpkeep = 6000,
                SustainableMaterialIncome = EconomyDirector.SustainableMaterialIncome(190865, 23378, true, 22, 45, 3) };
            Check(EconomyDirector.EnergySaturated(t22), "T22 (20k energy, 9.3k sustainable material): saturated");
            t22.WeaponUpkeep = 12000;
            Check(!EconomyDirector.EnergySaturated(t22), "upkeep counts toward wanted energy");
            EconomyBalance roomy = new EconomyBalance { EnergyIncome = 50000, MaterialIncome = 5000, TargetRatio = 0.6f,
                SustainableMaterialIncome = 8000f };
            Check(!EconomyDirector.EnergySaturated(roomy), "planet not binding (sustainable above production): never saturated");
            EconomyBalance unlimited = new EconomyBalance { EnergyIncome = 50000, MaterialIncome = 5000, TargetRatio = 0.6f };
            Check(!EconomyDirector.EnergySaturated(unlimited), "unlimited planet: never saturated");
            // Sol v0.12 #1: the adapter's real dry state - material income 0, Brother T26 (75.7k banked, material glut, no energy glut).
            EconomyBalance dry = new EconomyBalance { EnergyIncome = 37034, MaterialIncome = 0, TargetRatio = 0.6f, WeaponUpkeep = 8000,
                SustainableMaterialIncome = EconomyDirector.SustainableMaterialIncome(0, 75735, true, 26, 45, 3),
                EnergyBudget = 30000, MaterialBudget = 75735, MaterialGlut = true };
            Check(EconomyDirector.EnergySaturated(dry), "dry planet (income 0): saturated");
            EconOption plant = new EconOption { Id = "fp", Kind = OptionKind.Plant, Energy = 250, Material = 300, EnergyGain = 100f };
            Check(EconomyDirector.Best(new List<EconOption> { plant }, dry, 10) == null, "dry + material glut: no Floating Plant");
            dry.WeaponUpkeep = 30000;
            Check(!EconomyDirector.EnergySaturated(dry), "dry, but firing needs the energy: not saturated");
            dry.WeaponUpkeep = 8000;
            dry.EnergyIncome = 14000;
            Check(!EconomyDirector.EnergySaturated(dry), "dry: spending the 75.7k bank needs energy too (14k < 1.5 x (8k + 0.6 x 4k))");
            dry.EnergyIncome = 37034;
            dry.WeaponUpkeep = 8000;
            dry.EnergySaturation = 0f;
            Check(!EconomyDirector.EnergySaturated(dry), "EnergySaturation 0 = off");

            // Saturated: plants and energy upgrades are filtered; projects (Reactor Trials) and material options are not.
            EconOption mineUp = new EconOption { Id = "up", Kind = OptionKind.Upgrade, Energy = 50, Material = 250, MaterialGain = 40f };
            EconOption energyUp = new EconOption { Id = "eup", Kind = OptionKind.Upgrade, Energy = 50, Material = 150, EnergyGain = 75f };
            EconOption trials = new EconOption { Id = "trials", Kind = OptionKind.Project, Energy = 150, Material = 300, EnergyGain = 3000f, MaterialGain = -500f };
            EconomyBalance sat = new EconomyBalance { EnergyIncome = 20118, MaterialIncome = 16193, TargetRatio = 0.6f, WeaponUpkeep = 6000,
                SustainableMaterialIncome = 9300f, EnergyBudget = 20000, MaterialBudget = 23000, MaterialGlut = true };
            Check(EconomyDirector.Best(new List<EconOption> { plant, mineUp }, sat, 10) == null, "saturated + material glut: no Floating Plant, no extraction");
            sat.MaterialGlut = false;
            Check(EconomyDirector.Best(new List<EconOption> { plant, mineUp }, sat, 10) == mineUp, "saturated, no glut: the material option, not the plant");
            sat.MaterialGlut = true;
            Check(EconomyDirector.Best(new List<EconOption> { plant }, sat, 10) == null, "saturated: a plant alone is not bought");
            Check(EconomyDirector.Best(new List<EconOption> { energyUp }, sat, 10) == null, "saturated: an energy yield upgrade is not bought");
            Check(EconomyDirector.Best(new List<EconOption> { trials }, sat, 10) == trials, "saturated: Reactor Trials still can be (Sol #5)");
            sat.SustainableMaterialIncome = -1f;
            Check(EconomyDirector.Best(new List<EconOption> { plant }, sat, 10) == plant, "setup: unlimited planet, material glut buys the plant");

            // Economy reserve: once the planet runs dry within the warning, the economy cannot spend the reserve.
            Check(EconomyDirector.EconomyReserve(124334, 27504, true, 8, 15000) == 15000, "Beam T24 (4.5 turns left): reserve on");
            Check(EconomyDirector.EconomyReserve(190865, 16193, true, 8, 15000) == 0, "Beam T22 (11.8 turns left): off");
            Check(EconomyDirector.EconomyReserve(0, 0f, true, 8, 15000) == 15000, "dry planet: on");
            Check(EconomyDirector.EconomyReserve(0, 0f, false, 8, 15000) == 0, "unlimited planet: off");
            Check(EconomyDirector.EconomyReserve(0, 0f, true, 8, 0) == 0, "reserve 0 = off");
            EconomyBalance reserved = new EconomyBalance { EnergyIncome = 1000, MaterialIncome = 1000, TargetRatio = 0.6f,
                EnergyBudget = 20000, MaterialBudget = 15200, MaterialReserve = 15000 };
            Check(EconomyDirector.Best(new List<EconOption> { mineUp }, reserved, 10) == null, "250 material over a 15k reserve of 15.2k: not affordable");
            reserved.MaterialBudget = 15300;
            Check(EconomyDirector.Best(new List<EconOption> { mineUp }, reserved, 10) == mineUp, "15.3k: affordable");

            // Fire what can aim: abort only below AbortBelowWeapons (share 0 by default).
            StrategySettings s = new StrategySettings();
            Check(!Strategy.ShouldAbort(6, 53, "saturated", s), "v0.11.1 Brother T27: 6 of 53 aimed fires now");
            Check(Strategy.ShouldAbort(2, 52, "saturated", s), "2 aimed: called off");
            Check(!Strategy.ShouldAbort(3, 52, "saturated", s), "3 aimed (the minimum): fires");
            Check(!Strategy.ShouldAbort(0, 52, "max-hold", s), "max-hold fires whatever it has");
            s.AbortBelowShare = 0.3f;
            Check(Strategy.ShouldAbort(15, 52, "saturated", s), "with the v0.11 share 0.3: 15 of 52 is called off");

            // Sol v0.12 #2: a target nothing aims at moves the commit; max-hold comes before another "saturated" strike.
            StrategySettings st = new StrategySettings();
            var rails = Railguns(40);
            SalvoTarget hard = Target(1, 0.5f, 0, false, 0f);
            SalvoTarget other = Target(2, 0.5f, 6, false, 0f);
            StrategyDecision pick = Strategy.Decide(30, true, rails, 0.8f, new List<SalvoTarget> { hard, other }, EnhancedCounters(), -1000, 0, 40, st, 1);
            Check(pick.CommitTargetId == 1, "setup: the undefended target is committed");
            hard = Target(1, 0.5f, 0, false, 0f); hard.LowYieldTurns = 2;
            other = Target(2, 0.5f, 6, false, 0f);
            pick = Strategy.Decide(30, true, rails, 0.8f, new List<SalvoTarget> { hard, other }, EnhancedCounters(), -1000, 0, 40, st, 1);
            Check(pick.CommitTargetId == 2, "two low-yield turns at it: the commit moves; got " + pick.CommitTargetId);
            hard = Target(1, 0.5f, 0, false, 0f); hard.LowYieldTurns = 1;
            other = Target(2, 0.5f, 6, false, 0f);
            pick = Strategy.Decide(30, true, rails, 0.8f, new List<SalvoTarget> { hard, other }, EnhancedCounters(), -1000, 0, 40, st, 1);
            Check(pick.CommitTargetId == 1, "one low-yield turn: not yet");
            pick = Strategy.Decide(30, true, rails, 0.8f, new List<SalvoTarget> { Target(1, 0.5f, 0, false, 0f) }, EnhancedCounters(), -1000, 5, 40, st, 1);
            Check(pick.Fire && pick.Reason == "max-hold", "5 held turns and saturated: max-hold (exempt from the abort); got " + pick.Reason);

            // Reachability: failures count only where enough weapons at that target found a trajectory.
            ReachabilityMemory reach = new ReachabilityMemory(3);
            List<string> keys = new List<string>();
            List<int> targets = new List<int>();
            List<bool> found = new List<bool>();
            for (int i = 0; i < 50; i++) { keys.Add("w" + i); targets.Add(7); found.Add(i < 10); }
            int marked = reach.ReportTurn(keys, targets, found, 25, 0.5f);
            HashSet<int> unreachable = new HashSet<int>();
            reach.FillUnreachable("w20", 26, unreachable);
            Check(marked == 0 && !unreachable.Contains(7), "10 of 50 found (search out of time): nobody marks the target unreachable");
            keys.Clear(); targets.Clear(); found.Clear();
            for (int i = 0; i < 10; i++) { keys.Add("w" + i); targets.Add(8); found.Add(i < 7); }
            marked = reach.ReportTurn(keys, targets, found, 25, 0.5f);
            reach.FillUnreachable("w8", 26, unreachable);
            Check(marked == 3 && unreachable.Contains(8), "7 of 10 found: the 3 that failed mark it unreachable");
            reach.FillUnreachable("w2", 26, unreachable);
            Check(!unreachable.Contains(8), "a weapon that found one is not marked");
            reach.ReportTurn(new List<string> { "w8" }, new List<int> { 8 }, new List<bool> { true }, 26, 0.5f);
            reach.FillUnreachable("w8", 27, unreachable);
            Check(!unreachable.Contains(8), "a later success clears the mark");
            // Per target: a well-aimed target does not lend its share to a badly aimed one.
            keys.Clear(); targets.Clear(); found.Clear();
            for (int i = 0; i < 10; i++) { keys.Add("a" + i); targets.Add(1); found.Add(true); }
            for (int i = 0; i < 10; i++) { keys.Add("b" + i); targets.Add(2); found.Add(i < 2); }
            marked = reach.ReportTurn(keys, targets, found, 30, 0.5f);
            Check(marked == 0, "target 2 at 2 of 10 is not marked even though target 1 was 10 of 10");
        }

        private static void MaterialGlutRules()
        {
            Check(EconomyDirector.IsMaterialGlut(17836, 1829, 3f, 5000), "v0.9.1 Guts T21 (17.8k material, 1.8k energy) is glut");
            Check(!EconomyDirector.IsMaterialGlut(6948, 3045, 3f, 5000), "v0.9.1 T15 (6.9k vs 3.0k) is not");
            Check(!EconomyDirector.IsMaterialGlut(4000, 500, 3f, 5000), "under the minimum is never glut");

            // Energy income above 0.6 x material income + upkeep: without glut material gains win, with glut energy gains.
            EconOption energy = new EconOption { Id = "trials", Kind = OptionKind.Project, Energy = 150, Material = 300, EnergyGain = 1100f, MaterialGain = -360f };
            EconOption material = new EconOption { Id = "matproj", Kind = OptionKind.Project, Energy = 150, Material = 350, EnergyGain = 0f, MaterialGain = 900f };
            List<EconOption> options = new List<EconOption> { energy, material };
            EconomyBalance b = new EconomyBalance { EnergyIncome = 6300, MaterialIncome = 6000, WeaponUpkeep = 2500, TargetRatio = 0.6f, EnergyBudget = 2000, MaterialBudget = 30000 };
            Check(EconomyDirector.Best(options, b, 10) == material, "v0.9 weights prefer the material project here (the bug)");
            b.MaterialGlut = true;
            Check(EconomyDirector.Best(options, b, 10) == energy, "in material glut the energy project wins");
            float wE, wM;
            EconomyDirector.Weights(b, out wE, out wM);
            Check(wE == 1f && Math.Abs(wM - b.GlutMaterialWeight) < 1e-6, "glut weights: energy 1, material GlutMaterialWeight");

            // Same gain, one costs energy, the other material: in glut material is cheap, so spend it and keep the energy.
            EconOption energyHeavy = new EconOption { Id = "eh", Kind = OptionKind.Plant, Energy = 300, Material = 100, EnergyGain = 1000f };
            EconOption materialHeavy = new EconOption { Id = "mh", Kind = OptionKind.Plant, Energy = 100, Material = 700, EnergyGain = 1000f };
            List<EconOption> pair = new List<EconOption> { energyHeavy, materialHeavy };
            EconomyBalance plain = new EconomyBalance { EnergyIncome = 6300, MaterialIncome = 6000, TargetRatio = 0.6f, EnergyBudget = 2000, MaterialBudget = 30000 };
            Check(EconomyDirector.Best(pair, plain, 10) == energyHeavy, "without glut, material counts at full price");
            plain.MaterialGlut = true;
            Check(EconomyDirector.Best(pair, plain, 10) == materialHeavy, "in glut, material cost counts at GlutMaterialWeight");
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

        private static void CoverageRules()
        {
            Point P(float x) { return new Point(x, 0f, 0f); }
            float r = 5.9f;

            // A defense covers what is strictly inside its radius.
            var line = new List<Point> { P(0f), P(3f), P(5.9f), P(7f) };
            bool[] c = DefenseCoverage.Covered(line, new List<Point> { P(0f) }, r);
            Check(c[0] && c[1] && !c[2] && !c[3], "radius 5.9: 0 and 3 covered, 5.9 (not strictly inside) and 7 not");

            bool[] upgraded = DefenseCoverage.Covered(line, new List<Point> { P(0f), P(40f) }, new List<float> { 7.5f, 5.9f });
            Check(upgraded[2] && upgraded[3], "per-defense radius: an upgraded defense (7.5) also covers 5.9 and 7");
            Check(!DefenseCoverage.Covered(line, new List<Point> { P(0f) }, new List<float> { 2f })[1], "per-defense radius 2: 3 is outside");
            var values = new List<float> { 4f, 1f, 1f, 1f };
            Check(Math.Abs(DefenseCoverage.UncoveredValue(P(2f), line, values, new[] { false, false, false, false }, r) - 7f) < 1e-4f, "everything within 5.9 of 2 and uncovered: 4+1+1+1 = 7");
            Check(Math.Abs(DefenseCoverage.UncoveredValue(P(2f), line, values, new[] { true, false, false, false }, r) - 3f) < 1e-4f, "the covered city is not counted again");
            Check(Math.Abs(DefenseCoverage.CoveredShare(values, new[] { true, false, false, false }) - 4f / 7f) < 1e-4f, "covered share by value: 4 of 7");

            // Two clusters: A (x 0-4, 6 buildings) already covered, B (x 30-34, 4 buildings) not. The anchor is in B.
            var targets = new List<Point>();
            var tv = new List<float>();
            for (int i = 0; i < 6; i++) { targets.Add(P(i * 0.8f)); tv.Add(1f); }
            for (int i = 0; i < 4; i++) { targets.Add(P(30f + i)); tv.Add(1f); }
            var anchors = new List<Point> { P(1f), P(2f), P(31f), P(32f) };
            bool[] covered = DefenseCoverage.Covered(targets, new List<Point> { P(2f) }, r);
            var rng = new Random(3);
            int pick = DefenseCoverage.PickAnchor(anchors, targets, tv, covered, r, 0.6f, rng, null);
            Check(pick == 2 || pick == 3, "anchor next to the uncovered cluster, not the covered one (picked " + pick + ")");

            // A city (value 4) beats three ordinary buildings.
            var t2 = new List<Point> { P(0f), P(20f), P(21f), P(22f) };
            var v2 = new List<float> { 4f, 1f, 1f, 1f };
            Check(DefenseCoverage.PickAnchor(new List<Point> { P(0.5f), P(21f) }, t2, v2, new[] { false, false, false, false }, r, 1.0f, rng, null) == 0,
                "city 4 > 3 buildings: the city anchor wins with band 1.0");

            Check(DefenseCoverage.PickAnchor(anchors, targets, tv, DefenseCoverage.Covered(targets, new List<Point> { P(2f), P(31f) }, r), r, 0.6f, rng, null) == -1,
                "everything covered: no anchor");
            var excluded = new HashSet<int> { 2, 3 };
            Check(DefenseCoverage.PickAnchor(anchors, targets, tv, covered, r, 0.6f, rng, excluded) == -1, "excluded anchors are skipped (the others cover nothing uncovered)");

            // Greedy over three clusters: three defenses cover all three, one each (native random picks stack).
            var cl = new List<Point>();
            var cv = new List<float>();
            foreach (float x0 in new[] { 0f, 40f, 80f })
            {
                for (int i = 0; i < 5; i++) { cl.Add(P(x0 + i)); cv.Add(1f); }
            }
            var all = new List<Point>(cl);
            var defenses = new List<Point>();
            var used = new HashSet<int>();
            for (int step = 0; step < 3; step++)
            {
                bool[] now = DefenseCoverage.Covered(cl, defenses, r);
                int a = DefenseCoverage.PickAnchor(all, cl, cv, now, r, 0.9f, rng, null);
                Check(a >= 0, "step " + step + ": an anchor is found");
                if (a < 0) break;
                used.Add((int)(cl[a].X / 40f + 0.5f));
                defenses.Add(cl[a]);
            }
            Check(used.Count == 3, "three defenses went to three different clusters (" + used.Count + ")");
            Check(DefenseCoverage.CoveredShare(cv, DefenseCoverage.Covered(cl, defenses, r)) > 0.99f, "all three clusters covered after three defenses");

            // PickAnchors: one scoring pass, many draws; same band rule; empty when nothing is uncovered.
            var draws = DefenseCoverage.PickAnchors(anchors, targets, tv, covered, r, 0.6f, rng, 12);
            Check(draws.Count == 12 && draws.TrueForAll(i => i == 2 || i == 3), "12 draws, all next to the uncovered cluster");
            var t3 = new List<Point> { P(0f), P(20f), P(21f), P(22f) };
            var only = DefenseCoverage.PickAnchors(new List<Point> { P(0.5f), P(21f) }, t3, new List<float> { 4f, 1f, 1f, 1f }, new[] { false, false, false, false }, r, 1.0f, rng, 20);
            Check(only.Count == 20 && only.TrueForAll(i => i == 0), "band 1.0: only the best anchor (the city) is drawn");
            Check(DefenseCoverage.PickAnchors(anchors, targets, tv, DefenseCoverage.Covered(targets, new List<Point> { P(2f), P(31f) }, r), r, 0.6f, rng, 12).Count == 0,
                "everything covered: no draws (the adapter then places anywhere safe)");

            // v0.8 stacking: a second defense over a city still adds 0.8 x 0.2 of its value.
            var city = new List<Point> { P(0f), P(30f) };
            var cv8 = new List<float> { 20f, 1f };
            int[] counts = DefenseCoverage.CoverCounts(city, new List<Point> { P(1f), P(-1f), P(60f) }, new List<float> { 5.9f, 5.9f, 5.9f });
            Check(counts[0] == 2 && counts[1] == 0, "cover counts: the city has 2 defenses over it, the far building none");
            Check(Math.Abs(DefenseCoverage.MarginalValue(P(0f), city, cv8, new[] { 0, 0 }, r, 0.8f) - 16f) < 1e-3f, "first Ion over a city of value 20: 0.8 x 20 = 16");
            Check(Math.Abs(DefenseCoverage.MarginalValue(P(0f), city, cv8, new[] { 1, 0 }, r, 0.8f) - 3.2f) < 1e-3f, "second Ion over it: 0.8 x 0.2 x 20 = 3.2");
            Check(DefenseCoverage.MarginalValue(P(0f), city, cv8, new[] { 1, 0 }, r, 0.8f) > DefenseCoverage.MarginalValue(P(30f), city, cv8, new[] { 1, 0 }, r, 0.8f),
                "a second Ion over a city (3.2) beats the first over an ordinary building (0.8)");
            var nukePicks = DefenseCoverage.PickAnchorsMarginal(new List<Point> { P(0.5f), P(30f) }, city, cv8, new[] { 1, 0 }, r, 0.8f, 0.6f, rng, 10);
            Check(nukePicks.Count == 10 && nukePicks.TrueForAll(i => i == 0), "anti-nuke placement stacks over the city");
            Check(DefenseCoverage.PickAnchorsMarginal(new List<Point> { P(100f) }, city, cv8, new[] { 0, 0 }, r, 0.8f, 0.6f, rng, 5).Count == 0, "nothing in reach: no draws");

            // v0.8.2 per-city protection: centre within radius - cityRadius/2 (Ion 5.3, city 2 -> 4.3).
            var cities2 = new List<Point> { P(0f), P(40f) };
            var cityR = new List<float> { 2f, 2f };
            int prot;
            Check(DefenseCoverage.CityProtectionDeficit(cities2, cityR, new List<Point>(), new List<float>(), 2, out prot) == 4 && prot == 0, "no Ions, 2 cities x 2 = 4 missing");
            Check(DefenseCoverage.CityProtectionDeficit(cities2, cityR, new List<Point> { P(3.5f) }, new List<float> { 5.3f }, 2, out prot) == 3 && prot == 1,
                "Ion 3.5 from city A (the nearest a defense can stand is ~3.1): protects it -> A 1 of 2, B 0 of 2 = 3 missing");
            Check(DefenseCoverage.CityProtectionDeficit(cities2, cityR, new List<Point> { P(4.2f) }, new List<float> { 5.3f }, 2, out prot) == 3 && prot == 1,
                "Ion 4.2 from city A: within 4.3, protected (v0.8.1 said no: the bug)");
            Check(DefenseCoverage.CityProtectionDeficit(cities2, cityR, new List<Point> { P(4.5f) }, new List<float> { 5.3f }, 2, out prot) == 4 && prot == 0,
                "Ion 4.5 from city A: beyond 4.3, not protected");
            Check(DefenseCoverage.CityProtectionDeficit(cities2, cityR, new List<Point> { P(3.5f), P(-3.5f), P(60f) }, new List<float> { 5.3f, 5.3f, 5.3f }, 2, out prot) == 2 && prot == 1,
                "Ions elsewhere do not count: A full (2), B missing 2");
            // v0.8.3: scoring and counting use the same city rule (Terra v0.8.2 #1): an Ion 4.8 from a city (radius 2,
            // reach 4.3) does not count for it, so a new Ion over that city still scores the full first-defense value.
            var cityAndHouse = new List<Point> { P(0f), P(20f) };
            var loss = new List<float> { 1f, 0f };
            int[] cnt = DefenseCoverage.CoverCounts(cityAndHouse, new List<Point> { P(4.8f) }, new List<float> { 5.3f }, loss);
            Check(cnt[0] == 0, "Ion 4.8 from a city with reach 4.3: does not count for it");
            Check(DefenseCoverage.CoverCounts(cityAndHouse, new List<Point> { P(4.8f) }, new List<float> { 5.3f })[0] == 1, "without the loss it would count (the v0.8.2 mismatch)");
            Check(Math.Abs(DefenseCoverage.MarginalValue(P(0.5f), cityAndHouse, new List<float> { 20f, 1f }, cnt, 5.3f, 0.8f, loss) - 16f) < 1e-3f,
                "a new Ion over that city scores the full 0.8 x 20 = 16");
            Check(DefenseCoverage.MarginalValue(P(4.6f), cityAndHouse, new List<float> { 20f, 1f }, cnt, 5.3f, 0.8f, loss) == 0f,
                "a spot 4.6 from the city scores nothing for it (outside 4.3)");

            // v0.8.2 cap: at most 2 x perCity x cities, whatever the deficit says.
            Check(EconomyDirector.AntiNukeMissing(3, 21, 3, 1, 2f) == 0, "Junker T32 (21 Ions, 3 cities, 1 per city): capped at 6 -> 0 more");
            Check(EconomyDirector.AntiNukeMissing(3, 4, 3, 1, 2f) == 2, "4 owned, cap 6: at most 2 more even if 3 are missing");
            Check(EconomyDirector.AntiNukeMissing(2, 0, 3, 2, 2f) == 2, "under the cap: the deficit");
            Check(EconomyDirector.AntiNukeMissing(5, 0, 0, 2, 2f) == 0, "no cities: nothing");
            Check(EconomyDirector.AntiNukePerCity(true, 10, 24, 2, 1) == 2 && EconomyDirector.AntiNukePerCity(false, 24, 24, 2, 1) == 1 && EconomyDirector.AntiNukePerCity(false, 23, 24, 2, 1) == 0,
                "per city: 2 once seen, 1 from the prior turn, 0 before");

            // v0.8 anti-nuke defenses wanted.
            Check(EconomyDirector.AntiNukeDefenses(5, true, 20, 28, 2, 1) == 10, "nuke seen: 2 per city x 5 cities = 10");
            Check(EconomyDirector.AntiNukeDefenses(5, false, 27, 28, 2, 1) == 0, "no nuke seen, before T28: none");
            Check(EconomyDirector.AntiNukeDefenses(5, false, 28, 28, 2, 1) == 5, "no nuke seen, from T28: 1 per city");
            Check(EconomyDirector.AntiNukeDefenses(3, false, 40, 0, 2, 1) == 0, "prior turn 0: never without a sighting");
            Check(EconomyDirector.AntiNukeDefenses(0, true, 40, 28, 2, 1) == 0, "no cities left: none");

            // Impact geometry counters.
            var ledger = new DefenseLedger(2);
            ledger.Record(1, 2, "wea_Railgun", false, 0, 0, 0, 0, false, true, false);
            ledger.Record(1, 2, "wea_Railgun", true, 0, 0, 0, 100, true, false, true);
            ledger.Roll(1);
            DefenderStats d = ledger.LastDefender(1);
            Check(d.NearCity == 1 && d.NoDefenseNearby == 1 && d.NoEligibleDefense == 1, "impact geometry: 1 near a city, 1 with no defense nearby, 1 with none eligible");
            ledger.Record(3, 2, "wea_NuclearMissile", false, 0, 0, 0, 0, false);
            ledger.Record(3, 2, "wea_NuclearMissile", true, 0, 0, 0, 200, true);
            ledger.Roll(3);
            int nukes;
            Check(ledger.LastDefender(3).ImpactsByWeapon.TryGetValue("wea_NuclearMissile", out nukes) && nukes == 2, "impacts by weapon: 2 Nuclear Missiles reached us");
        }

        private static void OffenseAndScienceRules()
        {
            // Railgun (Enhanced): 125/125, aoe 6, fires for 100.
            var railgun = new WeaponProfile { BuildingDamage = 125f, PopulationDamage = 125f, Aoe = 6f, FiringEnergy = 100f, Fragments = 1, Projectiles = 1 };
            var costDown = UpgradeEffect.None(); costDown.BuildingDamageMul = 0.8f; costDown.PopulationDamageMul = 0.8f; costDown.FiringMul = 0.5f;
            var popDmg = UpgradeEffect.None(); popDmg.BuildingDamageMul = 0.8f; popDmg.PopulationDamageMul = 1.8f;
            var projectile = UpgradeEffect.None(); projectile.FiringMul = 1.8f; projectile.ProjectileAdd = 1;
            Check(Math.Abs(WeaponUpgrades.Gain(railgun, costDown, 1.5f) - 0.6f) < 1e-3f, "Railgun EnergyCostDown: 0.8 damage / 0.5 firing = +60% per energy");
            Check(Math.Abs(WeaponUpgrades.Gain(railgun, popDmg, 1.5f) - 0.4f) < 1e-3f, "Railgun PopulationDamage (pop weight 1.5): (100 + 337.5) / 312.5 = +40%");
            Check(Math.Abs(WeaponUpgrades.Gain(railgun, projectile, 1.5f) - (2f / 1.8f - 1f)) < 1e-3f, "Railgun ProjectileAmount1: 2 shots for 1.8x firing = +11%");
            var both = WeaponUpgrades.Apply(railgun, costDown);
            Check(Math.Abs(WeaponUpgrades.Gain(both, popDmg, 1.5f) - 0.4f) < 1e-3f, "gains stack: PopulationDamage still +40% after EnergyCostDown (2.24x together)");
            Check(WeaponUpgrades.Gain(railgun, popDmg, 0f) < 0f, "population weight 0: PopulationDamage is a loss (building damage x0.8)");
            // Coilcannon EnergyCostDown: fragments 3 -> 2, damage x0.75, firing x0.3 -> 0.75 x 2/3 / 0.3 = 1.67x
            var coil = new WeaponProfile { BuildingDamage = 200f, PopulationDamage = 200f, Aoe = 3.6f, FiringEnergy = 200f, Fragments = 3, Projectiles = 1 };
            var coilDown = UpgradeEffect.None(); coilDown.BuildingDamageMul = 0.75f; coilDown.PopulationDamageMul = 0.75f; coilDown.FiringMul = 0.3f; coilDown.FragmentAdd = -1;
            Check(Math.Abs(WeaponUpgrades.Gain(coil, coilDown, 1.5f) - (0.75f * 2f / 3f / 0.3f - 1f)) < 1e-3f, "Coilcannon EnergyCostDown: +67% per energy");
            Check(Math.Abs(WeaponUpgrades.ImmediateDamageRatio(railgun, costDown, 1.5f) - 0.8f) < 1e-3f, "EnergyCostDown loses 20% damage right away (taken only when upkeep-bound)");
            Check(Math.Abs(WeaponUpgrades.ImmediateDamageRatio(railgun, popDmg, 1.5f) - 1.4f) < 1e-3f, "PopulationDamage: +40% right away");
            Check(Math.Abs(WeaponUpgrades.ImmediateDamageRatio(railgun, projectile, 1.5f) - 2f) < 1e-3f, "ProjectileAmount1: 2x damage per shot right away (and 1.8x firing)");
            var single = new WeaponProfile { BuildingDamage = 100f, PopulationDamage = 100f, Aoe = 0f, FiringEnergy = 100f, Fragments = 1, Projectiles = 1 };
            var minus = UpgradeEffect.None(); minus.FragmentAdd = -1;
            Check(WeaponUpgrades.Apply(single, minus).Fragments == 1, "fragments never fall below 1");

            // Science weight: plan behind schedule raises it, capped; done plan leaves the base.
            Check(Math.Abs(ScienceValue.Weight(1.5f, 0f, 400f, 20, 40, 4f) - 1.5f) < 1e-4f, "plan done: base weight");
            Check(Math.Abs(ScienceValue.Weight(1.5f, 16000f, 400f, 20, 40, 4f) - 3f) < 1e-4f, "16,000 left over 20 turns = 800/turn vs 400: weight x2 = 3.0");
            Check(Math.Abs(ScienceValue.Weight(1.5f, 100000f, 400f, 20, 40, 4f) - 6f) < 1e-4f, "far behind: capped at x4 = 6.0");
            Check(Math.Abs(ScienceValue.Weight(1.5f, 2000f, 400f, 20, 40, 4f) - 1.5f) < 1e-4f, "ahead of pace: never below the base");
            Check(Math.Abs(ScienceValue.Weight(1.5f, 4000f, 400f, 39, 40, 4f) - 3f) < 1e-4f, "near the target turn: at least 5 turns assumed (4,000/5 = 800 vs 400)");
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

        // ---- v0.16 (council v0.16) -------------------------------------------------------------------

        private static void V016Rules()
        {
            // Item 1: slot policy.
            Check(SlotPolicy.PermanentAllowed(false, 1, 2, 1, 1), "opening: the 2nd permanent fits under the cap of 2");
            Check(!SlotPolicy.PermanentAllowed(false, 2, 2, 5, 1), "opening: a 3rd permanent waits for Lossless (match 16 queued 7 by T11)");
            Check(SlotPolicy.PermanentAllowed(false, 9, 0, 1, 1), "cap 0 = no opening cap");
            Check(SlotPolicy.PermanentAllowed(true, 9, 2, 2, 1), "after Lossless: a city with 2 free slots keeps 1 for SSTAR");
            Check(!SlotPolicy.PermanentAllowed(true, 0, 2, 1, 1), "after Lossless: a permanent never takes a city's reserved last slot");
            Check(SlotPolicy.JammingAllowed(2, 3, 2000f, 1500f), "Jamming: 3rd copy while research remains");
            Check(!SlotPolicy.JammingAllowed(3, 3, 2000f, 1500f), "Jamming: ceiling of 3 simultaneous copies");
            Check(!SlotPolicy.JammingAllowed(0, 3, 1499f, 1500f), "Jamming: not when under 1,500 plan science remains");

            // Item 4: war start.
            Check(WarStart.Turn(16, 20, true, 12000f, 12000f) == 16, "war at 16 with Orbital Assembly and 12k income");
            Check(WarStart.Turn(16, 20, true, 11999f, 12000f) == 20, "war waits for 20 below 12k income");
            Check(WarStart.Turn(16, 20, false, 50000f, 12000f) == 20, "war waits for 20 without Orbital Assembly");
            Check(WarStart.Turn(22, 20, false, 0f, 12000f) == 22, "a later WarFromTurn is never moved earlier by the fallback");

            // Item 5: intel bid, judged on own visibility results only.
            IntelBidState s = new IntelBidState { CreditedEnergy = 20000f };
            Check(IntelBudget.Allowed(s, 13, true, 14, 0.1f, 200f, 3, 2, 2) == 0, "no bid before IntelBidFromTurn");
            Check(IntelBudget.Allowed(s, 14, false, 14, 0.1f, 200f, 3, 2, 2) == 0, "no bid while a city is visible and every enemy readable");
            Check(IntelBudget.Allowed(s, 14, true, 14, 0.1f, 200f, 3, 2, 2) == 3, "a batch of 3 when blind");
            for (int i = 0; i < 3; i++) IntelBudget.Record(s, 14, 200f, 0, 1);
            Check(IntelBudget.Allowed(s, 14, true, 14, 0.1f, 200f, 3, 2, 2) == 0, "the batch is full");
            Check(IntelBudget.Allowed(s, 15, true, 14, 0.1f, 200f, 3, 2, 2) == 0, "wait for the batch's result");
            IntelBudget.Evaluate(s, 16, 0, 1, 2, 2);
            Check(s.FailedBatches == 1 && !s.Stopped && s.LastVerdict == "failed", "nothing new seen: a failed batch; got " + s.LastVerdict);
            Check(IntelBudget.Allowed(s, 16, true, 14, 0.1f, 200f, 3, 2, 2) == 3, "a second batch after one failure");
            for (int i = 0; i < 3; i++) IntelBudget.Record(s, 16, 200f, 0, 1);
            IntelBudget.Evaluate(s, 18, 0, 1, 2, 2);
            Check(s.Stopped && s.LastVerdict == "stop-loss", "two failed batches: stop-loss");
            Check(IntelBudget.Allowed(s, 18, true, 14, 0.1f, 200f, 3, 2, 2) == 1, "after the stop-loss: one probe");
            IntelBudget.Record(s, 18, 200f, 0, 1);
            Check(s.Probes == 1 && IntelBudget.Allowed(s, 18, true, 14, 0.1f, 200f, 3, 2, 2) == 0, "one probe at a time");
            IntelBudget.Evaluate(s, 20, 1, 1, 2, 2);
            Check(!s.Stopped && s.FailedBatches == 0 && s.LastVerdict == "success", "a probe that shows a city lifts the stop-loss");
            IntelBidState poor = new IntelBidState { CreditedEnergy = 10000f, SpentEnergy = 950f };
            Check(IntelBudget.Allowed(poor, 20, true, 14, 0.1f, 100f, 3, 2, 2) == 0, "the budget is 10% of credited energy: 950 + 100 > 1,000");
            IntelBidState spent = new IntelBidState { CreditedEnergy = 10000f, Stopped = true, Probes = 2 };
            Check(IntelBudget.Allowed(spent, 30, true, 14, 0.1f, 100f, 3, 2, 2) == 0, "at most IntelProbeMax probes after the stop-loss");

            // Item 6: forward defense need (match 16 T27: 83 impacts, 56 with a defense in range).
            int need = ForwardDefense.Need(83, 56f / 83f, 150f, 1.25f, 14725f, 0.8f);
            Check(need == 10500, "forward need = 56 eligible x 150 x 1.25 = 10,500; got " + need);
            Check(ForwardDefense.Need(83, 56f / 83f, 150f, 1.25f, 14725f, 0.5f) == 7362, "bounded by the hard cap share of income");
            Check(ForwardDefense.Need(0, 1f, 150f, 1.25f, 14725f, 0.8f) == 0, "no enemy fire yet: no forward need");

            // Item 4 (S3): match 16 T21, 9 Missiles, 7.65 guided through at 100%, two cities visible.
            Check(PrecisionOpportunity.Holds(2, 7.65f, 1f, 150f, 500, true, 6f, 0.9f, 1f), "T21: 1,147 population through vs a 500 city fires");
            Check(!PrecisionOpportunity.Holds(2, 7.65f, 1f, 150f, 1200, true, 6f, 0.9f, 1f), "no credible kill on a 1,200 city");
            Check(!PrecisionOpportunity.Holds(2, 5.9f, 1f, 150f, 300, true, 6f, 0.9f, 1f), "under lane A's 6 guided through");
            Check(!PrecisionOpportunity.Holds(2, 7.65f, 0.85f, 150f, 300, true, 6f, 0.9f, 1f), "under the 0.9 pass share");
            Check(!PrecisionOpportunity.Holds(0, 7.65f, 1f, 150f, 300, true, 6f, 0.9f, 1f), "no city visible now: no precision fire");
            Check(!PrecisionOpportunity.Holds(2, 7.65f, 1f, 150f, 300, false, 6f, 0.9f, 1f), "unfunded: no precision fire");
            Check(!PrecisionOpportunity.Holds(2, 7.65f, 1f, 150f, 300, true, 6f, 0.9f, 0f), "killShare 0 = off");

            // Item 4 through Strategy.Decide: match 16 T21, 9 Missiles at 0.85 onto a target with no Ion, two cities visible.
            StrategySettings ps = new StrategySettings();
            Dictionary<string, float> missiles = new Dictionary<string, float> { { "wea_Missile", 9f } };
            Dictionary<string, float> guidedRate = new Dictionary<string, float> { { "wea_Missile", 0.85f } };
            HashSet<string> guidedIds = new HashSet<string> { "wea_Missile" };
            Func<List<int>, SalvoTarget> t21 = pops => new SalvoTarget
            {
                PlayerId = 0, Name = "human", Score = 1f, DefensesSeen = true, EnergyKnown = true, EnergyProduction = 20000f,
                Defenses = new Dictionary<string, int> { { "bui_KineticDefense", 20 } }, VisibleCityPops = pops,
            };
            StrategyDecision pd = Strategy.Decide(21, true, missiles, 0.7f, new List<SalvoTarget> { t21(new List<int> { 500, 900 }) }, EnhancedCounters(),
                -1000, 1, 9, ps, -1, -1, false, guidedRate, false, guidedIds, 150f, true);
            Check(pd.Fire && pd.Reason == "precision", "T21: two visible cities, 7.65 guided through: precision fire; got " + pd.Reason + " " + pd.GuidedPassing);
            Check(Math.Abs(pd.GuidedPassing - 7.65f) < 0.01f, "guided through = 9 x 0.85 (Kinetic does not stop Missiles); got " + pd.GuidedPassing);
            pd = Strategy.Decide(21, true, missiles, 0.7f, new List<SalvoTarget> { t21(new List<int>()) }, EnhancedCounters(),
                -1000, 1, 9, ps, -1, -1, false, guidedRate, false, guidedIds, 150f, true);
            Check(!pd.Fire && pd.Reason == "below-saturation", "T23: no city visible: v0.15 hold stands; got " + pd.Reason);
            pd = Strategy.Decide(21, true, missiles, 0.7f, new List<SalvoTarget> { t21(new List<int> { 500 }) }, EnhancedCounters(),
                -1000, 1, 9, ps, -1, -1, false, guidedRate, false, guidedIds, 150f, false);
            Check(!pd.Fire, "unfunded guided firing: hold");
            pd = Strategy.Decide(21, true, missiles, 0.7f, new List<SalvoTarget> { t21(new List<int> { 500 }) }, EnhancedCounters(),
                -1000, 1, 9, ps, -1, -1, false, guidedRate, false, null, 150f, true);
            Check(!pd.Fire, "no guided weapons: no precision exception");
            Dictionary<string, float> mixed = new Dictionary<string, float> { { "wea_Missile", 9f }, { "wea_Railgun", 20f } };
            pd = Strategy.Decide(22, true, mixed, 0.7f, new List<SalvoTarget> { t21(new List<int> { 500 }) }, EnhancedCounters(),
                -1000, 1, 29, ps, -1, -1, false, guidedRate, false, guidedIds, 150f, true);
            Check(Math.Abs(pd.GuidedPassing - 7.65f) < 0.01f, "mixed salvo: only the Missiles count as guided through; got " + pd.GuidedPassing);

            // Sol v0.16 M1: the kill estimate uses the guided weapons that reach THIS target, weighted by their shots.
            SalvoTarget reach = t21(new List<int> { 1500 });
            reach.Shots = new Dictionary<string, float> { { "wea_Missile", 9f } }; // the Nuclear Missile cannot reach it
            Dictionary<string, float> withNuke = new Dictionary<string, float> { { "wea_Missile", 9f }, { "wea_NuclearMissile", 1f } };
            HashSet<string> bothGuided = new HashSet<string> { "wea_Missile", "wea_NuclearMissile" };
            Dictionary<string, float> popById = new Dictionary<string, float> { { "wea_Missile", 150f }, { "wea_NuclearMissile", 5000f } };
            pd = Strategy.Decide(21, true, withNuke, 0.7f, new List<SalvoTarget> { reach }, EnhancedCounters(),
                -1000, 1, 10, ps, -1, -1, false, guidedRate, false, bothGuided, (9f * 150f + 5000f) / 10f, true, popById);
            Check(Math.Abs(pd.GuidedPopDamage - 150f) < 0.01f && !pd.Precision,
                "an unreachable Nuke does not inflate the kill estimate (150 per shot, 1,147 < 1,500); got " + pd.GuidedPopDamage);

            // Sol v0.16 F5: one budget boundary; native minimum spend never opens a batch.
            IntelBidState b5 = new IntelBidState { CreditedEnergy = 5000f };
            Check(IntelBudget.BudgetRoom(b5, 0.1f, 500f) && !IntelBudget.BudgetRoom(b5, 0.1f, 501f), "budget room is 10% of credited energy");
            IntelBudget.RecordSpend(b5, 300f);
            Check(b5.SpentEnergy == 300f && b5.BatchBought == 0 && b5.BatchTurn == -1000, "minimum spend counts, opens no batch");
            Check(!IntelBudget.BudgetRoom(b5, 0.1f, 201f), "minimum spend reduces the room for everything");

            // Sol v0.16 F4: the pending phase is the one that has just finished, before Roll.
            DefenseLedger fresh = new DefenseLedger(2);
            fresh.Roll(7);
            fresh.Record(7, 2, "wea_Railgun", false, 0, 1, 0, 0, true);
            fresh.Record(7, 2, "wea_Railgun", true, 0, 0, 0, 150, true);
            Check(fresh.PendingDefender(7).Impacts == 2 && fresh.LastDefender(7).Impacts == 0, "repairs see the fresh phase; LastDefender is the older one until Roll");
            fresh.Roll(7);
            Check(fresh.LastDefender(7).Impacts == 2 && fresh.PendingDefender(7).Impacts == 0, "after Roll the fresh phase becomes LastDefender");
        }

        // ---- v0.16.1 (match 17) -----------------------------------------------------------------------

        private static void V0161Rules()
        {
            // Jamming keeps its slots: match 17 T13 had 5 free slots and bought 5 Rare Material, 0 Jamming.
            Check(!SlotPolicy.TemporaryAllowed(true, 0, 3, 3), "3 free slots, 3 Jammings wanted: another temporary must wait");
            Check(SlotPolicy.TemporaryAllowed(true, 0, 3, 4), "4 free slots, 3 Jammings wanted: one other temporary fits");
            Check(SlotPolicy.TemporaryAllowed(true, 3, 3, 1), "Jamming at its ceiling: nothing reserved");
            Check(SlotPolicy.TemporaryAllowed(false, 0, 3, 1), "Jamming not wanted (no EMC or no research left): no reservation");

            // Planet guard: T22 income 2,325, sustainable ~10,200 (235k over 23 turns).
            Check(PlanetGuard.ExtractionAllowed(2325f, 7000f, 10200f), "extraction up to the sustainable income is allowed");
            Check(!PlanetGuard.ExtractionAllowed(9000f, 2000f, 10200f), "extraction past the sustainable income is refused");
            Check(PlanetGuard.ExtractionAllowed(50000f, 2000f, -1f), "unlimited planet: no guard");
            Check(PlanetGuard.ExtractionAllowed(50000f, -300f, 10200f), "a project that lowers material is never blocked here");

            // Intel batches sized to the budget (match 17: 3-array batches, 1,000 E spent of ~27,000 E, then stop-loss).
            IntelBidState big = new IntelBidState { CreditedEnergy = 200000f };
            Check(IntelBudget.BatchSize(big, 0.1f, 200f, 8) == 8, "20,000 E of room: capped at 8");
            IntelBidState small = new IntelBidState { CreditedEnergy = 6000f };
            Check(IntelBudget.BatchSize(small, 0.1f, 200f, 8) == 3, "600 E of room: 3 arrays");
            Check(IntelBudget.BatchSize(small, 0.1f, 0f, 8) == 8, "unitCost 0 = fixed batch (v0.16)");
            Check(IntelBudget.Allowed(big, 19, true, 14, 0.1f, 50f, 8, 2, 2, 200f) == 8, "the bid buys a budget-sized batch");
            // Stop-loss needs the spend as well as the failures.
            IntelBidState st = new IntelBidState { CreditedEnergy = 270000f, SpentEnergy = 1000f, FailedBatches = 1, BatchBought = 3, BatchTurn = 19 };
            IntelBudget.Evaluate(st, 21, 0, 1, 2, 2, 0.1f, 0.5f);
            Check(!st.Stopped && st.FailedBatches == 2, "two failures but 1,000 of 27,000 E spent: no stop-loss");
            IntelBidState st2 = new IntelBidState { CreditedEnergy = 270000f, SpentEnergy = 14000f, FailedBatches = 1, BatchBought = 3, BatchTurn = 19 };
            IntelBudget.Evaluate(st2, 21, 0, 1, 2, 2, 0.1f, 0.5f);
            Check(st2.Stopped, "two failures and over half the budget spent: stop-loss");
            // Terra v0.16.1 #3: four failures stop regardless of spend (the 50% spend can be out of reach).
            IntelBidState st3 = new IntelBidState { CreditedEnergy = 270000f, SpentEnergy = 5000f, FailedBatches = 3, BatchBought = 8, BatchTurn = 25 };
            IntelBudget.Evaluate(st3, 27, 0, 1, 2, 2, 0.1f, 0.5f);
            Check(st3.Stopped, "four failed pushes stop even under half the budget");

            // Terra v0.16.1 #2: planet-pool drain guard (match 17 T24: pool 193k, drain ~22k, 12 turns to T36).
            Check(!PlanetGuard.DrainAllowed(22000f, 2000f, 0, 193000, 12), "22k a turn already exceeds a 193k pool over 12 turns");
            Check(PlanetGuard.DrainAllowed(8000f, 1000f, 0, 193000, 12), "8k + 1k a turn fits 193k over 12 turns");
            Check(!PlanetGuard.DrainAllowed(14000f, 6000f, 6, 193000, 12), "14k x 12 + 6k x 6 = 204k > 193k: refused");
            Check(PlanetGuard.DrainAllowed(12000f, 6000f, 6, 193000, 12), "a 6-turn project counts only its 6 turns: 144k + 36k = 180k fits");
            Check(PlanetGuard.DrainAllowed(5000f, 9999f, 0, -1, 12), "unlimited planet: no guard");

            // Terra v0.16.1 #4: reserve only placeable Jamming copies.
            Check(SlotPolicy.LeavesRoom(true, 0, 1), "no city can take another Jamming: nothing reserved");
            Check(!SlotPolicy.LeavesRoom(true, 2, 2), "two placeable copies wanted, 2 free: another project waits");
        }

        // ---- v0.16.2 (matches 18-19, docs/playtests/2026-11-04-v0.16.1-match19.md) ----------------------

        private static void V018Rules()
        {
            // Human first by reach (match 21 T38: Shoe scored 1,450 and 43 through; the human 740 and 27 through).
            StrategySettings hs = new StrategySettings();
            Func<float, float, SalvoTarget> h = (score, passing) => new SalvoTarget { PlayerId = 0, IsHuman = true, Score = score, Passing = passing };
            Check(Strategy.HumanFirstHolds(h(740f, 27f), 1450f, hs, 43f), "T38: 27 of the best 43 through (63%): the human");
            Check(!Strategy.HumanFirstHolds(h(740f, 27f), 1450f, new StrategySettings { HumanFirstReachShare = 0f }, 43f), "reach share 0: score only, Shoe");
            Check(!Strategy.HumanFirstHolds(h(740f, 10f), 1450f, hs, 43f), "10 of 43 (23%) is below 40%: the value rule");
            Check(Strategy.HumanFirstHolds(h(740f, 17.2f), 1450f, hs, 43f), "exactly 40% counts");
            Check(!Strategy.HumanFirstHolds(h(740f, 3f), 1450f, hs, 5f), "3 through is below the 4-shot floor even at 60% of the best");
            Check(!Strategy.HumanFirstHolds(h(740f, 27f), 1450f, hs, -1f), "no best passing given: score only");
            SalvoTarget lowYield = h(740f, 27f);
            lowYield.LowYieldTurns = 9;
            Check(!Strategy.HumanFirstHolds(lowYield, 1450f, new StrategySettings { LowYieldTurns = 2 }, 43f), "a low-yield human is left to the value rule");
            Check(Strategy.HumanFirstHolds(h(1400f, 5f), 1450f, hs, 43f), "score path still works (1,400 >= 0.9 x 1,450)");
            Check(!Strategy.HumanFirstHolds(h(300f, 27f), 1450f, hs, 43f), "Terra #1: a minor human (0.21 of the best score) does not override a dominant rival");
            Check(Strategy.HumanFirstHolds(h(508f, 27f), 1450f, hs, 43f), "0.35 of the best score with the reach: the human");

            // Aim window: T52 of match 21, 4 of 28 aimed at 40 s while trajectories were still being found.
            float t = AimWindow.Extend(39.5f, 40f, 37f, 4, 28, 0.8f, 15f, 90f, 2f);
            Check(Math.Abs(t - 41.5f) < 1e-4f, "4 of 28 aimed and progress 2.5 s ago: push the window 2 s; got " + t);
            Check(AimWindow.Extend(30f, 40f, 29f, 4, 28, 0.8f, 15f, 90f, 2f) == 40f, "not near the end yet: unchanged");
            Check(AimWindow.Extend(39.5f, 40f, 20f, 4, 28, 0.8f, 15f, 90f, 2f) == 40f, "nothing new for 19.5 s (geometry): stop waiting");
            Check(AimWindow.Extend(39.5f, 40f, 39f, 23, 28, 0.8f, 15f, 90f, 2f) == 40f, "23 of 28 >= 80%: enough");
            Check(AimWindow.Extend(39.5f, 40f, 39f, 22, 28, 0.8f, 15f, 90f, 2f) > 40f, "22 of 28 < ceil(22.4): keep going");
            Check(AimWindow.Extend(90f, 90f, 89f, 4, 28, 0.8f, 15f, 90f, 2f) == 90f, "hard cap 90 s");
            Check(AimWindow.Extend(91f, 89f, 90.5f, 4, 28, 0.8f, 15f, 90f, 2f) == 89f, "timer past the cap: no more extension");
            Check(Math.Abs(AimWindow.Extend(89f, 89.5f, 88f, 4, 28, 0.8f, 15f, 90f, 2f) - 90f) < 1e-4f, "never past the cap");
            Check(AimWindow.Extend(39.5f, 40f, 39f, 0, 0, 0.8f, 15f, 90f, 2f) == 40f, "no weapons: unchanged");
            Check(AimWindow.Extend(39.5f, 40f, 39f, 4, 28, 0.8f, 15f, 40f, 2f) == 40f, "cap not above the window (off): unchanged");
        }

        private static void V017Rules()
        {
            // Shelter: count the defenses over a spot (strictly inside the radius, as PlayerDefenseHandler.getDefenses).
            List<Point> defs = new List<Point> { new Point(0, 0, 0), new Point(10, 0, 0) };
            List<float> radii = new List<float> { 6f, 6f };
            Check(Shelter.CoverCount(new Point(5, 0, 0), defs, radii) == 2, "a spot between two defenses is under both");
            Check(Shelter.CoverCount(new Point(-5, 0, 0), defs, radii) == 1, "a spot under one defense counts 1");
            Check(Shelter.CoverCount(new Point(-6, 0, 0), defs, radii) == 0, "exactly on the radius is outside (strict)");
            Check(Shelter.CoverCount(new Point(30, 0, 0), defs, radii) == 0, "a far spot is uncovered");
            Check(Math.Abs(Shelter.NearestDistance(new Point(3, 4, 0), defs) - 5f) < 1e-4f, "nearest distance 5");
            Check(Shelter.NearestDistance(new Point(3, 4, 0), new List<Point>()) < 0f, "no points: -1");
            Check(Math.Abs(Shelter.NearestDistance(new Point(9, 0, 0), defs) - 1f) < 1e-4f, "the nearest point is not the first in the list");

            // Keep-out around cities (Terra v0.17 H3): the edge distance subtracts the city radius.
            List<float> cityRadii = new List<float> { 2f, 4f };
            Check(Math.Abs(Shelter.NearestEdge(new Point(16, 0, 0), defs, cityRadii) - 2f) < 1e-4f, "edge distance 6 - 4 = 2 (second city)");
            Check(Shelter.NearestEdge(new Point(1, 0, 0), defs, cityRadii) == 0f, "inside a city's radius: 0");
            Check(Shelter.NearestEdge(new Point(1, 0, 0), new List<Point>(), new List<float>()) < 0f, "no cities: -1");
            Check(Shelter.InKeepOut(2f, 3f), "2 from the city edge is inside a keep-out of 3");
            Check(!Shelter.InKeepOut(3f, 3f), "exactly at the keep-out is outside");
            Check(!Shelter.InKeepOut(2f, 0f), "keep-out 0: off");
            Check(!Shelter.InKeepOut(-1f, 3f), "no city: never in keep-out");

            // Score: any cover beats the closest uncovered spot; cover is capped; closeness breaks ties.
            Check(Shelter.Score(1, 2, 40f, 10f) > Shelter.Score(0, 2, 0f, 10f), "a covered far spot beats an uncovered spot next to a city");
            Check(Math.Abs(Shelter.Score(5, 2, 40f, 10f) - Shelter.Score(2, 2, 40f, 10f)) < 1e-4f, "cover is capped at 2");
            Check(Shelter.Score(2, 2, 40f, 10f) > Shelter.Score(1, 2, 0f, 10f), "a second defense still beats closeness");
            Check(Shelter.Score(1, 2, 2f, 10f) > Shelter.Score(1, 2, 8f, 10f), "same cover: closer to a city wins");
            Check(Shelter.Score(0, 2, 2f, 10f) > Shelter.Score(0, 2, 8f, 10f), "no cover yet: closer to a city wins");
            Check(Shelter.Score(0, 2, -1f, 10f) == 0f, "no city: closeness 0");

            // Accept: covered always; uncovered when nothing exists to shelter under, or after the covered steps.
            Check(Shelter.Accept(1, 0, 6, 5), "a covered spot is accepted on the first step");
            Check(!Shelter.Accept(0, 2, 6, 5), "an uncovered spot waits while defenses exist (step 2 of 6)");
            Check(Shelter.Accept(0, 6, 6, 5), "after 6 steps without cover the best uncovered spot is taken");
            Check(Shelter.Accept(0, 0, 6, 0), "no defenses at all: closeness decides at once");

            // Rare Material: build-up only, ends by the war, up to max at once.
            Check(SlotPolicy.RareMaterialAllowed(0, 2, 14, 6, 20, 0, false), "T14 + 6 = T20: ends as the war starts");
            Check(!SlotPolicy.RareMaterialAllowed(0, 2, 15, 6, 20, 0, false), "T15 + 6 runs into the war");
            Check(SlotPolicy.RareMaterialAllowed(1, 2, 12, 6, 20, 0, false), "a second copy (Pavol stacked two)");
            Check(!SlotPolicy.RareMaterialAllowed(2, 2, 12, 6, 20, 0, false), "not a third");
            Check(!SlotPolicy.RareMaterialAllowed(0, 2, 10, 6, 20, 0, true), "never once the war has started (damage trigger)");
            Check(!SlotPolicy.RareMaterialAllowed(0, 2, 12, 6, 20, 3, false), "endLead 3: T12 + 6 = T18 > T17");
            Check(SlotPolicy.RareMaterialAllowed(0, 2, 11, 6, 20, 3, false), "endLead 3: T11 + 6 = T17 is fine");
            Check(!SlotPolicy.RareMaterialAllowed(0, 0, 5, 6, 20, 0, false), "max 0: off");
        }

        private static void V0162Rules()
        {
            StrategySettings s = new StrategySettings();
            var counters = EnhancedCounters();

            // Human first. Match 19 T29: the human leads (1.45) with 10 through; the dying Turing (0.44) lets 18 through.
            // Value rule: human 10 x 1.5 = 15, Turing 18 x 1.15 = 20.7 (above the 1.25 x 15 stickiness bar).
            Func<SalvoTarget> human = () => new SalvoTarget { PlayerId = 0, Name = "jogi", IsHuman = true, Score = 1.45f, DefensesSeen = true, Shots = Railguns(10) };
            Func<SalvoTarget> turing = () => new SalvoTarget { PlayerId = 2, Name = "Turing", Score = 0.44f, DefensesSeen = true, Shots = Railguns(18) };
            StrategyDecision d = Strategy.Decide(29, true, Railguns(30), 1f, new List<SalvoTarget> { human(), turing() }, counters, 26, 0, 30, s, 0);
            Check(d.CommitTargetId == 0 && d.HumanFirst, "the leading human is the commit, not the dying AI; got " + d.CommitTargetId);
            d = Strategy.Decide(29, true, Railguns(30), 1f, new List<SalvoTarget> { human(), turing() }, counters, 26, 0, 30, new StrategySettings { HumanFirstShare = 0f, HumanFirstReachShare = 0f }, 0);
            Check(d.CommitTargetId == 2 && !d.HumanFirst, "HumanFirstShare 0: the v0.16.1 value rule picks Turing (the check can fail); got " + d.CommitTargetId);
            SalvoTarget far = human();
            far.Shots = Railguns(3);
            d = Strategy.Decide(29, true, Railguns(30), 1f, new List<SalvoTarget> { far, turing() }, counters, 26, 0, 30, s, 0);
            Check(d.CommitTargetId == 2 && !d.HumanFirst, "a human almost out of reach (3 through) is left to the value rule; got " + d.CommitTargetId);
            SalvoTarget trailing = human();
            trailing.Score = 0.5f;
            SalvoTarget rival = new SalvoTarget { PlayerId = 2, Score = 1f, DefensesSeen = true, Shots = Railguns(19) };
            d = Strategy.Decide(26, true, Railguns(40), 1f, new List<SalvoTarget> { trailing, rival }, counters, -1000, 0, 40, new StrategySettings { HumanFirstReachShare = 0f }, -1);
            Check(d.CommitTargetId == 2 && !d.HumanFirst, "score only (v0.16.2): a human who does not lead is chosen by value; got " + d.CommitTargetId);
            d = Strategy.Decide(26, true, Railguns(40), 1f, new List<SalvoTarget> { trailing, rival }, counters, -1000, 0, 40, s, -1);
            Check(d.CommitTargetId == 0 && d.HumanFirst, "v0.18 reach: the trailing human with comparable reach is the commit; got " + d.CommitTargetId);
            SalvoTarget close = human();
            close.Score = 0.95f;
            d = Strategy.Decide(26, true, Railguns(40), 1f, new List<SalvoTarget> { close, new SalvoTarget { PlayerId = 2, Score = 1f, DefensesSeen = true, Shots = Railguns(25) } },
                counters, -1000, 0, 40, s, -1);
            Check(d.CommitTargetId == 0 && d.HumanFirst, "a human at 95% of the best score counts as leading; got " + d.CommitTargetId);
            SalvoTarget stuck = human();
            stuck.LowYieldTurns = s.LowYieldTurns;
            d = Strategy.Decide(29, true, Railguns(30), 1f, new List<SalvoTarget> { stuck, turing() }, counters, 26, 0, 30, s, 0);
            Check(!d.HumanFirst && d.CommitTargetId == 2, "a human our weapons found no trajectory onto (low yield) is left to the value rule");

            // Return fire answers the committed target's own shots (match 19 T30-T35: the human hit Asona, Asona shot Brain).
            Func<SalvoTarget> brain = () => new SalvoTarget { PlayerId = 1, Name = "Brain", Score = 1f, DefensesSeen = true, Shots = Railguns(8) };
            d = Strategy.Decide(31, true, Railguns(30), 1f, new List<SalvoTarget> { brain() }, counters, 30, 0, 30, s, 1, 30, true, null, true,
                lastPhaseImpactsBy: new Dictionary<int, int> { { 0, 12 } });
            Check(!d.Fire && d.Reason == "below-saturation" && !d.HitByCommit, "only the human hit us; the commit is Brain: no return fire; got " + d.Reason);
            d = Strategy.Decide(31, true, Railguns(30), 1f, new List<SalvoTarget> { brain() }, counters, 30, 0, 30, s, 1, 30, true, null, true,
                lastPhaseImpactsBy: new Dictionary<int, int> { { 0, 12 }, { 1, 3 } });
            Check(d.Fire && d.Reason == "return-fire" && d.HitByCommit, "Brain hit us: return fire at Brain; got " + d.Reason);
            d = Strategy.Decide(31, true, Railguns(30), 1f, new List<SalvoTarget> { brain() }, counters, 30, 0, 30, s, 1, 30, true, null, true);
            Check(d.Fire && d.Reason == "return-fire", "no per-shooter map: any enemy impact counts (v0.15.3)");

            // Free fire. 20 Railguns at 19 Kinetic (6k energy): ~8 through, below saturation.
            StrategySettings so = new StrategySettings { OverlapDefenses = 20f };
            Func<List<SalvoTarget>> kin19 = () => new List<SalvoTarget> { Target(0, 1f, 19, true, 6000f) };
            d = Strategy.Decide(23, true, Railguns(20), 1f, kin19(), counters, -1000, 2, 20, so, 0, freeEnergy: 33613f, firingCost: 3000f);
            Check(d.Fire && d.Reason == "free-fire", "below saturation with 33.6k unspent last turn and a 3k salvo: fire; got " + d.Reason);
            d = Strategy.Decide(23, true, Railguns(20), 1f, kin19(), counters, -1000, 2, 20, so, 0, freeEnergy: 754f, firingCost: 2200f);
            Check(!d.Fire && d.Reason == "below-saturation", "754 unspent vs a 2,200 salvo: holding still funds construction; got " + d.Reason);
            d = Strategy.Decide(23, true, Railguns(20), 1f, kin19(), counters, -1000, 2, 20, so, 0, freeEnergy: -1f, firingCost: 3000f);
            Check(!d.Fire, "last turn unknown: hold");
            d = Strategy.Decide(23, true, Railguns(20), 1f, kin19(), counters, -1000, 2, 20, new StrategySettings { OverlapDefenses = 20f, FreeFire = false }, 0,
                freeEnergy: 33613f, firingCost: 3000f);
            Check(!d.Fire && d.Reason == "below-saturation", "FreeFire off: the v0.16.1 hold (the check can fail); got " + d.Reason);
            d = Strategy.Decide(15, false, Railguns(20), 1f, kin19(), counters, -1000, 0, 20, so, 0, freeEnergy: 33613f, firingCost: 3000f);
            Check(!d.Fire && d.Reason == "peace", "free fire needs war; got " + d.Reason);
            d = Strategy.Decide(23, true, Railguns(5), 1f, kin19(), counters, -1000, 2, 5, so, 0, freeEnergy: 33613f, firingCost: 3000f);
            Check(!d.Fire && d.Reason == "building", "free fire needs MinWeapons; got " + d.Reason);
            d = Strategy.Decide(24, true, Railguns(35), 1f, kin19(), counters, 23, 0, 35, so, 0, freeEnergy: 33613f, firingCost: 3000f);
            Check(d.Fire && d.Reason == "free-fire", "a pulse wait with energy to spare fires; got " + d.Reason);
            Check(Strategy.ShouldAbort(2, 30, "free-fire", s), "free fire is called off when almost nothing aimed at the commit");
            // Terra v0.16.2: not into defenses that stop almost everything (FreeFireMinPassing 4): 6 Railguns at 19 Kinetic.
            d = Strategy.Decide(23, true, Railguns(6), 1f, kin19(), counters, -1000, 2, 6, so, 0, freeEnergy: 33613f, firingCost: 600f);
            Check(d.Passing < so.FreeFireMinPassing && !d.Fire, "setup + rule: under 4 expected through, no free fire; got " + d.Passing + " " + d.Reason);

            // Free energy (Terra v0.16.2 #3): last turn's actual surplus, capped by this turn's energy beyond last turn's use.
            // Match 19 Brain T23 -> T24: 33,613 left of a 35,513 construction start (1,900 used); T24 net 38,967 after a 3,000 reservation.
            Check(Math.Abs(Strategy.FreeEnergy(33613f, 1900f, 38967f, 3000f) - 33613f) < 0.5f, "a glut turn: the whole surplus is free");
            Check(Math.Abs(Strategy.FreeEnergy(1442f, 4150f, 4754f, 1200f) - 1442f) < 0.5f, "T20 -> T21 shape: 1,442 free, enough for a 1,200 salvo");
            Check(Strategy.FreeEnergy(3000f, 5000f, 3000f, 1200f) < 1200f, "income fell: this turn cannot fund last turn's use and a salvo");
            Check(Strategy.FreeEnergy(-1f, 0f, 9000f, 1000f) < 0f, "last turn unknown: -1");

            // v0.19 late free fire (match 24, Doc T23): held below saturation for energy (free 1,034 vs 3,500), 9 primary
            // trajectories onto the human, 17,120 energy and 16 material left when construction ended (no material cost here).
            StrategySettings sl = new StrategySettings();
            Func<StrategyDecision> held = () => new StrategyDecision { Fire = false, War = true, CommitTargetId = 4, Reason = "below-saturation", Shots = 25, Passing = 9.0f };
            Check(Strategy.LateFreeFire(held(), 17120, 16, 3500, 0, sl), "Doc T23: 17,120 left covers the 3,500 reservation: fire");
            Check(!Strategy.LateFreeFire(held(), 3499, 2891, 3500, 0, sl), "left below the reservation: hold");
            Check(Strategy.LateFreeFire(held(), 3500, 2891, 3500, 0, sl), "exactly the reservation left: fire");
            Check(!Strategy.LateFreeFire(held(), 17120, 99, 3500, 100, sl), "synthetic: material for guided weapons not left: hold");
            Check(!Strategy.LateFreeFire(held(), 17120, 2891, 0, 0, sl), "nothing was released: nothing to take back");
            StrategyDecision pw = held(); pw.Reason = "pulse-wait";
            Check(!Strategy.LateFreeFire(pw, 17120, 2891, 3500, 0, sl), "a pulse wait is a choice, not an energy hold");
            StrategyDecision few = held(); few.Passing = sl.FreeFireMinPassing - 0.5f;
            Check(!Strategy.LateFreeFire(few, 17120, 2891, 3500, 0, sl), "under FreeFireMinPassing expected through: hold");
            StrategyDecision small = held(); small.Shots = sl.MinWeapons - 1;
            Check(!Strategy.LateFreeFire(small, 17120, 2891, 3500, 0, sl), "under MinWeapons: hold");
            StrategyDecision peace = held(); peace.War = false;
            Check(!Strategy.LateFreeFire(peace, 17120, 2891, 3500, 0, sl), "peace: hold");
            StrategyDecision none = held(); none.CommitTargetId = -1;
            Check(!Strategy.LateFreeFire(none, 17120, 2891, 3500, 0, sl), "no commit: hold");
            StrategyDecision fired = held(); fired.Fire = true;
            Check(!Strategy.LateFreeFire(fired, 17120, 2891, 3500, 0, sl), "already firing: no second take");
            Check(!Strategy.LateFreeFire(null, 17120, 2891, 3500, 0, sl), "no decision: hold");
            Check(!Strategy.LateFreeFire(held(), 17120, 2891, 3500, 0, new StrategySettings { FreeFire = false }), "free fire off: hold");

            // v0.20 (council S1 + I1a): late fire on pulse-wait holds, with a refusal reason for every non-fire. Fixture m25
            // Donnels T33: pulse-wait, 31 shots, ~21 expected through, 8,828 energy left against a 5,960 reservation.
            Func<StrategyDecision> pulse = () => new StrategyDecision { Fire = false, War = true, CommitTargetId = 4, Reason = "pulse-wait", Shots = 31, Passing = 21.2f };
            Check(Strategy.LateFreeFireRefusal(pulse(), 8828, 16, 5960, 0, sl, true) == "", "Donnels T33 pulse-wait, pulse path on: fire");
            Check(Strategy.LateFreeFireRefusal(pulse(), 8828, 16, 5960, 0, sl, false) == "reason:pulse-wait", "pulse path off: refused with the hold reason");
            Check(!Strategy.LateFreeFire(pulse(), 8828, 16, 5960, 0, sl), "the v0.19 entry point still refuses pulse waits");
            Check(Strategy.LateFreeFireRefusal(held(), 17120, 16, 3500, 0, sl, true) == "" && Strategy.LateFreeFireRefusal(held(), 17120, 16, 3500, 0, sl, false) == "",
                "below-saturation fires on both paths");
            Check(Strategy.LateFreeFireRefusal(pulse(), 5959, 16, 5960, 0, sl, true) == "energy", "refusal: energy");
            Check(Strategy.LateFreeFireRefusal(pulse(), 8828, 99, 5960, 100, sl, true) == "material", "refusal: material");
            Check(Strategy.LateFreeFireRefusal(pulse(), 8828, 16, 0, 0, sl, true) == "nothing-released", "refusal: nothing released");
            StrategyDecision pwFew = pulse(); pwFew.Shots = sl.MinWeapons - 1;
            Check(Strategy.LateFreeFireRefusal(pwFew, 8828, 16, 5960, 0, sl, true) == "few-weapons", "refusal: few weapons");
            StrategyDecision pwPass = pulse(); pwPass.Passing = 0.5f;
            Check(Strategy.LateFreeFireRefusal(pwPass, 8828, 16, 5960, 0, sl, true) == "few-passing", "refusal: few passing");
            StrategyDecision pwPeace = pulse(); pwPeace.War = false;
            Check(Strategy.LateFreeFireRefusal(pwPeace, 8828, 16, 5960, 0, sl, true) == "peace", "refusal: peace");
            StrategyDecision pwNone = pulse(); pwNone.CommitTargetId = -1;
            Check(Strategy.LateFreeFireRefusal(pwNone, 8828, 16, 5960, 0, sl, true) == "no-commit", "refusal: no commit");
            StrategyDecision pwFire = pulse(); pwFire.Fire = true;
            Check(Strategy.LateFreeFireRefusal(pwFire, 8828, 16, 5960, 0, sl, true) == "firing", "refusal: already firing");
            StrategyDecision pwBuild = pulse(); pwBuild.Reason = "building";
            Check(Strategy.LateFreeFireRefusal(pwBuild, 8828, 16, 5960, 0, sl, true) == "reason:building", "refusal: other hold reasons stay held");
            Check(Strategy.LateFreeFireRefusal(null, 8828, 16, 5960, 0, sl, true) == "no-decision", "refusal: no decision");
            Check(Strategy.LateFreeFireRefusal(pulse(), 8828, 16, 5960, 0, new StrategySettings { FreeFire = false }, true) == "free-fire-off", "refusal: free fire off");

            // v0.20 (council I2): retaliation latch + hysteresis. Real sequences (economy.recentDamage, trigger 600, scheduled
            // war T20): Ash m26 T17-T19 865/606/529 went war, war, PEACE; Eddie m24 T14-T19 608/426/418/680/476/333 went
            // war, peace, peace, war, peace, peace. The v0.19 rule reproduces the flips (baseline), the v0.20 rule does not.
            Func<int[], float[], int, int, bool[]> run = (turns, dmg, latchFrom, quiet) =>
            {
                var st = new EconomyDirector.RetaliationState();
                var r = new bool[turns.Length];
                for (int i = 0; i < turns.Length; i++) r[i] = EconomyDirector.WarPhase(turns[i], 20, st, dmg[i], 600f, latchFrom, quiet);
                return r;
            };
            int[] ashT = { 17, 18, 19 }; float[] ashD = { 865.274f, 605.691f, 528.984f };
            Check(!EconomyDirector.WarPhase(19, 20, 528.984f, 600f), "baseline: the v0.19 rule puts Ash on peace at T19");
            bool[] ash = run(ashT, ashD, 12, 2);
            Check(ash[0] && ash[1] && ash[2], "Ash m26: war at T17 latches (>= T12), still war at T19");
            int[] edT = { 14, 15, 16, 17, 18, 19 }; float[] edD = { 608.3f, 425.8f, 418f, 679.5f, 475.7f, 333f };
            bool[] ed = run(edT, edD, 12, 2);
            bool allWar = true; foreach (bool w in ed) allWar &= w;
            Check(allWar, "Eddie m24: no peace after the T14 trigger");
            int flipsOld = 0; bool prev = false;
            for (int i = 0; i < edT.Length; i++) { bool w = EconomyDirector.WarPhase(edT[i], 20, edD[i], 600f); if (prev && !w) flipsOld++; prev = w; }
            Check(flipsOld == 2, "baseline: the v0.19 rule flips Eddie twice; got " + flipsOld);
            // Before the latch turn: hysteresis. T8 hit, T9 and T10 quiet -> war, war (quiet 1), peace (quiet 2); T11 hit -> war.
            bool[] early = run(new[] { 8, 9, 10, 11 }, new[] { 700f, 500f, 500f, 700f }, 12, 2);
            Check(early[0] && early[1] && !early[2] && early[3], "pre-latch hysteresis: war, war, peace, war");
            // Fable v0.20 #4: a T11 hit carried into T12 by hysteresis latches (war, war, war), not war, war, peace.
            bool[] edge = run(new[] { 11, 12, 13 }, new[] { 865f, 590f, 529f }, 12, 2);
            Check(edge[0] && edge[1] && edge[2], "latch boundary: T11 hit, decaying T12-T13 stays war");
            // Fable v0.20 #1: after the scheduled war turn the retaliation state is not touched (no latch logged there).
            var post = new EconomyDirector.RetaliationState();
            Check(EconomyDirector.WarPhase(21, 20, post, 900f, 600f, 12, 2) && !post.Latched && post.LastTurn == -1,
                "a hit after the scheduled war turn does not set the latch");
            bool[] early1 = run(new[] { 8, 9 }, new[] { 700f, 500f }, 12, 1);
            Check(early1[0] && !early1[1], "quietTurns 1 = the v0.19 behaviour before the latch turn");
            bool[] noHit = run(new[] { 13, 14 }, new[] { 100f, 0f }, 12, 2);
            Check(!noHit[0] && !noHit[1], "no trigger: no war before the scheduled turn");
            Check(run(new[] { 20 }, new[] { 0f }, 12, 2)[0], "scheduled war turn: war without damage");
            var same = new EconomyDirector.RetaliationState();
            bool first = EconomyDirector.RetaliationWar(same, 17, 865f, 600f, 12, 2);
            bool second = EconomyDirector.RetaliationWar(same, 17, 0f, 600f, 12, 2);
            Check(first && second, "same turn: the second caller gets the first answer");
            Check(same.Latched && same.LatchTurn == 17, "the latch turn is recorded");
            var samePre = new EconomyDirector.RetaliationState();
            bool pre1 = EconomyDirector.RetaliationWar(samePre, 8, 700f, 600f, 12, 2);
            bool pre2 = EconomyDirector.RetaliationWar(samePre, 8, 0f, 600f, 12, 2);
            Check(pre1 && pre2 && !samePre.Latched, "same turn before the latch: the second caller (strategy) gets the director's answer");
            var gap = new EconomyDirector.RetaliationState();
            EconomyDirector.RetaliationWar(gap, 8, 700f, 600f, 12, 2);
            Check(!EconomyDirector.RetaliationWar(gap, 10, 0f, 600f, 12, 2), "a skipped turn (earlier save) does not carry a pre-latch retaliation");
            Check(EconomyDirector.RetaliationWar(null, 8, 700f, 600f, 12, 2) && !EconomyDirector.RetaliationWar(null, 8, 500f, 600f, 12, 2),
                "no state: the plain trigger");

            // Who hit us, by shooter; our own shots are not an enemy's.
            DefenseLedger ledger = new DefenseLedger(3);
            ledger.Record(5, 0, "wea_Railgun", false, 0, 0, 0, 0, true);
            ledger.Record(5, 0, "wea_Railgun", true, 0, 0, 0, 0, true);
            ledger.Record(5, 2, "wea_Railgun", false, 0, 0, 0, 0, true);
            ledger.Record(5, 5, "wea_Railgun", false, 0, 0, 0, 0, true);
            ledger.Roll(5);
            DefenderStats last = ledger.LastDefender(5);
            int fromHuman, fromTuring, fromSelf;
            Check(last.EnemyImpactsBy.TryGetValue(0, out fromHuman) && fromHuman == 2 && last.EnemyImpactsBy.TryGetValue(2, out fromTuring) && fromTuring == 1
                && !last.EnemyImpactsBy.TryGetValue(5, out fromSelf) && last.EnemyImpacts == 3, "impacts by shooter: human 2, Turing 1, our own shot not counted");

            // Material binds. Match 19 Brain T22 -> T23: 1,661 of 6,461 energy unspent, 121 of 8,896 material left.
            Check(EconomyDirector.IsMaterialBound(1661, 6461, 121, 8896, 0.2f, 0.1f, 600), "26% of energy unspent with 1% of material left: material binds");
            Check(!EconomyDirector.IsMaterialBound(292, 3942, 14, 4439, 0.2f, 0.1f, 600), "T14 shape: 7% unspent, energy binds");
            Check(!EconomyDirector.IsMaterialBound(1661, 6461, 4000, 8896, 0.2f, 0.1f, 600), "material left over too: something else binds");
            Check(!EconomyDirector.IsMaterialBound(500, 2000, 10, 2000, 0.2f, 0.1f, 600), "500 unspent is under the 600 minimum");
            Check(!EconomyDirector.IsMaterialBound(1661, 6461, 121, 8896, 0f, 0.1f, 600), "share 0 = off");
            Check(!EconomyDirector.IsMaterialBound(1661, 0, 121, 8896, 0.2f, 0.1f, 600), "no budget recorded last turn: not bound");

            // Extraction first: up to the share, only below the sustainable material income.
            Check(EconomyDirector.ExtractionFirstRoom(0.35f, 8896, 0, 6400f, 11700f) == 3113, "35% of 8,896 while income 6.4k < sustainable 11.7k");
            Check(EconomyDirector.ExtractionFirstRoom(0.35f, 8896, 3000, 6400f, 11700f) == 113, "3,000 of it spent: 113 left");
            Check(EconomyDirector.ExtractionFirstRoom(0.35f, 8896, 0, 12000f, 11700f) == 0, "income at the sustainable rate: no extraction first");
            Check(EconomyDirector.ExtractionFirstRoom(0.35f, 8896, 0, 12000f, -1f) == 3113, "unlimited planet: the share only");
            Check(EconomyDirector.ExtractionFirstRoom(0f, 8896, 0, 6400f, 11700f) == 0, "share 0 = off");
            // Terra v0.16.2 M2: the ceiling runs to the drain guard's T36, not MaterialHorizonTurn T45. T20, 282k planet + 9k stock.
            float to45 = EconomyDirector.SustainableMaterialIncome(282000, 9000, true, 20, 45, 3);
            float to36 = EconomyDirector.SustainableMaterialIncome(282000, 9000, true, 20, 36, 3);
            Check(EconomyDirector.ExtractionFirstRoom(0.35f, 8896, 0, 12500f, to45) == 0 && EconomyDirector.ExtractionFirstRoom(0.35f, 8896, 0, 12500f, to36) > 0,
                "12.5k income: past the T45 rate (" + to45 + ") but under the T36 rate (" + to36 + ")");

            // While material binds, a mine beats more energy (match 19 T23: 11 Floating Plant yield upgrades, 0 mines).
            EconOption energyUp = new EconOption { Id = "upg_FloatingEnergyYield2", Kind = OptionKind.Upgrade, Energy = 50, Material = 200, EnergyGain = 400f };
            EconOption mine = new EconOption { Id = "bui_OffshoreMine", Kind = OptionKind.Producer, Energy = 300, Material = 350, MaterialGain = 125f, NeedsSlot = true };
            List<EconOption> opts = new List<EconOption> { energyUp, mine };
            EconomyBalance bal = new EconomyBalance { EnergyIncome = 6461, MaterialIncome = 6400, WeaponUpkeep = 3000, TargetRatio = 1f, EnergyBudget = 6461, MaterialBudget = 8896 };
            Check(EconomyDirector.Best(opts, bal, 140) == energyUp, "setup: energy under 1.0 x material + upkeep picks the energy upgrade");
            bal.EnergyGlut = EconomyDirector.UseGlutWeights(false, true, 140, 4);
            Check(EconomyDirector.Best(opts, bal, 140) == mine, "material binds: the mine, not more energy");
            Check(!EconomyDirector.UseGlutWeights(false, true, 2, 4), "material binds but power slots short: normal weights");
            Check(!EconomyDirector.UseGlutWeights(false, false, 140, 4), "neither glut nor bound: normal weights");
            Check(EconomyDirector.UseGlutWeights(true, false, 140, 4), "energy glut with slots: glut weights (v0.11)");
            // Terra v0.16.2 #2, combined: energy glut AND no free slot. The mine cannot be placed; a plant for slots must stay buyable.
            EconOption plant = new EconOption { Id = "bui_FloatingPlant", Kind = OptionKind.Plant, Energy = 250, Material = 300, EnergyGain = 100f };
            List<EconOption> slotShort = new List<EconOption> { plant, mine };
            EconomyBalance glut = new EconomyBalance { EnergyIncome = 40000, MaterialIncome = 6000, TargetRatio = 1f, EnergyBudget = 39000, MaterialBudget = 6000,
                EnergyGlut = true };
            Check(EconomyDirector.Best(slotShort, glut, 0) == null, "setup: glut weights with 0 slots buy nothing (the v0.16.1 stall)");
            glut.EnergyGlut = EconomyDirector.UseGlutWeights(true, true, 0, 4);
            Check(EconomyDirector.Best(slotShort, glut, 0) == plant, "0 slots in glut and material-bound: the plant for its slots");
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
