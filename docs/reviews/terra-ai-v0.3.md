I’m tracing the director against the game’s actual queue, placement and end-turn execution paths first; those are where a seemingly successful decision can still stall or mis-spend.
The placement cap does terminate searches, but I’ve found a separate power-slot mismatch around generator placement that can turn an apparently valid queued building into an unpowered one. I’m checking the concrete ruleset and execution order before ranking it.
## Verdict: **Fix first** — confidence: **high**

The director’s basic queue accounting is mostly sound, but the placement and fallback paths can waste whole turns or leave paid buildings unpowered. The superweapon path can also defeat the firing-upkeep cap. I would fix findings 1–4 before the next match.

This is a source review only; **in-game behavior and Harmony hook installation are UNVERIFIED**. I did not run tests or modify files.

## Findings

1. **High — Floating Plants consume slots the director still considers free.**  
   **Evidence:** `AI/DirectorPatches.cs:164–189, 607–619, 699–711`; native placement reserves the chosen anchor in `decompiled/Interplanetary.AI/AIActions.cs:69–89`.  
   **Scenario:** A Floating Plant can attach to a water anchor and consume its last slot, but `SlotsLeft` is decremented only for non-generators. A later mine, weapon or defense can therefore be queued against a nonexistent slot. Native search falls back to random placement when no eligible anchor remains (`AIBuildingPlacementSearch.cs:109–115`), so it can place a non-generator without a power connection. Offshore searches have the same fallback when their water anchors disappear (`DirectorPatches.cs:705`).  
   **Fix:** Reserve actual anchor slots for *all* searches, including generators; require a valid connection for non-generators rather than allowing random placement.

2. **High — Random Floating Plant placement can retain a stale connection.**  
   **Evidence:** The patch alternates anchored and random attempts (`AI/DirectorPatches.cs:707–715`). Native anchored attempts set `m_result.PowerGridConnection` *before* placement succeeds; random attempts do not clear it (`AIBuildingPlacementSearch.cs:167–191, 216–217`).  
   **Scenario:** An anchored attempt fails, then a random attempt succeeds far away. The result still carries the old anchor; the AI reserves its slot, and execution attempts a line that the game rejects for distance or lack of slots (`PlayerPowerGridHandler.cs:63–72`). The plant generates independently, but the phantom reservation aggravates finding 1 and wastes capacity.  
   **Fix:** Clear the connection before every random attempt; test alternating failed anchored and successful random attempts.

3. **High — A failed placement can refund too late to be spent.**  
   **Evidence:** `AI/DirectorPatches.cs:727–766`; `AIConstruction.cs:477–490`; `AIConstructionQueue.cs:123–129`.  
   **Scenario:** The director returns false and construction is marked ready while searches continue. If a search later reaches its cap, the refund increases the decision budget, but `tryConstructNewBuilding` is never called again that turn. The AI silently loses that opportunity to build; `EnergyIncome`/`MaterialIncome`, `Bought`, and action counters can also still reflect the failed purchase. This is particularly costly for a failed superweapon.  
   **Fix:** Either defer construction readiness until searches resolve, or explicitly reopen decision-making on a refund; reverse projected gains and purchase state as well.

4. **High — Superweapons bypass the firing-upkeep cap.**  
   **Evidence:** Ordinary weapons are checked and added to `d.Upkeep` at `AI/DirectorPatches.cs:230–237, 356–397`; the earlier superweapon purchase at `:261–279` does neither. Nuclear Missile and Asteroid Diversion each cost **700 energy to fire** (`docs/ruleset/WeaponsData.csv:7`). Native `AIResources.BeginTurn` reserves actual firing costs from available energy (`AIResources.cs:38–62`).  
   **Scenario:** A purchase can push next turn’s upkeep well above the 40% cap, potentially making usable energy negative and freezing investment—the failure mode the playtest identified. Subsequent ordinary weapon choices also ignore the pending superweapon’s upkeep.  
   **Fix:** Include its effective firing cost in the cap and planned upkeep, or explicitly reserve a safe post-purchase energy margin.

5. **High — Intel’s native building path reintroduces dropped upgrades and bad accounting.**  
   **Evidence:** `AI/DirectorPatches.cs:297–322` delegates to `AIIntelConstruction.cs:48–58`; native evaluation creates building variants with upgrades (`AIConstructionEvaluationEntity.cs:119–126, 157–162`). The queue charges those upgrades, with a material-cost arithmetic bug (`AIConstructionQueue.cs:42–55`), but actual construction passes two null upgrades (`AIConstructBuildingAction.cs:27–34`). The cap refund returns only base building costs (`DirectorPatches.cs:751–764`).  
   **Scenario:** An intel building variant can consume phantom budget for upgrades never installed; an abandoned search leaves some budget unrefunded. A queue head can also cost more against `AllottedMaterial` than was deducted from the decision budget, leaving subsequent queued purchases unprocessed.  
   **Fix:** Queue a plain intel building and buy upgrades separately using the director’s accounting, or fully reconcile native variant costs and refunds.

