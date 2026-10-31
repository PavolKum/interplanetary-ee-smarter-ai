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
        public const string VERSION = "0.2.2";

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

        internal static readonly BuildWeights Build = new BuildWeights();
        internal static List<string> SuperweaponIds = new List<string>();
        internal static List<string> ResearchPlanIds = new List<string>();

        // Human consensus opening (first 10), Nuclear Missile line, rest of the human order, Asteroid Diversion line.
        private const string DefaultResearchPlan =
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
            GrudgeWeight = Config.Bind("Targeting", "GrudgeWeight", 0.6f,
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
                "Tech IDs in order. Default: the human's consensus opening from 6 recorded wins (AI/tools/human_research_order.py), then the Nuclear Missile line pulled forward (the AIs make ~40% of the human's science and would never reach it at position 33), then the rest of the human's order, then the Asteroid Diversion line.");

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
            patched += TryPatch(typeof(AIWeaponTargetingGoal), "TargetWeapons", "GoalTargetWeaponsPrefix", "GoalTargetWeaponsPostfix");
            patched += TryPatch(typeof(AIWeaponTargeting), "HandleSecondPhaseTargeting", "SecondPhasePrefix", null);
            patched += TryPatch(typeof(PlayerBuildingHandler), "DealDamageToBuilding", "DealDamagePrefix", null);
            patched += TryPatch(typeof(AICore), "BeginTurn", null, "CoreBeginTurnPostfix", typeof(EconomyPatches));
            patched += TryPatch(typeof(AICore), "EndTurn", null, "CoreEndTurnPostfix", typeof(EconomyPatches));
            patched += TryPatch(typeof(AIConstruction), "tryConstructNewBuilding", "TryConstructPrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AITechQueue), "BeginTurn", "TechQueueBeginTurnPrefix", null, typeof(EconomyPatches));
            patched += TryPatch(typeof(AIDefenseConstruction), "TryConstructDefense", "TryConstructDefensePrefix", null, typeof(EconomyPatches));
            int laserPatches = EnableLasers.Value ? LaserPatches.Install(new Harmony(GUID + ".lasers")) : 0;
            Log.LogInfo(NAME + " " + VERSION + " loaded; " + patched + " of 11 hooks patched. Accuracy by level: " + AccuracyByLevel.Value
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
            Reach.Clear();
            AccuracyCarry.Clear();
            LastTurnSeen = 0;
            EconomyPatches.Reset();
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
