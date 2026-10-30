using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Trajectories;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;
using Interplanetary.Weapons;

namespace InterplanetaryAI
{
    // Experimental, off by default ([Lasers] EnableLasers). The game ships an AI laser path that its
    // developers switched off (AIWeaponConstruction skips laser types; AIWeaponTargeting skips IsLaser).
    // Turning it on needs the bans lifted plus three fixes to the dormant code:
    //  1. AILaserTrajectoryWeapon.setBuildingsToTry indexes an empty valid-target list -> exception at turn start.
    //  2. AILaserTrajectoryWeapon.Update aims at m_buildingsToTry[m_currentLaserTargetIndex] (planet index used
    //     as a building index) -> wrong building or out of range.
    //  3. AITargetWeapons.CalculateTrajectory simulates a projectile from the laser's never-set shot input.
    // These patches are only installed when the switch is on.
    internal static class LaserPatches
    {
        private const string Unbanned = "type_LaserAllowedBySmarterAI";

        private static readonly FieldInfo s_valid = AccessTools.Field(typeof(AILaserTrajectoryWeapon), "m_validLaserTargets");
        private static readonly FieldInfo s_targetIndex = AccessTools.Field(typeof(AILaserTrajectoryWeapon), "m_currentLaserTargetIndex");
        private static readonly FieldInfo s_buildings = AccessTools.Field(typeof(AILaserTrajectoryWeapon), "m_buildingsToTry");
        private static readonly FieldInfo s_buildingIndex = AccessTools.Field(typeof(AILaserTrajectoryWeapon), "m_currentBuildingIndex");
        private static readonly FieldInfo s_weaponCore = AccessTools.Field(typeof(AITrajectoryWeapon), "m_core");
        private static readonly MethodInfo s_setTargetPlanet = AccessTools.PropertySetter(typeof(AILaserTrajectoryWeapon), "TargetPlanet");
        private static readonly MethodInfo s_setTargetPosition = AccessTools.PropertySetter(typeof(AILaserTrajectoryWeapon), "TargetLocalPosition");
        private static readonly FieldInfo s_toCalculate = AccessTools.Field(typeof(AITargetWeapons), "m_trajectoriesToCalculate");
        private static readonly MethodInfo s_isLaser = AccessTools.PropertyGetter(typeof(WeaponGameplayData), "IsLaser");

        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();

        // ---- lift the bans --------------------------------------------------------------------

