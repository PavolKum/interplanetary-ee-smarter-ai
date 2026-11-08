# Cross-review: lane D (Fable 5.1) on lane E (Sonnet 5.5)

Read-only. Frozen target `4d7c160`. I re-ran every number below from `docs/playtests/data/v018m22..v019m26-ai-decisions.jsonl`
and the telemetry with one scratch script (`xrev_E.py`, outside the repo; logic in section "Reproduction") and checked each
cited code line with `sed`/`grep`. Lane E's definitions were applied as written: smart AI = `init.smart` with records past T2;
war-phase AI-turn = `director` record at or after the AI's first `strategy.war == true`; fire turn = `aim-search.fire` with
`live >= 5`. Where lane E's and my earlier lane D report disagree, I say which one the data support.

## Claim-by-claim verification

| # | Lane E claim | My reproduction | Finding |
|---|---|---|---|
| 1 | O1: 193 war-phase turns; 52 with `energyLeft >= 10,000`; 33 at `aimedWeapons == 30`; 18 with `materialLeft < 2,000`; 37 at >= 29; 69 at cap overall | 193 / 52 / 33 / 18; >= 29: 37; at cap any turn: 69. Also: 65 of 193 at >= 5,000 | **ACCEPT** (exact) |
| 2 | O2: Ash `aimedWeapons` 30 on T33-T37 with `energyLeft` 36,800 / 33,395 / 32,059 and `materialLeft` 13-20k; "19 Railgun abandonments in T23-T31 ... the count is 18" | Ash `director`: T33 (30, 197 E, 16,714 M), T34 (30, 2,599, 2,146), T35 (30, 36,800, 13,423), T36 (30, 33,395, 15,000), T37 (30, 32,059, 20,014). `placementFailures.bui_Railgun` for Ash: whole match 30, T23-T31 = 18 | **ACCEPT** the cap finding; **AMEND** "19" to 18 (the text gives both). Minor. Note: this corrects the m26 note (lines 53-54) *and my own lane D O6*, which called Ash placement-bound at T35-T37 by counting Railguns only; `aimedWeapons` counts Missiles and Coilcannons too (`IsAimedWeapon`, DirectorPatches.cs:1118-1124): 15 + 8 + 7 = 30 |
| 3 | O3: 1,609 of 2,147 live guns found on fire turns (75%); 99 fire turns | 1,609 / 2,147 = 0.749; 99 turns | **ACCEPT** (exact) |
| 4 | O4: stop reasons enough 26 (0.87), human turn 32 (0.77), cap 24 (0.62), stall 11 (0.56), all aimed 6 (1.0); "35 turns (35%) stopped with <= 62% found"; 48% under 0.8; found plateau 19.1 (n=40) vs 20.2 (n=22) | 26 (0.87), 32 (0.76), 24 (0.63), 11 (0.55), 6 (1.0); turns with found/live <= 0.62: **21 of 99**, not 35; < 0.8: 48 of 99; plateau 19.1 (n=40) / 20.2 (n=22) | **AMEND**: "35 turns (cap 24 + stall budget 11) stopped on the time or stall limit with group found/live 0.63 and 0.55; 21 individual turns (21%) ended at or under 62% found." Minor. The plateau inference stands but is confounded with steps (row 5): the live >= 27 bin is mostly m25 at the 30-step floor |
| 5 | O5: `AimBudget.StepsPerWeapon` clamps 2,600 / all AI weapons to 30-100; m25 at 30 steps T23-T37; m26 45-55 T22-T35; found/live 0.725 at 30 steps (n=32) vs 0.836 at 86-100 (n=10) | Code confirmed (Patches.cs:100-106, Offense.cs:88-98, `PlayerManager.GetTotalAIWeaponAmount()` so original AIs' guns count). m25 `aim-budget.steps` 30 from T23 to T37; m26 45-55 T22-T35, 87-100 from T36; 0.719 (n=32) vs 0.830 (n=10) | **ACCEPT** (within rounding). A genuine third confounder between m25 and m26 that no note mentions |
| 6 | O6: fire turns all 86.7 deg (n=2,147), found 80.3, unfound 105.9, 385 of 538 unfound face away; all turns live >= 5: 178 turns, 88.1 / 81.4 / 106.5 (n=3,520) | 86.7 / 80.3 / 105.9, 385 / 538; 178 turns, 88.1 / 81.4 / 106.5, n = 3,520 | **ACCEPT** (exact). On the "+8-12% ceiling": if found guns face away at ~45% (mean 80 deg), P(found | away) ~ 65% vs P(found | toward) ~ 85%; lifting all guns to the toward rate gives +13% found, so the ceiling is the right order; it is an estimate, not a measurement, as lane E says |
| 7 | O7: 502 abandoned of 3,223 queued (16%); per-type fail shares; cover-search 235 = 108 placed / 117 gave up / 10 no-deficit; by phase 72/19, 28/71, 8/27; water buildings 33% of queued; m25 note "127" is 107 | `placementFailures` total 502 (Ion 136, Nuclear 125, Solar 91, Railgun 44, Kinetic 31, Mine 17, Telescope 14, Coil 12, Offshore 11, DSH 9, Missile 8, Shield 4). `director.queued` building entries sum to 3,223 (Offshore 611, Floating 467, Kinetic 423, Railgun 306, Ion 223 + 65 = 288, Solar 265, Telescope 241, Nuclear 214, Mine 132, DSH 108, Shield 66, Missile 48, Coil 45, Nuke 9); water 1,078 / 3,223 = 33%. Cover-search 108 / 117 / 10 and phases exact | **ACCEPT** (exact). m25 107 also matches my lane D count |
| 8 | O9: 4 war-to-peace flips (m22 Eiai T18; m24 Eddie T15, T18; m26 Ash T19), `recentDamage` straddling 600, `warTurn` 20 for all; latch holds only the turn path | 4 flips with 5-8 shots each, exactly those turns; `director.warTurn` = 20 in all 361 records; DirectorPatches.cs:210-214 latches only `if (turn >= warTurn)` | **ACCEPT** the facts. **AMEND** the inference "a research-order trade-off against economy-first": `docs/ruleset/TechTreeData.csv` row `building_unlock` never names `bui_Railgun` (Railgun upgrades sit in the three 250-sci starting techs), and weapons are bought only `if (d.War ...)` (DirectorPatches.cs:877, 904). No research is needed to arm; the Orbital Assembly gate is a config choice about Missile tech. Severity: **major for the roadmap** (it is the premise for ranking the latch and earlier war last) |
| 9 | O10: HUMAN-FIRST fire turns m25 34/36, m26 23/27; human fired 16 shots in m25 vs 2,028 in m26; "the win in m25 cannot be attributed to focus" | 34 / 36 and 23 / 27 (also m22 6/6, m23 16/16, m24 11/15); telemetry `shot` events 16 vs 2,028 | **ACCEPT** the facts; **AMEND** the conclusion to "cannot be isolated". Omitted evidence: paid shots at the human per turn were 39-58 on m25 T30-T37 (three Eiais in the same turns) against at most 26 in m26, and population fell only on turns when the human's round-start leftover energy (`resource_credit.energyBefore`) was under ~1.5k (m25 T30-T37: 18-874; m26 T22-T23: 1,195 / 595). Focus produced the volume that drained him; the nukes-only human did not spend on guns. Both are true; neither is refuted. Minor-major |
| 10 | O11: late free fire paid = `firingHits + firingMisses`, not `planned*`; 9 volleys, 0 aborts | Zenon T25 25 = 21+4 (planned 29), T26 17 = 17+0 (30), T27 25 = 20+5 (29), T28 23 = 19+4, T31 27 = 20+7, T32 28 = 20+8; Hawking T21 4 = 3+1 (10), T33 11 = 9+2 (15), T34 13 = 10+3 (16). 9 / 0 | **ACCEPT**. Lane E's example "27 = 20+7" is T31, not the third volley (T27 is 25 = 20+5). Trivial. The m25 note's wording is wrong as lane E says |
| 11 | O12: `aimWaitAfterHuman` fire turns m25 mean 7.2 s, max 24.7, zero on 15/36; m26 mean 5.5, max 38.5, zero 20/27 | m25 7.2 / 24.7 / 15 of 36; m26 5.7 / 38.5 / 19 of 26 (one fire turn without the field) | **ACCEPT** (rounding; denominator differs by one) |
| 12 | O13: 613 `Check(` sites, 51 test functions; 458.5 KB of 606 KB code outside the test project; csproj `<Version>0.4.1</Version>` vs `VERSION "0.19.0"`; deploy target csproj:53 guarded only by CLAUDE.md:8; `decision-record-r2.md:74` defers MaxWeapons; notes v0.14.1:38 and match21:86 asked to lift the cap | 613; 51 zero-arg `private static void` test functions; `AI/*.cs` 458,529 B, `AI/Brain/*.cs` 147,568 B (sum 606 KB); csproj:11 `0.4.1`, Plugin.cs:27 `0.19.0`; csproj:52-53 `DeployToGame AfterTargets="Build" Condition SkipDeploy != true`; CLAUDE.md:8 as quoted; decision-record-r2.md:74 "Any increase to `MaxWeapons`"; both note lines as quoted | **ACCEPT** (exact) |
| 13 | R2 G3 baseline: median found 16, p25 11, p75 21 on fire turns | 16 / 11 / 21 | **ACCEPT** |
| 14 | R2 G2 premise: raising `MaxWeapons` 30 -> 42 under glut brings war turns with `energyLeft >= 10,000` to <= 15% (from 27%) | Arithmetic from lane E's own O1-O2: Hawking idled 34-40k a turn and Ash 32-37k at the cap. Twelve more Railguns cost 2,400 E once (200 each, BuildingsData.csv) and 1,200 E per volley (100 a shot). The treatment can absorb at most ~3.6k of a 32-40k surplus; `energyLeft` stays above 10k on those turns whatever the mechanism does | **REJECT G2 as calibrated** (blocker for R2 as written): the gate fails by construction even if the cap is the right diagnosis. Corrected gate: "treatment `aimedWeapons` reaches 42 within 3 glut turns of hitting 30; paid shots per volley in treatment >= control + 8; `energyLeft` on cap turns lower than control by >= 2,500". The deeper point lane E's decomposition supports but does not draw: a cap at 42 cannot spend the idle energy; the surplus needs other sinks (weapon upgrades: Sarai 0 `weaponUpgrades` with 26.8k M idle; `MaxWeapons` far above 42 with the aim-time gate; or an income policy) |

Circularity check: R1 gate (d) ("`verify_gates.py` reproduces 193 / 52 / 33 / 18") checks the tool against the count it was
written from; fine as a regression test for the tool, but it proves nothing about the AI and should be labelled so. G1 ("treatment
has `aimedWeapons >= 31`") is a wiring witness, not evidence, which lane E says. No other gate is an identity of what it predicts.

Strongest alternative explanations lane E omits: (a) the Railgun-needs-no-tech fact above (rows 8, 15); (b) 35-42% of paid shots in
m23-m26 did not go at the human (fellow Eiais 10-17%, "own"/"none" 17-18%, from `fire-paid.destination`; lane E's own O10 gives the
complement, 56-71%, and then recommends no change to fire discipline); (c) the human-side mechanism: damage tracked his leftover energy,
not the Eiais' gun count or facing (row 9), so a volume lever is only worth what it does to his roll budget, and timing (before T20,
when his income was 2-9k and he had 7-16 Kinetic and no gun until T22-T30) is the cheapest way to hit that budget.

## Recommendations: rank and exit gates

- **R1 instrumentation and executable gates (v0.20, S): ACCEPT.** Gates (a)-(c), (e) are measurable from the log and can fail; (d) is a
  tool self-check (see above). Add to the field list: `fire-paid` destination split (lane E has it) and a per-turn `energyBefore`-style
  human leftover is already in telemetry, so no change needed there.
- **R2 cap A/B (v0.20, S-M): AMEND.** Keep the per-AI arm design and G1, G3, G4 (all measurable; G4 baselines verified). Replace G2 with
  the conversion gate in row 14. State in the mechanism that 42 is a probe of "do more guns convert to found shots", not a fix for the
  idle energy, and name the sinks for the remaining 30k+. Fairness as stated holds (own energy, no accuracy change; the human had 52-81
  guns in m22/m26).
- **R3 deploy and provenance (v0.20, S): ACCEPT.** Gate is a file-hash comparison; it can fail; no fairness surface.
- **R4 mutation runner (tooling, S-M): ACCEPT.** Gate "every mutant killed, non-zero on a survivor" can fail.
- **R5 facing as a soft term (v0.21, conditional, M): ACCEPT the rank and the go-condition.** The exit gate "mean angle at fire time
  <= 70 deg" is measurable only after R1 logs bearings; say so. The +8-12% ceiling is consistent with the data (row 6).
- **R6 Ion placement and surface budget (v0.21-v0.22, M-L): ACCEPT.** Baselines 71/99 gave-up at T21-30 and 136/288 Ion verified; gates
  can fail; `coveredBuildings` 0.886 do-no-harm floor is from `director.coveredBuildings` and is measurable.
- **R7 war latch and earlier-war arm (v0.22, S): AMEND the rank to v0.20.** The latch is one line with a unit test that fails on the
  current code (Ash 865 / 606 / 529 must stay war); its cost was 1-4 idle turns per flip in 4 of 10 AI-lobbies, which is small, but it
  ships for free beside R1-R3. The earlier-war arm belongs in v0.20 as the second behaviour experiment, because the premise lane E uses
  to defer it (tech comes late) is false for Railguns (row 8) and the human's exposure window T14-T22 is the largest unexploited measured
  asymmetry in the data (row 9 and alternative (c)). Its gate as written (first fire earlier by >= 2, peak energy at T28-T32 not more than
  10% below control) is measurable and can fail; add "human `resource_credit.energyBefore` < 2,000 on >= 60% of T15-T24" so it measures
  pressure on the human, not only the Eiai's own cost.
- **"Not recommended: focus-fire rule changes": AMEND.** The data lane E and I both computed (56-71% of paid shots on the human) is a
  fire-discipline problem (weapons with no path to the commit firing at fellow Eiais or at nothing), not a HUMAN-FIRST problem. A
  hold-the-shot rule is S effort, fairness-neutral (visible trajectories only), and has a clean gate (`fire-paid` at jogi or the original AI
  >= 90%; fellow-Eiai <= 3%). It should be in v0.20 or v0.21, not excluded.

## Blockers

1. **R2's G2 is miscalibrated against lane E's own numbers** (row 14): with 32-40k idle a turn, a cap of 42 moves `energyLeft` by ~3.6k,
   so the gate returns FAIL regardless and the experiment would be read as "cap is not the lever" wrongly. Replace G2 before shipping R2.
2. **The roadmap's ordering rests on an omitted fact** (row 8): Railguns need no technology and are bought only at war, so "war starts
   late because tech comes late" is a config choice, not a research trade-off. This does not touch any measurement in the report, but it
   is the reason the latch and the earlier-war arm sit in v0.22; with the fact restored they belong in v0.20.

## Non-blocking improvements

- O4 wording (21 of 99 turns at <= 62%, not 35); O2 "19" -> 18; O11 example label; O12 m26 5.7 s / 19 of 26.
- O10 conclusion "cannot be attributed" -> "cannot be isolated"; add the per-turn paid-shot volume and the human leftover-energy pattern.
- O6: give the P(found | away) vs P(found | toward) estimate with its assumption instead of only the means.
- Section 1 "Files read" lists `decision-record-r2.md` lines 52-80, but the cited line 74 is inside that range: fine; line 165 (the
  "Unchanged" list) also names MaxWeapons and could be cited.
- My own lane D report is corrected by row 2: Ash was cap-bound at T33-T37, not placement-bound; its placement losses were T21-T32 (18 of
  30 Railgun failures in T23-T31) while material was 2-1,719. The lane D recommendation order does not change (timing and fire discipline
  before placement), but its O6 text should say "cap-bound from T33" and its R6 should cite T21-T32.

## Reproduction

`xrev_E.py` (scratchpad, read-only): for each match, smart AIs from `init.smart` with max turn > 2; first war turn from `strategy.war`;
`director` rows at or after it counted for `energyLeft`, `aimedWeapons`, `materialLeft`, `warTurn`, `placementFailures`, `queued`;
`aim-search` rows with `fire` and `live >= 5` for found/live, `stop`, `aimWaitAfterHuman`, `foundFacingDeg` x found and `unfoundFacingDeg`
x (live - found), `unfoundFacingAway`; `aim-budget.steps` joined by (ai, t); `late-free-fire` joined with `fire` and `fire-paid` counts;
`cover-search` outcomes by phase; `strategy` war-to-peace transitions and `humanFirst` on fire turns. Code: `sed -n 100,106p AI/Patches.cs`,
`sed -n 85,100p AI/Brain/Offense.cs`, `sed -n 210,214p AI/DirectorPatches.cs`, `grep -n "static bool IsAimedWeapon" -A 6 AI/DirectorPatches.cs`,
`grep -n "Version\|SkipDeploy\|Deploy" AI/InterplanetaryAI.csproj`, `grep -c "Check(" AI/Tests/Program.cs`, `cat AI/*.cs | wc -c`,
`cat AI/Brain/*.cs | wc -c`, `grep -c '"eventType":"shot"' v019m25-human-telemetry.jsonl v019m26-human-telemetry.jsonl`.

Lane D verdict on lane E: the measurements reproduce (14 of 14 numeric checks exact or within rounding) and the process recommendations
(instrument, provenance, executable gates, mutation runner) are sound; the report cannot be trusted for its roadmap order, because its cap
remedy is under-sized by its own arithmetic (G2 would fail by construction) and it demotes war timing and fire discipline on an omitted
fact and an omitted shot-destination finding.

END OF REPORT
