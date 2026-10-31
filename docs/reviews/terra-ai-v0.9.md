I’m tracing the new scoring and budget logic through the game’s actual upgrade, firing and research code. The key question is whether the director’s assumptions survive the native rules, rather than just whether the new arithmetic tests pass.
The largest issue so far is structural: science projects are filtered out before the new science weight can score them. I’m checking the upgrade queue and native stat stacking next, because those determine whether the offense changes can also stall construction or misprice upkeep.
## Verdict: **fix first**

**High confidence.** The science change cannot achieve its main goal: science-only projects never enter the candidate list. The offense scoring also favors efficiency gains that can immediately *reduce* damage, and its queue accounting has errors around affordability and pending upgrades.

## Findings

### High

**1. Science projects are filtered out before scoring.**  
`AI/DirectorPatches.cs:850-860` sources projects exclusively from native energy and material evaluations. Those evaluations accept only projects that improve their respective resource (`decompiled/Interplanetary.AI/AIProjectEntity.cs:178-185`). Science Institute and the other science projects have science modifiers but no positive energy/material yield (`docs/ruleset/embedded/enhanced/ProjectsData.csv:188-202`). Raising `ScienceWeight` therefore cannot make them purchasable. This explains the observed absence more directly than their previous price.

**Fix:** Enumerate unlocked city projects directly, including science-only projects, and retain native city-slot, availability and queue checks. Add an integration-level candidate test proving Science Institute is considered and can win.

### Medium

**2. Damage per firing energy is not sufficient as the primary upgrade objective.**  
`AI/Brain/Offense.cs:35-40,57-62` ranks relative efficiency, regardless of immediate damage, upgrade cost, power, target, or whether released upkeep can actually buy another weapon. Railgun EnergyCostDown ranks +60%, but cuts both damage stats by 20% (`docs/ruleset/embedded/enhanced/UpgradesData.csv:2,76-77`; its multiplier rows). At a weapon-slot, construction-budget or placement bottleneck, that is an immediate damage loss, potentially against already-intercepted fire. The director’s cap is self-imposed (`AI/DirectorPatches.cs:465-473,634-652`); `AIResources` does reserve firing energy, but that alone does not prove it is the binding constraint.

**Fix:** Score expected incremental delivered damage and energy saved against material/energy cost, target defenses and whether additional weapons can realistically use the freed upkeep. Prefer powered, useful weapons; treat energy efficiency as a component, not the sole objective.

**3. Upgrade selection can discard every affordable alternative; the material cap is not enforced.**  
`AI/DirectorPatches.cs:1069-1087` selects the highest gain *before* checking affordability. If that upgrade is unaffordable, `Step` marks the entire weapon-upgrade category failed for the turn (`:477-481`), even when cheaper upgrades are available. The share gate also checks only spending *before* the next purchase (`:477-478`), so one upgrade can exceed 30% by its full price.

**Fix:** Filter by current budgets and remaining share before ranking; retry the next candidate on a queue failure. Check `spent + candidateCost <= cap`.

**4. Pending upgrades and global modifiers make same-turn upkeep and scores inaccurate.**  
`AI/DirectorPatches.cs:1055-1074,1092` reconstructs from base data and installed upgrades only. Two upgrades queued for one weapon are evaluated independently; the second upkeep delta ignores the first. Global firing-cost and damage modifiers are also omitted, although actual weapon stats apply them (`decompiled/Interplanetary.Prefabs/BuildingWeaponStats.cs:12-45`). Rounding is omitted. Next turn `AIResources.BeginTurn` remeasures actual costs (`decompiled/Interplanetary.AI/AIResources.cs:44-62`), but this turn’s cap and economy decisions can be wrong. A cost-increasing upgrade can also cross the cap: there is no candidate-level upkeep check.

**Fix:** Model installed **and queued** effects with actual global modifiers and game rounding; reject increases that breach the cap. Clear/reconsider the step-5 weapon failure after an upkeep-reducing upgrade, otherwise freed capacity may only be used by the later weapon sink (`AI/DirectorPatches.cs:469-499`).

**5. Once projects are reachable, their science value and pace need better grounding.**  
`AI/DirectorPatches.cs:877-878` estimates a local city project as total science divided by city count. That includes telescope/hub science and ignores unequal city populations and existing city modifiers; the game applies the local multiplier to the particular city (`decompiled/Interplanetary.Prefabs/BuildingProjectStats.cs:63-78`). Further, the pace calculation always uses the configured plan even when `UsePlan` is off (`AI/DirectorPatches.cs:203-216`; compare `AI/EconomyPatches.cs:464-469`).

The default plan totals **33,100 science**; the v0.8.1 opening produced 279/turn (`docs/playtests/data/v081-ai-decisions.jsonl:4`). A near-maximum science weight from an early turn is therefore plausible, competing directly with compounding energy/material growth (`AI/Brain/Director.cs:68-108`). The practical effect remains untested.

**Fix:** Price each eligible city’s actual incremental science, account for queued projects and duration, and gate plan pacing on the enabled/applicable plan. Compare growth and research milestones in a playtest.

### Low

**6. Tests cover formulas, not the integration that matters.**  
`AI/Tests/Program.cs:436-462` checks hand-built profiles and weight arithmetic; it cannot catch project enumeration, actual ruleset effects, affordability, pending queues, cap breaches or game-stat rounding. Add focused tests for those paths, including a red-before-fix regression. Tests were **not run**: the requested review was strictly read-only, and `dotnet run` can create build artifacts.

## Checked and fine

- **Native stat effects:** projectile and fragment additions and firing multipliers flow through building stats to gameplay; modifiers multiply in `NyaStat` (`decompiled/Interplanetary.Prefabs/BuildingUpgradeStats.cs:99-108`, `decompiled/NyaEngine.Stats/NyaStat.cs:350-375`). Projectile count produces multiple action-phase projectiles; fragments produce separate impact/interception checks. Do not assume all weapons fire: power, valid trajectories and resources matter (`decompiled/Interplanetary.AI/AIWeaponTargeting.cs:143-150`, `AIWeaponTargetingGoal.cs:78-102`, `AIActions.cs:130-142`).
- **Upgrade exclusivity:** the examined native install path exposes two slots and no tier-chain requirement; `GetAvailableBuildingUpgrades` is an owner list, while `IsUpgradeAvailable` checks unlock availability (`UpgradesData.cs:51-59`, `PlayerAvailableBuildings.cs:103-105`, `BuildingUpgrades.cs:105-140`). Radius tiers may coexist if unlocked and slots are free.
- **Science additions:** the +20 upgrades are correctly gated on potential power and multiplied by the global science modifier, subject to game rounding (`AI/DirectorPatches.cs:750-755,821`; `decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:45-52,240-252,296-305`).
- **Research plan:** all 39 Enhanced entries have their listed prerequisites earlier; no v0.4 entry was dropped, and Magnetic Saturation Elimination was added. Exact unchanged v0.4 defaults migrate; customized strings remain (`AI/Plugin.cs:102-121,226-234`). Cost-minus-progress and per-turn science use compatible units (`PlayerTechnologyHandler.cs:219-253,405-412`).
- **Fairness:** the new science and upgrade paths use the AI’s own state and ruleset data; I found no new hidden-enemy read in the reviewed diff.
