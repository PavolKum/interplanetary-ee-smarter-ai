# Interplanetary EE: Smarter AI (`AI/`)

BepInEx 5 + HarmonyX plugin that makes the single-player AI play to win. Offline games only; online games always run the native AI.

## Why the native AI is weak (verified in `decompiled/Interplanetary.AI/`)

- **Random targeting.** Each weapon picks a uniformly random living enemy every turn (`AIWeaponTargeting.cs:114`). No focus fire, no leader-punishing.
- **No memory.** Threat assessment is a stub (`OverallThreat` fixed at 1, weapon/vengeful threat methods return 1). Nothing records who attacked whom.
- **Deliberate misses.** `AIDifficulty` sets accuracy 0.4 / 0.75 / 0.9 / 1.0 by level and `AIWeaponTargetingGoal` plans that many misses per turn.
- **Difficulty only changes shooting.** Economy, research and construction are identical at every level.

The 29 Oct 2026 match (debrief `261029-be947731`) shows the result: the human's energy went 1.6k (T10) -> 13k (T30) -> 44k (T40) while all four AIs stayed flat at 2-3.5k, the human's population never dropped, and the AIs spent their fire on each other.

## v0.1 - combat

| Change | Hook | Config |
|---|---|---|
| Threat-based focus fire: score = visible economy (squared share of strongest) + grudge + finish-off, top target gets `FocusShare` of weapons, rest weighted random | postfix `AIWeaponTargeting.BeginTurn` rewrites `TargetEnemy` | `[Targeting]` |
| Reachability memory: a weapon that found no trajectory onto a target is steered to the next-best one for N turns | postfix `AIWeaponTargetingGoal.TargetWeapons` records; targeting reads | `ReachabilityMemoryTurns` |
| Grudges: damage per shooter, decayed per turn | prefix `PlayerBuildingHandler.DealDamageToBuilding` | `GrudgeWeight`, `GrudgeDecayPerTurn` |
| Accuracy: re-split the same shot count at a configured hit share | postfix `AIWeaponTargetingGoal.BeginTurn` | `[Accuracy] AccuracyByLevel` (default `0.6,0.95,1.0,1.0`) |
| Guided strikes go for the visible city closest to destruction (defeat = last city lost) | prefix `AIWeaponTargeting.HandleSecondPhaseTargeting` | `[Strikes]` |

Fairness: every input comes from buildings the AI's own intel can see (same filter the native AI uses), its own damage taken, and its own trajectory results. No hidden information, no resource bonuses.

`HumanBias` (default 1.0) multiplies human enemies' score. At 1.0 the AIs gang up on whoever leads the economy, which is usually the human.

## Files

- `Brain/` - game-independent decision logic (planner, memories, accuracy, strike choice).
- `Patches.cs` - Harmony adapters. Any exception keeps the native behaviour for that hook and is logged once.
- `Plugin.cs` - config, hook registration, offline guard, decision log.
- `Tests/` - console checks for `Brain/`: `dotnet run -c Release` in `AI/Tests` (exit code 1 on failure).
- `tools/summarize_decisions.py` - reads the newest decision log and prints targets, trajectory success and threat scores for the last match.

## Build, install, uninstall

```
dotnet build AI -c Release            # deploys to <game>/BepInEx/plugins/InterplanetaryAI/
dotnet build AI -c Release -p:SkipDeploy=true   # while the game is running
```

Config: `<game>/BepInEx/config/pavol.interplanetary.ai.cfg`. Log lines: `<game>/BepInEx/LogOutput.log` (`T12 AI 02: 9 weapons -> ...`). Decision log: `<game>/BepInEx/InterplanetaryAI/decisions-<start>.jsonl`.

Uninstall: delete `<game>/BepInEx/plugins/InterplanetaryAI/`, or set `[General] Enabled = false`.

## Playtest loop

1. Play a match against AIs (any difficulty).
2. `python AI/tools/summarize_decisions.py`
3. Compare against the recorder's postgame facts (`ScheduleImodes/interplanetary/debriefs/`).
4. Tune config or change the Brain; rebuild; repeat.

## Next (v0.2 candidates)

- Economy: the AIs plateau at 2-3.5k energy from turn ~20. Find out why (construction placement, `MinimumEnergyThreshold`, material builds failing per the director run) and fix it.
- Early pressure: weapon build-up timed against the leader's undefended boom.
- Research path: the native AIs never research some economy techs (Robotic Mining in the director run).
