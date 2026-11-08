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
using Interplanetary.Weapons;
using Interplanetary.Weapons.Trajectories;
using InterplanetaryAI.Brain;

namespace InterplanetaryAI
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "pavol.interplanetary.ai";
        public const string NAME = "Interplanetary EE: Smarter AI";
        public const string VERSION = "0.22.0";

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
        internal static ConfigEntry<string> SmartLevels;             // v0.17.1
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
        internal static ConfigEntry<int> RetaliationLatchFromTurn;   // v0.20 (council I2)
        internal static ConfigEntry<int> RetaliationQuietTurns;      // v0.20 (council I2)
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
        internal static ConfigEntry<float> EnergyGlutShare;
        internal static ConfigEntry<int> EnergyGlutMin;
        internal static ConfigEntry<float> LeaderInfluence;
        internal static ConfigEntry<float> BigSalvoShots;
        internal static ConfigEntry<float> AbortBelowShare;
        internal static ConfigEntry<int> AbortBelowWeapons;
        internal static ConfigEntry<float> AimingWindowSeconds;
        internal static ConfigEntry<bool> AimExtend;                 // v0.18
        internal static ConfigEntry<float> HumanFirstReachShare;     // v0.18
        internal static ConfigEntry<float> HumanFirstReachMinScore;  // v0.18 (Terra #1)
        internal static ConfigEntry<float> AimingWindowMax;          // v0.18
        internal static ConfigEntry<float> AimTargetShare;           // v0.18
        internal static ConfigEntry<float> AimStallSeconds;          // v0.18
        internal static ConfigEntry<float> AttritionLossShare;
        internal static ConfigEntry<float> ReturnFirePassShare;
        internal static ConfigEntry<float> ReturnFireMinPassing;
        internal static ConfigEntry<float> IntelSprintEnergyReserve;
        internal static ConfigEntry<float> OpeningPlantReserve;
        internal static ConfigEntry<int> AiProcessorResources;
        internal static ConfigEntry<int> PlacementBackoffTurns;
        internal static ConfigEntry<int> ResearchQueueDepth;
        internal static ConfigEntry<float> GuidedAccuracy;
        internal static ConfigEntry<float> VolleyOverkill;
        internal static ConfigEntry<bool> GuidedCityCover;
        internal static ConfigEntry<int> IntelSprintUntilTurn;
        internal static ConfigEntry<int> IntelSprintBuildings;
        internal static ConfigEntry<int> IntelSprintPerTurn;
        internal static ConfigEntry<float> IntelSprintMaterialReserve;
        internal static ConfigEntry<bool> CounterintelProjects;
        internal static ConfigEntry<float> ReachFailMinShare;
        internal static ConfigEntry<int> MaxWeapons;
        internal static ConfigEntry<int> MaterialHorizonTurn;
        internal static ConfigEntry<int> MaterialReserve;
        internal static ConfigEntry<float> EnergySaturation;
        internal static ConfigEntry<int> SellDryMinesPerTurn;
        internal static ConfigEntry<bool> RepairEnabled;
        internal static ConfigEntry<float> RepairEnergyShare;
        internal static ConfigEntry<int> RepairsPerTurn;
        internal static ConfigEntry<float> RepairBelowHpShare;
        internal static ConfigEntry<float> RepairMaterialShare;
        internal static ConfigEntry<float> IncomingImpactsPerWeapon;
        internal static ConfigEntry<bool> CounterResearch;
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
        // v0.11: Experimental Matter Control (global material project; the human had it at T11) at #10: v0.10.1 AIs were
        // material-bound with 8-59k energy unspent from T22. Railgun projectile techs (Magnetic Radiation Shielding,
        // Ultracapacitors) ahead of Uranium Alloys. Ultrasonic Projection (Shield Defense) added; it was missing.
        // v0.13: Orbital Assembly (Missile) at #13, right after the telescope tech: v0.12.1 AIs researched it at T21-22, after
        // war began at T20, and filled the 30-weapon cap with Railguns into a Kinetic-only human.
        // v0.15: Self-Adjusting Optics + Advanced Signal Processing (Telescope Array, 650 science) at #3-4, before the human's
        // counterintel (v0.14.1: they were #11-12, first Telescopes at T16, the human's first hub at T13, no city ever seen).
        // v0.16 (council v0.16 item 2, decision-record-r2): economy and science first. Super Tensile (Offshore Mine), Smart
        // Grids (Floating Plant), Quantum Computing, Experimental Matter Control (Electromagnetic Jamming, science x1.25 per
        // city), Nanostructuring + Microwave (Floating Plant yield), 4th Gen + Lossless (SSTAR, energy x1.3 per city), then
        // Telescopes, Kinetic (2nd Gen + Maglev), Plasma right after Maglev (Ion insurance), Orbital Assembly (Missile), Early
        // Warning AI (Kinetic range). Ultracapacitors + Composite Heat Cells (blind artillery, Pavol's P1) never before EMC,
        // Nano, Microwave, Lossless and Plasma. Robotic Mining stays late: the limited-planet upgrade policy cannot install its
        // second yield upgrade (council N6). Match 16 had EMC ~T18, Microwave T22, Plasma T28.
        // v0.16.1 (match 17): the v0.16 order put Kinetic (2nd Gen + Maglev) at #13-14 and Telescopes at #11-12, after Nano,
        // Microwave, 4th Gen and Lossless; at 400-600 science the AI idled 6-7k energy a turn T16-T19 waiting for them, met
        // the first volley with 6 Kinetics at T20 and never saw a city. Defense and sight now follow EMC (Jamming); the
        // energy upgrades follow them (the AI's energy was already ahead of the human's). Plasma stays right after Maglev.
        private const string DefaultResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SuperTensileMaterials,tec_SmartGrids,tec_QuantumComputing,"
            + "tec_ExperimentalMatterControl,tec_2ndGenComposites,tec_MaglevServos,tec_PlasmaChannelInduction,tec_SelfAdjustingOptics,"
            + "tec_AdvancedSignalProcessing,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,"
            + "tec_OrbitalAssembly,tec_EarlyWarningAI,tec_MagneticRadiationShielding,tec_Ultracapacitors,tec_CompositeHeatCells,"
            + "tec_UraniumAlloys,tec_EmDrives,tec_ThermonuclearWarheads,tec_InductionCoils,tec_ZeroReluctanceEngineering,"
            + "tec_MagneticSaturationElimination,tec_RoboticMining,tec_UltrasonicProjection,tec_Metamaterials,tec_DesignerNanomaterials,"
            + "tec_VerticalFarming,tec_QuantumCommunications,tec_AtmosphericInterferenceMitigation,tec_Octaazacubane,tec_AdvancedGenetics,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,tec_MassDrivers";

        // v0.16.0 default, replaced on load if the config still holds it unchanged.
        private const string V016ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SuperTensileMaterials,tec_SmartGrids,tec_QuantumComputing,"
            + "tec_ExperimentalMatterControl,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,"
            + "tec_SelfAdjustingOptics,tec_AdvancedSignalProcessing,tec_2ndGenComposites,tec_MaglevServos,tec_PlasmaChannelInduction,"
            + "tec_OrbitalAssembly,tec_EarlyWarningAI,tec_MagneticRadiationShielding,tec_Ultracapacitors,tec_CompositeHeatCells,"
            + "tec_UraniumAlloys,tec_EmDrives,tec_ThermonuclearWarheads,tec_InductionCoils,tec_ZeroReluctanceEngineering,"
            + "tec_MagneticSaturationElimination,tec_RoboticMining,tec_UltrasonicProjection,tec_Metamaterials,tec_DesignerNanomaterials,"
            + "tec_VerticalFarming,tec_QuantumCommunications,tec_AtmosphericInterferenceMitigation,tec_Octaazacubane,tec_AdvancedGenetics,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,tec_MassDrivers";

        // v0.15 default, replaced on load if the config still holds it unchanged.
        private const string V015ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SelfAdjustingOptics,tec_AdvancedSignalProcessing,tec_SmartGrids,"
            + "tec_SuperTensileMaterials,tec_QuantumComputing,tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,tec_2ndGenComposites,"
            + "tec_MaglevServos,tec_ExperimentalMatterControl,tec_OrbitalAssembly,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,"
            + "tec_PlasmaChannelInduction,tec_MagneticRadiationShielding,tec_Ultracapacitors,tec_UraniumAlloys,tec_EmDrives,"
            + "tec_ThermonuclearWarheads,tec_InductionCoils,tec_ZeroReluctanceEngineering,tec_MagneticSaturationElimination,tec_RoboticMining,"
            + "tec_EarlyWarningAI,tec_UltrasonicProjection,tec_Metamaterials,tec_DesignerNanomaterials,tec_VerticalFarming,"
            + "tec_QuantumCommunications,tec_AtmosphericInterferenceMitigation,tec_CompositeHeatCells,tec_Octaazacubane,tec_AdvancedGenetics,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,tec_MassDrivers";

        // v0.13-v0.14 default, replaced on load if the config still holds it unchanged.
        private const string V014ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SmartGrids,tec_SuperTensileMaterials,tec_QuantumComputing,"
            + "tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,tec_2ndGenComposites,tec_MaglevServos,tec_ExperimentalMatterControl,"
            + "tec_SelfAdjustingOptics,tec_AdvancedSignalProcessing,tec_OrbitalAssembly,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,"
            + "tec_PlasmaChannelInduction,tec_MagneticRadiationShielding,tec_Ultracapacitors,tec_UraniumAlloys,tec_EmDrives,"
            + "tec_ThermonuclearWarheads,tec_InductionCoils,tec_ZeroReluctanceEngineering,tec_MagneticSaturationElimination,tec_RoboticMining,"
            + "tec_EarlyWarningAI,tec_UltrasonicProjection,tec_Metamaterials,tec_DesignerNanomaterials,tec_VerticalFarming,"
            + "tec_QuantumCommunications,tec_AtmosphericInterferenceMitigation,tec_CompositeHeatCells,tec_Octaazacubane,tec_AdvancedGenetics,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,tec_MassDrivers";

        // v0.11 default, replaced on load if the config still holds it unchanged.
        private const string V011ResearchPlan =
            "tec_MassBiochemicalProcessing,tec_MolecularRecycling,tec_SmartGrids,tec_SuperTensileMaterials,tec_QuantumComputing,"
            + "tec_4thGenNuclearReactors,tec_LosslessEnergyTransfer,tec_2ndGenComposites,tec_MaglevServos,tec_ExperimentalMatterControl,"
            + "tec_SelfAdjustingOptics,tec_AdvancedSignalProcessing,tec_Nanostructuring,tec_MicrowaveEnergyTranmission,tec_OrbitalAssembly,"
            + "tec_PlasmaChannelInduction,tec_MagneticRadiationShielding,tec_Ultracapacitors,tec_UraniumAlloys,tec_EmDrives,"
            + "tec_ThermonuclearWarheads,tec_InductionCoils,tec_ZeroReluctanceEngineering,tec_MagneticSaturationElimination,tec_RoboticMining,"
            + "tec_EarlyWarningAI,tec_UltrasonicProjection,tec_Metamaterials,tec_DesignerNanomaterials,tec_VerticalFarming,"
            + "tec_QuantumCommunications,tec_AtmosphericInterferenceMitigation,tec_CompositeHeatCells,tec_Octaazacubane,tec_AdvancedGenetics,"
            + "tec_SelfHealingMaterials,tec_AITargetTracking,tec_MultiChainedStages,tec_GammaRayLasers,tec_MassDrivers";

        // v0.10 default, replaced on load if the config still holds it unchanged.
        private const string V010ResearchPlan =
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
        // v0.16 (council v0.16, decision-record-r2)
        internal static ConfigEntry<int> OpeningPermanentCap;
        internal static ConfigEntry<int> ProjectSlotReserve;
        internal static ConfigEntry<int> JammingMax;
        internal static ConfigEntry<int> JammingMinPlanScience;
        internal static ConfigEntry<int> WarFallbackTurn;
        internal static ConfigEntry<int> WarMinIncome;
        internal static ConfigEntry<float> IntelBudgetShare;
        internal static ConfigEntry<int> IntelBidFromTurn;
        internal static ConfigEntry<int> IntelBatch;
        internal static ConfigEntry<int> IntelEvalTurns;
        internal static ConfigEntry<int> IntelFailedBatchesStop;
        internal static ConfigEntry<int> IntelProbeMax;
        internal static ConfigEntry<float> PrecisionMinGuided;
        internal static ConfigEntry<float> PrecisionMinShare;
        internal static ConfigEntry<float> PrecisionKillShare;
        internal static ConfigEntry<bool> BlindArtillery;
        internal static ConfigEntry<string> BlindArtilleryUpgrades;
        internal static ConfigEntry<string> DefenseUpgradeIds;
        internal static ConfigEntry<int> DefenseUpgradesPerTurn;
        internal static ConfigEntry<float> ForwardDefenseMargin;
        internal static ConfigEntry<int> RareMaterialMax;            // v0.16.1; v0.17 key RareMaterialBuildUpMax
        internal static ConfigEntry<int> RareMaterialEndLead;        // v0.17
        internal static ConfigEntry<bool> ShelterPlacement;          // v0.17
        internal static ConfigEntry<int> ShelterCoveredSteps;        // v0.17
        internal static ConfigEntry<int> ShelterCoverCap;            // v0.17
        internal static ConfigEntry<float> AnchorRingShare;          // v0.17
        internal static ConfigEntry<float> ShelterCityKeepOut;       // v0.17 (Terra H3)
        internal static ConfigEntry<bool> OpeningCapExemptScience;   // v0.16.1
        internal static ConfigEntry<float> IntelStopMinBudgetUse;    // v0.16.1
        internal static ConfigEntry<bool> PlanetDrainGuard;          // v0.16.1
        internal static ConfigEntry<int> PlanetGuardHorizonTurn;     // v0.16.1
        internal static ConfigEntry<float> HumanFirstShare;          // v0.16.2
        internal static ConfigEntry<float> HumanFirstMinPassing;     // v0.16.2
        internal static ConfigEntry<bool> FreeFire;                  // v0.16.2
        internal static ConfigEntry<float> FreeFireMinPassing;       // v0.16.2
        internal static ConfigEntry<bool> LateFirePulseWait;         // v0.20 (council S1)
        internal static ConfigEntry<bool> AnchorMediumMatch;         // v0.21 (council I9)
        internal static ConfigEntry<bool> WaterPlantForWater;        // v0.22
        internal static ConfigEntry<float> MaterialBoundEnergyShare; // v0.16.2
        internal static ConfigEntry<float> MaterialBoundStockShare;  // v0.16.2
        internal static ConfigEntry<int> MaterialBoundMinEnergy;     // v0.16.2
        internal static ConfigEntry<int> MaterialBoundFromTurn;      // v0.16.2
        internal static ConfigEntry<int> MaterialBoundMinSlots;      // v0.16.2
        internal static ConfigEntry<float> ExtractionFirstShare;     // v0.16.2
        internal static ConfigEntry<float> AntiNukeCityValue;
        internal static ConfigEntry<int> NukeMemoryTurns;
        internal static ConfigEntry<float> CoverageCityValue;
        internal static ConfigEntry<float> CoverageBand;
        internal static ConfigEntry<float> ObservedPriorWeight;
        internal static ReachabilityMemory Reach;
        internal static readonly Dictionary<int, float> AccuracyCarry = new Dictionary<int, float>();
        internal static readonly Dictionary<int, float> GuidedAccuracyCarry = new Dictionary<int, float>(); // v0.16 item 4
        internal static readonly System.Random Rng = new System.Random();
        internal static int LastTurnSeen;

        private static StreamWriter s_decisionWriter;
        private Harmony m_harmony;

        private static ConfigFile s_config;                          // v0.20: for the config snapshot in init records

        // v0.20 (council I1a): the build that produced a data set, e.g. "0.20.0+2f297e2" or "0.20.0+2f297e2.dirty"
        // (InformationalVersion, stamped by the StampBuild target in the csproj).
        internal static string BuildId
        {
            get
            {
                try
                {
                    object[] a = typeof(Plugin).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
                    return a.Length > 0 ? ((AssemblyInformationalVersionAttribute)a[0]).InformationalVersion : VERSION;
                }
                catch (Exception) { return VERSION; }
            }
        }

        // v0.20 (council I1a): every config value that differs from its default, as a JSON object of strings, so the
        // decision log records the effective configuration (and an experiment arm set through the config).
        internal static string NonDefaultConfigJson()
        {
            var sb = new StringBuilder("{");
            try
            {
                if (s_config != null)
                {
                    bool first = true;
                    foreach (ConfigDefinition def in s_config.Keys)
                    {
                        ConfigEntryBase entry = s_config[def];
                        if (entry == null || Equals(entry.BoxedValue, entry.DefaultValue)) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append('"').Append(JsonEscape(def.Section + "." + def.Key)).Append("\":\"")
                          .Append(JsonEscape(Convert.ToString(entry.BoxedValue, CultureInfo.InvariantCulture))).Append('"');
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Config snapshot failed: " + e.Message);
            }
            return sb.Append('}').ToString();
        }

        private static string JsonEscape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void Awake()
        {
            Log = Logger;
            s_config = Config;

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
            SmartLevels = Config.Bind("General", "SmartLevels", "3",
                "v0.17.1 (read at game start; restart after a change): lobby AI levels (0 Easy, 1 Medium, 2 Hard, 3 Impossible; comma-separated) that run the Smarter AI. Every other AI runs the game's original AI untouched (native aim: 0.4 / 0.75 / 0.9 / 1.0). \"0,1,2,3\" = every AI is the Smarter AI (v0.17.0 behaviour).");
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
            GuidedAccuracy = Config.Bind("Strikes", "GuidedAccuracy", 0.85f,
                "v0.15: planned hit rate for guided weapons (second-phase targeting: Missile, Nuclear Missile; MIRV has no second phase in this engine and keeps the level rate). Used by firing and by the strategy estimate (v0.15.1). Pavol's decision: his own measured Missile rate (60 of 71 shots hit the planet, v0.14.1 match). Unguided weapons keep the level accuracy. 0 = use the level accuracy for all.");
            VolleyOverkill = Config.Bind("Strikes", "VolleyOverkill", 1.25f,
                "v0.15: a guided target counts as fully allocated once the damage aimed at it this volley reaches this multiple of its durability (population for cities, HP otherwise); further shots move to the next target.");
            GuidedCityCover = Config.Bind("Director", "GuidedCityCover", true,
                "v0.15: any enemy guided weapon (Missile, MIRV, Nuke) seen or landed triggers the per-city anti-missile cover (AntiNukePerCity defenses over each city), without the defense throttle. Off = Nuclear Missiles only (v0.14).");
            IntelSprintUntilTurn = Config.Bind("Director", "IntelSprintUntilTurn", 0,
                "v0.15: until this turn, while an enemy city has never been seen, intel buildings may be queued IntelSprintPerTurn a turn up to IntelSprintBuildings. Cities never move and once seen stay on the map for guided strikes. 0 = off.");
            IntelSprintBuildings = Config.Bind("Director", "IntelSprintBuildings", 6,
                "v0.15: intel buildings the sprint aims for (6 Telescopes x 8 intel = 48 >= a small city's 45 stealth before counterintel).");
            IntelSprintPerTurn = Config.Bind("Director", "IntelSprintPerTurn", 3, "v0.15: intel buildings a turn during the sprint.");
            IntelSprintEnergyReserve = Config.Bind("Director", "IntelSprintEnergyReserve", 450f,
                "v0.15.2: intel wanted only to find unseen enemy cities waits while energy is below this (a Telescope 200 + a power plant 250), so the economy still gets a plant that turn. v0.15.1 Thistle bought 8 Telescopes at T6-T8 and no plant; energy fell 925 -> 785. 0 = off.");
            OpeningPlantReserve = Config.Bind("Director", "OpeningPlantReserve", 250f,
                "v0.15.3: until IntelSprintUntilTurn, a counterintel city project must leave this much energy (one power plant) for the economy step (Sol v0.15.2 #3). 0 = off.");
            IntelSprintMaterialReserve = Config.Bind("Director", "IntelSprintMaterialReserve", 650f,
                "v0.15.1: the sprint's extra intel buildings wait while material is below this (a Telescope 300 + a Molecular Recycling project 350), so the opening economy keeps compounding (Sol v0.15 #5). The normal intel limit is unaffected.");
            CounterintelProjects = Config.Bind("Director", "CounterintelProjects", true,
                "v0.15: buy the Quantum Encryption Hub city project (+50 counterintel per city, permanent) in every city, up to 2 a turn, from CounterintelFromTurn.");
            ResearchQueueDepth = Config.Bind("Research", "QueueDepth", 3,
                "v0.14.1: plan targets queued at once (each with its prerequisites). The game spends science down the queue and banks the excess at its end; 1 = one target at a time (v0.13).");
            ResearchPlan = Config.Bind("Research", "Plan", DefaultResearchPlan,
                "Tech IDs in order. Default (v0.3): the human's order from the 31 Oct 2026 win, economy first (material and energy projects, yield upgrades, Offshore Mine, Floating Plant), then the Nuclear Missile line, then the rest.");
            if (ResearchPlan.Value == V02ResearchPlan || ResearchPlan.Value == V03ResearchPlan || ResearchPlan.Value == V04ResearchPlan
                || ResearchPlan.Value == V09ResearchPlan || ResearchPlan.Value == V010ResearchPlan || ResearchPlan.Value == V011ResearchPlan
                || ResearchPlan.Value == V014ResearchPlan || ResearchPlan.Value == V015ResearchPlan || ResearchPlan.Value == V016ResearchPlan)
            {
                ResearchPlan.Value = DefaultResearchPlan;
                Log.LogInfo("Research plan: replaced an unchanged older default with the v0.16.1 default.");
            }
            if (Math.Abs(GrudgeWeight.Value - 0.6f) < 1e-4f)
            {
                GrudgeWeight.Value = 0.35f; // v0.3 default; grudge alone drove blind AIs into vendettas
                Log.LogInfo("GrudgeWeight: replaced the unchanged v0.3 default 0.6 with 0.35.");
            }

            UnknownEconomyShare = Config.Bind("Targeting", "UnknownEconomyShare", 0.8f,
                "Economy share (of the strongest known) assumed for an enemy whose production our intel cannot read. Unknown is not weak.");
            IntelPerTurn = Config.Bind("Director", "IntelPerTurn", 2, "Most intel buildings (Telescope Array) queued per turn while some enemy's numbers are unreadable.");
            MaxIntelBuildings = Config.Bind("Director", "MaxIntelBuildings", 21, "Cap on intel buildings.");
            DefenseReserveShare = Config.Bind("Director", "DefenseReserveShare", 0.4f,
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
            MaterialGlutRatio = Config.Bind("Director", "MaterialGlutRatio", 1.5f,
                "v0.10: material counts as in glut once the banked stock is this many times the turn's energy budget; energy gains then count fully, material gains and material cost at 0.25. 0 = off.");
            MaterialGlutMin = Config.Bind("Director", "MaterialGlutMin", 5000, "Smallest material stock that counts as glut.");
            EnergyGlutShare = Config.Bind("Director", "EnergyGlutShare", 0.3f,
                "v0.11: energy counts as in glut when last turn ended with at least this share of its energy budget unspent; material gains then count fully, energy gains not at all (v0.12), energy cost at 0.25, and weapon upgrades are ranked by damage per building. 0 = off.");
            EnergyGlutMin = Config.Bind("Director", "EnergyGlutMin", 5000, "Smallest unspent energy that counts as glut.");
            MaxWeapons = Config.Bind("Director", "MaxWeapons", 30,
                "v0.12: most aimed weapons (all but lasers and superweapons) per AI. The game's trajectory search is time-bound: v0.11.1 aimed 2-15 of ~50 at the human. Surplus goes to weapon upgrades. 0 = no cap.");
            MaterialHorizonTurn = Config.Bind("Director", "MaterialHorizonTurn", 45,
                "v0.12: the planet's remaining material is budgeted to last until this turn. Energy income above EnergySaturation x what that material can use is not built. v0.11.1 AIs ran their planets dry at T26-27, the human at T38.");
            EnergySaturation = Config.Bind("Director", "EnergySaturation", 0f,
                "v0.12: see MaterialHorizonTurn. 0 = off (v0.13 default: energy is the throughput that spends material; v0.12.1 capped it at 16-24k while 96k material sat unused). In material glut, extraction is not bought instead.");
            RepairEnabled = Config.Bind("Director", "Repair", true,
                "v0.13: repair damaged buildings at turn start (the game's own AI never repairs; a human does between strikes). Weapons, defenses, power first.");
            RepairEnergyShare = Config.Bind("Director", "RepairEnergyShare", 0.25f, "v0.13: most of a turn's energy spent on repairs.");
            RepairsPerTurn = Config.Bind("Director", "RepairsPerTurn", 40, "v0.13: most repairs a turn. 0 = off.");
            RepairBelowHpShare = Config.Bind("Director", "RepairBelowHpShare", 0.8f, "v0.13: repair a building at or below this share of its HP (a repair costs the same whatever the damage).");
            RepairMaterialShare = Config.Bind("Director", "RepairMaterialShare", 0.5f, "v0.13.1: most of the material stock spent on repairs in a turn.");
            IncomingImpactsPerWeapon = Config.Bind("Director", "IncomingImpactsPerWeapon", 2f,
                "v0.13.1: weapon types that hit us count as known enemy weapons (for counter-defenses and counter research) even when their buildings are hidden: impacts / this, remembered with WeaponMemoryDecay. 0 = off.");
            MaterialReserve = Config.Bind("Director", "MaterialReserve", 15000,
                "v0.12: once the planet will run dry within BankWarningTurns, the economy keeps this much material back for defenses, weapons and rebuilding. 0 = off.");
            SellDryMinesPerTurn = Config.Bind("Director", "SellDryMinesPerTurn", 10,
                "v0.12: once the planet is dry, sell this many mines a turn (they yield nothing; selling returns 80% of their material and upgrades). 0 = off.");
            CounterResearch = Config.Bind("Research", "CounterResearch", true,
                "v0.11: when a known enemy weapon has no counter-defense we can build (Lasers -> Shield), research the tech that unlocks it next.");

            StrategyEnabled = Config.Bind("Strategy", "Enabled", true,
                "v0.10: hold fire (and give the firing energy the game reserves to construction) until a salvo at one committed target is expected to get enough shots through its defenses; then strike, at most every StrikeEvery turns. Off = fire every turn, split targets (v0.9).");
            StrikeEvery = Config.Bind("Strategy", "StrikeEvery", 2, "Turns between strikes (2 = every other turn, as the human who won).");
            MinPassingShots = Config.Bind("Strategy", "MinPassingShots", 12f, "Strike when at least this many shots are expected through the target's defenses...");
            MinPassShare = Config.Bind("Strategy", "MinPassShare", 0.45f, "...and at least this share of the salvo.");
            BrokenPassShare = Config.Bind("Strategy", "BrokenPassShare", 0.7f, "Expected pass share at which the target counts as broken: strike again without waiting for the pulse.");
            MaxHoldTurns = Config.Bind("Strategy", "MaxHoldTurns", 5, "At war with weapons, never hold fire longer than this many turns in a row.");
            MinWeapons = Config.Bind("Strategy", "MinWeapons", 6, "Fewer weapons than this: keep building, do not fire.");
            CoverHalf = Config.Bind("Strategy", "CoverHalf", 12f, "Defenses at which half of our shots are assumed to meet one in range (corrected by our observed shots).");
            LeaderInfluence = Config.Bind("Strategy", "LeaderInfluence", 0.5f,
                "v0.11: commit value = expected shots through x (1 + this x target score / best score). v0.10.1 used score x shots through and struck the other AI while the human was the softer target.");
            BigSalvoShots = Config.Bind("Strategy", "BigSalvoShots", 24f, "Strike when this many shots are expected through, whatever share of the salvo that is.");
            AbortBelowShare = Config.Bind("Strategy", "AbortBelowShare", 0f,
                "At firing time, call a strike off when fewer than max(AbortBelowWeapons, this share of the weapons) found a trajectory onto the committed target. v0.12: 0 (v0.11.1 aborted 13 of 26 strikes at 0.3).");
            AbortBelowWeapons = Config.Bind("Strategy", "AbortBelowWeapons", 3, "v0.12: call a strike off when fewer than this many weapons found a trajectory onto the committed target.");
            AimingWindowSeconds = Config.Bind("Strategy", "AimingWindowSeconds", 40f,
                "v0.12: on a strike turn the AI keeps searching for trajectories until this many seconds after its turn began, or the human ends the turn if later (the game: 20). Hold turns keep 20. Longer = more weapons aimed, more waiting between turns. 20-120.");
            AimExtend = Config.Bind("Strategy", "AimExtend", true,
                "v0.18: on a firing turn keep searching for trajectories past AimingWindowSeconds while fewer than AimTargetShare of the weapons have one and the search found a new one within AimStallSeconds, up to AimingWindowMax. Match 21: 4-13 of ~30 weapons aimed on several firing turns.");
            AimingWindowMax = Config.Bind("Strategy", "AimingWindowMax", 90f, "v0.18: hard cap (seconds since the AI's turn began) for the extended aim window. The wait only shows when the human ends the turn before it.");
            AimTargetShare = Config.Bind("Strategy", "AimTargetShare", 0.8f, "v0.18: stop extending once this share of the weapons has a trajectory.");
            AimStallSeconds = Config.Bind("Strategy", "AimStallSeconds", 15f, "v0.18: stall budget - stop extending when no new trajectory was found for this many seconds. A heuristic (random search or a low frame rate can also stall), not proof that the rest cannot reach.");
            AttritionLossShare = Config.Bind("Strategy", "AttritionLossShare", 0.15f,
                "v0.14: fire whatever aims when our live weapons dropped since last turn by at least this share (and at least 2), pulse or not. v0.13.1 Guts held at 26 -> 17 -> 11 -> 10 weapons while the human destroyed them unfired. 0 = off.");
            ReturnFirePassShare = Config.Bind("Strategy", "ReturnFirePassShare", 0.6f,
                "v0.15.2: under attack, fire when at least this share of the salvo is expected through (and ReturnFireMinPassing shots), pulse or not. v0.15.1 Thistle held at 64-82% through, short of MinPassingShots, while the human hit it every turn. 0 = off.");
            ReturnFireMinPassing = Config.Bind("Strategy", "ReturnFireMinPassing", 4f, "v0.15.2: fewest expected shots through for return fire.");
            AiProcessorResources = Config.Bind("Strategy", "AiProcessorResources", 2600,
                "v0.14: total trajectory-search budget per frame, shared by all AIs (the game: 1200). Each AI weapon gets clamp(this / all AI weapons, 30, 100) steps per frame: 1200 gives 26 weapons 46 steps, 2600 gives them the full 100. Higher = more weapons aimed in the same time, more CPU per frame during the human's turn. 0 = the game's 1200.");
            ReachFailMinShare = Config.Bind("Strategy", "ReachFailMinShare", 0.5f,
                "v0.12: a weapon that found no trajectory marks its target unreachable only when at least this share of the weapons aimed at that target found one (otherwise the search ran out of time).");
            RollEnergyShare = Config.Bind("Strategy", "RollEnergyShare", 0.5f, "Share of a target's energy income assumed left for its defense rolls (150 energy each).");

            DirectorEnabled = Config.Bind("Director", "Enabled", true,
                "v0.3 economy director: replaces the native construction choice. Buys yield upgrades and city projects first, keeps energy income near EnergyToMaterialTarget x material income, places water buildings properly, holds weapons until WarFromTurn, and counters visible enemy weapons with defenses. Off = the v0.2 build planner.");
            WarFromTurn = Config.Bind("Director", "WarFromTurn", 16,
                "Turn from which the AI builds weapons (v0.9.1: the human built its first at T22). Before it, only retaliation after heavy damage. Firing is decided by [Strategy].");
            DefenseFromTurn = Config.Bind("Director", "DefenseFromTurn", 15,
                "v0.10: turn from which the defense floor (DefenseFloorShare) and the defense energy reserve apply, before weapons (WarFromTurn).");
            WarDamageTrigger = Config.Bind("Director", "WarDamageTrigger", 600f,
                "Recent damage taken that starts retaliation before WarFromTurn.");
            RetaliationLatchFromTurn = Config.Bind("Director", "RetaliationLatchFromTurn", 12,
                "v0.20 (council I2): from this turn on, a retaliation (war started by WarDamageTrigger) stays war for the match. Matches 22-26: 4 war->peace flips in 10 AI-lobbies as the damage decayed, 5-8 guns idle on 'peace'.");
            RetaliationQuietTurns = Config.Bind("Director", "RetaliationQuietTurns", 2,
                "v0.20 (council I2): before RetaliationLatchFromTurn, a retaliation ends only after this many consecutive turns below WarDamageTrigger (1 = the v0.19 behaviour).");
            WarUpkeepShare = Config.Bind("Director", "WarUpkeepShare", 0.4f,
                "Cap on weapon firing cost per turn as a share of energy income, from WarFromTurn.");
            RetaliationUpkeepShare = Config.Bind("Director", "RetaliationUpkeepShare", 0.15f,
                "Same cap while retaliating before WarFromTurn.");
            WarSpendShare = Config.Bind("Director", "WarSpendShare", 0.4f,
                "Share of a turn's energy spent on new weapons before the economy gets the rest. Energy left over after the economy also goes to weapons (it does not carry over).");
            EnergyToMaterialTarget = Config.Bind("Director", "EnergyToMaterialTarget", 1.0f,
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
            if (migrated.Value == "0.10")
            {
                if (Math.Abs(AbortBelowShare.Value - 0.3f) < 1e-4f)
                {
                    AbortBelowShare.Value = 0f; // v0.11.1: aborted 13 of 26 strikes, throwing away 6-15 aimed weapons each
                    Log.LogInfo("AbortBelowShare: replaced the unchanged default 0.3 with 0 (once).");
                }
                migrated.Value = "0.12";
            }
            if (migrated.Value == "0.12")
            {
                if (Math.Abs(EnergySaturation.Value - 1.5f) < 1e-4f)
                {
                    EnergySaturation.Value = 0f; // v0.12.1: capped energy at 16-24k (v0.10.1 from the same save: 52-67k) while 96k material sat unused
                    Log.LogInfo("EnergySaturation: replaced the unchanged default 1.5 with 0 (off, once).");
                }
                migrated.Value = "0.13";
            }
            DefensesPerTurn = Config.Bind("Director", "DefensesPerTurn", 3,
                "Defenses queued per turn when on target; more when behind (see DefenseCatchUpTurns).");
            DefenseFloorShare = Config.Bind("Director", "DefenseFloorShare", 0.25f,
                "At war, defenses wanted are at least this share of our own non-city buildings, seen threat or not. 0 = off (v0.4 behaviour). v0.14: 0.25 (v0.13.1: the human held 23 Kinetic per ~100 buildings before attacking; the AI 15 per 99 at 0.15).");
            if (migrated.Value == "0.13")
            {
                if (Math.Abs(DefenseFloorShare.Value - 0.15f) < 1e-4f)
                {
                    DefenseFloorShare.Value = 0.25f;
                    Log.LogInfo("DefenseFloorShare: replaced the unchanged default 0.15 with 0.25 (once).");
                }
                migrated.Value = "0.14";
            }
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
            AntiNukePriorTurn = Config.Bind("Director", "AntiNukePriorTurn", 18, "Turn from which nukes are expected without having seen one (0 = never).");
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
            PlacementBackoffTurns = Config.Bind("Director", "PlacementBackoffTurns", 5,
                "v0.14: a building type whose placements gave up 3+ times in a turn is queued at most twice a turn for this many turns, unless one of its placements succeeds (v0.13.1: 6 Offshore Mines abandoned at T14, then 13 more at T18). 0 = only the next turn (v0.11).");
            PlacementStepCap = Config.Bind("Director", "PlacementStepCap", 1500,
                "Placement attempts before a building search gives up and its cost is returned (native searches never give up).");

            // ---- v0.16 (council v0.16, docs/reviews/2026-11-01-council-v016/decision-record-r2.md) ----
            OpeningPermanentCap = Config.Bind("Director", "OpeningPermanentCap", 2,
                "v0.16 item 1: permanent city projects (owned + queued) allowed before Lossless Energy Transfer is researched. Match 16 queued 5 Molecular Recycling Plants by T7 and 2 Quantum Encryption Hubs by T11, filling all 7 slots; the first SSTAR came at T31. 0 = no cap.");
            ProjectSlotReserve = Config.Bind("Director", "ProjectSlotReserve", 1,
                "v0.16 item 1: after the opening cap, a permanent city project goes only into a city that keeps this many free project slots for Jamming / SSTAR.");
            JammingMax = Config.Bind("Director", "JammingMax", 3,
                "v0.16 item 1: most simultaneous Electromagnetic Jamming projects (science x1.25 global each, 4 turns). Lane A's provisional ceiling, adopted by the facilitator.");
            JammingMinPlanScience = Config.Bind("Director", "JammingMinPlanScience", 1500,
                "v0.16 item 1: Jamming is bought only while at least this much research-plan science remains.");
            WarFallbackTurn = Config.Bind("Director", "WarFallbackTurn", 20,
                "v0.16 item 4: war starts at WarFromTurn only when Orbital Assembly is researched and energy income is at least WarMinIncome; otherwise at this turn (v0.15 behaviour).");
            WarMinIncome = Config.Bind("Director", "WarMinIncome", 12000, "v0.16 item 4: energy income needed for the early war start (WarFromTurn).");
            IntelBudgetShare = Config.Bind("Director", "IntelBudgetShare", 0.1f,
                "v0.16 item 5: intel construction and upgrade energy stays within this share of the energy credited so far (upkeep logged separately). Replaces the unconditional unseen-city escalation (Pavol's P2: no enemy city count is used).");
            IntelBidFromTurn = Config.Bind("Director", "IntelBidFromTurn", 14,
                "v0.16 item 5: intel bought to find enemy cities waits until this turn, timed to the strike window (a T8 sighting is worthless once the city goes dark).");
            IntelBatch = Config.Bind("Director", "IntelBatch", 8, "v0.16.1: most intel buildings or Lidar upgrades in one batch; the batch is sized to the remaining budget (budget room / 200 E).");
            IntelEvalTurns = Config.Bind("Director", "IntelEvalTurns", 2, "v0.16 item 5: turns after a batch before its visibility result is judged.");
            IntelFailedBatchesStop = Config.Bind("Director", "IntelFailedBatchesStop", 2,
                "v0.16 item 5: after this many batches in a row that showed no new enemy city or readable enemy, intel stops (stop-loss) except bounded probes.");
            IntelProbeMax = Config.Bind("Director", "IntelProbeMax", 2, "v0.16 item 5: single intel buildings allowed after the stop-loss (probes).");
            PrecisionMinGuided = Config.Bind("Strategy", "PrecisionMinGuided", 6f,
                "v0.16 item 4 (S3, lane A's floor): fire below MinPassingShots when a city is visible now and at least this many guided shots are expected through...");
            PrecisionMinShare = Config.Bind("Strategy", "PrecisionMinShare", 0.9f, "...at least this share of the salvo gets through...");
            PrecisionKillShare = Config.Bind("Strategy", "PrecisionKillShare", 1f,
                "...and (S3, lane B's credible-kill predicate) the guided population damage expected through is at least this multiple of the smallest visible city's population, within the war firing budget. Match 16 held at T21-T22 with 7.65 and 10 guided shots through onto two visible cities. 0 = off.");
            BlindArtillery = Config.Bind("Director", "BlindArtillery", true,
                "v0.16 item 4 (Pavol's P1): with no enemy city visible, buy no guided weapons (match 16: 45 Missiles struck random points) and prefer upgraded unguided artillery.");
            BlindArtilleryUpgrades = Config.Bind("Director", "BlindArtilleryUpgrades", "upg_RailgunRadius3,upg_RailgunProjectileAmount2",
                "v0.16 item 4: weapon upgrades bought first while blind (Optimized Airburst, Triple Barrel Assembly).");
            DefenseUpgradeIds = Config.Bind("Director", "DefenseUpgradeIds", "upg_KineticEnergyCostDown1,upg_KineticRange1",
                "v0.16 item 6: defense upgrades bought on owned defenses, in order (Kinetic roll cost x0.7; Kinetic range x1.3).");
            DefenseUpgradesPerTurn = Config.Bind("Director", "DefenseUpgradesPerTurn", 4, "v0.16 item 6: most defense upgrades queued per turn. 0 = off.");
            ForwardDefenseMargin = Config.Bind("Director", "ForwardDefenseMargin", 1.25f,
                "v0.16 item 6: once enemy fire has landed, the defense reserve asks for the largest volley seen so far x the share of impacts a defense could meet x the live roll cost x this margin (bounded by DefenseHardCapShare). The T26 estimator asked 1,250 E one phase before an 83-impact volley. 0 = off.");
            // ---- v0.16.1 (match 17, docs/playtests/2026-11-01-v0.16.md) ----
            // v0.17: new key (the old RareMaterialMax = 1 in existing config files would otherwise keep the v0.16.1 cap).
            RareMaterialMax = Config.Bind("Director", "RareMaterialBuildUpMax", 2,
                "v0.17: most simultaneous Rare Material Synthesis projects (material x1.3, energy x0.95, planet cost x1.3 each, 6 turns) during the build-up; none may start that would run past the war turn minus RareMaterialEndLead, and none once the war has started (then SSTAR). Pavol stacked two T16-T21 in match 20 (x1.69 material). Match 17 bought 5 at T13 (took the Jamming slots; the Jamming room rule still applies).");
            RareMaterialEndLead = Config.Bind("Director", "RareMaterialEndLead", 0,
                "v0.17: a Rare Material copy must end this many turns before the war turn (0 = it may run until the war starts).");
            ShelterPlacement = Config.Bind("Director", "ShelterPlacement", true,
                "v0.17: place every non-defense building inside existing defense cover when a covered spot exists (else close to a city); water buildings around the land base (shoreline) instead of random open water. Pavol's layout in match 20.");
            ShelterCoveredSteps = Config.Bind("Director", "ShelterCoveredSteps", 6,
                "v0.17: placement steps (12 candidates each) that wait for a covered spot before the best uncovered one is taken.");
            ShelterCoverCap = Config.Bind("Director", "ShelterCoverCap", 2,
                "v0.17: defenses over a spot that still add to its score (beyond this, closeness to a city decides).");
            ShelterCityKeepOut = Config.Bind("Director", "ShelterCityKeepOut", 0.5f,
                "v0.17 (Terra H3): ground within this share of a defense radius from a city's edge is kept for that city's defenses; non-defense buildings go there only after ShelterCoveredSteps steps found nothing else. 0 = off.");
            AnchorRingShare = Config.Bind("Director", "AnchorRingShare", 0.5f,
                "v0.17: share of city-cover candidates drawn around a power anchor that reaches the city's ring (match 20: half the ring spots had no anchor in line range). 0 = v0.16 ring only.");
            OpeningCapExemptScience = Config.Bind("Director", "OpeningCapExemptScience", true,
                "v0.16.1: permanent science projects (Science Institute) are exempt from OpeningPermanentCap, still leaving room for wanted Jamming. Match 17 had half the human's science.");
            IntelStopMinBudgetUse = Config.Bind("Director", "IntelStopMinBudgetUse", 0.5f,
                "v0.16.1: the intel stop-loss needs IntelFailedBatchesStop failed batches AND at least this share of the intel budget spent. Match 17 stopped after 1,000 E of ~27,000 E.");
            PlanetDrainGuard = Config.Bind("Director", "PlanetDrainGuard", true,
                "v0.16.1: extra planet drain (mines, mine yield upgrades, Rare Material / Molecular Recycling projects) only while today's drain to PlanetGuardHorizonTurn plus the new drain fits in the remaining planet pool.");
            PlanetGuardHorizonTurn = Config.Bind("Director", "PlanetGuardHorizonTurn", 36,
                "v0.16.1: the turn the planet pool should last to under the drain guard (matches 14-17 ended T29-T39; match 16's human ran dry at T30 and won).");
            // ---- v0.16.2 (matches 18-19, docs/playtests/2026-11-04-v0.16.1-match19.md) ----
            HumanFirstShare = Config.Bind("Strategy", "HumanFirstShare", 0.9f,
                "v0.16.2: the human is the commit target when his score is at least this share of the best enemy score and HumanFirstMinPassing shots are expected through his defenses. Match 19: Asona saw two human cities and still struck the dying AIs (passing x a leader bonus capped at 1.5). A human who does not lead is still chosen by value. 0 = off.");
            HumanFirstMinPassing = Config.Bind("Strategy", "HumanFirstMinPassing", 4f, "v0.16.2: fewest expected shots through the human's defenses for the human-first commit.");
            HumanFirstReachShare = Config.Bind("Strategy", "HumanFirstReachShare", 0.4f, "v0.18: the human is also the commit when his expected shots through are at least this share of the best target's (match 21: volleys went to dying AIs while 27-28 of 32 would have reached him). 0 = score only.");
            HumanFirstReachMinScore = Config.Bind("Strategy", "HumanFirstReachMinScore", 0.35f, "v0.18 (Terra #1): the reach path also needs the human's score >= this share of the best target's score, so a far more dangerous rival is not ignored.");
            FreeFire = Config.Bind("Strategy", "FreeFire", true,
                "v0.16.2: fire instead of holding when last turn's construction already left at least this turn's firing cost unspent and this turn's energy still covers last turn's use (holding would only add to unspent energy). Match 19 held T21-T25 with 0.8-41k energy unspent a turn.");
            FreeFireMinPassing = Config.Bind("Strategy", "FreeFireMinPassing", 4f, "v0.16.2: fewest expected shots through the commit's defenses for free fire.");
            LateFirePulseWait = Config.Bind("Strategy", "LateFirePulseWait", true,
                "v0.20 (council S1): late free fire also on pulse-wait holds when the energy actually left covers the reservation; the volley is booked like the hold it replaced, so the pulse cadence is unchanged. m25: Donnels T33 held with 8,828 left vs 5,960.");
            AnchorMediumMatch = Config.Bind("Director", "AnchorMediumMatch", true,
                "v0.21 (council I9): a land-only building (solar and nuclear plants, railguns, telescopes) is placed around land anchors when any exist, instead of a random anchor that may be an offshore mine or floating plant (its ring lands in the sea). With no land anchor the game's pool is kept. m28: 73 land placements given up (118 with defenses) while the anchor pools held 15-43 offshore mines.");
            WaterPlantForWater = Config.Bind("Director", "WaterPlantForWater", true,
                "v0.22: when every unpowered building that can take a power line stands on water (offshore mines), the plant for it is a floating plant, placed next to one of them and connected; a land plant could only anchor at sea. m29 T25-T27: 7 land plants given up that way.");
            MaterialBoundEnergyShare = Config.Bind("Director", "MaterialBoundEnergyShare", 0.2f,
                "v0.16.2: material binds when last turn left at least this share of its energy (and MaterialBoundMinEnergy) unspent while its material was spent down to MaterialBoundStockShare. Then extraction goes first and energy gains are worth nothing. 0 = off.");
            MaterialBoundStockShare = Config.Bind("Director", "MaterialBoundStockShare", 0.1f, "v0.16.2: most material left at turn end, as a share of the turn's material, that counts as spent down.");
            MaterialBoundMinEnergy = Config.Bind("Director", "MaterialBoundMinEnergy", 600, "v0.16.2: least unspent energy that can count as left over (two mines' worth).");
            MaterialBoundFromTurn = Config.Bind("Director", "MaterialBoundFromTurn", 6, "v0.16.2: first turn the material-binds rule applies (the opening has its own plant/slot rules).");
            MaterialBoundMinSlots = Config.Bind("Director", "MaterialBoundMinSlots", 4,
                "v0.16.2: with fewer free power slots than this, plants stay buyable while material binds or energy is in glut (a mine needs a slot), and a mine bought by the extraction-first step leaves this many slots for the defenses.");
            ExtractionFirstShare = Config.Bind("Director", "ExtractionFirstShare", 0.35f,
                "v0.16.2: while material binds, up to this share of the turn's material goes to extraction (mines, mine yield upgrades, material projects) before intel, defenses and weapons, while material income is below what the planet sustains to MaterialHorizonTurn. The planet drain guard still applies. 0 = off.");
            if (migrated.Value == "0.14")
            {
                // v0.16 (council v0.16): each unchanged v0.15 default moves once.
                if (Math.Abs(EnergyToMaterialTarget.Value - 0.6f) < 1e-4f) { EnergyToMaterialTarget.Value = 1.0f; Log.LogInfo("EnergyToMaterialTarget: 0.6 -> 1.0 (once)."); }
                if (Math.Abs(MaterialGlutRatio.Value - 3f) < 1e-4f) { MaterialGlutRatio.Value = 1.5f; Log.LogInfo("MaterialGlutRatio: 3 -> 1.5 (once)."); }
                if (WarFromTurn.Value == 20) { WarFromTurn.Value = 16; Log.LogInfo("WarFromTurn: 20 -> 16 (once; gated by WarMinIncome)."); }
                if (AntiNukePriorTurn.Value == 24) { AntiNukePriorTurn.Value = 18; Log.LogInfo("AntiNukePriorTurn: 24 -> 18 (once)."); }
                if (Math.Abs(DefenseReserveShare.Value - 0.3f) < 1e-4f) { DefenseReserveShare.Value = 0.4f; Log.LogInfo("DefenseReserveShare: 0.3 -> 0.4 (once)."); }
                if (MaxIntelBuildings.Value == 30) { MaxIntelBuildings.Value = 21; Log.LogInfo("MaxIntelBuildings: 30 -> 21 (once)."); }
                if (IntelSprintUntilTurn.Value == 14) { IntelSprintUntilTurn.Value = 0; Log.LogInfo("IntelSprintUntilTurn: 14 -> 0 (once; the opening sprint is retired)."); }
                migrated.Value = "0.16";
            }
            if (migrated.Value == "0.16")
            {
                // v0.16.1: IntelBatch is now the maximum of a budget-sized batch.
                if (IntelBatch.Value == 3) { IntelBatch.Value = 8; Log.LogInfo("IntelBatch: 3 -> 8 (once; now the cap of a budget-sized batch)."); }
                migrated.Value = "0.16.1";
            }

            EnableLasers = Config.Bind("Lasers", "EnableLasers", false,
                "EXPERIMENTAL. Let the AI build and fire Laser / EMP Laser / Solar Laser. The game's own AI laser code was switched off by its developers and has bugs (crash on no target, wrong building aimed); this installs fixes for both plus a bypass at end of turn. Needs a game restart. Try it in a throwaway match first.");

            DecisionLog = Config.Bind("Logging", "DecisionLog", true,
                "Write one JSON line per AI decision to BepInEx/InterplanetaryAI/decisions-<start time>.jsonl.");

            // Memory settings are read once; changing them needs a restart (a live change would wipe the match memory).
            Grudges = new GrudgeLedger(Clamp01(GrudgeDecayPerTurn.Value));
            Reach = new ReachabilityMemory(Math.Max(0, ReachabilityMemoryTurns.Value));
            ApplyConfig();
            Config.SettingChanged += (sender, args) => ApplyConfig();
            // v0.17.1 (Terra v0.17.1 #3): read once; a live change could leave an AI half smart, half original mid-match.
            SmartLevelSet = ParseLevels(SmartLevels.Value);

            m_harmony = new Harmony(GUID);
            int patched = 0;
            patched += TryPatch(typeof(AICore), "Initialize", null, "CoreInitializePostfix");
            patched += TryPatch(typeof(AIWeaponTargeting), "BeginTurn", null, "TargetingBeginTurnPostfix");
            patched += TryPatch(typeof(AIWeaponTargetingGoal), "BeginTurn", null, "GoalBeginTurnPostfix");
            // v0.16 (Sol re-check round 2): two overloads exist; patch the trajectory one explicitly.
            patched += TryPatch(typeof(PlayerWeaponHandler), "CompleteFirstPhaseTargeting", "CompleteFirstPhasePrefix", "CompleteFirstPhasePostfix", null,
                new[] { typeof(WeaponGameplayData), typeof(float), typeof(float), typeof(TrajectoryCalculationResults) });
            int holdHook = TryPatch(typeof(AIWeaponTargetingGoal), "TargetWeapons", "GoalTargetWeaponsPrefix", "GoalTargetWeaponsPostfix");
            HoldHookPatched = holdHook == 1; // without it the strategy never holds fire (nothing would stop the shots)
            patched += holdHook;
            patched += TryPatch(typeof(AIWeaponTargeting), "HandleSecondPhaseTargeting", "SecondPhasePrefix", null);
            patched += TryPatch(typeof(PlayerBuildingHandler), "DealDamageToBuilding", "DealDamagePrefix", null);
            patched += TryPatch(typeof(AICore), "BeginTurn", null, "CoreBeginTurnPostfix", typeof(EconomyPatches));
            patched += TryPatch(typeof(AICore), "BeginTurn", "CoreBeginTurnPrefix", null, typeof(DirectorPatches)); // v0.12 dry-mine sales
            patched += TryPatch(typeof(AICore), "EndTurn", "CoreEndTurnPrefix", "CoreEndTurnPostfix", typeof(EconomyPatches)); // v0.20: + prefix (open searches)
            patched += TryPatch(typeof(AIConstruction), "tryConstructNewBuilding", "TryConstructPrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AITechQueue), "BeginTurn", "TechQueueBeginTurnPrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AIDefenseConstruction), "TryConstructDefense", "TryConstructDefensePrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AIBuildingPlacementSearch), "step", "SearchStepPrefix", null, typeof(DirectorPatches));
            patched += TryPatch(typeof(AIBuildingPlacementFinder), "Update", "FinderUpdatePrefix", null, typeof(DirectorPatches));
            // v0.21 (council I1a): one place-search record per Smarter-AI placement search (start, then placed / gave-up / open)
            patched += TryPatch(typeof(AIBuildingPlacementFinder), "StartSearch", null, "StartSearchPostfix", typeof(DirectorPatches));
            patched += TryPatch(typeof(AIBuildingPlacementSearch), "Success", null, "SearchSuccessPostfix", typeof(DirectorPatches));
            patched += TryPatch(typeof(AIBuildingPlacementFinder), "StopAllSearches", "StopAllSearchesPrefix", null, typeof(DirectorPatches));
            patched += TryPatch(typeof(PlayerDefenseHandler), "IsHitIntercepted", "BeforeIntercept", "AfterIntercept", typeof(LedgerPatches));
            // Terra v0.17.1 #1: the laser patches have no owner check (transpiled bans), so they would change the original
            // AIs too; they install only when every level runs the Smarter AI.
            bool everyLevelSmart = SmartLevelSet.Contains(0) && SmartLevelSet.Contains(1) && SmartLevelSet.Contains(2) && SmartLevelSet.Contains(3);
            if (EnableLasers.Value && !everyLevelSmart) Log.LogWarning("Lasers stay off: SmartLevels does not cover every level, and the laser patches would change the original AIs.");
            int laserPatches = EnableLasers.Value && everyLevelSmart ? LaserPatches.Install(new Harmony(GUID + ".lasers")) : 0;
            int guards = TurnGuard.Install(m_harmony); // v0.17
            int aimWatch = AimWatch.Install(m_harmony); // v0.18
            Log.LogInfo(NAME + " " + VERSION + " loaded; " + patched + " of 19 hooks patched; turn guard " + guards + " of 9; aim watch " + aimWatch + " of 1. Director " + (DirectorEnabled.Value ? "on" : "off")
                + ", war from turn " + WarFromTurn.Value + ". Accuracy by level: " + AccuracyByLevel.Value
                + "; research plan " + ResearchPlanIds.Count + " techs; superweapons " + Superweapons.Value
                + "; lasers " + (EnableLasers.Value ? laserPatches + " of 5 patches" : "off"));
        }

        private int TryPatch(Type type, string method, string prefix, string postfix, Type patchClass = null, Type[] args = null)
        {
            if (patchClass == null) patchClass = typeof(AiPatches);
            try
            {
                MethodInfo target = args != null ? AccessTools.Method(type, method, args) : AccessTools.Method(type, method);
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
            StrategyCfg.LeaderInfluence = Math.Max(0f, LeaderInfluence.Value);
            StrategyCfg.BigSalvoShots = Math.Max(StrategyCfg.MinPassingShots, BigSalvoShots.Value);
            StrategyCfg.AbortBelowShare = Clamp01(AbortBelowShare.Value);
            StrategyCfg.AbortBelowWeapons = Math.Max(1, AbortBelowWeapons.Value);
            StrategyCfg.AttritionLossShare = Clamp01(AttritionLossShare.Value);
            StrategyCfg.ReturnFirePassShare = Clamp01(ReturnFirePassShare.Value);
            StrategyCfg.ReturnFireMinPassing = Math.Max(0f, ReturnFireMinPassing.Value);
            StrategyCfg.PrecisionMinGuided = Math.Max(0f, PrecisionMinGuided.Value);
            StrategyCfg.PrecisionMinShare = Clamp01(PrecisionMinShare.Value);
            StrategyCfg.PrecisionKillShare = Math.Max(0f, PrecisionKillShare.Value);
            StrategyCfg.HumanFirstShare = Math.Max(0f, HumanFirstShare.Value);
            StrategyCfg.HumanFirstMinPassing = Math.Max(0f, HumanFirstMinPassing.Value);
            StrategyCfg.HumanFirstReachShare = Math.Max(0f, Math.Min(1f, HumanFirstReachShare.Value));
            StrategyCfg.HumanFirstReachMinScore = Math.Max(0f, Math.Min(1f, HumanFirstReachMinScore.Value));
            StrategyCfg.FreeFire = FreeFire.Value;
            StrategyCfg.FreeFireMinPassing = Math.Max(0f, FreeFireMinPassing.Value);
            StrategyCfg.LateFirePulseWait = LateFirePulseWait.Value;

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

        // v0.17.1 (Pavol, 7 Nov: "our version on the impossible", the original AI on the other levels): the Smarter AI runs
        // only for AI players whose lobby level is in SmartLevels; every other AI runs the game's own code, aim included.
        // One lobby with the Smarter AI on Impossible and the original on Hard / Medium / Easy compares them directly.
        internal static HashSet<int> SmartLevelSet = new HashSet<int> { 3 };

        internal static bool IsSmart(PlayerData player)
        {
            return Active && player != null && player.AIPlayer && SmartLevelSet.Contains(player.AILevel);
        }

        internal static HashSet<int> ParseLevels(string text)
        {
            HashSet<int> set = new HashSet<int>();
            if (string.IsNullOrEmpty(text)) return set;
            foreach (string part in text.Split(','))
            {
                int level;
                if (int.TryParse(part.Trim(), out level)) set.Add(level);
            }
            return set;
        }

        internal static void ResetMatchMemory()
        {
            Grudges.Clear();
            Ledger.Clear();
            Reach.Clear();
            AccuracyCarry.Clear();
            GuidedAccuracyCarry.Clear();
            LastTurnSeen = 0;
            EconomyPatches.Reset();
            StrategyPatches.Reset();
            AiPatches.ResetCombatMemory(); // v0.15.1 (Sol v0.15 #7)
            AimWatch.Reset(); // v0.18
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
