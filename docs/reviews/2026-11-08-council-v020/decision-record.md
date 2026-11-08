# Council decision record: Smarter AI after v0.19.0

**Date:** 2026-11-08. **Facilitator:** Claude Opus 5.5. **Accountable human:** Pavol.
**Target:** commit `4d7c160` + evidence `manifest:sha256:824fa979489cce519158b5b93a99e1f14860b82bc4c6cb6cb272aa60818f4b38`.
**Claimed end state:** `CONSENSUS RECORD` (subject to sign-off). Two v0.20 items are open splits reserved for Pavol.

**Revision 2.** The first draft (`sha256:d01bdb5b…`) was CONFIRMed by C and D and drew OBJECTs from A, B and E. This
revision applies every material sign-off finding (section 11). Earlier confirmations do not carry over.

## 1. Direct answer

**v0.20 is a small, instrumented version. It is not a placement version.**
- **Ship:** measurement and tooling (I1) and a scoped retaliation-war latch (I2).
- **Pavol decides two items:**
  - late fire on pulse-wait holds (I3);
  - shot discipline that withholds off-commit shots (I4 form A).
- **Everything else becomes measured v0.21 experiments,** chosen by what the v0.20 data show: target-facing placement,
  the HUMAN-FIRST floor, material/cap, the earlier war, and placement failures.
- **Test rules change:**
  - a fixed primary lobby;
  - at least 3 matches per version before a ranking conclusion;
  - script-computed gates that report PASS, FAIL or UNTESTED;
  - no win/loss gate.

**The council changed the facilitator's ranking.** The notes ranked placement facing first. After cross-review, all five
lanes put facing in v0.21, behind per-gun data, including the three (A, B, C) that first proposed it for v0.20.

## 2. Roster and independence

| Lane | Model | Family | Sealed report | Cross-review (of) | Ballot |
|---|---|---|---|---|---|
| A | GPT-6.1 Terra (OpenRouter) | OpenAI | laneA.md | xreviewA-on-B | ballotA |
| B | GLM-5.3 Flash (Vercel) | Zhipu | laneB.md | xreviewB-on-C | ballotB |
| C | Gemini 3.8 Flash (OpenRouter) | Google | laneC.md | xreviewC-on-D | ballotC |
| D | Fable 5.1 (subagent) | Anthropic | laneD.md | xreviewD-on-E | ballotD |
| E | Sonnet 5.5 (subagent) | Anthropic | laneE.md | xreviewE-on-A | ballotE |

**Correlation risks:**
- Same brief; the facilitator's notes are shared input.
- D and E are the facilitator's family; A, B and C share the opencode harness.
- Cross-family support below means at least two non-Anthropic lanes.
- Lanes A, B and C ran in the main checkout.
  - No tracked file changed.
  - The porcelain-status digest of untracked paths outside this folder was unchanged (`a2f2188c` before and after).
  - That digest covers paths, not the contents of existing untracked or ignored files; no lane reported a write, and
    none was found.

## 3. What each lane contributed, and where each was wrong

- **A (Terra):**
  - Found the unlatched damage war, the Orbital Assembly + income war gate, and secondary trajectories filling hit
    quotas.
  - Found the note errors.
  - Its evidence reproduced 11/11 (E). Its v0.20 facing rank was withdrawn by A itself in its ballot.
- **B (GLM):**
  - Its raw counts reproduced: facing means, peaks, placement breakdowns.
  - Its central "four-factor prediction within ±2%" is an algebraic identity (REJECT, A).
  - "9 nukes landed" confused paid with landed (REJECT, A).
  - Its interception denominators were wrong (A): intercepted events are already inside impacts.
  - "Energy never bound" mixed early-turn budget with end-of-turn surplus (A).
  - Both claims are excluded from this record and from every gate.
- **C (Gemini):**
  - Its data were "unusually solid" (B): the F1 score ratios, war flips and facing means reproduced.
  - Contributed the test protocol and the HUMAN-FIRST floor finding.
  - Its "mathematically proven saturation path against standard play" rests only on the nukes-only match (B, major).
  - Its war-latch-as-written would cause the premature-war trap it warned about (B).
