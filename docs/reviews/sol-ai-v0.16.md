The review has already exposed two substantive gaps: the new intel path still consumes `EnemyView.VisibleCities`, which is an aggregate surviving-city count rather than currently targetable cities, and the slot-reserve calculation omits native projects already committed this turn. I’m tracing the native execution order to establish their actual effects and check the firing and defense interactions.
The native ordering confirms a more serious defense issue: repairs run before the defense ledger rolls forward, so their “protected” budget uses stale demand. I also found that Triple Barrel’s extra firing cost is added to the director’s estimate without reserving that energy, and the old preferred-intel purchase path bypasses the new budget and stop-loss. These affect the next-match funding gates, not just the logs.
A native-source check ruled out the suspected same-turn Triple Barrel funding leak: weapon firing multipliers activate at the next turn’s `FinishUpgrade`, not when the upgrade is purchased. The blind-upgrade path does omit the planned-effect bookkeeping used by the generic upgrade planner, but that is a forecasting issue rather than an immediate extra firing charge.
# Review: AI v0.16.0

**DO NOT SHIP the current build.** The implementation fixes the two identified temporary-project accounting defects in part, and the config/research-order changes broadly match the council. However, two retained fairness paths contradict the signed decisions, and several integration defects undermine the slot, intel, defense and accuracy policies.

**Confidence:** High on the source-level findings; competitive effects remain unverified in Unity.

Reviewed HEAD `c3b89f7`, the supplied diff, signed record and post-sign-off decisions, relevant native implementation, Enhanced ruleset data and v0.15.3 evidence. No files were changed. Builds, tests and mutations were not run under the read-only restriction.

## 1. Release-blocking findings

### F1 — BLOCKER: Shadow cities still rescue precision targeting

**Locations:** `AI/Patches.cs:647–653`, `AI/Patches.cs:599–606`, `AI/Patches.cs:609`.

`AddStrikeCandidates` still accepts a Shadow city, reads its frozen durability, and subsequently targets its coordinates. This directly contradicts decision-record-r2 §1.10: a city outside current visibility must not be rescued by remembered coordinates, durability or native frozen snapshots.

This is retained v0.15 behavior rather than a newly introduced regression, but removing it is part of the approved v0.16 boundary.

**Removing the Shadow branch alone is insufficient.** When no candidates remain, `SecondPhasePrefix` returns `true`. Native second-phase targeting accepts cities whose visibility is anything other than `Invisible`, which includes Shadow:

- `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:418–443`
- Native coordinate selection: `:483–485`.

**Concrete fix:** Use current-Visible candidates only, and handle the zero-candidate case without returning to the native building-selection path. Either withhold the weapon or explicitly invoke an information-independent random-surface fallback. Do not let a Shadow record select the point.

---

### F2 — BLOCKER: P2’s “no enemy city count” decision is not implemented completely

**Locations:** `AI/Patches.cs:216–220`; consumer at `AI/DirectorPatches.cs:469–472`; other consumer at `AI/Brain/TargetPlanner.cs:75`.

`VisibleEnemyCities` is a correctly filtered count of currently visible intact cities. But `EnemyView.VisibleCities` is a different quantity:

```csharp
view.VisibleCities = enemy.BuildingHandler.GetRemainingCities().Count;
```

It is the actual surviving-city count behind the population-readability gate. `BlindEnemies` still consumes it, and targeting still uses it to enable the finish-off term.

The old match demonstrates the distinction: at T22, `target.cities` was **5**, while only **2** cities were currently targetable. See `docs/playtests/data/v0153-ai-decisions.jsonl:107` and the signed record §1.4.

A readable aggregate count was an option under P2(a). Pavol chose **P2(c)** instead. The fact that this path predates v0.16 does not exempt it.

**Concrete fix:** Remove the aggregate city-count read and consumers. Represent permitted population readability with a boolean; use actual current-Visible candidates for targetability and own visibility outcomes for intel evaluation.

---

### F3 — HIGH: `CityFreeSlots` omits native projects already committed this turn

**Locations:** `AI/DirectorPatches.cs:1564–1567`, `:1611–1612`, `:1623`.

The native calculation is:

```text
Projects.FreeSlots − projectsInstalledThisTurn − queuedProjects
```

Your helper subtracts only queued projects.

**Authoritative source:** `decompiled/Interplanetary.AI.Construction/AIProjectConstruction.cs:24–36`.

This matters during normal execution:

1. A project leaves the construction queue.
2. `AIActions.PurchaseProject` registers it in `m_projectsInstalledThisTurn`.
3. Actual installation happens later when actions execute.

Sources:

- `decompiled/Interplanetary.AI/AIConstructionQueue.cs:145–178`
- `decompiled/Interplanetary.AI/AIActions.cs:107–115`.

**Concrete counterexample:** A city has two physically free slots. One project is already committed as an action, so native availability is one. Native `HasCityFreeSlots` allows another purchase, while your helper still reports two. `PermanentAllowed` therefore accepts a permanent and consumes the reserved final slot.

The existing native availability check prevents an outright over-capacity purchase; it **does not enforce your additional reserve**.

**Concrete fix:** Include the native committed-project ledger, or maintain equivalent per-city commitments until action execution. Use the same authoritative slot calculation for policy and logging.

---

### F4 — HIGH: Repairs protect stale demand, and still do not protect the planned defense allocation

**Locations:** `AI/DirectorPatches.cs:1001–1007`, `:1058–1059`, `:1118–1129`; `AI/EconomyPatches.cs:94`; reserve cap at `AI/DirectorPatches.cs:600–610`.

Repairs run in the **prefix** of `AICore.BeginTurn`. The defense ledger rolls in the **postfix**. Consequently, `RepairDefenseHold` reads the previous `LastDefender`, not the action phase that has just finished.

After a larger incoming volley, repairs can spend against the old small demand before construction sees the new demand.

There is a second problem: the repair hold includes only observed/forward demand. It does not protect the planned defense allocation that the signed post-sign-off diagnosis identified as already exceeding the available room.

The motivating arithmetic therefore remains possible:

```text
14,725 production − 5,100 firing − 3,600 repairs − 2,945 recovery = 3,080 reserve room
```

Raising the planned request to 40% does not change that room.

Also, repair firing protection counts only powered weapons (`:1023–1024`), whereas native `AIResources.BeginTurn` reserves firing costs for **all Normal weapon buildings**: `decompiled/Interplanetary.AI/AIResources.cs:44–61`.

**Concrete fix:** Prepare the current defense evidence once, before repairs. Compute the intended allocation—including planned, observed, forward and applicable Ion demand—against the same firing commitments native accounting will reserve. Protect that allocation before discretionary repairs, while explicitly retaining the recovery floor. Log each reduction and its reason.

---

### F5 — HIGH: Native minimum/preferred intel bypasses the new budget and stop-loss

**Locations:** `AI/DirectorPatches.cs:670–686`, `:782–787`; `AI/Brain/Tempo.cs:98–113`.

Two paths escape the adopted policy:

- **BelowMinimum:** `nativeRoom` can authorize purchases when `IntelBudget.Allowed` returns zero. The spend is recorded afterward, so it can exceed the 10% budget and continue beyond the probe limit.
- **BelowPreferred:** step 7 buys directly without calling either `Allowed` or `Record`. These purchases escape spending, batch and probe accounting entirely.

Repeated BelowMinimum purchases can also reset `BatchTurn` each turn through `Record`, postponing evaluation of the open batch.

This is not the budget starving native BelowMinimum. The present failure is largely the reverse: **native demand can override the budget indefinitely**.

**Concrete fix:** Route every intel building/upgrade purchase through one accounting boundary. Exempt native minimum from discretionary bid timing where required, but give it a prioritized allocation within the common spend policy. Remove the independent preferred-intel bypass, and do not restart an awaiting-evaluation discretionary batch with unrelated minimum purchases.

---

### F6 — HIGH: The zero-candidate purchase prohibition misses Nuclear Missiles and is not target-specific

**Locations:** `AI/DirectorPatches.cs:1185–1191`, `:801–827`; goal selection at `AI/EconomyPatches.cs:258–277`.

The Nuclear prohibition inside `TryWeapon` is ineffective: limited superweapons are excluded at `:1185` before that prohibition is reached. Nuclear Missiles are bought through `TryBuySuperweapon`, which has no visibility/candidate guard.

The ruleset confirms `bui_NuclearMissile.max_amount_at_once = 1`.

Furthermore, `d.Blind` means **no city visible anywhere**. In a multi-enemy match, a visible city on enemy A permits Missile purchases even when the committed target B has no legal candidates.

**Concrete fix:** Apply the current-candidate policy to superweapon goal selection and purchase as well as ordinary weapons. Evaluate candidates for the intended target. Filter forbidden goals before allocating savings, so a prohibited Nuclear purchase does not unnecessarily retain its material budget.

---

