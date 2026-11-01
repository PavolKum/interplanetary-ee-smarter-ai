I’m auditing the full v0.14.2 code at `f156892`, tracing adapter assumptions against the engine and checking the match data. I’ll distinguish confirmed bugs from strategic risks and rank the fixes by their likely impact on the next match.
Two findings sharpen the diagnosis: the city-by-city Ion policy activates for Nuclear Missiles, but not ordinary Missiles, and the first lethal volley recorded zero unpaid defense rolls. I also found that `target.cities = 0` measures whether the population total is readable—not whether individual city positions were ever seen—so that log field alone cannot prove the AI never saw a city.
Verdict: fix first

**The economy is now strong enough to win. The mod still lacks a reliable path from production to city kills—and from defense inventory to city survival.** Fixing research queue clearing does not address those two conversion failures.

Audited HEAD: `f1568928167370843f0ca7b89bafade6821f9463`, including all seven adapters, all Brain files, test coverage, the latest two matches, historical dispositions, and the relevant engine implementations.

**Confidence:** High in the findings marked CONFIRMED; Medium in the predicted ordering of win-rate improvements. CONFIRMED means the defect or missing policy is established by code/data, not that its contribution to the latest loss is measured.

No files were written or changed by this review. Builds, tests, and mutations were not run because they would violate the read-only constraint. Installed configuration and live-game behavior remain unverified.

## What the latest match actually proves

Three distinctions matter before changing anything:

1. **The first lethal volley was a coverage failure, not a defense-energy failure.** Before the T23 attack, Bolide had five cities and eleven operational Ions, but only two cities counted as Ion-protected. Fourteen incoming Missile impacts produced four interceptions, eight impacts with no eligible defense, and **zero unpaid rolls**. Population fell **2,740 → 860**, with two cities lost.  
   Evidence: `docs/playtests/data/v0141-ai-decisions.jsonl:149–160`.

2. **`target.cities = 0` is not a count of individually visible city targets.** It means the population thermometer was unreadable. The producer counts and counterintel timeline strongly support the blindness diagnosis, but that field is the wrong instrument for proving it.  
   Evidence: `AI/Patches.cs:199–219`; `decompiled/Interplanetary.PlayerDataManagement/FrozenIntelState.cs:63–73`.

3. **The binding spending constraint changed during the match.**

   | Turn | Director energy left | Director material left | Interpretation |
   |---|---:|---:|---|
   | 20 | 8,740 | 32 | Material-bound |
   | 21 | 3,300 | 69 | Material-bound |
   | 22–26 | 23–40 | 4,637–18,021 | Director spent essentially its entire energy allocation |
   | 27 | 5,283 | 20,603 | Thirty-weapon cap bound |
   | 28 | 6,183 | 26,933 | Thirty-weapon cap bound |

   These are planning balances **after reservations**, not physical end-of-turn energy. The diagnosis “5–9k unused every turn from T20 because of weapon caps” is too broad.  
   Evidence: `v0141-ai-decisions.jsonl:116,127,135,151,159,167,175,183,194`; `AI/DirectorPatches.cs:443–468,1917–1935`.

---

# High findings

## 1. CONFIRMED — Ordinary Missiles bypass the city-by-city protection policy

**Evidence**
- Ours: `AI/DirectorPatches.cs:292–327,527–542,1573–1594`.
- Ordinary counter quantity: `AI/Brain/DefensePlan.cs:66–93`.
- Engine: `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:223–247,264–287`.
- Activation delay: `decompiled/Interplanetary.Prefabs/BaseBuilding.cs:995–999`.

The special placement and per-city deficit policy activates for **Nuclear Missiles**, or its late nuclear prior. Conventional Missiles only increase ordinary defense demand. That demand can be satisfied by Ions anywhere on the planet.

**Failure scenario:** Eleven visible Missiles trigger Ion purchases, but not a requirement to cover every city. Eleven Ions can coexist with three exposed cities—as happened immediately before the decisive volley. Five more queued on T23 cannot protect that action phase.

**Fix:** Generalize the policy to city-threatening guided weapons, beginning with ordinary Missiles. Require matching, powered interception opportunities at each city’s relevant impact locations before ordinary military expansion. Close this deficit **before** the volley turn.

Do not interpret “at city centre” literally: collision rules require legal adjacent placement. Also retain the distinction between covering a city-centred hit and covering every nearby blast capable of damaging it.

## 2. CONFIRMED — Intel investment targets readable totals, has a hard ceiling, and can actively reduce its own effectiveness

**Evidence**
- Ours: `AI/DirectorPatches.cs:330–336,518–524,1026–1028,1074–1096,1132–1167`.
- Defaults: `AI/Plugin.cs:149–157,326–327`.
- Engine: `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:176–198`; `decompiled/Interplanetary.Prefabs/BuildingStatHolder.cs:123–129`; `decompiled/Interplanetary.Prefabs/BuildingUpgradeStats.cs:76–85`.

There are three connected defects:

- `BlindEnemies` tests aggregate economy/population readability, not acquisition of city coordinates.
- The director buys capped numbers of plain Telescopes, but does not value pure intel upgrades or projects.
- It buys the Telescope science upgrade without pricing its **×0.75 intel penalty**. The equivalent Data Security Hub science upgrade sacrifices seven counterintel.

