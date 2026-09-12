## (1) DESIGN ERRORS

`plan` denotes `docs/plan-qol-v0.1.md`; C# citations use basenames under `decompiled/`.

- **“Local” includes enemy AI.** “Compute `localFired = any shoot.PlayerNetworkID or laser.PlayerId in {p.PlayerID | p.LocalPlayer}`.” (plan:51). Default players, including AI, receive `LocalPlayer = true`; the game explicitly excludes AI when counting local humans. Enemy shots would therefore suppress automatic acceleration. **Correction:** use `LocalPlayer && !AIPlayer`, and define whose shots matter in hotseat. (`GameSessionManager.cs:214–232`; `PlayerManager.cs:283–293`)

- **The online barrier is backwards.** “results are precomputed and each client only signals ‘ready for results’ after playback, so faster local playback is harmless, but v0.1 defaults to `OfflineOnly = true` (skip when `GameManager.OnlineGame`) because it was not tested.” (plan:55). Readiness for results precedes simulation; another readiness exchange precedes playback. Cleanup advances locally to `RoundStart`. **Correction:** remove the harmlessness claim; keep acceleration strictly offline. (`PlayerManager.cs:178–185`; `OnlineGameRoomMessageHandler.cs:581–669`; `ActionphaseManager.cs:729–744`)

- **Projectile movement is elsewhere.** “Physics: the game moves projectiles by frame index in `ProcessEvent(frame)`, not by physics integration, so scaling fixed steps does not change outcomes.” (plan:56). Shoot-event processing merely starts the projectile; projectile components advance their own frame counters in separate `FixedUpdate` calls. **Correction:** distinguish predetermined outcomes from independently updated visuals. (`ActionphaseProjectileShootEvent.cs:247–257`; `NuclearMissileProjectile.cs:52–68`)

- **Calling the method bypasses button protections.** “Fast-forward is offline-only by default; End Turn hotkey calls the exact same method as the button, so it cannot produce a state the button cannot.” (plan:83). Targeting disables the button, but `Button_EndTurn` never checks button interactability, visibility, input state or modal state. **Correction:** reproduce those availability checks before calling it. (`EndturnOverlay.cs:150–159,201–223,238–286`)

- **Timing calculations are wrong.** “With 8x: 1 s countdown + 6.5 s minimum -> ~0.95 s wall clock.” (plan:52). Nonzero turns add another 30 frames: minimum playback is 355 frames plus countdown/state-transition ticks, approximately 1.02 seconds under the assumed 0.02-second step. Also, the plan’s 144-fps estimate is **2.78**, not 1.1, fixed ticks/rendered frame: `8/(0.02×144)`. (`ActionphaseManager.cs:64,154–161,263–266,624–639,651–738`; plan:16,56)

## (2) RISKS THE PLAN MISSES

Ranked by estimated likelihood × impact, each scored 1–3.

1. **3×2=6 — Build/package failure.** The proposed typed `TMP_InputField` guard lacks a TextMeshPro assembly reference. The game references `TextMeshPro-1.0.55.56.0b10.dll` separately. Deployment also copies only the DLL, not the promised localization TSV. Add the matching reference and explicitly package or embed defaults. (`QoL/QoL.csproj:24–58`; `Assembly-CSharp.csproj:21–23`; plan:69,82)

2. **2×3=6 — Stuck global speed and teardown exceptions.** An event exception can leave status at `DisplayActionphase`, making both status-based resets ineffective. Scene destruction instead nulls `GameManager.m_instance`; polling `CurrentManagerStatus` then throws. A null-results start also returns without entering playback. Require successful-start/lifetime guards, exception recovery, and restoration independent of manager availability. Restore the captured prior scale, not unconditionally 1. (`ActionphaseManager.cs:603–607,669–709`; `GameManager.cs:67,208–219`; plan:54,81)

3. **2×3=6 — One press performs multiple actions.** If Enter maps to both routes, raw polling plus the Rewired postfix can consume a warning and proceed during one key-down. Hotseat OK immediately restores `ReadInput`; chat deselection restores `"Gameplay"`, permitting same-frame context fall-through. Use one dispatcher and a release/context-transition latch. (`plan:60–64`; `EndturnOverlay.cs:244–286`; `TurnPhasesOverlay.cs:63–68`; `ChatOverlay.cs:317–330`)

