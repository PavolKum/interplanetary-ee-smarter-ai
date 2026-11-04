I’m tracing the turn lifecycle and resource accounting against the decompiled game first; those determine whether the new triggers mean what they claim.
Two concrete risks are emerging: the slot-shortage exception can be overridden by the older energy-glut flag, and extraction is prioritized before emergency defense with no material or slot carve-out. I’m checking their precise triggers and the free-fire accounting against the match records.
## Review verdict

**SHIP AFTER FIXES:** protect urgent defense from extraction-first, make the slot-shortage exception effective, and correct the free-fire energy signal. The other findings are smaller but worth addressing before relying on the next playtest’s diagnosis.

**Confidence: high** in the code-level findings; **medium** in their effect on match outcomes. This was a read-only review: I did not run tests or observe v0.16.2 in-game.

## Findings

### High — Extraction-first can consume the last slots needed for emergency defense

**`AI/DirectorPatches.cs:719–729, 767–798, 1930–1944`**

The 35% limit protects material only; it does **not** protect power slots. For example, with four slots and 4,000 material, extraction can queue four 350-material Offshore Mines and leave no slot for an Ion defense, even when `NukeThreat` or `GuidedThreat` is active. The subsequent anti-nuke and defense steps require `SlotsLeft > 0`. It can also leave too little material for the desired defenses after earlier superweapon or power purchases.

**Fix:** Run urgent anti-nuke/counter-defense first, or reserve the slots and material needed for an affordable emergency-defense floor before Step 2b. Add an adapter-level scenario with four slots, a nuke threat, and affordable mines and Ion defenses.

### High — The slot-shortage exception does not override the existing glut flag

**`AI/DirectorPatches.cs:1342–1347`; `AI/Brain/Director.cs:94–104, 241–260`**

`EnergyGlut = d.EnergyGlut || ... || MaterialBoundGlut(...)`. When fewer than four slots remain, `MaterialBoundGlut` becomes false, but `d.EnergyGlut` can still be true. Then pure plants score zero and cannot be selected. `EnergySaturated` independently filters energy-only plants too. At zero slots, mines are also unavailable, so the economy can stall despite the intended exception. The dedicated unpowered-building/superweapon plant step does not cover the general “need slots for future buildings” case.

**Fix:** Give a genuinely slot-starved plant a positive slot value or a dedicated bounded plant step, bypassing both energy-glut weighting and saturation *for that purpose*. Test the combined conditions, not only `MaterialBoundGlut(true, 2, 4)`.

### High — Free fire counts spent power investment as unspent energy

**`AI/DirectorPatches.cs:272–279`; `AI/Brain/Strategy.cs:273–276`**

`UnspentEnergyLastTurn` adds `PowerEnergySpent` to actual `EndEnergyLeft`. That is intentional for the *glut classification*, but it does not prove firing energy was unused. If the AI spent 3,000 on plants or upgrades and ended with zero, the helper reports 3,000 “unspent” and authorizes a 3,000-energy salvo. That energy had a productive construction use. Prior-turn conditions are also only a forecast: current income, firing reservation, defense needs and repairs can change.

**Fix:** Use actual end-of-construction surplus for free fire, without adding power investment. Treat it as a conservative prior-turn signal, not proof that the current salvo is free; guard against a significant current income drop or increased defense demand. Test a power-purchase-only surplus and a sudden income/defense change.

### Medium — Material-bound carry-over ignores the new `Ended` flag

**`AI/DirectorPatches.cs:299–304, 368–370, 2749–2755`**

Free fire correctly requires `Started && Ended && Turn == turn - 1`. Material-bound carry-over checks only the turn number. If construction started but `EndTurn` was skipped, `TurnStartMaterial` can be positive while default `EndMaterialLeft` is zero; energy-only purchases can provide enough `PowerEnergySpent` to falsely trigger material-bound behavior.

**Fix:** Carry the energy and material snapshots only when the previous state both started **and ended**, and was consecutive. A fresh match/load reset correctly produces unknown state; test skipped `EndTurn` separately from a normal load.

### Medium — The extraction-first “war horizon” is actually turn 45

