I’ll trace the reported failures from the match logs through the mod and the decompiled placement rules, then rank changes against measurable playtest gates. I’ll keep the repository read-only.
The “peace” flip is already distinguishable from target switching: construction and firing share a scheduled war turn, but an early damage-triggered war may have a different lifecycle. I’m checking that latch against the actual turn records before calling it a bug.
Two important corrections have emerged: Ash’s “14–15 railguns” were not its whole arsenal—the director counted 30 aimed weapons—and early retaliation can genuinely revert to peace because the existing latch covers only scheduled war. I’m checking placement failure denominators and whether the failures indicate full terrain or a narrower search constraint.
The win note also overstates focus: in match 25, two AIs switched to Beastie on T23, and most of their paid trajectories that turn pointed at the other Smarter AIs. That makes actual destination telemetry—not just the committed target—essential to the next gates.
# Lane A report: code and game mechanics

## 1. Scope and frozen target

**HEAD:** `4d7c160161e0ea0037ef44474077bd0ff9666a31`. Tracked code was unchanged. The match logs and extracted ruleset are local, untracked or ignored evidence; the commit alone does not freeze them.

**Substantively inspected:**

- `AI/README.md`, `AI/Plugin.cs`, `AI/DirectorPatches.cs`, `AI/StrategyPatches.cs`, `AI/Patches.cs`, `AI/AimWatch.cs`, and `AI/Brain/{Coverage,Director,Memory,Strategy,TargetPlanner,Tempo}.cs`; targeted searches also inspected `AI/Tests/Program.cs`.
- `docs/PROGRESS.md`; match 22–26 playtest notes; `v018m23-codex-handoff.md`; match 24 and 26 monitor excerpts; all five `v018m22`–`v019m26` AI decision logs. I inspected only the opening/schema of match 26 human telemetry, not its full contents.
- `docs/reviews/terra-ai-v0.19{,b,c,-disposition}.md`.
- `docs/ruleset/embedded/enhanced/{BuildingsData,TechTreeData,WeaponsData}.csv`.
- Relevant `decompiled/` placement search, queue, resource, targeting, trajectory, power-grid, building, weapon, technology-availability and solar-system simulator files cited below. Repository-wide targeted searches were also used to locate mechanics.

Local Git blob hashes for the match 25 and 26 decision logs are respectively `43301e7…` and `104343a…`; these are content fingerprints, not evidence that the files belong to the commit.

## 2. Verdict and confidence

**verdict: Ship a measured v0.20 with target-aware placement, placement telemetry and the early-war latch fix; investigate secondary-target fire and connection-constrained defense placement before raising the weapon cap or changing research. Confidence: Medium.** The war-latch mechanism and the 30-weapon cap are directly established in code and logs. Placement direction is plausible but not proved causal; the logs do not establish that failed searches exhausted legal space, and the two v0.19 matches changed both lobby size and human strategy.

## 3. Observations with evidence

1. **FACT — “War” can genuinely revert to peace before the scheduled start.** `WarTurnFor` latches only when the *scheduled* turn arrives; damage-triggered retaliation is computed separately each turn (`AI/DirectorPatches.cs:206–214,391–392`; `AI/Brain/Director.cs:271–274`). Ash’s recent damage was **865.274, 605.691 and 528.984** on T17–19 against the default **600** threshold: war on T17–18, peace on T19 (`v019m26-ai-decisions.jsonl:243,279,298–299`; command **R2**). Eddie showed the same pattern twice: T14 war, T15–16 peace, T17 war, T18–19 peace (`v018m24-ai-decisions.jsonl:185–186,203–204,245–246,264–265,291–292`). This is **not per-opponent war**.

2. **FACT — The late start is not simply late Railgun technology.** The enhanced ruleset has no Railgun building unlock; Orbital Assembly unlocks **Missiles** (`docs/ruleset/embedded/enhanced/TechTreeData.csv:2,9`; command **R6**). In match 25, all three Eiais were below the **12,000** early-war income gate at T19: 6,444, 11,694 and 7,344. Donnels already had Orbital Assembly; Zenon and Sarai did not (`v019m25-ai-decisions.jsonl:456–471`; **R3**). T20 was the fallback (`AI/Brain/Tempo.cs:56–64`; `AI/Plugin.cs:659–661`). Earlier war is a policy tradeoff, not a missing-tech fix.

