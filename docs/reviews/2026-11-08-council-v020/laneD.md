# Lane D (Fable 5.1) - adversarial roadmap for Smarter AI v0.20-v0.22

## 1. Scope and frozen target

- Commit `4d7c160` ("Playtest 26 (v0.19.0): human won the rematch at T44"), read-only. No file in the repository was
  changed except this report. No build, no tests run, no other lane report read.
- Files read: `docs/PROGRESS.md` (table rows 1-25), the five playtest notes (`2026-11-07-v0.18.0-match22.md`,
  `-match23.md`, `2026-11-08-v0.18.0-match24.md`, `2026-11-08-v0.19.0-match25.md`, `-match26.md`),
  `docs/reviews/terra-ai-v0.19-disposition.md`, `docs/ruleset/TechTreeData.csv`, `BuildingsData.csv`,
  `UpgradesData.csv`, `WeaponsData.csv` (headers), `AI/Brain/Strategy.cs` (lines 41-114, 144-211, 260-443),
  `AI/Brain/Tempo.cs` (58-65), `AI/Brain/Director.cs` (268-285), `AI/Brain/DefensePlan.cs` (11-19, 101),
  `AI/Brain/EconomyPlan.cs` (99-148), `AI/DirectorPatches.cs` (205-216, 862-910, 1280-1345, abandon valve 1946-2450
  by grep), `AI/Patches.cs` (590-608, 845-880), `AI/StrategyPatches.cs` (132, 161-162, 270), `AI/AimWatch.cs`
  (215-254), `AI/Plugin.cs` (config bindings 364-661, 732, research plan 183-192), `AI/Tests/Program.cs` (1-40,
  1700-1710, 1773), `AI/README.md` (37-45), `AI/tools/summarize_decisions.py` (header).
- Raw data parsed with three read-only Python scripts (section 7): `v018m22..v019m26-ai-decisions.jsonl`,
  `*-human-telemetry.jsonl`, `v019m25/26-monitor.log`. Match 25's aborted lobby (Helios/Uuno/Ruffian/Nanto) excluded.

## 2. Verdict and confidence

Lane D verdict: **the facilitator's ranking (placement first, then plant/defense placement, then the war/peace flip)
is inverted.** The largest measured lever is when and how hard the Eiais hit the human's energy, not where their guns
stand.** The data say three things the notes do not: (a) Railguns need no technology at all, yet the war gate waits
for Missile tech (Orbital Assembly) and otherwise falls back to T20, so every Eiai sat weaponless through T14-T19 while
the human had 7-16 Kinetic defenses, 2-9k energy a turn and no guns until T22-T30; (b) damage to the human happened
only on turns when his energy was exhausted (round-start leftover under ~1.5k), independent of how many shots arrived;
(c) 35-42% of paid shots in matches 23-26 went somewhere other than the human (fellow Eiais 10-17%, nowhere 17-18%),
while 28-57% of war-turn energy went unspent. Facing-aware placement is real but bounded: 72-78% of live guns already
found a trajectory on firing turns, so it can add at most a quarter to a volley that must first exist and land when the
human is broke. v0.20 should be: latch the damage-triggered war (the flip), start the Railgun war by affordability
rather than by Missile tech, stop paying for shots at non-commit targets, and let late free fire cover pulse waits.
v0.21 converts idle energy into guns (dynamic weapon cap, weapon placement retry, then facing). v0.22 does plants,
defenses and upgrades. **Confidence: Medium.** Anchored on five matches of decision logs and telemetry with
denominators (below), on the ruleset table for the Railgun unlock, and on the source for the war gate and flip; the
weak point is that the "damage only when broke" pattern rests on two matches, one of them the human's nukes-only
anomaly, and that early pressure may change how the human builds.

## 3. Observations with evidence

Denominators are given; "paid shot" = one `fire-paid` record; "war turn" = a turn whose `strategy.war` is true.

