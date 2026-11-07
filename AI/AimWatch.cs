using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Trajectories;
using Interplanetary.GameEngine;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;
using Interplanetary.Weapons;
using InterplanetaryAI.Brain;
using UnityEngine;

namespace InterplanetaryAI
{
    // v0.18 aim time (match 21): watches a Smarter AI's trajectory search every frame (postfix on AICore.Update) and, on a
    // firing turn, pushes the AI's timeout out while the search is still finding trajectories (Brain AimWindow.Extend).
    // At the end of the turn it logs an "aim-search" record: weapons searching / with a trajectory at 20 s, 40 s and at the
    // end, search seconds and frames (frame rate), seconds added, why it stopped, and for the weapons left without a
    // trajectory how far they face away from their target (angle between the weapon's outward normal and the direction
    // to the target planet): the evidence for v0.18 weapon placement.
    internal static class AimWatch
    {
        private static readonly FieldInfo s_timer = AccessTools.Field(typeof(AICore), "m_timeOutTimer");
        private static readonly FieldInfo s_timeOut = AccessTools.Field(typeof(AICore), "m_timeOut");
        private static readonly FieldInfo s_targeting = AccessTools.Field(typeof(AICore), "m_weaponTargeting");
        private static readonly FieldInfo s_targetables = AccessTools.Field(typeof(AIWeaponTargeting), "m_targetableWeapons");

        private sealed class Watch
        {
            public int Turn = -1;
            public int LastFound = -1;
            public float LastProgress;
            public int Frames;
            public int Live;
            public int Found;
            public int FoundAt20 = -1, FoundAt40 = -1;
            public float LastTimer;
            public float AddedSeconds;
            public float BaseTimeout = -1f;
            public string Stop = "";
            public bool Fire;
            public bool Logged;
            public bool Done;
        }

        private static readonly Dictionary<int, Watch> s_watch = new Dictionary<int, Watch>();

        internal static void Reset()
        {
            s_watch.Clear();
        }

