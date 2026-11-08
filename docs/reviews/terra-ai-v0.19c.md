I’ll trace the end-turn lifecycle and resource accounting against the decompiled game, then check the revised gates, firing bookkeeping, and fairness boundaries.
The relocated hook is reached before the final trajectory-input list is built, and the director gate closes the earlier timeout case. I’m checking two remaining subtleties: what the ledger means after an aborted or partial volley, and whether any later action can still consume the supposedly spare balance.
## Findings

### 1. **Major — The late take-back distorts next turn’s free-energy calculation**

**Locations:** `AI/StrategyPatches.cs:357–365`; `AI/DirectorPatches.cs:287–295, 649–652, 3069–3071`; `AI/Brain/Strategy.cs:302–310`.

`EnergyAtStart` is captured while this turn is still a **hold**, so it includes the released firing reservation. Late free fire then subtracts the full reservation from `core.Resources.Energy`, and the director records that reduced balance as `EndEnergyLeft`.

On the next turn, `FreeFireInputs` calculates `lastUses = EnergyAtStart - EndEnergyLeft`. It therefore treats the late salvo reservation as part of last turn’s **defense, repairs and construction use**. A normal firing turn starts with the reservation already excluded, so the two cases are not comparable.

**Failure scenario:** A held turn starts construction with 15,000 energy and finishes with 7,000 before a 3,000 late take-back. The director records 4,000 left and `lastUses = 11,000`. Next turn, with 12,000 available before firing, free energy is calculated as `min(4,000, 12,000 - 11,000) = 1,000`, so a 3,000 free-fire opportunity is rejected. Excluding the prior firing reservation from the construction-start comparison gives `lastUses = 8,000` and 4,000 free.

The abort case is worse: `ShouldAbort` clears the firing lists after the full take-back, but nothing restores it to the director ledger. A volley that fired **nothing** is still recorded as if its entire reservation was used. Partial volleys are similarly conservative: the reservation includes all normal weapons, including ones that may not fire.

**Fix:** Track the late reservation separately from construction uses. Restore it on abort; for a successful late volley, normalize the construction-start snapshot so firing is not counted as defense/construction demand. If the intent is to measure actual surplus rather than reserved surplus, reconcile partial and failed shots against actual payment after action execution. Add tests covering late fire, abort and partial fire, followed by the next turn’s `FreeFireInputs`.

### 2. **Minor — `Funded` can become true after the repair stage fails**

**Locations:** `AI/DirectorPatches.cs:690–694, 1138–1143, 1201–1204`; gate at `AI/StrategyPatches.cs:347`.

`RepairAfterReserve` catches its own exceptions and returns normally. `StartConstruction` then sets `d.Funded = true` regardless. The flag therefore proves that the repair method *returned*, not that repair budgeting and execution completed.

**Failure scenario:** With repairs enabled and damaged buildings present, an exception while collecting or processing repair candidates leaves some repairs unconsidered. The error is logged, but a later qualifying hold can flip to fire using the remaining balance even though the repair stage failed. The timeout-before-construction case from the previous review **is** closed; this is the error path inside the funding stage.

**Fix:** Have the repair stage return success/failure, treating disabled repairs and an empty candidate set as successful. Set `Funded` only on success. Preserve the current fail-closed behavior when the director never starts or fails before funding.

### 3. **Minor — A nonempty `fire` record does not prove the late volley was paid or fired**

**Locations:** `AI/Patches.cs:740–804`; `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:73–103`; `decompiled/Interplanetary.AI/AITargetWeapons.cs:82–100, 133–142`; `decompiled/Interplanetary.AI/AICore.cs:230–237`.

`ReportFired` and the `fire` record count the selected lists immediately after the goal builds its trajectory-input list. The actual end-turn trajectories, action queueing, and payment occur later. The end-turn phase has its own timeout; a planned trajectory can also lack a completed result or a shot can be refused at payment.

