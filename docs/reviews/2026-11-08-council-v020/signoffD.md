# Sign-off D (Fable 5.1) on decision-record.md

Hash check: `sha256sum decision-record.md` -> `d01bdb5b59637ef3491ac8fd864b6fe9f2efd8d0030615772947ca8946cc8423` (matches; 260 lines).
Read cold, then checked against laneD.md, xreviewD-on-E.md and ballotD.md, with four new read-only checks (below).

## Findings

1. **Faithful to lane D** (not material). Section 3 lists my four surviving findings and both errors (CF1 "only when broke"
   refuted and conceded; `weaponUpgrades` per-turn misread). FA4 carries my overlap wording (21 of the 39 material-short idle
   turns also at the cap; 12 cap-only). FA5 carries my own/none-are-accuracy-misses objection. FA10 carries the m24 = 78
   correction. CF1 carries my turn semantics and my replacement finding, correctly marked "unverified by another lane"; I13 is
   attributed to me with "investigate first". Section 10 preserves my dissent (I5 in v0.20) and both blockers I raised on E
   (cap gate withdrawn into I6; war-timing facts in FA2 and I8). Nothing I said is misattributed.
2. **Three record numbers re-verified** (not material, all hold):
   - CF1 "turn_end T23 holds 10,913": `v019m26-human-telemetry.jsonl`, turn 23 `turn_end` state `resources.energy` = 10,913,
     `round_start` 15,513; population 2,601 at both. So the rolls of action phase T23 spent at most 2,800 (about 18 rolls) and
     8,113 remained: the refutation of my O5 stands.
   - FA4 "7 of the 39 still had >= 200 material": reproduced (39 idle turns with `materialLeft < 2,000`; 7 at >= 200).
   - FA9 "a late volley sets `LastStrikeTurn`": `StrategyPatches.cs:127-131` (`if (fired > 0) s.LastStrikeTurn = s.Turn`) and
     the m25 log: Zenon late volleys T25-T28 show `lastStrike` 25, 26, 27, 28 on the following turns. This is the premise of S1
     and it is correct.
3. **One stale phrasing** (not material). Section 3, lane D bullet: "35-42% of paid shots do not reach the human" is what my
   report said, but after FA5's rewording the defensible waste is the 10-17% to fellow Smarter AIs plus the secondary fill; the
   own/none 17-18% are accuracy misses. Suggest "10-17% of paid shots go to fellow Eiais" if the bullet is ever edited.
4. **One ballot amendment not carried** (not material). My ballot asked I2's gate to add "`aimedWeapons` does not fall and
   energy is spent on every latched war turn with `energyLeft >= 400`", because the latch keeps `TryWeapon` running
   (`DirectorPatches.cs:877, 904`) but does not fire a 5-gun arsenal (`MinWeapons` 6; Ash "building" at T20-T21). The record's
   I2 gate (red-before-fix test; zero reversions with an observed opportunity, else UNTESTED) is complete and can fail, so
   this is a preference, not a defect.
5. **Gates and next states.** Every v0.20 item has PASS / FAIL / UNTESTED semantics and a next state: I1a cardinality gate,
   I1b doctored-log FAIL requirement (this answers the circularity I flagged on E's R1 gate d), I2 UNTESTED path, S1 fallback
   "instrumented in v0.20, tested in v0.21", S2 split to Pavol, section 6 stop-or-revert gates with E's measured baselines.
   No internal contradiction found: section 1, section 5 and section 8 agree on what ships, what splits, and who decides.
6. **Facts stated without a verifier.** CF1's replacement finding and I13 are correctly flagged Low / one lane. The section 2
   read-only digest (`a2f2188c`) is the facilitator's own check and cannot be verified from my side; it is not load-bearing.
7. **Authority limits.** No step in the record exceeds read-only or the council's authority: all behaviour changes are either
   agreed 5/5 with gates or reserved for Pavol (S1, S2, Form B, protocol, no-action).

## Positions on the open details

- **(a) I2 latch scope: shared state with the upkeep guard (A's wording), not strategy-only.** Evidence: weapons are bought
  only when the *director* is at war (`DirectorPatches.cs:877, 904`); with a strategy-only latch, Ash's T19 would still have
  stopped buying at 5 Railguns (under `MinWeapons` 6) and the strategy would have said war with nothing new to fire. The cost
  I measured (Ash's 5 and Eddie's 6 guns idle for 1-4 turns, purchases halted) is in the director. The pre-schedule
  `RetaliationUpkeepShare` guard (0.15 vs 0.4) keeps the latched retaliation from becoming a full early war, which is the
  trap lane C named.
- **(b) S1 resolution (late pulse-wait volley does not update `LastStrikeTurn`): accept it as the v0.20 form.** It is the
  conservative choice: the planned pulse cadence is unchanged and the late volley is strictly additional, paid from energy
  verifiably left (Donnels T33 8,828 vs 5,960; Zenon T33 6,544). Two notes for I1: (i) the record's own evidence shows the
  current late volleys already reset the pulse (Zenon T25-T28 fired every turn via the "below-saturation" path), so the
  cadence-preserving rule changes behaviour only on pulse-wait turns; (ii) the value of `StrikeEvery` = 2 for an AI whose
  energy does not carry over is itself unmeasured (guns fire once a turn regardless; the defender's roll budget is per
  action phase), so I1 should log arriving shots per pulse turn vs per late turn and let v0.21 decide whether the pulse is
  worth keeping at all.

CONFIRM decision-record.md @ sha256:d01bdb5b59637ef3491ac8fd864b6fe9f2efd8d0030615772947ca8946cc8423

Sign-off D verdict: the record preserves lane D's findings, errors and dissent accurately, every gate has a next state, and
the only facts without a second verifier are flagged as such; confirmed, with a preference for the shared-state latch and
the cadence-preserving S1 rule.

END OF REPORT