- **D (Fable):**
  - Railguns need no tech; weapons are bought only at war.
  - About a third of paid trajectories are not recorded at the human (including planned accuracy misses; see FA5).
  - Pulse-wait holds leave energy.
  - Its "damage only when the human is broke" was refuted (C; D conceded).
  - It misread `weaponUpgrades` as cumulative (C).
- **E (Sonnet):**
  - The process findings: deploy default, no provenance, no mutation script, hand-typed numbers.
  - The aim-step confounder between m25 and m26.
  - Its cap-first idle classification overstated the cap. E disclosed this itself, and later showed that low leftover
    material is the common state on all war turns (FA4).
  - Its 30 -> 42 cap gate fails by construction (D).

## 4. Agreed facts (after ballot objections)

- **FA1:** Damage-triggered war is not latched; there were 4 war -> peace transitions in 10 AI-lobbies.
- **FA2:** The scheduled war gate needs Orbital Assembly and 12k income, else T20; all 10 AIs had `warTurn` 20. Damage
  can start war earlier (A). Railguns need no tech; weapons are bought only at war.
- **FA3:** `MaxWeapons` 30 counts all aimed types.
- **FA4 (reworded per A, D and E):**
  - 52 of the 193 turns at or after first war (186 of them war-flagged) ended with >= 10k energy left. Of those 52:
    - 39 had material < 2,000 (21 of them also at the cap);
    - 12 were at the cap with material available.
  - Leftover material under 2,000 is the common end-of-turn state (97 of 141 non-idle war turns, 69%). So the 39 of 52
    does not by itself show a material constraint.
  - The cap binds alone on 12 of 52. The constraint on the other 40 is unresolved, and it is what I1a measures.
- **FA5 (reworded per A and D):**
  - About 10-17% of paid trajectories went to fellow Smarter AIs and 2-15% to the original AI.
  - Most own/none records are expected accuracy misses. `fire-paid` does not record hit/miss-list membership, and "own"
    also includes non-player bodies, so this stays an inference until the classification is logged (I1a).
- **FA6:**
  - Aim on firing turns is 72-78% found.
  - Facing means are per-record aggregates over all assigned targets. No per-gun data exist.
  - Uniform candidate sampling does not prove a 90-degree baseline for final placements.
- **FA7:** Aim steps per weapon depend on the total AI weapons (m25 at the 30-step floor from T23). This is a confounder
  between m25 and m26.
- **FA8:** Late free fire fired 9 volleys, paid = `firingHits + firingMisses`, 0 aborts.
- **FA9:** Late free fire excludes pulse-wait by design. A late volley sets `LastStrikeTurn`, and pulses need
  `turn - lastStrikeTurn >= StrikeEvery (2)` (`Strategy.cs:395`).
- **FA10 (corrected):** the notes' errors are:
  - placement totals: m23 is 64, not 59; m25 is 107, not 127; m26 is Ash 203, lobby 229, not 207/230. m24's 78 is
    correct.
  - "paid = planned" should read firing;
  - Ash's "14-15 railguns" were 30 weapons of three types;
  - the m25 commits were 49/51;
  - paid nukes are not observed landings.
- **FA11:** Process gaps:
  - 76% of the code is outside the unit tests;
  - there is no mutation script;
  - a Release build deploys by default;
  - there is no build stamp in the log;
  - the note numbers are hand-typed;
  - the raw evidence is git-ignored and exists as a single local copy.
- **CF1 refuted (5/5).** In m26 the human's population fell by 635 net across action phase T23 with 8,113 energy left
  after it (`turn_end` T23 holds 10,913).
  - The 635 is a difference between turn-end snapshots and includes the next turn's growth; it is not a measured gross
    damage figure.
  - **Turn semantics:** action phase T follows `turn_end(T)`; `energyBefore(T+1)` is the balance after its rolls.
  - **Replacement finding (D, unverified by another lane):** m26's damage came from guided strikes on visible cities
    without ion cover. m25's two largest losses came with 50+ railgun shots exhausting roll energy.

