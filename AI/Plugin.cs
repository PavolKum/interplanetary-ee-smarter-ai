using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Construction;
using Interplanetary.GameEngine;
using Interplanetary.GameSession;
using Interplanetary.PlayerDataManagement;
using InterplanetaryAI.Brain;

namespace InterplanetaryAI
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "pavol.interplanetary.ai";
        public const string NAME = "Interplanetary EE: Smarter AI";
        public const string VERSION = "0.10.1";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> SmartTargeting;
        internal static ConfigEntry<float> FocusShare;
        internal static ConfigEntry<float> LeaderWeight;
        internal static ConfigEntry<float> GrudgeWeight;
        internal static ConfigEntry<float> FinishWeight;
        internal static ConfigEntry<float> HumanBias;
        internal static ConfigEntry<float> GrudgeDecayPerTurn;
        internal static ConfigEntry<int> ReachabilityMemoryTurns;
        internal static ConfigEntry<bool> RefreshFiringList;
        internal static ConfigEntry<bool> AccuracyOverride;
        internal static ConfigEntry<string> AccuracyByLevel;
        internal static ConfigEntry<bool> FireMisses;
        internal static ConfigEntry<bool> SmartStrikes;
        internal static ConfigEntry<float> WeaponBuildingShare;
        internal static ConfigEntry<bool> DecisionLog;
        internal static ConfigEntry<bool> SuperweaponSavings;
        internal static ConfigEntry<float> SaveShare;
        internal static ConfigEntry<string> Superweapons;
        internal static ConfigEntry<bool> BuildPlannerEnabled;
        internal static ConfigEntry<string> GrowthWeights;
        internal static ConfigEntry<string> PressureWeights;
        internal static ConfigEntry<int> GrowthUntilTurn;
        internal static ConfigEntry<float> DefenseBonusWhenHit;
        internal static ConfigEntry<bool> ResearchPlanEnabled;
        internal static ConfigEntry<string> ResearchPlan;
        internal static ConfigEntry<bool> EnableLasers;
        internal static ConfigEntry<bool> SmartDefense;
        internal static ConfigEntry<bool> DirectorEnabled;
        internal static ConfigEntry<int> WarFromTurn;
        internal static ConfigEntry<int> DefenseFromTurn;
        internal static bool HoldHookPatched;
        internal static ConfigEntry<float> WarDamageTrigger;
        internal static ConfigEntry<float> WarUpkeepShare;
        internal static ConfigEntry<float> RetaliationUpkeepShare;
        internal static ConfigEntry<float> WarSpendShare;
        internal static ConfigEntry<float> EnergyToMaterialTarget;
        internal static ConfigEntry<float> DefensePerEnemyWeapon;
        internal static ConfigEntry<float> DamagePerDefense;
        internal static ConfigEntry<int> MaxDefenses;
        internal static ConfigEntry<int> DefensesPerTurn;
        internal static ConfigEntry<float> DefenseFloorShare;
        internal static ConfigEntry<int> DefenseCatchUpTurns;
        internal static ConfigEntry<float> WeaponMemoryDecay;
        internal static ConfigEntry<float> DefensesForFullCover;
        internal static ConfigEntry<int> PlacementStepCap;
        internal static ConfigEntry<float> SuperweaponUpkeepShare;
        internal static ConfigEntry<int> CounterintelPerBuildings;
        internal static ConfigEntry<int> CounterintelFromTurn;
        internal static ConfigEntry<float> UnknownEconomyShare;
        internal static ConfigEntry<int> IntelPerTurn;
        internal static ConfigEntry<int> MaxIntelBuildings;
        internal static ConfigEntry<float> DefenseReserveShare;
        internal static ConfigEntry<int> EndgameBankMaterial;
        internal static ConfigEntry<int> BankWarningTurns;
        internal static ConfigEntry<bool> PlanetEfficiency;
        internal static ConfigEntry<int> UnpoweredPlantsPerTurn;
        internal static ConfigEntry<float> ScienceWeight;
        internal static ConfigEntry<int> ScienceTargetTurn;
        internal static ConfigEntry<float> ScienceMaxMultiplier;
        internal static ConfigEntry<bool> WeaponUpgradesEnabled;
        internal static ConfigEntry<int> WeaponUpgradesPerTurn;
        internal static ConfigEntry<float> WeaponUpgradeShare;
        internal static ConfigEntry<float> WeaponUpgradeMinGain;
        internal static ConfigEntry<float> PopulationDamageWeight;
        internal static ConfigEntry<float> MaterialGlutRatio;
        internal static ConfigEntry<int> MaterialGlutMin;
        internal static ConfigEntry<bool> StrategyEnabled;
        internal static ConfigEntry<int> StrikeEvery;
        internal static ConfigEntry<float> MinPassingShots;
        internal static ConfigEntry<float> MinPassShare;
        internal static ConfigEntry<float> BrokenPassShare;
        internal static ConfigEntry<int> MaxHoldTurns;
        internal static ConfigEntry<int> MinWeapons;
        internal static ConfigEntry<float> CoverHalf;
        internal static ConfigEntry<float> RollEnergyShare;
        internal static readonly StrategySettings StrategyCfg = new StrategySettings();

        internal static readonly BuildWeights Build = new BuildWeights();
        internal static List<string> SuperweaponIds = new List<string>();
        internal static List<string> ResearchPlanIds = new List<string>();

        // v0.4: economy opening, then the one chain that gives telescopes (Advanced Signal Processing),
        // missiles (Orbital Assembly) and the Nuclear Missile (EmDrives -> Thermonuclear Warheads), with the
        // science project (Quantum Computing), Kinetic and Ion defense on the way.
        // v0.9: science (Quantum Computing, Ultracapacitors) and weapon-upgrade techs (Uranium Alloys, Zero Reluctance
        // Engineering, Magnetic Saturation Elimination) moved up; Railgun population damage was 31st of 38.
        // v0.10: 4th Gen Nuclear Reactors and Lossless Energy Transfer (Reactor Trials: x1.3 global energy per city
        // project) moved from #30-31 to #6-7; the human had them by T15, the AIs never reached them in v0.9.1.
        // Kinetic Defense (2nd Gen Composites -> Maglev Servos) right after them at #8-9 (v0.9: #12), Plasma Channel
        // Induction (Ion) at #15 ahead of the weapon-upgrade techs.
        private const string DefaultResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SmartGrids,tec_SuperTensileMaterials,tec_QuantumComputing,"
            + "tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,tec_2ndGenComposites,tec_MaglevServos,tec_SelfAdjustingOptics,"
            + "tec_AdvancedSignalProcessing,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,tec_OrbitalAssembly,tec_PlasmaChannelInduction,"
            + "tec_UraniumAlloys,tec_Ultracapacitors,tec_MagneticRadiationShielding,tec_EmDrives,tec_ThermonuclearWarheads,"
            + "tec_ExperimentalMatterControl,tec_InductionCoils,tec_ZeroReluctanceEngineering,tec_MagneticSaturationElimination,tec_RoboticMining,"
            + "tec_EarlyWarningAI,tec_Metamaterials,tec_DesignerNanomaterials,tec_VerticalFarming,tec_QuantumCommunications,"
            + "tec_AtmosphericInterferenceMitigation,tec_CompositeHeatCells,tec_Octaazacubane,tec_AdvancedGenetics,tec_SelfHealingMaterials,"
            + "tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,tec_MassDrivers";

        // v0.9 default, replaced on load if the config still holds it unchanged.
        private const string V09ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SmartGrids,tec_SuperTensileMaterials,tec_SelfAdjustingOptics,"
            + "tec_AdvancedSignalProcessing,tec_QuantumComputing,tec_UraniumAlloys,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,"
            + "tec_2ndGenComposites,tec_MaglevServos,tec_OrbitalAssembly,tec_Ultracapacitors,tec_PlasmaChannelInduction,"
            + "tec_MagneticRadiationShielding,tec_EmDrives,tec_ThermonuclearWarheads,tec_ExperimentalMatterControl,tec_InductionCoils,"
            + "tec_ZeroReluctanceEngineering,tec_MagneticSaturationElimination,tec_RoboticMining,tec_EarlyWarningAI,tec_Metamaterials,"
            + "tec_DesignerNanomaterials,tec_VerticalFarming,tec_QuantumCommunications,tec_AtmosphericInterferenceMitigation,"
            + "tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,tec_CompositeHeatCells,tec_Octaazacubane,tec_AdvancedGenetics,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,tec_MassDrivers";

        // v0.4 default, replaced on load if the config still holds it unchanged.
        private const string V04ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SmartGrids,tec_SuperTensileMaterials,tec_SelfAdjustingOptics,"
            + "tec_AdvancedSignalProcessing,tec_QuantumComputing,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,tec_2ndGenComposites,"
            + "tec_MaglevServos,tec_OrbitalAssembly,tec_PlasmaChannelInduction,tec_MagneticRadiationShielding,tec_EmDrives,"
            + "tec_ThermonuclearWarheads,tec_ExperimentalMatterControl,tec_RoboticMining,tec_InductionCoils,tec_EarlyWarningAI,"
            + "tec_Metamaterials,tec_DesignerNanomaterials,tec_VerticalFarming,tec_QuantumCommunications,tec_AtmosphericInterferenceMitigation,"
            + "tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,tec_Ultracapacitors,tec_CompositeHeatCells,tec_Octaazacubane,"
            + "tec_UraniumAlloys,tec_AdvancedGenetics,tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,"
            + "tec_GammaRayLasers,tec_ZeroReluctanceEngineering,tec_MassDrivers";

        // v0.3 default, replaced on load if the config still holds it unchanged.
        private const string V03ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SuperTensileMaterials,tec_QuantumComputing,tec_SmartGrids,"
            + "tec_ExperimentalMatterControl,tec_2ndGenComposites,tec_MaglevServos,tec_EarlyWarningAI,tec_Nanostructuring,"
            + "tec_MicrowaveEnergyTranmission,tec_RoboticMining,tec_SelfAdjustingOptics,tec_AdvancedSignalProcessing,tec_MagneticRadiationShielding,"
            + "tec_Metamaterials,tec_DesignerNanomaterials,tec_ThermonuclearWarheads,tec_VerticalFarming,tec_QuantumCommunications,"
            + "tec_PlasmaChannelInduction,tec_AtmosphericInterferenceMitigation,tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,tec_Ultracapacitors,"
            + "tec_CompositeHeatCells,tec_OrbitalAssembly,tec_EmDrives,tec_Octaazacubane,tec_UraniumAlloys,tec_AdvancedGenetics,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,"
            + "tec_InductionCoils,tec_ZeroReluctanceEngineering,tec_MassDrivers";

        // v0.2 default, replaced on load if the config still holds it unchanged.
        private const string V02ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SuperTensileMaterials,tec_QuantumComputing,tec_SmartGrids,"
            + "tec_2ndGenComposites,tec_MaglevServos,tec_MagneticRadiationShielding,tec_EarlyWarningAI,tec_Metamaterials,"
            + "tec_SelfAdjustingOptics,tec_AdvancedSignalProcessing,tec_OrbitalAssembly,tec_EmDrives,tec_ThermonuclearWarheads,"
            + "tec_RoboticMining,tec_ExperimentalMatterControl,tec_Nanostructuring,tec_QuantumCommunications,tec_MicrowaveEnergyTranmission,"
            + "tec_AtmosphericInterferenceMitigation,tec_4thGenNuclearReactors,tec_PlasmaChannelInduction,tec_VerticalFarming,tec_LosslessEnergyTransfer,"
            + "tec_Ultracapacitors,tec_Octaazacubane,tec_CompositeHeatCells,tec_UraniumAlloys,tec_AdvancedGenetics,tec_DesignerNanomaterials,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,"
            + "tec_InductionCoils,tec_ZeroReluctanceEngineering,tec_MassDrivers";

        internal static readonly PlannerSettings Planner = new PlannerSettings();
        internal static float[] Accuracy = new float[] { 0.6f, 0.95f, 1f, 1f };
        internal static GrudgeLedger Grudges;
        internal static DefenseLedger Ledger = new DefenseLedger(3);
        internal static ConfigEntry<float> DefenseObservedMargin;
        internal static ConfigEntry<float> DefenseHardCapShare;
        internal static ConfigEntry<int> ObservedMinImpacts;
        internal static ConfigEntry<float> DefenseRecoveryShare;
        internal static ConfigEntry<bool> CoveragePlacement;
        internal static ConfigEntry<int> AntiNukePerCity;
        internal static ConfigEntry<int> AntiNukePriorPerCity;
        internal static ConfigEntry<int> AntiNukePriorTurn;
        internal static ConfigEntry<float> AntiNukeCityValue;
        internal static ConfigEntry<int> NukeMemoryTurns;
        internal static ConfigEntry<float> CoverageCityValue;
        internal static ConfigEntry<float> CoverageBand;
        internal static ConfigEntry<float> ObservedPriorWeight;
        internal static ReachabilityMemory Reach;
        internal static readonly Dictionary<int, float> AccuracyCarry = new Dictionary<int, float>();
        internal static readonly System.Random Rng = new System.Random();
        internal static int LastTurnSeen;

        private static StreamWriter s_decisionWriter;
        private Harmony m_harmony;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch. The mod only acts in offline games; online games always run the native AI.");

            SmartTargeting = Config.Bind("Targeting", "SmartTargeting", true,
                "Replace the native per-weapon random target pick with threat-based focus fire.");
            FocusShare = Config.Bind("Targeting", "FocusShare", 0.7f,
                "Share of an AI's weapons sent at its top-scored enemy each turn (0-1). The rest are drawn weighted by score.");
            LeaderWeight = Config.Bind("Targeting", "LeaderWeight", 1.0f,
                "Weight of an enemy's visible economy (squared share of the strongest), so a runaway leader draws fire.");
            GrudgeWeight = Config.Bind("Targeting", "GrudgeWeight", 0.35f,
                "Weight of recent damage an enemy dealt to this AI (retaliation).");
            FinishWeight = Config.Bind("Targeting", "FinishWeight", 0.5f,
                "Weight of how little visible city population an enemy has left (a city is ruined at 0; finish off the weak).");
            HumanBias = Config.Bind("Targeting", "HumanBias", 1.0f,
                "Multiplier on human enemies' score. 1 = no favouritism; above 1 = AIs prefer the human.");
            GrudgeDecayPerTurn = Config.Bind("Targeting", "GrudgeDecayPerTurn", 0.7f,
                "How much remembered damage survives each turn (0-1).");
            ReachabilityMemoryTurns = Config.Bind("Targeting", "ReachabilityMemoryTurns", 1,
                "A weapon that found no direct trajectory onto a target is steered elsewhere for this many turns. A failed search is weak evidence (time-limited, random), so keep this short.");
            RefreshFiringList = Config.Bind("Targeting", "RefreshFiringList", true,
                "Fix a native bug: rebuild the list of weapons to fire from this turn's trajectory results right before firing. Without it, weapons that only found a hit on another enemy planet stay silent.");

            AccuracyOverride = Config.Bind("Accuracy", "Override", true,
                "Native AIs plan deliberate misses each turn (accuracy 0.4 / 0.75 / 0.9 / 1.0 by difficulty). Override that plan.");
            AccuracyByLevel = Config.Bind("Accuracy", "AccuracyByLevel", "0.4,0.7,0.7,0.7",
                "Planned hit share per difficulty level, lowest first, comma-separated. Default = human parity: the human's planet-contact rate over 6 recorded matches (Oct 2026) was 58% overall and 67% in the last two; 0.7 planned allows for shots lost to defenses. Easy keeps the native 0.4.");
            FireMisses = Config.Bind("Accuracy", "FireMisses", true,
                "Fire the planned misses like a human would, but only verified near-misses: never a trajectory onto the AI's own planet (the game counts that as a 'miss'). Off = planned misses hold fire and save their firing cost.");

            SmartStrikes = Config.Bind("Strikes", "SmartStrikes", true,
                "Guided second-phase strikes go for the visible city closest to destruction instead of a random visible building.");
            WeaponBuildingShare = Config.Bind("Strikes", "WeaponBuildingShare", 0.25f,
                "Chance a guided strike goes for the weakest visible weapon building instead of a city.");

            SuperweaponSavings = Config.Bind("Economy", "SuperweaponSavings", true,
                "Once a superweapon is unlocked, save toward it and buy it (the native AI never saves, so it never builds one).");
            SaveShare = Config.Bind("Economy", "SaveShare", 0.5f,
                "Share of each turn's unreserved stock added to the superweapon savings (0-1). The rest is spent normally.");
            Superweapons = Config.Bind("Economy", "Superweapons", "bui_NuclearMissile,bui_AsteroidDiversion",
                "Superweapon buildings to save for, in priority order. Add bui_SolarLaser only together with [Lasers] EnableLasers.");
            BuildPlannerEnabled = Config.Bind("Economy", "BuildPlanner", true,
                "Replace the native fixed construction roulette (firepower 3 / energy 1.75 / material 1.2 / defense 0.7) with phase weights below. Needs and minimum intel stay native.");
            GrowthWeights = Config.Bind("Economy", "GrowthWeights", "1.5,3.5,3,0.7,0.3",
                "Firepower,Energy,Material,Defense,Counterintel weights in the opening (before GrowthUntilTurn).");
            PressureWeights = Config.Bind("Economy", "PressureWeights", "2.5,2.5,2,1,0.8",
                "Firepower,Energy,Material,Defense,Counterintel weights after the opening: keeps growing while arming. Native was 3 / 1.75 / 1.2 / 0.7 / 1.25 (+ intel).");
            GrowthUntilTurn = Config.Bind("Economy", "GrowthUntilTurn", 15, "Opening length in turns.");
            DefenseBonusWhenHit = Config.Bind("Economy", "DefenseBonusWhenHit", 1.5f, "Extra defense weight while the AI has recently taken damage.");

            SmartDefense = Config.Bind("Economy", "SmartDefense", true,
                "Build the defense that counters the most visible enemy firepower not yet covered (Kinetic vs Railgun/Coilcannon, Ion vs Missile/MIRV/Nuclear, Shield vs Laser). Falls back to native when nothing visible threatens.");

            ResearchPlanEnabled = Config.Bind("Research", "UsePlan", true,
                "Research the Plan list in order (prerequisites are queued automatically) instead of the native random focus. Native research resumes when the plan is done.");
            ResearchPlan = Config.Bind("Research", "Plan", DefaultResearchPlan,
                "Tech IDs in order. Default (v0.3): the human's order from the 31 Oct 2026 win, economy first (material and energy projects, yield upgrades, Offshore Mine, Floating Plant), then the Nuclear Missile line, then the rest.");
            if (ResearchPlan.Value == V02ResearchPlan || ResearchPlan.Value == V03ResearchPlan || ResearchPlan.Value == V04ResearchPlan
                || ResearchPlan.Value == V09ResearchPlan)
            {
                ResearchPlan.Value = DefaultResearchPlan;
                Log.LogInfo("Research plan: replaced an unchanged older default with the v0.10 default.");
            }
            if (Math.Abs(GrudgeWeight.Value - 0.6f) < 1e-4f)
            {
                GrudgeWeight.Value = 0.35f; // v0.3 default; grudge alone drove blind AIs into vendettas
                Log.LogInfo("GrudgeWeight: replaced the unchanged v0.3 default 0.6 with 0.35.");
            }

            UnknownEconomyShare = Config.Bind("Targeting", "UnknownEconomyShare", 0.8f,
                "Economy share (of the strongest known) assumed for an enemy whose production our intel cannot read. Unknown is not weak.");
            IntelPerTurn = Config.Bind("Director", "IntelPerTurn", 2, "Most intel buildings (Telescope Array) queued per turn while some enemy's numbers are unreadable.");
            MaxIntelBuildings = Config.Bind("Director", "MaxIntelBuildings", 30, "Cap on intel buildings.");
            DefenseReserveShare = Config.Bind("Director", "DefenseReserveShare", 0.3f,
                "At war, energy kept unspent for defense rolls, at most this share of energy income (each roll costs the defender 100-200 energy; 0 left = the defense fails).");
            EndgameBankMaterial = Config.Bind("Director", "EndgameBankMaterial", 6000,
                "Material kept banked before a superweapon is available, once the planet is about to run dry. 0 = off.");
            BankWarningTurns = Config.Bind("Director", "BankWarningTurns", 8, "Start the endgame bank this many turns before the planet runs dry (at the current drain).");
            PlanetEfficiency = Config.Bind("Director", "PlanetEfficiency", true,
                "On a limited planet, mines take Yield1 + Multi-Stage Refinery (drain x0.7) instead of two yield upgrades (which also multiply the drain).");
            UnpoweredPlantsPerTurn = Config.Bind("Director", "UnpoweredPlantsPerTurn", 5, "Most power plants built per turn to connect unpowered buildings.");
            ScienceWeight = Config.Bind("Director", "ScienceWeight", 1.5f, "Science per turn valued against energy/material per turn when choosing projects and upgrades (base; raised while research is behind).");
            ScienceTargetTurn = Config.Bind("Director", "ScienceTargetTurn", 40, "Turn by which the research plan should be done; science is valued more while the pace falls short.");
            ScienceMaxMultiplier = Config.Bind("Director", "ScienceMaxMultiplier", 2.5f, "Most the science weight is raised when research is behind schedule.");
            WeaponUpgradesEnabled = Config.Bind("Director", "WeaponUpgrades", true, "At war, buy weapon upgrades that raise damage per firing energy (population damage weighted).");
            WeaponUpgradesPerTurn = Config.Bind("Director", "WeaponUpgradesPerTurn", 6, "Most weapon upgrades queued in one turn.");
            WeaponUpgradeShare = Config.Bind("Director", "WeaponUpgradeShare", 0.3f, "Most of a turn's starting material spent on weapon upgrades.");
            WeaponUpgradeMinGain = Config.Bind("Director", "WeaponUpgradeMinGain", 0.15f, "Smallest gain in damage per firing energy worth an upgrade (0.15 = +15%).");
            PopulationDamageWeight = Config.Bind("Director", "PopulationDamageWeight", 1.5f, "Population damage valued against building damage when judging weapons (cities at 0 population decide the game).");
            MaterialGlutRatio = Config.Bind("Director", "MaterialGlutRatio", 3f,
                "v0.10: material counts as in glut once the banked stock is this many times the turn's energy budget; energy gains then count fully, material gains and material cost at 0.25. 0 = off.");
            MaterialGlutMin = Config.Bind("Director", "MaterialGlutMin", 5000, "Smallest material stock that counts as glut.");

            StrategyEnabled = Config.Bind("Strategy", "Enabled", true,
                "v0.10: hold fire (and give the firing energy the game reserves to construction) until a salvo at one committed target is expected to get enough shots through its defenses; then strike, at most every StrikeEvery turns. Off = fire every turn, split targets (v0.9).");
            StrikeEvery = Config.Bind("Strategy", "StrikeEvery", 2, "Turns between strikes (2 = every other turn, as the human who won).");
            MinPassingShots = Config.Bind("Strategy", "MinPassingShots", 12f, "Strike when at least this many shots are expected through the target's defenses...");
            MinPassShare = Config.Bind("Strategy", "MinPassShare", 0.45f, "...and at least this share of the salvo.");
            BrokenPassShare = Config.Bind("Strategy", "BrokenPassShare", 0.7f, "Expected pass share at which the target counts as broken: strike again without waiting for the pulse.");
            MaxHoldTurns = Config.Bind("Strategy", "MaxHoldTurns", 5, "At war with weapons, never hold fire longer than this many turns in a row.");
            MinWeapons = Config.Bind("Strategy", "MinWeapons", 6, "Fewer weapons than this: keep building, do not fire.");
            CoverHalf = Config.Bind("Strategy", "CoverHalf", 12f, "Defenses at which half of our shots are assumed to meet one in range (corrected by our observed shots).");
            RollEnergyShare = Config.Bind("Strategy", "RollEnergyShare", 0.5f, "Share of a target's energy income assumed left for its defense rolls (150 energy each).");

            DirectorEnabled = Config.Bind("Director", "Enabled", true,
                "v0.3 economy director: replaces the native construction choice. Buys yield upgrades and city projects first, keeps energy income near EnergyToMaterialTarget x material income, places water buildings properly, holds weapons until WarFromTurn, and counters visible enemy weapons with defenses. Off = the v0.2 build planner.");
            WarFromTurn = Config.Bind("Director", "WarFromTurn", 20,
                "Turn from which the AI builds weapons (v0.9.1: the human built its first at T22). Before it, only retaliation after heavy damage. Firing is decided by [Strategy].");
            DefenseFromTurn = Config.Bind("Director", "DefenseFromTurn", 15,
                "v0.10: turn from which the defense floor (DefenseFloorShare) and the defense energy reserve apply, before weapons (WarFromTurn).");
            WarDamageTrigger = Config.Bind("Director", "WarDamageTrigger", 600f,
                "Recent damage taken that starts retaliation before WarFromTurn.");
            WarUpkeepShare = Config.Bind("Director", "WarUpkeepShare", 0.4f,
                "Cap on weapon firing cost per turn as a share of energy income, from WarFromTurn.");
            RetaliationUpkeepShare = Config.Bind("Director", "RetaliationUpkeepShare", 0.15f,
                "Same cap while retaliating before WarFromTurn.");
            WarSpendShare = Config.Bind("Director", "WarSpendShare", 0.4f,
                "Share of a turn's energy spent on new weapons before the economy gets the rest. Energy left over after the economy also goes to weapons (it does not carry over).");
            EnergyToMaterialTarget = Config.Bind("Director", "EnergyToMaterialTarget", 0.6f,
                "Wanted energy income as a share of material income (plus weapon upkeep). The human who won kept ~0.5-0.65 while booming.");
            DefensePerEnemyWeapon = Config.Bind("Director", "DefensePerEnemyWeapon", 0.4f,
                "Defenses wanted per visible enemy weapon building.");
            DamagePerDefense = Config.Bind("Director", "DamagePerDefense", 500f,
                "One more defense wanted per this much recent damage taken.");
            MaxDefenses = Config.Bind("Director", "MaxDefenses", 60, "Cap on defenses wanted.");
            ConfigEntry<string> migrated = Config.Bind("General", "ConfigMigratedTo", "",
                "Last version whose default changes were applied to this file. Do not edit.");
            // Explicit states, not an ordinal compare ("0.10" sorts before "0.5"; Terra v0.10 #5): "" -> "0.5" -> "0.10".
            if (migrated.Value == "")
            {
                if (MaxDefenses.Value == 40)
                {
                    MaxDefenses.Value = 60; // v0.4 default; the 31 Oct human held 32 defenses against one AI
                    Log.LogInfo("MaxDefenses: replaced the unchanged v0.4 default 40 with 60 (once).");
                }
                migrated.Value = "0.5";
            }
            if (migrated.Value == "0.5")
            {
                if (WarFromTurn.Value == 15)
                {
                    WarFromTurn.Value = 20; // v0.10: the human built its first weapons at T22; grow first
                    Log.LogInfo("WarFromTurn: replaced the unchanged default 15 with 20 (once).");
                }
                if (Math.Abs(ScienceMaxMultiplier.Value - 4f) < 1e-4f)
                {
                    ScienceMaxMultiplier.Value = 2.5f; // v0.9.1: a weight ~3 from T15 cut the AIs' T15 energy to 3.0k
                    Log.LogInfo("ScienceMaxMultiplier: replaced the unchanged default 4 with 2.5 (once).");
                }
                migrated.Value = "0.10";
            }
            DefensesPerTurn = Config.Bind("Director", "DefensesPerTurn", 3,
                "Defenses queued per turn when on target; more when behind (see DefenseCatchUpTurns).");
            DefenseFloorShare = Config.Bind("Director", "DefenseFloorShare", 0.15f,
                "At war, defenses wanted are at least this share of our own non-city buildings, seen threat or not. 0 = off (v0.4 behaviour).");
            DefenseCatchUpTurns = Config.Bind("Director", "DefenseCatchUpTurns", 3,
                "When behind on defenses, queue enough per turn to close the gap in this many turns.");
            DefenseObservedMargin = Config.Bind("Director", "DefenseObservedMargin", 1.25f,
                "The defense reserve is at least what the defenses used last action phase (energy spent plus rolls that found no energy) times this margin.");
            DefenseHardCapShare = Config.Bind("Director", "DefenseHardCapShare", 0.8f,
                "Most of the energy income the observed defense need may reserve. 0 = ignore observed spending (v0.5 behaviour).");
            ObservedMinImpacts = Config.Bind("Director", "ObservedMinImpacts", 12,
                "Weapon choice trusts the interception rate seen over the last 3 turns once at least this many shots of that weapon reached the target. 0 = never (v0.5 behaviour).");
            DefenseRecoveryShare = Config.Bind("Director", "DefenseRecoveryShare", 0.2f,
                "The defense reserve leaves construction at least this share of energy income, so a collapsing AI can still rebuild power. 0 = no floor (v0.6.0 behaviour).");
            ObservedPriorWeight = Config.Bind("Director", "ObservedPriorWeight", 6f,
                "Observed interception is blended with the estimate from remembered defenses as if this many shots had matched the estimate: few shots barely move it.");
            CoveragePlacement = Config.Bind("Director", "CoveragePlacement", true,
                "Place each new defense next to the building whose neighbourhood holds the most value no defense of that type covers yet (cities weigh CoverageCityValue). Off = the game's own placement: random city, no memory.");
            AntiNukePerCity = Config.Bind("Director", "AntiNukePerCity", 2,
                "Once an enemy Nuclear Missile is seen or lands on us: this many defenses that stop it (Ion in Enhanced) per city, placed over the cities. 0 = off.");
            AntiNukePriorPerCity = Config.Bind("Director", "AntiNukePriorPerCity", 1, "Before any nuke is seen, from AntiNukePriorTurn: this many per city.");
            AntiNukePriorTurn = Config.Bind("Director", "AntiNukePriorTurn", 24, "Turn from which nukes are expected without having seen one (0 = never).");
            AntiNukeCityValue = Config.Bind("Director", "AntiNukeCityValue", 20f, "Weight of a city when placing an anti-nuke defense (an ordinary building is 1).");
            NukeMemoryTurns = Config.Bind("Director", "NukeMemoryTurns", 12, "A Nuclear Missile that hit us keeps the threat on for this many turns.");
            CoverageCityValue = Config.Bind("Director", "CoverageCityValue", 4f, "Weight of a city when judging what a defense covers (an ordinary building is 1).");
            CoverageBand = Config.Bind("Director", "CoverageBand", 0.6f, "Anchors scoring at least this share of the best are all candidates, picked weighted by score squared, so defenses spread.");
            WeaponMemoryDecay = Config.Bind("Director", "WeaponMemoryDecay", 0.95f,
                "Enemy weapons once seen are remembered while hidden by counterintel, shrinking by this factor per turn. 0 = forget at once (v0.4 behaviour).");
            DefensesForFullCover = Config.Bind("Director", "DefensesForFullCover", 10f,
                "Defenses of one type taken to cover a whole planet, for picking weapons the target's defenses stop least.");
            SuperweaponUpkeepShare = Config.Bind("Director", "SuperweaponUpkeepShare", 0.7f,
                "Buy a superweapon only if all weapon firing costs, its own (700-800 a turn) included, stay under this share of energy income. Otherwise build power first.");
            CounterintelPerBuildings = Config.Bind("Director", "CounterintelPerBuildings", 12,
                "One counterintel building (Data Security Hub) wanted per this many non-city buildings; hides them from enemy intel. 0 = none.");
            CounterintelFromTurn = Config.Bind("Director", "CounterintelFromTurn", 8, "First turn counterintel is built.");
            PlacementStepCap = Config.Bind("Director", "PlacementStepCap", 1500,
                "Placement attempts before a building search gives up and its cost is returned (native searches never give up).");

            EnableLasers = Config.Bind("Lasers", "EnableLasers", false,
                "EXPERIMENTAL. Let the AI build and fire Laser / EMP Laser / Solar Laser. The game's own AI laser code was switched off by its developers and has bugs (crash on no target, wrong building aimed); this installs fixes for both plus a bypass at end of turn. Needs a game restart. Try it in a throwaway match first.");

            DecisionLog = Config.Bind("Logging", "DecisionLog", true,
                "Write one JSON line per AI decision to BepInEx/InterplanetaryAI/decisions-<start time>.jsonl.");

            // Memory settings are read once; changing them needs a restart (a live change would wipe the match memory).
            Grudges = new GrudgeLedger(Clamp01(GrudgeDecayPerTurn.Value));
            Reach = new ReachabilityMemory(Math.Max(0, ReachabilityMemoryTurns.Value));
            ApplyConfig();
            Config.SettingChanged += (sender, args) => ApplyConfig();

            m_harmony = new Harmony(GUID);
            int patched = 0;
            patched += TryPatch(typeof(AICore), "Initialize", null, "CoreInitializePostfix");
            patched += TryPatch(typeof(AIWeaponTargeting), "BeginTurn", null, "TargetingBeginTurnPostfix");
            patched += TryPatch(typeof(AIWeaponTargetingGoal), "BeginTurn", null, "GoalBeginTurnPostfix");
            int holdHook = TryPatch(typeof(AIWeaponTargetingGoal), "TargetWeapons", "GoalTargetWeaponsPrefix", "GoalTargetWeaponsPostfix");
            HoldHookPatched = holdHook == 1; // without it the strategy never holds fire (nothing would stop the shots)
            patched += holdHook;
            patched += TryPatch(typeof(AIWeaponTargeting), "HandleSecondPhaseTargeting", "SecondPhasePrefix", null);
            patched += TryPatch(typeof(PlayerBuildingHandler), "DealDamageToBuilding", "DealDamagePrefix", null);
            patched += TryPatch(typeof(AICore), "BeginTurn", null, "CoreBeginTurnPostfix", typeof(EconomyPatches));
            patched += TryPatch(typeof(AICore), "EndTurn", null, "CoreEndTurnPostfix", typeof(EconomyPatches));
            patched += TryPatch(typeof(AIConstruction), "tryConstructNewBuilding", "TryConstructPrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AITechQueue), "BeginTurn", "TechQueueBeginTurnPrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AIDefenseConstruction), "TryConstructDefense", "TryConstructDefensePrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AIBuildingPlacementSearch), "step", "SearchStepPrefix", null, typeof(DirectorPatches));
            patched += TryPatch(typeof(AIBuildingPlacementFinder), "Update", "FinderUpdatePrefix", null, typeof(DirectorPatches));
            patched += TryPatch(typeof(PlayerDefenseHandler), "IsHitIntercepted", "BeforeIntercept", "AfterIntercept", typeof(LedgerPatches));
            int laserPatches = EnableLasers.Value ? LaserPatches.Install(new Harmony(GUID + ".lasers")) : 0;
            Log.LogInfo(NAME + " " + VERSION + " loaded; " + patched + " of 14 hooks patched. Director " + (DirectorEnabled.Value ? "on" : "off")
                + ", war from turn " + WarFromTurn.Value + ". Accuracy by level: " + AccuracyByLevel.Value
                + "; research plan " + ResearchPlanIds.Count + " techs; superweapons " + Superweapons.Value
                + "; lasers " + (EnableLasers.Value ? laserPatches + " of 5 patches" : "off"));
        }

        private int TryPatch(Type type, string method, string prefix, string postfix, Type patchClass = null)
        {
            if (patchClass == null) patchClass = typeof(AiPatches);
            try
            {
                MethodInfo target = AccessTools.Method(type, method);
                if (target == null)
                {
                    Log.LogError("Hook target not found: " + type.Name + "." + method + " - that behaviour stays native.");
                    return 0;
                }
                HarmonyMethod pre = prefix == null ? null : new HarmonyMethod(patchClass, prefix);
                HarmonyMethod post = postfix == null ? null : new HarmonyMethod(patchClass, postfix);
                m_harmony.Patch(target, pre, post);
                return 1;
            }
            catch (Exception e)
            {
                Log.LogError("Failed to patch " + type.Name + "." + method + ": " + e);
                return 0;
            }
        }

        private static void ApplyConfig()
        {
            Planner.FocusShare = Clamp01(FocusShare.Value);
            Planner.LeaderWeight = LeaderWeight.Value;
            Planner.GrudgeWeight = GrudgeWeight.Value;
            Planner.FinishWeight = FinishWeight.Value;
            Planner.HumanBias = HumanBias.Value;
            Planner.UnknownEconomyShare = UnknownEconomyShare.Value;

            StrategyCfg.StrikeEvery = Math.Max(1, StrikeEvery.Value);
            StrategyCfg.MinPassingShots = MinPassingShots.Value;
            StrategyCfg.MinPassShare = Clamp01(MinPassShare.Value);
            StrategyCfg.BrokenPassShare = Clamp01(BrokenPassShare.Value);
            StrategyCfg.MaxHoldTurns = Math.Max(1, MaxHoldTurns.Value);
            StrategyCfg.MinWeapons = Math.Max(1, MinWeapons.Value);
            StrategyCfg.CoverHalf = Math.Max(0.1f, CoverHalf.Value);
            StrategyCfg.RollEnergyShare = Clamp01(RollEnergyShare.Value);
            StrategyCfg.ObservedMinImpacts = Math.Max(1, ObservedMinImpacts.Value);

            Build.Growth = BuildPlanner.Parse(GrowthWeights.Value, Build.Growth);
            Build.Pressure = BuildPlanner.Parse(PressureWeights.Value, Build.Pressure);
            Build.GrowthUntilTurn = GrowthUntilTurn.Value;
            Build.DefenseBonusWhenHit = DefenseBonusWhenHit.Value;
            SuperweaponIds = Brain.ResearchPlan.Parse(Superweapons.Value);
            ResearchPlanIds = Brain.ResearchPlan.Parse(ResearchPlan.Value);

            float[] parsed = ParseAccuracy(AccuracyByLevel.Value);
            if (parsed == null)
            {
                Log.LogError("AccuracyByLevel '" + AccuracyByLevel.Value + "' is not a comma-separated list of numbers 0-1; keeping " + string.Join(",", Array.ConvertAll(Accuracy, a => a.ToString(CultureInfo.InvariantCulture))));
            }
            else
            {
                Accuracy = parsed;
            }
        }

        private static float[] ParseAccuracy(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split(',');
            float[] values = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                float v;
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) || v < 0f || v > 1f) return null;
                values[i] = v;
            }
            return values;
        }

        private static float Clamp01(float v)
        {
            return Math.Max(0f, Math.Min(1f, v));
        }

        // Offline only: in an online game every client must run the same AI.
        internal static bool Active
        {
            get { return Enabled.Value && !GameManager.OnlineGame && !GameSessionManager.OnlineGame; }
        }

        internal static void ResetMatchMemory()
        {
            Grudges.Clear();
            Ledger.Clear();
            Reach.Clear();
            AccuracyCarry.Clear();
            LastTurnSeen = 0;
            EconomyPatches.Reset();
            StrategyPatches.Reset();
        }

        internal static void WriteDecision(string json)
        {
            if (!DecisionLog.Value) return;
            try
            {
                if (s_decisionWriter == null)
                {
                    string dir = Path.Combine(Paths.BepInExRootPath, "InterplanetaryAI");
                    Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, "decisions-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
                    s_decisionWriter = new StreamWriter(file, true, new UTF8Encoding(false));
                    s_decisionWriter.AutoFlush = true;
                    Log.LogInfo("Decision log: " + file);
                }
                s_decisionWriter.WriteLine(json);
            }
            catch (Exception e)
            {
                Log.LogError("Decision log write failed, disabling it: " + e.Message);
                DecisionLog.Value = false;
            }
        }
    }
}
