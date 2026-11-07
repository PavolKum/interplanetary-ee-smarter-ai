I’m tracing the placement and turn-start paths against the decompiled game, including what happens after a partial failure. I’ll distinguish confirmed defects from strategic risks that need playtesting.
## Verdict: SHIP AFTER FIXES

**Fix before playtesting:** gate the turn guard to offline/enabled play; prevent partially initialized modules from running as if initialization succeeded; and protect city-defense placement space. Also correct the Rare Material timing policy or explicitly accept that it can overlap an unexpectedly early war.

**Confidence:** High on the code-path findings; medium on the size of the placement and performance effects, which need an in-game measurement. This was a read-only review; no tests or game runs were performed.

## Findings

### High — Turn guard changes online and disabled games

**`AI/TurnGuard.cs:66-77`; `AI/Plugin.cs:862-865`**

Both finalizers swallow exceptions unconditionally. They are installed regardless of `Plugin.Active`, whose explicit contract is enabled **and offline**. A module exception in an online game—or with the mod disabled—therefore changes native AI behavior and may make clients behave differently.

**Fix:** In both finalizers, return the original exception immediately when `!Plugin.Active`; only log and recover under the intended offline policy. Test enabled/offline, disabled, and both online flags.

### High — Swallowing a module failure leaves dependent code using partial state

**`AI/TurnGuard.cs:66-77`; `decompiled/Interplanetary.AI/AIConstruction.cs:441-469`; `AIWeaponTargeting.cs:55-140`**

A caught exception does let the module loop continue, but it does **not** finish the failed module. For example, `AIConstruction.BeginTurn` can fail before its queue receives this turn’s allotment or before `m_isConstructionReady` resets; `AICore.Update` can then run construction with stale state. A targeting failure can leave some trajectories initialized and others stale; its own Harmony postfix is skipped, although the core postfix subsequently runs. A failure inside power-grid initialization similarly leaves a partial slot map for construction.

A core-level exception is worse: `CoreFinalizer` swallows it **after the core postfix has already been skipped**. If it occurred before `Status = TurnOngoing`, the game may not advance the AI normally.

**Fix:** Log every failure, but recover only with an explicit per-module safe state: disable that module for the turn, prevent its dependents and director from using incomplete state, and ensure the turn can finish. Otherwise propagate the exception for diagnosis rather than claim recovery. Do not swallow core-level failures without a proven recovery path. Add fault-injection tests before and after queue initialization and during targeting; verify subsequent update/end-turn and whether each postfix ran.

### High — Placement fills the space needed for the promised defense-first layout

**`AI/DirectorPatches.cs:2761-2769, 2823-2835`; `AI/Brain/Coverage.cs` (`Shelter.Score`); `AI/Plugin.cs:613-616`**

Before defenses exist, the first legal batch favors proximity to a city; afterward it favors overlapping cover and then city proximity. Neither path reserves physical space for a deficient city’s defenses. Anti-nuke city coverage normally starts at T18 unless threat triggers it sooner, so many turns of plants and mines can occupy the same restricted ring before Ion searches begin. This directly conflicts with “defenses first, then fill”; match 20 already had five of nine cover searches give up.

The exact extent is **unverified**, not a proven guarantee that a city becomes unprotectable. Reserving the entire ring could also starve early economy.

**Fix:** Reserve enough *feasible candidate locations* for the intended city defenses before accepting non-defense placements, including queued buildings and power connectivity; relax the reservation when geometry makes it impossible. Alternatively, actually build the initial city defenses before filling. Log per-city feasible slots and cover-search failure reasons, and test dense land and shoreline maps.

### Medium — Rare Material can start too early and survive into war

**`AI/Brain/Tempo.cs` (`RareMaterialAllowed`); `AI/DirectorPatches.cs:205-213, 1826-1833`; `AI/Plugin.cs:446-449`**

