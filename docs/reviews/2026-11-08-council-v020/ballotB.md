I have the matrix, brief, charter, all five lane reports and all five cross-reviews. As lane B (measurement), the one disagreement I can resolve with evidence is CF1 — the turn-index semantics of the human's defense budget. Let me check the decompiled source of truth and the telemetry before voting.
`resource_credit` fires at phase=RoundStart with `energyBefore` = leftover before the refresh and `energyRefreshed` = the new production, and `TurnStartInit` (PlayerResourceHandler.cs:158-159) sets Energy = production — leftover is discarded. Now I need the action-phase ordering to pin down which energy funds the defense rolls that show in population at t. Let me inspect the telemetry structure and the game loop order.
The sequence data is decisive: `turn_end(t)` (WaitingPlayerData) comes **before** the DisplayActionphase events tagged turn t, which come before `resource_credit(t+1)`. So turn_end population is a pre-action-phase snapshot, and the action phase of turn t is funded by the leftover recorded as `energyBefore(t+1)`. Let me build the per-turn table to test both indexings against the actual damage.
The `damage`/`shot` events are the human's outgoing fire (AI incoming shots aren't in human telemetry) — population deltas are the only incoming-damage signal. Everything hinges on the phase ordering at the turn boundary. Let me verify where `resource_credit(26)` sits relative to the turn-25 action phase.
Ordering confirmed: `turn_end(t)` → action phase tagged t → `resource_credit(t+1)`. So `energyBefore(t+1)` is the **residual after** the action phase whose damage shows in population at t+1 — the same-phase pairing is dpop(k) ↔ eb(k), which is Lane C's indexing. Lane D paired eb(t) with dpop(t+1), one phase late. Let me verify with match 25's numbers.
All evidence is in hand, and my read-only checks resolved CF1. Here is my ballot.

---

# Ballot — Lane B (GLM-5.3 Flash, measurement), Round 1

Self-correction first: I adopt the four blockers in xreviewA-on-B against my own sealed report (the four-factor "prediction" was an identity; "energy never bound" conflated budget snapshots; paid ≠ landed; the interception denominator double-counted). The matrix's agreed facts already encode the corrected picture, and my ballot is written from the corrected position.

## 1. Agreed facts FA1-FA11

I accept FA1-FA9 and FA11. I object to one:

- **FA10 (object, AMEND).** The matrix writes "m24 is 67 (note 78)" — that adopts lane B's original error, which xreviewA-on-B explicitly corrected ("the match 24 note's 78 is correct"), and the total confirms it: per-match placement failures 24 + 64 + 78 + 107 + 229 = 502, exactly lane E's five-match per-type total (xreviewD-on-E row 7). Also "m23 is 64-69" is wobbly: 64 is triple-confirmed (laneA O7; xreviewE-on-A C7, whose note breakdown 32+14+13+5 sums to 64; the 502 total). Replacement wording: "Placement totals: m23 is 64 (note 59), m24 is 78 (the note is correct), m25 is 107 (note 127), m26 Ash is 203 (note 207)."

## 2. CF1 — position: **REFUTED** (lane D's claim is wrong; lane C's counter-examples stand)

**Turn-index semantics I used (new evidence, from the recorder and the game loop):**

- Phase order per turn, established from the telemetry `sequence` field of `v019m26-human-telemetry.jsonl`: `resource_credit` at phase=RoundStart (t) → `round_start` (PlayerTurn, t) → `turn_end` (WaitingPlayerData, t) → the DisplayActionphase events **tagged turn t** → `resource_credit` (t+1). Example: turn_end(25) seq 183 → action-phase events tagged 25 at seq 185-268 → resource_credit(26) seq 271.
- `TurnStartInit` (decompiled `PlayerResourceHandler.cs:158-159`) sets Energy = production; the leftover is discarded at refresh. So the action phase tagged t is funded by turn t's residual energy, and `resource_credit(t+1).energyBefore` is the **residual after** that phase's defense rolls — not the budget before them, and not the previous turn's budget.
- Therefore the same-phase pairing is: **damage visible in dpop(k) = pop(k) − pop(k−1) ↔ residual `energyBefore(k)`, with incoming volume = paid shots fired at turn k−1.** Lane C used exactly this pairing; lane D paired `energyBefore(t)` with the drop at t+1, which attaches the previous phase's residual to the next phase's damage — off by one action phase.
- Caveat: human-telemetry `shot`/`damage` events are the human's outgoing fire only (all Railgun/Laser; shots begin T25 in m26 while the AIs paid shots at jogi from T22), so population deltas net of growth (~115-153/turn) are the only incoming-damage signal.

