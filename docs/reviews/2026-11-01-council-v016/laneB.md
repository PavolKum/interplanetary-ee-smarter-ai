The raw log changes two important parts of the diagnosis: city visibility was intermittent after T12, and the 28 `cover-search` records represent only three search attempts because give-up messages repeat. I’m checking the research unlock and construction gates before treating earlier Ion placement as an executable fix.
A larger bottleneck is now visible in the code and logs: by T14, five permanent material projects plus two counterintel projects had filled all seven unlocked city-project slots. SSTAR was researched, but had nowhere to go; its first recorded purchase was T31. That makes city-slot allocation a higher-priority fix than simply raising the weapon cap.
# Lane B — v0.16 AI code and runtime report

## 1. Scope and frozen target

Reviewed **`24683684d9799b9b22db765c251ac811ef8793ae`**, confirmed as HEAD before and after inspection.

I read the charter; the scoreboard and four specified playtest notes; the specified prior reviews and dispositions; relevant implementations in `AI/`; the match-16 decision log and human telemetry; comparison records from v0.14.1 and v0.15.1; Enhanced ruleset fields; and the native contracts cited below in `decompiled/`.

All six match-data files matched the SHA-256 values in `data-manifest.txt`. Git porcelain status was unchanged between the opening and closing checks. No files were created, edited or deleted; no builds, tests or mutation scripts ran. Computations used read-only Python and the permitted human-summary script.

**Unavailable evidence:** installed configuration, actual planet-placement geometry, and execution of the proposed changes. Next-match effects are **unverified on the live game**. I did not read another council lane’s report.

Citation shorthand below:

- **D16:** `docs/playtests/data/v0153-ai-decisions.jsonl`
- **H16:** `docs/playtests/data/v0153-human-telemetry.jsonl`
- Native filenames are under `decompiled/`, with their namespace directory specified where needed.

## 2. Verdict

The AI loses because its investment and combat policies are disconnected: it locks scarce city-project slots into permanent purchases, spends heavily chasing temporary visibility, and then pays for a predominantly precision arsenal after its precision targets have disappeared. **The single change most likely to flip the economic race is a city-project-slot policy that preserves and schedules stacked SSTAR cycles before permanent counterintel/science purchases consume the slots, with the resulting energy converted into visibility-independent artillery before the human’s mass-volley window; confidence: Medium, uncalibrated.**

## 3. Evidence and computations

### E1. The missing SSTAR engine is a slot-allocation problem, not merely a low score

The director queued:

- Five permanent material projects by T7.
- Two permanent counterintel projects by T11.
- One permanent Science Institute at T17.
- **No SSTAR before T26; the first recorded SSTAR purchase is T31.**

Sources: D16:19,26,30,34,38,53,80,250.

At T14, the five AI city populations were:

`344, 401, 336, 544, 694` — D16:68.

Every city has 1,000 maximum population, with project slots at 250/500/750 population:

- Small cities: `1 + 1 + 1` slots.
- Medium city: `2`.
- Big city: `2`.
- **Total: seven slots.**

Sources: `CitiesData.csv:17,25–28`; native `Interplanetary.Prefabs/BuildingProjects.cs:423–447`.

The **seven queued permanent projects account for every unlocked slot**. Installed project identities are not logged, so complete slot occupancy is a strong reconstruction rather than a directly captured snapshot. The structural problem is certain: `TryCityProject` purchases counterintel before ordinary economy, while project eligibility requires a free slot (`AI/DirectorPatches.cs:624–633,1340–1357`).

These are effectively irreversible opening choices. Native cancellation permits only projects installed **this turn**, not arbitrary replacement of an old permanent project (`BuildingProjects.cs:122–158,190–208`).

Meanwhile:

- Lossless Energy Transfer was the research head at T12–T13; the next batch began T14 — D16:56,60,63.
- SSTAR costs **150 E / 300 M**, lasts six turns, and contributes **×1.3 energy / ×0.9 material** per city — `ProjectsData.csv:6,9–10,15,18`.
- Global modifiers multiply: `Interplanetary.PlayerDataManagement/GlobalStatModifier.cs:53–59`.

