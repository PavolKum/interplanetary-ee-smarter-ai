verdict: ship with fixes

### Summary & Assessment by Priority Questions

#### A. Correctness (Search Lifecycle & Accounting)
1. **Can any search be logged twice?** No. [`LogPlaceSearch`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3125-L3140) immediately removes the search from `s_placeSearches` via `s_placeSearches.Remove(search)`. Any subsequent attempt to log the same `AIBuildingPlacementSearch` instance returns early on `!s_placeSearches.TryGetValue`. Because search instances are created fresh via `new` in `AIBuildingPlacementFinder.StartSearch`, instances are uniquely referenced. In [`StopAllSearchesPrefix`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3110-L3123), already placed or abandoned searches are already absent from `s_placeSearches` and will not log a second record.
2. **Can any search be never logged?** Under normal game flow, every started search terminates via:
   - Placement success: `AIBuildingPlacementSearch.Success` -> [`SearchSuccessPostfix`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3104-L3107) (`"placed"`).
   - Step cap abandon: [`FinderUpdatePrefix`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3144-L3167) (`"gave-up"`).
   - Turn end: `AIBuildingPlacementFinder.StopAllSearches` -> [`StopAllSearchesPrefix`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3110-L3123) (`"open"`).
   There are no other removal paths from `m_ongoingSearches` in the decompiled game code or mod hooks (`m_ongoingSearches` is private to `AIBuildingPlacementFinder` and only modified in `StartSearch`, `Update` upon completion, and `StopAllSearches`).
