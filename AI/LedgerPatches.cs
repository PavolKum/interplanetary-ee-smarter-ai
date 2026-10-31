using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Interplanetary.GameEngine.Actionphase;
using Interplanetary.GameDataFiles;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;

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

        private static void AfterIntercept(PlayerDefenseHandler __instance, int shooterID, Vector3 worldPositionHit, WeaponData weaponData, List<ActionphaseBaseEvent> events, bool __result, int __state)
        {
            if (!Plugin.Active || events == null || weaponData == null) return;
            try
            {
                PlayerData defender = s_defensePlayer != null ? s_defensePlayer.GetValue(__instance) as PlayerData : null;
                if (defender == null) return;
                int probabilityFails = 0, noResource = 0, disconnected = 0, energySpent = 0;
                for (int i = __state; i < events.Count; i++)
                {
                    ActionphaseDefenseActivationEvent e = events[i] as ActionphaseDefenseActivationEvent;
                    if (e == null) continue;
                    switch (e.Status)
                    {
                        case ActionphaseDefenseActivationEvent.DefenseStatus.FailedProbality: probabilityFails++; energySpent += e.Energy; break;
                        case ActionphaseDefenseActivationEvent.DefenseStatus.Success: energySpent += e.Energy; break;
                        case ActionphaseDefenseActivationEvent.DefenseStatus.FailedNoEnergy: noResource++; break; // energy or material
                        case ActionphaseDefenseActivationEvent.DefenseStatus.FailedDisconneted: disconnected++; break;
                    }
                }
                // No event means no normal defense registered against this weapon was in range (a defense that cannot
                // counter this weapon is not listed): "no eligible defense", not "no defense nearby".
                bool eligibleInRange = events.Count > __state;
                // Where did it land: near one of our cities, and is any defense of ours close by?
                bool nearCity = false, anyDefenseNearby = false;
                foreach (BaseBuilding b in defender.BuildingHandler.Buildings)
                {
                    if (b == null || b.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                    float d = (b.transform.position - worldPositionHit).magnitude;
                    if (b.IsCity) { if (d < b.Radius + 2f) nearCity = true; }
                    else if (b.BuildingData != null && b.BuildingData.defense_functionality.IsValueSet && d < 6.5f) anyDefenseNearby = true;
                }
                Plugin.Ledger.Record(defender.PlayerID, shooterID, weaponData.id.Value, __result, probabilityFails, noResource, disconnected, energySpent, eligibleInRange, nearCity, anyDefenseNearby);
            }
            catch (Exception e)
            {
                if (s_reported.Add("ledger")) Plugin.Log.LogError("Defense ledger hook failed (reported once): " + e);
            }
        }
    }
}
