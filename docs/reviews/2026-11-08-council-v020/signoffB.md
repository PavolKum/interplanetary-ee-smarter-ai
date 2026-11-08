Hash verified. Reading the record and the folder contents.
Hash matches. I'm running as GLM-5.3 Flash, so my lane is **B**. Reading my lane's report, cross-review, and ballot to check fidelity.
My lane's three artifacts check out against the record so far. Now verifying the code-cited facts and the FA4 arithmetic before forming findings.
Record's FA4 says 39 + 12 = 51 of 52 — the matrix's "1 was neither" clause appears dropped. Checking that plus the `LastStrikeTurn` update site and the "21 of them also at the cap" provenance.
FA4's "21 also at the cap" traces to E's cross-tab — verified. But the record's I2 reads "Before T12, behaviour is unchanged" while the matrix I2 (which my ballot accepted) had pre-K hysteresis. Checking whether the ballots superseded that.
The I2 pre-T12 clause is a real fidelity problem: ballots B, C, D, E all kept the matrix's pre-K hysteresis; only A's ballot dropped it — and the record adopted A's wording as "agreed, 5/5". Checking the remaining disposition claims (A-on-B blockers) before finalizing.
All checks done. Assembling the sign-off report.

---

# Sign-off report — Lane B (GLM-5.3 Flash)

**Hash:** `sha256sum` on `decision-record.md` → `d01bdb5b…c8423`. Matches. Proceeded cold, then consulted my lane's artifacts (`laneB.md`, `xreviewB-on-C.md`, `ballotB.md`) and the matrix/ballots for cross-checks.

## Lane-fidelity check (B)

- §2 roster: correct (GLM-5.3 Flash / Vercel / Zhipu; `xreviewB-on-C` on C; `ballotB`).
- §3 B entry: faithful — the three REJECTs are exactly xreviewA-on-B's blockers B1–B3, and my ballot adopted them as self-corrections. B4 ("energy never bound" replaced by phase-aware measurement) is honored by omission: the record nowhere repeats the claim, and FA4 is the replacement. The §10 disposition line ("B1-B4 → FA10 and the CF1 handling") is loose on B4 but not wrong.
- My ballot outcomes all preserved: FA10 rewording adopted verbatim (m23=64, m24=78 correct, m25=107, Ash 203/lobby 229); I1 cross-tab idleReason → I1a "independently observable idle constraints ... not a single label"; I5 to v0.21; I12 scope; missing-issue #2 (turn semantics) → CF1 "Turn semantics" line; missing-issue #3 (backup) → I1b "durable backup + sha256 manifest"; my non-negotiable (accuracy ratio) → §6 "any accuracy change" + S2's v0.21-arm argument. I recorded no dissents; none needed preserving.

## Findings

