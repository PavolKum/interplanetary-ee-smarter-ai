I’ll trace the six hooks through the decompiled game code, then check the planner, information visibility, and the native AI’s economic constraints. I’ll keep the review read-only.
The main risks are already visible: the planner treats remembered “shadow” buildings as current observations, and a 100% hit quota can suppress shots when no hit trajectory is found. I’m checking the game’s damage, visibility, and turn-finalization paths to pin down the exact consequences.
# Verdict: **fix first**

The two headline targeting features fail on ordinary visible cities: both adapters read a health stat that cities do not have. The fairness guarantee also fails for shadow intel. **Confidence: high** in the static findings; in-game behavior and actual patch installation remain **UNVERIFIED**.

## Findings, highest severity first

### 1. **Blocker — visible cities throw in both targeting hooks**

- **Evidence:** `AI/Patches.cs:110-119, 246-249, 268-281`; `decompiled/Interplanetary.Prefabs/BuildingStatHolder.cs:42-47, 79-84, 161-182`; `decompiled/NyaEngine.Stats/NyaStatHolder.cs:15-20`; `decompiled/Interplanetary.Prefabs/BaseBuilding.cs:964-983`.
- **Failure:** Cities have `CurrentPopulation`, not `hp_current` or `hp_max`. Reading `CurrentHP` returns a missing stat and then dereferences null. A visible city aborts `BuildEnemyView`, so focus targeting stays native; it also aborts candidate collection, so guided strikes fall back to native. The tests fabricate city HP and therefore miss the adapter failure.
- **Fix:** Use city population/max population for city durability and HP/max HP for ordinary buildings. Add adapter-level tests using real stat shapes.

### 2. **Blocker — shadow buildings leak current hidden state**

- **Evidence:** `AI/Patches.cs:110-130, 268-280`; `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:176-206`; `decompiled/Interplanetary.PlayerDataManagement/ShadowBuildingFrozenState.cs:39-67`; `decompiled/Interplanetary.Prefabs/BaseBuilding.cs:836-853`.
- **Failure:** `!= Invisible` accepts `Shadow`, but the mod reads the live building’s state, HP, generation and population. A formerly visible building can change while its icon shows frozen last-known information; the planner can react to those hidden changes. Even filtering by live `CurrentState` can leak whether it is still intact.
- **Fix:** For `Visible`, use current observable values. For `Shadow`, use only the viewer’s frozen intel state and known location, with conservative estimates for fields not recorded; otherwise exclude it. Test hidden changes after visibility is lost.

### 3. **Major — 100% planned accuracy can make weapons silent**

- **Evidence:** `AI/Patches.cs:145-158`; `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:129-207`; `decompiled/Interplanetary.AI.Trajectories/AITrajectoryWeapon.cs:55-68, 87-100`; `decompiled/Interplanetary.AI/AIDifficulty.cs:20-33`.
- **Failure:** With `Misses = 0`, a weapon with a miss trajectory but no hit trajectory is never selected as a miss. At native difficulty 2, a miss quota could have selected it; the default override changes that difficulty to 100%. Native difficulty 3 already has this limitation.
- **Fix:** At finalization, select available hits first, then explicitly decide whether an otherwise silent weapon should fire its best available miss. Keep the actual hit/miss counts distinct from the requested quota.

### 4. **Major — shooting lists can be stale or never populated**

- **Evidence:** `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:37-60, 63-103, 106-128`; `decompiled/Interplanetary.AI.Trajectories/AITrajectoryWeapon.cs:132-151, 174-183, 198-207, 210-232`; `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:173-184`; mod quota at `AI/Patches.cs:140-159`.
- **Failure:** `BeginTurn` does not clear the hitting/missing lists. `DetermineShootingWeapons` is called when a **primary** trajectory is registered, not when a secondary or miss is registered, nor immediately before finalization. If no primary is registered this turn, finalization can use last turn’s lists or empty lists even when secondary/miss trajectories exist. The override changes quotas but does not repair this native synchronization weakness.
- **Fix:** Recompute selection from current trajectories immediately before `TargetWeapons`; clear per-turn lists and test zero-primary, secondary-only, miss-only and changed-quota cases.

