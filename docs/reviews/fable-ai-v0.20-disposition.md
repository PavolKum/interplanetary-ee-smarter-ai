# Review of AI v0.20.0: disposition

**Reviewer:** Fable 5.1 (`fable-ai-v0.20.md`), verdict "ship with fixes". It stood in for Terra:
- the OpenAI subscription route hit its usage limit (`terra-ai-v0.20.attempt1/2.err`);
- OpenRouter was paused by Pavol for budget.

**Caveat:** Fable is the same model family as the implementer (Opus 5.5). **A Terra review stays pending** for when its
quota allows.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | major | A post-schedule latch counted as an I2 opportunity (PASS on no evidence) | Fixed in `WarPhase` (the retaliation state is not touched at or after the scheduled war turn; test + mutant `schedule-touches`) and in `verify_gates.py` (a latch counts only before `warTurn` and with a later strategy record; a self-test shows UNTESTED) |
| 2 | major | `verify_gates.py` did not split timelines | Fixed: segments at each init burst; m25's aborted lobby now separates; self-test |
| 3 | major | Record gates missing from the script | Added: aimedWeapons does not fall while latched; no unexplained energy/material refusal; pulse cadence (saturated pulse >= StrikeEvery after the last strike); max-hold not above the matches 22-26 baseline (3/186). Each has a self-test that FAILs. Generic placement start/terminal stays explicitly UNTESTED (no start record yet; v0.21) |
| 4 | minor | A retaliation carried into T12 by hysteresis never latched | Fixed: a single latch after the branch for any retaliation war at or after T12; boundary test (T11-T13 865/590/529 = war, war, war); the now-redundant in-branch latch was removed after the mutation runner showed it could not be killed |
| 5 | minor | Facing buckets and `live` came from different snapshots | Fixed: commit buckets counted on the same frame and loop as `live`; `commitLiveNoTarget` logged; the gate sums both |
| 6 | minor | Stamp dirty flag edge cases | Fixed: untracked `.cs`/`.csproj`/Brain counted; git stderr discarded |
| 7 | nit | CheckVersion after compile | Fixed: `BeforeTargets="CoreCompile"` |
| 8 | minor | A non-compiling mutant counted as killed | Fixed: INVALID, run fails |
| 9 | nit | Prefix swallowed exceptions | Fixed: ReportOnce |
| 10 | nit | Dual plumbing for the pulse-wait switch | Kept (same pattern as every StrategySettings value) |
| 11, 14 | nit | Script details, backup tool | Accepted as is |
| 12 | minor | S1 gates do not judge late-volley quality | Deferred: judged in play from `fire`/`fire-paid` on late-pulse turns; noted for the match 27 note |
| 13 | nit | Red-before-fix is a baseline comparison; the booking is untested (game-bound) | Accepted. The booking is checked in play by the pulse-cadence and max-hold gates |

**State after the fixes:** 650 checks pass; 10 of 10 mutants killed; verify_gates self-test 14 of 14.
