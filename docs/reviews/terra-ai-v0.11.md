I’m tracing the firing and research hooks against the game’s call order first; those are where a sound planner can still fail in play.
The main finding is that the new “aim first” premise conflicts with the engine: trajectory searches are reset each turn. I’ve also found that the salvo model reads base weapon data, so the upgrades v0.11 prioritizes are invisible to its firing decision.
## Verdict: **Fix first**

**Confidence: high** in the code-level findings; **medium** in their match impact. The abort path is mechanically sound, but aim-first rests on a false engine assumption, and the salvo estimate misses the very upgrades v0.11 prioritizes.

## High

### 1. “Aim first” discards a turn without preserving an aim

`AI/Brain/Strategy.cs:189–200` requires the previous commit before a normal strike. But every turn the engine clears stored shots and trajectory flags (`decompiled/Interplanetary.AI.Trajectories/AITrajectoryWeapon.cs:132–151`), resets gravity-search progress (`AIGravityTrajectoryWeapon.cs:26–37`), and assigns targets before that turn’s search starts (`decompiled/Interplanetary.AI/AIWeaponTargeting.cs:109–150`; `AI/StrategyPatches.cs:204–208`). Holding does not carry a calculated trajectory into the next turn.

Maverick’s T30 result—11 primary trajectories from 48 powered weapons—shows a search shortfall, not evidence that a previous turn of aiming fixes it (`docs/playtests/data/v0101-ai-decisions.jsonl:438,444`).

**Fix:** Remove the previous-commit firing prerequisite; keep stickiness if useful and judge the current turn’s trajectories at the abort gate. Test an immediate target switch against the actual lifecycle.

### 2. Upgraded projectiles are invisible to the salvo model

`AI/StrategyPatches.cs:141–154` reads projectile and fragment counts from base `WeaponData`. Runtime upgrades instead update `ShootingModule.ProjectileAmount` and `SurfaceModule.SplitFragments` (`decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:65–71`; `WeaponSurfaceModule.cs:18–24`); the action phase fires the former (`decompiled/Interplanetary.GameEngine/ActionphaseManager.cs:221–229`).

Consequently, upgraded Railguns can be underestimated up to threefold: the AI may hold, select the wrong opponent, or miss the big-salvo threshold despite buying the upgrades specifically to increase volume.

**Fix:** Build the estimate from each weapon’s current runtime projectile and fragment counts. Add an adapter-level check with an upgraded weapon, not just synthetic shot totals.

## Medium

### 3. Researching Shields does not ensure any are built

The ordinary defense branch runs only while **total** defenses are below the desired count (`AI/DirectorPatches.cs:472–476`), itself capped at 60 (`AI/Brain/Director.cs:159–165`; `AI/Plugin.cs:356`). Once the existing Kinetic/Ion total reaches that target, a newly unlocked Shield gets no allocation even if Lasers are known.

**Fix:** Add a bounded, threat-specific coverage deficit or permit replacement/rebalancing at the cap. Test a full Kinetic/Ion complement followed by visible Lasers.

### 4. Placement throttling suppresses alternatives and alternates with unthrottled turns

The cap is correctly keyed by building ID (`AI/DirectorPatches.cs:185–187,1036–1055,1586–1590`), but candidate selection does not filter capped types. If `TryDefense` selects a capped type, it returns false and disables the whole defense branch for the turn rather than trying another (`AI/DirectorPatches.cs:625–650,472–476`). Likewise, the unpowered-building path chooses one plant before queueing and can abandon the branch without trying another (`:429–440`).

Also, a capped turn can have at most two failures, so the `>=3` trigger clears next turn even if the planet remains full.

**Fix:** Exclude capped types before picking; try alternatives, and retain the throttle until successful placement or other evidence of recovery.

### 5. Urgent counter research is delayed by existing queues

`AI/EconomyPatches.cs:476–477` returns whenever *any* technology is queued. The engine queues a target **and its prerequisites** (`decompiled/Interplanetary.PlayerDataManagement/PlayerTechnologyHandler.cs:480–503`), so seeing Lasers cannot reprioritize until the whole existing chain empties. The hook also uses the previous turn’s enemy picture: module `BeginTurn` precedes the economy postfix (`decompiled/Interplanetary.AI/AICore.cs:162–168`; `AI/EconomyPatches.cs:81–93`).

A rejected tech is additionally placed in one match-wide `s_refused` set (`AI/EconomyPatches.cs:500–527`), potentially suppressing it for other AIs after a player-specific failure.

**Fix:** At a safe queue boundary, permit a bounded urgent replan preserving progress; scope refusal to player and failure cause. Test a Laser sighting during a multi-tech queue and a rejection for only one AI.

### 6. Firing history counts candidates, not confirmed shots

After the abort clears both lists, `ReportFired(0)` correctly increments holds without changing `LastStrikeTurn` (`AI/Patches.cs:308–313,365–366`; `AI/StrategyPatches.cs:90–103`). But on a non-aborted strike the same report counts list entries **before** end-turn calculations and action/resource checks (`decompiled/Interplanetary.AI/AITargetWeapons.cs:82–100,114–145`). A timeout, missing result, or rejected action can consume the pulse even when nothing fires.

**Fix:** Record successfully executed firing actions, or explicitly distinguish planned, accepted and fired counts.

## Checked and fine

- **Abort mechanics:** primary flags and targets are reset at turn start and remain available at targeting; primary results populate the hitting list (`AITrajectoryWeapon.cs:163–183`; `AIWeaponTargetingGoal.cs:125–145`). Clearing both lists yields an empty end-turn calculation (`AIWeaponTargetingGoal.cs:63–103`). No firing action means its reserved real energy remains available to defense; the defender initializes its roll budget from actual resources (`decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:215–237`).
- **Stickiness:** near-equal targets do not flip; eliminated targets drop out. Larger alternating swings can switch every turn, but max-hold bypasses aim and abort when predicted passing is positive (`AI/Brain/Strategy.cs:163–171,197–200`). Zero predicted passing is not bounded.
- **Glut:** `EndEnergyLeft` is the director’s remaining **construction budget**, after the defense reserve and completed refunds—not actual end-of-action energy (`AI/DirectorPatches.cs:370–395,1594–1602,1637–1641`). Both gluts favor material-glut economy weights; positive upgrade firing deltas are checked against the upkeep cap and damage-losing upgrades rejected in energy-glut mode (`:1164–1174`). A sudden income loss can leave one turn of stale glut weighting; monitor it.
- **Research IDs and fairness:** visible weapon `weapon_functionality` IDs match counter keys (`AI/EconomyPatches.cs:109–116`; `AI/DirectorPatches.cs:137–147`). Enhanced Shield requires Ultrasonic Projection, whose prerequisite is Plasma Channel Induction (`docs/ruleset/embedded/enhanced/TechTreeData.csv:2,4,9`). Inputs reviewed are own state, visibility-gated intel or shooter-specific observations.
- **Tests:** the cited checks exercise `Brain/`, not Harmony hooks or engine ordering (`AI/Tests/BrainTests.csproj:3,12–14`). Add lifecycle coverage for the findings above. **Tests were not run:** read-only review. Gameplay remains **unverified in-game**.