**Failure scenario:** Thirty bare Telescopes provide **240 intel**; science-upgraded ones provide **180**. At the human’s approximately ×11.725 counterintel, a big city requires about **352**, and a small city about **528**. The director cannot buy its way out under that policy.

The latest log explicitly queues ten Telescope science upgrades at T23: `v0141-ai-decisions.jsonl:151`.

**Fix:** Make intel investment threshold- and objective-driven: acquire city coordinates early, maintain weapon/counter visibility where useful, and price intel/counterintel gains and losses. Include Lidar, Observation Agency, and global intel projects. Reject visibility-destroying science trades while their lost capability matters.

## 3. CONFIRMED — The guided-target override can suppress native targeting of remembered cities

**Evidence**
- Ours: `AI/Patches.cs:228–231,460–467,490–502`.
- Native choice: `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:412–485`.
- Shadow persistence: `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:183–207`.
- Frozen observations: `decompiled/Interplanetary.PlayerDataManagement/ShadowBuildingFrozenState.cs:39–63`.

The mod accepts only currently Visible buildings. Native second-phase targeting accepts cities that are **not Invisible**, including Shadows.

**Failure scenario:** A city was observed earlier and is now a Shadow. One enemy weapon is currently Visible. The mod chooses that weapon and suppresses native targeting, even though native targeting could have used the remembered city.

If neither cities nor weapons are candidates, native fallback does run. With no known targets, its final fallback is a **random point on the sphere**, not a useful inference about city positions: `AIWeaponTargeting.cs:483–496`.

**Fix:** Include legitimate remembered city positions and frozen observations. Never rank a Shadow using hidden live population or HP. Log visible-city candidates, shadow-city candidates, and the selected identity separately.

## 4. CONFIRMED — Guided volleys have no damage allocation and repeatedly overkill the same target

**Evidence**
- Ours: `AI/Brain/Memory.cs:173–191`; `AI/Patches.cs:460–467`.
- Deferred actions: `decompiled/Interplanetary.AI/AIActions.cs:43–49,172–175`.
- Exact guided impact: `decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:145–148`.
- Destruction thresholds: `decompiled/Interplanetary.Prefabs/BaseBuilding.cs:941–985`.

Every targeting call selects the same weakest city or weapon from unchanged durability. No damage is reserved against a target between calls.

**Failure scenario:** At T22, eight consecutive strike records select a weapon with **330 HP from eleven candidates**: `v0141-ai-decisions.jsonl:137–144`. With stable candidate order, the deterministic selector repeatedly chooses the same building. Two base Missile hits provide 400 direct damage; six additional direct hits contribute nothing to destroying that target, although splash may still matter.

Acquiring city coordinates alone does not solve this: a whole volley can instead concentrate on one already-doomed city.

**Fix:** Maintain a per-volley target allocation. Reserve expected damage, account conservatively for interceptions and Shadow uncertainty, then move to the next kill. Include weapon/projectile identity and target ID in the allocation and log.

## 5. CONFIRMED — Native hit-list ordering can starve late-built superweapons indefinitely

**Evidence**
- Ours: `AI/Patches.cs:310–312`; strategy credits all ballistic projectiles at `AI/StrategyPatches.cs:169–186`.
- Engine insertion order: `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:79–106`.
- Engine hit selection: `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:129–145`.

The firing-list refresh fixes stale lists, but retains native **first-in-list** selection up to the accuracy quota.

**Failure scenario:** Thirty old conventional weapons and one newly built Nuclear Missile all find primary trajectories. At 70% accuracy, about 21–22 hit slots are available. The old weapons fill every slot before the Nuclear Missile is considered. This can recur every turn. Strategy nevertheless credited the superweapon when estimating the salvo.

**Fix:** Preserve the accuracy quota, but select hitting weapons by strike value. Prioritize ready superweapons and useful kill contribution, then rotate equivalent conventional weapons. Accuracy should constrain the planned volley without silently wasting its best weapon.

## 6. CONFIRMED — Repair protects base firing costs, not the engine’s live firing bill

**Evidence**
- Ours: `AI/DirectorPatches.cs:800–805,833–843`.
- Native reservation: `decompiled/Interplanetary.AI/AIResources.cs:44–62`.
- Live modifiers: `decompiled/Interplanetary.Prefabs/BuildingWeaponStats.cs:12–27`; `BuildingUpgradeStats.cs:99–107`.
- Payment: `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:230–260`.

The v0.13 repair fix is incomplete: it sums `WeaponData.firing_cost_energy`, while native firing uses `ShootingModule.EnergyFiringCost`.

**Failure scenario:** Ten Railguns with both projectile upgrades can cost **504 energy each**, rather than 100. After power losses, 4,000 available energy and a 1,000-energy repair bill leave 3,000. A seven-weapon hit assignment costs 3,528, so later firing payments fail.

Cost reductions produce the opposite error: repairs are unnecessarily restricted. Material firing costs are also absent from the repair protection calculation.

