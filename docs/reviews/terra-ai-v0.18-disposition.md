# Terra v0.18 review - disposition

Review: `terra-ai-v0.18.md` (GPT-6.1 Terra, read-only, 7 Nov 2026), verdict **SHIP AFTER FIXES**. Diff: `terra-ai-v0.18.diff`
(against 5170c53 plus `AI/AimWatch.cs`). All six findings were addressed. Tests went from 595 to 607 checks; 11 of 11 mutants
were killed. The fixes were not re-reviewed.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | High | Reach alone could make a minor human the commit over a far more dangerous rival | **Fixed.** The reach path also needs the human's score >= `HumanFirstReachMinScore` (0.35) x the best score. T38 of match 21 (740 / 1,450 = 0.51) still passes; a human at 0.21 of the best score with the same reach does not (test). |
| 2 | Medium | The watcher counted unpowered weapons and weapons aimed at a fallback target | **Fixed.** `Usable` requires normal and powered (the firing hook's set), and only weapons whose target is the commit count. |
| 3 | Medium | No decision counted as firing; an extended `m_timeOut` could carry into a turn whose decision failed | **Fixed.** Extension needs an explicit current decision with `Fire` and a commit (ClearDecision runs at every turn start). On the first frame of a turn without a firing decision, a timeout above 20 s is reset to 20 s. |
| 4 | Medium | The frame on which the AI became ready was not counted; the log was skipped without economy state | **Fixed.** The postfix counts that frame, then marks the turn done with a stop reason (all aimed / native ready / hold window / enough / cap / stall budget / window). `AimWatch.Log` runs before the economy-state check. |
| 5 | Medium | A long frame could cross the timeout before the postfix extended it | **Fixed.** The extension moved to a prefix on `AICore.Update`, using the previous frame's counts and timer + `Time.deltaTime`. Counting stays in the postfix. |
| 6 | Low | The stall test is a waiting heuristic, not proof of unreachable geometry | **Fixed (wording).** Called a "stall budget" in the code and config. The aim-search record (fps, found at 20/40 s, facing angles) is what will tune it. |

Unverified in game: the per-frame cost, the wait the human sees when ending a turn early (at most 90 s from the AI's turn
start), and whether the extra time raises the number of aimed weapons.