        private static IEnumerable<CodeInstruction> UnbanLaserTypesTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                string text = instruction.operand as string;
                if (text == "type_Laser" || text == "type_EMPLaser" || text == "type_SolarLaser")
                {
                    instruction.operand = Unbanned;
                    replaced++;
                }
                yield return instruction;
            }
            Plugin.Log.LogInfo("Lasers: construction ban lifted (" + replaced + " of 3 type checks).");
        }

        private static IEnumerable<CodeInstruction> TargetLasersTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            MethodInfo neverSkip = AccessTools.Method(typeof(LaserPatches), "NeverSkip");
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == System.Reflection.Emit.OpCodes.Callvirt || instruction.opcode == System.Reflection.Emit.OpCodes.Call)
                    && Equals(instruction.operand, s_isLaser))
                {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = neverSkip;
                    replaced++;
                }
                yield return instruction;
            }
            Plugin.Log.LogInfo("Lasers: targeting skip lifted (" + replaced + " of 1 IsLaser checks).");
        }

        public static bool NeverSkip(WeaponGameplayData weapon)
        {
            return false;
        }

        // ---- fix 1: empty valid-target list -------------------------------------------------

        private static bool SetBuildingsToTryPrefix(AILaserTrajectoryWeapon __instance)
        {
            try
            {
                List<LaserValidTarget> targets = s_valid.GetValue(__instance) as List<LaserValidTarget>;
                if (targets == null)
                {
                    targets = new List<LaserValidTarget>();
                    s_valid.SetValue(__instance, targets);
                }
                int index = (int)s_targetIndex.GetValue(__instance);
                List<BaseBuilding> buildings = new List<BaseBuilding>();
                if (index < targets.Count && targets[index].CelestialBody != null && targets[index].CelestialBody.Player != null)
                {
                    buildings = targets[index].CelestialBody.Player.BuildingHandler.Buildings;
                }
                s_buildings.SetValue(__instance, buildings);
                s_buildingIndex.SetValue(__instance, 0);
            }
            catch (Exception e)
            {
                ReportOnce("laser-targets", e);
                s_buildings.SetValue(__instance, new List<BaseBuilding>());
                s_buildingIndex.SetValue(__instance, 0);
            }
            return false;
        }

        // ---- fix 2: one building per frame, aiming at the building actually tested ------------

        private static bool UpdatePrefix(AILaserTrajectoryWeapon __instance)
        {
            try
            {
                if (__instance.HasFoundPrimaryTargetTrajectory || __instance.IsTurnOver) return false;
                List<LaserValidTarget> targets = s_valid.GetValue(__instance) as List<LaserValidTarget>;
                List<BaseBuilding> buildings = s_buildings.GetValue(__instance) as List<BaseBuilding>;
                if (targets == null || buildings == null)
                {
                    __instance.IsTurnOver = true;
                    return false;
                }
                int targetIndex = (int)s_targetIndex.GetValue(__instance);
                int buildingIndex = (int)s_buildingIndex.GetValue(__instance);
                if (targetIndex >= targets.Count)
                {
                    __instance.IsTurnOver = true;
                    return false;
                }
                if (buildingIndex >= buildings.Count)
                {
                    s_targetIndex.SetValue(__instance, targetIndex + 1);
                    if (targetIndex + 1 >= targets.Count) __instance.IsTurnOver = true;
                    else SetBuildingsToTryPrefix(__instance);
                    return false;
                }

                AICore core = (AICore)s_weaponCore.GetValue(__instance);
                CelestialBody planet = targets[targetIndex].CelestialBody;
                BaseBuilding building = buildings[buildingIndex];
                s_buildingIndex.SetValue(__instance, buildingIndex + 1);
                if (planet == null || planet == core.Planet || building == null || building.CurrentState != BaseBuilding.BuildingState.Normal) return false;
                if (!core.PlayerData.IntelHandler.IsBuildingVisible(building)) return false;
                if (!__instance.WeaponTargeting.findLaserTrajectory(__instance, planet, building)) return false;

                bool primary = planet == __instance.TargetEnemy;
                if (primary || !__instance.HasFoundSecondaryTargetTrajectory)
                {
                    s_setTargetPlanet.Invoke(__instance, new object[] { planet });
                    s_setTargetPosition.Invoke(__instance, new object[] { building.transform.localPosition });
                }
                if (primary) __instance.HasFoundPrimaryTargetTrajectory = true;
                else __instance.HasFoundSecondaryTargetTrajectory = true;
            }
            catch (Exception e)
            {
                ReportOnce("laser-update", e);
                __instance.IsTurnOver = true;
            }
            return false;
        }

        // ---- fix 3: lasers skip the projectile simulation at end of turn -----------------------

        private static bool CalculateTrajectoryPrefix(AITargetWeapons __instance, AITrajectoryCalculationInput input)
        {
            if (!(input.Weapon is AILaserTrajectoryWeapon)) return true;
            // targetWeapons() fires lasers from TargetPlanet/TargetLocalPosition and ignores CompletedResults.
            s_toCalculate.SetValue(__instance, (int)s_toCalculate.GetValue(__instance) - 1);
            return false;
        }

        internal static int Install(Harmony harmony)
        {
            int installed = 0;
            installed += Patch(harmony, AccessTools.Method(typeof(AIWeaponConstruction), "TryConstructWeapon"), null, "UnbanLaserTypesTranspiler");
            installed += Patch(harmony, AccessTools.Method(typeof(AIWeaponTargeting), "BeginTurn"), null, "TargetLasersTranspiler");
            installed += Patch(harmony, AccessTools.Method(typeof(AILaserTrajectoryWeapon), "setBuildingsToTry"), "SetBuildingsToTryPrefix", null);
            installed += Patch(harmony, AccessTools.Method(typeof(AILaserTrajectoryWeapon), "Update"), "UpdatePrefix", null);
            installed += Patch(harmony, AccessTools.Method(typeof(AITargetWeapons), "CalculateTrajectory"), "CalculateTrajectoryPrefix", null);
            if (installed < 5)
            {
                Plugin.Log.LogError("Lasers: only " + installed + " of 5 laser patches installed; removing them so the native ban stays intact.");
                harmony.UnpatchSelf(); // dedicated instance: removes only the laser patches
                return 0;
            }
            return installed;
        }

        private static int Patch(Harmony harmony, MethodInfo target, string prefix, string transpiler)
        {
            try
            {
                if (target == null) return 0;
                HarmonyMethod pre = prefix == null ? null : new HarmonyMethod(typeof(LaserPatches), prefix);
                HarmonyMethod trans = transpiler == null ? null : new HarmonyMethod(typeof(LaserPatches), transpiler);
                harmony.Patch(target, prefix: pre, transpiler: trans);
                return 1;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Lasers: patch on " + target + " failed: " + e);
                return 0;
            }
        }

        private static void ReportOnce(string hook, Exception e)
        {
            if (s_reportedErrors.Add(hook))
            {
                Plugin.Log.LogError("Hook '" + hook + "' failed (reported once): " + e);
            }
        }
    }
}
