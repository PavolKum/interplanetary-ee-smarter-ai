# AI v0.3 - review disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.3.md` (verdict: fix first). Spot-checked against `decompiled/` (project duration semantics in
`BuildingProjects.TryInstallProject`, `HasPotentialPower` in `PlayerResourceHandler`, power-line length check in
`PlayerPowerGridHandler`, slot reservation in `AIActions.ConstructBuilding`). All nine fixed in v0.3.1.

| # | Finding | Disposition |
|---|---|---|
| 1 | Floating Plants anchored to a water building take a slot the director counted as free | **Fixed**: water plants are placed only at random water positions (no power line, no anchor slot). Offshore mines still anchor to our water buildings with a free slot. Land plants anchor to unpowered / non-distributing buildings, which the slot count excludes |
| 2 | Random attempt after a failed anchored attempt keeps a stale power connection | **Fixed** by 1: plant searches never make an anchored attempt |
| 3 | A refund after construction ended is never spent | **Fixed**: when nothing is left to buy, construction stays open while queued placements resolve; a refund reopens decisions. Forecast income, weapon upkeep/spend, defense count and the superweapon purchase are undone on abandon |
| 4 | Superweapons bypass the firing-upkeep cap (700-800 a turn) | **Fixed**: bought only if all firing costs incl. its own stay under `SuperweaponUpkeepShare` (0.7) of energy income, otherwise energy push; its upkeep is added |
| 5 | Native intel module can queue building-with-upgrade variants (upgrades dropped, accounting off) | **Fixed**: a plain intel building (Telescope Array) through the director's own queue call |
| 6 | Project scores ignore recurring costs, global modifiers, "no duration = permanent" | **Fixed**: net of `turn_cost_*_addition`, flat additions x global modifier, duration semantics as the game |
| 7 | Damage-triggered defense needs a visible weapon; no counterintel | **Fixed**: fallback spreads over defense types when hit by unseen weapons; counterintel 1 per 12 non-city buildings from turn 8 (`CounterintelPerBuildings`, `CounterintelFromTurn`) |
| 8 | Valve counts all calls; exception falls back to native (power-plant loop) | **Fixed**: valve counts steps (2000); an exception ends construction for the turn instead of running native |
| 9 | `HasPower` too strict; planned upgrades missing from the multiplier | **Fixed**: `HasPotentialPower`; planned same-turn upgrades multiply into the next option's gain. Slot undercount from scheduled lines accepted (conservative) |

Unit tests (81/81, 6 mutations caught) cover `Brain/Director.cs`; all nine fixes are in the game adapter and are **unverified in game**.