1. **MATERIAL — I2's pre-T12 clause contradicts 4 of 5 ballots while labelled "agreed, 5/5."** Matrix I2 (the shared input all ballots voted on) says: "Before K, require 2 consecutive sub-threshold turns before reverting (hysteresis)" (`matrix.md:80`). Ballots B, C, D, E explicitly kept it: `ballotB.md:34` ("the hysteresis ... accepted"), `ballotC.md:49` ("pre-K hysteresis"), `ballotD.md:59` ("K = 12, hysteresis before K"), `ballotE.md:61` ("per I2's K and hysteresis rules"). Only `ballotA.md:32` replaced it ("before T12 retain current behavior"). The record adopted A's wording — "Before T12, behaviour is unchanged" (`decision-record.md:141`) — and the word "hysteresis" appears nowhere in the record, nor is the 4–1 split listed in §10. This is not one of the two designated open details; it is a silent majority-to-minority override inside an item marked 5/5, and it changes v0.20 behaviour (pre-T12 damage wars revert immediately, so the exact flip-flop FA1 documents continues below T12 undamped and untested).
2. **Minor — FA4 partition incomplete.** Record FA4: 39 low-material + 12 at-cap-with-material = 51 of 52. The missing fourth cell exists in the sources: "1 was neither" (`matrix.md:25`; xreviewE-on-A cross-tab: below-cap-with-material-available = 1, `xreviewE-on-A.md:62`). Since I1b's `verify_gates.py` "must reproduce the FA4 numbers," the incomplete partition makes the reproduction target ambiguous. Non-material to any decision; restore the clause.
3. **Minor — I2's upkeep guard is narrower than ballot B's.** My ballot asked to keep "the retaliation upkeep share **and pre-war defense posture**" pre-schedule; the record's guard names only `RetaliationUpkeepShare` (matching ballot A). The defense-floor/posture half of the side effect E flagged (xreviewE-on-A C1) is unpinned. Non-material given I1a logging, but should be restored or the posture shift explicitly logged.
4. **Minor — I1a's gate has no failure state.** §6's stop/revert list covers behaviour changes; I1a is instrumentation, so a cardinality-gate failure has no stated next state. One sentence would close it (fix logging before behaviour experiments).
5. **Minor — my ballot's missing issue #1 has no explicit disposition.** The "effective volley" term was properly removed from I8's gate (reworded to planned/observed hits by T26), but "observed hits on the human" per-target remains only measurable if I1a logs per-gun *outcomes* — I1a currently logs "target, facing bucket, found" (found ≠ hit). Flag for the I1 design pass; not material.
6. **Note — §2's read-only process claim** (untracked digest `a2f2188c` unchanged, no tracked file changed) is stated but not independently verifiable by this lane after the fact. Not material.

## Positions on the two open details

**(a) I2 latch scope — support the facilitator's proposal: shared state with the upkeep guard.** My ballot's amendment presupposes it (the guard exists precisely because a shared latch would otherwise shift director posture up to six turns early). One strengthening per finding 3: the guard should pin the whole pre-war director posture (upkeep share *and* defense floor), or at minimum log the posture so the unmeasured side effect is observable in v0.20 data.

**(b) S1 resolution — support: ship I3 in v0.20 with "late pulse-wait volleys do not update `LastStrikeTurn`."** It neutralises A's only objection (FA9) while capturing the verified energy opportunity my ballot accepted (Donnels T33 8,828 vs 5,960 cost). Code check confirms it is clean to implement: `LastStrikeTurn` is set at `AI/StrategyPatches.cs:129` and its only consumer is the `pulseReady` gate at `AI/Brain/Strategy.cs:395` (via `StrategyPatches.cs:270`) — no other consumer exists. Two cautions for the implementation: the same block also resets `HoldTurns` (`StrategyPatches.cs:130`), and the record does not say whether a late pulse-wait volley resets it; specify that, and let the pulse-cadence gate plus I1a's pulse-wait eligibility logging catch regressions.

## Summary of the record otherwise

Everything else checked faithful: the FA10 corrections match my ballot exactly; S1/S2 lane attributions and cross-family labels are correct (B is correctly in both the S1-v0.20 and S2-v0.20 columns); dissents (D: I5; A: I3; A+E: I4) are preserved; FA9's citation verifies at `Strategy.cs:395`; CF1's numbers match ballot A's verified figures; and the S2/S1 evidence strings trace to my own cross-review and ballot.

## Decision

OBJECT decision-record.md/§5-I2 @ sha256:d01bdb5b59637ef3491ac8fd864b6fe9f2efd8d0030615772947ca8946cc8423: I2's "Before T12, behaviour is unchanged" silently replaces the pre-K hysteresis that ballots B, C, D and E explicitly accepted (ballotB.md:34, ballotC.md:49, ballotD.md:59, ballotE.md:61) with only ballotA.md:32 supporting the record's wording, yet §5 labels I2 "agreed, 5/5" and §10 records no such split — restore the hysteresis or re-label I2 as 4-1 with A's amendment adopted and the choice surfaced to Pavol alongside the two open details.

verdict: The record is faithful to lane B everywhere except I2's pre-T12 clause, where a 4-1 ballot split is recorded as unanimous agreement; with that one clause fixed (or the split surfaced), I would confirm without reservation. Findings 2-5 are minor and non-blocking.

END OF REPORT