        internal static int Install(Harmony harmony)
        {
            try
            {
                MethodInfo target = AccessTools.DeclaredMethod(typeof(AICore), "Update");
                if (target == null || s_timer == null || s_timeOut == null || s_targeting == null || s_targetables == null)
                {
                    Plugin.Log.LogWarning("Aim watch: a game member is missing; the aim window stays fixed.");
                    return 0;
                }
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(AimWatch), "CoreUpdatePrefix"), postfix: new HarmonyMethod(typeof(AimWatch), "CoreUpdatePostfix"));
                return 1;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Aim watch: failed to patch AICore.Update: " + e);
                return 0;
            }
        }

        // Terra v0.18 #3: extend only on an explicit firing decision for this turn (ClearDecision runs at every turn start).
        private static bool FiringTurn(int playerId, out int commitId)
        {
            StrategyDecision d = StrategyPatches.Latest(playerId);
            commitId = d != null ? d.CommitTargetId : -1;
            return d != null && d.Fire && commitId >= 0;
        }

        private static Watch WatchFor(AICore core)
        {
            int id = core.PlayerData.PlayerID;
            int turn = GameManager.CurrentTurn;
            Watch w;
            if (!s_watch.TryGetValue(id, out w) || w.Turn != turn)
            {
                w = new Watch { Turn = turn };
                s_watch[id] = w;
                int commit;
                // Terra v0.18 #3: a turn without a firing decision must not inherit last turn's extended timeout.
                if (!FiringTurn(id, out commit) && (float)s_timeOut.GetValue(core) > StrategyPatches.NativeAimWindow)
                    s_timeOut.SetValue(core, StrategyPatches.NativeAimWindow);
            }
            return w;
        }

        // Terra v0.18 #5: extend before the native timeout check, from the previous frame's counts and the timer this frame
        // will reach, so a long frame cannot jump past the window unextended.
        private static void CoreUpdatePrefix(AICore __instance)
        {
            try
            {
                if (__instance == null || !Plugin.IsSmart(__instance.PlayerData) || !Plugin.AimExtend.Value) return;
                if (__instance.Status != AIStatus.TurnOngoing || __instance.IsReady) return;
                Watch w = WatchFor(__instance);
                int commit;
                if (!FiringTurn(__instance.PlayerData.PlayerID, out commit) || w.Live <= 0) return;
                float timer = (float)s_timer.GetValue(__instance) + Time.deltaTime;
                float timeout = (float)s_timeOut.GetValue(__instance);
                if (w.BaseTimeout < 0f) w.BaseTimeout = timeout;
                float next = AimWindow.Extend(timer, timeout, w.LastProgress, w.Found, w.Live, Plugin.AimTargetShare.Value,
                    Plugin.AimStallSeconds.Value, Plugin.AimingWindowMax.Value, 2f);
                if (next > timeout)
                {
                    w.AddedSeconds += next - timeout;
                    s_timeOut.SetValue(__instance, next);
                }
            }
            catch (Exception e)
            {
                ReportOnce("aim-extend", e);
            }
        }

        private static void CoreUpdatePostfix(AICore __instance)
        {
            try
            {
                if (__instance == null || !Plugin.IsSmart(__instance.PlayerData)) return;
                if (__instance.Status != AIStatus.TurnOngoing) return;
                Watch w = WatchFor(__instance);
                if (w.Done) return;
                float timer = (float)s_timer.GetValue(__instance);
                float timeout = (float)s_timeOut.GetValue(__instance);
                if (w.BaseTimeout < 0f) w.BaseTimeout = timeout;
                w.Frames++;
                w.LastTimer = timer;
                int commit;
                w.Fire = FiringTurn(__instance.PlayerData.PlayerID, out commit);

                // Terra v0.18 #2: the weapons the firing hook can use (normal, powered) aimed at the commit; without a
                // commit, every searching weapon.
                int live = 0, found = 0;
                AIWeaponTargeting targeting = s_targeting.GetValue(__instance) as AIWeaponTargeting;
                List<AITargetableWeapon> weapons = targeting != null ? s_targetables.GetValue(targeting) as List<AITargetableWeapon> : null;
                if (weapons != null)
                {
                    foreach (AITargetableWeapon tw in weapons)
                    {
                        if (!Usable(tw)) continue;
                        CelestialBody target = tw.TrajectoryWeapon.TargetEnemy;
                        if (commit >= 0 && (target == null || target.Player == null || target.Player.PlayerID != commit)) continue;
                        live++;
                        if (tw.TrajectoryWeapon.HasFoundPrimaryTargetTrajectory) found++;
                    }
                }
                w.Live = live;
                if (found > w.LastFound)
                {
                    w.LastFound = found;
                    w.LastProgress = timer;
                }
                w.Found = found;
                if (w.FoundAt20 < 0 && timer >= 20f) w.FoundAt20 = found;
                if (w.FoundAt40 < 0 && timer >= 40f) w.FoundAt40 = found;

                // Terra v0.18 #4: the frame on which the AI becomes ready is counted; say why it stopped.
                if (__instance.IsReady)
                {
                    w.Done = true;
                    int needed = (int)Math.Ceiling(Math.Max(0f, Math.Min(1f, Plugin.AimTargetShare.Value)) * live);
                    if (live > 0 && found >= live) w.Stop = "all aimed";
                    else if (timer < timeout) w.Stop = "native ready";
                    else if (!w.Fire) w.Stop = "hold window";
                    else if (found >= needed) w.Stop = "enough";
                    else if (timer >= Plugin.AimingWindowMax.Value) w.Stop = "cap";
                    else if (timer - w.LastProgress >= Plugin.AimStallSeconds.Value) w.Stop = "stall budget";
                    else w.Stop = "window";
                }
            }
            catch (Exception e)
            {
                ReportOnce("aim-watch", e);
            }
        }

        private static bool Usable(AITargetableWeapon tw)
        {
            if (tw == null || tw.Weapon == null || tw.Weapon.IsLaser || tw.TrajectoryWeapon == null) return false;
            BaseBuilding b = tw.Weapon.Building;
            return b != null && b.CurrentState == BaseBuilding.BuildingState.Normal && b.Power != null && b.Power.HasPower;
        }

        // Called from the end of a Smarter AI's turn (EconomyPatches.CoreEndTurnPostfix).
        internal static void Log(AICore core)
        {
            try
            {
                Watch w;
                if (core == null || !s_watch.TryGetValue(core.PlayerData.PlayerID, out w) || w.Turn != GameManager.CurrentTurn || w.Logged) return;
                w.Logged = true;
                int unfound = 0, facingAway = 0;
                float unfoundAngle = 0f, foundAngle = 0f;
                int foundN = 0;
                AIWeaponTargeting targeting = s_targeting.GetValue(core) as AIWeaponTargeting;
                List<AITargetableWeapon> weapons = targeting != null ? s_targetables.GetValue(targeting) as List<AITargetableWeapon> : null;
                Vector3 own = core.Planet.transform.position;
                if (weapons != null)
                {
                    foreach (AITargetableWeapon tw in weapons)
                    {
                        if (!Usable(tw)) continue;
                        CelestialBody target = tw.TrajectoryWeapon.TargetEnemy;
                        if (target == null) continue;
                        Vector2 pos = tw.Weapon.WeaponShootingPosition;
                        Vector2 normal = new Vector2(pos.x - own.x, pos.y - own.z);
                        Vector2 toTarget = new Vector2(target.transform.position.x - own.x, target.transform.position.z - own.z);
                        float angle = Vector2.Angle(normal, toTarget);
                        if (tw.TrajectoryWeapon.HasFoundPrimaryTargetTrajectory) { foundN++; foundAngle += angle; }
                        else
                        {
                            unfound++;
                            unfoundAngle += angle;
                            if (angle > 90f) facingAway++;
                        }
                    }
                }
                Plugin.WriteDecision(new Json().Str("e", "aim-search").Int("t", w.Turn).Str("ai", core.PlayerData.UserNickName)
                    .Bool("fire", w.Fire).Int("live", w.Live).Int("found", w.Found).Int("foundAt20", w.FoundAt20).Int("foundAt40", w.FoundAt40)
                    .Num("seconds", w.LastTimer).Int("frames", w.Frames).Num("fps", w.LastTimer > 0f ? w.Frames / w.LastTimer : 0f)
                    .Num("baseWindow", w.BaseTimeout).Num("addedSeconds", w.AddedSeconds).Num("lastProgress", w.LastProgress).Str("stop", w.Stop)
                    .Num("foundFacingDeg", foundN > 0 ? foundAngle / foundN : -1f).Num("unfoundFacingDeg", unfound > 0 ? unfoundAngle / unfound : -1f)
                    .Int("unfoundFacingAway", facingAway).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("aim-search-log", e);
            }
        }

        private static readonly HashSet<string> s_reported = new HashSet<string>();

        private static void ReportOnce(string what, Exception e)
        {
            if (s_reported.Add(what)) Plugin.Log.LogError("Aim watch '" + what + "' failed (reported once): " + e);
        }
    }
}