- **O1 FACT - Railgun has no tech requirement.** `docs/ruleset/TechTreeData.csv`, row `building_unlock`: Missile
  (`tec_OrbitalAssembly`, 650 sci, needs Advanced Signal Processing 400 and Self-Adjusting Optics 250), Coilcannon
  (`tec_InductionCoils`), Laser (`tec_GammaRayLasers`), Nuclear Missile (`tec_ThermonuclearWarheads`); `bui_Railgun`
  appears in no unlock. The three 250-sci starting techs carry Railgun *upgrades* (`upg_RailgunRadius1`,
  `upg_RailgunBuildingDamage1`, `upg_RailgunEnergyCostDown1`). `BuildingsData.csv`: Railgun 200 E / 200 M to build,
  100 E a shot (`WeaponsData` firing cost as logged in `fire-paid.energyPaid`). The original level-1 AIs built their
  first Railgun at T7 (Beastie, m25), T8 (Spiral, m23), T9 (Armiger, m26), T10 (Tarkoy, m24) (`original` events,
  `buildings`). So an early Railgun is in-character for this game, not an exploit.
- **O2 FACT - weapons are bought only at war, and war waits for Missile tech or T20.** `AI/DirectorPatches.cs:877`
  and `:904` call `TryWeapon` only `if (d.War ...)`. `AI/Brain/Tempo.cs:60-64`: war turn = `WarFromTurn` (16) only when
  Orbital Assembly is done *and* income >= `WarMinIncome` 12,000, else `WarFallbackTurn` 20 (`AI/Plugin.cs:538, 659-661`).
  Orbital Assembly reached the research head at T18 (Hawking, Doc), T20 (Zenon, Sarai, Ash, Eddie) or never (Donnels),
  so the T16 path never fired in m24-m26. Measured first war turns: m22 Eiai T13 (damage), m23 Arc T16 / Beastie T20,
  m24 Eddie T14 (damage) / Doc T18, m25 all three T20, m26 Ash T17 (damage) / Hawking T20. At T20 the m25 trio held
  0 weapons and 3.1-3.8k energy unspent each. Therefore "war starts late because weapon tech comes late" (brief) is a
  misdiagnosis: the plan researched Missile tech while Railguns were buildable from T1.
- **O3 FACT - the war/peace flip is an unlatched damage trigger.** `AI/Brain/Director.cs:271-274`:
  `WarPhase = turn >= warFromTurn || recentDamage >= damageTrigger` (600, `Plugin.cs:542`). `DirectorPatches.cs:213`
  latches `WarStartTurn` only `if (turn >= warTurn)`. Ash m26: `economy.recentDamage` 865 (T17, war), 606 (T18, war),
  529 (T19, peace), T20 war by turn. Eddie m24: 608 (T14, war), 426/418 (T15-16, peace), 680 (T17, war), 476/333
  (T18-19, peace), T20. Cost: Ash's 5 Railguns bought T18 idled under "peace" (T19) and "building" (T20-21); Eddie's 6
  idled four turns. One-line fix (latch the damage war too, or add hysteresis).
- **O4 FACT - the human had no guns for 2-10 turns after the Eiais went to war, and few defenses before T20.** Human
  telemetry `player.buildings`: first weapon T22 (m24, 23 Railguns), T24 (m26, 13), T27 (m22, 13; m23, 12 Lasers),
  T30 (m25, 17 Nuclear Missiles). Kinetic defenses at T14/T16/T20: m26 7/11/15, m25 8(T17)/9/16, m24 (T22) 32. Energy
  per turn at T14-T16: human 1.6-3.1k (all five matches) vs Eiai 4.0-4.6k (m25, m26 `economy.energyPerTurn`).
- **O5 FACT - damage landed only when the human's energy was exhausted.** `resource_credit.energyBefore` (energy left
  at round start) vs population: m26 T22 1,195 and T23 595 -> population -408 and -635 with 21-22 paid shots a turn;
  T24-T29 leftover 7.7-12.3k -> population +28 to +476 with 16-26 paid shots a turn. m25 T23 166 (pop -55), T30-T37
  leftover 18-874 while 39-58 paid shots a turn -> 2,257 to 0; T25-T29 leftover 3.4-11.9k -> population grew despite
  48-51 shots at T25 and T27. Natural growth is +100-170 a turn (m24, undamaged). INFERENCE: whether a volley hurts is
  decided by the human's roll budget that turn, not by the shot count or the guns' facing; the strategy's roll model
  (`Strategy.cs:205`, 0.5 x income / 150, `rollsBy.jogi` 23-68 in those turns) already encodes this.
