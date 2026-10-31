# Verdict: **fix first**

**Confidence: high** in the code findings; **medium** in their match-level impact. The native placement call is broadly compatible with the search lifecycle, but the new policy can repeatedly choose already-planned coverage and can produce defenses without valid power connections. Its improvement over native placement is not yet established.

## High

### 1. Same-turn defenses do not see one another’s planned positions

`PickCoverageAnchor` considers only `BuildingHandler.Buildings` (`AI/DirectorPatches.cs:991–1019`). Successful searches instead add construction **actions**; buildings are placed later (`decompiled/Interplanetary.AI/AIConstructionQueue.cs:145–159`, `AIActions.cs:43–50,69–93`). Multiple defenses bought in a catch-up turn therefore score against the same old coverage, even after earlier placements have succeeded. Random band selection may spread them, but does not ensure it.

**Fix:** Include successful pending defense actions and their `BuildingPlacementResult` positions in the projected coverage, refreshing that projection when selecting an anchor. Test several same-turn placements before action execution.

### 2. An accepted placement can have no usable power connection

The invoked native helper sets `m_result.PowerGridConnection` to the anchor, but **does not validate the eventual power line** (`decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs:216–247`). At execution, the game rejects a connection without a free slot or beyond the connector range, while still placing the building (`decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:243–267`; `PlayerPowerGridHandler.cs:63–72`).

Two new paths make this material:

- `st.Bases` is captured once; on a later selection another search may have consumed an anchor’s last slot. The newly picked anchor is invoked immediately, before `usable` is checked again (`AI/DirectorPatches.cs:970–983,992–999,1014–1023`).
- For defenses, native sampling replaces its connector-based maximum with the raw defense radius (`AIBuildingPlacementSearch.cs:218–234`). For example, an Enhanced small city and Kinetic Defense have a connector-distance limit of 1.7 + 1.1 + 2.7 = 5.5, versus a 5.9 sampling radius (`docs/ruleset/embedded/enhanced/CitiesData.csv:13,29`; `BuildingsData.csv:17–18`; `DefensesData.csv:5`). Some sampled positions can exceed the line limit.

**Fix:** Revalidate state, distribution and free slots immediately before *every* attempt; validate the successful result’s actual world-space distance against the game’s connector limit before accepting it. Add regressions for a consumed slot and an out-of-range result.

## Medium

### 3. The anchor score is not the coverage gained

`UncoveredValue` scores targets within a radius of the **anchor** (`AI/Brain/Coverage.cs:52–60`), but the final defense is sampled away from it (`AIBuildingPlacementSearch.cs:230–242`). A target near one edge of the anchor’s circle can be almost two radii from the resulting defense. Dense or edge-of-cluster anchors can consequently score highly while the chosen position covers little of their value. Defense anchors and the weighted band can also favor spreading rather than the overlap needed for multiple interception attempts.

The three-cluster test places the defense **at** the anchor (`AI/Tests/Program.cs:334–354`), so it does not establish actual gain or superiority over native random placement.

**Fix:** Sample legal candidate positions and score their *actual* marginal coverage, including pending placements; compare against native placement on representative planet layouts and the next playtest. Permit intentional overlap when additional interception probability is valuable.

### 4. “Covered” does not match active, weapon-specific defenses

The planner counts every non-ruined building with the same building ID, including under-construction and unpowered defenses, using one base radius (`AI/DirectorPatches.cs:1004–1019`). The game requires a **Normal** defense for eligibility and power for activation (`PlayerDefenseHandler.cs:264–287`). Gameplay radius is each building’s `Stats.Defenses.Radius`; upgrades modify that stat (`DefenseGameplayData.cs:13`; `BuildingDefenseStats.cs:16`; `BuildingUpgradeStats.cs:128`). Enhanced upgrades include radius multipliers (`docs/ruleset/embedded/enhanced/UpgradesData.csv:136`).

