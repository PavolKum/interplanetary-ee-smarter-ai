The new accuracy count runs before newly added trajectory weapons receive their weapon references, so it can omit them from the quota. The volley ledger also survives match resets. I’m checking the city-defense placement and project-queue paths next; both depend on native behavior beyond the new Brain helpers.
The defense trigger is broadened correctly, but placement still optimizes weighted building value; it does not require a city-cover purchase to close a city’s deficit. The hard cap bounds spending without guaranteeing coverage. Shadow-city targeting is compatible with the native API, and the research append fix is sound; the early intel spend still needs an opening-economy check.
Verdict: fix first

**v0.15 addresses the right match-deciding problems, but the accuracy adapter has a new initialization-order bug, and the city-defense policy still does not guarantee city-covering placements.** The Quantum Encryption Hub path also inherits the ruined-city selection defect identified in the previous audit.

Reviewed HEAD `a2e508f` against `4b8f60b`, the supplied diff, both previous audits, synthesis, match records, and relevant decompiled implementations. **Confidence: High on code findings; Medium on next-match predictions.** No files were changed. Builds, tests, and mutations were not run under the read-only constraint. Installed configuration and live v0.15 gameplay remain unverified.

## High findings

### 1. The accuracy count runs before new trajectory weapons receive their weapon references

**Evidence**
- `AI/Patches.cs:262–280` counts through `tw.TargetableWeapon.Weapon`, skipping null references.
- `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:94–110` creates new targetable weapons, then calls the goal’s `BeginTurn`.
- Only afterward, at `AIWeaponTargeting.cs:112–122`, does it initialize each trajectory weapon.
- `decompiled/Interplanetary.AI.Trajectories/AITrajectoryWeapon.cs:132–139` assigns `TargetableWeapon` during that later initialization.
- `decompiled/Interplanetary.AI/AITargetableWeapon.cs:15–26` does not initialize that back-reference in its constructor.

**Failure scenario:** Twenty newly operational Missiles all have null trajectory back-references during the postfix. Both class counts become zero; `live` falls back to twenty, but `MixedRate(0, …, 0, levelAccuracy)` returns **0.7**. The first volley plans fourteen hits instead of seventeen.

Mixed old/new arsenals are worse: nineteen established Missiles plus four new ones count as nineteen live weapons, producing roughly sixteen hit slots instead of nineteen. The new weapons can then be excluded entirely by native first-in-list selection. Loading a save recreates the initialization problem.

**Concrete fix:** Count from the `List<AITargetableWeapon> weapons` argument passed to `AIWeaponTargetingGoal.BeginTurn`; those objects already have valid `Weapon` properties. Remove the zero-live fallback when a successfully inspected list genuinely contains no operational weapons. Set `Misses = live - Hits`.

An adapter-level check must reproduce native initialization order; the current `MixedRate` tests cannot catch this.

### 2. An “anti-nuke” purchase need not close any city’s coverage deficit

The new trigger and protecting count improve the policy, but **quantity is still disconnected from placement purpose**.

**Evidence**
- Demand and caps: `AI/DirectorPatches.cs:338–365`.
- Unthrottled purchases: `AI/DirectorPatches.cs:567–583`.
- Placement merely raises city value and enables stacking: `AI/DirectorPatches.cs:1662–1705`.
- Candidate selection still scores all building value: `AI/DirectorPatches.cs:1782–1827`.
- Any legal connected best result is accepted, without requiring city-deficit reduction: `AI/DirectorPatches.cs:1829–1834`.
- Native interception requires the **impact point**, not the city label, to be within radius: `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:264–287`.

**Failure scenario:** A deficient city has crowded surroundings or no suitable nearby powered connector. The search finds a legal position protecting a dense producer cluster instead. That placement succeeds, consumes the special purchase allowance, and contributes nothing to `ProtectingCount`. The next turn purchases more.

Even when every new Ion protects some city, the scoring function does not require it to protect a city **still below two**. It can continue stacking at a favorable cluster while another city remains exposed.

