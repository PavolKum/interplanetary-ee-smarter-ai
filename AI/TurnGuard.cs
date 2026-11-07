using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Evaluation;
using Interplanetary.GameEngine;

namespace InterplanetaryAI
{
    // v0.17 (match 20, docs/playtests/2026-11-07-v0.16.2-match20.md): Junker's AICore.BeginTurn threw inside its module
    // loop every turn from T15 to T24. The modules after the throw never began their turn (nothing was built; 51k
    // material piled up), the mod's postfix never ran, and the game writes no log, so the cause is unknown.
    //
    // A Harmony finalizer on each module's BeginTurn logs the exception (stack trace in LogOutput.log once per module
    // type, a decision record every time) and swallows it, so the remaining modules and the mod's postfix still run.
    // A finalizer on AICore.BeginTurn logs a throw outside the module loop and lets it propagate. Offline and enabled
    // only (Plugin.Active); otherwise the exception passes through untouched.
    internal static class TurnGuard
    {
        private static readonly HashSet<string> s_reported = new HashSet<string>();

        private static readonly Type[] s_modules =
        {
            typeof(AIConstructionEvaluation), typeof(AITechQueue), typeof(AIThreatAssessment), typeof(AIShortTermStrategy),
            typeof(AIPowerGridMaintaining), typeof(AIIntel), typeof(AIConstruction), typeof(AIWeaponTargeting)
        };

        internal static int Install(Harmony harmony)
        {
            int installed = 0;
            HarmonyMethod moduleFinalizer = new HarmonyMethod(typeof(TurnGuard), "ModuleFinalizer");
            foreach (Type type in s_modules)
            {
                try
                {
                    MethodInfo target = AccessTools.DeclaredMethod(type, "BeginTurn");
                    if (target == null)
                    {
                        Plugin.Log.LogWarning("Turn guard: " + type.Name + ".BeginTurn not declared; not guarded.");
                        continue;
                    }
                    harmony.Patch(target, finalizer: moduleFinalizer);
                    installed++;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError("Turn guard: failed to patch " + type.Name + ".BeginTurn: " + e);
                }
            }
            try
            {
                MethodInfo core = AccessTools.DeclaredMethod(typeof(AICore), "BeginTurn");
                if (core != null)
                {
                    harmony.Patch(core, finalizer: new HarmonyMethod(typeof(TurnGuard), "CoreFinalizer"));
                    installed++;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Turn guard: failed to patch AICore.BeginTurn: " + e);
            }
            return installed;
        }

        private static Exception ModuleFinalizer(Exception __exception, object __instance)
        {
            if (__exception == null) return null;
            if (!Plugin.Active) return __exception; // Terra v0.17 H1: online or disabled, the game's behaviour is untouched
            AICore owner = CoreOf(__instance);
            if (owner == null || !Plugin.IsSmart(owner.PlayerData))
            {
                // v0.17.1: an original AI keeps the game's behaviour (logged, not swallowed).
                Report(__instance != null ? __instance.GetType().Name : "module", owner != null && owner.PlayerData != null ? owner.PlayerData.UserNickName : "?", __exception, false);
                return __exception;
            }
            Report(__instance != null ? __instance.GetType().Name : "module", NameOf(__instance), __exception, true);
            // Terra v0.17 H2: the failed module keeps whatever it set before the throw. Accepted: propagating stops every
            // later module (match 20: no construction at all for ten turns), which is worse than one half-begun module.
            return null; // the module loop goes on
        }

        // Terra v0.17 H2: a throw outside the module loop is logged, not swallowed (no proven recovery path there).
        private static Exception CoreFinalizer(Exception __exception, AICore __instance)
        {
            if (__exception == null) return null;
            if (Plugin.Active) Report("AICore", __instance != null && __instance.PlayerData != null ? __instance.PlayerData.UserNickName : "?", __exception, false);
            return __exception;
        }

        private static AICore CoreOf(object module)
        {
            try
            {
                FieldInfo field = AccessTools.Field(typeof(AIModule), "m_core");
                return field != null && module != null ? field.GetValue(module) as AICore : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NameOf(object module)
        {
            try
            {
                FieldInfo field = AccessTools.Field(typeof(AIModule), "m_core");
                AICore core = field != null ? field.GetValue(module) as AICore : null;
                return core != null && core.PlayerData != null ? core.PlayerData.UserNickName : "?";
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static void Report(string where, string ai, Exception e, bool swallowed)
        {
            try
            {
                if (s_reported.Add(where + ":" + ai + ":" + swallowed + ":" + e.GetType().Name)) // Terra v0.17.1 #4
                    Plugin.Log.LogError("Turn guard: " + where + ".BeginTurn threw for " + ai + (swallowed ? " (swallowed so the turn goes on" : " (passed on to the game: original AI or outside the module loop") + "; reported once): " + e);
                string message = e.GetType().Name + ": " + e.Message;
                if (message.Length > 300) message = message.Substring(0, 300);
                Plugin.WriteDecision(new Json().Str("e", "turn-start-error").Int("t", GameManager.CurrentTurn).Str("ai", ai)
                    .Str("module", where).Bool("swallowed", swallowed).Str("error", message).ToString());
            }
            catch (Exception)
            {
                // never throw from a finalizer
            }
        }
    }
}