- **O6 FACT - idle energy at war is 28-57% of income; two different binding constraints.** Unspent share of war-turn
  income (`director.energyLeft` / `economy.energyPerTurn`): m22 Eiai 29%; m23 Beastie 47%, Arc 30%; m24 Eddie 35%, Doc
  23%; m25 Zenon 28%, Donnels 45%, Sarai 34%; m26 Ash 42%, Hawking 57%. Hawking T22-T26: 34.3-39.6k unspent a turn at
  `aimedW 30` = `MaxWeapons` (monitor log; `DirectorPatches.cs:1292 atCap`). Ash T35-T37: 32-37k unspent with 14-15
  Railguns, `director.slotsLeft` 135-139 and 23 Railgun placements abandoned (monitor `abandoned Railgun`): placement
  bound, not slot or cap bound. Material idle at the end: Sarai 26.8k, Ash 34.2k.
- **O7 FACT - a third of paid shots did not go at the human.** `fire-paid.destination`: m25 995 paid -> jogi 650 (65%),
  fellow Eiais 146 (15%), own/none 176 (18%), original AI 23 (2%). m26 479 -> 63% / 11% / 17% / 9%. m23 318 -> 60% /
  17% / 18% / 4%. m24 166 -> 58% / 10% / 17% / 15%. `fire` events: `secondaryOnly` 312 of 1,458 retained weapon entries
  (m25), 134 of 845 (m26) fired at a target other than the commit. The m25 T23 "broken" flip to Beastie aimed 3/30
  (Zenon) and 6/29 (Sarai) and put 17 + 13 shots into Donnels, Sarai and Zenon. Repairs that match: Zenon 357 (35.7k E,
  54.0k M), Donnels 277, Sarai 106; m26 Ash 338 (33.8k E, 50.1k M), Hawking 119 (monitor `REP`). INFERENCE: part of the
  25-40 repairs a turn the m25 note attributes to nukes is Eiai-on-Eiai fire. "own"/"none" is the trajectory's hit body
  not being an enemy planet or being null (`Patches.cs:605`): 100 E paid for nothing, 17-18% of shots.
- **O8 FACT - pulse waits with energy left.** `strategy.reason == "pulse-wait"` with `director.energyLeft`: m25 7 holds
  (Zenon T24 2,198; Sarai T24 2,878, T26 3,068, T28 92; Donnels T31 227, T33 8,828; Zenon T33 6,544), m26 1 (Ash T34
  2,599), m23 3 (3.7-11.0k). Late free fire excludes pulse-wait by design (`Strategy.cs:290`; test at
  `Tests/Program.cs:1706` "a pulse wait is a choice, not an energy hold"). `StrikeEvery` = 2 (`Strategy.cs:67`).
- **O9 FACT - aim is already 72-78%; facing is a bounded gain.** Firing-turn `aim-search` found/live: m22 78/103, m23
  287/370 (78%), m24 168/220 (76%), m25 641/895 (72%), m26 439/563 (78%). `unfoundFacingDeg` mean 99-129 deg over 6-36
  samples. The retarget turns (m25 T23 3/30, 6/29) are outliers caused by the commit flip, not by placement.
- **O10 FACT - weapon upgrades uneven, interception falls with volume.** `director.weaponUpgrades` at the last record:
  Zenon 6, Donnels 3, Sarai 0, Ash 0, Hawking 0, Doc 6, Arc 6, Eddie 0, Beastie 0. Observed stop share on the human
  (`observedInterceptedBy.jogi` / `observedImpactsBy.jogi`, summed per turn): m25 25-46%, m26 50-62%, m23 53-67%, m24
  64-69%. Upgrades cost 50 E + 150-350 M (`UpgradesData.csv`); Sarai sat on 26.8k M with 0 upgrades.
- **O11 FACT - placement give-ups are mostly plants and defenses, not guns.** m25: NuclearPlant 52, SolarPlant 32,
  IonDefense 22, Coilcannon 1 (note: 127). m26: 230, of which weapons 48 (Railgun 30, Coilcannon 11, Missile 7),
  NuclearPlant 38, IonDefense 35, KineticDefense 25. `cover-search` gave-up 18/56 (m25), 28/55 (m26). Plants and
  defenses are refunded (`DirectorPatches.cs:1420`), so the loss is slots and turns, not resources.
