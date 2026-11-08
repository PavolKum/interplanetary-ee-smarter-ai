# Sign-off D (Fable 5.1) on decision-record.md, revision 2

Hash check: `sha256sum decision-record.md` -> `5d5b648b20460919e8d4cf39edadc06285dfbf4d308a1dae6bfe68d71a44f727` (matches; 321 lines).
Read cold; checked against laneD.md, xreviewD-on-E.md, ballotD.md and my round-1 sign-off; new numbers in this revision
re-derived from the decision logs with one read-only scratch script.

## Findings

1. **Round-1 findings applied correctly** (not material, both confirmed). Section 3's lane D bullet now reads "about a third of
   paid trajectories are not recorded at the human (including planned accuracy misses; see FA5)", which is what I asked for.
   The I2 gate carries "`aimedWeapons` does not fall on latched turns (D)". Section 11 records both as applied.
2. **Faithful to lane D.** FA4 keeps my overlap split (39 / 21 / 12); FA5 keeps my own/none objection, now qualified as an
   inference until I1a logs the classification, which is more careful than my ballot wording and correct (`fire-paid` has no
   hit/miss flag; `Patches.cs:605` labels non-player bodies "own"). FA10 keeps m24 = 78. CF1 keeps my turn semantics and the
   replacement finding marked unverified; I13 attributed to me with "investigate first". Section 10 preserves my dissent (I5 in
   v0.20) and the I2 hysteresis position (B, C, D, E). Nothing misattributed.
3. **New numbers re-verified** (not material; all hold):
   - FA4 "186 of the 193 war-phase turns war-flagged": reproduced (186; the 7 unflagged are the peace turns of the 4 flips).
   - FA4 "97 of 141 non-idle war turns (69%) ended with material < 2,000": reproduced (97 / 141 = 69%). The base-rate correction
     is right and my ballot's FA4 wording was incomplete without it; the revision's "constraint on the other 40 is unresolved"
     is the honest reading.
   - S2 condition "m25 T23 shape (Zenon `primaryFound` 3, `secondaryOnly` 22 of 30)": reproduced from the `fire` record
     (retained 30, primaryFound 3, secondaryOnly 22, plannedHits 21, plannedMisses 9).
   - S1 "firing also resets `HoldTurns`" (`StrategyPatches.cs:127-131`): confirmed in round 1.
4. **One count is off by the denominator** (not material; direction holds). I9 says "`unplaced` dominates 6 of 9 AI-lobbies,
   `noAnchor` dominates Eddie and Ash". Summing `cover-search` tries per AI over the five matches: all 10 AI-lobbies have records;
   `unplaced` dominates 8 (Eiai, Beastie, Arc, Doc, Zenon, Donnels, Sarai, Hawking) and `noAnchor` dominates 2 (Eddie 19,018 vs
   3,592; Ash 25,886 vs 19,152). Suggested wording: "8 of 10 AI-lobbies ... Eddie and Ash". The bimodal conclusion and the
   fix-by-measured-reason instruction are unchanged.
5. **Gates and next states.** Every v0.20 item and both splits now have PASS / FAIL / UNTESTED with a next state (section 7
   adds the generic next states; I1a has the UNTESTED-then-replay rule; S1 and S2 have UNTESTED thresholds; I8 names its primary
   and secondary metrics with an UNTESTED path for the join). No internal contradiction: sections 1, 5, 8 and 9 agree on what
   ships (I1, I2), what splits (S1, S2) and the 4-1 pre-T12 clause.
6. **Facts without a second verifier are flagged** (CF1 replacement, I13 Low; FA4 causal reading Low). Section 2's porcelain
   digest is the facilitator's own check, now with its limits stated; not load-bearing.
7. **Authority limits.** No step exceeds read-only or the council's authority; every behaviour change is agreed with gates or
   reserved for Pavol.

## Positions on the open details (preferences, not blocking)

- **(a) I2 latch scope: shared state with the upkeep guard.** Weapons are bought only while the *director* is at war
  (`DirectorPatches.cs:877, 904`); a strategy-only latch would have left Ash at 5 Railguns under `MinWeapons` 6 with purchases
  halted at T19, which is the cost I measured. The `RetaliationUpkeepShare` 0.15 guard and B's pre-war posture guard keep the
  latched retaliation from becoming the premature-war trap. The revision's default ("shared form with the guard unless Pavol
  prefers strategy-only") matches my position.
- **(b) S1 resolution (late pulse-wait volley does not update `LastStrikeTurn`): accept as the v0.20 form, now correctly
  framed as a hypothesis with the max-hold check.** One observation for I1: the current late volleys already reset the pulse
  (Zenon m25 `lastStrike` 25, 26, 27, 28 after late volleys T25-T28), so the rule changes behaviour only on pulse-wait turns; and
  the value of `StrikeEvery` = 2 for an AI whose energy does not carry over is itself unmeasured (guns fire once a turn; the
  defender's roll budget is per action phase). I1a's pulse-wait eligibility log should let v0.21 test whether the pulse is worth
  keeping at all.

CONFIRM decision-record.md @ sha256:5d5b648b20460919e8d4cf39edadc06285dfbf4d308a1dae6bfe68d71a44f727

Sign-off D verdict: revision 2 applies my round-1 findings, preserves lane D's facts, errors and dissent, gives every gate a next
state, and its new numbers reproduce (one denominator in I9 should read 8 of 10, not 6 of 9, without changing the conclusion);
confirmed, preferring the shared-state latch with the upkeep guard and the cadence-preserving S1 rule as a tested hypothesis.

END OF REPORT
