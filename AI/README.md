# Interplanetary EE: Smarter AI (`AI/`)

BepInEx 5 + HarmonyX plugin that makes the single-player AI play to win. Offline games only; online games always run the native AI.

## Why the native AI is weak (verified in `decompiled/Interplanetary.AI/`)

- **Random targeting.** Each weapon picks a uniformly random living enemy every turn (`AIWeaponTargeting.cs:114`). No focus fire, no leader-punishing.
- **No memory.** Threat assessment is a stub (`OverallThreat` fixed at 1, weapon/vengeful threat methods return 1). Nothing records who attacked whom.
- **Deliberate misses.** `AIDifficulty` sets accuracy 0.4 / 0.75 / 0.9 / 1.0 by level and `AIWeaponTargetingGoal` plans that many misses per turn.
- **Difficulty only changes shooting.** Economy, research and construction are identical at every level.

The 29 Oct 2026 match (debrief `261029-be947731`) shows the result: the human's energy went 1.6k (T10) -> 13k (T30) -> 44k (T40) while all four AIs stayed flat at 2-3.5k, the human's population never dropped, and the AIs spent their fire on each other.

## v0.1 - combat

| Change | Hook | Config |
|---|---|---|
| Threat-based focus fire: score = visible economy (squared share of strongest) + grudge + finish-off, top target gets `FocusShare` of weapons, rest weighted random | postfix `AIWeaponTargeting.BeginTurn` rewrites `TargetEnemy` | `[Targeting]` |
| Reachability memory: a weapon that found no trajectory onto a target is steered to the next-best one for N turns | postfix `AIWeaponTargetingGoal.TargetWeapons` records; targeting reads | `ReachabilityMemoryTurns` |
| Grudges: damage per shooter, decayed per turn | prefix `PlayerBuildingHandler.DealDamageToBuilding` | `GrudgeWeight`, `GrudgeDecayPerTurn` |
| Accuracy: re-split the same shot count at a configured hit share | postfix `AIWeaponTargetingGoal.BeginTurn` | `[Accuracy] AccuracyByLevel` (default `0.4,0.7,0.7,0.7` = human parity; see config text) |
| Native bug fix: rebuild the firing list from this turn's results right before firing (natively it only refreshes on a primary hit, so secondary-only weapons stay silent) | prefix `AIWeaponTargetingGoal.TargetWeapons` | `RefreshFiringList` |
| Planned misses fire only as verified near-misses, never onto the AI's own planet (the game counts an own-planet impact as a "miss") | same prefix | `[Accuracy] FireMisses` (default on) |
| Guided strikes go for the visible city with the least population (a city is ruined at 0; defeat = last city lost) | prefix `AIWeaponTargeting.HandleSecondPhaseTargeting` | `[Strikes]` |

Fairness: every input comes from buildings the AI's own intel currently sees (`Visible` only; shadow intel is skipped), its own damage taken, and its own trajectory results. No hidden information, no resource bonuses.

Review: two GPT-6.1 Terra passes; see `docs/reviews/terra-ai-disposition.md` for what was fixed and what is open.

`HumanBias` (default 1.0) multiplies human enemies' score. At 1.0 the AIs gang up on whoever leads the economy, which is usually the human.

## Files

- `Brain/` - game-independent decision logic (planner, memories, accuracy, strike choice).
- `Patches.cs` - v0.1 combat hooks; `EconomyPatches.cs` - v0.2 economy, research and defense hooks; `LaserPatches.cs` - experimental laser support. Any exception keeps the native behaviour for that hook and is logged once.
- `Plugin.cs` - config, hook registration, offline guard, decision log.
- `Tests/` - console checks for `Brain/`: `dotnet run -c Release` in `AI/Tests` (exit code 1 on failure).
- `tools/summarize_decisions.py` - reads the newest decision log and prints targets, trajectory success, threat scores, economy, superweapons, research and defense for the last match.
- `tools/human_research_order.py` - consensus research order from the recorder's winning-match debriefs.

## Build, install, uninstall

```
dotnet build AI -c Release            # deploys to <game>/BepInEx/plugins/InterplanetaryAI/
dotnet build AI -c Release -p:SkipDeploy=true   # while the game is running
```

Config: `<game>/BepInEx/config/pavol.interplanetary.ai.cfg`. Log lines: `<game>/BepInEx/LogOutput.log` (`T12 AI 02: 9 weapons -> ...`). Decision log: `<game>/BepInEx/InterplanetaryAI/decisions-<start>.jsonl`.

Uninstall: delete `<game>/BepInEx/plugins/InterplanetaryAI/`, or set `[General] Enabled = false`.

## Playtest loop

1. Play a match against AIs (any difficulty).
2. `python AI/tools/summarize_decisions.py`
3. Compare against the recorder's postgame facts (`ScheduleImodes/interplanetary/debriefs/`).
4. Tune config or change the Brain; rebuild; repeat.

## v0.2 - build and research like a strong player