**Fix:** Use live shooting-module costs and an explicit operational-weapon policy. Protect firing material as well. Reconcile repair, firing, defense, and recovery allocations using the same authoritative costs.

## 7. CONFIRMED — City Barrier is outside both construction and counter-research policy

**Evidence**
- Ours: `AI/DirectorPatches.cs:142–162,705–752,1124–1167`; `AI/EconomyPatches.cs:565–570`.
- Engine city-project defenses: `decompiled/Interplanetary.Prefabs/BuildingProjectStats.cs:47–60,96–102`.
- Interception eligibility: `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:264–287`.
- Ruleset: `docs/ruleset/embedded/enhanced/DefensesData.csv`, entries `def_CitySuperWeaponDefense`; `ProjectsData.csv`, `pro_CitySuperWeaponDefense`.

The counter table contains defense **buildings**. Project enumeration admits energy, material, and science projects. City Barrier enters neither path.

**Failure scenario:** An enemy switches to Asteroid Diversion or Solar Laser. Sixty ordinary defenses still provide **zero eligible interceptions** against either. In Enhanced, City Barrier is the only listed counter: **70% against Asteroid, 50% against Solar Laser**.

**Fix:** Add city-specific defensive projects to threat response and counter-research. Preserve suitable project slots and fund their 250-energy rolls. Research completion alone must not count as protection.

## 8. CONFIRMED — Experimental lasers can bypass far-side occlusion

**Scope:** Lasers enabled; not responsible for the latest default-settings loss.

**Evidence**
- Ours: `AI/LaserPatches.cs:149–165`.
- Native helper checks the hit planet only: `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:205–219`.
- First intersection: `decompiled/Interplanetary.Weapons.Trajectories/TrajectoryCalculator.cs:207–233`.
- Human rejection of an occluded point: `decompiled/Interplanetary.Views/PhaseTwoTargetingLogic.cs:162–176,237–244`.
- Action phase trusts the submitted laser position: `decompiled/Interplanetary.GameEngine/ActionphaseManager.cs:501–508`.

**Failure scenario:** A visible building is on the far hemisphere. The ray toward it hits the near side of the same planet, satisfying `findLaserTrajectory`. The patch nevertheless submits the requested building position. The action phase manufactures a hit there.

**Fix:** Apply the human path’s first-surface validation and shooting origin. Submit the validated intersection, and reject an occluded building point.

---

# Medium findings

## 9. CONFIRMED — The arsenal cap prevents adaptation, while upgrade policy can reduce damage at that cap

**Evidence**
- Ours: `AI/DirectorPatches.cs:856–884,887–906,1391–1446`.
- Engine sale API: `decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:623–675`.
- Actual upgrade effects: `decompiled/Interplanetary.Prefabs/BuildingUpgradeStats.cs:99–107`.

At thirty aimed weapons, a better type cannot be purchased until an old weapon disappears. There is no replacement policy. Separately, an upkeep-bound arsenal may take a damage-reducing cost-saving upgrade even when the weapon cap prevents spending the freed upkeep on another weapon.

**Failure scenario:** Thirty early Railguns cannot transition to Missiles against a Kinetic wall. Or EnergyCostDown reduces immediate output at the cap. The latter purchase appears at T26: `v0141-ai-decisions.jsonl:175`.

**Fix:** Allow bounded replacement of clearly inferior weapons. When search slots bind, optimize useful damage per slot and reject damage-reducing savings unless a concrete beneficial use exists. Make cap expansion conditional on delivered-shot performance.

## 10. CONFIRMED — Destroyed retained entries inflate accuracy quotas

**Evidence**
- Ours: `AI/Patches.cs:252–266`; live filtering occurs only later at `385–390`.
- Engine retains targeting entries: `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:61,79–110`.
- Quotas use retained count: `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:37–60`.

**Failure scenario:** Fifty retained entries but twenty-six live powered weapons yield a 70% quota of thirty-five hits. All twenty-six survivors can be assigned hits, violating configured human-parity accuracy. Strategy still models them at 70%.

**Fix:** Derive quotas and selection from live operational weapons. If removing calculator entities, preserve or rebuild their associated IDs correctly.

## 11. CONFIRMED — Superweapon affordability can override the defense reserve

**Evidence**
- Ours: `AI/DirectorPatches.cs:455–467,495–499,608–634`.
- Engine defense affordability: `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:215–247,283–287`.
- Resource semantics: `decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:92–105`.

**Failure scenario:** Available energy after firing reservation equals the superweapon’s build cost. Line 464 reduces the defense reserve to zero to make the purchase fit. The AI spends its current protection on a weapon that becomes operational next turn.

New-weapon upkeep estimates also use raw firing cost at `DirectorPatches.cs:907–908,1478–1482`, omitting applicable global firing modifiers.

**Fix:** Make critical defense funding a purchase precondition. Defer the superweapon rather than shrinking that funding. Use effective projected firing costs for all new purchases.

## 12. CONFIRMED — Counter-research is one threat refresh late and confuses membership with priority