The dedicated branch is bounded by its hard cap, but:
- Five cities at two defenses each permit **forty intact Ions** before that guard stops it.
- Losing cities shrinks the cap. Twenty-nine surviving Ions with one remaining city immediately suppress special purchases, even if none protects that city.
- This is **not a universal Ion cap**: `TryDefense` and `TryCounterDefense` do not consult `AntiNukeCap` (`AI/DirectorPatches.cs:756–809`).

**T20–T24 resource implications**

| Item | Energy | Material | Power slots |
|---|---:|---:|---:|
| Ten base Ions: two for each of five cities | 2,000 | 2,500 | 10 |
| Forty base Ions: dedicated branch’s five-city hard cap | 8,000 | 10,000 | 40 |
| Historical T22 director starting allocation | 5,923 | 13,137 | 98 available |

Costs: `docs/ruleset/embedded/enhanced/BuildingsData.csv:13–16`. Historical allocation: `docs/playtests/data/v0141-ai-decisions.jsonl:135`.

The recorded T22 budget could comfortably fund a properly placed city-cover response. Geometry and purpose enforcement remain the concern. Ions constructed at T22 become operational at T23; buying them during T23 cannot protect that action phase (`decompiled/Interplanetary.Prefabs/BaseBuilding.cs:995–999`).

**Concrete fix:** Give each critical-cover purchase an explicit deficient city. Accept a placement only if its actual position adds qualifying coverage to an under-covered city. Track planned per-city coverage across simultaneous searches. If the city cannot be covered, report that specific failure rather than accepting an unrelated placement. Retain a bounded inventory guard, with bounded replacement of irrelevant Ions when it otherwise blocks the last exposed city.

## Medium findings

### 3. A weighted global accuracy quota does not deliver the promised per-class rates

**Evidence**
- `AI/Brain/Memory.cs:153–157` computes one weighted average.
- `AI/Patches.cs:275–284` converts that into one hit quota.
- Native selection takes primary trajectories in insertion order, then secondary trajectories: `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:129–168`.

**Failure scenario:** Ten older Railguns and ten newer Missiles all find primary trajectories. The weighted rate is 0.775, giving fifteen hits with zero initial carry. Native selection assigns all ten Railguns and only five Missiles: **100% unguided, 50% guided**, rather than 70% and 85%.

The same ordering can still starve a late-built Nuclear Missile behind conventional weapons. Raising the aggregate quota reduces that risk but does not remove it.

**Concrete fix:** Allocate hit slots by class, with separate fractional carries, then select within each class using a stable rotation or strike-value priority. Prioritize ready superweapons within their applicable quota. If one aggregate rate is intentional, describe it as aggregate accuracy rather than a per-class guarantee.

### 4. Strategy still evaluates guided salvos at the old level accuracy

**Evidence**
- New firing quota: `AI/Patches.cs:269–280`.
- Strategy continues to use only `AccuracyPlan.ForLevel`: `AI/StrategyPatches.cs:190–191,220–222`.
- That rate scales every weapon’s arriving shots: `AI/Brain/Strategy.cs:100–104`.
- Fire/hold decisions depend on the resulting passing-shot count: `AI/Brain/Strategy.cs:223–248`.

**Failure scenario:** Twenty guided weapons with an estimated 75% defense pass rate produce:
- Old strategy model: `20 × 0.7 × 0.75 = 10.5` passing shots.
- Intended new model: `20 × 0.85 × 0.75 = 12.75`.

That crosses the default twelve-passing-shot firing threshold. Strategy can hold and spend released firing energy when the intended new volley would qualify to fire.

**Concrete fix:** Use the same authoritative weapon classification and class rates in both strategy estimation and firing selection. Apply rates before combining weapon families; a blanket 0.85 replacement would incorrectly improve unguided estimates.

### 5. The intel sprint can displace the opening’s compounding investments

**This is a confirmed budget trade-off, not a demonstrated v0.15 economy collapse.**

**Evidence**
- Intel purchases precede ordinary economy: `AI/DirectorPatches.cs:554–565,636–637`.
- The sprint has a count/deadline gate, without an opening-income or spending-share guard: `AI/Brain/Director.cs:270–274`.
- Telescope construction, upkeep and yield: `docs/ruleset/embedded/enhanced/BuildingsData.csv:13–14,22,25`.
- Historical opening budgets and purchases: `docs/playtests/data/v0141-ai-decisions.jsonl:21–38`.

