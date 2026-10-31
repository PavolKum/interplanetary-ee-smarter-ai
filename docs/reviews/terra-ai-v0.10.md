I’m tracing the game’s actual firing and turn lifecycle first; the main risk is a mismatch between the strategy’s planned salvo and what the engine can actually fire.
Two concrete issues are emerging: the migration marker is compared lexicographically on every load, and the salvo estimate counts an arsenal that may not reach—or even fire at—the committed target. I’m checking their gameplay consequences and the hold-fire budget edge cases.
## Verdict: **Fix first**

**Confidence: high** in the code-level findings; **medium** in their relative impact on the next match. The core hold-fire path is sound when its hooks succeed, but the salvo decision can substantially overstate the force reaching a target, and postponing war also postpones defensive preparation.

## High

### 1. The salvo can be “saturated” on paper but fragmented or absent in play

`AI/StrategyPatches.cs:81–95,119–124` counts every powered non-laser weapon once for **every** enemy. It ignores known per-weapon reachability, planned misses and incomplete trajectories. After choosing a target, `AI/Brain/TargetPlanner.cs:107–112,143–150` reroutes unreachable weapons; native targeting can also select secondary-planet hits (`decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:129–166`). Yet the strike and pulse are recorded before any shot fires.

This is material: the strategy review reports 41 powered entries but only 13 firing-list entries for Guts at T28 (`docs/reviews/sol-ai-strategy-v0.10.md:26`). The count also misses multiple projectiles and fragments: an Asteroid Diversion launches ten projectiles, and Coilcannons split (`docs/ruleset/embedded/enhanced/WeaponsData.csv:11–13`; `decompiled/Interplanetary.GameEngine/ActionphaseManager.cs:221–245`).

**Fix:** Estimate *per-target* reachable, likely-to-fire projectiles using reachability history, accuracy and weapon multiplicity. Treat unknown trajectories conservatively, log predicted versus actual arrivals, and update strike history from actual firing rather than the initial decision.

### 2. Hidden defenses are treated as absent, and overlapping defenses are under-modelled

With no remembered defenses, `AI/Brain/Strategy.cs:74–93,106–109` predicts every shot passes—even with hidden enemy energy. Twelve weapons therefore meet both the minimum and “broken” thresholds and can fire every turn. Against 19 known Kinetic defenses, the formula assumes only 19/31 coverage and one 80% roll per eligible shot; the game can try **multiple** defenses on the same impact until one succeeds (`decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:223–249`). That is not calibrated to the playtest’s heavily intercepted volleys.

Observed results are also pooled across weapon types before being applied to the entire new mix (`AI/StrategyPatches.cs:101–116`; `AI/Brain/Strategy.cs:96–105`), so missile observations can distort a Railgun estimate.

**Fix:** Distinguish unknown from observed absence; retain observations per weapon and use conservative coverage/overlap bounds until calibrated. Replay the v0.9.1 arrivals and interceptions against predicted pass rates before trusting the thresholds.

### 3. Delaying war also delays the defensive floor and reserve

`AI/Plugin.cs:306–307` moves war from T15 to T20. The same `d.War` gates the own-building defense floor (`AI/DirectorPatches.cs:188–206`; `AI/Brain/Director.cs:135–148`) **and the entire defense-energy reserve** (`AI/DirectorPatches.cs:377–393`). Visible threats can still drive defense construction, but a hidden opponent can attack an unprepared AI before T20; even built defenses may have no energy left. The previous blind-defense failure is documented in `AI/README.md:112–123`.

**Fix:** Separate offensive buildup timing from defensive readiness. Preserve an earlier defense floor and reserve when defenses or a credible threat exist; test a hidden early attacker.

### 4. Budget release is not fail-safe against a failed firing hook