**Result.** Same-phase pairing, m25 (dpop ↔ residual, incoming paid@jogi of the previous turn): damage landed with residuals of 1,930 (−183), 4,740 (−66), 3,448 (−34), **8,830 (−85)**, **6,454 (−740)**, 3,023 (−158), 874 (−198) — six of twelve damage phases ended with > 1.5k unspent. m26 T24: −635 at 8,113. Conversely, a genuinely broke phase produced no damage (m25: residual 774, dpop +5). Residuals under ~200 (166, 184, 18, 180) mark real energy exhaustion, so energy poverty is *one* mechanism; damage with thousands unspent (m25 T28, T32; m26 T24) means coverage/saturation, matching `PlayerDefenseHandler` roll gating (xreviewC-on-D) and lane C's saturation thesis. CF1 is resolved: **do not use lane D's "damage only when broke" for any ranking or gate**, and note that the human-pressure gate D-on-E proposed ("energyBefore < 2,000 on ≥ 60% of T15-T24") is invalid under the correct semantics too — lane C already showed 90% of those turns are naturally below 2,000.

## 3. Issues I1-I12

- **I1: AMEND.** Evidence: lane E's cap-first classification hid the overlap — material was short on 39 of 52 idle turns, not 33 (xreviewE-on-A disclosure 1); a first-match label would reproduce that error in every future log. Replacement wording: in the `idleReason` bullet, replace "`idleReason` (cap / material / placement / slots)" with "`idleReason` recorded as a cross-tab of cap × material × placement × slots flags (not a single first-match label)". Everything else in I1 accepted as written.
- **I2: AMEND.** Evidence: a latched damage war before the scheduled turn silently changes director posture (retaliation upkeep share 0.15 vs 0.4, defense floor) up to six turns early — flagged as an unmeasured side effect in xreviewE-on-A C1 and handled explicitly in lane A rec 1. Replacement wording: append to I2: "For a damage-latched war before the scheduled war turn, keep the retaliation upkeep share and pre-war defense posture; the latch changes the war flag only." The K=12 proposal, the hysteresis, the red-before-fix test and the gate are otherwise accepted.
- **I3: ACCEPT.** The opportunity is verified to the energy unit (Donnels T33: 8,828 left vs 5,960 cost; xreviewC-on-D claim 8), the gate is measurable (baseline 9 volleys, 0 aborts), fairness-neutral.
- **I4: ACCEPT** — form A in v0.20, as a per-AI arm where the lobby allows (I10). The waste is verified to the shot across four matches (fellow-Eiai share 10-17%, xreviewC-on-D claim 7; repairs Zenon 357, Ash 338), the hold form is fairness-safe, and the matrix already carries the accuracy-ratio preservation that xreviewE-on-A made a blocker for the exclusion form. Form B correctly reserved for Pavol.
- **I5: AMEND** — move to v0.21. Evidence: F1 rests on one AI in one match (Ash m26 T21-T25 commits), and I4 form A already removes most of the paid-shot waste that made Ash's split costly, so v0.20's effect can be measured before changing the target-choice policy. Replacement wording: "I5 enters v0.21, after the v0.20 matches measure I4's effect on paid destinations; the gate is unchanged (HUMAN-FIRST commit share ≥ 85% on T21-T28 in 2-Smarter-AI lobbies, no rise in retarget aim penalty)."
- **I6: ACCEPT.** v0.20 diagnose-only matches FA4 (material binds 39/52 idle turns); the cap-raise-alone rejection matches xreviewD-on-E row 14 (a 30→42 cap moves `energyLeft` ~3.6k against a 32-40k surplus — G2 fails by construction).
- **I7: ACCEPT.** Per-gun data does not exist (FA6), xreviewE-on-A shows the v0.20 gate is underpowered (SD 0.193 over 99 fire turns; ~50% power at n=30/arm), and the Hawking 10/10-at-113.8° existence proof cuts against building it blind. The w=0 golden test is the right shape.
- **I8: ACCEPT.** Not a v0.20 default; the v0.21 arm with the income do-no-harm gate is the right test of lane D's lever versus lane C's premature-war trap (PROGRESS rows 8-9). Note the matrix correctly omits D-on-E's proposed human-`energyBefore` gate, which my CF1 analysis also invalidates.
- **I9: ACCEPT.** The "full planet" claim is unproved (xreviewE-on-A C4: give-up causes are bimodal — anchor-limited for Ash/Eddie, unplaced-dominated elsewhere), so I1-driven diagnosis first is correct.
- **I10: ACCEPT.** It operationalizes exactly the confounds all five reports and both playtest notes name (m25 vs m26 lobby/strategy/aim-steps); PASS/FAIL/UNTESTED with denominators is the v0.16 rule that makes gates falsifiable.
- **I11: ACCEPT** — with the FA10 correction above: the correction pass must set m24 to 78 (note already correct), m23 to 64, m25 to 107, m26 Ash to 203 / lobby 229, before any of these numbers serve as gate baselines.
- **I12: AMEND.** Evidence: I4 form A is S-effort with the strongest verified numbers of the three behaviour candidates, while I5 is single-match and deferrable. Replacement wording: "v0.20 scope = I1 + I2 (amended) + I3 + I4 form A. Everything else, including I5, goes to v0.21+."

