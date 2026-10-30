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
        public const string VERSION = "0.1.0";

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
        internal static ConfigEntry<bool> AccuracyOverride;
        internal static ConfigEntry<string> AccuracyByLevel;
        internal static ConfigEntry<bool> SmartStrikes;
        internal static ConfigEntry<float> WeaponBuildingShare;
        internal static ConfigEntry<bool> DecisionLog;

        internal static readonly PlannerSettings Planner = new PlannerSettings();
        internal static float[] Accuracy = new float[] { 0.6f, 0.95f, 1f, 1f };
        internal static GrudgeLedger Grudges;
        internal static ReachabilityMemory Reach;
        internal static readonly Dictionary<int, float> AccuracyCarry = new Dictionary<int, float>();
        internal static readonly System.Random Rng = new System.Random();

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
                "Weight of how little visible city HP an enemy has left (finish off the weak).");
            HumanBias = Config.Bind("Targeting", "HumanBias", 1.0f,
                "Multiplier on human enemies' score. 1 = no favouritism; above 1 = AIs prefer the human.");
            GrudgeDecayPerTurn = Config.Bind("Targeting", "GrudgeDecayPerTurn", 0.7f,
                "How much remembered damage survives each turn (0-1).");
            ReachabilityMemoryTurns = Config.Bind("Targeting", "ReachabilityMemoryTurns", 2,
                "A weapon that found no trajectory onto a target is steered elsewhere for this many turns.");

            AccuracyOverride = Config.Bind("Accuracy", "Override", true,
                "Native AIs plan deliberate misses each turn (accuracy 0.4 / 0.75 / 0.9 / 1.0 by difficulty). Override that plan.");
            AccuracyByLevel = Config.Bind("Accuracy", "AccuracyByLevel", "0.6,0.95,1.0,1.0",
                "Planned hit share per difficulty level, lowest first, comma-separated.");

            SmartStrikes = Config.Bind("Strikes", "SmartStrikes", true,
                "Guided second-phase strikes go for the visible city closest to destruction instead of a random visible building.");
            WeaponBuildingShare = Config.Bind("Strikes", "WeaponBuildingShare", 0.25f,
                "Chance a guided strike goes for the weakest visible weapon building instead of a city.");

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
            Log.LogInfo(NAME + " " + VERSION + " loaded; " + patched + " of 6 hooks patched. Accuracy by level: " + AccuracyByLevel.Value);
        }

        private int TryPatch(Type type, string method, string prefix, string postfix)
        {
            try
            {
                MethodInfo target = AccessTools.Method(type, method);
                if (target == null)
                {
                    Log.LogError("Hook target not found: " + type.Name + "." + method + " - that behaviour stays native.");
                    return 0;
                }
                HarmonyMethod pre = prefix == null ? null : new HarmonyMethod(typeof(AiPatches), prefix);
                HarmonyMethod post = postfix == null ? null : new HarmonyMethod(typeof(AiPatches), postfix);
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