- **O12 FACT - precision is intel-gated and dead.** `strategy.visibleCities` 0 at T30 for Zenon and Ash;
  counterintel x6-70 (telemetry). `BlindArtillery` refuses Missiles when blind (`DirectorPatches.cs:1304-1310`). So a
  Missile-heavy mix against the human's 0-9 Ion defenses is not available without an intel fix; it stays off v0.20.
- **O13 JUDGMENT - lobby size and the human's strategy confounded m25 vs m26** (the note says so, m26 line 46). What is
  common to both: the only damaging turns were the ones where his leftover energy was under ~1.5k (O5).

## 4. Risks, ranked

1. **Over-fitting to a broke human.** O5 rests on m25 (nukes-only) and m26 T22-T23, where the human was poor because he
   was buying 25 Railguns himself. If he turtles instead, early pressure buys defenses, not damage.
2. **An early war slows the Eiai economy.** At T14-T16 the Eiais spent everything (unspent 14-1,638 E). Fifteen
   Railguns = 3,000 E + 3,000 M plus 1,500 E a volley against 4.0-4.6k income: roughly one lost economy turn, then
   ~35% of income to firing until income grows.
3. **Eiai-on-Eiai fire discipline looks like an alliance.** Not in the five fairness rules, but a design choice for
   Pavol: fellow Eiais already share HUMAN-FIRST. Holding unreachable shots (no target change) is the fairness-safe form.
4. **More guns cost aim time.** The v0.19 measured cost is 0-25 s (mean 10 s, m25) and 0 s on 18 of 23 turns (m26).
   A dynamic cap above 30 raises it; the gate below bounds it.
5. **Placement work before the strike exists** repeats the v0.17 pattern (placement first, human won at T57 and T36).
6. **Small samples.** Two v0.19 matches, one of each lobby size; every exit gate below needs two matches per lobby size.

## 5. Recommendations, ranked (v0.20 = 1-4, v0.21 = 5-7, v0.22 = 8-10)

1. **Latch the damage-triggered war (the flip).** Evidence O3. Mechanism: in `DirectorPatches.WarTurnFor` (205-216)
   latch `WarStartTurn` when `WarPhase` is true for any reason, or require two consecutive sub-threshold turns before
   peace. Files: `AI/DirectorPatches.cs`, `AI/Brain/Director.cs`, test in `AI/Tests/Program.cs` (a check that fails on
   the current code: war at T17 by damage 865, T18 at 606, T19 at 529 must stay war). Exit gate: zero `strategy`
   records with `war=false` after the first `war=true` for every Eiai, two matches. Fairness: own damage only. **S.**
2. **Start the Railgun war by affordability, not by Missile tech.** Evidence O1, O2, O4, O5. Mechanism: `WarStart.Turn`
   gets a third path: Railgun buildable (always) and income >= a Railgun threshold (propose 3,000 E/t, reached T12-T14
   in m25/m26) -> war at max(turn, 14); keep `WarFallbackTurn` 20 as the ceiling; `GrowthUntilTurn` 15 stays so the
   first two Railgun turns use the retaliation upkeep share (0.15), full share from T16. Files: `AI/Brain/Tempo.cs`,
   `AI/Plugin.cs` (new `WarRailgunIncome`), `AI/DirectorPatches.cs:207-216`. Exit gates (decision log + telemetry):
   first `strategy.war=true` <= T14 and first `fire` at jogi <= T16 for every Eiai; Eiai `economy.energyPerTurn` at T18
   >= 85% of the v0.19 median for that lobby size (m25 3.9-7.5k, m26 4.4-9.5k); human `resource_credit.energyBefore` <
   2,000 on >= 60% of turns T15-T24 (v0.19: 4 of 10 in m26, 5 of 10 in m25); human population at T24 below his T20
   value in at least one of two matches. Fairness: the AI uses its own income and a building the game gives everyone at
   T1; the original AIs buy Railguns at T7-T10 (O1). **M.**
