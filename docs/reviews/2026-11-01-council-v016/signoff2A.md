Verdict: CONFIRM

# Lane A sign-off on decision-record-r2.md (retry)

Hash check: `decision-record-r2.md` sha256 = `ef9657c24b94b45b97658e06300f8b00406bf9d10f6b00e4f546ca4d27404691` (matches). Section 0 re-checked: HEAD is `24683684d979…`; porcelain is byte-identical to `status-before.txt` (five untracked paths); all seven seals in `seals.txt` recompute, including `signoffA.md` `afa5770c…6d19`; the six data files match `data-manifest.txt`. No file was written or changed by this pass.

## 1. Lines stating my position, numbers or withdrawals

Every line that quotes me was checked against `laneA.md`, `xreviewA.md` and `signoffA.md`. Agreements 4, 7, 8 and 10, facts N1, N2, N4, N7, N8 and N9, items 1, 4, 5 and 6, splits S1 to S7, and P1 and P2 state my positions, numbers and withdrawals in my own words. All seven corrections I asked for in `signoffA.md` were applied as written (section 8 cites them correctly). Three precision items follow; none is grounds to object.

1. **Agreement 10, last bullet (line 76), labelled "A's non-negotiable", is a partial paraphrase.** It drops the memory clause from my sealed wording (`xreviewA.md:68`). The omitted part is covered by the preceding bullet and by item 5's runtime-input gate, so it is handled, but the label should sit on the full text. Replacement I accept:
   > **Hidden enemy values at runtime.** Every new rule runs on observable inputs only: own spend, own visibility results and currently visible candidates. No threshold or memory derived from the enemy's counterintel multiplier, hidden positions, frozen snapshots or the true surviving-city count. (A's non-negotiable.) The aggregate-count memory under P2(a) is permitted only under the condition recorded in section 5.

2. **Item 2 "Sources" (line 128) omits A3.** The adopted quotation is B's I4 amendment text (`xreviewB.md:450`), and `issues.md` lists I4's sources as "A3, B2, B6": the economy-first order (Super Tensile, EMC, Nano, Microwave, Lossless early) is `laneA.md` §3.5 and A3. Replacement I accept:
   > **Sources:** an adopted synthesis of A3 (the economy-first order, laneA §3.5) as amended by B's I4 text, B5/B6, and A's research-hijack guard (N1, N4, N6). B's proposal to advance Ultracapacitors and Composite Heat Cells is not adopted here; it is preserved under S4/P1.

3. **N5 and S6: not a misstatement, a correction to my own position.** The record quotes my sign-off note faithfully ("the binding limit on the volley turn was the estimator's one-phase lag"). At this retry I read the code and B is right. `DefenseReserve` returns max(planned, observed) (`Director.cs:433-434`), the call site then clips it with `CapReserveForRecovery` (`DirectorPatches.cs:513-519`, `Director.cs:448-452`), and the logged T26 reserve 3,080 (`v0153-ai-decisions.jsonl:169`) equals the recovery room exactly: 14,725 − 5,100 − 3,600 − 0.2 × 14,725. The planned term alone (42 defenses × 150 E, capped at 0.3 × income = 4,417) already exceeded that room, so a 9,250 ask would have been clipped to 3,080 as well. I withdraw "the lag was binding". Replacement for the A bullet in N5:
   > • A: the estimator lagged one phase and asked for only 1,250 on the volley turn. At the retry A verified B's reading in code: the T26 reserve 3,080 is the recovery cap (`Director.cs:448-452`, `DirectorPatches.cs:519`), so the money order bound that turn. A's forward signal becomes necessary once the reordering raises the room, because `DefenseReserveShare` still caps the planned term at 4,417 (0.3) or 5,890 (0.4) against the 5,600-8,400 the 56 in-range impacts needed at 100-150 E per roll.

   Replacement for the S6 Lane A cell:
   > Floor = 100 E × in-range impacts × 1.25 when rolls went unpaid. Withdrawn as redundant: the T27 record logs `observedNeed` 9,250, and the T26 reserve was clipped by the recovery cap, so B's money-order reading is right. A keeps the forward signal as a complement to the reordering, not a substitute.

   Status column I accept: "Resolved: the recovery cap bound T26 (B); the estimator's lag binds next (A). B's reordering and A's forward signal are both adopted in item 6." The adopted item 6 needs no change.

## 2. Non-negotiable and blockers

| Requirement | Where it appears | Handled |
|---|---|---|
| Non-negotiable: observable inputs only; no threshold or memory from enemy counterintel, hidden positions or frozen snapshots | Agreement 7 (third bullet), agreement 10 (last two bullets), N3 → P2 replacement, item 5 gate "no hidden counterintel, stealth or surviving-city values", outcome gate "never at runtime" | Yes (wording precision in 1.1 above) |
| Blocker 1: latched roster and reservation release | Agreement 10 first bullet, in my exact words; item 4 "Unchanged" | Yes |
| Blocker 2: any threshold from the enemy counterintel multiplier | Agreement 7, including my A5 withdrawal; item 5 gate | Yes |
| Blocker 3: memory of city positions after they leave sight | Agreement 10 third bullet (B's non-negotiable in full, which subsumes mine on positions) | Yes |

No omission.

## 3. Factual notes and Pavol-reserved choices (recorded, not binding)

1. **N5's T28 line needs one clause.** Line 206 logs 78 impacts, 0 unpaid, but `lastNoEligibleDefense` 63 and `lastDefenseEnergy` 1,300 (`ownDefenses` had fallen 42 → 20): only 15 impacts had a Kinetic in range, so "0 unpaid" there shows coverage collapse, not funding success.
2. **Item 1's 15,000 E gate at T20** is two turns looser than my A1 gate (T18, Lossless ≈ T15 plus two turns); keep T20 as pass, log T18 as the stretch so the gate can fail on a slow SSTAR ramp.
3. **P1:** (a) stays my position; I accept (b) as Pavol's call with the two conditions as recorded in S4 and P1.
4. **P2:** (c). (a) only with the condition recorded; I rank (b) below (c) because its count is derived from a remembered per-city seen-set even when positions are dropped.
5. The record's defense arithmetic should state its roll cost: the mod reserves 150 E per roll (`DirectorPatches.cs:514`) while my A6, the 97 E per stop figure and agreement 8 assume 100.

## 4. Verdict line

CONFIRM @ ef9657c24b94b45b97658e06300f8b00406bf9d10f6b00e4f546ca4d27404691
END-OF-REPORT
