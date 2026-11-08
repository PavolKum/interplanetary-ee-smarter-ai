# Round 1 ballot: lane E (Sonnet 5.5)

Read-only. Frozen target `4d7c160`. I read `matrix.md` and `xreviewD-on-E.md` (the review of my report); I did not read ballotA-D. Checks I ran
for this ballot are listed at the end; scripts are in my scratch directory outside the repo.

## 1. Agreed facts FA1-FA11

- **FA10: OBJECT on the m24 and m23 numbers.** The matrix says "m23 is 64-69 (note 59), m24 is 67 (note 78)". My recount, with three
  independent counters per match (`director.placementFailures` sum, `queued.abandoned:*` sum, `refunds` sum; no duplicate `(ai, t)` rows):
  m22 24, **m23 64** (Beastie 27, Arc 37), **m24 78** (Eddie 26, Doc 52), m25 107, m26 229 (Ash 203, Hawking 26) (`python fa10.py`).
  The m24 note's 78 is **correct** (its breakdown 26+17+14+8+6+6+1 = 78); the m23 note's headline 59 is wrong, and its own breakdown sums to 64.
  Replacement: "Placement totals: m23 is 64 (note 59; its breakdown sums to 64), m25 is 107 (note 127), m26 is 229 with Ash 203 (note 230 and 207);
  m22 (24) and m24 (78) agree with their notes." Without this, I11 would "correct" a note that is right.
- **FA2: ACCEPT, with stronger evidence.** Beyond the empty `bui_Railgun` unlock, the buildings that no tech unlocks are exactly
  `bui_SolarPlant`, `bui_Mine`, `bui_Railgun` (parsed from `TechTreeData.csv` against `BuildingsData.csv`), so Railgun is available from T1.
  Weapons are bought only under `d.War` (`DirectorPatches.cs`, step 5 "Weapons").
- **FA6: ACCEPT with a caveat.** "Placement is target-blind (uniform random angle)" rests on an overall 88 deg figure that I weighted by
  `live - found`, while the angle means cover every usable weapon with a target (`AimWatch.cs:151-165` vs `220-254`; 60 of 99 fire turns had
  weapons assigned off-commit). The unweighted mean of per-turn means (81.9 found / 104.0 unfound) agrees. Say "approximately target-blind".
- **FA9: ACCEPT (verified).** `LateFreeFire` requires `d.Reason == "below-saturation"` (`Brain/Strategy.cs:290`). There were 12 smart-AI
  `pulse-wait` holds on war turns; 4 had `energyLeft >= releasedEnergy`: m23 Beastie T32 (6,684 vs 5,500) and T34 (10,994 vs 6,180), m25 Zenon T33 (6,544 vs 4,100),
  Donnels T33 (8,828 vs 5,960) (`python pulse.py`).
- **FA11: ACCEPT, add one item:** the raw evidence (89 MB) is git-ignored (`.gitignore:9`) and exists in one local copy; only this council has a sha256 manifest.
- FA1, FA3, FA4, FA5, FA7, FA8: ACCEPT (each reproduced in my own report or in `xreviewE-on-A.md`; FA5 recount: 650 jogi, 146 fellow Eiais, 98 `own`, 78 `none`, 23 Beastie of 995).

## 2. CF1: REFUTED as stated ("damage lands only when the human's leftover energy is < ~1.5k")

**Turn-index semantics I used** (verified on sequence numbers: in m26, m25 and m23, 33 of 33 turns with shot/damage events have every such event
between `turn_end(t)` and `resource_credit(t+1)`, 0 violations; `python order.py`):
`resource_credit(t)` -> `round_start(t)` -> human turn -> `turn_end(t)` -> **action phase t** (events labelled t) -> `resource_credit(t+1)`.
So `credit(t+1).energyBefore` is the energy left **after** action phase t, and `turn_end(t).state.player.resources.energy` is what the human had to fund rolls.
Population effect of action phase t = `populationMillions` at `turn_end(t+1)` minus at `turn_end(t)` (plus round t+1 growth, about +115).

Lane D paired `credit(t)` (the leftover of phase t-1) with the drop of phase t, an off-by-one. Correct pairing (`python cf1.py v019m26 18 43`, `v019m25 19 38`):

| phase | energy at his turn end | left after the phase | population change |
|---|---|---|---|
| m26 A22 | 2,595 | 595 | -408 |
| m26 A23 | 10,913 | **8,113** | **-635** |
| m25 A30 | 6,184 | 184 | -125 |
| m25 A31 | 6,194 | **6,454** | **-740** |
| m25 A32 | 3,018 | 18 | -715 |
| m25 A33 | 4,063 | **3,023** | -158 |
| m25 A20 | 1,930 | **1,930** | -183 |

