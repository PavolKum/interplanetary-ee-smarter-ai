# Council Round 1: Sealed Ballot (Lane C — Gemini 3.8 Flash)

## 1. Agreed Facts FA1–FA11

Lane C **ACCEPTS** all agreed facts **FA1 through FA11** without objection.

- **FA1–FA3 (War Mechanics & Weapon Caps):** Verified in `AI/Brain/Director.cs:271–274`, `AI/DirectorPatches.cs:207–215`, `AI/Brain/Tempo.cs:60–64`, and `AI/DirectorPatches.cs:1118–1124`.
- **FA4 (Material Starvation vs. Cap):** Confirmed by Lane E's cross-tabulation in `xreviewE-on-A.md:22,62`. Out of 52 war turns with $\ge 10\text{k}$ unspent energy, 39 had `materialLeft < 2,000` (75%), while the 30-weapon cap bound alone on only 12 turns (23%).
- **FA5–FA9 (Firing, Aim, and Hold Mechanics):** Verified against decision logs across matches 22–26 and code in `AI/Patches.cs:605`, `AI/AimWatch.cs:233–236`, `AI/Brain/Offense.cs:92`, and `AI/Brain/Strategy.cs:290`.
- **FA10–FA11 (Playtest Notes & Process Gaps):** Verified. Reconciling upstream counting slips (e.g., Ash placement failures 203 vs. note 207) and closing the deploy/stamp/mutation gaps is necessary before establishing new baselines.

---

## 2. Contested Fact CF1: Human Leftover Energy and Damage Penetration

- **Lane C Position:** **REFUTED**