### F7 — HIGH: Existing blind Missiles remain unrestricted; the paid-energy gate cannot be enforced or measured

**Locations:** `AI/Patches.cs:407–475`, `:583–606`; planned-cost logging at `AI/StrategyPatches.cs:381–383`.

Blocking purchases does not stop existing guided weapons from receiving hit slots and firing through the zero-candidate fallback. A predominantly Missile arsenal can therefore spend far more than the adopted **10% of paid firing energy** on zero-candidate precision weapons after blindness.

The added `guidedFiring`/`unguidedFiring` fields describe the operational roster at turn start. They are **not actual paid firing energy**:

- Some weapons lack trajectories.
- Some planned misses are discarded.
- Holds and aborts fire nothing.
- Native targeting can fail to complete/payment can fail.

Neither the AI log nor the supplied human-only telemetry provides the missing AI payment ledger. Consequently, Pavol’s P1 condition—loss per 1,000 **paid** firing E from the first blind salvo—is not currently satisfied.

**Concrete fix:** Withhold zero-candidate precision weapons at firing selection, or enforce an explicit ≤10% allowance. Leave unused reservations unspent for defense; this does not require implementing the deferred reservation-release scheme. Log successful firing/payment by weapon, class and target, including current candidate count and actual E paid.

**Judgment on the NOT-built item:** **Release-blocking under the adopted gate**, not merely a follow-up.

---

### F8 — HIGH: Reordering does not enforce per-class hit quotas

**Locations:** `AI/Patches.cs:359–380`; native selection at `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:129–168`.

The prefix places quota-sized primary-hit subsets first, but native selection subsequently fills the **combined** `Hits` count from the remainder. It can transfer missing guided hit slots to unguided weapons, or vice versa.

For example, with ten guided and ten unguided weapons, quotas can be eight and seven. If the guided weapons have only secondary trajectories while all ten unguided weapons have primary trajectories, native selection can take **ten unguided primary hits**, then five guided secondary hits. The unguided quota is exceeded by three.

`RefreshFiringList` runs the same selection again, so it does not repair this. In addition, `hit-quota` logs only the first invocation each turn—often while trajectories are still being found—not the final selected class counts.

**Concrete fix:** Enforce separate class limits across primary and secondary selection. Do not fill one class’s deficit from another class. Log final selected hits/misses per class immediately before hold/abort handling, and distinguish those selections from actual paid shots.

## 2. Medium/low findings and concrete fixes

| Severity | Finding, location and consequence | Concrete fix |
|---|---|---|
| **Medium** | **The credible-kill predicate is not based on allocated damage.** `AI/StrategyPatches.cs:183–187,251–256` averages damage across all guided weapons; `AI/Brain/Strategy.cs:196–214` then applies it to the committed target’s passing shots. A high-damage weapon unreachable to that target can inflate the estimate. Second-phase selection can also divert shots to weapons through the 25% weapon preference (`AI/Patches.cs:601–602`). | Sum expected passing population damage from the weapons actually available to that target. For a precision exception, allocate sufficient expected damage to a specific currently visible city before optional weapon targeting. Log that allocation. |
| **Medium** | **The flexible-slot forecast is incomplete.** `AI/DirectorPatches.cs:1483–1508` compounds queued E/M project copies, but Jamming still uses live `res.ScienceProductionPerTurn` repeatedly. Later plant/upgrade options also use live global multipliers (`:1310–1312`, `:1646–1647`), not queued global modifiers. Installed-project expiry is not modeled over the investment horizon. | Track projected science and queued global multipliers consistently across option types; incorporate own installed project durations into the common-horizon forecast. |
| **Medium** | **Save/load loses policy provenance and cumulative state.** `AI/EconomyPatches.cs:516–521,609–615` treats every loaded head as unowned. A legitimate observed-threat counter head can be replaced before current observations repopulate `States`. `AI/DirectorPatches.cs:148–158,208–212` also resets war latching, intel spend/stop-loss/probes and volley peaks. | Persist own policy state/provenance, or reconstruct explicitly supported parts before applying the guard. Preserve/revalidate a loaded observed-threat counter head. Do not present post-load spend as whole-match cumulative spend. |
| **Medium** | **Intel is not all-in priced, and upgrade/building counts are conflated.** `AI/DirectorPatches.cs:670` checks a fixed 200 E even when Lidar costs 50 E; `:871` increments `IntelThisTurn` for an upgrade, which subsequently counts toward the building ceiling and logged building total. Recurring upkeep is neither explicitly priced nor separately logged. At the building cap, native minimum skips Lidar because `:681` requires `!nativeRoom`. | Price the actual candidate, separate building and upgrade counters, permit useful upgrades for minimum intel, and include recurring upkeep in valuation and diagnostics. Capture actual energy credits rather than assuming post-boundary production equals the credited amount. |
| **Medium** | **Blind upgrades omit planned weapon effects.** `AI/DirectorPatches.cs:1847–1851` updates upkeep but not `PlannedWeaponEffects`; generic upgrade planning reads that ledger at `:1911–1915`. Also, new weapons are bought before the blind package (`:754–775`), potentially consuming its upkeep room. | Record the same `UpgradeEffect` bookkeeping as the generic path. Rank/fund the blind package before marginal plain-weapon purchases, or price the intended package in that purchase decision. |
| **Medium** | **“Working defense” cost/upgrade selection includes unpowered defenses.** `AI/DirectorPatches.cs:1088–1097` averages all Normal defense-building costs without checking power or counter type. `:1808–1814` likewise upgrades unpowered defenses. The forward request is therefore not specifically the live roll capacity of relevant in-range Kinetics. | Filter operational defenses and make the cost/overlap assumption explicit for observed incoming weapon types. Log relevant powered counts, assumed roll cost, eligible share and overlap allowance. Enhanced’s listed defenses have no material activation cost, so I did not find a material-reserve blocker for this ruleset. |
| **Low** | **“One terminal result” is only “at most one.”** `AI/DirectorPatches.cs:2351–2355` deduplicates emitted results, but native turn-end cancellation clears outstanding searches without a terminal result: `decompiled/Interplanetary.AI.Construction/AIBuildingPlacementFinder.cs:31–35`. | Emit `cancelled`/`timeout` outcomes for outstanding search IDs before clearing them. |