4. **2×2=4 — Patch/bootstrap failure.** Do not copy pseudoparameter names into Harmony patches: originals use `newGameStatus` and `localizationStringKey`; use those names or indexed argument annotations. `StartActionphase` is static. Empty-plugin loading does not validate Mono detours: installed Harmony documents Mono IL-compilation failures, and the preloader separately patches UnityEngine’s entrypoint. Require an actual patch smoke test; execution is unavailable in this read-only review. (`GameManager.cs:222`; `LocalizationManager.cs:182`; `ActionphaseManager.cs:600`; `BepInEx/core/0Harmony.xml:794–828,1208–1209`; `BepInEx/core/BepInEx.Preloader.xml:147–149`; plan:79,89)

5. **3×1=3 — Audio gets truncated, not merely accelerated visually.** Explosion cleanup waits scaled seconds and then destroys its attached sound component. Speeding that cleanup can cut sound short; an upper speed cap does not preserve audio duration. (`BaseExplosion.cs:37–42,59–67`)

6. **1×3=3 — Online turn-state corruption if enabled.** A fast client can enter and finish its next turn while another still plays the previous action phase. Incoming end-turn messages immediately change player state and invoke turn switching, without a playback guard. Apply offline eligibility to **every** speed-writing path, including hold-to-speed; the hold specification currently lists only phase status. (`OnlineGameRoomMessageHandler.cs:536–554`; `PlayerManager.cs:829–834`; plan:53,55)

## (3) SIMPLER ALTERNATIVES

- **Drop the “bonus” Rewired patch.** One configurable input route achieves End Turn without maintaining two dispatch mechanisms. Keep a single availability predicate and mutually exclusive hotseat/End Turn handling. (`plan:60–64`; `EndturnOverlay.cs:238–286`)
- **For one rename, use one configured string and an exact-key prefix.** This removes TSV deployment/parsing and avoids executing the original lookup for overridden keys. That lookup indexes mod-language dictionaries without checking language existence, so a postfix cannot rescue an exception there. (`plan:40,69`; `LocalizationManager.cs:182–204`; `QoL/QoL.csproj:55–58`)

## (4) ANSWERS

1. **Prefer `timeScale`, not repeated `playActionphase` calls. High confidence.** Repetition advances event frames without equivalently advancing projectile `FixedUpdate`, laser `Update`, or delayed pool returns. Do not scale `fixedDeltaTime`: it also feeds trajectory calculations. (`NuclearMissileProjectile.cs:52–68`; `LaserProjectile.cs:47–60`; `BaseProjectile.cs:151–155`; `TrajectoryCalculator.cs:13–26`)

2. **Yes for normal cleanup; no as a complete recovery mechanism.** The prefix restores before `ChangeGameManagerStatus` emits view notifications and before `Ended`. It cannot cover an exception that prevents cleanup or manager destruction. (`ActionphaseManager.cs:729–744`; `GameManager.cs:208–230`)

3. **Yes, popup holes exist.** Message boxes do not change input state; building-info opening likewise leaves it unchanged. Generic popups merely activate content/background, whereas the pause menu explicitly switches to `"Menu"`. Distinguish intentional nonmodal warnings from blockers. Do **not** block whenever `MessageOverlay` is open—it is opened during game-manager initialization. (`MessageOverlay.cs:79–104`; `BuildingInfoPopup.cs:129–144`; `PopupOverlay.cs:107–130`; `PausemenuScreen.cs:74–83`; `GameManager.cs:155`)

## (5) VERDICT

**GO-WITH-CHANGES — High confidence in the source findings, not runtime compatibility.**

Before implementation:

- Correct human-shot classification and enforce offline acceleration everywhere. (`GameSessionManager.cs:218–231`; plan:53–55)
- Specify speed ownership, teardown and exception recovery. (`GameManager.cs:67,208–219`; plan:54)
- Replace dual hotkey dispatch with explicit button/modal/context guards. (`EndturnOverlay.cs:150–159,238–286`; plan:60–64)
- Fix TMP/default-data packaging and require old-Mono bootstrap **and detour** testing before deployment approval. (`QoL/QoL.csproj:24–58`; plan:79,89)
