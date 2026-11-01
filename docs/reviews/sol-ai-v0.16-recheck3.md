The overload lookup is now explicit, and repairs run after the current-turn reserve and hold-fire release. I’m checking the remaining edge cases: whether repairs can exceed the queue allotment and whether guard exceptions stay limited to AI guided shots.
The repair accounting is consistent, but moving repairs later introduces a superweapon-savings regression: repairs can consume a previously affordable goal’s funding after `BuyPending` is set. If material then falls short, the purchase fails without restoring the savings reserve, leaving that material available for ordinary construction. The guard catch also checks only AI ownership, so its fail-closed scope is broader than the requested “AI guided shots only.”
# v0.16 round-3 re-check — `1a6276b`

**Both round-2 blockers are resolved.** Round 3 introduces a superweapon-savings regression, however, and the exception handler does not strictly limit fail-closed behavior to AI guided shots.

**Confidence: High on source-level conclusions.** In-game execution remains unverified. All inspection stayed inside this repository; no files were edited, created or deleted.

## 1. Explicit overload registration — RESOLVED

`AI/Plugin.cs:665–667` supplies exactly:

```text
WeaponGameplayData, float, float, TrajectoryCalculationResults
```

`TryPatch` passes those types into `AccessTools.Method(type, method, args)` at `:689–694`.

That uniquely selects the trajectory overload returning `PlayerWeaponTargetingResult` (`decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:230`), excluding the `CelestialBody` overload returning `bool` (`:186`). The prefix/postfix result types and shared integer `__state` consequently match the selected method.

Actual Harmony installation still needs the in-game **“16 of 16 hooks patched”** check.

## 2. Repairs after the current-turn reserve — RESOLVED

### Native and patched order

1. Native `AICore.BeginTurn` calls `Resources.BeginTurn` before each module’s `BeginTurn` (`decompiled/Interplanetary.AI/AICore.cs:162–168`).
2. `AIResources.BeginTurn` snapshots player stock and subtracts firing costs for every Normal weapon; this is a planning reservation, not payment (`decompiled/Interplanetary.AI/AIResources.cs:38–64`).
3. Construction initializes its queue allotment from that snapshot (`decompiled/Interplanetary.AI/AIConstruction.cs:441–446`; `AIConstructionQueue.cs:132–137`).
4. The mod’s core postfix rolls the ledger, refreshes the enemy picture, runs strategy, handles savings, and creates this turn’s director state (`AI/EconomyPatches.cs:94–230`). A hold-fire decision releases firing reservations into **both** `core.Resources` and the queue allotment (`AI/StrategyPatches.cs:280–282,313–320`).
5. Construction subsequently waits for the power grid, then calls `tryConstructNewBuilding` **before** queue processing (`decompiled/Interplanetary.AI/AIConstruction.cs:477–486`).
6. The replacement calls `StartConstruction` before `Step`, hence before its first construction purchase (`AI/DirectorPatches.cs:556–578`). `StartConstruction` allocates the current planned/observed/forward defense request, including the Ion floor, before invoking repairs (`:616–635`).

The stale parallel `RepairDefenseHold` calculation is gone. Repairs cannot reduce the defense allocation already made from this turn’s inputs.

### Accounting and queue safety

Each repair spends player stock once and subtracts the same energy/material from both planning balances (`AI/DirectorPatches.cs:1111–1120`). Native `RepairBuilding` restores HP and records/notifies the repair; **it does not charge resources** (`decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:589–599`). No repair is queued for a second payment.

At this first-construction boundary, the queue and core balances remain aligned: initialization, hold-fire release, savings and defense reservations adjust both consistently. No construction item has yet consumed allotment. Repair selection is bounded by the available snapshot, material share and recovery floor (`AI/DirectorPatches.cs:1103–1112`; `AI/Brain/Repair.cs:31–38`). Therefore affordable repairs do not overspend the queue. `d.Started` prevents another repair pass when construction resumes.

## 3. Refusal path — authoritative, but exception scope is incomplete

**Authoritative refusal: RESOLVED.** Once refusal is selected, `__state = -2`; result bookkeeping and decision logging have separate catches, and control returns `false` (`AI/Patches.cs:523–548`). Their failures no longer restore native payment. The postfix ignores the negative sentinel (`:554`).

On ordinary refusal, clearing `IsFirstPhaseTargeted` matches native refusal. Any previously queued second-phase action encounters that false flag and returns without completing targeting (`decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:302–306`).

**Human isolation: RESOLVED.** Human handlers return at `AI/Patches.cs:508`, before the energy snapshot or targeting-result mutation, leaving `__state = -1`; the postfix exits.

**“Guard-error fails closed only for AI guided shots”: NOT fully met.** The protected block reads `me.ResourceHandler.Energy` at `:509` **before** testing `BlindArtillery` and `IsGuided` at `:510`. Its catch checks only AI ownership (`:520–521`). An exception before the applicability check therefore refuses an AI shot without establishing that it is guided or that the option is enabled.

This is an exceptional-state scope defect, not a demonstrated normal-game failure. **Required correction:** isolate payment-ledger snapshot errors from guard failures, and make guard-error refusal conditional on established guard applicability.

## 4. New round-3 regression: repairs invalidate pending superweapon funding

**Medium — required fix.**

Superweapon affordability and `BuyPending` are decided **before repairs**, with no material reserved when the purchase is pending (`AI/EconomyPatches.cs:171–184`). Repairs now run afterward and can consume that funding.

If repairs leave material below the goal cost, `TryBuySuperweapon` simply returns `false` (`AI/DirectorPatches.cs:839–840`). `Step` marks the superweapon attempt failed and continues ordinary construction (`:663–667`). The savings calculation is not rerun, so the remaining previously saved material becomes available to other purchases.

An illustrative, default-share-compatible case:

- Stock: **10,000 E / 6,000 M**; goal: **4,000 E / 6,000 M**; previously saved material: **5,000 M**.
- Defense reserves **4,000 E**.
- One eligible **100 E / 1,000 M** repair fits both repair budgets.
- Remaining construction balances: **5,900 E / 5,000 M**.
- The goal fails its material check, leaving **5,000 M** available for other purchases.

Before round 3, that repair happened before affordability assessment; `Savings.NextReserve` would retain the existing **5,000 M** (`AI/Brain/EconomyPlan.cs:12–18`).

**Required correction:** preserve pending-goal funding across repairs, or reconcile affordability and savings after repairs before ordinary construction proceeds.

No new hidden-information access or resource bonus was found. Repair callback exceptions occur after all three debits, so they do not reopen an uncharged construction budget.

## Verification

`git diff --check 46c28e9..1a6276b -- AI` passed. An in-memory arithmetic walkthrough confirmed the accounting equality and funding counterexample. Builds, mutation tests and gameplay were not run under the read-only constraint.

**verdict: SHIP AFTER FIXES — preserve/reconcile pending superweapon funding after repairs; restrict guard-error refusal to established AI-guided applicability.**