## 4. Missing issues

1. **"Effective volley" is undefined and unmeasurable from current logs** — yet I8's gate requires it ("first effective volley at least 2 turns earlier"). Human telemetry logs only outgoing shots; incoming damage is inferable only as population deltas net of growth; per-target realized damage does not exist (lane E unknowns). Material: add to I1 either an operational definition (e.g., first turn with ≥ N paid shots at the commit) or an explicit log field, before I8's gate is treated as falsifiable.
2. **Record the turn-index semantics in the decision record** (my section 2): dpop(k) ↔ `energyBefore(k)` same-phase pairing, action phase tagged t runs between `turn_end(t)` and `resource_credit(t+1)`. Any future human-side gate (pressure, defense-drain) must use this pairing; two proposed gates already died on it.
3. **Raw-evidence backup** (minor): the 89 MB git-ignored `docs/playtests/data/` is a single local copy (lane E O13 / risk 8); I1 stamps builds but nothing in the matrix backs up the evidence. Fold a post-match checksummed copy into I1's provenance item.

## 5. One non-negotiable

**No change may lower the realized hit/miss fraction below the configured accuracy level.** The secondary-trajectory quota fill (`AI/Patches.cs:449-475`) *is* the human-like-accuracy mechanism; a fire-discipline "cleanup" that deletes secondary hits without replacement silently turns the AI into a better shot than its level (xreviewE-on-A, major point 3). Accuracy is a difficulty knob, not an optimization target (Pavol's 2026-10-31 principle) — hold the shot, never reclassify the miss.

## 6. Verdict

verdict: support the proposed v0.20 scope as amended — I1 (cross-tab idleReason) + I2 (latch with posture guard) + I3 + I4 form A, with I5 deferred to v0.21 — a small, instrumented, independently gated version; conditional on the note corrections (including m24 = 78) landing before those matches serve as baselines and on the accuracy ratio surviving untouched.

Reproduction (read-only, repo root, commit `4d7c160`): the phase-ordering and pairing tables come from the two `python -c` commands shown above (sequence dump filtered to `round_start/resource_credit/shot/damage/turn_end`; per-turn `energyBefore`/`energyRefreshed`/`populationMillions` joined with `fire-paid` destination counts); the placement totals come from lane A's R7 command and the 502 per-type cross-check in xreviewD-on-E row 7.

END OF REPORT