6. **Medium — Project scores omit real recurring costs and misstate some gains.**  
   **Evidence:** `AI/DirectorPatches.cs:527–542`; `BuildingProjectStats.cs:63–75`; `BuildingStatHolder.cs:135–137`. In the shipped rules, Robotic Ocean Prospecting gives 200 material but costs 100 energy per turn; Home Planet Hour gives 200 energy but costs 100 material (`ProjectsData.csv:191, 214–215`).  
   **Scenario:** These timed projects are scored as pure gains, so the AI can choose a materially worse project and overestimate its future budget. Flat additions also need the applicable global modifier; missing duration means permanent in `BuildingProjects.cs:222–227`, but the director scores it as zero duration.  
   **Fix:** Score net changes, global modifiers, actual city effects and duration semantics; retain the horizon discount as a deliberate heuristic.

7. **Medium — Damage-triggered defense can build nothing; counterintel is eliminated.**  
   **Evidence:** Desired defenses include recent damage (`AI/DirectorPatches.cs:107–112`), but `TryDefense` only builds when `DefensePlanner.Choose` finds a *visible counterable weapon* (`DirectorPatches.cs:327–351`; `AI/Brain/DefensePlan.cs:23–46`). The director has no counterintel construction branch (`DirectorPatches.cs:192–256`), whereas native gives counterintel weight 1.25 and defense 0.7 (`AIConstruction.cs:265–270, 335–345`).  
   **Scenario:** An AI damaged by unseen or unsupported weapons requests defenses but never places one; it also never builds Data Security Hubs through the director. Whether this is worse in the next match is **UNVERIFIED**, but both capabilities are removed relative to native.  
   **Fix:** Provide a bounded fallback defense and a counterintel policy; test against hidden threats.

8. **Medium — The 400-call valve is not a complete termination guarantee.**  
   **Evidence:** `AI/DirectorPatches.cs:140–161`; native construction loops at `AIConstruction.cs:477–490`. On an exception, the prefix returns true to run native, including after the counter increment; exceptions before the valve can recur every frame. Native’s need branch can report repeated success (`AIConstruction.cs:224–235`).  
   **Scenario:** A reflection or adapter failure can bypass the director’s valve and return to the native power-plant loop. Conversely, 400 legitimate queued purchases are cut off even if each made progress. The game has a separate 20-second timeout once humans finish (`AICore.cs:193–200`).  
   **Fix:** On adapter failure, disable the director for that AI/turn with a separately bounded fallback; count **no-progress** calls rather than all purchases.

9. **Medium — Slot and upgrade forecasts are optimistic or conservative in edge cases.**  
   **Evidence:** Slot counting uses the same free-slot dictionary and `IsPotentiallyDistributingPower` as placement (`DirectorPatches.cs:178–189`; `AIBuildingPlacementSearch.cs:119–126`), but scheduled power lines are actions, not live connections yet (`AIActions.cs:59–66`). `HasPower` excludes potential power (`BuildingPower.cs:71–115`), while resource registration uses `HasPotentialPower` (`PlayerResourceHandler.cs:240–252`).  
   **Scenario:** The slot snapshot can undercount capacity that scheduled connections will make available; the upgrade filter can skip a building with potential power even though the resource system includes it. The claim that every `!HasPower` building produces nothing is too broad. Planned upgrades also are not included in the next option’s multiplier, so a second same-turn upgrade’s gain is understated.  
   **Fix:** Distinguish actual, potential and scheduled power, and include planned upgrades in forecasts.

## Checked and found broadly sound

- **Normal decision-budget accounting:** Director-queued buildings are charged by the queue; separately queued upgrades/projects are charged by the director. Actual player resources are charged later by `PlaceBuilding`, `TryInstallUpgrade`, and `TryInstallProject`: these are separate ledgers, not double charges. Existing weapon firing costs are reserved initially. Intel variants are the exception above.
- **Progress:** On the normal director path, building/project success corresponds to a queue addition; `TryIntel` checks queue count. Its zero-cost upgrade edge can enqueue but return false (`DirectorPatches.cs:299–322`), although default upgrade costs are positive.
- **Projects and upgrades:** Reflection targets the correct protected project method; the city checks match its slot/duplicate checks for normal cities. Director-owned upgrade plans enforce two slots and duplicate IDs. The shipped yield multipliers multiply as assumed, apart from rounding and planned-upgrade forecasting (`NyaStat.cs:350–368`).
- **Placement cap:** Completed searches are excluded and removed searches cannot be refunded twice by this loop. `AIResourceConstruction` is **not instantiated by the decompiled `AICore.Initialize`**, so its first-Solar callback is not on the observed core path; other runtime wiring is **UNVERIFIED**.
- **Research migration:** It replaces only the exact old default string. Lasers default off. **No claim of match performance or live hook compatibility without a playtest.**