Holding the historical underlying economy constant:

- Three SSTAR copies: `12,816 × 1.3³ = 28,156.8 E/turn`.
- Human T25 production: **29,410** — H16:165.
- Thus three copies almost close the historical T25 production gap, for **450 E / 900 M** and three city slots.
- The associated material multiplier is `0.9³ = 0.729`, welcome when material is already accumulating unused.

This is **leverage arithmetic, not a causal replay**: preserving those slots changes earlier production and population.

There is also a forecast defect worth correcting alongside the policy. `AddProjectOptions` discounts a six-turn project’s gain to 60% for scoring, then `TryEconomy` adds that discounted gain into projected income; simultaneous global projects are priced from unchanged live production rather than their compounded queued state (`DirectorPatches.cs:1056–1060,1266–1288`). Duration-adjusted utility and projected next-turn production should be separate quantities.

### E2. The Telescope race bought transient visibility at substantial recurring cost

D16 records six arrays by T8, then thirty by T26.

At base ruleset costs:

- Thirty arrays: `30 × 200 = 6,000 E` construction.
- Material: `30 × 300 = 9,000 M`.
- Recurring energy: `30 × 20 = 600 E/turn`.
- Twenty-four arrays beyond the initial six: **4,800 E / 7,200 M**, plus **480 E/turn** upkeep.

Sources: D16:38,169; `BuildingsData.csv:13–14,22,25`. Summing base upkeep against recorded inventories through T25 gives another **4,940 E** nominally consumed.

**Correction to the supplied paragraph:** all five cities were not continuously visible T8–T22.

`director.unseenCities` was:

| Turn | Own intel | Invisible cities |
|---|---:|---:|
| 8 | 48 | 0 |
| 13 | 48 | 4 |
| 15 | 80 | 0 |
| 16 | 80 | 4 |
| 19 | 128 | 4 |
| 21 | 160 | 3 |
| 22 | 176 | 3 |
| 23 | 192 | 5 |

Sources: D16:38,61,72,76,91,103,111,119.

The current objective is effectively “continue until nothing is missing,” with a count cap rather than a return-on-investment cap (`DirectorPatches.cs:574–588`). Native BelowMinimum can bypass the opening energy guard. The guard is also only a balance check; it does not reserve an economy purchase against later priorities.

The native visibility contract supports stopping this race: visibility is evaluated from intel versus stealth, and shadows are created only under the relevant setting (`PlayerIntelHandler.cs:176–208`). A policy can measure **its own spending and current visible results** without learning the hidden multiplier or target threshold.

### E3. Strategy missed the final useful precision window

At T21:

- Nine Missiles.
- Estimated arrivals/passing: `9 × 0.85 = 7.65`.
- Pass share: **100%**.
- Strategy held because passing shots were below twelve.
- Two cities were still currently visible.

At T22:

- Eleven Missiles plus two Railguns.
- Passing estimate: **10.007**, pass share **93.1%**.
- Again held below twelve.
- Two cities remained visible.

Sources: D16:100–111.

The unobstructed nominal city damage represented by the Missile portions was:

- T21: `9 × 0.85 × 150 = 1,147.5 population`.
- T22: `11 × 0.85 × 150 = 1,402.5 population`.

That is enough to make a focused attack on a currently visible city a serious opportunity. It is **not** proof of two kills: allocation, current visible durability, realized trajectories, accuracy and growth still matter.

The first strike occurred at T24, after all cities were hidden. Across T24–T28, all **45** recorded Missile assignments had zero candidates and used native fallback:

`13 + 13 + 14 + 4 + 1 = 45`.

Sources: D16:129–141,150–162,171–184,193–196,208.

Native fallback is literally a random surface point (`Interplanetary.AI/AIWeaponTargeting.cs:483–496`). The engine substitutes the chosen second-phase point for the impact location (`WeaponGameplayData.cs:145–148`). Therefore **“gets through defenses” is not equivalent to “does useful damage”** when the guided target list is empty.

Holding also had a genuine benefit: T21–T23 released `2,700 + 3,500 + 3,800 = 10,000 E` for construction. Any earlier-fire proposal must account for that lost investment, rather than treating those shots as free.

