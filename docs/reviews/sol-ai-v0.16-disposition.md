# Disposition: Sol review of AI v0.16.0 (docs/reviews/sol-ai-v0.16.md)

**Verdict received:** DO NOT SHIP (2026-11-01 19:51).

**Scope decision (Pavol, 19:5x):** Ion cover-site reservation is deferred to v0.16.1. v0.16 ships with the T18 prior and Plasma right after Maglev. The gate `minCityCover >= 1 by T20` decides whether v0.16.1 builds reservation.

**Verification after the fixes:**
- Brain tests: 490 checks pass.
- Mutations: 26 of 26 across v0.16 are caught (22 for the original rules, 4 for the review fixes).
- Plugin build: clean.
- The adapter code (Unity hooks) has no unit harness. Those fixes are checked by build and code reading only, and stay **unverified in-game** until the next match.

| ID | Severity | Finding | Fix | Status |
|---|---|---|---|---|
| F1 | Blocker | Shadow cities still rescue precision targeting; native zero-candidate fallback accepts Shadow | `AddStrikeCandidates`: Visible only (the Shadow branch is removed). Zero candidates: an information-free random surface point (`Random.onUnitSphere x radius`); never falls back to native selection | fixed |
| F2 | Blocker | `EnemyView.VisibleCities` = real surviving-city count | `BuildEnemyView`: the count of currently visible intact cities, outside the population gate; population readability stays as `EconomyKnown`/`CityPopulation` | fixed |
| F3 | High | `CityFreeSlots` omits native projects committed this turn | Subtracts `AIProjectConstruction.m_projectsInstalledThisTurn` (reflection), as native `HasCityFreeSlots` does | fixed |
| F4 | High | Repairs read stale ledger; hold excludes planned allocation; firing protection only powered | `DefenseLedger.PendingDefender` (the phase just finished, before `Roll`). Repair hold = the intended reserve: `DefenseReserve` with planned share, observed need and forward need, using last turn's defense inputs, capped to leave firing and the 0.2 recovery floor. Firing protection covers every Normal weapon | fixed |
| F5 | High | Native minimum/preferred intel bypass the budget/stop-loss; minimum restarts batches | `IntelBudget.BudgetRoom` and `RecordSpend`. Native minimum stays inside the budget share and never opens a batch. Step 7 (preferred intel) is removed. Each candidate is priced at its own cost (Lidar 50, array 200). Lidar has its own counter | fixed |
| F6 | High | Nuclear bypasses the blind ban; the ban is not target-specific | `TargetCandidates(core)`: visible strike candidates on the committed target. `d.Blind` comes from it. The superweapon goal skips Nuclear when blind, and `TryBuySuperweapon` refuses it | fixed |
| F7 | High | Existing Missiles still fire blind; no paid-firing ledger | At the firing point (`GoalTargetWeaponsPrefix`), guided weapons whose target has zero visible candidates are removed from both firing lists, so their energy stays unspent. New `fire` record: hits and misses by class, live firing E by class and target, candidates by target, withheld count | fixed (`fire` is what is committed to fire; native payment failure is not observable) |
| F8 | High | Reorder doesn't enforce per-class quotas | The reorder prefix is removed. `EnforceClassQuota` at the firing point: a class over its quota moves the extras to verified near-misses (or holds them); a class under its quota is filled only from its own class (primary, then secondary trajectory); blind and non-operational weapons are excluded. The final counts are in the `fire` record | fixed |
| M1 | Medium | Credible kill uses the average guided damage | Damage per shot is weighted over the guided weapons that reach the committed target (`guidedPopDamageById`). On a precision strike the second phase skips the 25% weapon-building preference | fixed |
| M2 | Medium | Jamming science doesn't compound | `d.ScienceIncome` projected and incremented by `NextTurnScience`; the global science multiplier applies to it | fixed |
| M3 | Medium | Save/load loses provenance/state | A counter-research head (urgent, or any defense-unlocking tech) survives the guard, including right after a load. The war latch is re-derived from the conditions. Intel spend restarts at 0 after a load | partly fixed; intel/war state persistence deferred |
| M4 | Medium | Intel pricing fixed; counters conflated | Per-candidate pricing; `IntelUpgradesThisTurn` and `intelLidar` logged separately; native minimum may buy Lidar | fixed |
| M5 | Medium | Blind upgrades skip planned effects; plain weapons take upkeep first | `PlannedWeaponEffects` bookkeeping added; when blind, the artillery package is tried before step 5's plain weapons | fixed |
| M6 | Medium | Roll cost and upgrades include unpowered defenses | Powered defenses only, in `LiveRollCost` and `TryDefenseUpgrade` | fixed |
| L1 | Low | Cancelled cover searches log no terminal record | deferred to v0.16.1 (with S1) | deferred |
| R1 | Adoption gap | Robotic Mining is not conditional on an installable upgrade path | kept late in the plan; the predicate is deferred | deferred |
| S1 | Scope | Ion cover-site reservation | deferred to v0.16.1 (Pavol) | deferred |

**Gate logging added (Sol section 5):**
- `projectSlots` now carries city IDs.
- A `project` event records the city, capacity before the purchase, reserve, and the counts after it.
- `keyTechs` lists the gate techs completed.
- `upgradedDefenses` counts powered defenses by upgrade.
- `targetCandidates`.
- `intelBatchTurn` / `intelBatchBought` / `intelLidar`.
- The `fire` record.
- `guidedPopDamage` in the strategy record.

**Still not logged:** ranked economy alternatives with rejection reasons, and allocation reasons for reserve shortfalls.