3. **FACT — Weapon “facing” is primarily location, not a rotatable gun setting.** `ShelterStep` samples random positions around eligible anchors, ranks them for cover and city proximity, and has no target-direction term (`AI/DirectorPatches.cs:2794–2888`; `AI/Brain/Coverage.cs:368–385`). The game derives the shooting position from the building’s projected position relative to its planet, then samples launch angles around that outward direction (`decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:419–429`; `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:127–139,222–234`). Planets also orbit and rotate (`decompiled/Interplanetary.Solarsystem/SolarSystemSimulator.cs:127–140`).

   **INFERENCE —** Random, cover-first placement plus changing planetary geometry can leave guns facing away from a later commit. It does not prove they cannot reach it: on Hawking T21, **10/10** commit-facing guns found trajectories while their mean found angle was **113.755°** (`v019m26-ai-decisions.jsonl:362`). Gravity and search time matter.

   **Measurement caveat:** `aim-search.live/found` count usable weapons assigned to the commit, but the facing-angle averages cover **all usable weapons with assigned targets**, including reroutes (`AI/AimWatch.cs:151–165,220–254`). The existing aggregate cannot establish a per-gun causal relationship.

4. **FACT — Placement failures have different causes, and “full planet” is unproved.** Native validation checks terrain mask, existing-building collision and fallout; the AI separately checks queued-building overlap (`decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:804–850`; `decompiled/Interplanetary.AI/AIConstruction.cs:354–382`). Solar/Nuclear plants, Railguns and Ion defenses are land-only; Floating Plants are water-only (`docs/ruleset/embedded/enhanced/BuildingsData.csv:13–18,36–37`). Power connections also require available slots at both ends and the native distance limit (`decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs:63–72`).

   Ash’s anti-nuke searches ended **10 placed, 25 gave up, 1 no-deficit**, in 36 terminal `cover-search` records. Across the 25 give-ups, **25,316/45,000** candidates passed the earlier placement and city-deficit checks but had **no eligible anchor**; **18,412/45,000** failed the combined native placement check (**R1**). The latter does not distinguish terrain, collision, fallout or raycast failure. A finite random search is not an exhaustive proof of no legal connected spot.

   Power and ordinary weapons use shelter sampling followed by native fallback; defenses have a separate bounded search and refund path (`AI/DirectorPatches.cs:2437–2479,2539–2635,2794–2809,2929–3025`). A queued placement can also be stopped at end turn without reaching the abandon counter (`decompiled/Interplanetary.AI/AIConstructionQueue.cs:139–143`). Therefore, **abandoned / queued is not a complete success rate**.

5. **FACT — The cap and material constraints bind separately.** The default `MaxWeapons` is **30**, counting ordinary trajectory weapons including Railguns, Missiles and Coilcannons—not just Railguns (`AI/Plugin.cs:482–483`; `AI/DirectorPatches.cs:1117–1125,1288–1318`). Ash had **15 Railguns + 8 Missiles + 7 Coilcannons = 30** at T35, not a 15-gun arsenal (`v019m26-ai-decisions.jsonl:1107,1116`). At T35 its director ledger had **36,800 energy, 13,423 material and 139 free slots**, but also a **15,000 material reserve**; T36 it had 33,395 energy and exactly 15,000 material (**R4**). Hawking had 30 aimed weapons and **15/53 material left** at T22/T23 despite 39,586/38,344 energy left (`v019m26-ai-decisions.jsonl:394,468`).

   **JUDGMENT —** Raising the cap alone would not spend material that is unavailable or reserved, and more guns can reduce shared search work per weapon: Hawking received **51, 46 and 45 steps** with 51, 57 and 58 total AI weapons on T22–24 (`v019m26-ai-decisions.jsonl:379,452,510`). Energy resets each turn; material persists (`decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:152–165`).

6. **FACT — The match 25 focus claim is overstated.** The notes say all three committed to the human every turn (`2026-11-08-v0.19.0-match25.md:27`), but the decision log shows **49/51** human commits over T21–37: Zenon and Sarai each selected Beastie on T23 (**R5**). On that turn, their **42 paid trajectory records** had **9 Beastie, 3 jogi and 30 other Eiai** destinations (`v019m25-ai-decisions.jsonl:659,672,685,690,699–719,742–762`; **R5**). The firing hook can fill hit quotas from secondary trajectories (`AI/Patches.cs:449–475,669–695`).

   Across the match, **650/995 paid trajectory records** named jogi; the remaining records named another AI, `none`, or `own` (**R5**). These are completed targeting/payment records, **not confirmed action-phase impacts**. The logged `own` label also includes non-player celestial bodies, so it cannot establish self-hits (`AI/Patches.cs:594–607`). The note’s 774 planned hits must not be interpreted as 774 confirmed hits on jogi.

