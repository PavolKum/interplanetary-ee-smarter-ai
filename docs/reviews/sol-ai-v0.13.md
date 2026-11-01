The repair call is on the correct side of the resource snapshot: it spends already-credited energy, and native AI initialization does not refill it. The human-repair premise is much weaker: telemetry has no per-building HP or repair events, and several attacked turns show no material spending. I’m checking interception and damage spread before attributing the surviving buildings to repairs.
Railguns never reach the second-phase targeting hook. The game also randomizes their surface impact location after a planet hit, so that hook cannot concentrate Railgun fire. Guided Missiles can use it, but the logs show zero visible human cities in the sampled attack turns; precision fire needs better intel as well as a different weapon mix.
The comparison has a major confound: the human’s first recorded shot is T24 in v0.12.1 versus T28 in v0.10.1. He also has 30 Railguns at T24 in the new match, versus none in the old one. The cap demonstrably restricted AI growth, but the four-turn earlier defeat does not isolate its effect.
Verdict: fix first

**Confidence: High on the lifecycle and scoring findings; Medium on their effect on match results.** Repair is integrated at the correct point, and the director reads the current turn’s committed target correctly. The main problems are economic allocation and offensive effectiveness: disabling saturation still permits wasteful power investment, the weapon score assumes the weapon cap is always the binding constraint, and the evidence does not establish human repair as the reason strikes failed.

## High findings

### 1. Disabling saturation leaves a path back to the v0.11.1 power-investment failure

**Evidence**

- With saturation disabled, the remaining broad brake on energy investment is previous-turn energy glut: `AI/Brain/Director.cs:81–107,123–127`.
- Without energy glut, energy gains retain a positive weight even when energy income already exceeds the desired ratio. `Best` buys any affordable positive-scoring option; there is no investment-payback or useful-capacity ceiling: `AI/Brain/Director.cs:175–180,190–213`.
- Material glut suppresses extraction, but does not constrain power purchases: `AI/Brain/Director.cs:140–142,203–204`.
- The 15k floor applies inside `Best`. Power recovery, intel, defenses, weapons and weapon upgrades execute before that floor: `AI/DirectorPatches.cs:468–562`.
- Repair also spends material before the reserve is calculated: `AI/DirectorPatches.cs:756–812,210–217`.

**Walking Beam’s recorded T22–T28 states through v0.13**

These are **fixed-state counterexamples**, not a prediction that a replay would reproduce the same purchases. Earlier v0.13 choices would change later states.

| Turn | Planet material | Starting material | Recorded energy-glut state | Consequence under v0.13 |
|---|---:|---:|---|---|
| 22 | 190,865 | 23,378 | False | Material glut is also false. Extraction and power remain eligible; saturation no longer blocks power. |
| 23 | 163,547 | 21,157 | False | Same: neither glut provides a brake on power investment. |
| 24 | 124,334 | 27,611 | False | The reserve certainly engages: even production alone implies under eight turns remaining. Economy can still invest material above 15k in power. |
| 25 | 64,398 | 42,408 | False | Significant stock remains available above the floor; positive-scoring plants/upgrades remain eligible. |
| 26 | 9,598 | 40,184 | False | Same, immediately before depletion. |
| 27 | 0 | 9,682 | False | The floor blocks ordinary economy purchases, but repairs and earlier construction branches can consume the remaining stock. |
| 28 | 0 | 32 | True | Ordinary energy gains finally receive zero weight. Dry-mine sales provide some liquidity, subject to eligible leaves and the ten-per-turn limit. |

Sources: `docs/playtests/data/v0111-ai-decisions.jsonl:214,221,230,236,245,251,260,267,276,283,312,319,328,335`.

The glut detector’s timing matters. Beam’s T24 unspent energy was 6,670, but only **23.9%** of its 27,920 budget, below the configured 30% threshold. T26 left 5,333, only **19.2%**. Neither activates the following turn’s energy glut. The defaults are 30% **and** 5,000: `AI/Plugin.cs:339–341`; the conjunction is implemented at `AI/Brain/Director.cs:170–172`.

**Failure scenario**

An AI can continue turning a shrinking material bank into plants because each plant has a positive score. Spending on those plants keeps measured unspent energy below the glut threshold, delaying the very signal intended to stop investment.

The reserve reduces the size of the old failure; it does **not** establish that power investment is useful. Selling dry mines is liquidation afterward, not protection against the original misallocation.

**Concrete fix**

Keep saturation off as the default, but replace it with an explicit **useful-energy investment gate**:

