# Lane A report: game math and economy for v0.16

## 1. Scope and frozen target

Read at commit `2468368` (HEAD): the charter; `AI/README.md`; `docs/PROGRESS.md`; playtest notes v0.13.1, v0.14.1, v0.15.1, v0.15.3; `docs/strategy/game-math-report.md`; `docs/original-mechanics.md` (sections 0-7); `docs/reviews/sol-ai-v0.15.2-disposition.md` and `audit-v0.14.2-synthesis.md`; the ruleset CSVs under `docs/ruleset/embedded/enhanced/` (Buildings, Weapons, Projects, Upgrades, TechTree, Cities, StartingValues); `AI/Brain/Director.cs`, `EconomyPlan.cs`, `Strategy.cs` (Decide), `Offense.cs` (ScienceValue); `AI/DirectorPatches.cs` lines 240-310, 960-1060, 1090-1340; `AI/Plugin.cs` config defaults; decompiled `PlayerResourceHandler.cs`, `AIResources.cs`, `AIProjectEntity.cs`, `AIEnergyGenerationEvaluation.cs`, `Interplanetary.Prefabs/BuildingProjects.cs`, `GlobalStatModifier.cs`. Raw data: `v0153-*`, `v0141-*`, `v0151-*` decision logs and telemetry, extracted per turn with short inline Python and the two allowed tools.

Not accessed: `BuildingStatHolder.cs` (path in the game-math report is stale; I did not search for it, so population growth and science-per-pop are taken from `CitiesData.csv` and the telemetry, which agree). The human telemetry does not record projects, so the human's Jamming and SSTAR use is inferred from science and energy jumps (arithmetic shown). Working tree porcelain before and after my run is identical (5 untracked paths, nothing modified).

## 2. Verdict

The AI loses to this build-out because its economy is pinned to its material income by the director's 0.6 energy-to-material target while every city project slot is filled with permanent projects by T11, so the two multipliers that made Pavol's curve (Jamming x3 science at T11-T14, SSTAR x1.7-2.5 energy from T20) were never available to it; it then arrives at the only strike window it will ever get (T19-T22, cities visible, 0 Ion) with 9-15 Missiles capped by a 0.4 x income firing share. The one change most likely to flip it: a project-slot policy that keeps slots free for SSTAR and Jamming (permanent projects capped at 2 total until Lossless), which on the match-16 numbers lifts energy from 7.3k to 16-27k at T20 and funds 25-30 Missiles two turns before the cities go dark. Confidence: Medium (the slot diagnosis is arithmetic on logged populations, not a logged free-slot count).

## 3. Evidence

### 3.1 The curves (per turn, from `economy`/`director` records and telemetry `turn_end` states)

| Turn | Human E | Human M | Human sci | AI E (Labo) | AI M | AI sci | AI stock | AI energyLeft |
|---|---|---|---|---|---|---|---|---|
| 10 | 1,800 | 3,321 | 375 | 1,205 | 2,025 | 367 | 4,225 | 5 |
| 12 | 1,680 | 3,759 | 1,221 | 1,905 | 2,369 | 389 | 5,738 | 55 |
| 15 | 3,310 | 3,759 | 552 | 4,625 | 3,898 | 432 | 3,912 | 1,425 |
| 16 | 3,310 | 7,457 | 463 | 4,625 | 5,148 | 446 | 5,260 | 1,025 |
| 18 | 4,830 | 10,716 | 988 | 4,545 | 7,714 | 555 | 9,112 | 45 |
| 20 | 12,947 | 10,869 | 1,074 | 7,265 | 7,858 | 603 | 14,798 | 36 |
| 22 | 17,434 | 17,709 | 595 | 9,735 | 9,758 | 654 | 15,132 | 15 |
| 24 | 29,410 | 14,344 | 640 | 11,476 | 19,192 | 705 | 26,966 | 34 |
| 26 | 24,168 | 15,938 | 683 | 14,725 | 16,758 | 717 | 43,726 | 45 |

Bolide (v0.14.1) for comparison: T16 4,025 E; T18 5,545; T19 9,305 (Floating Yield1 x47); T20 17,740 (Yield2 x48); T27 28,961. Its human opponent: 10,110 at T20. Bolide was 48 Floating Plants and 33 Offshore Mines at T20; Labo was 12 Floating, 18 Nuclear, 34 Offshore.