**`AI/DirectorPatches.cs:328–333, 723–725`; `AI/Plugin.cs:468, 673`**

The new priority stops at `SustainableMaterial`, calculated with `MaterialHorizonTurn = 45`, while the planet drain guard uses `PlanetGuardHorizonTurn = 36`. At T20, ignoring stock, the priority ceiling is approximately pool/25 rather than pool/16. Thus Step 2b can stop well before the approved T36 drain guard would bind; ordinary economy may still buy extraction later, but only after military spending. The playtest diagnosis specifically calls for extraction to the war horizon.

**Fix:** Calculate the priority’s ceiling from the intended T36 horizon, while retaining the separate authoritative planet-pool drain guard. Add a test where the T45 ceiling has been reached but the T36 guard still allows a mine.

### Medium — Placement refunds do not restore the extraction-first allowance

**`AI/DirectorPatches.cs:1373–1378, 2689–2712`**

A mine counts against `ExtractionFirstMaterial` when queued. If placement later fails, `Abandon` refunds resources and reopens construction but does not subtract that mine’s material from the extraction-first tally. The priority can therefore be exhausted by purchases that never landed. The `extraction-first` failure marker can also prevent reconsideration after reopening.

**Fix:** Track which queued mines used the early allowance, reverse their contribution on refund, and reconsider the step after reopening while retaining failed-type backoff. Test a failed early mine followed by a viable alternative.

## Answers to the remaining questions

- **Turn accounting:** Strategy runs before the new director state replaces the old one (`AI/EconomyPatches.cs:159–161, 229–230`). `EnergyUpkeep` is the native reservation for **all normal weapon buildings**, including potentially unpowered weapons—not necessarily the cost of the operational weapons that actually fire (`decompiled/Interplanetary.AI/AIResources.cs:38–62`). On a hold, that reservation is returned before construction; on fire it stays reserved. `EndEnergyLeft` is the AI construction snapshot after the defense reserve and repairs were deducted, before action-phase defense spending; it is not total player energy or actual end-of-action surplus (`AI/DirectorPatches.cs:629–672, 2749–2755`).
- **Extraction filter:** With the current rules, mines, Offshore Mines, yield upgrades and Rare Material qualify; refinery options flagged `SavesReserve` do not. The guard still applies. The six-attempt loop is bounded; failed option keys are filtered on later calls. No new unbounded loop found.
- **Stability:** The trigger has no hysteresis, so borderline energy/material outcomes can flip it between turns. Free fire also changes the next construction budget. Instrument and playtest this; a flip alone does not prove a defect.
- **Commit and fairness:** `IsHuman` uses the publicly identifiable player type. The new rule does not itself read hidden enemy values or city count. Reachability filters the estimated salvo, and two low-yield turns disable human-first; the existing abort remains in force. Low-yield memory can persist after switching targets, so recovery after orbital geometry changes deserves a test.
- **Return fire:** The change prevents the specific wrong-target response, but an attacker other than the commit no longer triggers return fire. It also does **not** retarget to that attacker. That is a deliberate trade-off against the playtest candidate “aim return fire at whoever hit us.”
- **Free-fire combat behavior:** Yes, `passing > 0` permits repeated low-yield salvos and bypasses the pulse. Even if construction energy would otherwise expire, shots can consume energy that might fund action-phase defenses, especially if today’s reserve is capped or needs rise. Max-hold, attrition and abort still operate; blind guided weapons are withheld later. The policy does not guarantee useful damage.
- **Coverage:** `V0162Rules` exercises pure predicates and target choices, but not the adapter lifecycle, combined glut/saturation/slot conditions, emergency-defense contention, refunds, or changed current-turn resources. The reported 12 mutants do not establish those integration cases. I found no new obvious crash or direct fairness leak in the reviewed paths.

**Scope note:** The saved diff omits the current change to `AI/tools/live_monitor.py`; I inspected that additional diff as well. Match 18 and 19 notes and selected raw match-19 director records were checked, but the new behavior remains **unverified in-game**.

**verdict: SHIP AFTER FIXES — emergency-defense priority, effective slot recovery, and actual-surplus free-fire accounting.**