3. **Pay only for shots that can reach the commit.** Evidence O7. Mechanism: at first-phase targeting, a weapon with no
   trajectory to the commit holds (no `fire-paid`) instead of firing at a secondary; the saved 100 E per weapon flows to
   construction the same turn. Variant for Pavol's decision: allow the secondary when it is the original AI or the
   human, never a fellow Smarter AI while a human is alive (this is the alliance form, risk 3). Files: `AI/Patches.cs`
   (first-phase targeting, 590-608 and the strike planner 845-880), `AI/StrategyPatches.cs`. Exit gate: `fire-paid`
   with `destination` in {jogi, original AI} >= 90% of paid shots; fellow-Eiai share <= 3% (from 10-17%); own/none <= 8%
   (from 17-18%); Eiai repairs per war turn down by a third against m25/m26 (Zenon 357, Ash 338 total). Fairness: a
   target choice on visible trajectories; no new information. **S** (hold form) / **M** (alliance form).
4. **Late free fire also covers pulse waits.** Evidence O8. Mechanism: `Strategy.LateFreeFire` accepts
   `Reason == "pulse-wait"` when `energyLeft >= firingCost` (the energy it would have reserved); flip the check at
   `Tests/Program.cs:1706` to the new rule and add the m25 Donnels T33 fixture (8,828 left, cost 5,960). Files:
   `AI/Brain/Strategy.cs:287-293`, tests. Exit gate: zero `strategy` records with `reason=pulse-wait` and
   `director.energyLeft >= firingCost`. Fairness: none affected. **S.**
5. **Dynamic weapon cap while energy is idle.** Evidence O6 (Hawking at 30 with 36-39k idle). Mechanism: `MaxWeapons`
   becomes a floor; while `d.EnergyGlut` holds and last firing turn's found/live >= 0.7, the cap rises by 5 a turn to a
   hard ceiling (propose 50); it falls back when `aimWaitAfterHuman` mean over the last three firing turns > 20 s.
   Files: `AI/DirectorPatches.cs:1288-1345`, `AI/AimWatch.cs` (expose the wait), `AI/Plugin.cs`. Exit gate: war-turn
   unspent share <= 20% per Eiai (from 28-57%); `aim-search.found` on firing turns >= 0.72 of `live` (no regression);
   `aimWaitAfterHuman` mean <= 20 s. Fairness: more of its own energy spent, no accuracy change. **M.**