### 5. **Major — exception fallback is not transactional**

- **Evidence:** `AI/Patches.cs:83-99, 236-265, 325-329`; `decompiled/Interplanetary.AI/AIActions.cs:172-175`; `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:483-490`.
- **Failure:** The target postfix mutates weapons one by one; an exception after some assignments leaves a partial plan despite the log claiming native behavior. The strike prefix queues an action before all subsequent work completes; an exception afterward returns `true`, allowing native code to queue a second action.
- **Fix:** Validate and stage the complete plan before mutation. Once a strike action has been queued, do not fall through to native; separate diagnostics from the action decision. Log partial failures accurately.

### 6. **Major — reachability memory mistakes search failure for physical impossibility**

- **Evidence:** `AI/Patches.cs:192-211`; `AI/Brain/Memory.cs:73-93`; `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:66-77, 222-234`; `decompiled/Interplanetary.AI.Trajectories/AIGravityTrajectoryWeapon.cs:9-11, 40-50`.
- **Failure:** Any powered weapon without a **primary** hit is marked unreachable, including one that found a secondary hit or simply ran out of randomized search time. Orbital geometry changes between turns, yet the mark suppresses the chosen target for two subsequent turns.
- **Fix:** Treat this as low-confidence *search failure*, not proof of impossibility; track attempts/time and secondary successes, expire quickly, and periodically retry.

### 7. **Minor — unknown city health is scored as a weak enemy**

- **Evidence:** `AI/Brain/TargetPlanner.cs:49-65`; `AI/Patches.cs:110-120`; `AI/Tests/Program.cs:69-79`.
- **Failure:** Once city-stat handling is fixed, an enemy with no visible cities has `CityHp = 0`. If another enemy has visible city population, the unknown enemy receives the maximum finish-off bonus, although no weakness was observed.
- **Fix:** Apply the finish term only with sufficient known city coverage, or model unknown separately. Add a no-visible-cities test.

### 8. **Minor — match memory and online guard have gaps**

- **Evidence:** `AI/Patches.cs:32-35, 177-180, 287-293`; `AI/Plugin.cs:167-177`; `decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:263-295`.
- **Failure:** Every `AICore.Initialize`, including the core created for a human player, clears **all** AI memory. Reinitialization/load mid-match loses grudges, reachability and accuracy carry. The damage and firing-summary hooks check `Enabled`, rather than the offline `Active` guard, so they can still mutate memory and log in online games.
- **Fix:** Reset at a verified match boundary, not per-player initialization; apply `Active` consistently. Decide whether save/load should persist memory.

### 9. **Minor — tests miss the integration and one assertion is weak**

- **Evidence:** `AI/Tests/Program.cs:114-125, 181-194`; `AI/Tests/BrainTests.csproj:12-14`; city mismatch in `AI/Patches.cs:268-280`.
- **Failure:** The all-unreachable assertion passes even if assignment always returns the preferred target. Brain-only tests cannot exercise Harmony binding, real city stats, shadow snapshots, or end-turn list synchronization.
- **Fix:** Add adapter and game-shaped integration cases; for reachability, assert both the all-blocked fallback and a nearby case where clearing exactly one mark changes the choice. Verify regression tests fail before the fixes.

## Hook audit

All six named target methods exist as single declarations in their respective decompiled classes; the explicit original argument names used by the patches match. Harmony installation against the shipped game assembly is **UNVERIFIED**.

