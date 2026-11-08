# Amendment 1 - two do-no-harm gates (2026-11-08)

**Decision:** Pavol, 2026-11-08, after match 27: "Yeah, yeah, you can amend both."
**Applies from:** match 28. Match 27 stays on record as FAIL on both gates under the original wording. The amended
re-score below is reported alongside that result; it does not replace it.
**Not changed:** the decision record (rev 2, sha256 `5d5b648b...`) and its sign-offs. This amendment changes how two
section-6 gates are counted, not their thresholds.

## 1. HUMAN-FIRST share (threshold unchanged: >= 85%)

- **Was:** the share of fire decisions carrying the `humanFirst` flag.
- **Now:** the share of fire decisions whose `commit` is the human. Return fire (`reason == return-fire`) at a
  non-human attacker is excluded from the denominator and reported on its own INFO line, with the old flag share.
- **Why:** the flag is not set on attrition volleys, so three volleys at the human counted against the gate in match 27.
  Returning fire on an AI that attacked first is v0.16.2 design, not neglect of the human. An attrition or free-fire
  volley at another AI still counts against the share.

## 2. Reserve shortfall (threshold unchanged: <= 5%)

- **Was:** the share of all war director records with `reserveShortfall > 0`.
- **Now:** the same share over non-collapse war records. A collapse record is a turn on which the AI's total building
  count (the economy record's `buildings`, summed) is below 80% of its peak so far. The raw share, including collapse
  turns, is reported on an INFO line.
- **Not changed:** the covered-buildings gate still reads every war record.

## Match 27 under both wordings

| Gate | Original | Amended |
|---|---|---|
| HUMAN-FIRST | FAIL, 65% of 23 | PASS, 95% of 19 (4 Brain return-fire volleys at Simmons excluded; 1 Brain attrition volley at Simmons, T24, counts against) |
| Reserve shortfall | FAIL, 12.2% of 41 | PASS, 3.3% of 30 (11 collapse records excluded; 1 real shortfall left: Analog T32, 102 energy, 199 of 202 peak buildings) |

## Checks

- `python AI/tools/verify_gates.py --selftest`: 20/20, 6 of them new.
- The 3 new checks that encode the amendment fail on the pre-amendment code (return fire, unflagged volleys at the
  human, a collapse-turn shortfall); the other 3 pass on both.