Of the five largest drops (-740, -715, -635, -408, -183), three had more than 1.9k left. In m26 the `population_millions` damage events in T31-T36
sum to 488-1,124 per turn with 16-24k left. Lane D's quoted m25 range "18-874" omits 8,569 / 6,454 / 3,023 present in the same credit series.
The weaker statement "exhaustion contributes" (A22, A30, A32 coincide with < 1k left) is **unresolved**; it is not usable for ranking.
Side finding: the telemetry `damage` stream is incomplete: m26 has zero `population_millions` events for T18-T24 although population fell 408 and 635 at T22-T23.
Use the `turn_end` population series for any gate on human damage, not damage event counts.

## 3. Issues I1-I12

- **I1: AMEND.** Evidence: the log fields are right, but the bundle mixes game-bound logging (untested code, 76% of the project) with tooling.
  Replacement: "I1a (ships with v0.20): the log fields in the list, each wrapped in the existing try/catch + `ReportOnce` so a logging fault never reaches a patch's
  return value; the placement start/terminal record must cover **all** building types (today `cover-search` gives-up is logged only `if (model.AntiNuke)`, `DirectorPatches.cs:2462`);
  `idleReason` is computed at the director's `EndTurn` (before the late-free-fire deduction, with the released reservation included, as `energyLeft` is today) and says so;
  `verify_gates.py` prints PASS/FAIL/UNTESTED with denominators, with a negative control (a synthetic log that must return FAIL) and a labelled tool self-check
  (reproduces 193/52/39/12/1). I1b (parallel, not blocking a match): opt-in deploy that fails when the game runs, one version source, note numbers generated by script, scripted mutation runner."
- **I2: AMEND.** Evidence: 4 flips in 10 AI-lobbies, each 1-2 turns with 5-8 guns on "peace"; the director's posture (defense floor, `RetaliationUpkeepShare` 0.15 vs 0.4, `DirectorPatches.cs:866`)
  would also move earlier by up to 6 turns for Eddie, unmeasured. Replacement: "Latch the **strategy** war flag (`StrategyPatches.cs:162`) per I2's K and hysteresis rules; leave `director.War` unchanged in v0.20 and log `warLatched`
  beside it. Gate: zero war->peace in `strategy.war` on turns with `shots > 0` after the latch; red-before-fix test on the Ash (865.3, 605.7, 529.0) and Eddie (608.3, 425.8, 679.5, 475.7) sequences;
  UNTESTED if no AI crosses the trigger in the 2-3 matches."
- **I3: AMEND.** Evidence: 4 qualifying holds in 193 war turns (2%); expected 2-3 in six AI-lobbies. Replacement: "Late free fire also accepts `pulse-wait` (a reason list instead of `==`) under the same funding guards. It is a no-regret S item that does not count toward v0.20 success; its gate is
  PASS/FAIL/UNTESTED by opportunity, and a pulse-wait volley that aborts is a FAIL."
- **I4: AMEND (move form A to a v0.21 arm).** Evidence: it edits `EnforceClassQuota` (`Patches.cs:449-475`), the accuracy-bearing path, so keeping the configured hit/miss ratio needs a defined algorithm (quotas recomputed on the weapons that remain) and a red-before-fix test on the T23 shape
  (Zenon `primaryFound` 3, `secondaryOnly` 22 of 30); the matrix gate "destinations to fellow Smarter AIs <= 5%" is nearly an identity of the mechanism. Replacement: "v0.20 logs only (`destination` split, per-gun target and trajectory class). v0.21 arm per AI:
  hold weapons whose only completed trajectory is to a non-commit target. Gate: fellow-Eiai share <= 5% **and** paid shots at the commit and the original AI not below baseline **and** realized hit fraction within 3 pp of the configured level. Form B stays reserved for Pavol."
- **I5: AMEND (v0.21 per-AI arm, not a default).** Evidence: F1 reproduces (Ash jogi/max 0.307, 0.218, 0.335, 0.246 against 0.35 at T21, T22, T24, T25; m25 Zenon 0.193 and Sarai 0.239 at T23), but that is 6 AI-turns in 3 AIs, and the human's score falls because his cities are not visible (strict visible-only is working as intended).
  Replacement: "v0.21 arm: `HumanFirstReachMinScore` 0.35 vs 0.20 per AI. Gate: HUMAN-FIRST share of commits T21-T28 >= 85% in the lowered arm vs the control arm, shots lost to the retarget aim penalty not higher; UNTESTED below 20 commit events per arm."