7. **FACT — Several failure totals in the notes do not reconcile with these local raw logs.** Summing smart-AI director records gives match 23 **64**, versus 59 stated; match 25 **107**, versus 127 stated; match 26 **229**—Ash 203, Hawking 26—versus 230 and Ash 207 stated (**R7**). The raw counts are counts of recorded abandonments, not independent attempts or proof of lost builds. Reconcile before using the notes as baselines.

## 4. Risks, ranked

1. **Misidentifying geometry as the dominant bottleneck.** Angle, target changes, gravity, search time, terrain and defense compete; the current aggregates mix weapon populations.
2. **Creating better-facing but exposed or unpowered guns.** A hard hemisphere filter could increase failures or sacrifice the shelter and native power rules.
3. **Changing fire distribution accidentally.** Suppressing secondary trajectories may save energy but also remove useful fire against legitimate free-for-all opponents; it must not inflate accuracy.
4. **Raising the cap into worse throughput.** More guns increase firing reservations and share a finite trajectory budget; material and repairs can bind first.
5. **Overfitting one win.** Match 25 had three Eiais and an experimental human offense; match 26 had two and standard offense. Neither isolates the changes.
6. **Incomplete denominators and lifecycle accounting.** No-deficit refunds are intentional; timeout-cancelled searches and planned versus paid versus actual shots need separate accounting.

## 5. Recommendations, ranked

### 1. v0.20 — Make retaliation sticky and instrument the actual bottlenecks

- **Evidence:** Observation 1; placement and destination denominator gaps in 4 and 6.
- **Mechanism:** Add a match-scoped retaliation latch shared by strategy and director. Keep the *scheduled* war turn separate: before it, retain `RetaliationUpkeepShare`, rather than accidentally granting full war upkeep. Reset on new match and earlier-save load. Log trigger, scheduled turn and latch. Add placement start/terminal records—placed, abandoned, no-deficit and end-turn cancelled—with type, reason counters and search ID. Record per-weapon target, placement cohort and trajectory destination; preserve `unknown` where the engine cannot distinguish a cause.
- **Files:** `AI/DirectorPatches.cs`, `AI/StrategyPatches.cs`, `AI/Brain/Tempo.cs` or `Director.cs`, placement/fire logging, `AI/Tests/Program.cs`.
- **Exit gate:** For every completed AI turn, started searches reconcile to terminal outcomes plus explicitly pending/cancelled searches; **zero peace decisions after a triggered latch** within the same match, including declining damage. Replay Eddie/Ash shapes and demonstrate the pre-fix test fails. No added weapon purchases above the existing pre-schedule upkeep limit.
- **Fairness:** Own damage, own placements and already-computed trajectories only; smart-AI guards remain. No resources granted.
- **Effort:** **M**.

### 2. v0.20 — Bias new weapon placement toward the likely target

- **Evidence:** Observation 3; Zenon/Sarai found only **3/30** and **6/29** primary trajectories after switching to Beastie (`v019m25-ai-decisions.jsonl:685–691`).
- **Mechanism:** Among **already legal, connected** shelter candidates, use projected facing to the current committed enemy as a bounded score or tie-break. Preserve cover preference and permit an unpreferred candidate after a bounded search. When there are no weapons, `Strategy.Decide` returns no commit (`AI/Brain/Strategy.cs:368–374`): use the existing fair target score as an explicit fallback, not hidden enemy building positions. Re-evaluate direction at placement time; never rotate existing guns or assume a >90° angle means unreachable.
- **Files:** `AI/DirectorPatches.cs` shelter search; a small pure scoring rule in `AI/Brain/Coverage.cs`; placement and per-gun aim logging.
- **Exit gate:** On matched saves/maps, at least **30 comparable firing AI-turns per arm**, new-weapon commit-primary trajectories / assigned operational weapons improve by **≥10 percentage points**, with placement abandonment rate and covered-building share each no worse than **5 percentage points**. Report paid destinations and p95 `aimWaitAfterHuman` separately. Falsified if angle improves but trajectory yield does not, or if placement/coverage materially worsens.
- **Fairness:** Only public planet positions and the AI’s existing fair commit/score; no hidden building coordinates. Keep configured level and guided accuracy quotas unchanged; original AI untouched.
- **Effort:** **M**.

### 3. v0.21 — Fix connection-constrained placement before declaring the planet full

