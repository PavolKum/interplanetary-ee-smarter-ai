# Decision matrix for Round 1 ballots (facilitator draft, from 5 sealed reports + 5 cross-reviews)

Sources: laneA-E.md, xreviewA-on-B, xreviewB-on-C, xreviewC-on-D, xreviewD-on-E, xreviewE-on-A (all in this folder).

**Facilitator check F1** (reproduced):
- In match 26, Ash's `HumanFirstHolds` failed on score: jogi/max was 0.307 at T21, 0.218 at T22, 0.335 at T24 and 0.246
  at T25, against the 0.35 reach floor.
- Ash committed to Armiger (T21, T22, T24) and to Hawking (T25), and rated Hawking (another Smarter AI) highest at about
  1,100.

## Agreed facts (no lane disputes them after cross-review; any lane may still object)

- **FA1:** Damage-triggered war is not latched.
  - `WarPhase = turn >= warFromTurn || recentDamage >= 600`; `WarTurnFor` latches only the scheduled turn.
  - There were 4 war -> peace transitions in 10 AI-lobbies: Eiai m22 T18, Eddie m24 T15 and T18, Ash m26 T19.
- **FA2:** The war gate.
  - Railguns need no technology.
  - Weapons are bought only at war.
  - The early war needs Orbital Assembly (Missiles) and 12k income; otherwise the war falls back to T20 (all 10 AIs had
    `warTurn` 20).
- **FA3:** `MaxWeapons` 30 counts all aimed weapon types (Ash at T35: 15 Railgun + 8 Missile + 7 Coilcannon).
- **FA4:** Of 52 war turns with >= 10k energy left (out of 193 war turns):
  - 39 had `materialLeft < 2,000`;
  - 12 were at the cap with material available;
  - 1 was neither.
  - **Material, not the cap, is the main binding constraint on idle-energy turns.** This is lane E's self-correction,
    reproduced by D's and E's cross-reviews.
- **FA5:** Paid shots are not all aimed at the human.
  - In m25, 650 of 995 paid trajectory records named jogi.
  - The rest named fellow Eiais (about 15%), own/none (about 18%) and the original AI.
  - Secondary trajectories fill hit quotas (`AI/Patches.cs` firing-list fill).
- **FA6:** Aim on firing turns is 72-78% found/live.
  - Placement is target-blind (uniform random angle).
  - Unfound guns average 99-120 degrees from the target, against a 90 degree chance baseline.
  - Facing at build time may not hold at fire time (orbits; target changes).
  - Per-gun data does not exist.
- **FA7:** `AimBudget` steps per weapon depend on the total weapons of all AIs. m25 sat at the 30-step floor from T23;
  m26 ran 45-55. This is a third confounder between m25 and m26.
- **FA8:** Late free fire fired 9 times in m25 + m26. Paid = `firingHits + firingMisses` (not `planned*`). 0 aborts.
- **FA9:** Late free fire excludes `pulse-wait` holds by design. Several pulse-wait holds ended with energy left >= the
  firing cost (m25: Donnels T33 8,828; Zenon T33 6,544).
- **FA10:** The playtest notes contain errors.
  - Placement totals: m23 is 64-69 (note 59), m24 is 67 (note 78), m25 is 107 (note 127), m26 Ash is 203 (note 207).
  - The m25 "all three committed every turn" was 49 of 51 commits.
  - "Paid = planned" should read firing.
  - Ash's "14-15 railguns" were 30 weapons of three types.
  - Paid nukes are not landed nukes; landing is unobserved.
- **FA11:** Process gaps.
  - About 76% of the code is game-bound and outside the unit tests.
  - There is no mutation script.
  - A Release build deploys into the game folder by default.
  - The decision log has no build/config stamp.
  - The note numbers are hand-typed.

## Contested fact

- **CF1:** Lane D's claim that damage lands only when the human's leftover energy is < ~1.5k.
  - Lane C rejects it as a turn-index error: m26 T24 lost 635 population at 8,113 leftover; m25 T32 lost 740 at 6,454.
  - Unresolved: which energy (`energyBefore` of turn t or t+1) funds the defense rolls of the action phase that shows in
    population at t.
  - **Not used for ranking unless resolved with evidence in your ballot.**

## Issues (ACCEPT / AMEND / REJECT each)