## 5. Decision: ranked actions

### v0.20 (agreed 5/5, except I2's pre-T12 clause at 4-1)

1. **I1a: instrumentation, game-bound.** Log in the decision log:
   - independently observable idle constraints and purchase refusals (cap, material affordability at the price, slots,
     placement), not a single label;
   - per-step material spending, repairs vs weapons;
   - per-gun target, facing bucket, found, and the bearing to each opponent;
   - `unpoweredWeapons`;
   - late-fire and pulse-wait eligibility with refusal reasons;
   - `destination` split into own / neutral / none;
   - placement-search start and terminal records with IDs and reason counts (including end-turn cancels);
   - a `config` event with the build fingerprint, dirty flag, effective non-default config and experiment arm.
   - **Gate:**
     - per-gun records with target = commit sum to `live`;
     - all per-gun records sum to the count of usable weapons with a target;
     - every started search reaches a terminal record or an explicit cancel.
     - A missing field or short cardinality in match 1 is UNTESTED: fix it and replay before judging any behaviour
       change.
2. **I1b: tooling, in parallel.**
   - `AI/tools/verify_gates.py`: PASS / FAIL / UNTESTED with denominators. It must reproduce the FA4 numbers on m22-m26
     and must return FAIL on a doctored log.
   - Note numbers generated by script.
   - Deploy opt-in, refusing to deploy while `Interplanetary.exe` runs.
   - One version source.
   - A scripted mutation runner that exits non-zero on a surviving mutant.
   - A durable backup + sha256 manifest of each match's data.
3. **I2: retaliation latch.** Agreed 5/5 on the latch from T12. The pre-T12 clause is 4-1.
   - From T12, a damage-triggered war latches for the match, in separate match-scoped state.
   - **Before T12 (B, C, D, E):** a damage war reverts to peace only after 2 consecutive sub-threshold turns
     (hysteresis).
     - **Dissent (A):** before T12, keep the current behaviour.
   - The scheduled war turn, the pre-schedule `RetaliationUpkeepShare` and the pre-war defense posture stay as they are
     (no full-war upkeep or posture before the schedule; B).
   - It resets on a new match or an earlier-save load.
   - `MinWeapons` / `MinPassingShots` are unchanged.
   - **Gate:**
     - a red-before-fix test on the Ash and Eddie damage sequences;
     - reset and upkeep-guard tests (A);
     - zero post-latch reversions in 2 matches *with an observed latch opportunity*, else UNTESTED;
     - `aimedWeapons` does not fall on latched turns (D).
   - **Scope preference, not blocking:** shared state with the upkeep guard (A, B, facilitator), or strategy-only for
     v0.20 (E, who accepts either). The implementer takes the shared form with the guard unless Pavol prefers
     strategy-only.
4. **I11: correct the notes and scoreboard (FA10)** before they serve as baselines.
5. **I10: adopt the test protocol** (section 7).

### Split S1: I3, late fire on pulse-wait holds

| | v0.20 behaviour | Defer |
|---|---|---|
| Lanes | B, C, D, E (E: "no-regret S") | A (instrument in v0.20, test in v0.21) |
| Argument | Energy is verifiably left: m25 Donnels T33 8,828 vs 5,960; Zenon T33 6,544 | A late volley moves `LastStrikeTurn` and so postpones or dilutes the next synchronized pulse (FA9) |

- **Proposed resolution** (facilitator; B and E support it, A treats it as a hypothesis): v0.20 ships it only if a
  late volley on a pulse-wait turn does not update `LastStrikeTurn`.
  - Unchanged pulse cadence is a **hypothesis to test**, not a consequence. Firing also resets `HoldTurns`, which feeds
    the max-hold decision (`StrategyPatches.cs:127-135`).
  - **Gate:**
    - pulse turns occur at the control cadence;
    - max-hold decisions are not more frequent than control;
    - zero eligible pulse-wait holds left unfired;
    - paid = firing hits + misses;
    - UNTESTED when no eligible hold occurs.
- Otherwise it is instrumented in v0.20 and tested in v0.21.

