# AI v0.2 - review disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.2.md` (verdict: fix first). Findings 1-3 were checked against `decompiled/` before changing code. Fixed in v0.2.1.

| # | Finding | Disposition |
|---|---|---|
| 1 | Buying a superweapon with no free power slot raises `PowerSlotNeed`; the native need branch never lowers it within a turn, so the savings go on power plants | **Fixed** (verified `AIConstruction.cs:128-176, 224`, `AIConstructionModule.cs:177-183`). When affordable but no free slot: keep the goal reserved, build one generator via the native `tryConstructPowerGeneratingBuilding`, buy next turn. A buy attempt also refuses when the slot was used earlier that turn. |
| 2 | The research plan pre-empts native urgent intel research; no preferred-intel construction | **Fixed**: the plan yields when `AIShortTermStrategy.IsMoreIntelTechRequired` (private, read by reflection); the build planner keeps the native preferred-intel weight 6 when `!HasPreferredIntel`. |
| 3 | After a failed category, restoring only the needs loses the shortage signal | **Fixed**: needs and attempted marks are kept, as if native had rolled that category (native then takes its need branch). |
| 4 | Lasers: Shadow targets via `IsBuildingVisible`; Solar Laser fires from the Sun but the native check starts at the building | **Partly fixed**: `Visible` only. Solar Laser firing position **open**; lasers stay off by default and are untested. |
| 5 | Laser patches ignore the offline guard; install does not check IL matches | **Fixed**: ban string and IsLaser skip are swapped only when `Plugin.Active`; install requires 3/3 construction and 1/1 targeting IL matches, otherwise it unpatches itself. |
| 6 | Defense telemetry counts a defense that only raised a power need | **Fixed**: counted only on a real queue addition. |

Unit tests (58/58) cover the Brain only; all six fixes are in game adapters and are **unverified in game**.