The check is an **upper bound**, not “a few turns before shooting”: any earlier affordable start is allowed. Also `WarTurnFor` can change from T20 to an earlier turn when Orbital Assembly completes. A T14 project accepted against T20 can still be active when the war begins at T19, as the match’s war timing demonstrates.

For a **fixed** T20 war, `T14 + 6 <= T20` is consistent with the game’s project lifecycle: it finishes its resource effect at the start of T20 (`decompiled/Interplanetary.Prefabs/BuildingProjects.cs:337-382`). The default lead of zero nevertheless does not implement the playtest note’s “ending a few turns before.”

**Fix:** Define an earliest as well as latest start, set the intended lead explicitly, and base the safe end on the earliest plausible war or a documented forecast. If early-war overlap is acceptable when circumstances change, log it rather than promising a guarantee.

### Medium — Geometric “cover” can be ineffective

**`AI/DirectorPatches.cs:2704-2725, 2823-2825, 2867-2878`; `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:264-287`**

The shelter model counts any non-ruined defense and queued defenses regardless of power or weapon type. Two disconnected Kinetics can beat a powered Ion in the score; the reported coverage shares likewise describe geometric reach, not protection against a particular threat. The game only activates a powered defense that handles the incoming weapon and has activation resources.

**Fix:** Distinguish geometric coverage from powered/type-specific coverage in the log. For placement, favor operational or demonstrably soon-powered defenses and account for relevant defense types; don’t present the current aggregate as effective protection.

### Medium — Shared candidate budget can delay defenses behind shelter searches

**`AI/DirectorPatches.cs:2411-2430, 2754-2759, 2803-2818`; `decompiled/Interplanetary.AI.Construction/AIBuildingPlacementFinder.cs:43-51`**

Shelter and defense searches share 120 candidates per frame; the finder visits searches in list order. Earlier shelter searches can consume the budget before later defense searches are visited. Waiting doesn’t increment `m_steps`, so the placement cap does not bound **wall-clock** waiting. The game can instead reach its AI turn timeout.

`BuildShelterModel` and anchor coverage are also rebuilt per productive step; cost grows with buildings and defenses. I cannot quantify this without a game run.

**Fix:** Reserve a defense budget or schedule searches fairly, and measure per-turn placement time, budget waits and unfinished searches at timeout. Cache the own-defense snapshot within a frame if profiling warrants it.

## Other checks

- **Connections and overlap:** the reflected anchor methods have the expected two-list signatures. Connected candidates use the game’s anchor eligibility and a line-length check; plant registration mirrors native behavior. `IsBuildingPlacementValid` checks queued-building overlap, while `TryPlaceBuilding` checks terrain, existing collision and fallout. When no eligible anchor exists, fallback can place an unconnected consumer, but that is also possible in the native random path; it is not introduced by the shelter candidate path.
- **Water and abandon:** water plants deliberately use no connection; water consumers use grid anchors and fall back to the older water/native path. Productive shelter steps increment `m_steps`; after 15 failed default shelter steps, fallback runs, and the finder cap remains the eventual abandon valve. Budget-starved waiting is the exception described above.
- **Ring sampling:** `RingAnchor` is a reachability heuristic, not proof that a sampled point is legal or closes the *chosen* city’s deficit. The subsequent legal-placement, deficit and actual-anchor checks prevent it accepting an invalid candidate. Whether the 50% mix improves match 20’s failure rate needs measurement.
- **Projects:** Rare Material still falls through to `leavesJamming`; SSTAR remains exempt. Queued copies increment the active count, and the planet guard accounts for added drain. The guard can be conservative for a second temporary copy because the first copy’s increased drain is treated as baseline through the horizon.
- **Fairness:** I found no new enemy-information read or resource bonus in the reviewed placement and timing changes. The unconditional online guard is the exception to the mod’s offline-only contract.
- **Tests:** `V017Rules` tests arithmetic, not the Harmony, game-geometry, queue, scheduling, or partial-failure integrations above. Prioritize targeted fault-injection and placement integration tests over more score-only checks.