- Forecast firing, defensive readiness, critical repair/reconnection and affordable construction.
- Buy additional generation only when that forecast needs it or the investment pays back within the relevant horizon.
- Exempt genuinely necessary connection recovery.
- Protect an operational/recovery material allocation across all spending branches, rather than only `Best`.

Do not restore the old material-income saturation formula unchanged. The v0.12.1 stockpiling evidence does support rejecting that formula.

---

### 2. The diagnosis overstates both the evidence for human repair and the controlled nature of the comparison

**Evidence: human repair**

The telemetry contains building-type counts and aggregate resources, but **no per-building HP, building identities or repair event type**. Its damage records contain only `kind`, `coverage` and `nominalAmount`, insufficient to reconstruct an individual building’s damage and repair history. Examples: `docs/playtests/data/v0121-human-telemetry.jsonl:98,112–114`.

More importantly, several turns show **no net material spending**:

| Turn | Material at round start | Material at turn end |
|---|---:|---:|
| 24 | 16,266 | 16,266 |
| 26 | 43,818 | 43,818 |
| 27 | 54,781 | 54,781 |
| 31 | 78,674 | 78,674 |
| 32 | 86,568 | 86,568 |
| 33 | 94,462 | 94,462 |
| 34 | 102,356 | 102,356 |
| 35 | 110,315 | 110,315 |

Sources: `…/v0121-human-telemetry.jsonl:98,102,1352,1358,1903,1906,2858,2864,3117,3124,3322,3327,3501,3508,3661,3668`.

Ordinary repair explicitly spends material: `decompiled/Interplanetary.Screens/BuildingInfoPopup.cs:793–805`. These snapshots therefore do not support routine repair between attacks. Offsetting sales cannot be excluded absolutely, but there is no evidence here establishing that explanation.

Likewise:

- **141 → 161 is a net count**, not proof that no building was destroyed.
- Population growth does not prove no damage. Between T26 and T27 population rises **3,723 → 3,783**, only 60 despite reported growth of 117: `…/v0121-human-telemetry.jsonl:1352,1903`.
- Railgun damage is not universally 125 in this match. That is the base value; the AIs queued Building Damage upgrades repeatedly, including six per AI at T22: `…/v0121-ai-decisions.jsonl:96,99`. The upgrade multiplier is ×2: `docs/ruleset/embedded/enhanced/UpgradesData.csv:103`.

**Evidence: comparison confound**

The human attacked substantially earlier in v0.12.1:

- First recorded human shot: **T24**, versus **T28** in v0.10.1:  
  `…/v0121-human-telemetry.jsonl:104`; `…/v0101-human-telemetry.jsonl:189`.
- At T24, the v0.12.1 human has **30 Railguns**; the v0.10.1 human has none:  
  `…/v0121-human-telemetry.jsonl:98`; `…/v0101-human-telemetry.jsonl:145`.
- The v0.10.1 human first has 16 Railguns at T27: `…/v0101-human-telemetry.jsonl:168`.

The four-turn earlier defeat coincides with four-turn earlier recorded human fire. That does not exonerate the energy cap, but it prevents attributing the survival difference to the cap alone.

**Failure scenario**

Development treats “the human repairs away our damage” as an observed mechanism, then answers with repair and additional throughput while leaving interception, dispersed impacts, target visibility and earlier human aggression unresolved.

**Concrete fix**

Revise the diagnosis to:

> AI strikes produced little observable suppression. Human repair is unverified; interception and dispersed, often partial-damage impacts are confirmed mechanisms.

Record building-specific damage, destruction and repair outcomes before assigning causality. Compare versions under a fixed human opening/attack schedule or a scripted opponent.

## Medium findings

### 3. `MaxWeapons > 0` does not mean weapon slots are the binding constraint

**Evidence**

- Per-slot scoring is enabled whenever the configured cap is positive, regardless of current army size or available firing energy: `AI/DirectorPatches.cs:889–891`.
- It then discards all construction and firing cost: `AI/Brain/Director.cs:292–298`.
- The adapter **does** filter construction affordability and the modeled upkeep cap before scoring: `AI/DirectorPatches.cs:865,871–872`.

Thus this change does **not** simply buy a weapon that fails its affordability checks. The problem is choosing the wrong feasible composition.

**Failure scenario**

At 10k income and the default 40% upkeep cap, an otherwise empty army can support approximately:

- 30 base Railguns, using 3k firing energy;
- 13 base Missiles, using 3.9k.

Before interception, the current proxy gives roughly:

- Railguns: `30 × 375 = 11,250`;
- Missiles: `13 × 455 = 5,915`.

Yet per-slot scoring prefers each Missile. In that state, firing energy—not the thirty-weapon cap—is binding.

There is also an adaptation problem: existing Normal and UnderConstruction weapons occupy cap slots, and purchasing at the cap rejects additional aimed weapons: `AI/DirectorPatches.cs:230–234,854–870`. There is no ordinary arsenal-replacement path; the selling implementation handles dry mines only: `:820–848`.

Moving Orbital Assembly earlier helps the default opening, but does not solve a delayed unlock or an existing thirty-Railgun save.

**Concrete fix**

Rank a small **feasible army increment/composition** under both upkeep and weapon-slot limits, rather than selecting the score mode from the existence of a cap.

Also reserve some capacity for the approaching counter-weapon unlock, or allow a bounded replacement of demonstrably poor weapons. Include installed upgrades and observed launch usefulness in replacement value.

The same binding constraint should inform upgrades: currently only `EnergyGlut` switches upgrades to immediate damage ranking; reaching the weapon cap does not: `AI/DirectorPatches.cs:1339,1382–1393`.

**Timing check passed:** `StrategyPatches.BeginTurn` executes at `AI/EconomyPatches.cs:154`, before `DirectorPatches.BeginTurn` at `:223`. Therefore the new `CommitTarget` read is the current turn’s decision, not accidentally last turn’s.

---

### 4. Weapon-specific interception memory can make an untested Coilcannon displace the intended Missile counter

**Evidence**

- Observed pass overrides the estimate only for the exact weapon ID. An unobserved type with no remembered defenses receives pass 1: `AI/Brain/Director.cs:271–287`.
- The adapter supplies observations separately by weapon ID: `AI/DirectorPatches.cs:350–357`.
- Enhanced Kinetic Defense counters **both Railguns and Coilcannons at 0.8**: `docs/ruleset/embedded/enhanced/DefensesData.csv:10–11,26–27`.
- With the actual adapter formula, base scores before interception are:
  - Railgun: **375**
  - Missile: **455**
  - Coilcannon: **1,320**

  Formula: `AI/DirectorPatches.cs:873–878`; inputs: `docs/ruleset/embedded/enhanced/WeaponsData.csv:9–12,20`.
- The selection band is 60% of the best: `AI/Brain/Director.cs:315–318`.

**Failure scenario**

Against the unseen human defenses, Railgun observations show pass 0.45, while Coilcannon has not been sampled:

- Railgun: 168.75
- Missile: 455
- Coilcannon: 1,320

The Missile falls below the Coilcannon’s **792** eligibility threshold and is excluded. The new rule can therefore favor another weapon stopped by the same hidden defense.

Coilcannon fragments may still be useful for saturation; this is not proof that Missiles always win. It is proof that the claimed Missile-counter behavior is not established by the two-weapon test.

**Concrete fix**

Maintain uncertainty-aware beliefs across weapon/defense classes. Railgun interception is evidence of defenses relevant to Coilcannons, although it does not uniquely identify Kinetic Defense or establish its count.

Allow limited probes of untested types; do not treat “untested” as “known to pass completely” while filling the whole arsenal. Test the full available candidate set, including Coilcannon and MIRV.

---

### 5. Repair’s lifecycle is correct, but its budget does not protect firing, emergency defense or critical reconnection

**Verified mechanics**

The important integration checks pass:

- Normal round initialization credits resources before AI turn execution:  
  `decompiled/Interplanetary.GameEngine/GameManager.cs:368–373`;  
  `…/PlayerManager.cs:664–674`;  
  `decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:446–458,314–320`.
- Energy is reset to production during that resource initialization: `…/PlayerResourceHandler.cs:158–159`.
- Repair spends real resources before `AIResources.BeginTurn` reads them: `AI/DirectorPatches.cs:803–807`; `decompiled/Interplanetary.AI/AIResources.cs:38–64`. Native AI initialization does not refill them.
- Hold-fire release adjusts planning budgets, not the real resource handler: `AI/StrategyPatches.cs:269–276`.
- `RepairBuilding` directly restores HP, invokes the callback, emits `BuildingRepaired`, and records the repaired building. It does not require an AI-specific UI: `…/PlayerBuildingHandler.cs:589–599`.
- Single-player proceeds directly to local end-turn/action-phase handling rather than replaying network repair lists: `…/PlayerManager.cs:138–158`.
- The adapter correctly excludes cities, ruins and buildings still UnderConstruction: `AI/DirectorPatches.cs:777`. The handler itself only checks null, so these guards are necessary. City survival uses population, not ordinary building HP: `…/BaseBuilding.cs:964–987`.
- Repair does not erase interception history: `AI/LedgerPatches.cs:27–73`. Native threat/construction initialization has no separate “damaged building” repair queue to invalidate; threat snapshots read HP normally: `decompiled/Interplanetary.AI/AIThreatAssessment.cs:263–273`.