**Evidence**
- Ours: `AI/EconomyPatches.cs:84–145,485–508`.
- Engine module order: `decompiled/Interplanetary.AI/AICore.cs:103–110,162–168`.
- Research occurs earlier: `decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:446–456`.
- Membership and append semantics: `decompiled/Interplanetary.PlayerDataManagement/PlayerTechnologyHandler.cs:290–296,480–518`.

**Failure scenarios:**
- Newly visible weapons or last action phase’s impacts enter `EnemyWeapons` after the research prefix has already run.
- An urgent counter already queued behind unrelated research is not promoted, because `GetTechnologyIsInQueue` merely returns true.

**Fix:** Refresh threat evidence once per player/turn before counter-research. Check whether unrelated work precedes the urgent prerequisite chain, not merely whether its target exists somewhere in the queue.

## 13. CONFIRMED — Defense upgrades are never selected

**Evidence**
- Ours: `AI/DirectorPatches.cs:1026–1028,1095–1096,1394–1398`.
- Engine applies radius, activation-cost, and probability upgrades: `decompiled/Interplanetary.Prefabs/BuildingUpgradeStats.cs:126–135`.
- Ruleset: `UpgradesData.csv`, `upg_KineticRange1`, `upg_IonRange1`, and defense cost/probability upgrades.

**Failure scenario:** The AI reaches count/placement limits while cities remain poorly protected. Researched range and roll-efficiency improvements cannot be purchased through either the economy or weapon-upgrade selector.

Kinetic range changes **5.9 → 7.67**, roughly **69% more covered area**, with probability ×0.9. Ion range changes **5.3 → 7.155**, roughly **82% more area**, with probability ×0.95.

**Fix:** Add a defense-upgrade selector based on critical-impact coverage and funded interceptions, including each upgrade’s downside.

## 14. CONFIRMED — Population growth and combat city projects have no investment policy

**Evidence**
- Ours: project enumeration at `AI/DirectorPatches.cs:1124–1167`; ordinary placement at `1785–1818`.
- Engine growth: `decompiled/Interplanetary.Prefabs/BuildingStatHolder.cs:108–115,417–529`.
- Project effects: `decompiled/Interplanetary.Prefabs/BuildingProjectStats.cs:65–79,88–93`.
- Multiplication: `decompiled/Interplanetary.PlayerDataManagement/GlobalStatModifier.cs:53–59`.

Population-only and combat-only projects never become candidates. Placement does not value filling city connectors.

**Failure scenario:** The AI has spare material but cannot purchase Vertical Farms, population medication, global population damage, or firing-cost projects. Permanent economy projects can occupy slots those options later need.

**Numbers:**
- One filled connector: approximately **13 population/turn**.
- Three filled connectors plus defended bonus: approximately **29**.
- Vertical Farms multiplies that by **1.5**.
- Five permanent population-damage projects: **×1.1⁵ = 1.61**.
- Five permanent firing-cost projects: **×0.9⁵ = 0.590**.
- Five temporary reallocation projects: **×0.75⁵ = 0.237**.

**Fix:** Add explicit growth/combat candidates and project-slot opportunity costs. Value city connector completion in placement. Do not assume five filled connectors are an opening benefit: the fifth unlocks at maximum population.

## 15. CONFIRMED — Queued global projects are forecast additively, and discounted value is treated as actual income

**Evidence**
- Ours: `AI/DirectorPatches.cs:976–982,1184–1208`.
- Actions execute later: `decompiled/Interplanetary.AI/AIActions.cs:43–49`.
- Multiplicative stacking: `decompiled/Interplanetary.PlayerDataManagement/GlobalStatModifier.cs:53–59`.
- Applied global effects: `decompiled/Interplanetary.Prefabs/BuildingProjectStats.cs:74–80`.

Each queued project is evaluated against unchanged live production. Its duration-discounted gain is then added to the income forecast used by subsequent purchases.

**Failure scenario:** From 1,000 energy income, five ×1.3 SSTAR projects produce approximately **3,713**. The director credits five `1,000 × 0.3 × 0.6` gains and forecasts **1,900**. Associated material penalties and depletion consequences likewise need projected composition.

**Fix:** Track projected global multipliers and actual income deltas separately from discounted scoring value. Forecast all affected resources and planetary drain.

## 16. CONFIRMED — Economy upgrade scoring omits operating-cost changes

**Evidence**
- Ours: `AI/DirectorPatches.cs:1074–1096`.
- Engine net production: `decompiled/Interplanetary.Prefabs/BuildingStatHolder.cs:135–139`.
- Cost modifiers apply: `decompiled/Interplanetary.Prefabs/BuildingUpgradeStats.cs:78–85`.

**Failure scenario:** Nuclear yield upgrades increase energy **200 → 700**, but material consumption **50 → 135 per turn**. Scoring misses the extra 85 material. Fast Breeder Reactors, which save **40 material/turn** on a base plant, receive no positive gain and are discarded.

**Fix:** Score marginal net production, including operating additions/multipliers and installed/planned effects—not yield changes alone.

## 17. CONFIRMED — Project eligibility is checked on one city, but native purchase can select a ruin

