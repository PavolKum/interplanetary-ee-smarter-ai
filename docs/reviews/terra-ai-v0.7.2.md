The fallback and candidate-validation changes are present. I’m checking whether the exception path and the game’s actual connector defaults still allow an unsafe placement, then tracing the queue and time-budget behavior.
# Verdict: **fix first**

**Confidence: high** in the code-path findings; **medium** in their frequency and frame-time impact. The reviewed checkout is commit `a9a2fcb`. Runtime behavior and performance are **unverified in game**; no tests were run in this read-only review.

## High

### 1. Preferred anchors can trap the search until abandonment

`AI/DirectorPatches.cs:1047–1052,1061–1087`; `AI/Brain/Coverage.cs:124–136`

The safe fallback only samples all anchors when **no anchor scores above zero**. If a high-scoring anchor has no legal placement—crowded terrain, for example—while a lower-scoring anchor does, the `CoverageBand` cutoff can exclude that usable anchor on every step. Nothing relaxes the band or remembers failed anchors. The search can run to the default 1,500-step cap and abandon the defense despite a legal connected placement elsewhere. In v0.7.1, the 60-step native fallback at least eventually escaped this selection bias.

**Fix:** After a bounded number of unsuccessful attempts, progressively widen the anchor band or sample all eligible anchors, retaining the full placement and line checks. Test a blocked best anchor alongside a legal lower-scoring one.

## Medium

### 2. Error paths still return defenses to unsafe native placement

`AI/DirectorPatches.cs:1025–1030,1132–1143,1167–1170`

The ordinary no-coverage and no-anchor paths no longer fall back, but an exception in `CoverageStep` does: the catch returns `true`, invoking native `step()`. A missing or nonpositive defense radius also sets `GaveUp` and returns native. Native can accept an overlong or absent connection (`decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs:109–115,218–247`).

**Fix:** For AI defense searches, fail closed: log the error and arrange bounded abandonment/refund, rather than invoking native placement. Keep native fallback for unrelated searches. Verify that a deliberately injected placement/reflection error cannot queue an unsafe defense.

### 3. The performance finding is only partially addressed

`AI/DirectorPatches.cs:1026–1033,1047–1079`; `decompiled/Interplanetary.AI.Construction/AIBuildingPlacementFinder.cs:37–47`

Anchor scores are now computed once per step, fixing the specific 12-fold rescoring issue. But one finder update can still run approximately **100 steps**, each sampling 12 candidates. Each candidate may run queued-building validation, which raycasts once per queued action (`decompiled/Interplanetary.AI/AIConstruction.cs:354–373`), followed by the full placement check. The custom-search limit has also risen from 60 to the general default cap of 1,500 steps.

The 20-second AI timeout is checked **after** module updates and cannot interrupt a slow synchronous update (`decompiled/Interplanetary.AI/AICore.cs:171–201`). Actual frame time is unmeasured.

**Fix:** Bound candidate work per frame, including across failed searches, and profile a crowded planet with a large construction queue. Preserve a bounded safe fallback before abandoning.

## Checked and fine

- **v0.7.1 findings:** #1 is fixed on normal paths but **not error paths** (finding 2 above). #2 is fixed: each candidate receives the full `TryPlaceBuilding` check before ranking. #3 is fixed for accepted ordinary defense placements by checking the result’s world position against the line limit. #4 is **partial** (finding 3). #5 is fixed: queued defenses receive weight `0.25` (`AI/DirectorPatches.cs:991–1002`).
- **Success lifecycle:** Setting `IsComplete` during `step()` makes `Update()` call `Success()`; the queue callback creates the action and reserves the anchor slot (`AIBuildingPlacementSearch.cs:74–88`; `AIConstructionQueue.cs:153–159`; `AIActions.cs:82–93`). Native’s extra projected-power registration concerns a power-distributing new building, not ordinary power-consuming defenses.
- **Ring and connection:** Sampling uses anchor-local `TransformPoint`, as native does; this alone does not establish world-space reach. The subsequent successful-result distance check uses the same radius-plus-maximum-connector formula as execution (`PlayerPowerGridHandler.cs:63–72`). It protects acceptance even if scale or terrain projection changes the sampled distance. Actual transform scales and placement success rate remain unverified in game.
- **Try/refund:** `TryPlaceBuilding` creates a result and collision object but does not construct, spend resources, or register it in the world (`PlayerBuildingHandler.cs:804–852`). Discarding successful candidates therefore requires no world rollback. The abandon valve refunds the queue’s planning budgets for its incomplete searches (`AI/DirectorPatches.cs:1176–1216`).
- **Raycast:** Native uses layer `256`, distance `1000`, and stores `hit.point` as `BuildingWorldPosition` (`PlayerBuildingHandler.cs:817–821`).
- **Queue lifetime:** Successful planned constructions are in `ConstructedBuildings` until `AIActions.EndTurn()` executes the actions and clears the list (`AIActions.cs:43–50,69–93`). It is appropriate for same-turn projected coverage, not persistent history.
