I’m tracing the new budgets and intel snapshots through the game’s actual turn order; the main question is whether the apparent reserves and visibility gates behave as intended in the live engine.
Two material-budget problems are emerging: the bank can indefinitely suppress all spending when no eligible superweapon exists, and the refinery is credited as production even after the planet is exhausted. I’m checking their exact triggers and the targeting-energy interaction before finalizing severity.
## Verdict: **fix first**

**Confidence: high** in the code-path findings; medium in their match impact without a playtest. The most consequential issues are a material bank that can freeze a depleted AI indefinitely, refinery purchases credited with nonexistent income, and a targeting change whose test does not exercise the focus-fire behavior it claims to fix.

## Findings

1. **High — The endgame bank can permanently immobilize an AI.**  
   `AI/EconomyPatches.cs:161-170` banks up to 6,000 whenever the planet is depleted and `Goal == null`. The goal can remain null when the desired weapon is unavailable, at its construction limit, disabled by settings, or research cannot reach it (`:184-205`). If remaining stock is ≤6,000, the director gets **zero material every turn**, even for survival or research-supporting construction. A tiny or empty starting planet can trigger this immediately. Stock is preserved in the real player resource holder, but there is no release path until an eligible goal appears; `PlayerResourceHandler.cs:161-184` confirms no replenishment on an empty planet.  
   **Fix:** make the bank conditional on a reachable, still-needed goal, with an explicit release/degradation policy and a minimum operating budget. Test depletion, tiny starts, disabled research and weapon limits.

2. **High — Refinery “income” is imaginary, including after depletion.**  
   `AI/DirectorPatches.cs:604-607` assigns a refinery a positive `MaterialGain` of roughly 43% of mine output; `:510-513` then adds it to forecast *per-turn income*. But the refinery reduces drain, not production, and an empty planet produces no material (`PlayerResourceHandler.cs:161-184`). Thus the director can buy refineries on a dead planet and distort subsequent energy/material balancing. Even before depletion, savings are bounded by the engine’s **max of drain and production**, so reduced drain may save less than claimed or nothing at all.  
   **Fix:** value saved finite reserve separately from actual per-turn production, using the engine’s max rule and remaining horizon; never count the refinery as income.

3. **High — Blind “spread fire” still concentrates on one opponent.**  
   The new test checks score ratios only (`AI/Tests/Program.cs:218-230`). `TargetPlanner.Assign` still sends 70% of weapons to the single top score (`AI/Brain/TargetPlanner.cs:95-104`). Any grudge makes its holder the top target even if the score advantage is small; equal scores still select one ordered target. The claimed fix therefore does not establish the behavior needed to break the observed vendetta loop. Native targeting had selected random enemies (`decompiled/Interplanetary.AI/AIWeaponTargeting.cs:111-115`).  
   **Fix:** make focus depend on evidence strength or score separation, and test actual assignments across blind, tied and modest-grudge cases.

4. **Medium — Defense reserve can mask the superweapon energy-push path.**  
   `EconomyPatches.cs:119-132` sets `BuyPending` using pre-defense-reserve energy. `DirectorPatches.cs:221-229,327-342` then withholds energy and checks affordability *before* the firing-upkeep check that sets `EnergyPush`. If the reserve makes the weapon unaffordable, the purchase fails without reserving its material or signaling an energy shortfall; the director can spend that material elsewhere. The weapon sink correctly uses the reduced budget, and `WarSpendShare` uses the recomputed start budget (`:296-320`). Native targeting checks and spends **actual player resources**, not the director budget (`AIActions.cs:130-142`; `PlayerWeaponHandler.cs:230-260`).  
   **Fix:** reconcile the purchase after the reserve is calculated; preserve goal material and explicitly request power or adjust the reserve when blocked.

5. **Medium — The refinery slot policy does not enforce the stated design.**  
   `DirectorPatches.cs:575-611` reserves a slot only when the refinery is *currently researched*. Before then both slots may fill, and `:559-560` prevents retrofitting. Once a refinery is installed, `hasEfficiency` disables the Yield2 exclusion, so Yield2 can be chosen instead of Yield1. The same logic applies to Offshore Mines. Two-slot behavior is confirmed by `decompiled/Interplanetary.Prefabs/BuildingUpgrades.cs:11-32,105-115`; the enhanced unlocks differ between land and offshore (`docs/ruleset/embedded/enhanced/TechTreeData.csv:18-23`).  
   **Fix:** define the intended pair and slot reservation explicitly, including unavailable technology and already-full buildings; otherwise document the opportunistic behavior.

