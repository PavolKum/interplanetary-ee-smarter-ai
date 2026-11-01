All checks are done: HEAD is the frozen target, the working tree matches `status-before.txt`, and `applyGlobalModifier` only registers with the player's global table, so the population multiplier never reaches city growth. Writing the sign-off.

Verdict: CONFIRM

# Lane A sign-off on decision-record.md

Hash check: `decision-record.md` sha256 = `888f8bb0ad1c1eb6ee38a3164a68a9b1e51ce0a66efb229c7a76d277158d63fe` (matches). Section 0 integrity also re-checked: `laneA.md` `1facc88e...709b`, `xreviewA.md` `dc6ac40e...94e4`, `laneB.md` `a06745b5...430a`, `xreviewB.md` `9d7f9f4b...05e7` all match; HEAD is `24683684d979...`; porcelain is the same five untracked paths. No file was written or changed by this pass.

## 1. Lines that state my position, numbers or withdrawals: corrections with accepted wording

Each item below is a replacement I accept. None changes an adopted action; items 1 and 3 stem from inconsistencies in my own submissions, which I correct here.

1. **S5, "Lane A's position" cell: "6 arrays by T8" misstates my sealed position.** laneA §3.7 and A5 say the T8 sighting has no later value under strict visible-only and time the arrays to the firing window (T15-T19). The "6 arrays by T8" in xreviewA item 5 carried match 16's actual behaviour into the bid and is withdrawn. Replacement cell: "`MaxIntelBuildings` 30 → 21 as a hard cap; arrays timed to the firing window T15-T19, not T8 (laneA §3.7, A5); `unseenCities` = 0 on T19 as the gate, withdrawn under N3." Consequence for the split text: "a T8 sighting is worthless under strict visible-only" is shared, not B's correction of A; the remaining split is the 21 hard cap versus no fixed ceiling. The proposed handling stands.

2. **N7, "Fable treats it as live":** replace with "Fable read the ×0.9 population multiplier from `ProjectsData` and proposed the 3-copy cap as a precaution until the growth bite is measured. At sign-off A re-checked the growth path and withdraws the growth effect: `PopulationGeneration` (`BuildingStatHolder.cs:108-116`) reads only the per-building `TotalBonusModifier`, growth is `CurrentPopulation += PopulationGeneration` (`:414`), `applyGlobalModifier` (`BuildingProjectStats.cs:115-119`) only registers with `PlayerStats`, and no game-logic caller reads `GlobalStatModifierID.TurnYieldPopulation`, whereas energy's global modifier is consumed at `PlayerResourceHandler.cs:29`. Sol's withdrawal was right. The 3-copy cap stands on slot economy alone." Facilitator state can move from UNVERIFIED to "no consumer found on the growth path (A re-checked); treat as inert".

3. **Agreement 4, "A did not contest that":** replace with "A withdraws 2,900-3,600: it double-counted accuracy (the logged 7.65 and 10.007 already include the 0.85 hit rate, 9 × 0.85 = 7.65) and included T23, when no city was visible. A accepts Sol's figure of about 2,550 (T21 7.65 plus T22's 11 guided × 0.85, × 150)."

4. **Agreement 8, "Neither lane treats the 40 unpaid events as 40 distinct stoppable impacts":** not true of laneA A6, which read them as "~30 more stops at p 0.8, 17 fewer lost structures". Replacement: "Lane A's A6 arithmetic read the 40 unpaid rolls as about 30 lost stops; A withdraws that as an upper bound, since overlapping defenses each report an unpaid roll for the same impact (`Director.cs:437-443` caps them at eligible impacts). A's floor formula itself was sized on the 56 in-range impacts, not on rolls."