Building ID is also not weapon coverage: Enhanced Shield and city defenses can counter Railguns with different probabilities (`DefensesData.csv:10–18,26–38`), but are ignored by Kinetic scoring. `CoverageShares` has the same base-radius limitation and counts unpowered Kinetics (`AI/DirectorPatches.cs:1034–1051`).

**Fix:** Model each existing defense’s actual functionality, current radius, state and power, and weight coverage by relevant weapon and interception probability. If under-construction defenses are intentionally projected, distinguish projected from currently effective coverage in both scoring and logging.

### 5. Impact geometry reads every defender and is only a proxy

`AI/LedgerPatches.cs:51–59` scans **every defender’s** Normal buildings, including a human opponent’s hidden buildings, on every impact. These new geometry counts are stored under the defender and the director reads only its own counts (`AI/Brain/DefenseLedger.cs:48–65`; `AI/DirectorPatches.cs:179–185`); I found no path using the human’s hidden geometry for AI decisions. Nevertheless, the claim that the new hook reads only own buildings is false.

`nearCity` means within `city.Radius + 2`, not necessarily a city hit; `anyDefenseNearby` uses a fixed 6.5, not actual radius, weapon eligibility or power, and misses city-project defenses. The scan costs O(buildings) per impact, with a square root per building.

**Fix:** Compute geometry only for AI defenders, use squared distance, and name the counters as proximity proxies—or use active functionality and actual per-building radius. Measure cost under large volleys.

## Low

### 6. The surviving mutant is return-value equivalent, but not fully equivalent

Removing `if (best <= 0f) return -1` still returns `-1` when all scores are zero: the final loop finds no positive score (`AI/Brain/Coverage.cs:79–96`). However, the mutant also calls `rng.NextDouble()`, advancing the shared `Plugin.Rng` used by later placement, weapon and targeting decisions (`AI/Plugin.cs:140`; `DirectorPatches.cs:551,1020`; `Patches.cs:98,325`).

**Fix:** Retain the early return; if mutation coverage matters, assert that a no-candidate call does not advance the supplied RNG.

## Checked and fine

- **Native lifecycle:** The helper sets `IsComplete` on successful placement; `Update` then calls `Success`, and the queue records the action (`AIBuildingPlacementSearch.cs:74–88,247–279`; `AIConstructionQueue.cs:153–159`). The prefix increments `m_steps`; at ≥100 the native city restriction is skipped. Bypassing native list construction is intentional, subject to finding 2.
- **Termination:** On `GaveUp`, native `step()` resumes; after a custom attempt, its counter remains ≥101. Finder updates divide the allowance across searches and the cap removes incomplete searches, refunds queued costs, marks the ID failed and reopens the director (`AIBuildingPlacementFinder.cs:37–57`; `AI/DirectorPatches.cs:969–977,1104–1145,1162–1170`). The default cap of 1,500 effectively permits roughly 1,400 custom/native attempts after the forced jump; a configured cap of 100 can abandon after the first custom attempt. The game also has a turn timeout (`AICore.cs:171–201`).
- **Director and state:** Defense floor, catch-up and type selection still run before placement (`AI/DirectorPatches.cs:190–191,280–281,360–365,485–511`). Waiting/reopened is bounded by failed IDs and the 2,000-step valve (`:238–255`). The coverage dictionary clears at reset and above 300 entries (`:100–104,960–967`); clearing can restart active anchor selection, but not its native step counter.
- **Geometry:** World-space positions agree with the game’s world-space impact test; buildings are parented to the planet (`PlayerBuildingHandler.cs:230–236`; `PlayerDefenseHandler.cs:264–275`). Rigid planet movement or rotation preserves relative distances.
- **Tests:** The pure tests establish strict boundaries, scoring, exclusions and synthetic cluster behavior (`AI/Tests/Program.cs:297–354`); they do not exercise Harmony, actual sampled positions, power connections, pending actions or the game lifecycle. **Tests and in-game behavior were not run or verified:** this review was read-only.