### 3.2 Energy is the binding resource all match; the target ratio keeps it there

- `director.energyLeft` is 5-155 on 20 of 26 turns T6-T26 while `materialLeft` is 1.4k-6.9k at T17-T19 and 21k-40k at T24-T26 (table above). Energy is spent to zero; material banks.
- Spending at war (income minus stock change): T24 material spent = 19,192 - (26,966 - 18,699) = 10,925 against 11,476 energy; T25: 18,802 - 13,077 = 5,725 against 12,816; T26: 16,758 - 3,683 = 13,075 against 14,725. So the AI spends 0.5-1.0 material per energy, while its income is 1.3-1.7 material per energy.
- Why: `EconomyDirector.WantedEnergy` = 0.6 x min(material income, sustainable) + upkeep (`Director.cs:115-121`, `Plugin.cs:458` default 0.6). At T24: 0.6 x min(19,192, 11,062) + 4,550 = 11,187; actual 11,476. At T16: 0.6 x 5,148 = 3,089 < 4,625 actual, so `Weights` returns energyWeight 0.5, materialWeight 1.0 (`Director.cs:106-110`) and the next buys are material: Offshore Mines 22 -> 32 -> 48 -> 73 (T16-T24) while plants stayed at 12 Floating + 18 Nuclear. `MaterialGlut` (stock >= 3 x energy budget, `Director.cs:198`) did not trigger at T16-T18 (5.3k-9.1k vs 3 x 4.6k = 13.9k) because three turns of energy budget is already far more material than one turn can spend.
- The human's ratio at war: 29,410 / 14,344 = 2.05 (T24-T25), 46,991 / 8,570 = 5.5 (T29). Bolide, which out-produced its human, ran 2.1 at T20-T27.

### 3.3 Project slots: filled with permanent projects by T11, so SSTAR and Jamming were impossible

- Slots per city unlock at 25/50/75% of `population_max` 1000 (`CitiesData.csv` `project_slot_N_unlock_population` 0.25/0.5/0.75; `BuildingProjects.cs:447`). Cities start at 200/200/200/400/550 pop, so 3 slots at T1, 6 at T6 (small cities cross 250), 7 at T12, 8 at T18, 9 at T20, 10 at T22, 11 at T24 (computed from `director.ownCityPop` each turn).
- Permanent projects queued (`director.queued`): Molecular Recycling Plant x5 at T4-T7 (exactly as slots opened: 1, 2, 1, 1), Quantum Encryption Hub (+50 counterintel) at T8, T11, T20, T23, T25, Science Institute at T17. Permanent count by turn: 7 at T11, 8 at T17, 9 at T20, 10 at T23. Slots: 7 at T12-T16, 8 at T18, 9 at T20. **Free slots: zero on every turn from T7 to T30.** The first SSTAR (`pro_EnergyYieldLongGlobal1`) was bought at T31, the one Rare-Material project at T22 (into the only slot that opened, with 15k material banked).
- A placed permanent project can never be cancelled: `IsProjectCancellable` returns true only for projects installed this turn (`BuildingProjects.cs:122-132`), and `TryInstallProject` refuses an occupied slot otherwise (`:199-203`).
- The valuation was not the blocker: `AddProjectOptions` values SSTAR at EnergyProduction x (1.3 - 1) x span 0.6 (`DirectorPatches.cs:1267-1269`); at T16 that is 4,625 x 0.18 = 833 per (300 M + 0.3 x 150 E) = 2.4 per cost unit, ten times a Floating Plant (100 / 375 = 0.27). It would have been bought on any turn with a free slot. SSTAR is listed natively (`AIProjectEntity.IsValidForEnergyGeneration`, multiplier_global > 1).
- The game stacks global multipliers as a product (`GlobalStatModifier.recalculateTotalAmount`, verified), so n copies give 1.3^n energy and 0.9^n material: 3 copies x2.20 / x0.73, 5 copies x3.71 / x0.59.
- What it would have given Labo (energy, material income after the 0.9 per copy): T16 with 2 free slots: 4,625 x 1.69 = 7.8k; T20 with 4 free: 7,265 x 2.86 = 20.8k (vs human 12.9k), material 7,858 x 0.66 = 5.2k; T24 with 5: 11,476 x 3.71 = 42.6k (vs 29.4k), material 19,192 x 0.59 = 11.3k. Cost 300 M per city per 6 turns against a 15-47k stock.
- The human did exactly this. His energy T23 -> T24: 17,049 -> 29,410 = x1.73 while material 17,826 -> 14,344 = x0.80 = 0.9^2 (two copies landed T24, plus Orbital Solar Plants); x2.5 at T29.

