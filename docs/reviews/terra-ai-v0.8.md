I’m tracing the nuclear damage path and the actual placement/budget rules against the decompiled game; the key question is whether the intended city protection survives contact with those mechanics.
The main defect is already clear: the anti-nuke shortfall is added to a total-defense target that also counts existing Ion defenses, so the target can fall as Ion is built and stop construction early. I’m checking the consequences for coverage, first-volley energy, and city-project alternatives.
## Verdict: **Fix first** — confidence: high

The anti-nuke objective can stop short before the requested Ion defenses are built, and its count does not establish that any city is covered. Both defects affect the scenario v0.8 is intended to address.

### High

**1. The total-defense gate can suppress anti-nuke construction.** `AI/DirectorPatches.cs:194–211, 381–385, 545–549`  
The target adds the *remaining* Ion shortfall, but the gate compares it with **all** owned defenses, including those Ion defenses. For example, with a base target of 20, 20 Kinetics, zero Ions and five wanted, the first turn can queue three Ions. Next turn the target is `20 + (5−3) = 22`, while 23 defenses are owned: construction stops two Ions short. An existing surplus of conventional defenses can suppress it immediately.

**Fix:** gate the anti-nuke deficit independently, or calculate a stable total target from non-Ion defenses plus the wanted Ion count. Explicitly decide whether the base target is inclusive or additive. Cover multi-turn construction and existing-defense surplus in tests.

**2. “Two per city” is a planet-wide inventory target, not city protection.** `AI/DirectorPatches.cs:205–211, 537–549, 1038–1065`  
Existing Ions anywhere count toward the quota, including **unpowered** normal buildings (`AI/EconomyPatches.cs:138–143`). Ions built before T28 may be far from cities. Even newly placed defenses merely maximize a weighted score; buildings can outscore a city and there is no per-city minimum. Thus five owned Ions can satisfy the prior with zero protected cities.

**Fix:** measure powered and successfully queued anti-nuke coverage **per living city**, prioritize the least-protected city, and base the deficit on coverage rather than inventory. Log per-city coverage and placement failures.

### Medium

**3. Center coverage overstates protection against splash.** `AI/Brain/Coverage.cs:156–177`; `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:223–228, 264–275`; `decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:302–315`  
Interception checks the **impact point**, not the city center. Enhanced Ion radius is 5.3; nuclear blast radius is 3.7 and city radii are 1.7–2.1 (`docs/ruleset/embedded/enhanced/DefensesData.csv:5`, `WeaponsData.csv:11`, `CitiesData.csv:13`). A blast centered as far as roughly 5.4–5.8 from a city can touch it, beyond even an ideally centered Ion’s reach. A merely center-covering Ion leaves larger gaps.

**Fix:** score sampled damaging impact points or use a conservative geometric coverage model, including multiple overlapping Ions. Treat the resulting probability as an estimate; validate actual interception and city losses in-game.

**4. The T28 prior is a playtest heuristic, not a tech bound.** `AI/Plugin.cs:283–288`; `docs/ruleset/embedded/enhanced/TechTreeData.csv:4, 8–9`  
Thermonuclear Warheads costs 3,000 science; its prerequisite chain totals 5,250. Research speed and route determine the turn, so a rush can arrive before T28. Hidden launchers cannot trigger the visible-only detector (`AI/EconomyPatches.cs:108–115`; `AI/Patches.cs:164–167`), and a detected impact is acted on at the next AI turn—potentially after several cities are lost.

**Fix:** move the prior earlier or make it configurable against observed match pacing; log first nuke sighting, first impact, city coverage and city losses. T28 is plausible for the reported match, but not a demonstrated safety threshold.

**5. First-volley funding can be inadequate.** `AI/DirectorPatches.cs:301–321`; `AI/Brain/Director.cs:225–248`  
Ion rolls cost 200 energy, but the unseen-threat reserve estimates 150 per defense and is capped at 30% of income; observed spending only helps **after** the volley. Superweapon purchasing can reduce the reserve further. Actual rolls consume resources sequentially (`decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:229–247`), so the nominal 96% for two Ions requires both rolls to be affordable.

**Fix:** include the cost of expected Ion rolls in the pre-attack reserve, and log unpaid nuclear rolls separately. Exercise low-income and superweapon-purchase cases.

**6. City projects are a useful complement, currently ignored.** `AI/DirectorPatches.cs:766–795`; `docs/ruleset/embedded/enhanced/ProjectsData.csv:180–187`  
DDI and City Barrier are permanent, cost 250 energy/1,000 material, and provide 50% nuclear interception at radii 4.8 and 5.1 respectively; Barrier also counters asteroid and solar weapons (`DefensesData.csv:5–7, 18–41`). The director’s project path requires economic yield, so it excludes them. Only one defense functionality is selected per city (`decompiled/Interplanetary.Prefabs/BuildingProjects.cs:271–278`).

**Fix:** consider one unlocked project per suitable city alongside Ions, accounting for project slots, 250-energy rolls, coverage gaps and competition with economy projects.

**7. Kinetic placement changes too.** `AI/DirectorPatches.cs:1023–1029, 1145–1148, 1180–1181`; `AI/Brain/Coverage.cs:171–177`  
The new marginal score gives covered buildings residual value, so dense clusters can attract another Kinetic before an uncovered area. That is mathematically reasonable but may regress the v0.7 coverage improvement. For Shield, taking the **best** counter probability also assumes 0.95 against weapons it actually stops at 0.15.

**Fix:** compare Kinetic uncovered coverage and interception outcomes against v0.7; use threat-specific probabilities rather than a defense’s maximum.

### Low

**8. Tests exercise formulas, not the integrated behavior.** `AI/Tests/Program.cs:365–393`  
They cover synthetic stacking, requested counts and ledger recording, but not the total-defense gate, powered/per-city coverage, placement geometry, reserve, visibility, unavailable Ion, or Classic selection.

**Fix:** add regression cases for findings 1–2 first, then adapter/in-game checks for geometry and interception. Tests were **not run**: build/test execution would write files, contrary to the read-only instruction.

### Checked and fine

- Nuclear impacts reach `IsHitIntercepted` with the original weapon data (`decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:312–341`). Enhanced nuclear missiles default to one fragment (`WeaponsData.csv:12`; `WeaponSurfaceModule.cs:18–24`).
- **Base nuclear fallout does not bypass interception** (`WeaponAndBuildingEffectsData.csv:9`); the fallout-ignore upgrade can change that (`UpgradesData.csv:150`). With that upgrade, interception still prevents direct blast damage, but not fallout; exact city survival depends on impact and fallout geometry.
- Enhanced Ion is the strongest **buildable** nuclear counter at 0.8; the stacking formula is correct for independent, powered, affordable rolls at the same impact point (`DefensesData.csv:10–41`; `DefenseGameplayData.cs:21–37`).
- If Ion is unavailable, selection falls back to other affordable candidates (`AI/DirectorPatches.cs:525–553`). In stock Classic, neither the nuclear weapon nor an anti-nuclear building counter exists, so the anti-nuke ID is null (`docs/ruleset/embedded/classic/WeaponsData.csv:1–2`, `DefensesData.csv:10–15`).
- The match’s raw v0.7.3 decision and telemetry files were not available inside the repository; the timing assessment relies on the supplied playtest note. In-game behavior remains **unverified**.
