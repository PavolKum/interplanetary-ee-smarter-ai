# Terra v0.10 review: disposition (AI 0.10.1)

Verdict was **fix first**. All eight findings accepted and fixed in 0.10.1.

| # | Finding | Fix |
|---|---|---|
| 1 High | Salvo counted every weapon against every enemy, once each | Per-target salvo: weapons the reachability memory marks unreachable are left out; projectiles x fragments; scaled by the AI's planned accuracy. Strike history (pulse, hold counter) now follows what actually went into the firing lists (`StrategyPatches.ReportFired` from the fire postfix), not the plan. |
| 2 High | Unseen defenses = none; overlapping defenses under-modelled; observations pooled | `DefensesSeen` (our intel has seen any of its buildings): a never-seen planet gets `UnseenCover` 0.6 at 0.8. Overlap: `tries = 1 + K/20` defenses roll in turn, stop = 1-(1-p)^tries, rolls consumed per eligible impact = stop/p. Observations kept per weapon id. |
| 3 High | War at T20 also delayed the defense floor and reserve | `DefenseFromTurn` 15 (new) gates the floor and the energy reserve (`d.Defensive`); `WarFromTurn` 20 gates weapons only. |
| 4 High | Release not fail-safe against a failed firing hook | Hold only when the TargetWeapons hook installed (`Plugin.HoldHookPatched`) and its fields resolved; otherwise the decision becomes fire (`/hold-unavailable`). The decision is latched for the turn (no config re-check after release). |
| 5 Med | Migration re-ran every load ("0.10" < "0.5" ordinal) | Explicit states: "" -> "0.5" -> "0.10". |
| 6 Med | Superweapon savings computed before the release | Strategy decides and releases inside the economy postfix before the savings block. |
| 7 Med | Director off: hold without release | Strategy runs only with the director on; targeting falls back to v0.9 otherwise. |
| 8 Med | Small arsenals never fired | Max-hold applies to any arsenal (held turns counted whenever at war with weapons). |

Tests: 257 checks; 16 mutations of the new rules all caught (`pulse`, `rolls`, `overlap`, `unseen`, `accuracy`,
`per-target shots`, `observed`, `max-hold` x2, `commit`, `broken`, `peace`, planner commit, glut x3).
Unverified in game: hold-fire, release, re-pointing, the salvo thresholds (`MinPassingShots` 12, `MinPassShare` 0.45).
