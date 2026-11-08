# Ballot D (Fable 5.1) - Round 1

Read-only. Target `4d7c160`. New checks run for this ballot: `ballot_checks.py` (scratchpad, read-only) re-aligning population,
paid shots by weapon, `strike` records, human Ion count and `resource_credit.energyBefore` per turn for m25/m26; summing
`director.weaponUpgrades` and `placementFailures` per AI; splitting the 52 idle turns; listing the human's early leftover energy.
Code: `AI/DirectorPatches.cs:63, 870, 883, 2122, 3086-3090` (`WeaponUpgradesThisTurn`, `upgradesInstalled`),
`docs/ruleset/WeaponsData.csv` (damage, lock-on, firing cost).

## 1. Agreed facts FA1-FA11

Accepted: FA1, FA2, FA3, FA6, FA7, FA8, FA9, FA11. Objections:

- **FA4, wording.** Reproduced the split exactly (193 / 52 / 39 / 12 / 1), but the test order hides an overlap: of the 39 turns with
  `materialLeft < 2,000`, 21 were also at `aimedWeapons >= 30` (lane E's 33 at the cap = 21 + 12). Hawking T22-T26 was at the cap with
  `materialLeft` 7-54 and the monitor's `MBOUND` flag. Replace the bold line with: "On idle-energy turns both constraints bind: 21 at the
  cap and short of material, 18 short of material only, 12 at the cap with material, 1 neither. Material is the more frequent one; the cap
  is not a cause to dismiss."
- **FA5, "own/none" is not waste.** `own`/`none` (17-18% of paid shots) are trajectories that hit no enemy planet; `fire` records plan
  misses at the configured accuracy (m25: 431 planned misses of 1,445 entries, 30%), and a planned miss by construction lands nowhere.
  So most `own`/`none` shots are the accuracy rule working, not a defect, and no gate should try to drive them down. The fellow-Eiai share
  (10-17%) and the secondary-only fill are the real waste. (My lane D O7 called the 100 E "paid for nothing"; that is withdrawn.)
- **FA10, one number.** `director.placementFailures` summed per AI: m22 24; m23 Beastie 27 + Arc 37 = 64; m24 Eddie 26 + Doc 52 = **78**,
  which is the note's number, not 67; m25 39 + 24 + 44 = 107; m26 Ash 203 + Hawking 26 = 229. Correct the m24 line to "78 (note 78,
  agrees)". The rest of FA10 stands, and one more belongs there: my lane D O10 read `director.weaponUpgrades` as cumulative; it is
  `d.WeaponUpgradesThisTurn` (DirectorPatches.cs:63, 3106). Summed over the match: Zenon 68, Donnels 66, Sarai 61, Ash 52, Hawking 20, Doc 51,
  Eddie 30, Arc 46, Beastie 36, Eiai 50. Lane C's correction is right; lane D R8 ("upgrades for every Eiai") loses its premise.

## 2. CF1

**Position: refuted as stated; replaced by a turn-aligned finding that is supported.** Lane C's alignment is the right one.

Turn-index semantics used now: within a human turn the telemetry order is `resource_credit` (RoundStart) -> `round_start` -> `turn_end`
(WaitingPlayerData) -> `shot`/`damage` (DisplayActionphase). `pop(t)` = population in the last `state` record of turn t (before the action
phase of t); `delta(t) = pop(t+1) - pop(t)` is the damage of action phase t, i.e. of the volleys the AIs paid for in turn t;
`energyBefore(t+1)` is the energy left *after* the defense rolls of action phase t. The energy that funded those rolls (end of his turn t)
is not logged; `energyBefore(t+1)` is its lower bound, and a value near 0 means the rolls ran him dry.

Evidence (paid shots at jogi in turn t by weapon; `strike` records at jogi; `energyBefore(t+1)`):

- m26: T22 Railgun 17 + Missile 4 -> **-408**, four `strike` records `Missile -> city, visible`, human Ion 0, 595 left after. T23 Railgun 17 +
  Missile 5 -> **-635**, five Missile strikes on visible cities, Ion 0, **8,113 left after** (rolls were not energy-limited). T24-T42: Railgun
  (and Coilcannon) only, 5-26 paid shots a turn against 28-39 Kinetic, Missiles only at weapons (T28-T29) -> population rose on all 19
  turn-pairs; the human bought 5 Ion at T26. So in m26 the only damage came from guided Missiles on visible cities with no Ion cover, not
  from Railgun volume and not from his energy.
- m25: T30 Railgun 51 -> **-736**, 184 left after; T32 Railgun 56 -> **-715**, 18 left after (rolls exhausted); T31 37 Railgun + 1 Missile +
  1 Nuke -> -129 with 6,454 left; T33 15 Railgun + 1 Nuke -> -211 with 3,023 left; T34-T36 nukes and Missiles on visible cities -> -31,
  -101, -334 (to 0). T24-T29: 16-51 Railguns a turn with 3.4-11.9k left after -> +102 to -197 (two small losses at 48-51 shots, four gains).
  T20: -183 with no Eiai shot at all (unexplained; Beastie or his own action).