### Turn-Index Semantics
In `*-human-telemetry.jsonl`, a game round follows this strict phase ordering:
1. `resource_credit(t)` at phase `RoundStart`: records `credit.energyBefore` (residual unspent energy from the conclusion of round $t-1$'s action phase, immediately prior to turn $t$'s refresh) and `credit.energyRefreshed` (turn $t$'s fresh production).
2. `round_start(t)` at phase `PlayerTurn`: human planning phase begins with fresh energy.
3. `turn_end(t)` at phase `WaitingPlayerData`: snapshot of state after planning phase $t$, recording `resources.energy` (unspent energy entering action phase $t$) and `resources.populationMillions`.
4. `ActionphaseSimulation(t)`: incoming projectiles arrive and defense rolls execute via `PlayerDefenseHandler.IsHitIntercepted()`. If intercept rolls fail or coverage is saturated, building and city damage are applied during `ActionphaseProcessStatusEnum.Finished` (`ActionphaseManager.cs:270–349`).
5. `resource_credit(t+1)` at phase `RoundStart`: records `credit.energyBefore` (the unspent energy remaining at the end of action phase $t$) and refreshes resources for turn $t+1$.
6. `turn_end(t+1)`: records the resulting post-damage population after turn $t+1$ growth.

### Evidence
Under **both** plausible indexing semantics, Lane D's claim that damage occurred only when human leftover energy was $< \sim 1.5\text{k}$ is disproven by the raw telemetry:

1. **Standard Indexing (Action phase $t$ funded by unspent energy recorded at `resource_credit(t).energyBefore` / `turn_end(t).energy`):**
   - **Match 26, Turn 24:** Entering action phase 24, human unspent energy was **8,113** (`resource_credit(24).energyBefore = 8,113`; `turn_end(24).energy = 8,113`). Despite holding more than five times Lane D's $1.5\text{k}$ ceiling, the human suffered **$-635$ population loss** (2,601 down to 1,966), the single largest population drop of the match.
   - **Match 25, Turn 32:** Human unspent energy entering action phase 32 was **6,454** (`resource_credit(32).energyBefore = 6,454`). The human suffered a catastrophic **$-740$ population collapse** (2,132 down to 1,392). Turn 34 similarly lost **$-158$ population** while entering with **3,023** unspent energy.
2. **Shifted Indexing (Lane D's off-by-one pairing of `energyBefore(t)` with damage showing at `turn_end(t)`):**
   - Lane D paired $T22$ ($1,195$) and $T23$ ($595$) with the $-408$ and $-635$ population drops. However, in Match 26 at $T22$, population actually **grew by $+115$** (2,894 to 3,009); the drops occurred on $T23$ ($-408$) and $T24$ ($-635$).
   - In Match 25, Lane D reported $T30\text{–}T37$ leftover energy as "$18\text{–}874$", selectively omitting $T32$ ($6,454$) and $T34$ ($3,023$).

**Engine Mechanism:** In `decompiled/PlayerDefenseHandler.cs:231–287`, individual defense rolls cost 100–200 energy. High-density salvos (85–90 shots/turn) penetrate because unrolled sectors lack coverage or volley volume outstrips activation budgets. Defender energy depletion is the *result* of artillery saturation, not an independent prerequisite for damage.

---

## 3. Issues I1–I12

### I1 (v0.20, no behaviour change). Instrument, stamp, make gates executable.
- **Vote:** **ACCEPT**
- **Rationale:** Crucial foundation. Adds `idleReason`, per-gun facing buckets, `unpoweredWeapons`, late-free-fire refusal tracking, destination splits, search failure categorization, build provenance stamps, opt-in deployment, and executable gate verification scripts without altering gameplay behavior.

### I2 (v0.20). Latch damage-triggered war, scoped.
- **Vote:** **ACCEPT**
- **Rationale:** Corrects the unlatched `recentDamage >= 600` bug in `AI/DirectorPatches.cs:207–215` that caused 4 war $\to$ peace flips across matches 22, 24, and 26. Scoping the permanent latch to $K \ge 12$ with pre-$K$ hysteresis and leaving `MinWeapons` (6) and `MinPassingShots` (12) unrelaxed safely resolves peacetime regressions while avoiding the Premature War Trap.

### I3 (v0.20). Late free fire also covers `pulse-wait` holds.
- **Vote:** **ACCEPT**
- **Rationale:** Resolves unspent energy waste on hold turns (e.g., Donnels $T33$ 8,828; Zenon $T33$ 6,544). Because energy does not bank between turns (`PlayerResourceHandler.TurnStartInit`), firing verified residual energy after construction and repairs complete delivers free offensive pressure at zero economic risk.

### I4. Fire discipline, form A (shot-waste).
- **Vote:** **AMEND**
- **Evidence Sentence:** In match 25, 345 of 995 paid shots (35%) terminated at non-commit targets—including 146 shots hitting fellow Smarter AIs costing $\sim 14.6\text{k}$ energy and triggering massive mutual repair cycles—making shot-waste suppression a zero-risk economic and tactical improvement that belongs directly in v0.20.
- **Exact Replacement Wording:** Deploy Form A in v0.20: a weapon whose only completed trajectory is to a non-commit target holds (no `fire-paid`) instead of firing, preserving the configured hit/miss ratio. Gate: paid destinations to fellow Smarter AIs $\le 5\%$ (down from 10–17%); paid shots to the commit target not lower than baseline. Form B (hardcoded non-aggression against fellow AIs) is an alliance-like design choice reserved exclusively for Pavol.

### I5. HUMAN-FIRST reach floor.
- **Vote:** **AMEND**
- **Evidence Sentence:** In match 26, Ash committed fire to Armiger and Hawking on turns 21, 22, 24, and 25 because the human's quiet economic opening kept jogi's score ratio at 0.218–0.335 below the 0.35 floor (F1), but scheduling this tuning for v0.21 prevents confounding the evaluation of v0.20's fire-discipline (I4) and war-latch (I2) fixes.
- **Exact Replacement Wording:** Lower `HumanFirstReachMinScore` (from 0.35 to 0.15–0.20) as a v0.21 tuning item, evaluated under paired A/B lobby testing. Gate: HUMAN-FIRST share of primary commits on T21–T28 $\ge 85\%$ in 2-Smarter-AI lobbies, without a rise in shots lost to the retarget aim penalty.

### I6. Material starvation at war (FA4).
- **Vote:** **ACCEPT**
- **Rationale:** FA4 demonstrates that material shortage ($\text{materialLeft} < 2,000$), rather than the 30-weapon cap, is the binding constraint on 39 of 52 idle-energy war turns. Diagnosing exact per-step material allocation in v0.20 before implementing a wartime energy-to-material rebalance or conditional cap raise in v0.21 is sound engineering.

### I7. Target-facing weapon placement.
- **Vote:** **ACCEPT**
- **Rationale:** FA6 and Lane C's rotational decay analysis show that planetary axial rotation and multi-body orbital motion can render fixed-azimuth placement ineffective or induce severe blind phases. Conditioning this feature in v0.21 on I1's per-gun telemetry showing far-bucket found rates $\le 0.6 \times$ near-bucket across $\ge 30$ firing turns with stable bearings protects against geometric regressions.

### I8. Earlier war.
- **Vote:** **ACCEPT**
- **Rationale:** Historical telemetry from v0.5–v0.9 (`PROGRESS.md:27,51`) proves that declaring war at T15 consumed $\sim 60\%$ of turn energy on premature firing and defense reservations, stifling economic expansion. Testing earlier war strictly as an experimental v0.21 A/B arm gated on T28–T32 income remaining within 10% of control rigorously contains the Premature War Trap risk.

### I9. Placement failures (ion/anti-nuke anchor reach, land plants).
- **Vote:** **ACCEPT**
- **Rationale:** Placement abandonments refund building costs and predominantly affect late-game power plants and ion defenses rather than weapons. Deferring search modifications to v0.21–v0.22 until I1 logs explicit terminal rejection reasons avoids heuristic changes that could compromise city shelter coverage.

### I10. Test protocol.
- **Vote:** **ACCEPT**
- **Rationale:** Adopts Lane C's standardized testing framework (Tier 2 primary lobby: 2 Smarter AIs + 1 Original L1 + Human, locked 400k crust, standard human fortress play, minimum $N = 3$ matches per version, and falsifiable PASS/FAIL/UNTESTED denominators). Eliminates the confounding variables that undermined matches 21–26.

### I11. Note corrections (FA10).
- **Vote:** **ACCEPT**
- **Rationale:** Resolves historical logging inaccuracies (Ash placement failures 203 vs. 207, Match 25 placement failures 107 vs. 127, and planned vs. firing labeling) before earlier playtests serve as statistical baselines.

### I12. v0.20 scope.
- **Vote:** **ACCEPT**
- **Rationale:** Accurately bounds v0.20 to `I1 + I2 + I3 + I4 (Form A)`. This delivers comprehensive telemetry instrumentation, fixes the peacetime flip regression, eliminates unspent energy on pulse holds, and suppresses off-target shot waste, while deferring behavioral re-weighting and complex search heuristics to v0.21+.

---

## 4. Missing Issues

- **None material.** The matrix comprehensively captures all load-bearing issues identified across the five sealed reports and cross-reviews.
- *(Minor Telemetry Refinement for I1):* In `AI/DirectorPatches.cs:3106`, the decision log records `d.WeaponUpgradesThisTurn` under the key `weaponUpgrades`. Because this logs per-turn purchases rather than cumulative upgrades, it directly misled Lane D into diagnosing a match-long upgrade deficit. I1's logging additions should clarify this field (e.g., logging both `weaponUpgradesThisTurn` and `weaponUpgradesTotal`) to prevent future analytical confusion.

---

## 5. One Non-Negotiable

- **The Non-Negotiable:** Strict enforcement of the **$N \ge 3$ Standardized Test Protocol (I10)** under locked Tier 2 lobby conditions (2 Smarter AIs + 1 Original L1 + Standard Human, 400k crust material), with an absolute prohibition against drawing architectural conclusions from $N=1$ trials or experimental human handicaps.
- **Why It Matters:** In *Interplanetary*, celestial orbital radii, initial planetary phase angles, and axial rotation periods are procedurally randomized each match. Across matches 21–26, the project repeatedly fell into the trap of re-architecting roadmaps based on single-match outcomes that confounded mod changes with lobby size (3 AIs in Match 25 vs. 2 in Match 26) and human playstyle variations (experimental nukes-only in Match 25 vs. standard fortress boom in Match 26). Without a locked, multi-match evaluation baseline, false positives and false negatives will continue to derail development.

---

## 6. Overall Scope Verdict

Lane C verdict: **ACCEPT** the proposed v0.20 scope (**I1 + I2 + I3 + I4 Form A**) as a disciplined, high-confidence release that resolves critical instrumentation blindspots and execution waste without introducing premature behavioral confounding.

END OF REPORT
