# interplanetary-mods

Quality-of-life mods for *Interplanetary Enhanced Edition* (Unity 5.6.7f1, Mono). Loader: BepInEx 5.4.23.3 x64 + HarmonyX.

## QoL plugin (`QoL/`)

| Feature | Default | Config section |
|---|---|---|
| Action phase fast-forward when no human fired this turn | 8x (offline games only) | `[ActionPhase] SpeedWhenHumanIdle` |
| Action phase speed when a human did fire | 1x (vanilla) | `[ActionPhase] SpeedWhenHumanFired` |
| Hold to fast-forward any action phase | Space, 8x | `[ActionPhase] HoldToFastForwardKey / HoldSpeed` |
| Force time scale back + log state | F12 | `[ActionPhase] PanicKey` |
| End Turn hotkey (also confirms hotseat "next player") | Enter / Keypad Enter | `[Hotkeys] EndTurnKey / EndTurnKeyAlt` |
| Localized text overrides | Quantum Computing -> AGI | `[Localization] Overrides` (`key=value;key=value`) |

Config file: `<game>/BepInEx/config/pavol.interplanetary.qol.cfg` (created on first launch). Log: `<game>/BepInEx/LogOutput.log`.

## Build

```
dotnet build QoL -c Release
```

Requires .NET SDK 8 and the game installed at the path in `QoL/QoL.csproj` (`GameDir`). The build copies the DLL to `<game>/BepInEx/plugins/InterplanetaryQoL/`.

## Install / uninstall

Install: unpack `BepInEx_win_x64_5.4.23.3.zip` into the game root (adds `winhttp.dll`, `doorstop_config.ini`, `BepInEx/`), then build. No game file is modified.

Uninstall: delete or rename `<game>/winhttp.dll`. Everything else is inert without it.

## Layout

- `QoL/` - the plugin (net35, C# 7.3).
- `decompiled/` - ILSpy 9.1 output of `Assembly-CSharp.dll`, for reference only.
- `docs/plan-qol-v0.1.md` - design, with the survey of the game's internals and the post-review revisions.
- `docs/reviews/` - external review passes (GPT-6 Astra via OpenCode).