There is also an adoption gap in research: Robotic Mining remains an unconditional plan entry at `AI/Plugin.cs:176`. Moving it late does not implement “fund only when an installable yield-upgrade path is selected.” Add that installability predicate or explicitly defer this requirement.

## 3. Correctness and interaction checks that did pass

### No demonstrated new null-reference crash in the named hooks

I found no reproducible new null-reference/collection-modification failure in normal initialized Enhanced gameplay for `DetermineShootingPrefix`, `TryIntelUpgrade`, `TryDefenseUpgrade`, `TryBlindArtilleryUpgrade`, `CountProjects` or `LiveRollCost`.

Relevant checks:

- `CurrentProjects` deliberately contains null entries; `CountProjects` handles them.
- `GetAvailableBuildingUpgrades` returns an empty list for an unknown owner, not null: `decompiled/Interplanetary.GameDataFiles/UpgradesData.cs:51–58`.
- The reflected targeting fields exist in the decompiled class.
- Reordering `m_weapons` does not invalidate the native targeting iterator: trajectory discovery iterates the separate targetable-weapon collection, and native shooting selection subsequently makes a copy.

These are static checks, not a live-hook validation.

### Permanent projects are not double-counted across ordinary consecutive turns

`DirectorState` is replaced at `AI/DirectorPatches.cs:287–290`. Queued purchases increment this turn’s counters; next turn starts fresh and counts installed projects.

The native construction queue is cleared at turn end: `decompiled/Interplanetary.AI/AIConstructionQueue.cs:139–143`.

The real defect is **same-turn committed capacity**, F3—not installed projects being added to last turn’s retained counter.

### Hold and abort ordering remains coherent

- Hold clears firing lists and returns before refresh.
- Otherwise refresh rebuilds lists.
- Abort then clears the rebuilt lists.

The new reordering does not itself undo a hold or abort. Its problem is class-quota enforcement, F8.

### Strategy calling `WarTurnFor` before the director is safe within an uninterrupted match

Both see the same own technology/income state, and a qualifying first call latches war before the director reads it. The return value can differ nominally on the first late qualifying call—e.g. strategy gets the configured T16 threshold while the director gets the actual latched T18 start—but both classify the current turn as war.

Save/load continuity is the outstanding problem.

### Triple Barrel does not immediately increase the purchase-turn firing bill

Native weapon upgrades apply their weapon-stat effects during next-turn `FinishUpgrade`:

- `decompiled/Interplanetary.Prefabs/BuildingUpgrades.cs:183–191`
- `decompiled/Interplanetary.Prefabs/BuildingUpgradeStats.cs:71–73,99–107`.

