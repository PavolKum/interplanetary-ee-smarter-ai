# v0.5.0 match diagnosis review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.6-diagnosis.md` (prompt `.prompt.txt`; the first three runs ended early, see the process note below).
Match note: `docs/playtests/2026-10-31-v0.5.0.md`.

## Corrections to my write-up (accepted)
- T21 defenses were 10 Kinetic + 5 Ion (not 15 Kinetic); 19 Kinetic + 2 Ion at T25; 21 Kinetic at T26.
- The human's Railgun events include planet misses; planet-hit counts were 49, 46, 51, 84, 96, 66, 27, 70 for T28-35.
- Oort saw 10-16 human Kinetic Defenses at T15-24 before counterintel hid them: it could have remembered them.
- "271 hits" are firing-list selections, not confirmed hits (planned hits 450).
- Nuclear Missile: present in the building snapshots at T28, T30-32, T35; "placed once" unsupported.

## Verdicts
| H | Verdict |
|---|---|
| H1 reserve too small | PARTLY: at the logged planet-hit volumes the reserve (2.0-3.7k) was below even one 100-energy roll per hit (T28: 4,900), but real interception spending and energy at the action phase were not logged. Unproven that defenses ran out of energy |
| H2 coverage | Plausible, unproven: 21 radius-5.9 defenses cover at most ~48% of a radius-19.6 sphere. No positions logged |
| H3 growth cost | PARTLY: 64% of T25 income went to firing allocation + reserve; confounded |
| H4 Kinetic-only human | PARTLY: Enhanced Kinetic does not stop Missiles; Oort had seen the wall yet queued Railguns |
| H5 Nuclear Missile | PARTLY |
| H6 human energy | PARTLY: his T27 energy fell 15,339 -> 14,339 across the action phase: consistent with defense spending while plenty remained |

## v0.6.0 changes (Terra ranked #1, #2, #3)
| Terra | Change |
|---|---|
| #1 measure and adapt offense | `DefenseLedger` + hook on `PlayerDefenseHandler.IsHitIntercepted`: per shot reaching a planet, the defense events (success / probability fail / no energy / disconnected, energy) are counted for the defender and, per weapon, for the shooter. The AI's own interception rate per weapon over the last 3 turns (>= 12 shots) overrides the estimate in the weapon pick; enemy defenses seen are remembered per enemy (x0.95 a turn) |
| #2 budget from observed impacts | Reserve = max(planned, last action phase's defense energy spent + unpaid rolls x 100, x1.25), up to 80% of income (`DefenseObservedMargin`, `DefenseHardCapShare`) |
| #3 power network | Unpowered buildings: if no land plant can be placed, a Floating Plant is queued |

Deferred: coverage (needs defense positions and impact points), pinning down where powered weapons went (57 -> 31 in v0.4.1; 31 -> 11 here, still unexplained), Nuclear Missile tracing (building ids and placement events), healing a power cascade beyond the plant fallback.

## Process note
The first three Terra runs ended with only progress notes (exit 0). The cause: Terra tried to read a folder outside the repo
(`Repos_Local/ScheduleImodes/interplanetary`, the telemetry recorder's source); read-only mode auto-rejects that and the run
ends. Fix: tell Terra to stay inside the repository and describe the recorder's fields in the prompt.
