# Interplanetary Enhanced Edition - QoL mod, v0.1 design

Date: 2026-09-12. Author: Claude (Fable 5.1) for Pavol. Status: DRAFT, awaiting adversarial review before implementation.

## 0. Facts established from the game files (read-only survey)

- Game: Unity **5.6.7f1**, 64-bit, **Mono** scripting backend, **.NET 3.5 profile** (`mscorlib` 2.0). Not IL2CPP. Game code in `Interplanetary_Data/Managed/Assembly-CSharp.dll` (2.0 MB, 1271 types), input via **Rewired**, UI via TextMeshPro + uGUI.
- Official modding (`ModdingGuide.rtf`, `ModBase.zip`) is **data-only**: CSV rulesets under `PlayerData/<steamid>/Sets/<name>/` selected at game setup, plus a `Localizations.tsv` that is only loaded as part of a ruleset (`LocalizationManager.LoadModLocalization`). No code hooks, no keybinding file (`PlayerSettings.cfg` has no key section; Rewired maps live inside the scene assets).
- Decompiled source (ILSpy 9.1) is in `decompiled/` next to this doc. All file:line references below point there.
- **BepInEx 5.4.23.3 x64** has been unpacked into the game root (`winhttp.dll`, `doorstop_config.ini`, `BepInEx/`). It has not run yet (game was running during install; it activates on next launch). Uninstall = delete `winhttp.dll`; no game file is modified.
- Build toolchain: .NET SDK 8.0.425 (installed today via winget) + `Microsoft.NETFramework.ReferenceAssemblies.net35` to target net35. Project skeleton at `QoL/QoL.csproj`; it references `BepInEx/core/{BepInEx,0Harmony}.dll` and the game's `Managed/` assemblies with `Private=false`, and copies the built DLL into `BepInEx/plugins/InterplanetaryQoL/`.

### 0.1 Action phase mechanics (`decompiled/Interplanetary.GameEngine/ActionphaseManager.cs`)

- The action phase is **not simulated live**. `StartActionphase(ActionphaseResultsMessage)` (line 600) deserializes precomputed event lists (shoot / hit / explosion / split / lock-on / defense / fallout / building damage / player defeat / `WeaponShotAndHit` for lasers) and sets `m_currentActionphaseStatus = Playing`.
- Playback is driven by `FixedUpdate()` (line 139) calling `playActionphase()` (line 651), which advances **one frame per FixedUpdate tick**: `m_currentActionphaseFrame++` until `m_actionphaseEndingFrame`. `m_minActionphaseLenghtAsFrames = 325` (line 64). At Unity's default fixed step 0.02 s that is a **6.5 s minimum** even when nothing was fired, plus a 1 s start countdown (`m_actionphaseStartDelay = 1f`, line 632) that is decremented with `Time.deltaTime` inside FixedUpdate.
- `Cleanup` (line 727) resets status to Idle, calls `GameManager.ChangeGameManagerStatus(RoundStart)` and posts `ActionPhaseStateMessage.Ended`.
- The devs' own debug cheat (`GameManager.Update`, lines 289-307, gated on `Debug.isDebugBuild`, so dead in the shipped build) speeds the game with **`Time.timeScale` = 1/2/4/10 on Keypad1-4**. So `Time.timeScale` is a mechanism the game was designed to tolerate. No other code in the assembly touches `timeScale` (grep confirmed).
- Which player fired: `ActionphaseProjectileShootEvent.PlayerNetworkID` is an `int` set from `Weapon.PlayerData.PlayerID` (`ActionphaseProjectileShootEvent.cs:49`); `ActionphaseWeaponShotAndHitEvent.PlayerId` likewise. `PlayerData` has `int PlayerID` and `bool LocalPlayer` (`Interplanetary.PlayerDataManagement/PlayerData.cs:65,~105`); `PlayerManager.PlayerList` is public static.
- Game status enum: `None, WaitNetworkPlayers, RoundStart, RoundSync, PlayerTurn, WaitingPlayers, WaitingAIExecution, WaitingPlayerData, SyncPlayerData, WaitingActionphase, DisplayActionphase, GameHasEnded` (`GameManager.cs:23`). `GameManager.CurrentManagerStatus`, `GameManager.OnlineGame` are public static.