`AI/DirectorPatches.cs:364–371` releases all upkeep, while suppression depends on a separately installed reflection-based prefix (`AI/Patches.cs:245–266`). Patch installation can fail independently yet the plugin continues (`AI/Plugin.cs:399–407,422–442`); reflection exceptions likewise fall through to firing. A live strategy toggle can also make `Latest` return null after release (`AI/StrategyPatches.cs:42–46`).

If construction spends the released budget and a weapon is nevertheless targeted, the game checks actual resources and rejects unaffordable shots; affordable shots can still consume the intended defense reserve (`decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:230–261`).

**Fix:** Enable budget release only when the suppression hook and required fields are verified, and latch the hold decision for the whole turn. On suppression failure, prevent targeting rather than silently falling through.

## Medium

### 5. Config migrations repeat on every startup

`AI/Plugin.cs:323–347`: ordinal `"0.10" < "0.5"`, so a migrated config re-enters the old migration and then the new one on every load. A user setting `MaxDefenses=40`, `WarFromTurn=15`, or `ScienceMaxMultiplier=4` can be overwritten again.

**Fix:** Use numeric/version-aware comparison or explicit migration states, including recognition of existing `"0.10"`; test two successive loads and intentional settings restored after migration.

### 6. Hold-fire savings arrive too late for superweapon purchase decisions

Savings and `BuyPending` are calculated from energy **before** release (`AI/EconomyPatches.cs:152–199,217–220`); release occurs later (`AI/DirectorPatches.cs:348–375`). A superweapon affordable only on hold turns can remain indefinitely classified unaffordable and its material reserved, even though the released energy could buy it.

**Fix:** Decide hold and adjust both planning budgets before computing the savings/buy decision; preserve the separate material bank and defense reserve.

### 7. Director-off mode holds fire without recovering the reservation

Strategy runs independently of `DirectorEnabled` (`AI/EconomyPatches.cs:217–221`), but release exists only in the director construction path (`AI/EconomyPatches.cs:275–285`; `AI/DirectorPatches.cs:364–371`). With the director off, the AI loses the shots **and** their investment budget.

**Fix:** Release safely in a shared turn-start path, or disable hold when the director is off.

### 8. Max-hold does not cover small armies

`AI/Brain/Strategy.cs:134–155` returns before the fallback for peace or fewer than six weapons; `AI/StrategyPatches.cs:125–127` does not increment the hold counter for small armies. A damaged AI stuck below six can remain silent indefinitely.

**Fix:** Apply an explicit emergency/finish-off or bounded-hold rule to small arsenals, while retaining a deliberate opening policy.

## Checked and fine

- **Normal firing path:** non-laser AI targeting flows through `AIWeaponTargeting.EndTurn` → goal → `AITargetWeapons.UpdateEndTurn`; an empty goal list produces no targeting actions. Lasers share that goal when enabled. Guided missiles are **gravity** weapons here, so they are included in repointing (`decompiled/Interplanetary.Weapons/WeaponFlightModule.cs:15–19`).
- **Order and repointing:** resource and module initialization precede the economy postfix; construction is later. Gravity search starts in `Update`, and `AngleToTarget` is the launch angle, not the chosen planet (`decompiled/Interplanetary.AI/AIWeaponTargeting.cs:109–150`; `AIGravityTrajectoryWeapon.cs:26–50`).
- **Lifecycle:** normal initialization clears memory; the backward-turn backstop runs before strategy. I found no demonstrated stale decision in the ordinary turn sequence, but missed hooks, same-turn reloads and live toggles need integration coverage.
- **Glut and research:** glut uses a fixed start-of-construction snapshot and respects the already deducted material reserves; it cannot flip mid-turn. Research order and unchanged-plan migration match the stated intent. Long-term material depletion remains a playtest risk.
- The 250 checks exercise game-independent Brain code, not Harmony registration, actual resource/action ordering, save/load, or the realized volley (`AI/Tests/BrainTests.csproj:3,12–14`). **Tests were not run:** this was a read-only review. Gameplay remains **unverified in-game**.