### E4. Weapon selection has no representation of targetability

`TryWeapon` scores:

- Base damage.
- Fragment/projectile count.
- A linear AoE factor.
- A lock-on bonus.
- Estimated defense passage.

It does **not** price whether there is a currently legal second-phase target (`DirectorPatches.cs:965–1007`).

Consequently, the absence of Ion defenses made Missiles attractive even when there was nothing useful to aim them at. Observed Missile passage of 1.0 then reinforced the preference — D16:148,169.

For a blind artillery package, the existing cap need not be increased:

- Thirty Railguns with Triple Barrel + Optimized Airburst produce **90 projectiles**.
- Firing cost: `30 × 280 = 8,400 E`.
- Material including construction/upgrades: `30 × (200 + 350 + 250) = 24,000 M`.
- Construction/upgrade energy: `30 × (200 + 50 + 50) = 9,000 E`.
- Nominal damage per projectile: **100 building / 100 population**, radius **9.6**.

Sources: `BuildingsData.csv:13–14`; `WeaponsData.csv:7,9–11`; `UpgradesData.csv:8–9,33,35,37,39,44`.

At level-one accuracy, the planning expectation is `90 × 0.7 = 63` arrivals **before interception and trajectory limitations**. That is a materially different offensive instrument from nineteen nominal arrivals dominated by random-point Missiles.

The native rules support ordinary blind bombardment without coordinates: unguided impacts are scattered by the engine (`WeaponGameplayData.cs:150–160`). This is fair area fire, not remembered-city targeting.

### E5. The upkeep cap restricts an arsenal as though every weapon must fire every turn

Native `AIResources.BeginTurn` subtracts every Normal weapon’s firing costs before construction (`AIResources.cs:38–62`). The mod returns that reservation only for an all-or-nothing hold (`StrategyPatches.cs:280–289`).

Construction separately limits prospective firing upkeep to 40% of forecast income (`DirectorPatches.cs:644–677,985–986`). For scale:

- T20 turn-start income: 7,265.
- `0.4 × 7,265 = 2,906`.
- Nine base Missiles already represent **2,700 E**.

Thus the AI cannot accumulate the same reserve arsenal as a human who builds weapons without committing to fire all of them next turn. The cap interacts with purchasing order, remaining energy and forecast growth; it is not the sole restriction on every turn.

Material was available: **40,043 at T25; 46,426 at T27; 47,256 at T28** — D16:146,189,204.

Actual firing is charged per successfully completed first-phase targeting, with native affordability checks (`PlayerWeaponHandler.cs:230–260`). There is no game rule requiring all owned weapons to fire together.

Two accounting issues must be fixed before exploiting this:

- New weapons are admitted at **base** firing cost, while upgrade deltas include global modifiers (`DirectorPatches.cs:985,1568`).
- Repair budgeting also protects base rather than live firing costs (`DirectorPatches.cs:878–883`).

The authoritative live cost includes global modifiers (`BuildingWeaponStats.cs:12–18`).

### E6. City-cover failure was partly an unlock/priority failure, and the log overstates search count

**Correction:** twenty-eight `cover-search` records do not mean twenty-eight searches.

The purchase and search records show:

- T30: one Ion queued and placed; 792 candidates, 754 native placement rejections.
- T31: one Ion queued and abandoned; the same 1,800 rejected candidates reported fifteen times.
- T32: one Ion queued and abandoned; the same 1,800 rejected candidates reported twelve times.

Sources: D16:225–250,257–270.

That is **three purchases/searches: one placed, two failed**, not one success out of twenty-eight independent attempts.

More importantly:

- The prior requested five Ions at T24, but queued none — D16:127.
- Plasma Channel Induction, which unlocks Ion Defense, did not become the research head until T28 — D16:197,204.
- Its prerequisite is Maglev Servos, and its own cost is only **400 science** — `TechTreeData.csv:4,8–9`.
- The purchase branch correctly checks native availability (`DirectorPatches.cs:599–601`).

Changing `AntiNukePriorTurn` alone cannot create an unavailable building. By T29, reconnection purchases could also consume the energy before the city-cover branch — D16:215.