### 0.2 Input (`decompiled/Interplanetary.InputManagement/GameplayInputManager.cs`, `Interplanetary.Screens/EndturnOverlay.cs`)

- Rewired actions that exist: `Gameplay Select/Cancel/Endturn/Overview/Buildview/Intelview/Targetingview/Techtreeview/Nextview/Previousview`, `Open Menu`, `Toggle Defense Visual`, `Open Chat`, `Toggle Chat`, camera axes.
- View switching is already handled by `ViewcontrolsOverlay.userInputMessage` (line 106), so the view hotkeys presumably work today.
- **`Gameplay Endturn` is dead code**: `EndturnOverlay.userInputMessage` (line 138) tests `GetButtonDown("Gameplay Endturn")` and then does nothing (empty `if` body). Whether that action even has a keyboard binding in the Rewired asset is unknown.
- Engine input gate: `TJR_Engine.InputManagement.InputManager.CurrentInputStatus` (`BlockInput | ReadInput`) and `CurrentInputActionState` (`"Gameplay" | "Menu" | "Chat"`), both public static.
- `EndturnOverlay.Button_EndTurn()` (line 238) is public. It shows warning popups first (not-finished-targeting, no-tech-selected) via `OpenMessageBoxMessage` and only ends the turn on the next press; it also checks `GameManager.CurrentManagerStatus == PlayerTurn` (offline) or `!OnlinePointOfNoReturn` (online). `ScreenManager.GetScreenReference("EndturnOverlay")` returns the instance (`TJR_Engine.ScreenManagement/ScreenManager.cs:149`).
- `TurnPhasesOverlay.Button_OK()` (hotseat "next player" dialog, blocks input until clicked) is public.

### 0.3 Localization (`decompiled/TJR_Engine.LocalizationManagement/LocalizationManager.cs`)

- `public static string GetLocalizedString(string key)` (line 182): mod dictionaries first (only populated by a ruleset), then the official dictionary for the current language, else `string.Empty`.
- Tech names are looked up as `"ui_technology_name_" + tech.id` (TechtreeOverlay.cs:191, ResourcesOverlay.cs:439, CodexScreen.cs:102). The tech id is **`tec_QuantumComputing`** (from `ModBase.zip` TechTreeData.csv), so the key is **`ui_technology_name_tec_QuantumComputing`**. The description key naming was not found in code yet; the plugin will log every official key containing `QuantumComputing` on first run so the description can be overridden too.

## 1. Requirements (Pavol, 2026-09-12, verbatim intent)

1. "if I don't target or shoot in that round the action phase would be super super fast. Right? So maybe like three seconds and just give me the result And let's continue with the build phase."
2. "lack of keyboard shortcuts for repeated actions" - primary candidate: End Turn (no hotkey today). Others TBD after asking which actions he repeats.
3. "rename Tech 'Quantum Computing' to 'AGI'".
4. "monthly debt" - meaning unclear; nothing in the assembly's strings mentions debt/loan/interest. **Open question for Pavol**, not designed here.

## 2. Design

One BepInEx 5 plugin, `InterplanetaryQoL.dll`, `[BepInPlugin("pavol.interplanetary.qol", "Interplanetary QoL", "0.1.0")]`, HarmonyX patches created with `Harmony.CreateAndPatchAll(typeof(Plugin).Assembly)`. All behaviour behind `BepInEx/config/pavol.interplanetary.qol.cfg` entries. Logs through `BepInEx/LogOutput.log`.

### 2.1 Action-phase fast-forward

Mechanism: set `Time.timeScale` while `ActionphaseManager` is playing; never touch the simulation code.

