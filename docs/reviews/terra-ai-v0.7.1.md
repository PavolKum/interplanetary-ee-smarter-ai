I’m tracing the replacement through the game’s placement, queue and power-grid code, including what happens after a search succeeds or is abandoned.
# Verdict: **fix first**

**Confidence: high** in the placement-selection and fallback findings; **medium** in their frequency and performance impact. Reviewed code matches commit `b80cadc` for the affected placement files. Runtime behavior was not tested in this read-only review.

## High

### 1. The power-connection fix is bypassed on fallback

`AI/DirectorPatches.cs:1022–1025,1045–1048,1061–1064` returns control to native placement after 60 custom steps, no eligible anchors, or no uncovered value near an anchor. Native defense placement can again sample to the defense radius rather than connector reach (`decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs:218–247`). At execution, the game can place the building but reject its too-long connection (`decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs:63–72`).

This is particularly relevant when coverage is saturated: fallback is then expected behavior, not just an exceptional case. The original unpowered-defense issue is therefore **not fully fixed**.

**Fix:** Keep a connection-safe placement path on fallback, including when coverage gain is zero. Before accepting any connected result, validate its actual `BuildingWorldPosition` against the game’s world-space connector limit and available slot. If no connected placement is found, abandon/refund rather than silently reverting to an unsafe defense search.

### 2. The best-scoring candidate is not necessarily placeable

`AI/DirectorPatches.cs:1076–1091` scores 12 candidates after `IsBuildingPlacementValid`, but that method checks only overlap with **queued** buildings; it does not check existing-building collisions, terrain or fallout (`decompiled/Interplanetary.AI/AIConstruction.cs:354–382`). Those checks happen only in `TryPlaceBuilding` (`decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:839–850`).

Consequently, a high-value invalid spot wins over a lower-value legal spot. If the winner fails, the other 11 are discarded. Dense valuable areas can repeatedly dominate until the search falls back to native placement.

**Fix:** Run the full placement check on each candidate before ranking, retaining its independent successful result; select the highest-scoring successful result. Add a case where the highest-scoring candidate collides or is on invalid terrain and the next candidate is legal.

## Medium

### 3. The ring does not prove the eventual connection is in range

`AI/DirectorPatches.cs:1067–1072` reproduces the native non-defense ring formula, including `TransformPoint`, but the distance is an **anchor-local vector**, while the execution check compares final **world positions** (`PlayerPowerGridHandler.cs:67–72`). Transform scale and projection onto the planet mean the 0.9 factor is a margin, not a guarantee. The `outer = inner` clamp can also override the defense-radius cap.

The data’s `building_scale` changes the radius calculation (`BaseBuilding.cs:562–568`); that alone does not establish the runtime anchor transform’s world scale. Actual prefab/planet scales and terrain geometry are **unverified in game**.

**Fix:** Check the actual successful hit/result position against the exact world-space line limit, regardless of the sampling formula. Test non-unit anchor/planet transforms and terrain elevations.

### 4. A placement step can be expensive enough to stall a frame

Each of 12 calls to `PickAnchor` recomputes every anchor’s score against every target (`AI/DirectorPatches.cs:1058–1060`; `AI/Brain/Coverage.cs:86–96`). Each candidate also calls native queued-collision validation, which performs a raycast **per queued action** (`AIConstruction.cs:357–373`). A finder update can allocate about 100 steps across searches (`AIConstructionQueue.cs:132–136`; `AIBuildingPlacementFinder.cs:37–47`), with up to 60 custom steps per search.

The step cap and 20-second AI timeout bound progress between updates, but neither preempts a costly synchronous update (`AICore.cs:171–201`). Frame time and turn impact are **unmeasured**.

**Fix:** Compute anchor scores once per step, reuse the distribution for 12 draws, and budget candidate work across frames. Profile a crowded planet with many queued constructions before shipping.

## Low

### 5. Queued defense targets have the wrong weight

All queued buildings receive value `1` (`AI/DirectorPatches.cs:991–1001`), while existing defenses receive `0.25` (`:983–984`). Pending defenses consequently attract more coverage than the stated model intends.

**Fix:** Look up each queued action’s building data and apply the same defense weighting.

## Checked and fine

- **Success lifecycle:** Setting `IsComplete` inside `step()` causes `Update()` to call `Success()` (`AIBuildingPlacementSearch.cs:74–88`). The queue callback creates the action, reserves the anchor slot and registers projected power (`AIConstructionQueue.cs:153–159`; `AIActions.cs:82–93`). The omitted native registration at search lines 275–278 applies to a power-distributing building powering an existing anchor; ordinary defenses need power.
- **Try/refund:** `TryPlaceBuilding` mutates its result and creates a collision object, but does not place a building, spend resources, or add it to the octree. Actual placement occurs during action execution (`PlayerBuildingHandler.cs:804–852,193–267`). Abandoning an incomplete search refunds the queue’s decision budget (`AI/DirectorPatches.cs:1177–1219`); no successful `TryPlaceBuilding` object needs removal from the world.
- **Raycast:** Layer `256`, distance `1000`, and `hit.point` match `TryPlaceBuilding`’s `BuildingWorldPosition` calculation (`PlayerBuildingHandler.cs:817–821`).
- **Queue lifetime:** `ConstructedBuildings` contains successfully planned actions until `AIActions.EndTurn()` executes and clears them (`AIActions.cs:43–50,69–93`). It is suitable for same-turn projected coverage; it is not a persistent history of executed buildings.