**Evidence**
- Ours: `AI/DirectorPatches.cs:1214–1223,1353–1361`.
- Native re-selection has no Normal-state check: `decompiled/Interplanetary.AI/AIConstructionModule.cs:250–284`.
- Slot tracking also lacks that check: `decompiled/Interplanetary.AI.Construction/AIProjectConstruction.cs:24–36`.
- Ruined cities skip project BeginTurn: `decompiled/Interplanetary.Prefabs/BaseBuilding.cs:1033–1046`.
- Installation still charges: `decompiled/Interplanetary.Prefabs/BuildingProjects.cs:190–237`.

**Failure scenario:** A living city passes `HasCityForProject`, but an earlier ruin of the same city type retains free project slots. Native selection buys the project on that ruin. Local resource gains are wasted; project lifecycle can stall.

**Fix:** Retain the actual eligible city and queue directly against it. Revalidate its state, slots, and duplicate status immediately before queueing.

## 18. CONFIRMED — Floating Plants bought for grid recovery are placed without a recovery connection

**Evidence**
- Ours: `AI/DirectorPatches.cs:502–513,1271–1279,1801–1807`.
- Native random placement: `decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs:167–192`.
- Placement connection is passed through: `decompiled/Interplanetary.AI.Actions/AIConstructBuildingAction.cs:27–34`.
- Reconnection constraints: `decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs:63–72`.

**Failure scenario:** A coastal network becomes disconnected and land recovery placements fail. Floating recovery plants are randomly scattered elsewhere. Five purchases can consume **1,250 energy / 1,500 material** without reconnecting the intended buildings.

**Fix:** Give recovery plants an explicit disconnected-grid anchor and retained connection. Use random water placement for expansion, not as evidence that grid repair succeeded.

## 19. CONFIRMED — Abandonment reverses forecast gains that were never credited, or reverses too much

**Evidence**
- Ours: `AI/DirectorPatches.cs:507–510,976–982,1243–1252,1863–1880`.
- Native queue spends allotments, not forecast income: `decompiled/Interplanetary.AI/AIConstructionQueue.cs:149–159`.

**Failure scenarios:**
- Recovery plants do not receive `TryEconomy` income credit, but abandonment subtracts their full yield.
- A Mine credited +20 material after planet discount can have 100 subtracted on abandonment, leaving the forecast 80 below its starting value.

The resource refund itself is correctly scoped; the defect is forecast rollback.

**Fix:** Retain the exact credited deltas and role for every pending placement. Reverse those deltas, rather than reconstructing them from base data.

## 20. CONFIRMED — The material reserve can block the energy needed to spend that reserve

**Evidence**
- Ours: `AI/Brain/Director.cs:158–162,212–218`; `AI/DirectorPatches.cs:567–602,949–968`.
- Engine finite extraction and energy reset: `decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:152–185`.

**Failure scenario:** A dry planet has 12,000 material, low energy income, and weapons at the upkeep cap. Every economy option is rejected by the 15,000 reserve, including enabling power upgrades. Weapons cannot expand until energy increases. The reserve remains stranded.

**Fix:** Permit bounded enabling investments when the military cannot otherwise spend its reserve. Protect emergency material without prohibiting the investment that makes it usable.

## 21. CONFIRMED — Refinery policy is an unconditional throughput restriction

**Evidence**
- Ours: `AI/DirectorPatches.cs:1041–1094`.
- Two slots: `decompiled/Interplanetary.Prefabs/BuildingUpgrades.cs:11–32`.
- Multiplication: `decompiled/NyaEngine.Stats/NyaStat.cs:350–375`.
- Depletion: `decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:163–183`.

This is an acknowledged policy restriction, not a new regression.

**Failure scenario:** A Mine with Yield I remains at **175 material/turn** instead of reaching **350**, even when the planet has substantial reserves and faster extraction would fund a decisive military window. Researching Yield II does not change this policy.

**Fix:** Compare conservation with accelerated extraction over a finite strategic horizon. Preserve the efficiency preference, but allow urgent throughput when its value exceeds the reserve saved.

## 22. CONFIRMED — The salvo model treats partial visibility as absence and heterogeneous defenses as the strongest type

**Evidence**
- Ours: `AI/StrategyPatches.cs:197–212`; `AI/Brain/Strategy.cs:105–135`.
- Per-building visibility: `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:176–190`.
- Individual defense rolls: `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:229–247`; `decompiled/Interplanetary.Defenses/DefenseGameplayData.cs:21–37`.

**Failure scenarios:**
- Seeing any non-city building permanently sets `DefensesSeen`. No matching remembered defense then means zero coverage, although higher-stealth defenses may remain hidden.
- Twenty Shields at 15% versus Missiles plus one Ion at 80% become twenty-one defenses modeled with the **80%** probability.

**Fix:** Track uncertainty by defense type and observation threshold. Combine separate defense-type contributions instead of assigning every matching defense the maximum probability. Preserve activation-cost differences.

## 23. CONFIRMED — Counter readiness counts disconnected defenses as functioning counters

**Evidence**
- Ours: `AI/EconomyPatches.cs:148–151`; `AI/DirectorPatches.cs:745–752`.
- Engine requires power: `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:278–287`.