### Split S2: I4 form A, withhold paid shots whose only trajectory is to a non-commit target

| | v0.20 | v0.21 experiment arm |
|---|---|---|
| Lanes | B, C, D (cross-family: Zhipu, Google, Anthropic) | A, E (OpenAI, Anthropic) |
| Argument | S effort; strongest verified waste (10-17% of paid shots to fellow Eiais; repair totals Zenon 357 and Ash 338, from all attackers, so the link to Eiai fire is an inference) | It edits `EnforceClassQuota`, the accuracy-bearing path; keeping the configured hit/miss ratio needs a defined algorithm and its own test; it also withholds legitimate free-for-all fire |

- **Gate (either path):**
  - fellow-Smarter-AI share of paid trajectories <= 5%;
  - paid shots at the commit and at the original AI not below baseline;
  - realized hit fraction within 3 percentage points of the configured level (accuracy rule);
  - UNTESTED below 20 fire turns per arm.
- **Condition for v0.20:** only if the hit/miss ratio is preserved by an algorithm with a red-before-fix test on the m25
  T23 shape (Zenon `primaryFound` 3, `secondaryOnly` 22 of 30).

- **Form B** (never fire at a fellow Smarter AI while a human lives) is alliance-like. **Reserved for Pavol** and not
  recommended by any lane.

### v0.21 candidates (agreed as experiments, ordered by what I1 shows)

- **I5:** HUMAN-FIRST reach floor 0.35 vs 0.15-0.20 (4 of 5; D wanted v0.20).
  - Gate counted only on turns meeting the other reach and low-yield conditions.
  - No fellow-AI exemption.
- **I6:** material rebalance, or a cap raised only when material is available. Chosen by the I1a constraint counts.
  E's 30 -> 42 gate is withdrawn.
- **I7:** target-facing placement as a soft score with a `w = 0` golden test.
  - Screening heuristic: far-bucket found-rate <= 0.6 x near-bucket over >= 30 fire turns, and a stable bearing.
- **I8:** earlier war: Railgun affordability, or fallback T17 vs T20, against a control.
  - **Primary metric:** `plannedHits` at the human by T26 from the `fire` log (the firing plan), against control.
  - **Secondary metric:** observed impacts on the human, including intercepted ones, from the defense ledger.
  - Income: T28-T32 income >= 0.9 x control.
  - If the observed-impact join fails, the secondary metric is UNTESTED.
  - "First volley earlier" alone is an identity (E).
  - The premature-war trap (C) is the risk under test.
- **I13 (new, D; v0.21-v0.22):** guided weapons on visible cities without ion cover. It rests on D's CF1 replacement
  finding, which no other lane verified. Investigate first.

### v0.21-v0.22

- **I9: placement failures.** The cause is bimodal by AI (`unplaced` dominates 6 of 9 AI-lobbies, `noAnchor` dominates
  Eddie and Ash). Fix by measured reason; no "full planet" claim without an instrumented search.

## 6. Non-goals and stopping conditions

- **Not in v0.20:**
  - placement facing;
  - cap changes;
  - research or war-timing changes;
  - target-choice tuning;
  - any accuracy change.
- **No action is still valid:** if Pavol prefers, keep v0.19.0, add only I1b tooling and play the protocol's 3 matches
  first.
- **Stop or revert** any v0.20 behaviour change whose do-no-harm gate fails:
  - `aimWaitAfterHuman` fire-turn mean <= 10 s, max <= 45 s;
  - HUMAN-FIRST share >= 85%;
  - `reserveShortfall` <= 5% of records;
  - `coveredBuildings` mean >= 0.85.

## 7. Test protocol (I10)

- **Primary lobby:** 2 Smarter AIs (Impossible) + 1 original level 1 + Pavol; crust 400k; standard play.
- 3-Smarter-AI lobbies are a stress test.
- At least 3 comparable matches per version before a provisional ranking. Three is not statistical proof.
- **Causal comparisons:**
  - Prefer paired saves or match-level arms.
  - Within-match per-AI arms are interference-prone (shared aim budget, mutual targeting); label them so.
