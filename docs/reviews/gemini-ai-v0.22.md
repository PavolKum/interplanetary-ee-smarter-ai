verdict: ship with fixes

---

### Executive Summary

The v0.22 change correctly identifies the core issue from Match 29 (land plants queued for unpowered offshore mines because the native search anchored land plants to unpowered buildings at sea). However, the implementation has several critical defects in placement state handling, power grid registration, director queue logic, and gate verification:

1. **Dirty Connection Leak in Placement Fallback (Blocker)**: [`tryPlaceBuildingNextToExistingBuilding`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs#L216-L250) sets `m_result.PowerGridConnection` on candidate sampling *before* placement validity is checked. If [`WaterLinkStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3104-L3116) fails for 60 steps and falls through to [`ShelterStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2823-L2937) (which sets `connect = false` for water plants) or random water placement, `PowerGridConnection` is not cleared. The randomly placed shoreline plant will attempt to connect to the offshore mine across the planet, consuming its slot and failing the line distance check.
2. **`TryRegisterBuildingPowered` is a No-Op for Unpowered Buildings (Major)**: The game's [`AIPowerGridMaintaining.TryRegisterBuildingPowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI/AIPowerGridMaintaining.cs#L93-L104) requires `m_availablePowerConnections.Contains(building)`. Cut-off offshore mines do not distribute power and are never in that list. Consequently, the anchor is never removed from `m_buildingsRequiringPower`. If the mine has more than one slot, [`CountUnpowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3084-L3099) still considers it unpowered on subsequent checks, causing multiple plants to queue and connect to the same mine.
3. **`Abandon` and `WaterCooldownUntil` Lockout (Major)**: [`Abandon`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3248-L3250) only decrements `PlantsForUnpowered` for `bui_SolarPlant` and `bui_NuclearPlant`. If a floating plant placement fails, `PlantsForUnpowered` is not refunded and `WaterCooldownUntil` locks out water plants for 3 turns. For the next 3 turns, [`Step`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L780-L792) falls back to `PickLandPlant`, forcing land plants to queue for offshore mines and directly re-triggering the stranded-at-sea give-ups v0.22 was created to stop.
4. **Gate I9b Flaws (Major)**: In [`verify_gates.py`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/tools/verify_gates.py#L141-L151), `rate` normalizes against all AI-turns (`len(v22)`) rather than turns with water-only unpowered opportunities (`len(opportunity)`), allowing long matches to pass despite high failure rates. In addition, economy-queued land plants that strand at sea are counted against I9b even when Step 2 acted correctly.

---

### Priority Questions Analysis

#### A. Does `WaterLinkStep` actually power the offshore mine?
* **Placement & Connection Mechanics**: When [`WaterLinkStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3104-L3116) invokes `tryPlaceBuildingNextToExistingBuilding(unpowered)` on a step where placement succeeds, `m_result.PowerGridConnection` is set to `baseBuilding.Power`. When the turn ends and actions execute, [`PlayerBuildingHandler.PlaceBuilding`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs#L260-L266) calls [`CreatePowerLineConnection`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs#L24-L99), which successfully links the new floating plant to the offshore mine. Because the floating plant generates power, the mine receives power.
* **Ring Radius**: `tryPlaceBuildingNextToExistingBuilding` calculates inner radius $r_{inner} = r_{plant} + r_{mine}$ and outer radius $r_{outer} = (r_{plant} + r_{mine} + \max(range_{plant}, range_{mine})) \times 0.9$. The candidate distance is sampled uniformly in $[r_{inner}, r_{outer}]$. Because $r_{outer}$ is strictly 90% of the maximum connector limit, the candidate is guaranteed to be within the distance limit enforced by [`PlayerPowerGridHandler.CreatePowerLineConnection`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs#L67-L73).
* **Terrain Acceptance**: `bui_FloatingPlant` has `buildable_on_water = true` and `buildable_on_land = false`. Candidate rays hitting the water mask (`pixel.r <= 0f && pixel.b > 0f`) around an offshore mine in open water pass [`internalTryPlaceBuilding`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs#L840-L851) if fallout-free and clear of collisions.
* **Power Registration & Double-Registering**: `tryPlaceBuildingNextToExistingBuilding` attempts to register the anchor via `m_core.PowerGrid.TryRegisterBuildingPowered(baseBuilding)`. However, decompiled [`AIPowerGridMaintaining.TryRegisterBuildingPowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI/AIPowerGridMaintaining.cs#L93-L104) requires `m_availablePowerConnections.Contains(building)`. That list only contains buildings where `Power.IsPotentiallyDistributingPower` was true at turn start. An unpowered, isolated offshore mine does not distribute power and is never in `m_availablePowerConnections`. Therefore, `TryRegisterBuildingPowered` does not remove it from `m_buildingsRequiringPower`. If the mine has 2 slots, [`CountUnpowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3084-L3099) continues to see `IsBuildingRequiringPower(mine) == true` and `HasBuildingAvailableSlots(mine) == true`, leading to multiple plants connecting to the same mine.
* **Connecting with No Free Slots**: If two placement searches are active in the same turn before the first finishes, both can select the same 1-slot anchor. The second placement will decrement `m_freePowerSlotsByBuilding` to negative in [`AIActions.ConstructBuilding`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI/AIActions.cs#L84), and [`PlayerPowerGridHandler.CreatePowerLineConnection`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs#L63) will abort at line 63 (`AvailablePowerSlotCount <= 0`), leaving the second plant unpowered and floating unconnected.

#### B. Interactions: Abandon, Cooldown, Fallback, and Growth Plan
* **`PlantsForUnpowered` in `Abandon`**: [`DirectorPatches.Abandon`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3248) only decrements `d.PlantsForUnpowered` for `bui_SolarPlant` and `bui_NuclearPlant`. If a `bui_FloatingPlant` is abandoned, `PlantsForUnpowered` remains incremented, blocking the director from retrying this turn.
* **`WaterCooldownUntil` and `PickWaterPlant`**: When a floating plant placement gives up, line 3249 sets `d.WaterCooldownUntil = GameManager.CurrentTurn + 3`. For 3 turns, [`PickWaterPlant`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L1990-L1998) returns `null`. Line 784 (`PickWaterPlant(core, d) ?? PickLandPlant(core, d)`) then falls back to a land plant. For the next 3 turns, the AI queues land plants whose only anchors are at sea, causing repeated give-ups.
* **`ShelterStep` Fallback**: If [`WaterLinkStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3104-L3116) fails for 60 steps, execution falls through to [`ShelterStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2823-L2937). For water plants, `ShelterStep` computes `connect = false` ([line 2854](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2854)), intending to place the plant unconnected on the shoreline under defense cover. But because `tryPlaceBuildingNextToExistingBuilding` already set `m_result.PowerGridConnection` during `WaterLinkStep` without clearing it, the shoreline plant incorrectly attempts a power line to the offshore mine.
* **Slot Starvation of Growth Plan**: Free slots on floating plants at sea are counted in [`CountFreeSlots`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L746-L757) and populate `d.SlotsLeft`. The economy director sees available slots and attempts to queue land-only buildings (railguns, telescopes, land mines). When these land buildings run placement searches, they find no land anchors with free slots, widen to sea anchors, and fail placement. Furthermore, if [`economy.NeedSlotForGoal`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L976) is set for a superweapon (which is land-only) while an offshore mine is unpowered, `waterFirst` queues a floating plant at sea, producing a slot that the superweapon cannot use.

#### C. `CountUnpowered` Performance and Correctness
* **Performance**: [`CountUnpowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3084-L3099) iterates over all buildings in `core.PlayerData.BuildingHandler.Buildings`. The filter `!core.PowerGrid.IsBuildingRequiringPower(b) || !core.PowerGrid.HasBuildingAvailableSlots(b)` quickly rejects powered or full buildings. For the few matching buildings, [`AnchorOnWater`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3068-L3080) immediately returns `true` for `IsWaterOnly(b.BuildingData)` (e.g. offshore mines) without running texture lookups. However, calling `CountUnpowered` on every step of [`WaterLinkStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3104-L3116) allocates a `new List<BaseBuilding>()` each step.
* **Correctness**:
  * `Ruins` and `Destroyed` states are correctly skipped via `b.CurrentState`.
  * Buildings `UnderConstruction` have `!b.Power.HasPower` and are counted if they have available slots.
  * If unpowered buildings exist but have 0 available slots, `CountUnpowered` returns `unpoweredLand == 0, unpoweredWater == 0`, but `d.Unpowered` remains positive (`core.PowerGrid.BuildingsRequiringPowerAmount > 0`). This causes Step 2 to queue land plants indefinitely because `AnchorMedium.WaterPlantFirst(0, 0)` is false.

#### D. Gate I9b Analysis
* **Can it pass when it should fail?** Yes:
  1. *Denominator dilution*: `rate = len(stranded) / len(v22)` divides by total match AI-turns rather than `len(opportunity)`. In a 60-turn match, 6 stranded land plants yield a rate of $6 / 60 = 0.10 \le 0.12$, passing the gate even if every single water opportunity failed.
  2. *Searches cut at turn end*: Searches cut by `StopAllSearchesPrefix` have `outcome == "open"`. Gate I9b strictly requires `outcome == "gave-up"`, ignoring stranded searches terminated by turn boundaries.
  3. *Anchor pools with one crowded land anchor*: If an anchor pool has 1 crowded land anchor and 10 offshore mines, `stat.NoSameMedium` is never incremented because `onWater.Contains(false)` is true. The placement fails after widening, but `noSameMedium >= 0.9 * filtered` evaluates to false, excluding it from `stranded`.
* **Can it fail on a correct log?** Yes:
  1. *Short games*: In matches with $\le 8$ AI-turns, a single stranded plant results in $rate \ge 0.125 > 0.12$, causing an immediate failure.
  2. *Mixed turns*: If a turn starts with 1 land and 3 water unpowered buildings, `opportunity` is 0 at turn start. If Step 2 queues a plant that strands at sea during that turn, `opportunity == 0` but `stranded > 0`, causing Gate I9b to evaluate and fail on "0 AI-turns with only water unpowered".
  3. *Economy-queued plants*: Step 4 (Economy) can queue a `bui_SolarPlant` when slots or energy are needed. If land mine slots are full, its anchor pool falls back to offshore mines, where it gives up and falsely trips Gate I9b.

---

### Numbered Findings

#### 1. [Blocker] Dirty `PowerGridConnection` leak on `WaterLinkStep` fallback
* **File & Line**: [AI/DirectorPatches.cs:2984-2994](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2984-L2994), [AI/DirectorPatches.cs:3104-3116](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3104-L3116), [decompiled/AIBuildingPlacementSearch.cs:216-248](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs#L216-L248)
* **Failure Scenario**: `tryPlaceBuildingNextToExistingBuilding` unconditionally sets `m_result.PowerGridConnection = baseBuilding.Power` on line 217 before checking candidate validity on line 243. When candidate placement fails on that step, `PowerGridConnection` remains set. After 60 failed steps, [`WaterLinkStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3104-L3116) returns `false` and execution drops into [`ShelterStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2823-L2937). For water plants, `ShelterStep` sets `connect = false` ([line 2854](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2854)) and does not touch `result.PowerGridConnection`. When `ShelterStep` places the plant on the shoreline, `PowerGridConnection` still references the distant offshore mine. [`AIActions.ConstructBuilding`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI/AIActions.cs#L84) consumes the mine's power slot, and at turn resolution, [`CreatePowerLineConnection`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs#L67-L73) rejects the connection because it exceeds maximum distance.
* **Suggested Fix**: Clear `m_result.PowerGridConnection` whenever a placement search step does not complete, or explicitly clear `PowerGridConnection` in `WaterLinkStep` before returning:
```csharp
private static bool WaterLinkStep(AIBuildingPlacementSearch search, AICore core)
{
    PlaceSearch stat = StatOf(search);
    if (stat != null && stat.WaterLinkSteps >= MediumWidenAfter) return false;
    List<BaseBuilding> unpowered = new List<BaseBuilding>();
    int land, water;
    CountUnpowered(core, out land, out water, unpowered);
    if (unpowered.Count == 0) return false;
    if (stat != null) stat.WaterLinkSteps++;
    s_searchSteps.SetValue(search, (int)s_searchSteps.GetValue(search) + 1);
    s_placeNextTo.Invoke(search, new object[] { unpowered });
    if (!search.IsComplete)
    {
        AIBuildingPlacementResult res = (AIBuildingPlacementResult)s_searchResult.GetValue(search);
        if (res != null) res.PowerGridConnection = null;
    }
    return true;
}
```

#### 2. [Major] `TryRegisterBuildingPowered` is a no-op for unpowered buildings
* **File & Line**: [AI/DirectorPatches.cs:3084-3099](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3084-L3099), [decompiled/AIPowerGridMaintaining.cs:93-104](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI/AIPowerGridMaintaining.cs#L93-L104)
* **Failure Scenario**: Decompiled [`TryRegisterBuildingPowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/decompiled/Interplanetary.AI/AIPowerGridMaintaining.cs#L93-L104) requires `if (m_availablePowerConnections.Contains(building))`. Unpowered offshore mines do not distribute power, so they are never in `m_availablePowerConnections`. The method exits without modifying `m_buildingsRequiringPower`. Consequently, `core.PowerGrid.IsBuildingRequiringPower(mine)` remains `true`. If the mine has 2 slots, [`CountUnpowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3084-L3099) continues to count it as unpowered, causing redundant floating plants to queue and connect to the same mine.
* **Suggested Fix**: Track queued plant connections locally in `DirectorState` or directly remove the building from `m_buildingsRequiringPower` via reflection when a plant successfully links to it in `WaterLinkStep` / `ConstructBuilding`.

#### 3. [Major] `Abandon` does not refund floating plants and triggers 3-turn water lockout
* **File & Line**: [AI/DirectorPatches.cs:3248-3249](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3248-L3249), [AI/DirectorPatches.cs:780-785](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L780-L785)
* **Failure Scenario**: Line 3248 checks `if ((id == "bui_SolarPlant" || id == "bui_NuclearPlant") && d.PlantsForUnpowered > 0) d.PlantsForUnpowered--;`. When `bui_FloatingPlant` is abandoned, `PlantsForUnpowered` is not decremented. Line 3249 then sets `d.WaterCooldownUntil = GameManager.CurrentTurn + 3`. For the next 3 turns, [`PickWaterPlant`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L1990-L1998) returns `null`, causing line 784 (`PickWaterPlant(core, d) ?? PickLandPlant(core, d)`) to fall back to `PickLandPlant` despite `waterFirst == true`. The AI spends the next 3 turns queueing land plants for offshore mines, causing repeated placement failures.
* **Suggested Fix**: Include `bui_FloatingPlant` in the `PlantsForUnpowered` decrement in `Abandon`. Furthermore, do not fall back to `PickLandPlant` when `waterFirst` is true:
```csharp
// In Abandon:
if ((id == "bui_SolarPlant" || id == "bui_NuclearPlant" || id == "bui_FloatingPlant") && d.PlantsForUnpowered > 0) d.PlantsForUnpowered--;

// In Step 2:
BuildingData plant = waterFirst ? PickWaterPlant(core, d) : (PickLandPlant(core, d) ?? PickWaterPlant(core, d));
```

#### 4. [Major] `waterFirst` queues water plants for land-only superweapon slot goals
* **File & Line**: [AI/DirectorPatches.cs:776-785](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L776-L785)
* **Failure Scenario**: Step 2 fires if `(economy.NeedSlotForGoal && d.PlantsForUnpowered == 0)`. If an offshore mine is unpowered at the same time, `waterFirst` is true, causing Step 2 to queue a `bui_FloatingPlant`. The superweapon (such as `wea_NuclearMissile`) is land-only and cannot connect to a floating plant at sea. On subsequent turns, the superweapon remains blocked for lack of a land slot.
* **Suggested Fix**: Disallow `waterFirst` when the plant is queued primarily for `economy.NeedSlotForGoal`:
```csharp
bool forUnpowered = d.Unpowered > 0 && d.PlantsForUnpowered < Math.Min(d.Unpowered, Plugin.UnpoweredPlantsPerTurn.Value);
bool waterFirst = forUnpowered && Plugin.WaterPlantForWater.Value && AnchorMedium.WaterPlantFirst(unpoweredLand, unpoweredWater);
```

#### 5. [Major] Gate I9b denominator dilution and opportunity misclassification
* **File & Line**: [AI/tools/verify_gates.py:141-151](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/tools/verify_gates.py#L141-L151)
* **Failure Scenario**: Line 146 computes `rate = len(stranded) / len(v22)`. `v22` includes all director records carrying `unpoweredWater`. In a 60-turn match with only 3 turns of water opportunities, 6 stranded land plants produce $rate = 6 / 60 = 0.10 \le 0.12$ (PASS), masking a 100% failure rate. Additionally, line 143 requires `unpoweredLand == 0`. Turns starting with both land and water unpowered are excluded from `opportunity`, even if the land building was powered on step 1 and a land plant subsequently stranded at sea on step 2, leading to false failures reported on "0 AI-turns with only water unpowered".
* **Suggested Fix**: Normalize `rate` against `opportunity` rather than `v22`, or define `opportunity` per placement attempt. Also include searches terminated with `outcome == "open"` that were stranded at sea.

#### 6. [Minor] Step 2 loops on land plants when unpowered buildings have 0 slots
* **File & Line**: [AI/DirectorPatches.cs:776-785](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L776-L785), [AI/DirectorPatches.cs:3084-3099](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3084-L3099)
* **Failure Scenario**: `d.Unpowered` is initialized to `core.PowerGrid.BuildingsRequiringPowerAmount`, which counts all unpowered buildings regardless of available slots. [`CountUnpowered`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3084-L3099) filters out buildings with 0 available slots. If all unpowered buildings have 0 slots, `d.Unpowered > 0` remains true while `unpoweredLand == 0, unpoweredWater == 0`. Because `AnchorMedium.WaterPlantFirst(0, 0)` is false, Step 2 queues a land plant every turn that cannot anchor to anything.
* **Suggested Fix**: Base `needPlant` for unpowered buildings on `(unpoweredLand + unpoweredWater > 0)` rather than `d.Unpowered > 0`.