The eventual failures are real, but `unplaced` does not distinguish terrain, footprint obstruction, fallout and other native rejections. “The ring was built over” is plausible, not conclusively diagnosed by this field alone.

### E7. The decisive defensive failure included funding, not just coverage

The large T26 human volley produced:

- **83 impacts**.
- **23 intercepted:** `23 / 83 = 27.7%`.
- **27 with no eligible defense:** `27 / 83 = 32.5%`.
- **40 unpaid-roll events**.
- **3,400 E** spent by defenses.

Source: D16:191, reporting the preceding phase.

Therefore, “42 Kinetics existed” and `coverageKinetic ≈ 0.8` did not establish operational interception readiness. Native interception requires a Normal defense in range, power, and sufficient running energy/material; every attempted roll costs resources (`PlayerDefenseHandler.cs:215–247,264–287`).

The material-recovery trap documented in v0.15.1 is genuine—its T37 director had 115 M and 3,043 E left—but it is **not match 16’s main material problem**. Labo still had over 45,000 M during its collapse. Here the urgent task is converting material into useful power and military capacity before destruction, not rebuilding extraction afterward.

## 4. Ranked proposals — maximum six

### B1 — Preserve city-project slots for an actual energy cycle

**Mechanism / code**

Add a per-city project-slot policy to `DirectorPatches.CityForProject`, `TryCityProject` and `AddProjectOptions`.

- Preserve flexible slots on the medium/big cities and subsequently unlocked small-city slots.
- Give stacked SSTAR a scheduled growth/arming window.
- Stop automatically filling every newly opened slot with permanent counterintel or Science Institute.
- Keep current-turn spending, projected next-turn production, and duration-adjusted investment utility separate.
- Compound queued global modifiers correctly.

This must begin in the opening; old permanent projects cannot simply be removed later.

**Fairness:** own cities, population, projects, technology and resources only; native costs, slots and durations remain authoritative.

**Expected effect:** three active copies multiply underlying energy by **2.197**. The fixed-base T25 illustration is **12.8k → 28.2k**, nearly matching the human’s 29.4k.

**Risk / counter-play:** less early counterintel exposes the AI; population loss removes project slots; expiry creates a production cliff. Do not budget tomorrow’s firing from an expired multiplier.

**Next-match gate:** log actual installed projects and free/reserved slots by city. Require **three simultaneous SSTAR copies by T20**, and **≥20,000 E/turn at T22**. These are experimental thresholds, not promised outcomes.

**Tests / failure modes:** a cheaper permanent purchase must not consume the last reserved slot; three ×1.3 projects must forecast ×2.197; pending projects must consume capacity; expiry and population-slot loss must reduce the forecast. Mutation: remove the slot guard and require the reserved-slot case to fail.

---

### B2 — Buy weapons for the targets currently available

**Mechanism / code**

Use one visibility-filtered candidate description in `Patches.cs`, `StrategyPatches.cs` and `DirectorPatches.TryWeapon`.

- With useful visible cities: retain precision weapons.
- With zero legal precision candidates: sharply reduce new Missile/Nuclear purchases and favor upgraded unguided bombardment.
- Advance **Ultracapacitors and Composite Heat Cells** into the pre-volley research window.
- Choose a deliberate two-slot artillery package instead of filling those slots opportunistically with cheap upgrades.
- Under sustained blindness, prefer the **Mass Drivers/Asteroid** branch over a blind Nuclear objective, after the artillery core is funded.

Do not require the Asteroid branch to complete before the first artillery strike; its research cost is substantial.

**Native contract:** second-phase classifications are explicit in `WeaponTargetingModule.cs:32–38`; unguided scatter and second-phase impact substitution are in `WeaponGameplayData.cs:145–160`; buildings have only two upgrade slots (`BuildingUpgrades.cs:11–31`).

**Fairness:** current visible candidates and observed combat outcomes only. No hidden building count, position, population or defense value enters the choice. Asteroids and Railguns use native bombardment.