**Failure scenario:** A `late-free-fire` event is followed by a `fire` record with nonzero `firingHits`, but the end-turn trajectory work does not complete before the timeout. The record and `LastStrikeTurn` describe a planned volley, while no corresponding paid shot is established. This bookkeeping limitation predates this slice, but it matters to the proposed next-match validation.

**Fix:** For the playtest, join `late-free-fire` and `fire` with subsequent `fire-paid` records for the same AI and turn, and inspect any `abort` or `fire-refused` events. Longer term, base strike history on successfully queued or paid shots, or explicitly label it as planned-shot history. A lifecycle check should cover a successful paid volley, an abort, and an end-turn timeout.

## Requested interaction checks

- **Goal ordering and call count — confirmed with a qualification.** Native `RegisterPrimaryTargetTrajectory` can rebuild the *candidate firing lists during aiming* (`AITrajectoryWeapon.cs:174–183`), so the prefix is not reached before any such list exists. It **is** reached before the final trajectory-input list is built and before targeting actions are queued. The only decompiled call to the goal’s `TargetWeapons()` is `AIWeaponTargeting.EndTurn` at `AIWeaponTargeting.cs:181–184`; it checks `AreWeaponsTargeted`, which the goal sets before building the inputs (`AIWeaponTargetingGoal.cs:63–103`). `UpdateEndTurn` does not call the goal again. With no weapons, the goal starts already targeted and the late hook is skipped, appropriately for this feature.

- **Director and planned costs — normal path checked.** Construction precedes weapon targeting in the module list (`AICore.cs:103–110`). The director withholds the defense reserve and charges repairs before setting `Funded`; buildings are charged on queueing, and director upgrades and projects are explicitly charged (`AIConstructionQueue.cs:42–55`; `DirectorPatches.cs:1966–2023`). Intel and counterintel purchases go through construction. Power-line actions are connections, not energy purchases (`AIConnectPowerLineAction.cs:16–19`). The queue is cleared and placement searches stopped before the late check (`AIConstructionQueue.cs:139–143`). I found no later, uncharged planned purchase that the late take-back would displace on the normal director path.

- **Savings — conservative double subtraction.** `ReservedMaterial` is already removed from `core.Resources.Material` at turn start (`EconomyPatches.cs:185–203`), so subtracting it again at `StrategyPatches.cs:352–353` can suppress a valid late volley. The separate endgame bank is also already outside the ledger. A pending, unbought goal is not generally charged yet, so the additional goal-cost subtraction is a conservative protection. I found no missed savings subtraction in the normal path.

- **Abort and history.** The late decision now precedes list rebuilding, accuracy filtering and `ShouldAbort`. An abort clears both lists, so `ReportFired` increments `HoldTurns` and does not update `LastStrikeTurn`. The budget accounting after that abort remains wrong, as described in finding 1. On a nonempty plan, history follows the lists—not confirmed payment.

- **Original AIs and fairness.** The new goal call is behind `SmartGoal` / `Plugin.IsSmart` (`AI/Patches.cs:625–629, 81–87`). The late predicate uses the AI’s own decision and resource ledger; it grants no resources and adds no enemy-data lookup. Existing strike candidate selection accepts **Visible** buildings and rejects Shadow candidates (`AI/Patches.cs:903–924`). I found no new fairness or original-AI behavior regression in this slice.

- **Fixture.** The Doc T23 fixture now matches the reported 17,120 energy, 16 material, and nine primary trajectories toward `jogi` (`docs/playtests/data/v018m24-ai-decisions.jsonl:446–448`). The nonzero-material case is labelled synthetic.

## Verification

Read-only source, diff and match-record review. `git diff --check -- AI` passed, with Git line-ending warnings. I did **not** run the tests or independently verify the stated 620 checks and mutations; that would create build artifacts. **Unverified in game:** actual late-volley payment, timeout behavior, and the resulting next-turn budget.

**verdict: ship with fixes** — the previous lifecycle blocker and normal timeout funding gap are addressed. Fix the next-turn accounting in finding 1 before shipping; tighten the funding failure gate and use paid-shot evidence for the playtest.
