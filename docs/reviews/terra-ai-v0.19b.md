I’ll trace the turn lifecycle and resource accounting against the decompiled game, then check the strategy and telemetry changes for regressions and fairness.
The key ordering assumption does not hold: the native targeting module can call the firing goal inside `AICore.EndTurn`, before the new postfix runs. I’m tracing the consequences for shots, history, and the next turn’s budget.
## Findings

### 1. **Blocker — Late free fire runs after the firing goal has already committed to holding**

**Location:** `AI/EconomyPatches.cs:655–669`, `AI/StrategyPatches.cs:341–356`; native order at `decompiled/Interplanetary.AI/AICore.cs:241–249` and `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:173–184`.

`AICore.EndTurn` calls each module’s `EndTurn` **before** the Harmony postfix. The weapon-targeting module calls `AIWeaponTargetingGoal.TargetWeapons()` inside its own `EndTurn`, not later in `UpdateEndTurn`. The only native call to that goal is at `AIWeaponTargeting.cs:183`.

On a qualifying held turn, the sequence is therefore:

1. The goal prefix sees `HoldFire`, clears both firing lists, and the native goal marks the weapons targeted with an empty trajectory-input list (`AI/Patches.cs:625–642`; `AIWeaponTargetingGoal.cs:63–103`).
2. The goal postfix calls `ReportFired` with zero shots, incrementing hold history (`AI/Patches.cs:790–794`; `AI/StrategyPatches.cs:114–135`).
3. Only afterward does `LateFreeFire` subtract the reservation, flip `Fire`, and log “late-free-fire.”
4. `UpdateEndTurn` processes the already-empty input list. It does **not** rerun the goal (`AITargetWeapons.cs:82–101`).

**Failure scenario:** Doc’s T23 shape qualifies by energy. The new code would announce late free fire and reduce its AI budget by 3,500, but the AI would still fire nothing. The director would record 13,620 rather than the 17,120 actually left in the decision budget, distorting next turn’s `FreeFireInputs` and potentially its energy-glut assessment. The match log itself shows the relevant ordering: the zero-shot `fire` record precedes `aim-search` and `director` (`docs/playtests/data/v018m24-ai-decisions.jsonl:446–448`).

**Fix:** Make the decision **after construction’s `EndTurn` but before the goal’s existing prefix reads `HoldFire`**—for example, at the start of the smart-AI goal prefix, before the hold check. Then let the existing list rebuild, accuracy filtering, abort guard, and `ReportFired` run once in their normal order. Keep director logging after the final decision. Add an integration-level lifecycle test that would fail on the current order: a qualifying hold must produce a nonempty firing plan, run the abort guard, update strike history appropriately, and record the correct end budget.

### 2. **Major — The “spare” budget is not guaranteed to have passed through defense and repair funding**

**Location:** `AI/StrategyPatches.cs:341–347`; director funding at `AI/DirectorPatches.cs:597–603, 641–683`.

Late free fire checks the strategy state and resource balances, but it does not check that the director actually reached and completed `StartConstruction`. Defense reserve and repair funding are applied there—not at strategy decision time.

**Failure scenario:** A held AI reaches the native 20-second timeout after the human ends, while its power-grid module is still searching. Native construction only updates when `PowerGrid.IsReady` (`decompiled/Interplanetary.AI/AIConstruction.cs:477–490`); the timeout can nevertheless mark the core ready (`AICore.cs:189–201`). In that case the balance can cover the released reservation even though the director never withheld the defense reserve or ran repairs. Once finding 1 is fixed, the supposed free volley could consume energy that was intended for defense.

**Fix:** Fail closed unless this turn’s director has **completed** the defense-reserve and repair budgeting stage. A flag set at the end of that stage is safer than merely checking `Started`, which is set before funding begins. Test a timed-out/no-construction turn and a failed or incomplete budgeting stage.

### 3. **Minor — Aim-watch stop labels can still misidentify why the AI became ready**

**Location:** `AI/AimWatch.cs:177–189`.

Logging `timeoutAtReady` is useful, but it does not resolve the earlier stop-reason problem. “All aimed” counts usable weapons aimed at the commit; native readiness depends on all modules and all native targetable weapons (`decompiled/Interplanetary.AI/AICore.cs:177–201`, `AIWeaponTargeting.cs:26–40, 143–156`).