**Expected effect:** **24 Triple/Airburst Railguns** represent 72 projectiles for **6,720 E** per volley, with **19,200 M** invested. Thirty reach 90 projectiles without increasing the current weapon cap.

**Risk / counter-play:** Kinetic walls remain effective; the human can change to Ion against guided alternatives or city barriers against Asteroids. Artillery’s realized damage is geometry-dependent.

**Next-match gate:** by **T24**, require **≥60 live projectiles per planned artillery salvo**, and **≤10% of firing energy spent on zero-candidate precision weapons** after blindness is observed.

**Tests / failure modes:** blind Missiles must lose their precision premium despite pass probability 1.0; visible-city availability must restore it; unknown defenses must remain unknown; planned upgrades must respect both slots. Mutation: restore the unconditional lock-on premium and require the blind-choice case to fail.

---

### B3 — Give intel an expenditure budget and a stop-loss

**Mechanism / code**

Replace the unconditional unseen-city objective in `DirectorPatches.Step` and `EconomyDirector.IntelPerTurnLimit` with bounded bids for current visibility.

- Protect a selected core energy investment across the entire opening priority sequence.
- Start with a bounded acquisition package.
- Permit an additional batch only after measuring its actual visibility result.
- Suspend escalation after unsuccessful batches; allow occasional small probes rather than restarting the race indefinitely.
- Apply the same stop-loss to the native urgent-intel research escape at `EconomyPatches.cs:507–508`, so a construction cap does not merely redirect the waste into research.

Price intel upgrades too, including upkeep. The Telescope upgrade gives +4 intel for 50 E / 175 M but increases upkeep by 12 E; a new array gives +8 for 200 E / 300 M and 20 upkeep. Neither is automatically superior.

**Native contract:** `AIIntel.cs:120–157` and `PlayerIntelHandler.cs:176–208` expose current visibility; native urgent-intel research is driven by `AIShortTermStrategy.cs:40–75`. No hidden threshold needs to be read.

**Fairness:** retain only expenditure/outcome counters, not hidden city coordinates or durability. Replace decisions based on enumerating the true hidden surviving-city count with decisions based on the current permitted observation.

**Expected effect:** stopping around six to eight arrays instead of thirty avoids approximately **4.4–4.8k E construction**, **6.6–7.2k M**, and **440–480 E/turn** of later upkeep.

**Risk / counter-play:** a stopped bid can miss an affordable visibility window. This is why bounded probes remain useful.

**Next-match gate:** after two unsuccessful marginal batches, log `intel-policy=stop-loss`; require **no more than two further arrays through T25**, and total intel construction/upgrades **≤10% of cumulative available planning energy through T20**.

**Tests / failure modes:** repeated blindness must not reset the spending counter; native BelowMinimum must not bypass it indefinitely; a successful probe can reopen a bounded bid. Mutation: reinstate the native-minimum bypass and require an over-budget case to fail.

---

### B4 — Separate arsenal ownership from the paid firing roster

**Mechanism / code**

Extend `StrategyPatches.State` from a single Fire/Hold Boolean to a **latched firing roster and reserved cost**, enforced in `Patches.GoalTargetWeaponsPrefix`.

- Release native reservations for unselected weapons before construction.
- Reserve the selected roster’s live firing costs, including misses.
- Permit arsenal construction beyond the blanket 40% upkeep test when a funded future volley is feasible.
- Keep the current 30-aimed-weapon cap initially.
- Add a precision opportunity rule: a useful currently visible city can justify firing below twelve passing shots when the allocated expected damage supports a credible kill.
- At firing time, remove failed trajectories but never add unfunded weapons after construction spent their reservation.
- Enforce separate 0.7/0.85 class quotas; the current aggregate quota and native insertion-order selection do not guarantee those class rates.

**Native contract:** `AIResources.cs:38–62`; `AIWeaponTargetingGoal.cs:63–103,125–205`; `PlayerWeaponHandler.cs:230–260`. Payment remains entirely native.

**Fairness:** roster selection uses own weapons and currently legal targets. Accuracy values remain unchanged.

**Expected effect:** restores a possible T21–T22 precision attack—the historical Missile components represented **1,147.5 and 1,402.5 nominal expected population damage**—and allows a 30-gun arsenal to pulse at **8,400 E** without requiring every weapon to be treated as permanently committed upkeep.

