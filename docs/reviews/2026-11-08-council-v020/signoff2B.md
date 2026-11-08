Hash matches. This is a sign-off for a lane whose revision-2 prompt should match one of the pending ones (D or E, since 2A–2C are done). Let me identify my lane identity from the prompt files, then read the record cold.
The prompts are identical — lane identity isn't in the prompt. Let me check how lanes were assigned and read the record cold.
I'm Lane B (GLM-5.3 Flash). Let me read my lane materials to check fidelity.
I'm Lane B. My round-1 sign-off raised one material finding (I2 pre-T12 clause) plus four minor ones. Revision 2 looks like it applied them — let me verify the newly added code citations (S1's `HoldTurns` caveat) before finalizing.
All checks complete. Report follows.

---

# Sign-off report, revision 2 — Lane B (GLM-5.3 Flash)

**Hash:** `sha256sum decision-record.md` → `5d5b648b…f727`. Matches. Read cold, then checked against my lane artifacts (`laneB.md`, `xreviewB-on-C.md`, `ballotB.md`) and my round-1 sign-off (`signoffB.md`).

## Findings

1. **Round-1 material finding applied correctly (I2 pre-T12 clause).** Rev 2 §5 now reads: latch from T12 agreed 5/5; before T12, hysteresis (B, C, D, E) with A's dissent stated inline and again in §10; the §5 header says "except I2's pre-T12 clause at 4-1". This matches the ballots I cited in round 1 (`ballotB.md:34`, `ballotC.md:49`, `ballotD.md:59`, `ballotE.md:61`; only `ballotA.md:32` dissenting). The majority position is restored and the split is no longer hidden. **Resolved.**
2. **Round-1 minor #3 applied (upkeep guard scope).** I2 now keeps "the pre-schedule `RetaliationUpkeepShare` and the pre-war defense posture" with "(no full-war upkeep or posture before the schedule; B)" — both halves of the side effect, not just the share. **Resolved.**
3. **Round-1 minor #4 applied (I1a gate failure state).** The I1a gate now ends "…is UNTESTED: fix it and replay before judging any behaviour change." **Resolved.**
4. **Round-1 minor #2 applied in substance (FA4 partition).** The explicit "1 was neither" clause was not restored, but the arithmetic is closed: 39 + 12 = 51 of 52, and "the constraint on the other 40 is unresolved" correctly covers the 39 low-material turns plus the one remainder; every number `verify_gates.py` must reproduce is stated. **Non-material; satisfied.**
5. **Round-1 minor #5 adequately handled (per-gun outcomes for I8's secondary metric).** I8 now sources observed impacts from the defense ledger with an explicit UNTESTED fallback if the join fails, so the metric can no longer be treated as measurable when it is not. **Satisfied.**
6. **Minor, non-material — ambiguous antecedent in §3 (B entry).** Four REJECT bullets are listed, then "Both claims are excluded from this record and from every gate." The intended pair is presumably the two A-attributed claims in the last two bullets; §10's dispositions (B1→FA10, B2→excluded, B3→not used, B4→excluded, FA4 carries the measurement) make traceability complete regardless. Wording nit only.
7. **Note, non-material.** §2's read-only process claim (untracked digest `a2f2188c` unchanged) remains a facilitator attestation I cannot independently re-verify after the fact; it is properly framed as a claim about process, not a game fact.

**New-content checks (nothing new is wrong):** the new S1 citations verify at the source — `AI/StrategyPatches.cs` sets both `LastStrikeTurn` and `HoldTurns = 0` under `if (fired > 0)` inside the cited 127-135 range, and `AI/Brain/Strategy.cs:395` is the `pulseReady` gate (`turn - lastStrikeTurn >= Math.Max(1, s.StrikeEvery)`; the record's "(2)" is the configured default). Every gate now has a next state in all three outcomes (§7's PASS/FAIL/UNTESTED lattice, plus item-level UNTESTED clauses for I1a, S1, S2 and I8, with §6 covering behaviour-change failures). FA10 matches my ballot verbatim (m23 64, m24 78 correct, m25 107, m26 Ash 203 / lobby 229); my I1 cross-tab wording, I5 deferral, accuracy non-negotiable, backup item and turn-semantics note all persist in I1a/I1b/§6/CF1. The "both claims excluded" handling no longer lets any rejected B claim into a gate. My S2 placement (v0.20 column, cross-family) and S1 placement (v0.20 column) are correct.

## Positions on the two open details

**(a) I2 latch scope — shared state with the upkeep guard (the facilitator's proposal).** My ballot's amendment presupposed shared state: the guard exists precisely because a shared damage-latch would otherwise shift director posture up to six turns early. Rev 2's guard now pins both the upkeep share and the pre-war defense posture, which closes my round-1 gap. Strategy-only for v0.20 would defer the exact side effect E flagged into unobserved territory; and since E accepts either, this is not even a live split. Implement the shared form with the guard.

**(b) S1 resolution — support: ship I3 in v0.20 with late pulse-wait volleys not updating `LastStrikeTurn`.** It neutralises A's only FA9 objection while capturing the verified energy opportunity my ballot accepted (Donnels T33: 8,828 left vs 5,960 cost). The code check confirms it is clean: `LastStrikeTurn`'s only consumer is the `pulseReady` gate. One implementation specification the record leaves open, and which I recommend fixing in the I3 task text: the same `if (fired > 0)` block also resets `HoldTurns` (`StrategyPatches.cs:129-133`), so the exclusion should skip **both** assignments for late pulse-wait volleys — otherwise the max-hold decision state still shifts even though cadence is preserved. The record's pulse-cadence and max-hold gates will catch a miss either way, and I1a's pulse-wait eligibility logging makes it observable.

## Decision

CONFIRM decision-record.md @ sha256:5d5b648b20460919e8d4cf39edadc06285dfbf4d308a1dae6bfe68d71a44f727

verdict: Revision 2 faithfully preserves lane B's report, cross-review, ballot and round-1 sign-off findings — the material I2 hysteresis split is restored and recorded as 4-1 with A's dissent, the posture guard, I1a failure state and FA4 rewording all landed, and the record's new claims (including the S1 `HoldTurns` caveat) verify against the code; the two open details are preferences I support on the facilitator's side of both, with one implementation note (skip the `HoldTurns` reset along with `LastStrikeTurn`).

END OF REPORT