6. **Weapon placement retry before giving up.** Evidence O6 (Ash), O11 (48 weapon give-ups in m26 with 135 slots left).
   Mechanism: when a weapon placement is abandoned, re-queue once on a different anchor (the human-facing hemisphere
   first, which folds in the facilitator's #1 at the anchor-choice step), then back off as today. Files:
   `AI/DirectorPatches.cs` (abandon valve 1946-1971, 2412-2450; shelter-step anchors). Exit gate: `abandoned Railgun|
   Coilcannon|Missile` <= 10 per match (m26: 48); live weapons at T30 >= 25 for every Eiai with income >= 20k. **M.**
7. **Facing-aware weapon anchor (the facilitator's #1), after 5-6.** Evidence O9: bounded at +22-28% of a volley.
   Mechanism as the notes propose. Exit gate: `aim-search.found/live` on firing turns >= 0.85 (from 0.72-0.78) and
   `unfoundFacingDeg` mean < 90. Fairness: own geometry only. **M.**
8. **Weapon upgrades for every Eiai with idle material.** Evidence O10. Mechanism: when `materialLeft` > `MaterialGlutMin`
   at war, lift `WeaponUpgradesPerTurn`/`WeaponUpgradeShare` for the turn. Files: `AI/DirectorPatches.cs:883-897`,
   `AI/Plugin.cs`. Exit gate: every Eiai at war >= 4 `director.weaponUpgrades` by war turn +5; end-of-match
   `materialLeft` < 10k. Fairness: own material. **S.**
9. **Plant and defense placement give-ups (the facilitator's #2).** Evidence O11. Exit gate: NuclearPlant + SolarPlant
   abandons <= 20 per match (m25 84, m26 65); IonDefense <= 10 (m25 22, m26 35). **M.**
10. **Intel for a Missile mix (v0.22 or later).** Evidence O12 and the human's 0-9 Ion defenses. Only after a measured
    intel bid can see one city for two turns running. **L.**

Order and what should change it: if after two v0.20 matches per lobby size the human's leftover energy is still
> 2,000 on most of T15-T24 (gate 2 fails) while the war started by T14, the lever is not timing but volume: move 5
ahead of 3-4 in v0.21. If gate 2 passes but population does not fall, placement (6-7) moves up: the shots exist and
land when he is broke but too few reach. If the Eiai economy gate fails (income at T18 < 85% of v0.19), raise the
Railgun income threshold to 4,000 rather than abandon the early war.

## 6. The strongest reason I may be wrong

The human was broke at m26 T22-T23 because he chose to buy 25 Railguns, and at m25 T31-T37 because he over-committed to
nuclear launches; in neither case did the Eiais *make* him poor. An earlier Railgun war may simply convince him to
build 15 more Kinetic defenses at T14 (he already had 15 by T19 unprompted), after which 30-45 early shots meet full
cover and the Eiais have traded two economy turns for nothing. The counter-evidence I have is indirect: the roll model
says 5-9 affordable rolls at T14-T17 against 31 arriving shots from three 15-gun Eiais, and defenses cost him 200 E
each when his income is 2-3k. Two matches with the early war, both lobby sizes, settle it; the gates in 5 are written
so that a null result redirects the roadmap to volume (5) rather than to placement.

## 7. Reproduction and unknowns

Commands run (Git Bash, repo root `C:\Users\pavol\Repos_Local\interplanetary-mods`; scripts lived in the session
scratchpad and only read and print):

- Head: `git log --oneline -1` -> `4d7c160`.
- Event schema: `python -I -c` over each `*-ai-decisions.jsonl`, counting `e` and printing the first record per type.
- Per-AI summary (war turn, first weapon, unspent share, paid-shot targets, research heads): script `laneD_analyze.py`
  = group `economy`/`director`/`strategy`/`aim-search` by (ai, t); unspent = sum(`director.energyLeft`) /
  sum(`economy.energyPerTurn`) over turns with `strategy.war`; human rows from `state.player.resources` and
  `state.player.buildings` (bridge 0.4.0 keeps buildings under `player`, not `state`).
- Per-turn war table (AI income, energyLeft, weapons from `economy.buildings`, `strategy.reason/commit/passing/
  rollsBy.jogi`, `aim-search.found/live/unfoundFacingDeg`, `fire-paid` by destination; human E/t, defenses, weapons):
  script `laneD_turns.py`.
- Saturation table (paid shots at jogi per turn vs `rollsBy.jogi` vs human population t and t+1), pulse-wait holds with
  `director.energyLeft`, observed impacts/intercepted on jogi: script `laneD_saturation.py`.
- Human leftover energy: `resource_credit.credit.energyBefore` per turn >= 20 from `*-human-telemetry.jsonl`.
- Paid-shot destination classes: `fire-paid.destination` in {jogi} / {smart nicknames} / {own, none} / else.
- Abandoned placements: `grep -o "abandoned [A-Za-z0-9,x]*" v019m26-monitor.log | tr ',' '\n' | sed -E 's/x([0-9]+)$/ \1/'`
  summed by type; repairs: `grep -o "REP [0-9]* (-[0-9]*E -[0-9]*M)"` per AI line.
- Railgun unlock: `TechTreeData.csv` rows `id`, `building_unlock`, `tech_requirement`, `science_cost`, `upgrade_unlock`.
- War gate and flip: `grep -n "WarTurnFor\|WarPhase\|WarMinIncome\|WarFallbackTurn\|WarDamageTrigger" AI/*.cs AI/Brain/*.cs`,
  then `sed -n 205,216p AI/DirectorPatches.cs`, `sed -n 268,275p AI/Brain/Director.cs`, Tempo.cs 58-65.

Unknowns: whether any Nuclear Missile landed (not in the logs; 10 `fire-paid` nukes at jogi in m25 from T31); the exact
meaning of destination "own" (a non-enemy hit body or the AI's own planet); the human's actual roll spending (leftover
energy is a proxy); whether the level-1 original AIs' T7-T10 Railguns reflect level-3 native behaviour; the aim-time
cost of more than 30 weapons (unverified in game); whether `director.weaponUpgrades` counts bought or queued upgrades
(m25 note credits Sarai with projectile upgrades, the field says 0); the m25 abandon total (note 127, my per-type sum
107 because entries without an `xN` suffix were not counted).

END OF REPORT