**Failure scenario:** Six intact but disconnected Shields satisfy the counter-deficit rule against Lasers. None can intercept. Coverage modeling excludes them, so inventory readiness and operational readiness disagree.

**Fix:** Keep intact inventory for safety caps, but use operational coverage for readiness. Restoring power should explicitly close a counter deficit.

## 24. CONFIRMED — Experimental laser support has two additional integration failures

**Evidence**
- Strategy excludes lasers: `AI/StrategyPatches.cs:169–173`.
- Zero weapons holds: `AI/Brain/Strategy.cs:209–212`.
- Hold clears all firing lists: `AI/Patches.cs:294–303`.
- Hold age advances only with positive shots: `AI/StrategyPatches.cs:123–130`.
- Failed laser installation rolls back patches: `AI/LaserPatches.cs:185–202`.
- Director purchases depend on requested configuration: `AI/DirectorPatches.cs:898–905`.
- Native skip: `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:79–84`; supported firing branch: `AITargetWeapons.cs:119–125`.

**Failure scenarios:**
1. A laser-only surviving arsenal holds forever as “no-weapons”; max-hold never advances.
2. Laser patch installation fails, but `EnableLasers=true` still lets the director buy weapons native targeting will skip.

**Fix:** Integrate laser readiness into strategy and committed targeting. Latch an effective capability flag only after all required patches/dependencies validate; use that flag for purchases.

## 25. CONFIRMED — Small superweapon arsenals inherit conventional buildup delays

**Evidence**
- Ours: `AI/Brain/Strategy.cs:65–67,230–248`.
- Projectile output is independent of building count: `decompiled/Interplanetary.GameEngine/ActionphaseManager.cs:221–245`.
- City destruction uses population: `decompiled/Interplanetary.Prefabs/BaseBuilding.cs:964–985`.

**Failure scenario:** One or two surviving superweapons can deliver meaningful damage, but the six-weapon readiness gate treats them like opening Railguns. Without attrition, they can wait five turns for max-hold.

**Fix:** Add readiness based on expected meaningful damage or kill capability. Weapon-building count is not a universal measure of salvo strength.

## 26. CONFIRMED — Incoming-weapon memory survives the shooter’s elimination

**Evidence**
- Ours: living-enemy pruning at `AI/EconomyPatches.cs:135–144`.
- Incoming memory is type-only: `AI/Brain/DefensePlan.cs:128–148`.
- Shooter identity is available but discarded from defender aggregates: `AI/LedgerPatches.cs:73`; `AI/Brain/DefenseLedger.cs:49–61`.
- Engine removes eliminated targets: `decompiled/Interplanetary.AI/AICore.cs:116–123`.

**Failure scenario:** An AI opponent attacks with Lasers and is eliminated. Its remembered incoming Lasers continue influencing Shields, research, and reserves while the surviving human uses another weapon family.

**Fix:** Retain incoming estimates per shooter and weapon type; merge only living shooters.

## 27. SUSPECTED — Multi-AI commitment still permits stable kingmaking against the weaker rival

**Evidence**
- Ours: `AI/Brain/Strategy.cs:199–208`; `AI/Brain/TargetPlanner.cs:69–86`.
- Independent enemies: `decompiled/Interplanetary.AI/AICore.cs:114–123`.
- Shared defender action-phase balance: `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:215–236`.

**Failure scenario:** At equal expected passing shots, a known leader scoring 1.1 gets multiplier 1.5; an unknown rival scoring 0.74 gets approximately 1.336. The leader is only **12.2% more valuable**, below the **25% switching hurdle**. Existing rival commitment survives.

Each AI also estimates saturation independently, without accounting for concurrent pressure on the same defender.

**Fix:** Bound commitment by progress toward elimination and third-player consequences. Remove the obsolete rationale that switching costs a turn of retained aiming—the engine resets searches each turn. Validate with a controlled multi-AI match.

## 28. CONFIRMED — Persisted v0.14.0 defaults can bypass the intended rollback and aiming improvement

**Evidence**
- Ours: `AI/Plugin.cs:149–157,312–317,390–391`.
- Previous default: `docs/reviews/sol-ai-v0.14.diff:296–321`.
- Queue uses the persisted plan: `AI/EconomyPatches.cs:512–533`.
- Engine executes that requested chain: `decompiled/Interplanetary.PlayerDataManagement/PlayerTechnologyHandler.cs:480–518`.

**Failure scenario:** A config saved by v0.14.0 retains its reordered research plan because that exact legacy value is absent from migration checks. Its stock `AiProcessorResources=0` also remains zero: changing a Bind default to 2600 is not migration.

The latest match logged 2600, so the latter did not affect that run.

**Fix:** Recognize the exact reverted stock plan. Version configuration migrations explicitly and distinguish stock values from deliberate customization. Log effective settings at match initialization.

## 29. CONFIRMED — The decision log records intent, not delivered combat or city survival

**Evidence**
- Ours: `AI/Patches.cs:421–430,478–481,508–515`; `AI/LedgerPatches.cs:49–73`; `AI/DirectorPatches.cs:1924–1940`.
- Actual firing acceptance/payment: `decompiled/Interplanetary.AI/AITargetWeapons.cs:133–151`; `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:238–260`.
- Engine already has projectile/building identities and impact positions: `decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:314–336,369–396`.

