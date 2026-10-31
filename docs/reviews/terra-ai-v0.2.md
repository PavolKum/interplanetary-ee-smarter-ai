I’ll trace the current implementation against the decompiled game code, prioritizing turn stalls and regressions with lasers disabled. I’ll use the current five-category, turn-based planner as the source of truth.
The turn-start timing is sound, but the research hook appears to preempt native intel requests. I’m checking whether that and the construction fallbacks have concrete gameplay consequences before ranking them.
**Verdict: fix first.** The highest-value fix is the superweapon purchase’s no-free-power-slot path: it can turn a saved purchase into a burst of power-plant construction instead. **Confidence: high** in the static finding; in-game behavior remains **UNVERIFIED**.

1. **Major — A superweapon can trigger power-plant spending instead of being bought.**  
   **Evidence:** `AI/EconomyPatches.cs:176–182, 210–239`; `decompiled/Interplanetary.AI/AIConstructionModule.cs:167–185`; `AIConstruction.cs:128–176, 218–235, 441–469`.  
   **Scenario:** When the AI can afford its goal but has no free slot, the purchase raises `PowerSlotNeed` and returns success. Subsequent calls go through the native need branch, which queues power generators but does not reduce that need. The AI can keep building generators until it runs out of affordable options, without retrying the purchase that turn. This is an exposed native need-accounting weakness, but the new forced purchase triggers it.  
   **Fix:** Account for power slots as generators are queued or placed, and reserve enough for the goal while resolving the slot; verify the purchase is actually placed. For tonight, disabling `SuperweaponSavings` is the simpler mitigation.

2. **Major — The research plan starves native intel research.**  
   **Evidence:** `AI/EconomyPatches.cs:324–352`; `decompiled/Interplanetary.AI/AICore.cs:103–110, 162–168`; `AITechQueue.cs:17–27, 42–52`; `AIShortTermStrategy.cs:40–74, 128–135, 223–230`.  
   **Scenario:** The plan fills an empty queue before short-term strategy runs. Its urgent intel requests enter the native key-tech backlog but cannot start while the plan keeps replenishing the queue. The planner also gives no explicit preferred-intel category once minimum intel is met (`AI/EconomyPatches.cs:184–201`; native preference: `AIConstruction.cs:279–288`). Guided weapons can therefore lack useful visibility longer than native.  
   **Fix:** Give urgent intel requests precedence at queue boundaries, and preserve a preferred-intel construction path.

3. **Major — Failed category fallback is not equivalent to native behavior.**  
   **Evidence:** `AI/EconomyPatches.cs:188–201`; `decompiled/Interplanetary.AI/AIEnergyConstruction.cs:37–72`; `AIConstructionModule.cs:294–328`; `AIConstruction.cs:323–351`.  
   **Scenario:** A failed category can add resource needs and mark entities attempted. The prefix restores only the three needs, not the attempted lists, before running native roulette. Native can therefore lose both the shortage signal and candidates it otherwise would have tried, ending construction early.  
   **Fix:** Do not call a mutating category speculatively unless its state can be preserved; explicitly choose a fallback that retains the resulting needs and attempted-entity semantics.

4. **Major — Experimental lasers still have broken targeting paths; keep them off.**  
   **Evidence:** `AI/LaserPatches.cs:110–152`; `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:205–215`; `Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:363–366, 419–429`; `Interplanetary.Views/PhaseTwoTargetingLogic.cs:164`; `PlayerIntelHandler.cs:236–251`.  
   **Scenario:** The patch uses native laser trajectory checking, which starts from the *building* position; a Solar Laser fires from the Sun. It can reject valid shots or accept invalid ones. It also accepts `Shadow` targets through `IsBuildingVisible` and reads their live positions, violating the visible-only rule.  
   **Fix:** Calculate from `WeaponShootingPosition`, and target only currently `Visible` buildings. Validate normal and Solar Lasers in a throwaway match.

5. **Major — Enabling lasers bypasses the offline-only contract.**  
   **Evidence:** `AI/Plugin.cs:144–145, 169, 243–247`; `AI/LaserPatches.cs:39–70, 80–81, 110–111, 164–169`.  
   **Scenario:** When enabled at startup, the laser transpilers and prefixes have no `Plugin.Active`/AI-player guard. They remain effective in online games or with the master switch disabled. Their installation also counts successful patch calls without requiring the expected transpiler replacement counts.  
   **Fix:** Gate laser behavior consistently, and fail installation if the expected IL matches are absent. **UNVERIFIED:** actual Harmony IL matches against the shipped assembly.

6. **Minor — Defense queue telemetry can claim a defense that was never queued.**  
   **Evidence:** `AI/EconomyPatches.cs:271–300`; `decompiled/Interplanetary.AI/AIConstructionModule.cs:167–185`.  
   **Scenario:** With no free slot, `tryBuildAffordableBuilding` raises a power need instead of queueing a defense, yet `DefensesQueued` is incremented and influences later choices.  
   **Fix:** Increment only after observing a queue addition; treat a raised power need separately.

**Other checks**

- **Savings timing and bounds:** `AICore.BeginTurn` runs resources and all module `BeginTurn`s before the postfix; the queue has already copied its allotment (`AICore.cs:162–168`; `AIConstruction.cs:441–446`; `AIConstructionQueue.cs:132–137`). `NextReserve` cannot subtract more than positive stock (`AI/Brain/EconomyPlan.cs:12–18`); native usable resources can already be negative after firing-cost reservation (`AIResources.cs:38–64`).
- **Goal lookup and dispatch:** The firepower evaluation is rebuilt at turn start and includes unlocked base-building entities (`AIConstructionEvaluation.cs:49–72, 104–110`; `AIConstructionEvaluationEntity.cs:104–118, 246–291`). Calling the virtual base method on the weapon module dispatches its `constructBuilding` override, which increments weapon count (`AIWeaponConstruction.cs:341–345`). Runtime reflection is **UNVERIFIED**.
- **Termination:** I found no unbounded *new purchase retry*: it is capped at four, and the no-slot case enters the need branch. The broader construction path can run until its resources/candidates are exhausted; `AICore` also has a 20-second timeout after humans finish (`AICore.cs:171–201`). This is not proof that every runtime edge case terminates promptly.
- **Research API:** `AddTechnologyToQueueById(..., true)` replaces the queue and adds prerequisites; the head-availability check is appropriate (`PlayerTechnologyHandler.cs:256–296, 480–527, 707–766`). All **38** default IDs are present in `docs/ruleset/TechTreeData.csv:2`.
- **Smart defense:** The reflected core field and generic defense cost functions match native (`AIConstructionModule.cs:10–14, 33–49, 100–115`; `AIDefenseConstruction.cs:21–53`). Actual placement and effectiveness are **UNVERIFIED**.
- **Lasers:** The empty-list and wrong-index fixes address the identified source defects; bypassing projectile simulation matches the laser firing branch (`AITargetWeapons.cs:38–50, 82–100, 114–125`). End-to-end firing, targeting-data initialization and transpiler installation are **UNVERIFIED**.

Static review only; no files changed and no game or build was run.