Six bare Telescopes cost **1,200 energy, 1,800 material, six power slots, and 120 recurring energy per turn**, before any enabling plants.

Historical T5 began with **925 energy / 850 material**. Two priority Telescopes would consume 600 material, leaving 250—insufficient for even one 350-material Molecular Recycling Plant project. The actual opening bought two such projects that turn.

The research timing also needs correcting: the intel branch costs 650 science, but the first four plan targets cost **1,300 science total**. At the recorded opening science rate, Telescope availability is approximately a T5 event, followed by construction/activation delay—not guaranteed T4 acquisition. See `AI/Plugin.cs:158–162` and `docs/ruleset/embedded/enhanced/TechTreeData.csv:8`.

**Failure scenario:** Telescope purchases delay material projects and power expansion; the resulting lower income then prolongs the sprint. Counterintel projects beginning at T8 add another priority demand. The AI acquires coordinates but reaches its military window materially weaker.

**Concrete fix:** Keep the early research reorder, but reserve a bounded core-economy investment during the sprint, or impose a sprint spending limit while material income is low. Compare bare construction with available intel upgrades when choosing the cheapest operational route to the acquisition threshold. Validate the joint research-and-construction opening rather than replaying unchanged v0.14.1 income assumptions.

### 6. `TryCityProject` still permits native re-selection onto a ruined city

**Evidence**
- `TryCityProject` constructs an entity containing a city **type**: `AI/DirectorPatches.cs:1268–1281`.
- `HasCityForProject` proves that some Normal city is eligible, but returns only a Boolean: `AI/DirectorPatches.cs:1303–1313`.
- `QueueProject` delegates selection back to native: `AI/DirectorPatches.cs:1442–1451`.
- Native iterates all buildings of that type without checking Normal state: `decompiled/Interplanetary.AI/AIConstructionModule.cs:250–284`.
- Native slot checks also lack a state test: `decompiled/Interplanetary.AI.Construction/AIProjectConstruction.cs:24–36`.
- Ruined cities skip project `BeginTurn`: `decompiled/Interplanetary.Prefabs/BaseBuilding.cs:1033–1046`.

**Failure scenario:** A living Small City passes eligibility, but an earlier ruined Small City retains a free project slot. Native selects the ruin, and installation can still charge resources (`decompiled/Interplanetary.Prefabs/BuildingProjects.cs:190–236`).

**Concrete fix:** Retain the actual eligible `BaseBuilding` and enqueue `AddProjectToQueue(project, city)` against that city, preserving decision-budget charging. Revalidate state, slots, installed/pending duplicates immediately before enqueueing.

**No infinite loop was established:** the new path permits at most two successful purchases per turn, and queued/installed duplicate checks prevent repeatedly choosing the same project on the same city. Native does honor the supplied project ID and city type; the defect is instance re-selection, not substitution of another project or rejection because its energy-generation efficiency is zero.

### 7. The volley ledger is not reset on match initialization or load

**Evidence**
- Static ledger and turn-only invalidation: `AI/Patches.cs:469–479`.
- Match initialization calls `ResetMatchMemory`: `AI/Patches.cs:37–39`.
- That reset never clears `s_volley`: `AI/Plugin.cs:672–681`.

**Failure scenario:** Fire at T22, then load a T22 save in the same process. Player ID, turn and saved building IDs match, so previous reservations are reused. The first new shot can treat targets as already fully allocated.

**Concrete fix:** Add an explicit combat-patch reset that clears `s_volley`, and invoke it from `ResetMatchMemory`.

Player isolation and ordinary advancing-turn reset are otherwise correct. Building `ObjectId` keys also separate targets across enemy planets; an additional enemy key is unnecessary for normal globally identified buildings.

### 8. Guided threat inherits shooter-blind incoming memory

**Evidence**
- New trigger: `AI/DirectorPatches.cs:306–315`.
- Incoming estimates are merged into `EnemyWeapons`: `AI/EconomyPatches.cs:139–144`.
- Incoming memory is keyed only by weapon type: `AI/Brain/DefensePlan.cs:128–148`.
- Defender impacts are recorded before the self-shot exclusion: `AI/Brain/DefenseLedger.cs:49–71`.