- Log the human's strategy.
- Experimental strategies (for example nukes-only) are excluded from balance conclusions.
- Every gate is PASS / FAIL / UNTESTED from the script, with denominators.
- No win/loss gate.
- **Next states for every feature gate:**
  - **PASS:** keep the change; it may be built on.
  - **FAIL:** revert or rework it before the next match.
  - **UNTESTED:** keep it marked unproven and collect eligible evidence in the next match. After 3 matches still
    UNTESTED, Pavol decides keep/revert.
- Gates and wiring witnesses are fixed in the script before the match they judge (E).

## 8. Decisions reserved for Pavol

1. **S2:** I4 form A in v0.20, or a v0.21 arm.
2. **S1:** I3 in v0.20 with the cadence-preserving rule, or instrument-only.
3. **Form B:** whether Smarter AIs may hold fire on each other while a human lives. This is a design choice, not a
   fairness-rule question.
4. **Whether to commit to the protocol:** 3 matches per version in the fixed lobby, standard play.
5. **The no-action alternative:** tooling only, then 3 matches on v0.19.0.

## 9. Confidence by section

| Section | Confidence | Basis |
|---|---|---|
| Agreed facts | High | Each reproduced by at least two lanes, cross-family where marked. FA4's causal reading is Low (base rate) |
| v0.20 agreed scope | High | 5/5; I2's pre-T12 clause 4-1 |
| S1/S2 | Medium | Real splits with evidence on both sides |
| v0.21 ordering | Low-Medium | Depends on I1 data by design |
| CF1 replacement finding (I13 basis) | Low | One lane, unverified |

## 10. Traceability

- Every FA maps to the lane reports and cross-reviews cited in matrix.md.
- Every blocker raised has a disposition here:
  - A on B:
    - B1 (landed nukes) → FA10.
    - B2 (interception denominators) → excluded (section 3).
    - B3 (circular prediction) → not used.
    - B4 ("energy never bound") → excluded; FA4 carries the phase-aware measurement.
  - B on C: B1 → "saturation proven" not adopted; B2 → C's nuke gate not adopted; B3 → I2 scoped from T12 with the
    upkeep guard.
  - C on D: B1 → CF1 refuted; B2 → earlier war is a v0.21 arm; B3 → the upgrade misread is noted in section 3.
  - D on E: B1 → cap gate withdrawn (I6); B2 → war-timing facts in FA2 and I8.
  - E on A: no blockers; its majors went into I7 (v0.21), I4 (split S2) and FA4.
- **Dissent preserved:**
  - A: I2 pre-T12 unchanged (no hysteresis).
  - E: I2 strategy-only preference.
  - D: I5 in v0.20.
  - A: I3 deferred.
  - A and E: I4 deferred.
  - The original v0.20 facing proposals of A, B and C were withdrawn in their ballots (all ACCEPT I7).

## 11. Sign-off round 1 (on `sha256:d01bdb5b…`) and what changed

| Lane | Result | Material findings | Applied in this revision |
|---|---|---|---|
| A | OBJECT §5 | I2 mislabelled unanimous; A's B2/B4 blockers had no disposition; gates lacked next states; I1a and I8 gate populations undefined; FA5 categorical; S1 cadence presented as fact | I2 split, reset and upkeep tests; §3 and §10 dispositions; §7 next states; I1a gate and I8 metrics defined; FA5 qualified; S1 marked a hypothesis with a max-hold check; CF1 net-change and read-only qualifiers |
| B | OBJECT §5-I2 | Pre-T12 hysteresis dropped while 4 of 5 ballots kept it | Restored, with A's dissent; pre-war posture guard added |
| C | CONFIRM | None | None |
| D | CONFIRM | None material (a stale phrasing in §3; the `aimedWeapons` gate condition was missing) | Both applied |
| E | OBJECT §4, §5 | FA4 ignored the 69% base rate; I1a gate fails on 60 of 99 fire turns; S2 had no gate | FA4 reworded; I1a gate from E's wording; S2 gate and v0.20 condition added; repair-total wording |