`ReportFired` consumes firing-list lengths before end-turn trajectory completion and payment. A listed shot may never fire, yet strategy consumes its pulse.

The existing logs also cannot answer whether missiles hit cities, repeatedly hit one weapon, were paid for, or damaged an already-doomed target.

**Failure scenario:** End-turn calculations time out or payment fails. Strategy records a strike and may hold the next turn. The analyst sees “firingHits” and cannot distinguish successful fire from intent.

**Fix:** Separate selected inputs, accepted actions, paid first-phase targeting, impacts, interceptions, and actual damage. Drive strike history from successful execution, not list length.

Minimum additional instrumentation:

| Record | Required fields |
|---|---|
| Guided assignment | Weapon/building ID, target ID, local position, Visible/Shadow, frozen durability, allocated damage, native-fallback reason |
| Executed fire | Live firing costs, acceptance/payment result, weapon/projectile/fragment IDs |
| Impact | Shooter, defender, weapon, local impact position, interception result and eligible defense IDs |
| Damage | Projectile ID, target building ID/type, actual HP/population reduction, ruin/destruction |
| Per-city snapshot | Population, growth, connectors used, projects, power, matching defense coverage |
| Planning constraints | Rejected purchase reason: cap, upkeep, material, reserve, slots, placement, availability |
| Session boundary | Match/load identifier, effective config, hook/capability state |

The telemetry recording’s damage entries carry only kind, nominal amount, and `visible-events-only`; they cannot substitute for that chain.

---

# Low findings

## 30. CONFIRMED — Placement backoff can be released by cancellation rather than successful placement

**Evidence**
- Ours: `AI/Brain/Memory.cs:220–226`; `AI/DirectorPatches.cs:1299–1302,1829–1845`.
- Engine timeout/cleanup: `decompiled/Interplanetary.AI/AICore.cs:193–201`; `AIConstructionQueue.cs:139–143`.

**Failure scenario:** Queued searches remain unresolved when the turn ends, without reaching the explicit abandon callback. “Queued with no failure” releases throttling despite zero completed placements.

**Fix:** Release backoff only on confirmed placement; record unresolved cancellation before queue cleanup. This previously deferred finding remains present.

## 31. CONFIRMED — Repair/sale diagnostics survive match reset

**Evidence**
- Ours: `AI/DirectorPatches.cs:134–139,228–233,777–779`.
- Reset hook: `AI/Patches.cs:37–39`.
- Engine initialization: `decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:294–295`.

**Failure scenario:** Load another match at the same turn and player ID. If no new repair/sale replaces the entries, old totals enter its director record.

**Fix:** Clear `s_soldThisTurn` and `s_repairedThisTurn` in `Reset`. This contaminates telemetry; it does not double-credit resources.

## 32. CONFIRMED — Disabling the plugin mid-hold breaks its latched hold invariant

**Evidence**
- Ours: `AI/StrategyPatches.cs:74–86`; `AI/Patches.cs:288–303`; `AI/Plugin.cs:633–635`.
- Native firing proceeds from lists: `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:78–103`.

**Failure scenario:** Hold releases firing energy into construction. The master switch is disabled before firing; the prefix returns before enforcing the latched hold. Native firing can resume without its reservation.

**Fix:** Honor an already-latched hold before the live enabled guard, or apply disabling at the next turn boundary.

## 33. SUSPECTED — Yielding to native intel research can consume an obsolete backlog

**Evidence**
- Ours: `AI/EconomyPatches.cs:507–508`.
- Native buffering: `decompiled/Interplanetary.AI/AITechQueue.cs:17–27,42–51`.
- Native strategy keeps submitting requests: `decompiled/Interplanetary.AI/AIShortTermStrategy.cs:128–160,297–300`.

**Failure scenario:** Requests accumulate behind planned research. When our prefix yields for urgent intel, native BeginTurn consumes the oldest buffered request rather than choosing fresh intel research. The exact frequency of that condition in stock Enhanced remains unestablished.

**Fix:** Purge completed/obsolete requests before yielding, or directly queue the intended urgent intel target.

---

## Lifecycle/API checks that did not produce another confirmed bug

These are important distinctions from the defects above:

- **The v0.14.2 research append fix is correct.** `added == 0` clears only the first successful addition; later targets append their prerequisite chains. Queue snapshots are copies, and restoring a queue does not erase per-tech progress.  
  `AI/EconomyPatches.cs:521–549`; `decompiled/Interplanetary.PlayerDataManagement/PlayerTechnologyHandler.cs:271–287,480–518`.

- **The two construction budgets are not duplicate physical charges.** `AIResources`, queue allotments, and actual player payment are different stages. Manual upgrade/project decision-budget deductions are necessary.  
  `AI/DirectorPatches.cs:1326–1363`; `decompiled/Interplanetary.AI/AIConstructionQueue.cs:78–82,117–120,145–173`.

- **Bare-building abandon refunds are correctly limited to the construction queue’s own searches.** The forecast rollback is wrong; the actual refund path is not a free-resource exploit.

