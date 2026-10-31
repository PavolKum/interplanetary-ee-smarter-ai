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
            Check(EconomyDirector.Best(options, bal, 10) == material, "both gluts: energy gains are worthless, material wins");

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

        // ---- v0.12: material budget, energy saturation, fire what can aim, reachability ----------------
        private static void V012Rules()
        {
            // Sustainable material: planet left / turns to the horizon (at least minTurns); -1 when unlimited.
            Check(Math.Abs(EconomyDirector.SustainableMaterialIncome(124334, true, 24, 45, 3) - 124334f / 21f) < 1f, "Beam T24: 124k over 21 turns");
            Check(Math.Abs(EconomyDirector.SustainableMaterialIncome(30000, true, 44, 45, 3) - 10000f) < 1f, "near the horizon: at least minTurns");
            Check(EconomyDirector.SustainableMaterialIncome(0, true, 30, 45, 3) == 0f, "dry planet: 0");
            Check(EconomyDirector.SustainableMaterialIncome(50000, false, 30, 45, 3) < 0f, "unlimited planet: -1");

            // Energy saturation, v0.11.1 Beam numbers (Floating Plant wave T22-27).
            EconomyBalance t18 = new EconomyBalance { EnergyIncome = 6792, MaterialIncome = 10884, TargetRatio = 0.6f,
                SustainableMaterialIncome = EconomyDirector.SustainableMaterialIncome(276774, true, 18, 45, 3) };
            Check(!EconomyDirector.EnergySaturated(t18), "T18 (6.8k energy, planet barely binding): not saturated");
            EconomyBalance t22 = new EconomyBalance { EnergyIncome = 20118, MaterialIncome = 16193, TargetRatio = 0.6f, WeaponUpkeep = 6000,
                SustainableMaterialIncome = EconomyDirector.SustainableMaterialIncome(190865, true, 22, 45, 3) };
            Check(EconomyDirector.EnergySaturated(t22), "T22 (20k energy, 8.3k sustainable material): saturated");
            t22.WeaponUpkeep = 12000;
            Check(!EconomyDirector.EnergySaturated(t22), "upkeep counts toward wanted energy");
            EconomyBalance roomy = new EconomyBalance { EnergyIncome = 50000, MaterialIncome = 5000, TargetRatio = 0.6f,
                SustainableMaterialIncome = 8000f };
            Check(!EconomyDirector.EnergySaturated(roomy), "planet not binding (sustainable above production): never saturated");
            EconomyBalance unlimited = new EconomyBalance { EnergyIncome = 50000, MaterialIncome = 5000, TargetRatio = 0.6f };
            Check(!EconomyDirector.EnergySaturated(unlimited), "unlimited planet: never saturated");
            EconomyBalance dry = new EconomyBalance { EnergyIncome = 30000, MaterialIncome = 25000, TargetRatio = 0.6f, SustainableMaterialIncome = 0f };
            Check(EconomyDirector.EnergySaturated(dry), "dry planet with income: saturated");
            dry.EnergySaturation = 0f;
            Check(!EconomyDirector.EnergySaturated(dry), "EnergySaturation 0 = off");

            // Saturated: a Floating Plant loses to a material option even in material glut.
            EconOption plant = new EconOption { Id = "fp", Kind = OptionKind.Plant, Energy = 250, Material = 300, EnergyGain = 100f };
            EconOption mineUp = new EconOption { Id = "up", Kind = OptionKind.Upgrade, Energy = 50, Material = 250, MaterialGain = 40f };
            EconomyBalance sat = new EconomyBalance { EnergyIncome = 20118, MaterialIncome = 16193, TargetRatio = 0.6f, WeaponUpkeep = 6000,
                SustainableMaterialIncome = 8298f, EnergyBudget = 20000, MaterialBudget = 23000, MaterialGlut = true };
            Check(EconomyDirector.Best(new List<EconOption> { plant, mineUp }, sat, 10) == mineUp, "saturated + material glut: no Floating Plant");
            Check(EconomyDirector.Best(new List<EconOption> { plant }, sat, 10) == null, "saturated: a plant alone is not worth buying");
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