**Failure scenario:** Every commit-facing weapon has a trajectory, but a fallback-target weapon or another module remains unfinished. If the human finishes after the timeout, the native timeout makes the AI ready, yet the record says `all aimed`. The converse ambiguity remains on a frame where native readiness and timeout both become true.

**Fix:** Log the native all-modules-ready condition independently from commit-facing aim counts, and report timeout/human-gate facts separately. If both readiness paths occur on the same frame, label the cause ambiguous rather than choosing `all aimed`.

### 4. **Minor — The match-based unit fixture uses the wrong end-of-turn material value**

**Location:** `AI/Tests/Program.cs:1696–1702`; source record at `docs/playtests/data/v018m24-ai-decisions.jsonl:448`.

The Doc T23 fixture supplies **2,891** as material left, but that is the director’s `materialStart`; its `materialLeft` is **16**. The fixture also describes 13 aimed at the human, whereas the matching `fire` record reports nine primary trajectories for `jogi` (`:446`).

For this particular match the enhanced ruleset’s firing-material cost is blank (`docs/ruleset/embedded/enhanced/WeaponsData.csv:8`), so the material discrepancy does not change the intended predicate result. It does, however, make the claimed replay misleading and would mask the distinction if used to validate a material-cost scenario.

**Fix:** Use the actual end-of-turn values and recorded aim count, and keep the nonzero-material-reservation cases as clearly labeled synthetic tests. Separately add the lifecycle test in finding 1: the existing pure-predicate checks cannot detect where the adapter runs.

## Requested interaction checks

- **Construction and repairs:** Normal construction decisions and repair charges precede the proposed decision point, but native construction, upgrade, and project actions execute later through `AIActions.EndTurn` (`PlayerManager.cs:123–129`; `AIActions.cs:43–50`). The director has already charged their planned costs to `core.Resources`, so that balance is a conservative planning ledger—not necessarily the player’s already-spent stock. The incomplete-director exception is finding 2.
- **Reservation and actual payment:** `Release` credits both the AI resource ledger and queue allotments. Taking back only `core.Resources` is **not a literal inverse**, but after `AIConstructionQueue.EndTurn` clears the queue and stops placement searches (`AIConstructionQueue.cs:139–143`), I found no later native construction use of those allotments this turn; `BeginTurn` replaces them next turn. Actual weapon costs are charged separately to `PlayerResourceHandler` when queued targeting actions execute (`AIFirstPhaseTargetWeaponAction.cs:27–34`; `PlayerWeaponHandler.cs:230–260`). The ledger subtraction itself neither pays for nor grants a shot.
- **Abort and history:** In the current code, the late decision occurs too late for `ShouldAbort`; `ReportFired` has already counted a hold. After fixing the order, verify both a successful volley and an abort. An abort must not consume a strike or falsely represent the full reservation as actually spent when calculating later surplus.
- **Aim window:** A late candidate began as a hold, so its trajectory search used the native 20-second timeout, potentially longer while waiting for the human. Flipping the decision after that search cannot recover trajectories that a longer firing window might have found. This is a real effectiveness tradeoff, not an accuracy or hidden-information cheat; the abort guard becomes particularly important.
- **Telemetry:** `strategy.fire=false` remains a valid **start-of-turn** snapshot, but consumers must join the later event to determine the final decision. `aim-search.fire=false` describes the search phase, not the final decision. Currently the `late-free-fire` event is misleading because finding 1 prevents the shot.
- **Original AIs and fairness:** The new call is behind the `Plugin.IsSmart` guard in `CoreEndTurnPostfix`; the aim hooks also check `IsSmart`. I found no new hidden-enemy input or resource grant in this diff. Actual firing remains subject to native resource checks and the existing accuracy/targeting hooks.

## Verification

Read-only review of the diff, current source, decompiled lifecycle, and relevant match records. `git diff --check -- AI` passed with line-ending warnings. I did **not** run the tests or game, and did not independently verify the stated check count or mutation results. **Unverified in game:** the corrected lifecycle, actual paid shots, abort accounting, and visible turn timing.

**verdict: do not ship** — the main feature currently logs and budgets a volley after the game has already committed to firing nothing. Fix the ordering and the incomplete-budget guard, then verify the full lifecycle rather than only the pure predicate.