Replacement statement: "Damage to the human came by two routes: (1) guided Missiles or nukes striking *visible* cities with no or few Ion
defenses (m26 T22-T23, all of m26's loss; m25 T34-T36); (2) Railgun saturation of 50+ shots in one action phase that exhausted his roll
energy (m25 T30, T32: 184 and 18 left after). Railgun volleys of 5-39 shots against 28-46 Kinetic with energy to spare did nothing in
23 of 25 turn-pairs." The original "only when broke" is withdrawn. This resolves CF1 and bears on I5/I8 (below) and on a missing issue.

## 3. Issues

- **I1: ACCEPT.** Add to the logged fields: `strike` already records `seen` and `building`; add the target's Ion/Kinetic counts the AI
  can see at fire time, so route (1) above becomes measurable per volley.
- **I2: ACCEPT** (K = 12, hysteresis before K, red-before-fix test). One sentence to add, from lane C's point: the latch keeps `TryWeapon`
  running (`DirectorPatches.cs:877, 904`), it does not fire a 5-gun arsenal (`MinWeapons` 6; Ash held "building" at T20-T21 with 5). Gate
  addition: "`aimedWeapons` does not fall and energy is spent on every latched war turn with `energyLeft >= 400` before weapons".
- **I3: ACCEPT.**
- **I4 form A: ACCEPT in v0.20** (S). AMEND the gate: drop any target on own/none (FA5 objection); keep "fellow Smarter AIs <= 5% of
  paid shots (from 10-17%)" and "paid shots to the commit not lower than baseline". Form B stays reserved for Pavol.
- **I5: ACCEPT, in v0.20.** It is one config value through an already-tested pure function (`HumanFirstHolds`, Strategy.cs:314-323);
  F1's 0.218-0.335 against 0.35 is reproducible from `target` scores; fairness holds (`score` uses visible economy and city population).
  Gate as written is measurable (`strategy.humanFirst`, `commit`).
- **I6: ACCEPT** with the FA4 wording (both constraints bind on 21 of 52). The v0.21 change should test "material rebalance" and "cap raise
  when material > floor" as arms, since 12 turns were cap-only and 18 material-only.
- **I7: ACCEPT** (v0.21, conditional, golden test at `w = 0`).
- **I8: AMEND the gate, accept the rank.** Evidence: the human's `energyBefore` was under 1,200 on every turn T13-T23 in m26 and under 700
  on T13-T19 in m25 (expansion, not pressure), so lane D's "leftover < 2,000" gate is invalid and is withdrawn. Replacement gates:
  "treatment arm's first paid volley at jogi >= 2 turns earlier than control; human population at T24 below his T20 value, or his Kinetic
  count at T20 at least 5 above the v0.19 median (15-16), in 2 of 3 matches; T28-T32 income not more than 10% below control". Supporting
  evidence that the arm is worth running: m25 T21-T23 Railgun-only volleys of 24-30 shots against 16-19 Kinetic took -66, -180, -55 with
  energy 166-4,740 left after; small, but the only Railgun-only damage outside saturation.
- **I9: ACCEPT.**
- **I10: ACCEPT.** Add: "turn alignment is fixed as in CF1 above (`delta(t)` = action phase t; `energyBefore(t+1)` = after its rolls) and
  every per-turn log field is checked against its C# source for per-turn vs cumulative before it is used in a gate" (two lanes, including
  this one, got one of these wrong).
- **I11: ACCEPT**, with the m24 total corrected to 78 and the `weaponUpgrades` per-turn note added.
- **I12: ACCEPT** I1 + I2 + I3 + I4A; propose adding I5 (S, config + one test) because F1 shows the mechanism and its fix is already
  unit-tested logic.

## 4. Missing issues

- **I13 (new, v0.21 experiment): guided weapons on visible cities when Ion cover is absent.** Evidence: CF1 replacement route (1); the
  m26 note (lines 17-22) attributes the T23-T24 dip to Hawking's Railguns, but the `strike` records show nine Missile strikes on visible
  cities with the human at 0 Ion, and no Railgun-only turn in m26 cost him population. The lever is intel plus weapon mix: when
  `strategy.visibleCities > 0` and the visible Ion count is below `DefensesForFullCover`, buy Missiles rather than Railguns (the existing
  `BlindArtillery` rule already does the inverse). Gate: strikes on visible cities per visible-city turn; human Ion count response.
  Fairness: visible-only (`seen: visible` is already the filter).
- **FA5/I4 clarification** that own/none are accuracy misses (above), so no roadmap item should target them.
- **Lane D self-corrections for the record:** O5 withdrawn as stated (CF1); O6 "Ash placement-bound at T35-T37" corrected to cap-bound
  (`aimedWeapons` 30 from T33); O7 "own/none paid for nothing" withdrawn; O10 and R8 withdrawn (per-turn field).

## 5. One non-negotiable

**I1 before any behaviour change, with I10's rule that every gate is PASS / FAIL / UNTESTED from a script with denominators.** This
round produced four field-semantics errors across lanes (per-turn `weaponUpgrades` read as cumulative, `energyBefore` mis-aligned to the
action phase, hand-typed placement totals, "paid = planned"); each was caught only by re-deriving from the log. A v0.20 that changes
behaviour without the stamp, the executable gates and script-generated note numbers would be judged by the same hand-typed process that
produced them.

## 6. Verdict

Ballot D verdict: ACCEPT the v0.20 scope I1 + I2 + I3 + I4 form A, add I5; CF1 is refuted as lane D stated it and replaced by the
turn-aligned two-route finding (guided strikes on visible cities without Ion cover; Railgun saturation that exhausts roll energy), which
puts the Missile/intel route on the v0.21 list and leaves the earlier-war arm as an experiment, not a default.

END OF REPORT
