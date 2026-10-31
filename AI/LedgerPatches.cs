using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.GameEngine.Actionphase;
using Interplanetary.GameDataFiles;
using Interplanetary.PlayerDataManagement;

namespace InterplanetaryAI
{
    // Records, for every shot that reaches a planet, what the defender's defenses did with it
    // (PlayerDefenseHandler.IsHitIntercepted adds one ActionphaseDefenseActivationEvent per defense in range).
    // Both players see these events in the action phase; the mod only reads them, for the AI as defender (how much
    // energy its defenses really used) and as shooter (how many of its shots were intercepted, per weapon).
    internal static class LedgerPatches
    {
        private static readonly FieldInfo s_defensePlayer = AccessTools.Field(typeof(PlayerDefenseHandler), "m_player");
        private static readonly HashSet<string> s_reported = new HashSet<string>();

        private static void BeforeIntercept(List<ActionphaseBaseEvent> events, out int __state)
        {
            __state = events != null ? events.Count : 0;
        }

        private static void AfterIntercept(PlayerDefenseHandler __instance, int shooterID, WeaponData weaponData, List<ActionphaseBaseEvent> events, bool __result, int __state)
        {
            if (!Plugin.Active || events == null || weaponData == null) return;
            try
            {
                PlayerData defender = s_defensePlayer != null ? s_defensePlayer.GetValue(__instance) as PlayerData : null;
                if (defender == null) return;
                int probabilityFails = 0, noEnergy = 0, disconnected = 0, energySpent = 0;
                for (int i = __state; i < events.Count; i++)
                {
                    ActionphaseDefenseActivationEvent e = events[i] as ActionphaseDefenseActivationEvent;
                    if (e == null) continue;
                    switch (e.Status)
                    {
                        case ActionphaseDefenseActivationEvent.DefenseStatus.FailedProbality: probabilityFails++; energySpent += e.Energy; break;
                        case ActionphaseDefenseActivationEvent.DefenseStatus.Success: energySpent += e.Energy; break;
                        case ActionphaseDefenseActivationEvent.DefenseStatus.FailedNoEnergy: noEnergy++; break;
                        case ActionphaseDefenseActivationEvent.DefenseStatus.FailedDisconneted: disconnected++; break;
                    }
                }
                bool covered = events.Count > __state;
                Plugin.Ledger.Record(defender.PlayerID, shooterID, weaponData.id.Value, __result, probabilityFails, noEnergy, disconnected, energySpent, covered);
            }
            catch (Exception e)
            {
                if (s_reported.Add("ledger")) Plugin.Log.LogError("Defense ledger hook failed (reported once): " + e);
            }
        }
    }
}