- **Postfix on `ActionphaseManager.StartActionphase`** (static, public). After the original runs, read the private instance (`m_instance`) and its private lists `m_actionphaseProjectileShootEventList` and `m_actionphaseWeaponShotAndHitEventList` through `AccessTools`. Compute `localFired = any shoot.PlayerNetworkID or laser.PlayerId in {p.PlayerID | p.LocalPlayer}`. Also count total events.
- Choose the speed: `AutoSpeedWhenIdle` (default **8**) when `!localFired`, else `AutoSpeedWhenFired` (default **1** = untouched). With 8x: 1 s countdown + 6.5 s minimum -> ~0.95 s wall clock. A config cap of 20 (see risk R2).
- **Hold-to-fast-forward** in the plugin's `Update()`: while `GameManager.CurrentManagerStatus == DisplayActionphase` and `HoldKey` (default `Space`) is held, `timeScale = HoldSpeed` (default 8); on release, back to the auto speed chosen above. Lets him skip the phases where he did fire but does not care.
- **Restore**: (a) postfix on `ActionphaseManager.playActionphase` is not needed; instead a **prefix on `GameManager.ChangeGameManagerStatus(newStatus)`**: whenever `newStatus != DisplayActionphase` and the plugin has a speed applied, set `timeScale = 1`. (b) Belt and braces: in `Update()`, if the plugin applied a speed and `CurrentManagerStatus != DisplayActionphase`, reset to 1. (c) `OnDestroy` resets to 1. The plugin only ever writes `timeScale` when it previously recorded that it changed it, so a future game update that uses `timeScale` itself is not fought.
- **Online games**: results are precomputed and each client only signals "ready for results" after playback, so faster local playback is harmless, but v0.1 defaults to `OfflineOnly = true` (skip when `GameManager.OnlineGame`) because it was not tested.
- Physics: the game moves projectiles by frame index in `ProcessEvent(frame)`, not by physics integration, so scaling fixed steps does not change outcomes. Unity caps fixed steps per rendered frame via `Time.maximumDeltaTime` (default 0.333 s = 16 fixed steps); at 144 fps and 8x only ~1.1 fixed steps per frame are needed, so playback stays smooth.

### 2.2 Hotkeys

Implemented in the plugin's `Update()` with `UnityEngine.Input.GetKeyDown`, not through Rewired, so no dependency on the unknown Rewired maps. Common gate for every hotkey: `InputManager.CurrentInputStatus == ReadInput && InputManager.CurrentInputActionState == "Gameplay"` (so nothing fires in menus, chat, or while a hotseat dialog blocks input).

- **End Turn**: `EndTurnKey` (default `Return`) and `EndTurnKeyAlt` (default `KeypadEnter`). Additional gate: `GameManager.CurrentManagerStatus == PlayerTurn` (offline) - mirrors `Button_EndTurn`'s own check - and `PlayerManager.CurrentPlayer != null`. Action: `((EndturnOverlay)ScreenManager.GetScreenReference("EndturnOverlay")).Button_EndTurn()`. The overlay's existing warning popups still apply (second press proceeds), which is the same behaviour as clicking.
- **Hotseat OK**: if the `TurnPhasesOverlay.TurnStartHotseat` object is active, the same key calls `Button_OK()` instead (that dialog runs with input blocked, so this one hotkey bypasses the `ReadInput` gate but only while that dialog is showing).
- Bonus, zero-risk: **postfix on `EndturnOverlay.userInputMessage`** that completes the dead `Gameplay Endturn` branch: if `GetButtonDown("Gameplay Endturn")` -> `Button_EndTurn()`. Only fires if Rewired actually has a binding; the plugin logs at startup whether the Rewired keyboard map contains that action so we learn the answer.
- Further shortcuts (build menu, research, next city...) are deferred until Pavol says which repeated actions he means. The `Button_*` inventory of every screen is in `docs/` grep output for when he does.

### 2.3 Localization overrides