3. **Can any search be logged under the wrong turn or AI?** No. `Turn` and `Ai` are captured at search creation in [`StartSearchPostfix`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3086-L3101) directly from `GameManager.CurrentTurn` and `core.PlayerData.UserNickName`, matching the `DirectorState.PlaceSearchesStarted` counter incremented at that exact turn. Even if a search concludes during `StopAllSearches`, [`LogPlaceSearch`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3125-L3140) writes `p.Turn` and `p.Ai` from the cached `PlaceSearch` record.
4. **Does the `Success` postfix fire for custom completed prefixes?** Yes. `AIBuildingPlacementSearch.Update` executes a loop over `step()`. In the decompiled code, if `IsComplete` is true immediately following `step()`, `Update` unconditionally calls `Success()`. [`CoverageStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2454-L2466) and [`ShelterStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L2815-L2820) set `IsComplete = true` via reflection and return `true` (causing `SearchStepPrefix` to return `false`), so `Update` sees `IsComplete` and calls `Success()`. The water branch and [`LandStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3049-L3062) call the game's internal `tryPlaceToRandomPosition` or `tryPlaceBuildingNextToExistingBuilding`, which set `IsComplete = true` upon valid placement, triggering `Success()` when `step()` returns.

#### B. Behaviour & Power Connection Semantics
1. **Can `LandStep` or `SameMedium` make a placement fail that used to succeed?** Yes (see Finding 4). If an AI has one cramped land anchor with no legal uncollided spots left on its connector ring and several coastal offshore mines whose connector rings reach the beach, native placement would have sampled the offshore mines and succeeded on the shoreline. [`AnchorMedium.Preferred`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/Brain/Coverage.cs#L19-L33) strips all offshore mines whenever any land anchor exists, starving the coastal water anchors and forcing step-cap abandonment.
2. **Power-connection semantics:** Preserved. When a plant (`!m_needsPowerGrid`) is placed next to an anchor requiring power, [`LandStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3049-L3062) delegates to `tryPlaceBuildingNextToExistingBuilding`, which executes `PowerGrid.TryRegisterBuildingPowered` identically to native logic. If a land plant abandons at step cap, [`Abandon`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3169-L3234) decrements `d.PlantsForUnpowered`, correctly enabling the `PickWaterPlant` fallback to execute on subsequent director decisions.
3. **Land plants preferring land anchors when unpowered buildings are offshore mines:** Not an issue when all unpowered buildings are offshore mines. In decompiled `findBuildingsThatNeedPower`, when any building requires power, it returns *only* those unpowered buildings (the offshore mines). Because all anchors in the pool are water, [`AnchorMedium.Preferred`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/Brain/Coverage.cs#L19-L33) finds zero land anchors and falls back to keeping the entire pool (`same.Count > 0 ? same : all`), allowing coastal attempts as before.

#### C. Classification (Medium Matching)
1. **Is "water-only" (`buildable_on_water && !buildable_on_land`) the right test for an anchor being on water?** No (see Finding 1). It tests building type schema definitions rather than the physical coordinates where the anchor was placed on the planet.
2. **Which buildings are buildable on both, and where can they stand?** Defenses (`bui_KineticDefense`, `bui_IonDefense`, `bui_LaserDefense`) have both `buildable_on_land` and `buildable_on_water` set to true. They can stand on land or out in open water. Under [`SameMedium`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3029-L3045), water-standing defenses are misclassified as land anchors.

#### D. Gate Script Logic
Gate `I1a every placement search terminal` in [verify_gates.py](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/tools/verify_gates.py#L340-L354) checks `placeSearchesStarted == per[(ai, t)]` only for turns that have a director record in `counted`. Any orphan `place-search` records on turns or AIs without a director record bypass validation entirely and can allow a defective log to pass (see Finding 2). In addition, if no director records contain `placeSearchesStarted`, or if director records drop the field intermittently, the gate can report a false pass (see Finding 3).

---

### Numbered Findings

#### 1. [Major] Water-standing defenses are misclassified as land anchors
- **File:Line:** [AI/DirectorPatches.cs:3033](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3033) and [AI/Brain/Coverage.cs:26-28](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/Brain/Coverage.cs#L26-L28)
- **Failure Scenario:** Defenses (`bui_KineticDefense`, `bui_IonDefense`, `bui_LaserDefense`) have both `buildable_on_land == true` and `buildable_on_water == true`. Because they distribute power once powered, they appear in power anchor pools. When an AI places an offshore defense in the sea to protect coastal assets, `IsWaterOnly(defense.BuildingData)` evaluates to `false` because `buildable_on_land` is true. In [`SameMedium`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3029-L3045), `waterOnly.Add(...)` appends `false` for this water-standing defense. [`AnchorMedium.Preferred`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/Brain/Coverage.cs#L19-L33) then treats the offshore defense as a *land* anchor. When a land-only building (Railgun, Telescope, Solar/Nuclear Plant) searches for a spot, this water-standing defense is kept in `kept`. Every placement attempt on the connector ring around it lands in the sea and fails.
- **Suggested Fix:** Check the anchor's physical position on the planet rather than its static `BuildingData` definition. Interplanetary's `PlayerBuildingHandler` exposes `public static bool IsOnWater(Vector3 localPoint, CelestialBody planet)`. In [`SameMedium`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3029-L3045), determine anchor medium using:
  ```csharp
  waterOnly.Add(b != null && PlayerBuildingHandler.IsOnWater(b.transform.localPosition, b.Owner.PlanetHandler.GetPlayerPlanet()));
  ```

#### 2. [Major] Gate I1a passes when orphan terminal records exist or when zero searches run
- **File:Line:** [AI/tools/verify_gates.py:347-350](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/tools/verify_gates.py#L347-L350)
- **Failure Scenario:**
  1. The gate computes `bad` by iterating over `r in counted` (turns with director records):
     ```python
     bad = ["%s T%d %d vs %d" % (r["ai"], r["t"], r["placeSearchesStarted"], per[(r["ai"], r["t"])])
            for r in counted if r["placeSearchesStarted"] != per[(r["ai"], r["t"])]]
     ```
     If there are extra `place-search` records emitted on turns lacking a director record (e.g. early turn initialization or turns where director throws), `per[(ai, t)]` contains entries that never appear in `counted`. Because `bad` only inspects keys in `counted`, these orphan records are ignored and the gate reports `PASS`.
  2. If a match runs where placement search hooks fail completely or no searches are initiated, `sum(r["placeSearchesStarted"]) == 0` and `len(places) == 0`. `bad` is empty and `dup` is 0, so the gate outputs `PASS` instead of `UNTESTED`.
- **Suggested Fix:** In [verify_gates.py](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/tools/verify_gates.py#L347-L350), verify total cardinality equality and ensure non-zero sample size:
  ```python
  total_started = sum(r["placeSearchesStarted"] for r in counted)
  cardinality_ok = (total_started == len(places)) and not bad and not dup
  if total_started == 0:
      gate(res, "I1a every placement search terminal", "UNTESTED", "no placement searches started in observed turns")
  else:
      gate(res, "I1a every placement search terminal", "PASS" if cardinality_ok else "FAIL",
           "%d searches over %d director records; mismatched AI-turns %d %s; duplicate ids %d" % (len(places), len(counted), len(bad), bad[:3], dup))
  ```

#### 3. [Minor] Gate I1a does not verify `placeSearchesStarted` exists across all director records
- **File:Line:** [AI/tools/verify_gates.py:340](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/tools/verify_gates.py#L340)
- **Failure Scenario:** `counted = [r for r in dirs if "placeSearchesStarted" in r]`. If a regression causes director records to omit `placeSearchesStarted` on most turns, `counted` only includes the turns where the field was emitted. If only 1 out of 50 director records contains the field, the gate evaluates only that single turn and reports `PASS`.
- **Suggested Fix:** Verify that `len(counted) == len(dirs)` before evaluating `placeSearchesStarted`, consistent with the check on lines 89-90:
  ```python
  if len(counted) != len(dirs):
      gate(res, "I1a every placement search terminal", "FAIL",
           "only %d/%d director records carry placeSearchesStarted" % (len(counted), len(dirs)))
  ```

#### 4. [Minor] Anchor filtering starves viable coastal water anchors when cramped land anchors exist
- **File:Line:** [AI/DirectorPatches.cs:3031-3045](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3031-L3045) and [AI/Brain/Coverage.cs:31](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/Brain/Coverage.cs#L31)
- **Failure Scenario:** When an anchor pool has 1 land anchor and 5 coastal offshore mines, `AnchorMedium.Preferred(true, false, pool)` returns only the 1 land anchor. If that land anchor's connector ring has zero valid uncollided spots (due to steep terrain, craters, or nearby structures), every candidate generated by [`LandStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3049-L3062) fails. The search repeats this anchor until reaching the 100-step cap and gives up. Natively, the 5 coastal offshore mines would have been sampled and could have placed the building along the beach.
- **Suggested Fix:** In [`LandStep`](file:///C:/Users/pavol/Repos_Local/interplanetary-mods/AI/DirectorPatches.cs#L3049-L3062), allow fallback to the full anchor pool if a search has spent multiple steps (e.g. `(int)s_searchSteps.GetValue(search) > 20`) without finding a spot around preferred anchors, mirroring the widen-on-failure design in `CoverageStep`.