**Risk / counter-play:** this is the highest adapter-risk change. Double-releasing a reservation would create an effective resource cheat; releasing first and deciding to fire later would overspend. Earlier attacks also sacrifice growth.

**Next-match gate:** log roster IDs, reserved/paid firing costs and released reservation. Require **zero funding overruns**; **zero below-saturation holds when a funded, currently visible kill opportunity passes the rule**; and class hit quotas within rounding/carry tolerance of 0.7/0.85.

**Tests / failure modes:** reconcile native reservation = selected + released; include safe misses; reject stale/unpowered weapons; preserve the roster across the turn; test mixed old/new classes and save resets. Mutations must catch both double-release and a late unfunded roster addition.

---

### B5 — Upgrade funded Kinetic coverage before the mass-volley window

**Mechanism / code**

Add defense-upgrade options to the director and bring Early Warning AI forward enough to use them.

Prioritize Kinetic range on city/economy protection, then cost reduction where affordable. Use actual powered coverage and live activation costs in readiness logs; do not infer readiness from inventory alone.

**Native contract:** interception checks impact position and live radius/cost (`PlayerDefenseHandler.cs:231–287`). Upgrade combat effects finish at the next turn start (`BuildingUpgradeStats.cs:30–73`; `BaseBuilding.cs:1014–1019`).

**Fairness:** own geometry, own defenses, public rules and incoming impacts only.

**Expected effect:**

- Range upgrade: radius `5.9 × 1.3 = 7.67`; geometric cap area **×1.69**, probability **0.8 → 0.72**.
- Isolated-defense interception footprint therefore improves approximately `1.69 × 0.9 = 1.521`.
- Add cost-down: radius **6.903**, activation **70 E**.
- Energy per successful single roll: `70 / 0.72 = 97.2 E`, versus base `100 / 0.8 = 125 E`.

Overlap and damage distribution prevent treating these ratios as a guaranteed planet-wide improvement.

**Risk / counter-play:** expanded coverage creates more eligible rolls and therefore more demand for energy. Production from B1 must fund it; neither range nor a bigger reserve substitutes for the other.

**Next-match gate:** by T24, **≥20 powered Kinetics with range upgrades**; across the first ≥60-impact Railgun phase, require **≥50% interception and zero unpaid rolls**. Failure distinguishes whether the revised economy, coverage or reserve is still insufficient.

**Tests / failure modes:** use combined range/probability/cost modifiers, exclude disconnected defenses, and respect next-turn activation. Mutation: ignore cost-down’s radius penalty and require the geometry check to fail.

---

### B6 — Reserve legal city-cover sites and unlock Ions before requesting them

**Mechanism / code**

Make the city-cover policy executable rather than moving its prior date alone.

- Reserve a small number of **specific legal own-planet footprints and connector capacity** during early AI placement.
- Have ordinary AI placement avoid those intended sites; this is a planning preference, not a modification of game collision rules.
- Move Plasma Channel Induction immediately after Maglev Servos when the prior cover schedule requires it.
- Build a bounded one-Ion-per-city prior; escalate to two on an observed guided threat.
- Log city ID, search ID, exact terminal result and native placement failure reason once per search.

**Native contract:** native availability remains mandatory (`PlayerAvailableBuildings.cs:43–45`); footprint collisions include ruins (`PlayerBuildingHandler.cs:753–770`); connection length and free slots remain native (`PlayerPowerGridHandler.cs:63–73`). New Ions are not operational until the next turn (`BaseBuilding.cs:995–999`).

**Fairness:** exclusively own cities, land, buildings and power grid. No special placement or free construction.

**Expected effect:** one Ion per five cities costs **1,000 E / 1,250 M**. At a covered guided impact, one funded base Ion changes passage to **20%**, two to **4%**. This protects against the v0.14.1 Missile variant; it does **not** stop match 16’s Railguns.

**Risk / counter-play:** this is insurance, ranked last for the usual build-out. Over-reserving land or buying ten speculative Ions can damage the economic opening.