6. **Medium — Intel caps are not global caps.**  
   The normal blind-enemy branch respects `MaxIntelBuildings`, but native `BelowMinimum` bypasses it (`DirectorPatches.cs:270-279`), and the preferred-intel branch bypasses both the cap and `IntelPerTurn` (`:309-314`). Blindness is sampled once per turn, so two arrays may be queued even when one would suffice next round. Thirty arrays mean **600 energy/turn upkeep**, subject to power (`BuildingsData.csv:22,25`; `BuildingStatHolder.cs:135`). Placement failure marks the type failed, but the logged queued count is not decremented (`DirectorPatches.cs:893-927`).  
   **Fix:** enforce one shared cap in every branch, account for powered and pending arrays, and log successful versus abandoned construction separately.

## Rule verdicts

- **R1 — TRUE, with a city-count caveat.** All players’ resource values update and snapshots freeze before AI turns (`PlayerManager.cs:664-675`; `GameManager.cs:368-373`). The overlay uses frozen observer intel and the specified thresholds (`IntelOverlayIntelThermometerComponent.cs:313-373`); this is fair for an AI seat. The mod’s live city count includes any state except ruins/destroyed, whereas the overlay counts **Normal** cities only (`AI/Patches.cs:151-158`; `PlayerBuildingHandler.cs:435-445`). Use `GetRemainingCities()`. Null snapshots produce unknowns, not a crash; null handlers/players are not guarded.
- **R2 — TRUE, when powered.** Array +8 and tech unlock are in `BuildingsData.csv:22` and `TechTreeData.csv:9`; powered building stats feed intel (`PlayerResourceHandler.cs:55,278-305`), then `FrozenIntelState.cs:49-52`.
- **R3 — TRUE in the ordinary budgeted path, qualified.** Construction spends within the reduced allotment; defense reads actual remaining resources (`AIConstructionQueue.cs:132-150`; `PlayerDefenseHandler.cs:203-237`). Weapons **do** spend actual energy, but `AIResources.cs:38-60` first deducts their estimated firing costs from the construction budget. The reservation is therefore conditional on that estimate and action ordering, not an isolated resource account.
- **R4 — TRUE.** Reducing AI budgets does not subtract player stock; actual expenditures do (`AIResources.cs:38-60`; `PlayerResourceHandler.cs:107-129`).
- **R5 — TRUE, with final-turn clipping.** Drain is `max(cost, production)`, capped at remaining planet material; `-1` prevents depletion (`PlayerResourceHandler.cs:161-184`; `PlayerResourceHolder.cs:120-123`).
- **R6 — TRUE for the enhanced ruleset.** Both refinery entries are 0.7 with no yield multiplier; no other mine/offshore upgrade has that combination (`UpgradesData.csv:83,95`). Buildings have two slots.
- **R7 — TRUE for enhanced; FALSE for classic.** All 38 default IDs exist in enhanced; nine are absent from classic. `AddTechnologyToQueueById` finds and queues a prerequisite path (`PlayerTechnologyHandler.cs:480-503,707-736`). Classic’s missing IDs are skipped by the mod, with possible research delays (`EconomyPatches.cs:414-433`).

## Checked and found fine

- Intelligence `1` means enabled; the aggregate resource gates and unknown-economy fallback match that distinction. Growth-phase selection currently depends on turn, not the changed enemy-economy value (`EconomyPlan.cs:53-67`).
- The weapon picker now skips placement-failed IDs; the director also has a 2,000-step valve and the game has an AI timeout. These bound the inspected paths, though they do not prove every live placement path terminates.
- Banked material is available again when `Goal` becomes non-null; the problem is that this transition is not guaranteed.
- **Tests were not run:** the request prohibits file creation, and the normal .NET test invocation may write build artifacts. The new tests cover pure scoring/arithmetic, not the game adapters, resource-action ordering, intel caps, refinery slots or end-to-end bank release.