Therefore, **lack of an immediate extra current-turn firing reservation is not itself a funding leak**. The valid issue is inconsistent projected-effect bookkeeping, listed above.

### Config migration matches the requested defaults

The seven unchanged-default migrations at `AI/Plugin.cs:633–643` match the supplied implementation description. The new plan also puts Plasma immediately after Maglev and keeps Ultracapacitors/Composite Heat Cells after EMC, Nano, Microwave, Lossless and Plasma.

## 4. Fairness and council adherence

### Fairness assessment

- **`VisibleEnemyCities`: acceptable output.** Only current-Visible, Normal cities contribute.
- **`VisibleCityPops`: acceptable output.** Population is added only after current-visibility filtering.
- **Enumerating a native list is not by itself the leak.** The decisive question is whether hidden entries influence the resulting decision.
- **`BlindEnemies` / `EnemyView.VisibleCities`: fails P2**, as described in F2.
- **Research counter selection:** uses remembered observed enemy weapons rather than a newly added hidden counterintel/stealth threshold.
- The research guard’s native urgent-intel query is logged rather than used to jump the plan. It does not establish that all downstream native visibility predicates satisfy the council’s stronger boundary.
- **Shadow targeting remains a definite boundary violation**, F1.

### By adopted item

| Item | Assessment |
|---|---|
| **1. Flexible-slot engine** | **Partial.** Opening cap and full-vs-utility E/M separation are implemented; queued E/M project compounding is improved. Native committed slots, science compounding, expiry forecasting and required per-city diagnostics are incomplete. |
| **2. Research/guard** | **Mostly implemented for uninterrupted fresh matches.** Economy-first ordering and native-head replacement are present. Loaded counter-head provenance, complete head-change reasons and conditional Robotic Mining remain gaps. |
| **3. E:M balance** | **Implemented as a trial.** Defaults/migration match. Required alternative-option/rejection logging is absent. |
| **4. Offense** | **Partial.** Precision branch and ordinary live-cost purchase accounting exist. Nuclear/target-specific prohibition, strict-visible firing, class quotas, allocated credible-kill damage and paid-energy measurement remain deficient. |
| **5. Intel** | **Partial.** The bid/stop-loss mechanism exists, but independent native paths bypass it; all-in pricing and complete P2 removal are unfinished. |
| **6. Defense** | **Partial.** Upgrades, forward request and deduplication exist. Repair protection still has the wrong evidence/order, allocation-shortfall diagnostics are missing, and executable cover-site reservation was omitted. |

### Judgment on omitted Ion-site reservation

**Release-blocking against the approved “build all six” scope.**

The signed requirement was to reserve feasible cover sites before ordinary construction blocks them, not merely to move the prior from T24 to T18. The motivating playtest explicitly found built-over rings and almost universally unsuccessful placement searches.

An earlier prior may help, but there is no evidence here that it removes that failure mode. Implement feasible site/power-connector protection, or obtain an explicit scope change accepting this as a follow-up. The T20 Ion-cover outcome remains an experiment; the prescribed implementation is presently missing.

## 5. Can the next-match gates be computed?

Several existing fields are useful, but the log is not sufficient for the full agreed evaluation.

