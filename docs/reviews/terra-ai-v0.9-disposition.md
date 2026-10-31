# AI v0.9.0 code review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.9.md` (fix first; wrapper accepted after 115 s). Fixed in v0.9.1.

| # | Finding | Disposition |
|---|---|---|
| High 1 | Science projects never reach the director: native evaluations list only energy/material projects (AIProjectEntity.IsValidFor*) | **Fixed**: science projects listed from ProjectsData.GetAvailableCityProjects per own city type (unlocked only), queued through the same native tryBuildAffordableProject (slot/queue checks). This, not the price, explains zero science projects in six matches |
| Medium 2 | Damage per firing energy alone can buy immediate damage losses | **Fixed**: an upgrade that lowers damage per shot (EnergyCostDown) is taken only when upkeep is >= 85% of the cap; efficiency still ranks the rest |
| Medium 3 | Best pick before affordability; one failure blocks the step; share checked before, not with, the purchase | **Fixed**: budget, share (spent + cost <= cap) and upkeep cap filter before ranking; next candidate tried on queue failure |
| Medium 4 | Queued upgrades ignored; global firing modifier omitted; cost increases can cross the cap; freed upkeep unused | **Fixed**: queued effects applied to the profile, firing delta x global EnergyFiringCost, increases past the cap skipped, step-5 "weapon" failure cleared after a saving. Game rounding not modelled |
| Medium 5 | Local project science = total / cities; pace uses the plan even when UsePlan is off | **Fixed**: per city type: average population x science_yield_per_population x global; pace only with UsePlan. Plan totals 33,100 science: weight likely near max early; to watch in play |
| Low 6 | Tests formula-level | Partly: ImmediateDamageRatio tests; integration via the next match (log: scienceWeight, planScienceLeft, weaponUpgrades, project:pro_Science*) |

215 checks.