- **Evidence:** Observation 4, especially Ash’s **25,316 no-anchor candidates**; land-plant failures and the separate water fallback.
- **Mechanism:** Use the new rejection data to distinguish cases. For city-cover searches, preferentially sample the **intersection of deficient-city coverage and eligible power-anchor reach**, retaining every native validation. If no connected legal candidate can be found, consider a normally paid, legal relay plant that makes a later defense connection possible; do not claim success until the relay is built and powered. For land plants, retain bounded attempts and permit the existing Floating Plant fallback when appropriate. Do not use the native unconnected defense fallback.
- **Files:** `AI/DirectorPatches.cs` `AntiNukeStep`, `CoverageStep`, plant selection and refund/reopen paths; geometry helpers in `AI/Brain/Coverage.cs`.
- **Exit gate:** In matched crowded and sparse saves, **≥20% fewer abandoned anti-nuke/plant searches per started search**, with no rise in timeout-cancelled searches; **100%** of placed defenses satisfy native connection and city-deficit checks. City coverage must not fall; count no-deficit cancellations separately. Falsified if an exhaustive instrumented legal search finds no feasible spot or improvement merely shifts failures to timeouts.
- **Fairness:** Own terrain, buildings and grid only; pay native plant/defense costs and preserve exact refunds.
- **Effort:** **M–L**, depending on whether relays are actually required.

### 4. v0.21 — Test commit-respecting secondary fire

- **Evidence:** Observation 6: a commit does not constrain all paid secondary trajectories.
- **Mechanism:** First run a controlled variant that excludes unintended secondary hit trajectories to a non-assigned enemy from the final hitting list, while retaining **explicitly rerouted primary assignments**. Apply exclusion before quota refill as well as after native list construction; never replace an excluded hit with an extra hit. Keep existing abort and payment accounting. This is an experiment: in free-for-all, another enemy may legitimately be worth attacking.
- **Files:** `AI/Patches.cs` firing-list and class-quota logic; `AI/StrategyPatches.cs` reporting; `AI/Tests/Program.cs`.
- **Exit gate:** On matched switch-target turns, fewer non-assigned paid destinations, with **no decrease in paid trajectories to the chosen target** and no increase in configured per-class hit quotas. Track total paid fire, defense reserve and later population/building outcomes; reject the variant if it merely withholds useful damage. Include a red-before-fix regression for the T23 secondary-list shape.
- **Fairness:** No opponent is exempt; decisions use existing assignment and completed trajectories, not hidden values. Preserve level-specific and guided accuracy.
- **Effort:** **M**.

### 5. v0.22 — Experiment with cap and war timing independently

- **Evidence:** Observations 2 and 5.
- **Mechanism:** Test a bounded **30 → 35** weapon-cap increase only when material remains after reserves, native upkeep fits, and the existing arsenal has demonstrated high primary-trajectory yield. Separately test an earlier war gate based on an actually available, affordable weapon and sustainable upkeep, rather than treating Orbital Assembly as a Railgun prerequisite. Keep the existing opening economy/defense protections. Do not combine these experiments initially.
- **Files:** `AI/DirectorPatches.cs`, `AI/Brain/Tempo.cs`, `AI/Plugin.cs`; research order only if the separate timing experiment supports it.
- **Exit gate:** Against matched baseline saves, the cap variant must increase **paid trajectories to the committed target per firing turn by ≥10%** without worsening p95 aim wait by >10 seconds, defense coverage by >5 percentage points, or causing unfunded upkeep. The timing variant must advance the first paid attack by at least one turn in **≥70% of matched runs**, without reducing T25 energy production or survival by >10%. Retain the old policy if either gate fails.
- **Fairness:** Native affordability, availability, firing costs, reserves and existing accuracy settings; no enemy hidden values or original-AI patches.
- **Effort:** **M**, plus paired playtests.

## 6. Strongest reason I may be wrong

The biggest apparent placement and focus problems may be **strategically rational under changing gravity, targets and defense coverage**. The available logs do not include a controlled replay of the same planet with alternative placements or prove action-phase damage from the paid trajectories. In particular, suppressing secondary fire could reduce total damage, and target-facing placement could point the wrong way by the time a gun fires.

## 7. Reproduction and unknowns

These are read-only commands run from the repository root:

**R1 — Ash anti-nuke search outcomes**

```bash
python -c "import json,collections; a=[x for x in map(json.loads,open('docs/playtests/data/v019m26-ai-decisions.jsonl',encoding='utf8')) if x.get('e')=='cover-search' and x.get('ai')=='Ash']; b=[x for x in a if x['outcome']=='gave-up']; print(len(a),dict(collections.Counter(x['outcome'] for x in a)),sum(x['tries'] for x in b),sum(x['unplaced'] for x in b),sum(x['noAnchor'] for x in b))"
```

Output: `36 {'placed': 10, 'gave-up': 25, 'no-deficit': 1} 45000 18412 25316`.

**R2 — Ash damage across the war flip**