**Next-match gate:** Plasma unlocked by **T17**, `minCityCover ≥1` by **T20**, and exactly **one terminal log per search**. If an observed guided threat occurs, require minimum cover two before its next feasible large volley.

**Tests / failure modes:** blocked terrain must remain blocked; reserve only feasible sites; pending placements consume sites/slots; refunded searches release commitments; cover must count powered operational Ions. Mutation: allow ordinary construction over a reserved footprint and require the reservation case to fail.

## 5. What NOT to do

- **Do not remember Invisible cities**, read their live durability/positions, or use native frozen snapshots as an illicit substitute. With shadows off, counterintel must genuinely remove precision targeting.
- **Do not interpret `target.cities` as coordinate visibility.** It is gated aggregate population readability; current `director.unseenCities` and actual strike candidates answer different questions.
- **Do not raise accuracy.** Also fix the aggregate-quota ordering issue rather than accidentally giving old Missiles near-perfect selection.
- **Do not merely raise MaxWeapons to 60.** The existing trajectory budget falls from roughly `2600/30 ≈87` steps per weapon to `2600/60 ≈43`; more buildings can reproduce the old aiming collapse.
- **Do not globally fire every turn or lower twelve to four everywhere.** Match 16 released 10k E during three holds. Fire useful windows and funded rosters, not every high-pass random-point Missile.
- **Do not call all material stock waste.** A bank is useful for war and recovery. The error is continuing extraction and failing to convert a growing bank while energy and useful combat capacity bind.
- **Do not fix “28 failed Ion searches” with a larger sampling cap.** There were three searches; technology, purchase priority and site feasibility come first.
- **Do not prioritize the late extraction-recovery subsystem over the pre-T26 conversion problem.** It matters for the early-fire variant, but match 16 collapsed while materially rich.
- **Do not rely on MIRV as an assumed fair precision workaround without resolving its native targeting behavior.** Its homing path examines buildings in an impact region, and its ignore-intel field does not appear to gate that path (`WeaponGameplayData.cs:187–205,411–474`; `PlayerBuildingHandler.cs:302–319`). Railguns/Asteroids provide a cleaner initial implementation under this charter.

## 6. Strongest reason this analysis may be wrong

**The proposed benefits are not additive, and the engine’s realized bombardment may be much weaker than the nominal package.** Preserving SSTAR slots sacrifices permanent projects; spending earlier on artillery and defense changes growth; withholding intel changes observed opportunities; and the human can respond to early pressure.

Bolide already demonstrated that an economic lead alone does not win. If a correctly funded 60–90-projectile blind volley still produces little durable city/economy damage, this package may buy a longer loss rather than a win. That is the most important possible falsification.

## 7. Unknowns and the single measurement that resolves each

| Unknown | Single resolving measurement |
|---|---|
| Were permanent projects actually blocking SSTAR in match 16? | Per-city installed project IDs plus free/unlocked slots at T14, T17 and T20. |
| How much does an early blind artillery volley actually accomplish? | **Persistent enemy city-population/economy loss per 1,000 paid firing energy**, measured across the following repair turn; runtime decisions still receive permitted information only. |
| Does the visibility-window rule beat investing that energy? | A controlled same-save comparison of T21 fire versus hold, measuring surviving cities and production at T26. |
| Which native condition rejects city-ring candidates? | First terminal native placement-result reason, keyed by city and search ID. |
| Can thirty upgraded weapons retain the improved aiming rate? | Operational weapons with primary trajectories divided by selected operational weapons, on the first two large volleys. |
| Does the funded Kinetic upgrade package survive the first shock? | First large-volley ledger separating no eligible defense, no power, no resources and probabilistic failure. |
| Does native urgent-intel research keep hijacking the revised plan? | A complete research queue-change record with owner/reason, completed status and remaining science cost. |
| Is recovery still required after these changes? | Turn at which material stock becomes unable to fund one useful powered mine while planet material remains positive. |

**Implementation priority:** B1–B4 form the main attempt to beat the usual build-out; B5 funds survival through the exchange; B6 is bounded insurance against the demonstrated Missile counter-play.

END-OF-REPORT