**Failure scenarios**
- An eliminated opponent’s Missile impacts continue contributing to incoming memory and therefore guided-cover demand.
- If a self-inflicted guided impact reaches this ledger, it also qualifies. The default safe-miss filtering reduces opportunities for this; I did not establish such an occurrence in the evidence match.

Visible-weapon memory is correctly pruned by living enemy identity. Incoming-impact memory is not.

**Concrete fix:** Retain incoming estimates per shooter and weapon, exclude self impacts from enemy-threat inference, and aggregate only living enemies. Keep self-damage in separate diagnostic totals if useful.

### 9. The allocation ledger spreads nominal damage, but its kill budgets can be badly wrong

**Evidence**
- Reservation uses raw live damage: `AI/Patches.cs:507,518–521`.
- Target fullness uses frozen durability and fixed overkill: `AI/Brain/Memory.cs:235–247`.
- Shadow state remains frozen while visibility remains Shadow: `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:195–207`.
- Damage registration does not refresh that frozen state: `decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:572–585`.
- Interception removes direct damage: `decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:335–360`.

**Failure scenario:** A city recorded at 300 population early is now at 600. Three base Missile assignments reserve 450, exceed the 375 allocation threshold, and move to another city without killing the first.

Interception causes a separate large error: five Missiles reserve 750 against a 600-population city. With two funded 80% Ions covering each impact, expected direct population damage is only **30**. A fixed 1.25 multiplier is not a general interception allowance.

**Concrete fix:** Track observation age and use an explicitly uncertain durability estimate based on legitimate observations and plausible growth. Incorporate conservative pass probability when reserving useful damage, rather than treating all assigned damage as delivered. Do not replace frozen values with hidden live population.

## Low finding

### 10. The new logs still cannot reconcile assignment with delivered city damage

**Evidence**
- Strike records omit firing weapon identity: `AI/Patches.cs:561–576`.
- The defense ledger aggregates by defender/shooter/weapon family, not assigned target building: `AI/Brain/DefenseLedger.cs:49–86`.
- City populations are an order-dependent string without city IDs: `AI/DirectorPatches.cs:1292–1300`.
- `idleEnergy` explicitly includes defense reserve: `AI/DirectorPatches.cs:2029–2031`.
- Execution may later reject first-phase payment or second-phase completion: `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:273–315`.

**Failure scenario:** Assignment records show adequate allocated damage, but payment, interception or stale durability prevents kills. The next analysis still cannot identify which assignments actually damaged which cities. Meanwhile, `idleEnergy` can be mistaken for waste despite including deliberately protected defense funding.

**Concrete fix:** Add firing weapon ID to assignments, stable city IDs to population snapshots, and an observable assignment-to-impact/damage link. Describe `idleEnergy` as a planning balance including reserve; it is not a physical post-action unused-energy measurement.

## Native-contract answers that checked out

### Shadow source, persistence and position

- **`GetShadowBuildingState` is the correct engine-owned frozen source.** Its city `HP` is population, and `BuildingState` is a stored field captured when the snapshot is constructed—not a live forwarding property.  
  `decompiled/Interplanetary.PlayerDataManagement/ShadowBuildingFrozenState.cs:39–63`.
- The engine refreshes snapshots while non-Shadow and on entry into Shadow, then retains them during continued Shadow visibility. Surviving own hits do **not** update frozen population. Ruined cities become Visible and are rejected by the adapter’s Normal-state check.  
  `PlayerIntelHandler.cs:73–75,183–207`; `PlayerBuildingHandler.cs:572–585`.
- **Using the city’s local transform position is compatible with native targeting.** Native itself submits remembered building transforms. The queued action stores the position, and completion checks targeting prerequisites without requiring current visibility.  
  `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:418–443,483–485`; `decompiled/Interplanetary.AI.Actions/AISecondPhaseTargetWeaponAction.cs:15–29`; `PlayerWeaponHandler.cs:295–315`.