**Budget problem**

Repair uses up to 25% of current real energy and **all available material**, before firing reservations and defense/recovery allocations: `AI/DirectorPatches.cs:798–807`.

An income collapse can leave existing upkeep above its purchase-time cap. For example, with 4k available energy and 3.5k existing upkeep, 1k of repairs leaves only 3k for firing. `AIResources` then computes negative usable energy; actual firing also checks real resources: `decompiled/Interplanetary.AI/AIResources.cs:59–64`; `…/AIActions.cs:130–138`.

Priority also ignores connectivity. An unpowered weapon outranks the generator or reconnectable fighting core that could make surviving weapons useful: `AI/DirectorPatches.cs:777–787`.

**Assessment of 25% / 80% / 40**

These are reasonable experimental limits, not data-calibrated values:

- At the recorded 16–24k income, forty ordinary repairs cost 4k energy; the count cap frequently binds.
- Forty ordinary plant/mine/defense/Railgun repairs can consume roughly **4–9k material**; advanced weapons cost more: `docs/ruleset/embedded/enhanced/BuildingsData.csv:31–32`.
- An 80% threshold catches substantial direct hits while skipping small grazes. The logs lack the own-building HP distribution needed to establish its efficiency.
- Repair cannot save buildings destroyed within one action phase, nor restore lost cities.

**Concrete fix**

Fund essential firing/readiness and critical reconnection before discretionary repair. Rank powered or imminently reconnectable fighting assets first, allowing a critical generator to outrank an isolated weapon. Give repair a material allocation as well as an energy allocation.

The empty callback is functionally valid, but omits the icon refresh performed by the human repair callback. Passing `b.RefreshBuildingIcon` would improve observable state consistency.

---

### 6. The extraction filter is sensible for this match, but its latched glut signal can suppress needed recovery investment

**Evidence**

- Material glut is recomputed from fixed **turn-start** stock and energy, explicitly ignoring subsequent spending: `AI/DirectorPatches.cs:921–923`.
- Earlier military branches can spend that stock before economy selection: `:502–559`.
- The new extraction filter is categorical: `AI/Brain/Director.cs:203–204`.

**Failure scenario**

A borderline-glut AI spends much of its stock on defenses, weapon purchases or material-heavy upgrades, but remains classified as glut for the rest of the turn. With weak surviving extraction and a still-productive planet, it can reject a useful mining investment.

This is not an observed starvation problem in Clowrus’s 57–96k-stock states. It is an unhandled recovery state.

**Important qualifications**

- **Repair does not itself stale this flag:** it runs before the snapshots. Its spending is already reflected in starting stock.
- Dry-planet behavior is sound: new mines are excluded, project extraction gains are scaled to zero, and ordinary extraction upgrades have no positive gain: `AI/DirectorPatches.cs:1033,1148–1151,1201–1202`.
- Refineries remain eligible through `SavesReserve`, and the adapter checks whether they actually reduce global drain: `:1035–1047`.
- The current Enhanced yield upgrades are not efficiency improvements: yield and planet cost both multiply by ×1.75 or ×2. The separate refinery reduces cost ×0.7: `docs/ruleset/embedded/enhanced/UpgradesData.csv:83,95`.
- A future combined yield/efficiency upgrade would need better classification: the current efficiency discovery requires yield multiplier ≤1: `AI/DirectorPatches.cs:1009`.

**Concrete fix**

Use projected post-commitment material cover, including existing extraction and near-term obligations, with hysteresis. Avoid simply flipping glut on every purchase, which would create oscillation. Preserve an explicit exception for genuinely planet-saving upgrades.

## Low finding

### 7. The new tests validate isolated rules, not the decisive adapter interactions

`V013Rules` tests repair ordering, extraction classification and a Railgun-versus-Missile comparison: `AI/Tests/Program.cs:335–394`. The test project compiles the Brain files, not the game adapters: `AI/Tests/BrainTests.csproj:12–14`.

Missing counterexamples include:

- The full weapon set under hidden defenses.
- Upkeep-bound versus slot-bound composition.
- Existing-cap adaptation after a counter-weapon unlock.
- Repair funding after an income collapse.
- A historical Beam state with saturation off and no energy glut.
- Material glut after committed spending.

The research migration and prerequisite placement look correct: Orbital Assembly requires Advanced Signal Processing and unlocks Missile (`docs/ruleset/embedded/enhanced/TechTreeData.csv:4–5,9`). The unchanged v0.11 plan migrates, and the 1.5 saturation default migrates once: `AI/Plugin.cs:304–308,437–444`.

**Verification limit:** read-only static inspection and JSON analysis only. `git diff --check 0d3d05a` passed. No builds, tests, mutations or in-game execution were performed; repair presentation and runtime behavior remain **unverified in-game**.

## Ranked next moves

### 1. Build a guided, intel-supported kill capability

**Concentrating Railgun fire through `SecondPhasePrefix` is not feasible.**

- Railguns do not have second-phase targeting: `decompiled/Interplanetary.Weapons/WeaponTargetingModule.cs:35–39`.
- The AI calls the hook only for weapons with that phase: `decompiled/Interplanetary.AI/AITargetWeapons.cs:142–151`.
- For unguided weapons, the game additionally rotates the surface hit location randomly: `decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:155–160`.
- Splash damage depends on overlap, so a 6.0-radius hit does not deliver full damage to every touched building: `:375–396`.

Use guided Missiles with the existing hook. Allocate enough expected damage to destroy a **visible** valuable target, then move to the next; account for planned damage to avoid overkill. The present planner repeatedly chooses the weakest city or weapon without a salvo allocation: `AI/Brain/Memory.cs:169–191`.

Measure precision-target availability explicitly. The logged `cities:0` indicates an unreadable population/city overlay (`AI/Patches.cs:180–184`), not a definitive per-building visibility census. Actual candidate visibility must be logged separately.

### 2. Counter incoming weapon types even when their buildings remain hidden

The human’s Laser shots are directly recorded from T29: `docs/playtests/data/v0121-human-telemetry.jsonl:2232–2234`.

The AI already records incoming weapon IDs in `DefenderStats.ImpactsByWeapon`: `AI/Brain/DefenseLedger.cs:24,58–61`. Yet ordinary counter-defense and counter-research use the visible/remembered weapon-building inventory: `AI/DirectorPatches.cs:709–727`; `AI/EconomyPatches.cs:481–492`.

After Maverick disappears, Clowrus reports no known enemy weapons despite continuing attacks: `…/v0121-ai-decisions.jsonl:257,264,272`.

Feed observed incoming types into counter urgency. This gives a fair, direct route to Shields against Lasers without waiting to see the Laser buildings. Enhanced Shields have base interception probability 1 against Lasers: `docs/ruleset/embedded/enhanced/DefensesData.csv:13,29`.

### 3. Finance the fighting core over a rolling horizon

Implement the shared material/energy allocation described above, then spend repair money on surviving combat value and connectivity. Track operational weapons, protected cities and funded recovery—not total buildings or depletion turn alone.

This is the prerequisite for converting the restored economy into sustained pressure instead of another large, disposable plant estate.

### 4. Adapt the arsenal using interception evidence and controlled replacement

**Yes, infer hidden defenses from observed interception.** Infer uncertain effective coverage/counter classes, not exact hidden building counts or coordinates.

Combine that belief with:

- Per-type launch success.
- Actual firing-energy constraints.
- The thirty-weapon limit.
- A small exploration allowance.
- Bounded replacement of poor weapons.

Missiles are not intrinsically worse for the trajectory engine: both use gravity flight with mass 3, while Missiles have lock-on support. However, their minimum launch velocity is higher, and the sloppy search does not pass lock-on range while accurate search does: `decompiled/Interplanetary.Weapons/WeaponFlightModule.cs:18–23`; `docs/ruleset/embedded/enhanced/WeaponsData.csv:17–20`; `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:289,342–347`. Measure realized service by type rather than assuming the fixed ×1.5 homing bonus captures it.

### 5. Measure kills and suppression per aiming budget, then repeat the comparison with fixed aggression

Log accepted firing actions, weapon type, intended surface target, interception, actual damage, destruction and repair. Include completed search work and target visibility.

Replay the same starting position with a fixed human attack schedule and fixed thinking budget. The success criterion should be **cities/weapons disabled and wins**, supported by surviving material and recovery capacity. Another energy-production record or four extra turns of survival would not, by itself, demonstrate progress toward beating this human.