### 3.4 Science: Jamming is what put the human 8 turns ahead on the yield techs

- Human science 375 (T10) -> 945 (T11) -> 1,221 (T12). Base at T11 = 2,150 pop x 0.18 = 387; 387 x 1.25^4 = 945 (four Jammings); T12: 2,216 x 0.18 = 399, x 1.25^5 = 1,217 (five). Expired T15 (552 = 442 x 1.25, one left). Second cycle T17-T20 at x1.95 (three copies: 943 / 484). Cost: 8 x 900 = 7,200 M. Extra science over the two cycles: about 5,100. The techs it bought in T12-T21 total 11,150 science (2ndGen, MagRad, EarlyWarning, Maglev, Microwave, Nano, SmartGrids, Robotic Mining, DesignerNano, Metamaterials, Optics, 4thGen, Lossless, AdvSignal, CompositeHeatCells, QuantumComm, Ultracap).
- The AI (no Jamming, Experimental Matter Control only at ~T18) made 378-603 a turn T11-T20 and reached Nanostructuring/Microwave at T22 (`upg_FloatingEnergyYield2` x12 queued T22; research record T19 lists them 2nd and 3rd behind Orbital Assembly). The human had them at T14. Robotic Mining never.
- Cumulative AI science by turn (sum of `sciencePerTurn`): T6 1,817; T9 2,860; T11 3,605; T12 3,994; T15 5,246; T20 7,896. Experimental Matter Control's chain (MassBio 250, MolRec 400, SuperTensile 650, QuantumComputing 750, EMC 1,300 = 3,350) is therefore reachable by T11-T12 if researched first; the AI spent its first 1,150 on Optics + AdvSignal + SmartGrids instead (research records T1, T5).
- Director science valuation: Jamming's score at T12 would be 389 x 0.25 x span 0.4 x scienceWeight 3.75 / (900 + 60) = 0.15, below a Floating Plant (0.27). So even with a slot the scorer would not buy it. Science is valued at 3.75 energy per point (`Offense.cs:76-83`, base 1.5 x 2.5 cap); the marginal value in T12-T15 is the yield techs: Nano + Microwave (1,350 sci) on 22-31 Floating Plants = +5,500-7,750 E per turn four turns earlier = 22-31k energy, i.e. 16-23 energy per science point.

### 3.5 Research order the AI ran versus what the science budget allows

Actual (queue heads, `research` records): Optics T4, AdvSignal ~T6, SmartGrids ~T7, SuperTensile ~T9, QuantumComputing ~T11, 4thGen ~T12, Lossless ~T14, 2ndGen/Maglev T14-T15, EMC ~T18, Orbital Assembly ~T20, Nano ~T21, Microwave T22, Plasma/Ultracap/EmDrives T28-T29. Consequences in the data: first Offshore Mine T10 (human T7); Floating fleet at x1 until T22; SSTAR available from ~T14 but no slot; Missiles buildable only from ~T20.

Feasible order with the AI's own science (completion turn = first turn cumulative science covers the cost, using the T1-T12 figures above; from T13 assumes four Jammings, x2.44, about 1,000 a turn):