- **I1 (v0.20, no behaviour change). Instrument, stamp, make gates executable.**
  - Log in the decision log:
    - `idleReason` (cap / material / placement / slots);
    - per-gun facing buckets and the bearing to each opponent;
    - `unpoweredWeapons`;
    - late-free-fire eligibility and refusal reasons;
    - `destination` split into own / neutral / none;
    - a placement search start/terminal record with reason counts (including end-turn cancels);
    - a `config` event with the build hash + dirty flag + non-default config + experiment arm.
  - Add `AI/tools/verify_gates.py` printing PASS / FAIL / UNTESTED with denominators.
  - Generate the note numbers by script.
  - Make deploy opt-in and fail if the game runs.
  - Add a scripted mutation runner.
- **I2 (v0.20). Latch damage-triggered war, scoped.**
  - Once a damage-triggered war happens at or after turn K (proposal: K = 12), it stays war for the match.
  - Before K, require 2 consecutive sub-threshold turns before reverting (hysteresis).
  - Do not relax `MinWeapons` / `MinPassingShots`.
  - Gate: zero war -> peace transitions after the latch, in 2 matches; a red-before-fix test on the Ash/Eddie damage
    sequences.
- **I3 (v0.20). Late free fire also covers `pulse-wait` holds** when energy and material left cover the reservation (same
  funding guards).
  - Gate: zero pulse-wait holds with `energyLeft >= reservation` and pulse conditions otherwise met; every late volley
    paid = firing hits + misses; 0 unintended aborts.
- **I4. Fire discipline, form A (shot-waste).**
  - A weapon whose only completed trajectory is to a non-commit target holds (no `fire-paid`) instead of firing. The
    configured hit/miss ratio is preserved (accuracy rule).
  - Placement: v0.20 or a v0.21 experiment arm.
  - Gate: paid destinations to fellow Smarter AIs <= 5% (from about 10-17%); paid shots to the commit not lower than
    baseline.
  - Form B (never fire at a fellow Smarter AI while a human lives) is an alliance-like design choice: **reserved for
    Pavol**.
- **I5. HUMAN-FIRST reach floor.** Lower `HumanFirstReachMinScore` (0.35) so a quiet but reachable human is not dropped
  for AI-on-AI fire (F1). v0.20 or v0.21?
  - Gate: HUMAN-FIRST share of commits on T21-T28 >= 85% in 2-Smarter-AI lobbies, without a rise in shots lost to the
    retarget aim penalty.
- **I6. Material starvation at war (FA4).**
  - v0.20: diagnose only (`idleReason`, per-step material spend, repairs vs weapons).
  - v0.21: a change, either a war-time energy -> material rebalance or a cap raise only when `materialLeft` > floor.
  - A cap raise alone (E's 30 -> 42) is rejected as under-sized and mis-targeted (D on E: G2 fails by construction).
- **I7. Target-facing weapon placement.** Moves to v0.21, conditional on I1 per-gun data showing far-bucket found-rate
  <= 0.6 x near-bucket on >= 30 fire turns and a stable bearing.
  - It enters as a soft score term with `w = 0` == v0.19 (golden test).
  - Dissent on record: lanes A, B, C proposed v0.20.
- **I8. Earlier war.** Not a v0.20 default.
  - A v0.21 per-AI A/B arm: a Railgun-affordability war start (D) or `WarFallbackTurn` 17 vs 20 (E).
  - Gated on the first effective volley at least 2 turns earlier, with T28-T32 income not more than 10% below control.
  - Lane C's "premature war trap" (v0.5-v0.9 history) is the risk under test.
- **I9. Placement failures** (ion/anti-nuke anchor reach, land plants): v0.21-v0.22, driven by I1 rejection-reason data.
  Not a "full planet" claim until an instrumented search shows it.
- **I10. Test protocol.**
  - Primary lobby: 2 Smarter AIs + 1 original L1 + human, crust 400k, standard human play.
  - 3-AI lobbies are a secondary stress test.
  - At least 3 matches per version before a ranking conclusion.
  - Per-AI A/B arms inside a match where possible.
  - Every gate is PASS / FAIL / UNTESTED with denominators.
  - Experimental human strategies are logged and excluded from balance conclusions.
  - No outcome-only gate (win/loss).
- **I11. Note corrections (FA10)** are applied to the match 22-26 notes and the scoreboard before they serve as
  baselines.
- **I12. v0.20 scope** = I1 + I2 + I3 (+ I4 form A if you accept it in v0.20). Everything else goes to v0.21+.
