I’m tracing each registered hook through its owner gate and checking the game’s shared targeting machinery, where a per-player gate may not be enough.
## Findings

**1. High — enabling lasers changes the original AI.**  
`AI/LaserPatches.cs:79–87` gates both transpilers on `Plugin.Active`, not the owning player. With `EnableLasers=true`, original AIs can build lasers and no longer skip targeting them; the three laser prefixes at `:92–182` also replace native behavior without an owner check. The game’s native bans are at `decompiled/Interplanetary.AI/AIWeaponConstruction.cs:213` and `AIWeaponTargeting.cs:81`. **Fix:** gate all five laser patches by the owning core’s `IsSmart`; pass or recover the owner in the transpilers. Lasers are off by default.

**2. High — the playtest summarizer crashes on every new init record.**  
At `AI/Patches.cs:49`, a comment accidentally comments out `.Num("acc", ...)`. `AI/tools/summarize_decisions.py:33` still requires `r['acc']`, so it raises `KeyError` before reporting the match. **Fix:** restore the field or update the consumer to handle the new schema and distinguish native from smart accuracy.

**3. Medium — changing SmartLevels during a match can leave a hybrid AI.**  
`AI/Plugin.cs:743,847` reapplies the level set on config changes. A formerly smart AI can retain its modified aim timeout (`AI/StrategyPatches.cs:48–52`), and switching during a turn can bypass the firing hook after smart strategy has already released firing energy (`:290–295,323–330`). The native timeout is per-core and persists across turns (`decompiled/Interplanetary.AI/AICore.cs:34,193–200`). **Fix:** latch membership for the match and apply changes on the next match, or explicitly implement safe transitions. This is conditional on a live config change.

**4. Medium — propagated exceptions are reported as swallowed.**  
Original-AI module exceptions are correctly returned at `AI/TurnGuard.cs:71–76`, but `Report` always says “swallowed” (`:123–128`), as does `AI/tools/live_monitor.py:140–141`. The same misleading report applies to core exceptions. The deduplication key can also suppress a later smart-AI stack trace. **Fix:** record and display the actual disposition, and include it in the deduplication key.

**5. Low — finalizer can mask an exception on a malformed core.**  
`AI/TurnGuard.cs:72–75` permits a non-null owner with null `PlayerData`, then dereferences it while reporting. **Fix:** null-check `owner.PlayerData` before obtaining the name.

## Hook audit

For a stable level assignment and lasers disabled, the registered hooks preserve original AI behavior:

- `CoreInitializePostfix`: resets shared memory for every player; only smart cores get `SetAimWindow`. Game-start initialization is sequential before play (`PlayerManager.cs:648–653`).
- `TargetingBeginTurnPostfix`, `GoalBeginTurnPostfix`: original targeting, processor budget and accuracy remain native.
- `CompleteFirstPhasePrefix/Postfix`: original returns with `__state=-1`; postfix skips.
- `GoalTargetWeaponsPrefix/Postfix`, including hold: original gets `__state=false`; both skip modifications.
- `SecondPhasePrefix`: original uses native selection.
- `DealDamagePrefix`: observation only.
- `CoreBeginTurnPrefix` / `CoreBeginTurnPostfix`: smart-only sales, economy and strategy.
- `CoreEndTurnPostfix`: original gets observation only.
- `TryConstructPrefix`, `TryConstructDefensePrefix`, `TechQueueBeginTurnPrefix`: originals continue native.
- `SearchStepPrefix`, `FinderUpdatePrefix`: placement changes and abandonment are smart-only.
- `BeforeIntercept` / `AfterIntercept`: observation only.
- **TurnGuard:** finalizers cover eight module `BeginTurn` methods and `AICore.BeginTurn`; original exceptions propagate, subject to finding 5.
- **LaserPatches:** all five need attention as described in finding 1.

## Shared state and comparison

Economy and strategy state are keyed by player ID; enemy iteration builds views rather than requiring enemy director/economy state. I found no original-AI-induced state corruption with a fixed assignment. Grudge and interception observations appropriately include originals.

The processor calculation **does count original AIs and their weapons**, reducing the smart AI’s allowance as the lobby grows (`AI/Patches.cs:100–106`); the calculator itself is per core (`AIWeaponTargeting.cs:46–52`). That is a shared-budget design consequence, not an original-AI rewrite.

The comparison also changes native difficulty: Hard’s native accuracy is **0.9**, Impossible’s is **1.0** before the smart override (`AIDifficulty.cs:18–33`). Results therefore compare the configured lobby opponents, not two implementations at identical native difficulty.

**Verification:** static review against the supplied diff and decompiled source; no game run or tests performed.

**verdict: SHIP AFTER FIXES — findings 1–4; fix finding 5 with the finalizer changes.**
