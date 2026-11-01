# COUNCIL RESULT: Smarter AI v0.16

**State: CONSENSUS RECORD, with CROSS-FAMILY CONCURRENCE across two families (Anthropic and OpenAI).**

**Signed record:** `decision-record-r2.md`, sha256 `ef9657c24b94b45b97658e06300f8b00406bf9d10f6b00e4f546ca4d27404691`.

| Lane | Model | Revision 1 (`888f8bb0`) | Revision 2 (`ef9657c2`), the retry |
|---|---|---|---|
| A | Fable, xhigh | CONFIRM, with 7 corrections (`signoffA.md`) | CONFIRM (`signoff2A.md`) |
| B | GPT-6.1 Sol, xhigh | OBJECT on attribution (`signoffB.md`) | CONFIRM (`signoff2B.md`) |
| C | Gemini 4 Argon | recused (charter, Amendment 1) | |

**Integrity:**
- HEAD stayed at `24683684` throughout.
- Git porcelain was byte-identical to `status-before.txt` after every pass.
- 6 of 6 data files match the manifest.
- Every file is sealed in `seals.txt`.

**Budget:**
- One sealed pass, one cross-review and one sign-off.
- The retry is the single retry the charter allows per lane.
- No lane failed. Lane C was recused before evidence, with Pavol's approval.

**What CONFIRM means:** the record faithfully states the agreements, the facts and the splits. It does not mean each lane adopts every choice. Lane A's dissent on S4 (the blind-phase posture) stands.

## Notes raised at the retry (not in the signed record)

These carry into implementation as notes. Editing them into the record would have changed its hash and voided both sign-offs.

| From | Note | Implementation consequence |
|---|---|---|
| A, 1 | A's non-negotiable, in its full sealed wording: "No threshold or memory derived from the enemy's counterintel multiplier, hidden positions, frozen snapshots or the true surviving-city count." | Add a code comment and a test. |
| A, 2 | Item 2's sources also include A3, the economy-first order (laneA §3.5). | Attribution only. |
| A, 3 | **A withdraws "the estimator lag was binding at T26".** The T26 reserve of 3,080 equals the recovery cap exactly (`Director.cs:448-452`, `DirectorPatches.cs:513-519`); the planned term alone (4,417) already exceeded it. So the money order bound T26. The forward signal still matters once the reordering raises the room, because `DefenseReserveShare` caps the planned term at 4,417 (0.3) or 5,890 (0.4) against the 5,600-8,400 needed. | Item 6: build the reordering first; the forward signal is a complement. |
| A, note 1 | T28's "0 unpaid" is coverage collapse, not funding success: there were 63 no-eligible-defense impacts and own defenses fell from 42 to 20. | Gate: count unpaid rolls together with `lastNoEligibleDefense`. |
| A, note 2 | Item 1's 15,000 E: T20 is the pass, and T18 is logged as a stretch gate so a slow SSTAR ramp can show. | Gate. |
| A, note 5 | The mod reserves 150 E per roll (`DirectorPatches.cs:514`); A6 and agreement 8 assumed 100. | Use the live per-roll cost in the diagnostics. |
| B, 3 | **Forward signal: AMEND.** Define "full roll capacity" as an explicit, bounded live-cost reserve request, with an incoming-impact and overlap assumption and a logged funding shortfall. Native defenses roll again on later impacts, and each attempt costs energy and material (`PlayerDefenseHandler.cs:223-247`, `:264-287`). | Item 6 design. |

## Reserved for Pavol

The options are in decision-record-r2 §5.

| ID | Choice | Recommendation |
|---|---|---|
| P0 | Build v0.16 | All six items. |
| P1 | Blind-phase posture | (b) artillery primary, with A's two conditions. |
| P2 | Replacement for `UnseenCities` | (c) no count, own visibility results only. Both lanes prefer it. |

## Pavol's decisions (2026-11-01 19:2x)

| ID | Decision |
|---|---|
| P0 | **Build all six items**, plus the `UnseenCities` replacement. |
| P1 | **(b) Artillery primary** in the blind phase, with lane A's two conditions: log loss per 1,000 paid firing E from the first blind salvo; Ultracapacitors and Composite Heat Cells never ahead of EMC, Nano, Microwave, Lossless and Plasma in the plan. |
| P2 | **(c) No count.** Intel spending runs on own visibility results only; no enemy city count is used or remembered. |