| # | Tech (cost) | Cumulative | Done | What it buys |
|---|---|---|---|---|
| 1-2 | MassBio 250, MolRec 400 | 650 | T4 | Solar Y1, Mine Y1, MRP |
| 3 | SuperTensile 650 | 1,300 | T6 | Offshore Mines (human: T6) |
| 4 | SmartGrids 500 | 1,800 | T7 | Floating Plants |
| 5 | QuantumComputing 750 | 2,550 | T9 | (Science Institute; MineYield2 is NOT here in the shipped Enhanced file, `original-mechanics.md` 0.1) |
| 6 | Experimental Matter Control 1,300 | 3,850 | T12 | Jamming x1.25 global per city |
| 7-8 | Nano 600, Microwave 750 | 5,200 | T14 | Floating x3.5 |
| 9-10 | 4thGen 500, Lossless 800 | 6,500 | T15 | SSTAR x1.3 per city; Nuclear Y2 |
| 11 | Robotic Mining 1,100 | 7,600 | T16 | Mine/Offshore x2 (restores material after SSTAR's 0.9^n) |
| 12-13 | Optics 250, AdvSignal 400 | 8,250 | T17 | Telescopes, Telescope intel x1.5 upgrade |
| 14 | Orbital Assembly 650 | 8,900 | T18 | Missiles |
| 15-17 | 2ndGen 250, Maglev 400, MagRad 400 | 9,950 | T19 | Kinetic, DSH |
| 18 | CompositeHeatCells 750 | 10,700 | T20 | Nuclear Y1, Railgun Airburst |

### 3.6 The strike window and the firing cap

- City visibility (`director.unseenCities`, `ownIntel`; human `counterintelMultiplier`): all 5 cities seen T8-T12 (intel 48 vs ci 1.0 x 45), 3-4 unseen T13-T22 (intel 48-176 vs small-city requirement 45 x 2.05-5.35 = 92-241), all 5 unseen from T23 (ci 17.9-27.5: 804-1,237 needed). The AI never installed `upg_RadiotelescopeIntelYield1` (x1.5, 50 E / 175 M): `AddUpgradeOptions` values only energy, material and science gains (`DirectorPatches.cs:1154-1176`), so intel upgrades score 0. With it, 20 Telescopes give 240 intel at T20 (241 needed for small cities, 160 for big): all five cities visible one turn longer and the big/medium ones two turns longer.
- Weapons were exactly firing-cap bound: cap = 0.4 x income (`DirectorPatches.cs:646`); T20 2,906 -> upkeep 2,700 (9 Missiles); T21 3,570 -> 3,500; T22 3,894 -> 3,800; T23 4,605 -> 4,550; T24 4,590 -> 4,550; T25 5,126 -> 5,100; T26 5,890 -> 5,850. For 30 Missiles (9,000 firing) the cap needs 22.5k income; the human fired 105-120 shots (10.5-12k) from 24-47k income, 25-45%, so 0.4 is human-like and the shortfall is income.
- The hold: T21-T23 `strategy` records show 9/13/15 Missiles, 7.65/10.0/10.35 shots expected through at 93-100% share, reason `below-saturation` (`MinPassingShots` 12, `Strategy.cs:246,276`), target `cities 5, cityPop 3183-3307` at T21-T22, `cities 0` from T23. The forfeited expected impacts, 28 x 150 pop x 0.7-0.85 accuracy = 2,900-3,600 pop, equal the human's whole population (3,183-3,431). He had 0 Ion all match (telemetry `bui_IonDefense` 0 on every turn).
- Defense energy at the volley (`director` T27): 83 impacts, 56 with an eligible Kinetic in range, 23 intercepted, **40 rolls unpaid**, 3,400 E spent against a 2,679-3,080 reserve. Energy, not coverage, capped interceptions in the decisive turn; at 100 E per roll the 56 in-range impacts needed ~7,000 E.

### 3.7 Opening (T1-T8), both sides

Human: T1 Solar + Mine; T2 Solar + Mine; T3 +2 Mines; T4 +2 Mines (6); T5 Mine Yield1 x5-6 (800 -> 1,175 M); T6 Solar; T7 Solar + Mine + Offshore; T8-T10 Offshore to 9. Energy 850-950 to T5, 1,275 T6, 1,625 T8, 1,800 T10. AI: T1 Solar; T2-T3 2+2 Mines; T4-T7 Yield1 + MRP projects (material-bound: 25-50 M left, 575-675 E unspent at T4-T5); T7-T8 six Telescopes (energy 925 -> 805, upkeep 120); T9 first plants; T10 first Offshore. Material is a wash to T8 (AI 1,900 vs 1,725); the AI gives up ~400 E/turn T7-T12 and three turns of Offshore Mines (~1,100 M/turn by T10). With strict visible-only targeting the T8 sighting has no later value; the intel only matters on the turns the AI can fire guided weapons.

## 4. Proposals (ranked)

**A1. Project-slot policy: keep slots for stacking multipliers.** Mechanism (`DirectorPatches.AddProjectOptions` / `TryEconomy`, pure rule in `Director.cs`): permanent projects capped at 2 total before Lossless (MRP in the big and medium city only; no Quantum Encryption Hub while no enemy intel building has been seen); once Lossless is done and last turn ended energy-bound (energyLeft < 10% of budget), queue SSTAR in every free slot up to one per city; once EMC is done and the plan has >= 1,500 science of economy techs left, Jamming in free slots up to 5; value temporary projects by gain x duration / cost, not gain x duration/10. Fair: the human's own tools, 300-900 M per copy. Expected: energy x2.2 (3 copies) to x3.71 (5) within one turn of Lossless; on Labo's T20 numbers 16-27k instead of 7.3k; science x2.4-3.05 at T12-T15 (Nano/Microwave T14 instead of T22). Risk: SSTAR's 0.9^n cuts material 27-41%; Robotic Mining must follow within two turns (order above does), and on a 100k planet the lower drain is a benefit. Gate: `director` logs `projectSlotsFree` and installed copies per project id; next match SSTAR copies >= 3 by Lossless + 2 turns and `economy.energyPerTurn` >= 15,000 at T18.

**A2. Energy-to-material target 0.6 -> 1.0 in growth, 2.0 at war; material glut at 1.5 turns, not 3.** Mechanism: `Plugin.EnergyToMaterialTarget` and `MaterialGlutRatio`; `WantedEnergy` unchanged otherwise. Fair: a spending rule. Expected: from T14 (M 3,332 > E 3,265) energyWeight stays 1.0, so the ~40 Offshore Mines bought T14-T24 (14k M, 12k E) become ~40 Floating Plants: +4,000 E at base, +14,000 with Yield 1+2. Labo at T20: ~11k before SSTAR. Risk: material can bind at T16-T18 under SSTAR x4; the ratio 2.0 at war is the human's measured 2.05-2.2. Gate: `director.materialLeft` <= 2 x `energyStart` on every turn T14-T22 and `energyLeft` > 300 on at least 3 of them.

**A3. Research order 3.5: economy chain first, intel and weapons timed to the strike window.** Mechanism: `[Research] Plan` default (`Plugin.cs:164-215`) migrated once as before. Fair. Expected: Offshore T6 (+1,100 M/turn by T10), EMC T12, Floating x3.5 at T14, SSTAR T15, Missiles T18, Kinetic T19. Risk: Kinetic arrives T19 instead of T15; the human fires nothing before T25 in this shape, and the v0.15.1 early-fire shape (first 3 shots T8) needs the retaliation trigger, which exists (`WarDamageTrigger`). Gate: `research` record shows `tec_ExperimentalMatterControl` head by T10 and `tec_MicrowaveEnergyTranmission` done by T15; `sciencePerTurn` >= 900 on T13-T15.

**A4. Weapons from T16, sized to the window, not to T20.** Mechanism: `WarFromTurn` 20 -> 16 with a precondition (Orbital Assembly done and income >= 12k) so weapons start when SSTAR has landed; keep `WarUpkeepShare` 0.4. Arithmetic: 25-30 Missiles = 6,250-7,500 E and 11-13.5k M to build over T17-T18, 7.5-9k firing per turn; at 20-27k income that is 28-45% of income. Target: 0.7-0.85 x 150 pop x 25-30 shots = 2,600-3,800 pop per volley against the human's 2,935-3,183 at T19-T21, two volleys at the weakest cities first. Fair: 0.7/0.85 accuracy, visible cities only. Risk: slots (30 x 1 power connector; Bolide had 100+ free at T20) and the aim budget (30 weapons at 87 steps with 2600, per the v0.14.2 audit). Gate: `strategy.shots` >= 25 at T19 and the first `strike` record with `candidates` > 0 no later than T20.

**A5. Telescope intel upgrade and a 20-Telescope ceiling timed T15-T19; no race after the cities go dark.** Mechanism: `AddUpgradeOptions` gives `turn_yield_intel_multiplier` > 1 a gain when `unseenCities` > 0 (50 E / 175 M for +4 intel per Telescope); `IntelPerTurnLimit` 0 once ci x 45 exceeds 1.3 x reachable intel. Arithmetic: 20 Telescopes x 12 = 240 vs small-city requirement 209 (T19), 241 (T20), 286 (T21); 30 upgraded Telescopes (360) never reach 734 (T22). Fair. Expected: all five cities visible on the volley turns; 10 fewer Telescopes (3,000 M, 200 E/turn). Risk: the window still closes at T21-T22 against this counterintel curve; the strike must be done by then. Gate: `director.unseenCities` = 0 on T19 and T20 with `intelBuildings` <= 21.

**A6. Defense energy sized to in-range impacts at war.** Mechanism: `DefenseReserve` floor = 100 E x (last turn's impacts with an eligible defense) x 1.25 when `lastUnpaidRolls` > 0, inside the existing `DefenseReserveShare` cap raised 0.3 -> 0.4. Arithmetic: T26 needed ~7,000 E for 56 in-range impacts; the reserve held 3,080 and 40 rolls went unpaid; at p 0.8 the unpaid rolls were ~30 more stops, 17 fewer lost structures per the 3.4-per-impact rate. Fair. Risk: 7k of a 14.7k income leaves less for repair; with A1-A2 the income is 2-3x. Gate: `lastUnpaidRolls` = 0 on every turn with `lastImpacts` > 20.

## 5. What NOT to do

- Do not chase intel after T22 with buildings: counterintel projects stack x1.5 per city (x16-27 observed); 30 Telescopes cost 9,000 M and 600 E/turn and never saw a city.
- Do not buy more material producers or material projects while material is banked (T22 Rare Material Synthesis with 15k stock; 40 Offshore Mines T16-T24).
- Do not place permanent projects in every city early (5 MRP + 5 Quantum Encryption Hubs here; the human had no Telescope until T24). The slot is gone for the match.
- Do not raise `MaxWeapons` past what the aim budget serves, and do not cap energy (the v0.12 lesson stands).
- Do not hold a visible-city Missile volley for `MinPassingShots` when the target has 0 Ion: pass share is ~1.0 by construction and the cities will not stay visible.
- Do not weaken the firing share to buy more weapons; income, not the share, is the shortfall.

## 6. The strongest reason this may be wrong

The energy projections assume the AI can place 45-50 Floating Plants by T18 as Bolide did; Labo placed 12 and chose Nuclear and Offshore by score, so A1-A3 may lift energy less than computed if the scorer still prefers Nuclear Yield2-only plants (400 E for 650 M plus 90 M/turn) over Floating (350 E for 650 M), and SSTAR x3.7 on a 7k base is still only 27k. The Missile plan also assumes the human keeps building 0 Ion and late counterintel; a player struck once by 25 Missiles at T19 will adapt, so this edge is one match deep unless the blind-fire plan (Railgun Triple Barrel + Airburst, 6,000 HP per 1,000 E through 30 Kinetics by the game-math report's model) is funded by the same economy.

## 7. Unknowns and the single measurement for each

- Free project slots per turn are inferred from population thresholds: log `projectSlotsFree` and the installed project list per city.
- Why Labo stopped at 12 Floating Plants (water, score or placement failures): log the top three `EconOption` scores per economy step with `placementFailures` by type.
- Whether the human runs SSTAR/Jamming by choice or habit (telemetry lacks projects): add the active project list to the recorder, or read the visible global multipliers from the AI's intel view each turn.
- Whether 25-30 Missiles find trajectories on one turn at 2600 aim budget: `fire.primaryFound` / `retainedEntries` on the first strike turn.
- Human pop per city at T19-T21 (the volley allocation target): the `target` record's `cityPop` is a total; log per-city visible population when `cities` > 0.
- How much SSTAR's 0.9^n material cut binds under plan A: `materialLeft` on the three turns after the third copy.

END-OF-REPORT