- **I6: ACCEPT.** I withdraw my R2 G2 (the idle-energy gate) as miscalibrated: lane D's arithmetic holds (12 more Railguns absorb about 2,400 E once and 1,200 E per volley against 32-40k idle). Add: "the v0.21 change is chosen by `idleReason` counts, and the plan names the energy sinks for the remainder."
- **I7: ACCEPT.** (Dissent of A, B, C noted.) Add that the go-condition is evaluated on per-gun buckets (hundreds of gun-turns per match), so it is testable after one match of I1a.
- **I8: AMEND the gate.** Evidence: "first effective volley at least 2 turns earlier" is an identity of setting `WarFallbackTurn` 17. Replacement: "Gate: planned hits on the human by T26 above the control arm; T28-T32 income not below 0.9 x control (stop-loss); the human `energyBefore` < 2,000 gate is **not** used: it already held on about 90% of T15-T23 in m26 from expansion alone (lane C), and it is the wrong-index metric of CF1."
- **I9: ACCEPT with two amendments.** (a) The give-up cause is bimodal by AI-lobby, not "anchor reach": `unplaced` >= 75% of tries in 6 of 9 AI-lobbies (Arc 99%, Zenon 99%, Donnels 99%, Doc 97%, Hawking 97%, Eiai 75%), `noAnchor` dominates only Eddie (87%) and Ash (56%) = 37 of 117 give-ups (`python cover2.py`). (b) `cover-search` covers anti-nuke searches only (`DirectorPatches.cs:2462`), so Kinetic and plant give-ups have no reason counters until I1a.
- **I10: AMEND.** Evidence: found/live has SD 0.193 over 99 fire turns, and AI-lobby means span 0.60-0.87 (SD 0.074, n=10); with one AI per arm per match, between-AI variance swamps a 10 pp effect (about 2 SE at n=30 per arm). Replacement: "...Per-AI arms are read as a difference-in-differences: each AI is compared with its own pre-activation window (arm switches on at a pre-registered turn K, for example T24) as well as with the other arm; arms are swapped between matches; `aim-budget.steps` (shared across all AIs' guns, FA7) is a logged covariate; gates, thresholds and the minimum n are written down before the match and evaluated by `verify_gates.py`; fewer than the minimum n is UNTESTED and play continues."
- **I11: AMEND.** Replacement: apply the corrected FA10 list above (do not touch the m24 count), plus the "paid = firing", "30 weapons of three types" and "49 of 51" fixes, and add an "unverified" tag for landed nukes.
- **I12: AMEND.** Replacement: "v0.20 scope = I1a + I2 (strategy-only) (+ I1b in parallel, + I3 as a no-regret S item). No other behaviour change; I4 form A, I5, I6, I8 become v0.21 per-AI arms, chosen in order by what `idleReason` and the facing buckets show."

## 4. Missing issues

1. **Arm design versus variance** (above, I10): the matrix's "per-AI A/B arms where possible" would be read as a plain two-group comparison and is underpowered; difference-in-differences with a pre-registered switch-on turn is the cheap fix.
2. **Telemetry damage events undercount early damage** (m26: no `population_millions` events T18-T24 while population fell 408 and 635): any gate that counts human damage events is wrong before T25; use the `turn_end` population series.
3. **Shared search budget as a confounder inside a match:** each AI's steps per weapon depend on all AIs' weapon counts, so one arm's gun count changes the other arm's search depth. Log it per AI-turn (already in `aim-budget`) and use it in the gate.
4. **Evidence backup:** 89 MB of git-ignored raw logs with a single copy (add to FA11 and I1b).
5. **Disclosure:** my sealed O1 (cap "63%") and R2 G2 are superseded by FA4 and the withdrawal in I6; O6's 88 deg is approximate (FA6); O7's "defense cover searches" are anti-nuke searches only.

## 5. One non-negotiable

**No behaviour change ships without its wiring-witness field in the decision log and a gate fixed before the match; a missing field or too-small n is UNTESTED, never PASS.** Why: 76% of the code is game-bound and outside the 620 checks, v0.19 needed three review rounds and a blocker (the call ran too late) that no test would have caught, and the notes have already drifted from the logs (m23, m25, "paid = planned"). Only an executable, pre-registered gate with a visible "nothing happened" state keeps v0.20 from being judged on one win or loss.

## 6. Verdict

**verdict:** accept the matrix's direction, but shrink v0.20 to I1a + a strategy-only I2 (with I3 as a no-regret extra and I1b in parallel), because every other candidate is either unproven (facing, floor, earlier war), accuracy-touching (I4 form A), or mis-targeted (the cap, now that material binds on 39 of 52 idle turns); keep CF1 out of the ranking (refuted as stated), and correct FA10 before I11 edits any note.

## Checks run for this ballot (all read-only)

`fa10.py` (placement totals, 3 counters), `cf1.py v019m26 18 43` / `cf1.py v019m25 19 38` (CF1 pairing), `order.py` (action-phase sequence ordering), `pulse.py` (pulse-wait holds), `f1.py` (facilitator check F1), `cover2.py` (per-AI give-up causes), `sd.py` (variance), parse of `TechTreeData.csv` vs `BuildingsData.csv` (start-available buildings), `sed` on `Brain/Strategy.cs:283-300` and `DirectorPatches.cs:858-880`.
Unverified on the game surface: everything here comes from logs and source; nothing was observed live and no tests were run.

END OF REPORT