| Gate | Current observability / missing fields |
|---|---|
| **Per-city installed, pending, unlocked, free and reserved slots** | **No.** `projectSlots` lacks city IDs, pending/reserved counts and project identities/durations. It is captured at turn start, while aggregate project counters are subsequently incremented. |
| **Zero permanent purchases into reserved capacity** | **No.** Need a purchase event containing city ID and installed/committed/queued/unlocked/reserved/free capacity immediately before and after acceptance. |
| **Three simultaneous Jammings within two turns of EMC** | **Partial.** `jamming` mixes installed and newly queued purchases. Need EMC completion turn and installed active project IDs/durations, plus pending separately. “Useful economy science remaining” is not distinguished from all remaining plan science. |
| **Three simultaneous SSTARs by T20** | **Partial.** Same installed-versus-queued ambiguity. |
| **15,000 E production at T20; T18 stretch** | **Yes for the logged production value:** `economy.energyPerTurn`. Capture actual credit separately: native resource credit precedes project expiry processing, so post-boundary production can differ from energy actually credited. |
| **EMC head by T10** | **Yes**, using `economy.researchHead`. |
| **EMC completed by T14; Plasma by T18** | **Not reliably.** Head transitions can reflect preemption; purchases prove some unlocks only when they occur. Add completion events or completed-tech snapshots/progress. |
| **No unexplained off-plan head lasting >1 turn** | **Partial.** Need full queue/head transition records with old/new head, ownership and reason—including prerequisites, counter continuation and load reconstruction. |
| **T22 material stock ≤2× round-start E production** | **Partial.** `director.materialLeft` is a decision budget, not necessarily full actual stock: savings/firing reservations and failed actions complicate reconciliation. Log actual post-construction stock and separate retained allocations. |
| **Zero extraction-only glut purchases except logged recovery need** | **Mostly inspectable** from action IDs, ruleset and `materialGlut`. Recovery exceptions lack a reason field. |
| **Top affordable economy alternatives and rejection reasons** | **No.** Add ranked candidates, costs/gains, budget snapshot and rejection reason. |
| **No below-saturation hold when S3 is true** | **Partial.** `precision` permits a check against the implementation’s own boolean, not independent recomputation. Missing allocated expected city damage, guided funding budget/result and selected visible city. |
| **No Missile queued at zero candidates** | **Partial.** Global `visibleCities` catches some cases. Need target ID and current legal candidate count on every ordinary/superweapon purchase. |
| **≤10% blind paid E on zero-candidate precision weapons** | **No.** Missing successful firing/payment ledger, class, target and candidate count. |
| **Zero funding overruns** | **No complete reconciliation.** Need planned versus actual spend/payment outcomes and failed actions, with reservation/allocation changes. |
| **Per-class planned hits within one weapon of quotas** | **Quota arithmetic is inspectable; actual assignment is not.** Add final class-specific selected hits/misses after refresh. Current `hit-quota` can be an early partial-search snapshot. |
| **Intel construction/upgrades ≤10% cumulative credits through T20; upkeep separately** | **Not faithfully from current counters.** Preferred purchases are unrecorded, minimum purchases can overspend, credits can differ at expiry, and separate upkeep is absent. |
| **Stop-loss after two failed batches; ≤2 further arrays through T25** | **Partial.** Queued arrays can be counted against a logged stop verdict, but batch baselines/outcomes and native bypasses are not represented adequately. Add explicit batch IDs, start/evaluation outcomes, stop transitions and purchase reasons. |
| **No forbidden hidden runtime inputs** | **Code-audit gate, not provable from the present logs.** Currently fails F1/F2. |
| **Ten upgraded Kinetics by T22; twenty powered/range-upgraded by T24 stretch** | **No reliable distinct-building count.** Upgrade totals do not identify which defenses received which upgrades or whether they are powered. Log defense ID, installed upgrades and operational state. |
| **First ≥60-impact volley: ≥50% intercepted, zero unpaid** | **Largely yes**, from `lastImpacts`, `lastIntercepted`, `lastUnpaidRolls`; include `lastNoEligibleDefense` as the post-sign-off note requires. Add explicit enemy-only phase counts/source turn and a terminal phase record if the AI dies before another turn. |
| **Every defense shortfall has an allocation reason** | **No.** Need requested/allocated reserve, live roll cost, eligible/overlap assumptions and reductions attributed to firing, repairs, recovery, affordability and superweapon allocation. |
| **Minimum operational city Ion cover ≥1 by T20** | **Yes**, using `minCityCover`, with the caveat that it is a turn-start snapshot. |
| **Survive first qualifying volley with ≤1 city lost** | **Partial.** Consecutive `ownCityPop` records help; an eliminated AI may never emit the next record. Add phase-end/death state. |
| **Persistent enemy losses; P1 loss per 1,000 paid firing E from first blind salvo** | **Incomplete.** Human snapshots can establish some persistent inventory losses, but supplied damage events contain nominal amounts without target/shooter identity, and AI paid firing E is missing. Define persistent-loss measurement explicitly and join it to the actual paid salvo ledger. |

### Required verification after fixes

The current test project includes only `Brain/*.cs` (`AI/Tests/BrainTests.csproj:12–14`). Its green result would not validate the adapter defects above.

Before release, add meaningful checks for:

- A project transferred from queue to native committed actions still consuming reserve capacity.
- Fresh incoming defense evidence and planned allocation protected before repairs.
- Every intel purchase path obeying one policy/accounting boundary.
- Class quotas with asymmetric primary/secondary trajectory availability.
- Shadow-only targets and blind Nuclear/ordinary purchase paths.
- Save/load preservation of counter-research provenance and own cumulative policy state.

The motivating T21–T22 precision replay also remains **unverified**: the added T21 test uses illustrative city populations, not a demonstrated replay of the actual visible-city populations.

**verdict: DO NOT SHIP.**