| Change | Hook | Config |
|---|---|---|
| Superweapon savings: once Nuclear Missile / Asteroid Diversion is unlocked, a reserve that converges on the cost (the native AI drops any need above 1.65x its stock, so it never saves); bought through the native weapon module | postfix `AICore.BeginTurn` (reserve), prefix `AIConstruction.tryConstructNewBuilding` (buy) | `[Economy] SuperweaponSavings`, `SaveShare`, `Superweapons` |
| Build planner: growth weights (energy/material heavy) for the first 15 turns, then a balanced mix that keeps growing while arming (growing while behind the leader was rejected: it never punishes a human boom); counterintel kept; +defense when recently hit. Replaces the fixed native roulette; needs and minimum intel stay native | prefix `AIConstruction.tryConstructNewBuilding` | `[Economy] BuildPlanner`, `GrowthWeights`, `PressureWeights`, `GrowthUntilTurn` |
| Research plan: the human's consensus opening from 6 recorded wins (`tools/human_research_order.py`), the Nuclear Missile line pulled forward, then the rest, then Asteroid Diversion. Prerequisites are queued by the game; native research resumes after the plan | prefix `AITechQueue.BeginTurn` | `[Research] UsePlan`, `Plan` |
| Smart defense: build the defense that counters the most visible, not-yet-covered enemy firepower (Kinetic vs Railgun/Coilcannon, Ion vs Missile/MIRV/Nuclear, Shield vs Laser) | prefix `AIDefenseConstruction.TryConstructDefense` | `[Economy] SmartDefense` |
| Lasers (EXPERIMENTAL, off): lift the two native bans and fix the dormant AI laser code (crash on an empty target list, wrong building aimed, projectile simulation of a never-set shot) | transpilers on `AIWeaponConstruction.TryConstructWeapon` and `AIWeaponTargeting.BeginTurn`, prefixes on `AILaserTrajectoryWeapon.setBuildingsToTry` / `Update`, `AITargetWeapons.CalculateTrajectory` | `[Lasers] EnableLasers` |
| Economy, build, research, superweapon and defense records in the decision log, to diagnose the turn-20 plateau | same hooks | `[Logging] DecisionLog` |

Why the research plan is not the human order verbatim: that order reaches Thermonuclear Warheads at about 24.5k cumulative science, and the strongest native AI in the 30 Oct match had 17k by turn 40.

## v0.3 - the economy director

The 31 Oct 2026 match (`docs/playtests/2026-10-31-v0.2.0.md`): by turn 25 the human made 31.5k energy and 13.8k material a turn, the best AI 3.1k and 1.25k. The human's engine was mines first, then 83 Floating Plants and 27 Offshore Mines, every one with yield upgrades (x1.75 then x2), plus city projects. Three native bugs kept the AIs from building the same:

1. A non-power building with no free power slot only raises `PowerSlotNeed` and reports success; the need branch then builds power plants on every call and never lowers the need. Result: Solar Plant spam, 1-3 mines all game.
2. Water-only buildings are placed next to a random land building, and the search never gives up. Result: 0 Floating Plants in 40 turns with the tech from turn 9.
3. A queued building's pre-installed upgrades are dropped at placement (`AIConstructBuildingAction` passes `{null, null}`), though their cost was budgeted.

v0.2.2 also fixed a v0.2 bug: the superweapon reserve saved energy, which resets every turn, so construction froze from turn 25.

| Change | Hook | Config |
|---|---|---|
| Economy director (`Brain/Director.cs`, adapter `DirectorPatches.cs`): one queued item per call, by value per cost. Yield upgrades and city projects (no power slot needed), then mines / offshore mines into free power slots and plants (Solar, Floating, Nuclear). Energy income is kept near 0.6x material income plus weapon upkeep. Upgrades and projects are charged to the decision budget (the native queue does not) | prefix `AIConstruction.tryConstructNewBuilding` | `[Director] Enabled`, `EnergyToMaterialTarget` |
| Military: no weapons before `WarFromTurn` (15) unless hit hard (retaliation, smaller upkeep cap); then up to `WarSpendShare` of the turn's energy on weapons and leftover energy too, under a firing-upkeep cap. Weapon type scored by damage per cost against the defenses of the strongest visible economy (32 kinetic defenses -> missiles). Defenses: a share of visible enemy weapons plus damage taken, the type from the smart-defense rule | same | `WarFromTurn`, `WarDamageTrigger`, `WarUpkeepShare`, `RetaliationUpkeepShare`, `WarSpendShare`, `DefensePerEnemyWeapon`, `DamagePerDefense`, `MaxDefenses`, `DefensesPerTurn`, `DefensesForFullCover` |
| Water placement: water-only buildings go next to our own water buildings with a free slot; plants (no power line needed) may also go anywhere on the planet | prefix `AIBuildingPlacementSearch.step` | - |
| Stuck placements give up after `PlacementStepCap` steps and the queue gets the cost back; water types then rest 3 turns | prefix `AIBuildingPlacementFinder.Update` | `PlacementStepCap` |
| Research default: the human's 31 Oct order (an unchanged v0.2 default in the config is migrated) | - | `[Research] Plan` |
| `director` record per AI turn: power slots, unspent energy, queued items, upgrades installed, refunds, placement failures | `AICore.EndTurn` postfix | - |

## Next

- Read the first v0.3 match log (`summarize_decisions.py` prints a director section): did the AIs build Floating Plants and upgrades, how much energy went unspent, how often did placement give up.
- Calibrate accuracy from the AIs' realized hit rate against the human's 58-67%.
- Lasers: one throwaway match with `EnableLasers = true` (the human won with 35 lasers).
- Population: filling a city's power connectors multiplies its population growth (x1.3 each); the director ignores it.