| Hook | Static assessment |
|---|---|
| `AICore.Initialize` postfix | Signature is valid; fires after each player core initializes, not once per match (`AICore.cs:70-112`; `PlayerData.cs:294-295`). |
| `AIWeaponTargeting.BeginTurn` postfix | Runs after native weapon setup and before subsequent `Update` searches. `TargetData` contains **all** enemy planet IDs, and `AngleToTarget` is calculated from the weapon’s outward radial direction, not the chosen enemy; changing `TargetEnemy` there is consistent with the shown search path (`AIWeaponTargeting.cs:110-140, 143-152`; `AIGravityTrajectoryWeapon.cs:26-50`). |
| `AIWeaponTargetingGoal.BeginTurn` postfix | Runs after native quota and flags are set (`AIWeaponTargeting.cs:109-110`; `AIWeaponTargetingGoal.cs:37-60`). Total shots is unchanged, so the zero/nonzero initialization of `IsGoalMet` and `AreWeaponsTargeted` remains consistent. `IsGoalMet` has no other reader in the inspected AI source; finalization uses `AreWeaponsTargeted`. |
| `AIWeaponTargetingGoal.TargetWeapons` prefix/postfix | `out bool __state` and the postfix’s `bool __state` are correctly paired in the same patch class (`AI/Patches.cs:172-179`). It captures pre-call targeting state, not proof that shots were actually fired; final trajectory calculation and action queuing occur later (`AITargetWeapons.cs:38-51, 82-99, 114-154`). |
| `HandleSecondPhaseTargeting` prefix | Argument names match. Called after first-phase success, with the **actually hit enemy planet**, for gravity weapons having a second targeting phase (`AITargetWeapons.cs:142-152`). The native method selects a candidate or random surface point and queues the same action; skipping it changes candidate selection and omits its diagnostics, but no additional native action is evident (`AIWeaponTargeting.cs:398-497`; `AIActions.cs:172-175`). |
| `DealDamageToBuilding` prefix | `building`, `damage`, `sourcePlayerID` match (`PlayerBuildingHandler.cs:572-585`). Projectile events set shooter ID from `PlayerData.PlayerID` and forward it; owner IDs use the same player dictionary (`WeaponGameplayData.cs:369-373`; `ActionphaseBuildingDamageEvent.cs:35-46`). Victim-facing city damage logs group by shooter (`CityDamagedEvent.cs:78-99`). Other damage origins and exactly what the player can observe in every mode are **UNVERIFIED**. |

**Runtime:** The normal and end-turn paths have 20-second and 5-second timeouts, respectively (`AICore.cs:193-201, 230-237`). They bound waiting while frames continue; they cannot rescue a synchronous exception or blocked main thread. The shown AI update path is synchronous (`PlayerData.cs:314-341`), but an exhaustive thread-affinity proof is **UNVERIFIED**. No in-game run or test execution was performed.

## Next three changes by expected win-rate impact

These are hypotheses to validate in matched playtests, not measured effects.

1. **Economy and construction policy.** Replace random construction-category selection with a resource-growth and firing-upkeep plan; instrument placement and affordability failures. Hook `AIConstruction.tryConstructNewBuilding` / `AIConstruction.Update`. Native uses fixed weighted draws after short-term needs (`AIConstruction.cs:218-258, 265-345`), while it reserves firing costs (`AIResources.cs:38-64`). **Confidence: medium**; exact cause of the observed plateau is **UNVERIFIED**.
2. **Research for compounding economy.** Prioritize economic technologies by payback and current bottleneck before switching to firepower. Hook `AIShortTermStrategy.BeginTurn` or its tech-selection methods. Native randomly chooses focus and increases firepower weight over time (`AIShortTermStrategy.cs:85-125, 128-160`). **Confidence: medium**.
3. **Reliable pressure against the leader.** After fixing the blockers above, improve shot selection and allocate trajectory-search effort to strategically valuable, plausibly reachable targets; consider visible economic infrastructure as guided-strike targets rather than only cities/weapons. Hook `AIWeaponTargeting.BeginTurn` and final selection at `AIWeaponTargetingGoal.TargetWeapons`. Native has a capped per-weapon search allowance and random trajectories (`AIWeaponTargeting.cs:66-77, 100-109, 222-234`). **Confidence: medium**; actual hit-rate and time-budget impact are **UNVERIFIED**.