- **Normal hold ordering is sound.** Native reservations and queue allotments exist before release, and release updates both. Savings sees the released budget afterward. The defense reserve correctly withholds planning money rather than physically spending energy.

- **Repair does not charge twice.** Native `RepairBuilding` restores HP without charging; the adapter must pay. Skipping city HP repair is correct because city durability is population.

- **Dry-mine sales run before resource and grid snapshots and preserve relay nodes.** The current leaf test addresses the earlier selling bug.

- **The interception hook covers lasers and superweapons too.** There is no separate uninstrumented surface-interception path for those weapon families. Live projectile × fragment counting matches the engine’s separate impact/interception opportunities.

- **Principal tactical memories reset on normal match initialization/load.** Per-player refusal/preemption state is isolated. I found no normal offline duplicate-BeginTurn trigger; however, strategy’s duplicate guard does not make the entire economy/ledger pipeline idempotent.

- **“Any exception uses native behavior” is inaccurate documentation.** Director exceptions end construction; laser update exceptions stop that weapon; some postfix mutations are not rolled back. Once-only error suppression also outlives match resets. No additional ordinary-match trigger was established beyond the concrete failures above.

- **The tests principally prove Brain rules.** They do not exercise the engine contracts responsible for queue clearing, native project re-selection, firing-list insertion order, live repair costs, or laser occlusion. Mutation-checking pure rules cannot validate those adapter assumptions.

---

# Five changes most likely to win the next match

## 1. Acquire city coordinates early and retain legitimate Shadow targets

Move the intel critical path forward and target **six operational Telescopes before the counterintel wall**, rather than readable population totals. Preserve their intel until the coordinates are acquired.

**Number behind it:** Six bare Telescopes cost **1,200 energy / 1,800 material**, yield **48 intel**, and beat base small-city stealth **45**. In this match, human counterintel was still ×1.0 through T13; the AI queued its first Telescopes at T16. Chasing a small city at ×11.725 instead requires about **528 intel**.

**Acceptance criterion:** Every enemy city has a recorded legitimate coordinate before the wall; guided assignment logs show those Shadows remain usable.

## 2. Establish two powered Ion opportunities over every city before the first Missile volley

Make this a critical-core requirement triggered by Missile evidence and an earlier prior, independent of total defense inventory.

**Number behind it:** Ten base Ions cost **2,000 energy / 2,500 material**. With two 80% rolls covering each impact, fourteen incoming Missiles have expected pass count:

`14 × 0.2² = 0.56`

Expected roll energy is:

`14 × (1 + 0.2) × 200 = 3,360`

The actual first volley passed **ten**, while the planned reserve was **4,650** and unpaid rolls were zero. Geometry and readiness were the missing capability.

**Acceptance criterion:** No city lacks its required powered coverage at T21–22; the feeding power infrastructure is protected too.

## 3. Allocate guided damage across kills and prioritize valuable weapons inside the hit quota

Implement a volley allocator before expanding the arsenal.

**Number behind it:** The T22 selector issued eight assignments against unchanged **330-HP** weapon durability. Two base Missile hits supply **400** direct damage. For cities, twenty directed base Missile hits supply **3,000 population damage**—enough to transform the match if allocated across legitimate targets, instead of random terrain or one doomed building.

**Acceptance criterion:** Logged allocated damage moves to the next target once a conservative kill budget is assigned; ready superweapons cannot be starved behind old guns.

## 4. Convert late surplus into useful damage with bounded replacement and an adaptive arsenal limit

Keep the aiming constraint, but stop treating thirty as a permanent composition lock. Expand only while accepted useful shots justify it.

**Number behind it:** T27–28 left **5,283–6,183 director energy** and **20,603–26,933 material** with thirty weapons. But at a total aiming budget of 2600:

- 30 weapons receive about **87 steps each**.
- 40 receive **65**.
- 60 receive **43**.
- Three AIs with thirty each reach the **30-step floor**.

Therefore a blind cap increase can recreate the previous aiming collapse. Replacement, damage-per-slot upgrades, and measured expansion should precede unlimited growth.

**Acceptance criterion:** More accepted city-threatening damage per turn, not merely more weapon buildings.

## 5. Spend city slots on combat multipliers when search slots bind

Add a focused combat-project policy, especially population damage. Use firing-cost reductions when energy genuinely limits the volley.

**Number behind it:** Five permanent population-damage projects give **×1.61** for **3,750 material / 1,000 energy**. Thirty base Missiles at 70% planned hits produce nominal city damage:

- Without multiplier: `21 × 150 = 3,150`
- With five copies: `21 × 150 × 1.1⁵ ≈ 5,073`

The human had **3,306 population** before the first lethal exchange. These are pre-interception totals, and city-by-city rounding/allocation still matters, but they show a way to cross the kill threshold without adding search entities.

**Acceptance criterion:** Project slots and research unlocks produce a measured increase in delivered population damage; firing savings are not purchased merely to leave more energy idle.

**Bottom line:** The next version should aim to **see cities, preserve its own cities, and allocate a lethal guided volley**. That is a substantially more promising route to the first win than another general increase in production or defense count.
