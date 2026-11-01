# Sol v0.13 review: disposition (AI 0.13.1)

Verdict was **fix first** (GPT-6.1 Sol). Both High findings addressed (one by a code fix, one by correcting the
diagnosis), three Medium findings fixed, one deferred; the review's next move 2 (counter incoming weapon types) added.

| # | Finding | Disposition |
|---|---|---|
| 1 High | With saturation off, plant buying can hide its own glut: energy spent on plants keeps "unspent" under 30% (v0.11.1 Beam T24 24%, T26 19%) | **Fixed.** Energy spent on energy-only purchases (plants, energy upgrades) in the economy step counts as unspent for next turn's energy-glut test (refunded plants subtracted). Beam T24: 6.7k left + 7.5k on 30 Floating Plants = 51% -> glut -> energy gains worth 0. Saturation stays off. A full rolling allocation across all branches is deferred (see next moves). |
| 2 High | "The human repairs" is unverified (several attacked turns with no material spent; 141 -> 161 is a net count; Railgun damage was upgraded x2); the comparison is confounded (human's first shot T24 vs T28, 30 Railguns at T24 vs none) | **Accepted; diagnosis corrected** in the v0.12.1 note: strikes produced little observable suppression; human repair is unverified; interception and dispersed partial damage are confirmed; the earlier defeat coincides with earlier human fire. AI repair stays: AI damage accumulates regardless. |
| 3 Med | Per-slot scoring whenever a cap exists, even when firing energy binds | **Fixed.** Per-slot only when the aimed-weapon count is at >= 70% of the cap or in energy glut; otherwise cost-effectiveness ranks. Arsenal replacement (no path to swap Railguns for a later counter) deferred. |
| 4 Med | An untested Coilcannon (pass 1) can push the Missile out of the 60% band while Kinetic stops both it and the Railgun | **Fixed.** `ShareObservedPass`: an unobserved weapon inherits the observed pass of a weapon stopped by the same defenses (>= 0.5). Railgun 0.45 -> Coilcannon 0.45 (594 per slot), Missile unchanged (455) and back inside the band. |
| 5 Med | Repair could eat firing energy, used all material, and ranked isolated weapons over generators | **Fixed.** Repair energy = min(25% of energy, energy - this turn's firing cost of powered weapons); material at most 50% of stock (`RepairMaterialShare`); unpowered non-generators last; icon refresh callback. |
| 6 Med | Material glut is latched at turn start; the extraction filter can block a needed mine after in-turn spending | **Deferred** (not observed; Clowrus held 57-96k). Hysteresis on projected post-commitment cover is the fix if it shows up. |
| 7 Low | Tests cover Brain rules, not adapter interactions | **Partly.** New Brain tests for the counterexamples above; adapter behaviour (repair, selling, weapon target) remains unverified in game. |

Next move 2 (added): weapon types that hit us count as known enemy weapons even when their buildings stay hidden
(`DefensePlanner.RememberIncoming`: impacts / 2, decayed like seen weapons, never lowering what was seen). v0.12.1: the
human's Lasers hit from T29 and no Shield was built. Feeds counter-defenses and counter research.

Deferred next moves: guided, intel-supported kill capability (Railguns cannot be concentrated: no second phase and a
random surface rotation; use Missiles through the second-phase hook with damage allocation); a rolling material/energy
allocation for the fighting core; arsenal adaptation with bounded replacement; logging kills, suppression and repairs
per aiming budget; comparisons under a fixed human attack schedule.

Tests: 358 checks; 10 + 2 mutations of the v0.13.1 rules caught.
