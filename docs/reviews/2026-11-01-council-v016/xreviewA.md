## Cross-review of lane B (task 1)

**Method.** Every number below was re-derived from `v0153-ai-decisions.jsonl` (D16), `v0153-human-telemetry.jsonl` (H16), the Enhanced CSVs and `decompiled/`. Director records were read per turn; the CSVs were parsed with the csv module because multi-line cells break line-number citations (B's `ProjectsData.csv:6,9-10` style refs land on wrapped cells, but the values are right).

**Evidence claims E1-E7**

| Claim | Check | Verdict |
|---|---|---|
| E1 slots: 5 MRP by T7, 2 QEH by T11, SI T17, first SSTAR T31 | D16 director `queued`: MRP 1/2/1/1 at T4-T7, QEH T8 and T11, SI T17, `pro_EnergyYieldLongGlobal1` first at T31 (line 250) | ACCEPT |
| E1 seven slots at T14 | pops 344/401/336/544/694 vs 250/500/750 of 1,000 (`CitiesData` rows 25-28, `BuildingProjects.cs:430-447`) = 1+1+1+2+2 = 7; my extension: slots 7/8/9/10/11/12 at T12/T18/T20/T22/T23/T25 and permanents filled each one as it opened (QEH T20, T23, T25; Rare Material T22), so free slots were zero T7-T30 | ACCEPT |
| E1 no cancel after install turn | `BuildingProjects.cs:122-132` and `197-203` | ACCEPT |
| E1 SSTAR 150 E/300 M, 6 turns, x1.3/x0.9; product stacking | `ProjectsData` id/duration/multiplier rows; `GlobalStatModifier.cs:53-59` | ACCEPT |
| E1 12,816 x 1.3^3 = 28,157 vs human 29,410 | arithmetic exact; H16 T25 energyPerTurn 29,410 | ACCEPT |
| E1 forecast defect | `DirectorPatches.cs:1267` span 0.6, `:1286` gain x span, `:1058` added to projected income; `:1269` prices from live production | ACCEPT |
| E2 6 arrays T8, 30 by T26; 4,940 E upkeep | director `intelBuildings`; sum of turn-start counts T7-T25 = 247 array-turns x 20 = 4,940 | ACCEPT |
| E2 visibility table | director `unseenCities`: T8 0, T13 4, T14 3, T15 0, T16 4, T17 3, T18 2, T19 4, T20 3, T21 3, T22 3, T23+ 5. B's rows are exact; it corrects my report (see amendments) | ACCEPT |
| E3 T21 9 Missiles 7.65 at 100%, T22 13 shots 10.007 at 93.1%, two cities visible | strategy lines 100 and 108; unseen 3 both turns | ACCEPT |
| E3 45 blind strikes, random point | strike records 13+13+14+4+1 with `candidates 0`; `AIWeaponTargeting.cs:493-497`; `WeaponGameplayData.cs:145-148` | ACCEPT |
| E3 holds released 10,000 E | `releasedEnergy` 2,700/3,500/3,800 | ACCEPT |
| E4 TryWeapon has no targetability term | `DirectorPatches.cs:989-992` | ACCEPT |
| E4 Railgun package 90 projectiles, 280 E, 800 M, 300 E, 100/100 dmg, radius 9.6 | Triple Barrel +2 projectiles firing x2.8; Airburst radius x1.6 damage x0.8; costs 350+250 M, 50+50 E; two upgrade slots (`BuildingUpgrades.cs:11-17`) | ACCEPT numbers |
| E4 "scattered by the engine" | `WeaponGameplayData.cs:155-160` rotates the hit by a random angle up to +/-(90 - split angle); the split angle is prefab data, not in the CSVs | UNVERIFIED dispersion |
| E5 reservation, cap, accounting | `AIResources.cs:38-62`; release only on hold `StrategyPatches.cs:281-290`; cap `DirectorPatches.cs:646`; upkeep initialised live at `:504`, new weapons base at `:985`, deltas with global at `:1568`, repair base at `:883` | ACCEPT (low severity: no global firing modifier was active) |
| E5 material 40,043 / 46,426 / 47,256 | economy T25/T27/T28 | ACCEPT |
| E6 three Ion purchases, not 28 | `antiNukeQueued` 1 at T30, T31, T32; T30 placed (792 tries, 754 unplaced); T31 15 records, T32 12 records, each 1,800 tries all unplaced at consecutive steps | AMEND: `AntiNukeStep` draws fresh candidates every step (`DirectorPatches.cs:1933-1952`), so the 27 records are 27 fresh 1,800-candidate samples that all failed, not one failure re-reported |
| E6 prior 5 at T24, none queued; Plasma head T28; 400 science after Maglev | director `antiNukeWanted 5, antiNukeQueued 0` T24-T26; research line 197; `TechTreeData` | ACCEPT |
| E7 83 / 23 / 27 / 40 unpaid / 3,400 E | director T27 | ACCEPT |
| E7 v0.15.1 T37 figures | not re-opened; not load-bearing | UNVERIFIED |

**Two facts lane B's evidence did not surface.** Between the mod's research records at T19 and T28 the economy records show five off-plan heads (T23 MassBio, T24 Optics, T25 Uranium Alloys, T26 Induction Coils, T27 MagRad) with no mod `research` record. The mod yields at `EconomyPatches.cs:506-508` whenever the queue is non-empty or native urgent-intel research asks. That is why Plasma, 16th in the plan, was set only at T28 and the Ion prior at T24 could not execute. And the human's planet material hit zero at T30 (H16 planetMaterials 8,570 at T29, 0 from T30; materialsPerTurn 0), while the AI's planet still held 134,014 at T32.

**Proposals**

- **B1 ACCEPT, severity high.** Same diagnosis as A1 with the same arithmetic. Add two ruleset facts both lanes under-weighted: Jamming carries a x0.9 global population-growth multiplier per copy (`ProjectsData` turn_yield_population_multiplier_global), and SSTAR, Jamming, Rare Material and Shielded Comms set the host city's stealth to x0.75 (`BuildingProjectStats.cs:85`, used by `PlayerIntelHandler.cs:177-178` as stat x counterintel). Cap Jamming at 3 copies until the growth bite is measured.
- **B2 AMEND, severity medium.** Accept the targetability term and "no new Missiles while candidates are zero". Downgrade the Railgun package to a gated fallback: it needs 1,550 science (Ultracapacitors 800 and Composite Heat Cells 750, 18th and 33rd in the shipped plan), its dispersion is unverified, and the only baseline we have for blind fire is the 45 Missiles at T24-T28, after which the human's population rose 3,555 to 3,796 and his income did not move. Non-city visible targets in T12-T21 were only mines and Kinetics (stealth 20 and 15 vs 35 for plants, `StealthGroupsData`), so there is no "hit his plants" window either.
- **B3 ACCEPT, severity medium.** The unconditional objective is at `DirectorPatches.cs:580`, the native exemption at `:583`, the research escape at `EconomyPatches.cs:508`. Fairness condition: the stop-loss keys on own spend and `unseenCities`, never on the enemy multiplier. Include the Lidar upgrade in the bid (+4 intel for 50 E/175 M and +12 E upkeep; it has scored zero since T6 because `AddUpgradeOptions` values only energy, material and science, `:1154-1176`).
- **B4 AMEND, split.** The precision-opportunity rule: ACCEPT (there is no visible-city branch in `Strategy.Decide`, `Strategy.cs:246-276`). Per-class hit quotas: ACCEPT, low severity (the mod sets an aggregate hit count, `Patches.cs:318-328`, and T22's logged 0.827 equals the 11 guided/2 unguided mix; which weapons take the slots is native). The latched roster and reservation release: REJECT for v0.16. Income was the shortfall (human fired 10.5-12k E of 24-47k income, 25-45%, so 0.4 is human-like), and this is the one change whose failure mode is a resource cheat. **Blocker** if ever implemented without a reservation-reconciliation test.
- **B5 ACCEPT, severity medium.** Range x1.3 with probability x0.9 (area x1.69, footprint x1.52) and cost-down x0.7 cost / x0.9 radius (70 E per roll, 97 E per stop vs 125) reproduce from `UpgradesData`. Cost-down is on Maglev, already researched by T15 in match 16, so it is buyable now; range needs Early Warning AI (600, 26th in the plan). At T26 the same 3,400 E would have paid 48 rolls instead of 34.
- **B6 ACCEPT, lowest rank.** Numbers reproduce (5 Ions 1,000 E/1,250 M; pass 0.2 and 0.04 per `PlayerDefenseHandler.cs:229-243`). It is executable only with the research-hijack fix above.

**Blockers.** B4's reservation release (cheat risk). Any threshold computed from the enemy counterintel multiplier (my own A5 wording, see below). Any memory of city positions after they leave sight (both lanes agree).

## Amendments to lane A, ballot, non-negotiable (tasks 2-3)

**Amendments (supersede the named parts of laneA.md).**

1. §3.6 "all 5 cities seen T8-T12, 3-4 unseen T13-T22": replace with the per-turn series above (T15 fully visible, T18 two unseen). And `target.cities 5` at T21-T22 is the population-readability gate (`Patches.cs:216-221`: intel >= population stealth shows the total and the city count), not per-city visibility; two cities were targetable at T21-T22. The forfeited volley was 7.65-10 expected shots on two cities, not spread over five, which makes it more decisive, not less.
2. A1: add the Jamming population multiplier and the 3-copy cap; add the x0.75 stealth side effect on the AI's own cities.
3. A5: Lidar raises upkeep 20 to 32 per array (I omitted it); 20 upgraded arrays give 240 and the small-city requirement at T20-T21 rounds to 241, so 21 arrays. The "stop when ci x 45 exceeds 1.3 x reachable" rule reads a hidden value: withdrawn, replaced by B3's spend-and-outcome rule.
4. §3.5 and A3: add the T23-T27 research hijack as a cause of the late Plasma and Ultracapacitors.
5. New finding for §3: the human's planet ran dry at T30 while the AI's held 134k at T32.

**Round-1 ballot**

| Issue | Vote | Evidence / replacement wording |
|---|---|---|
| I1 | ACCEPT | Seven permanents in seven slots by T14, zero free slots T7-T30, first SSTAR T31. |
| I2 | AMEND | "Jamming in free slots up to 3 copies (not 5) because each copy multiplies global population growth by 0.9; EMC after Quantum Computing, reachable by T11-T12 on the AI's own cumulative science (3,605 by T11)." |
| I3 | ACCEPT | Energy left 5-155 on 20 of 26 turns while material banked 15-47k; the human ran 2.05 at war. |
| I4 | AMEND | "Economy-first order (A3) with three targeted moves: Plasma immediately after Maglev, Early Warning AI before T22, Ultracapacitors and Composite Heat Cells only when the artillery fallback is funded; plus a guard at EconomyPatches.cs:506-508 so no off-plan head survives more than one turn." Evidence: five off-plan heads T23-T27 with no mod record. |
| I5 | ACCEPT | T21: 9 guided shots at 100% pass on two visible cities with 0 Ion, held for MinPassingShots 12. |
| I6 | AMEND | "No new Missile while strike candidates are zero; Railguns instead. The Triple Barrel + Airburst package is a fallback gated on logged enemy loss per 1,000 E fired, because blind Missiles produced no measurable loss (human pop 3,555 to 3,796 across the volleys)." |
| I7 | AMEND | "Keep 0.4. Fix the base-versus-live firing cost at DirectorPatches.cs:985, 1568 and 883 as a correctness item. The latched roster is out of v0.16." |
| I8 | AMEND | "B3's budget and stop-loss on own spend and unseenCities is primary; A5's Lidar valuation (+4 intel, +12 E upkeep) sits inside the bid; 21 upgraded arrays, not 30 plain; no threshold from the enemy multiplier." |
| I9 | ACCEPT | 40 unpaid rolls at T26; cost-down already unlocked by Maglev; range needs Early Warning AI. |
| I10 | ACCEPT | Prior 5 at T24 unexecutable, Plasma set T28; add the hijack guard or it recurs. |

**Non-negotiable.** Every new rule runs on observable inputs only: own spend, own visibility results, currently visible candidates. No threshold or memory derived from the enemy counterintel multiplier, hidden positions or frozen snapshots. Why: Pavol's charter makes "smarter" mean better economy and timing; one hidden input turns the whole v0.16 result into a cheat and wastes the match that tests it.

## Pavol's weak windows and the combined v0.16 list (tasks 4-5)

**Weak windows in the usual build-out (H16, compared with v0141 and v0151).**

- **T1-T11:** zero defenses, counterintel x1.0, all five cities readable at 48 intel; energy 850-1,800. The same shape in v0141 (first Kinetic T15, DSH T13). He builds nothing defensive until he decides to, not in response to anything seen.
- **T12-T21:** 0 Ion all match (also v0141; Ions only in v0151 at T29 after seeing Missiles), Kinetics 5 to 23, cities intermittently readable at 48-160 intel, mines and Kinetics visible, plants never after T11. Science x2.4-3.1 from Jamming at T11-T14 and T17-T20 is when his techs run away.
- **T22-T24:** DSH 11 to 20 (x5.35 to x26), 20 to 35 Railguns built but unfired until T25. Nothing the AI does here lands.
- **T30 on:** planet material 0, income 0, 44-54k stock. If the AI survives to T35 with its 134k planet, the long game flips.

**Likely adaptations.** After a T19-T21 Missile strike on his two big cities he buys Ions within two turns (Plasma is one 400-science step from Maglev, which he has at T13) and surges DSH at T15 instead of T22. After early Railgun harassment he builds Kinetics at T8-T10 and may fire back early, the v0151 shape, which cost him 80% of his T20 energy (2,575 vs 12,947) and stretched the game to T39; an AI with the SSTAR engine should welcome that. After a strong AI energy curve he will see it (AI cities are readable to him all match) and may strike at T20 instead of T25, so the defense funding has to be in place by T19.

**Final combined v0.16 list (ranked)**

1. **Slot policy with SSTAR stacking and Jamming (A1/B1/I1/I2).** Permanents capped at 2 before Lossless, no QEH before an enemy intel building is seen, SSTAR in every free slot up to one per city once Lossless is done and the last turn ended energy-bound, Jamming up to 3 once EMC is done; score temporary projects by gain x duration / cost, and stop folding the 0.6 span into projected income (`DirectorPatches.cs:1267-1288`, `:1058`, `TryCityProject`, `CityForProject`, `Director.cs`; config `CounterintelFromTurn`). Gate: log free slots and installed projects per city; 3 SSTAR copies active by Lossless + 2 turns and `economy.energyPerTurn` >= 15,000 at T18.
2. **Research order 3.5 with the hijack guard (A3/I4/I10).** `[Research] Plan` migrated to the economy-first order (EMC by T12, Nano and Microwave by T14, Lossless T15, Plasma right after Maglev, Early Warning AI before T22); a guard at `EconomyPatches.cs:506-508` that logs every head change with a reason. Gate: `research` shows EMC head by T10, Microwave done by T15, Plasma done by T17, and no head outside the plan for more than one turn.
3. **Energy-to-material target 1.0 in growth, 2.0 at war, glut at 1.5 turns (A2/I3).** Config `EnergyToMaterialTarget`, `MaterialGlutRatio`. Gate: `director.materialLeft` <= 2 x `energyStart` on every turn T14-T22, `energyLeft` > 300 on at least three of them.
4. **Strike window and weapon choice (A4/B4-rule/B2-term/I5/I6/I7).** Precision-opportunity rule in `Strategy.Decide`: fire when a visible city exists, guided shots >= 6 and pass share >= 0.9, below MinPassingShots; `WarFromTurn` 20 to 16 with preconditions (Orbital Assembly done, income >= 12k); per-class hit quotas in `Patches.cs:318-328`; a targetability factor in `TryWeapon` so Missiles are not bought at zero candidates; base-versus-live cost fix at `:985`, `:1568`, `:883`. Gate: zero `below-saturation` holds while `unseenCities` < 5 and guided >= 6; first `strike` with `candidates` > 0 by T20; no Missile queued on a turn with zero candidates.
5. **Intel budget and stop-loss with Lidar (B3/A5/I8).** `IntelPerTurnLimit` and step 3 replaced by a bounded bid: 6 arrays by T8, Lidar valued in `AddUpgradeOptions` when `unseenCities` > 0, one further batch only after a measured visibility gain, stop after two failed batches; `MaxIntelBuildings` 30 to 21. Gate: `intelBuildings` <= 21 through T25; `intel-policy=stop-loss` logged within two turns of `unseenCities` reaching 5; `unseenCities` = 0 on T19 if the window exists.
6. **Defense funding (A6/B5/B6/I9/I10).** `DefenseReserve` floor = 100 E x last turn's in-range impacts x 1.25 when `lastUnpaidRolls` > 0, `DefenseReserveShare` 0.3 to 0.4; Kinetic cost-down now and range once Early Warning AI lands; one Ion per city prior from T20 with the ring search as is. Gate: `lastUnpaidRolls` = 0 on every turn with `lastImpacts` > 20; >= 10 Kinetics upgraded by T22; `minCityCover` >= 1 by T20.

**Not in v0.16:** B4's latched roster and reservation release; the blind Railgun package as a primary weapon (fallback only, with the loss-per-1,000-E gate); any `MaxWeapons` increase; any accuracy change.

END-OF-REPORT