```bash
python -c "import json; a=list(map(json.loads,open('docs/playtests/data/v019m26-ai-decisions.jsonl',encoding='utf8'))); print([(x['t'],x['recentDamage']) for x in a if x.get('e')=='economy' and x.get('ai')=='Ash' and x.get('t') in (17,18,19,20)])"
```

Output: `[(17, 865.274), (18, 605.691), (19, 528.984), (20, 641.189)]`.

**R3 — Match 25 T19 income**

```bash
python -c "import json; a=list(map(json.loads,open('docs/playtests/data/v019m25-ai-decisions.jsonl',encoding='utf8'))); print([(x['ai'],x['energyPerTurn']) for x in a if x.get('e')=='economy' and x.get('t')==19 and x.get('ai') in ('Zenon','Donnels','Sarai')])"
```

Output: `[('Zenon', 6444), ('Donnels', 11694), ('Sarai', 7344)]`.

**R4 — Ash cap, resources and slots**

```bash
python -c "import json; a=list(map(json.loads,open('docs/playtests/data/v019m26-ai-decisions.jsonl',encoding='utf8'))); print([(x['t'],x['energyLeft'],x['materialLeft'],x['aimedWeapons'],x['slotsLeft']) for x in a if x.get('e')=='director' and x.get('ai')=='Ash' and x.get('t') in (33,35,36,37)])"
```

Output: `[(33, 197, 16714, 30, 118), (35, 36800, 13423, 30, 139), (36, 33395, 15000, 30, 136), (37, 32059, 20014, 30, 135)]`.

**R5 — Match 25 commits and paid destinations**

```bash
python -c "import json,collections; a=[x for x in map(json.loads,open('docs/playtests/data/v019m25-ai-decisions.jsonl',encoding='utf8')) if x.get('e')=='strategy' and x.get('ai') in ('Zenon','Donnels','Sarai') and 21<=x.get('t',0)<=37]; print([(n,len([x for x in a if x['ai']==n]),dict(collections.Counter(x['commit'] for x in a if x['ai']==n))) for n in ('Zenon','Donnels','Sarai')])"
python -c "import json,collections; a=[x for x in map(json.loads,open('docs/playtests/data/v019m25-ai-decisions.jsonl',encoding='utf8')) if x.get('e')=='fire-paid' and x.get('t')==23 and x.get('ai') in ('Zenon','Sarai')]; print(len(a),dict(collections.Counter(x['destination'] for x in a)))"
python -c "import json,collections; a=[x for x in map(json.loads,open('docs/playtests/data/v019m25-ai-decisions.jsonl',encoding='utf8')) if x.get('e')=='fire-paid' and x.get('ai') in ('Zenon','Donnels','Sarai')]; print(len(a),dict(collections.Counter(x['destination'] for x in a)))"
```

Outputs: commits **16/17, 17/17, 16/17** to jogi; T23 destinations `42 {'Beastie': 9, 'Donnels': 15, 'jogi': 3, 'Sarai': 7, 'Zenon': 8}`; overall **650 jogi / 995** paid records.

**R6 — Railgun and Orbital Assembly unlocks**

```bash
python -c "import csv; p='docs/ruleset/embedded/enhanced/TechTreeData.csv'; d={row[0]:row for row in csv.reader(open(p,encoding='utf8',newline=''))}; ids=d['id']; print('building_unlock containing Railgun:',[(ids[i],v) for i,v in enumerate(d['building_unlock']) if 'bui_Railgun' in v]); print('OrbitalAssembly unlock:',d['building_unlock'][ids.index('tec_OrbitalAssembly')]); print('IonDefense unlock:',[(ids[i],v) for i,v in enumerate(d['building_unlock']) if 'bui_IonDefense' in v])"
```

Output: no Railgun unlock; Orbital Assembly unlocks `bui_Missile`; Plasma Channel Induction unlocks `bui_IonDefense`.

**R7 — Match 26 recorded placement failures**

```bash
python -c "import json,collections; a=[x for x in map(json.loads,open('docs/playtests/data/v019m26-ai-decisions.jsonl',encoding='utf8')) if x.get('e')=='director']; print([(n,sum(sum(x['placementFailures'].values()) for x in a if x['ai']==n),len([x for x in a if x['ai']==n])) for n in ('Ash','Hawking')])"
```

Output: `[('Ash', 203, 43), ('Hawking', 26, 35)]`.

**Unknowns:** Actual legal-space exhaustion by type; action-phase impacts and damage for each paid trajectory; whether match-note count discrepancies reflect different source snapshots; causal performance under matched planet geometry. No tests, build, installation or live-game verification were run.

END OF REPORT