- **I found no ordinary path returning `false` without queueing a second-phase action.** The queue method unconditionally appends an action, and the prefix returns `false` only after that call succeeds. Later execution failure remains possible.  
  `AI/Patches.cs:507–528`; `decompiled/Interplanetary.AI/AIActions.cs:172–175`.

### Damage and accuracy API

- **`DamageModule.PopulationDamage` includes currently applied upgrades and global modifiers.** It is the engine’s nominal per-impact damage, not expected damage after interception, splash overlap, fallout or multiple projectiles.  
  `decompiled/Interplanetary.Weapons/WeaponDamageModule.cs:22–27`; `decompiled/Interplanetary.Prefabs/BuildingWeaponStats.cs:30–45`; `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:382–392`.
- The current hit cap cannot exceed the retained list total, and native selection treats Hits/Misses as count limits rather than array indexes. Retained dead entries still inflate `Misses`, making the goal equality potentially unattainable, but I found no other consumer of `IsGoalMet` that turns this into a separate deadlock.
- `AccuracyCarry` is per player and cleared on normal resets; its fractional arithmetic is sound.
- **`HasTargetingPhase` is available on every normally constructed weapon.** However, **MIRV does not have second-phase targeting** in this engine. It retains level accuracy under this classifier. Its inclusion in the defensive threat list is reasonable, but the config text listing MIRV as second-phase guided is inaccurate.  
  `decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:52–62`; `decompiled/Interplanetary.Weapons/WeaponTargetingModule.cs:22–38`.

I independently confirmed the supplied human calibration: **60 planet hits from 71 Missile shots**, approximately 84.5%, in `docs/playtests/data/v0141-human-telemetry.jsonl:166–710`.

### “Never seen” and research

- For surviving cities with **SeenBuildingsShadows enabled**, `Invisible` is a reasonable current “no remembered coordinate” test. Normal visible cities transition to Shadow and remain Shadow when intel falls.
- `RemovePlotOccupiedShadowBuildings` and `RemoveHitShadowBuildings` remove shadow-list/observer membership; they do not directly rewrite the visibility dictionary. They do not establish a Normal remembered city returning to Invisible. With shadows disabled, however, Invisible can mean “previously seen, currently hidden.”  
  `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:183–215`; `IntelMemory.cs:151–204`.
- **The research append fix is correct.** Clearing only the first successful addition preserves later chains, and native append preserves the existing queue. The unchanged V014 default is now explicitly migrated. Existing saved queues drain normally before the new plan gets its next boundary.  
  `AI/EconomyPatches.cs:504–545`; `decompiled/Interplanetary.PlayerDataManagement/PlayerTechnologyHandler.cs:480–518`; `AI/Plugin.cs:169–177,344–350`.
- The intel-cutting upgrade guard implements the stated condition correctly. It does not yet value positive intel upgrades or ensure that counted Telescope buildings are powered.

## What most likely still loses the next match?

**My leading residual risk is another exposed-city loss despite owning plenty of Ions.** The trigger now notices the right weapons, but the placement success condition still permits spending the response somewhere else. The accuracy initialization bug then weakens the first newly built offensive volley, reducing the chance of winning the exchange first.

**Single fastest measurement:** immediately before the first human Missile volley, record **the minimum qualifying powered-Ion count across surviving AI cities**—using actual placed positions and the same reach rule as the deficit calculation.

A minimum below two immediately falsifies the intended per-city policy. `antiNukeProtecting` and `citiesProtected` cannot establish that requirement: the former is planet-wide inventory, and the latter counts cities with at least one defense.

## Next moves

1. **Fix the accuracy adapter’s initialization-order bug**, using the native targetable-weapon argument; verify new-only, mixed old/new, dead-entry and load cases.
2. **Make critical Ion placement close an explicit city deficit**, with planned per-city reservations and a bounded response when inventory caps block an exposed city.
3. **Unify accuracy planning and strategy**, then enforce class quotas and prevent insertion-order starvation of superweapons.
4. **Bind projects to the validated city and clear the volley ledger on reset.**
5. **Run one instrumented opening-to-first-volley playtest:** confirm operational city acquisition before counterintel, check T4–T8 income displacement, and capture the minimum per-city Ion count before the first guided exchange.
