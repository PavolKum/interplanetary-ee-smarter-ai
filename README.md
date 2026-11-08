# Interplanetary EE: Smarter AI

A BepInEx mod that replaces the computer opponents in *Interplanetary: Enhanced Edition* with a rules-based AI that
plays to win and fights fair. It sees only what its own intel shows, aims with measured human-like accuracy and gets no
resource bonuses. It builds its economy, researches, defends and strikes the way a strong human player does. Each
version is tested in logged human-vs-AI matches, and every change is reviewed by an LLM against the logs and the code
before it ships. It is still losing: it has won 1 of 29 matches so far.

## Download

Get `InterplanetaryAI-vX.Y.Z.zip` from the [Releases](../../releases) page.

## Install (3 steps)

1. Install **BepInEx 5.4.x (x64)** from its [releases page](https://github.com/BepInEx/BepInEx/releases): unpack
   `BepInEx_win_x64_5.4.x.zip` into the game folder (next to `Interplanetary.exe`).
2. Start the game once and quit. BepInEx creates its folders (`BepInEx/plugins`, `BepInEx/config`).
3. Unpack the mod zip and drop the `InterplanetaryAI` folder into `BepInEx/plugins`, so you have
   `BepInEx/plugins/InterplanetaryAI/InterplanetaryAI.dll`.

Start an offline game and set at least one AI to **Impossible**: by default only Impossible AIs run the Smarter AI
(see Config). No game file is modified.

## Compatibility

| | |
|---|---|
| Game | Interplanetary: Enhanced Edition (Steam), Windows |
| Engine | Unity 5.6 (Mono, .NET 3.5) |
| Loader | BepInEx 5.4.x x64 (HarmonyX is included with BepInEx) |
| Game modes | Offline games only. In an online game the mod does nothing and every AI runs the original code. |
| Other AIs | AIs on levels not listed in `SmartLevels` run the game's original AI, untouched. |

The defense rules read the active ruleset, so Classic rules (where Kinetic defenses also stop Missiles and there is no
Ion defense) are handled; most testing was done on the Enhanced ruleset.

## Fairness rules

| Area | Rule |
|---|---|
| Information | Only what the AI's own intel shows: visible buildings, and an enemy's production and population only when its intel beats their counterintel. Hidden enemies get an assumed share, never the true value. No shadow intel, no map reveal. |
| Accuracy | Planned hit rate matched to a human's measured rate (default 0.7 for most weapons, 0.85 for guided weapons). Misses are fired as real near-misses, not withheld. Accuracy is a difficulty setting, not where the "smarter" part lives. |
| Resources | No bonuses. Same income, costs and build rules as a human; buildings, upgrades and weapons are bought through the game's own systems. |
| Memory | It remembers what it has seen and who hit it (with decay), as a human would. |

## Scoreboard

Human 28 wins, Smarter AI 1 win, through match 29. The AI's win was match 25 (v0.19.0, three Smarter AIs, with the
human trying an experimental nukes-only offense). Matches 7 and 8 were human wins with no notes recorded. Every match,
what that version tried and why it still lost: [docs/PROGRESS.md](docs/PROGRESS.md). Per-match notes are in
[docs/playtests/](docs/playtests/), reviews in [docs/reviews/](docs/reviews/).

## Config

The config file is created on first launch: `BepInEx/config/pavol.interplanetary.ai.cfg`. Every option has a comment.
Restart the game after a change. The main options:

| Section / key | Default | What it does |
|---|---|---|
| `[General] Enabled` | `true` | Master switch. |
| `[General] SmartLevels` | `3` | Lobby AI levels that run the Smarter AI (0 Easy, 1 Medium, 2 Hard, 3 Impossible; comma-separated). Other levels run the original AI. |
| `[Accuracy] AccuracyByLevel` | `0.4,0.7,0.7,0.7` | Planned hit share per level, lowest first. |
| `[Strikes] GuidedAccuracy` | `0.85` | Planned hit rate for guided weapons (Missile, Nuclear Missile). |
| `[Accuracy] FireMisses` | `true` | Fire planned misses as near-misses (off = hold them and save the firing cost). |
| `[Logging] DecisionLog` | `true` | Write the per-turn decision log used for bug reports. |

Known behaviour: on firing turns the AI searches for trajectories for up to 40 seconds (the game uses 20,
`[Strategy] AimingWindowSeconds`), extended up to 90 seconds while it still finds new ones. If you end your turn sooner,
you may wait a little before the action phase. `[Strategy] AimExtend = false` turns the extension off.

## Uninstall

Delete the `BepInEx/plugins/InterplanetaryAI` folder (and `BepInEx/config/pavol.interplanetary.ai.cfg` if you want the
settings gone too). Or set `[General] Enabled = false`.

## Bug reports

Open an issue on this repository and attach, from the game folder:

- `BepInEx/LogOutput.log`
- the newest `BepInEx/InterplanetaryAI/decisions-*.jsonl`

Say which version you ran (the first lines of `LogOutput.log` show it), the map, the number of AIs and their levels,
and roughly on which turn the problem showed.

## How updates ship

New versions are published as [GitHub Releases](../../releases) with a changelog entry ([CHANGELOG.md](CHANGELOG.md)).
To be notified, use *Watch > Custom > Releases* on this repository. The mod never checks for updates over the network.
To update, replace the `InterplanetaryAI` folder; your config file is kept.

## Build from source

Requires the .NET SDK 8 and the game with BepInEx installed (the build references the game's and BepInEx's DLLs from
your install; they are never copied into the repository).

```
dotnet build AI -c Release
dotnet build AI -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Interplanetary Enhanced Edition"
dotnet build AI -c Release -p:Deploy=true
dotnet run -c Release --project AI/Tests
```

- The first line uses the default Steam library (`C:\Program Files (x86)\Steam\steamapps\common\Interplanetary Enhanced Edition`).
- The second points the build at another game folder.
- `-p:Deploy=true` also copies the DLL into `<game>/BepInEx/plugins/InterplanetaryAI/`; it refuses while the game runs.
  A plain build never touches the game.
- The last line runs the game-independent tests for `AI/Brain/` (exit code 1 on failure).

Developer notes: `AI/README.md` (design notes for the early versions), `docs/` (playtests, reviews, strategy),
`AI/tools/` (log summaries, gate checks, review wrapper). Some notes cite a local reference copy of the game's code
(`decompiled/...`); that copy is not part of this repository.

## License

MIT (see [LICENSE](LICENSE)). Not affiliated with TJR Games or Team Jolly Roger; see [NOTICE](NOTICE).