- **Postfix on `LocalizationManager.GetLocalizedString(string)`**: if `overrides.TryGetValue(key, out v)` then `__result = v`. Overrides are loaded from `BepInEx/config/InterplanetaryQoL.localization.tsv` (`key<TAB>value`, `#` comments), shipped with `ui_technology_name_tec_QuantumComputing<TAB>AGI`. The `HasLocalizationString` path is left alone (the key already exists).
- Discovery aid: config `Localization.LogKeysContaining` (default `QuantumComputing`) makes the plugin call `GetAllAvailableLocalizationItemsForLanguage(CurrentLanguage)` once after the manager exists and log matching keys + values, so the description key can be added to the TSV.
- Language-independent (applies to whatever language is active), ruleset-independent, works on existing saves.

### 2.4 Not in scope for v0.1

- "Monthly debt" (unclear). - Changing action-phase content or skipping to results instantly (8x already yields ~1 s; true skipping would need to replay every `ProcessEvent` in one tick and risk missing VFX/state side effects). - Rewired remapping UI.

## 3. Risks and mitigations

- **R1 - BepInEx 5 on Unity 5.6 / old Mono.** BepInEx 5 documents Unity 5.x support and HarmonyX ships net35 builds, but this exact game is untested. Mitigation: first launch with an empty plugin, confirm `LogOutput.log` shows the preloader, the chainloader and the plugin; only then add patches. If Doorstop fails to inject, try `doorstop_config.ini` `enabled=true` sanity and the x64 build is correct (the exe is 64-bit).
- **R2 - `timeScale` side effects.** Scaled `Time.deltaTime` also speeds camera transitions, UI animators (countdown, "Start" banner), coroutine `WaitForSeconds`, and `SolarSystemManager.SetStateByFrame` (already frame-indexed). Audio is not pitch-shifted by timeScale in Unity. Mitigation: cap at 20, default 8, and reset on any status change (2.1). Verify visually that the planet orbit display and the timer bar look right at 8x.
- **R3 - Stuck fast time if an exception aborts the action phase.** Three independent reset paths (status-change prefix, per-frame check, OnDestroy). Also a `PanicKey` (default `F12`... TBD) that forces `timeScale = 1` and logs state.
- **R4 - Hotkey firing into a wrong context.** Enter is only consumed in chat (a different `CurrentInputActionState`) and in TMP input fields (which sit under `BlockInput`/`Menu` states). Extra guard: skip if `UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject` holds an `InputField`/`TMP_InputField`.
- **R5 - Online desync.** Fast-forward is offline-only by default; End Turn hotkey calls the exact same method as the button, so it cannot produce a state the button cannot.
- **R6 - Steam Cloud / verify integrity.** No game file is modified; only new files are added. Steam "verify integrity" does not remove unknown files.
- **R7 - Save compatibility.** Nothing is persisted by the plugin; `timeScale` is not saved. Localization override is display-only.

## 4. Verification plan

1. Launch game with the plugin built empty -> `BepInEx/LogOutput.log` shows `[Info : BepInEx] Loading [Interplanetary QoL 0.1.0]` and Harmony patch count.
2. Load `Autosave_Local_2026_08_07_19_57_58.sav` (offline, existing), end a turn without targeting -> action phase completes in ~1-2 s, next turn UI appears normal, `timeScale` back to 1 (logged).
3. Fire a weapon, end turn -> normal speed; hold Space -> 8x; release -> 1x; results identical to expectation (hit/miss shown).
4. Press Enter during build phase -> End Turn (with the warning popup behaviour if targeting unfinished).
5. Open tech tree -> "AGI" instead of "Quantum Computing"; log shows the discovered description key.
6. Open the pause menu during a fast action phase, resume -> speed still resets at phase end.
7. Uninstall test: rename `winhttp.dll` -> vanilla game, no residue.

## 5. Questions for the reviewer

- Any reason to prefer Harmony-postfixing `FixedUpdate` to call `playActionphase` N times per tick over `Time.timeScale`? (My view: timeScale is what the devs themselves used, and it keeps VFX/coroutines in sync.)
- Is the `ChangeGameManagerStatus` prefix sufficient as the primary reset, given `Cleanup` calls it with `RoundStart` before posting `ActionPhaseStateMessage.Ended`?
- Any hole in the hotkey gating that could end a turn while a popup is open?
