I’m tracing the timeout and targeting paths against the decompiled game code, including when the search actually stops and when the commit can change.
## Verdict: SHIP AFTER FIXES

The two changes address real match-21 problems, but the reach rule can override a much more dangerous rival, and the aim watcher measures a different population from the one the firing logic uses. The telemetry also misses the final search frame, weakening the evidence needed to tune the feature.

### High

**1. Reach alone can make the human the permanent commit despite a far more valuable AI.**  
`AI/Brain/Strategy.cs:299-306, 344-350`

At the default `0.4`, a human with 4 expected projectiles through beats a rival with 10, regardless of their relative scores. The override happens *after* stickiness and before precision and return-fire evaluation; consequently those checks are recomputed only for the human. A dangerous rival attacking the AI—or with a visible, readily killable city—can be ignored. The low-yield exclusion addresses failed trajectories, not strategic value.

**Fix:** Keep a leader/threat or value floor on the reach override, or specifically suppress the finishing/grudge inflation that caused match 21. Add decision-level tests for a trailing human versus a dominant rival, a credible rival precision kill, and attacks from each player. Match 19’s 10/18 and match 21’s 27/43 both clear 0.4, but those examples do not establish that 0.4 is safe in the opposite situation.

### Medium

**2. The watcher counts unpowered and fallback-target weapons as useful progress.**  
`AI/AimWatch.cs:94-105, 117-119`; `AI/Patches.cs:148-149, 184-193, 703-716`

Native targeting does not update unpowered weapons (`decompiled/Interplanetary.AI/AIWeaponTargeting.cs:143-150`), yet the watcher includes them in `live`. It also counts primary trajectories to **each weapon’s assigned planet**, which may be a reachability fallback rather than the commit. Those fallback successes can satisfy the 80% threshold while few weapons have aimed at the commit; the firing hook can then abort for fewer than three committed hits.

**Fix:** Align eligibility with the registered, owned, normal, powered weapons used by the firing hook (`AI/Patches.cs:287-301`). Track committed-target progress separately from fallback progress, and make the stopping rule reflect the actual objective. Test unpowered weapons, fallback-heavy assignments and abort thresholds.

**3. No decision is treated as a firing decision; an extended timeout can carry into later turns.**  
`AI/AimWatch.cs:89-92, 117-123`; `AI/StrategyPatches.cs:79-89, 136-138, 282-294`

`HoldFire` returns false when there is no decision, so disabling strategy/director or a failed decision still permits extension. Native `BeginTurn` resets the timer but **not** `m_timeOut` (`decompiled/Interplanetary.AI/AICore.cs:158-161`); if decision setup fails after a prior extension, that longer timeout can persist.

**Fix:** Require a current, explicit `Latest(id)?.Fire == true` before extending, and reset the base timeout at turn start even when strategy setup fails. Test disabled strategy and a failed decision following an extended turn. An explicit hold decision itself does not extend.

**4. “End” telemetry omits the frame on which the AI becomes ready.**  
`AI/AimWatch.cs:78, 87-115, 172-177`

The postfix returns as soon as native `Update` sets `IsReady`. That native call already updated the trajectory calculator and timer (`decompiled/Interplanetary.AI/AICore.cs:177-200`), so `found`, seconds, frames, and 20/40-second snapshots can omit final progress; `stop` may remain blank or stale. `AimWatch.Log` is also skipped if economy state is absent (`AI/EconomyPatches.cs:665-668`).

**Fix:** Capture the final snapshot on the readiness transition and record an explicit native-ready/timeout reason. Log independently of economy-state availability. Test a trajectory found on the terminating frame.

**5. A long frame can bypass the extension.**  
`AI/AimWatch.cs:73-78`; `decompiled/Interplanetary.AI/AICore.cs:193-200`

The timeout is extended in a postfix, after native code checks readiness. If the timer crosses its timeout in one frame before the watcher has extended it—for example, a frame longer than the two-second margin—the AI is already ready and the postfix cannot help.

**Fix:** Evaluate a prospective extension before the native timeout check, while preserving the native all-modules-ready path. Test a timer jump across the boundary.

### Low

**6. Stall is a waiting heuristic, not evidence of unreachable geometry.**  
`AI/Brain/Offense.cs:113-118`; `AI/Plugin.cs:523`

Fifteen seconds without a new primary result can also reflect a low-probability random search or low frame rate. Conversely, no “unreachable” mark in match 21 does not establish that more time would have succeeded.

**Fix:** Describe this as a *stall budget*, not proof of unreachable weapons; measure attempts, frame rate and subsequent gains before tuning it.

## Other answers and verification

- **Search:** Gravity weapons continue sampling while `IsTurnOver` is false; the 50 slots are concurrent search slots, not a 50-attempt lifetime cap (`decompiled/Interplanetary.AI.Trajectories/AIGravityTrajectoryWeapon.cs:33-52`). Extra time can help, but its benefit is unproven.
- **Timeout safety:** With ordinary finite settings, the 90-second cap prevents oscillation from hanging the turn; `LastFound` is a high-water mark. Native end-turn uses a separate five-second timeout and resets the timer (`AICore.cs:204-248`).
- **Fairness and cost:** The new watcher reads the smart AI’s own weapons and assigned planets; I found no new hidden-enemy-value or resource leak. `Plugin.IsSmart` gates it away from original AIs. Per-frame work is linear in the targetable list plus reflection; likely modest, but **unmeasured in-game**.
- **Reflection:** Missing fields make installation return zero with a warning; the independently called logger can still encounter a missing field and report an exception once.
- **Coverage:** `V018Rules` exercises the pure predicates, not the native lifecycle, actual target assignment, final-frame logging or mixed-lobby integration. The stated ten mutants were not independently rerun: this was strictly read-only. The referenced raw match-21 data was unavailable in this checkout.