5. **N8, "accepted by A in its own amendment 3":** amendment 3 conceded only the upkeep. Replacement: "Accepted by A at sign-off, after verifying Telescope Array 200 E / 300 M / 8 intel / 20 E upkeep (`BuildingsData.csv`) and Lidar 50 E / 175 M / intel ×1.5 / upkeep ×1.6 (`UpgradesData.csv`). The 3,000 M and 200 E/turn saving was wrong."

6. **Agreement 10, "Fable called it a blocker because of the cheat risk":** precision only. Replacement: "Fable: REJECT for v0.16, and a blocker if ever implemented without a reservation-reconciliation test, because its failure mode is a resource cheat."

7. **S1 handling, "A may object if the new opening's extra Jamming science changes the arithmetic":** A reserves no such objection. Jamming requires EMC, so it cannot move EMC earlier. A verified the T1 non-credit (`PlayerResourceHandler.cs:161-186`: the science credit sits inside `CurrentTurn > 1`) and accepts T13 as the earliest completion on the frozen history and T14 as the gate. "EMC head by T10" stands (a head can be set before the science exists); "done by T12" is withdrawn.

Stated fairly and accepted as written: S2 (my `energyLeft > 300` gate is withdrawn, B is right that it rewards unspent energy; 2.0 at war deferred because the additive upkeep term in `WantedEnergy` makes it overshoot the human's 2.05 total-income ratio), S3 (I read "guided passing is at least 6" as the expected passing shots from guided weapons, the `d.Passing` quantity that `MinPassingShots` tests at `Strategy.cs:246`; I accept the combined predicate), S4, S6 (verified: the T27 director record logs `observedNeed` 9,250, so my 7,000 floor is redundant), S7 (verified: human telemetry line 123, T20, intel 77 at counterintel ×5.35), sections 2 (N1, N2), 3 and 6 wherever they cite A.

## 2. Non-negotiable and blockers

- **Non-negotiable (observable inputs only; no threshold or memory from enemy counterintel, hidden positions or frozen snapshots):** present in agreement 7, agreement 10 (last two bullets), item 5's gate "Runtime decision inputs contain no hidden counterintel, stealth or surviving-city values", the N3 → P2 replacement, and the outcome gate's "human telemetry never at runtime". Handled.
- **Blocker 1, latched roster and reservation release:** agreement 10 and item 4 "Unchanged". Handled.
- **Blocker 2, any threshold from the enemy counterintel multiplier:** agreement 7, including my A5 withdrawal. Handled.
- **Blocker 3, memory of city positions after they leave sight:** agreement 10. Handled.

No omission.

## 3. Factual notes and Pavol-reserved choices (recorded, not binding)

1. **N5 conflates two records.** The T26 director record shows `observedNeed` 1,250 and `defenseReserve` 3,080; the 9,250 is in the T27 record, logged after the 83-impact volley. On the volley turn the reserve exceeded the estimator's ask, so the binding limit there was the estimator's one-phase lag, not the money order; item 6's reordering protects the second volley, and the outcome gate's "survive the first mass volley" needs a forward signal from observable inputs (e.g. after the first enemy impacts, fund the in-range Kinetics' full roll capacity).
2. **Lidar per intel point costs more material and upkeep than a new array** (43.75 vs 37.5 M; 3.0 vs 2.5 E/turn) but less build energy (12.5 vs 25 E); item 5 should value it on build energy at the window and array count, not on savings.
3. **P1:** my position remains (a); I accept (b) as Pavol's call provided the loss-per-1,000-E metric is logged from the first blind salvo and Ultracapacitors plus Composite Heat Cells (1,550 science) never jump ahead of EMC, Nano, Microwave, Lossless and Plasma in the plan.
4. **P2:** (a) is compatible with my non-negotiable only if the remembered count sizes the intel budget and never feeds targeting or position inference; write that condition into (a). (c) is the cleanest if (a) is contested.

## 4. Verdict line

CONFIRM @ 888f8bb0ad1c1eb6ee38a3164a68a9b1e51ce0a66efb229c7a76d277158d63fe
END-OF-REPORT
