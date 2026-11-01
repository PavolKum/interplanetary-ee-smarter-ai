**Verdict: fix first.** The adapters are sound where I traced them, but five rules lose the game on their own: the AI fires guided Missiles at nothing, its per-city Ion rule never engages against Missiles and is suppressed by its own cap, the accuracy plan throws away 21% of aimed guided shots, the director never values intel or counterintel, and at war it keeps buying energy it cannot spend. Every finding below is traced in `decompiled/` and, where the match data exists, in `docs/playtests/data/v0141-*`.

## Findings, ranked

**1. Critical, CONFIRMED. Guided weapons are built and fired with no target; the engine then aims them at a random point on the sphere.**
Ours: `AI/DirectorPatches.cs:911-928` scores weapons by damage x pass chance / cost with a x1.5 lock-on bonus and no term for whether a target exists, so 28 Missiles were chosen against a Kinetic-only human. `AI/Patches.cs:461-465` offers only Visible cities and weapons and hands the rest to native. Game: `AIWeaponTargeting.cs:483-497` picks a random visible or shadow building, and with none, `Random.onUnitSphere * Radius`. `WeaponGameplayData.cs:145-149` lands a second-phase weapon exactly on that point; `TrajectoryCalculation.cs:160-171` shows lock-on only widens the planet's success radius, it never homes onto buildings. Data: `target` records show `cities 0` on every turn; `build` records show `visibleWeapons {}` from T23; only 8 `strike` records exist for about 140 guided shots, so the other 130 went to the native random point. Expected structures per random 2.2 u impact is 0.22, so about 26 building hits and no population for 39k energy of Missile fire T23-T28.
Root cause of the blindness: the plan puts Self-Adjusting Optics and Advanced Signal Processing at #11-12 (`AI/Plugin.cs:149-157`), researched T14-T15, telescopes from T16 at 2 per turn (`Plugin.cs:326`). The human's first Data Security Hub came at T13; small-city stealth is 45 x counterintel (`StealthGroupsData.csv`, `FrozenIntelState.cs:66-71`, `PlayerResourceHandler.cs:57`), so the requirement was 77 at T13 and 527 at T23 while the AI peaked near 150 intel.
Fix: move Optics + ASP to plan positions 3-4 (650 science, done by T4 at the logged 279-306 science/turn), raise `IntelPerTurn` to 6 until every enemy city has been seen once, then let shadows carry it (`PlayerIntelHandler.cs:183-190` keeps a once-seen building as Shadow for the rest of the match; `AIThreatAssessment.cs:233` and `AIWeaponTargeting.cs:418` accept Shadow). In `TryWeapon`, multiply a second-phase weapon's score by 1 when a Visible or Shadow enemy city exists and by about 0.1 otherwise, so Railguns or MIRVs are built while blind.

**2. Critical, CONFIRMED. The per-city Ion rule never fires against Missiles and is suppressed by the total-Ion cap.**
Ours: `AI/DirectorPatches.cs:297` sets `NukeThreat` from `wea_NuclearMissile` only; `AntiNukePerCity` at `AI/Brain/Director.cs:267-271` returns 0 before `AntiNukePriorTurn` 24; and `AntiNukeMissing` at `Director.cs:262` returns 0 whenever intact Ions anywhere on the planet reach 2 x perCity x cities. The generic coverage rule had already placed 16-29 Ions by value, so the cap of 6, then 4, then 2 was always exceeded. The test at `AI/Tests/Program.cs:1029` enshrines exactly this behaviour.
Data, `director` records:

| turn | Missiles known | Ions owned | antiNukeMissing | cities protected | impacts near city, no Ion in range |
|---|---|---|---|---|---|
| 22 | 11 | 7 | 0 | 0 of 5 | 0 |
| 24 | 10 | 16 | 0 | 2 of 3 | 14 impacts, 8 no eligible defense |
| 25 | 9 | 21 | 0 | 1 of 2 | 9, 0 |
| 28 | 8 | 29 | 0 | 1 of 1 | 10, 9 |

Population went 2,740 to 860 in the T23 volley and two cities fell with 7-11 Ions standing. Game: a Missile is stopped only by an Ion within 5.3 u of the impact (`PlayerDefenseHandler.cs:264-271`, `DefensesData.csv`), and the human aimed every Missile at a city (`lastNearCity` equals impacts).
Fix: trigger per-city cover on any guided weapon seen or landed (`wea_Missile`, `wea_MIRV`, `wea_NuclearMissile`, from `economy.EnemyWeapons` and `ImpactsByWeapon`); count only Ions that protect a city toward the cap; place them within 4.35 u of the centre (`Coverage.cs:239`); exempt them from `DefenseTurnCap`.

**3. High, CONFIRMED. The accuracy plan withholds or deliberately misses 21% of aimed guided shots.**
Ours: `AI/Patches.cs:252-266` re-splits all targetable weapons at 0.7 for level 1 (`Plugin.cs:278`); game `AIWeaponTargetingGoal.cs:131-146` then fires only `Hits` weapons as hits. Data from `accuracy` and `fire` records T23-T28: primary trajectories found 22, 23, 23, 29, 28, 22; fired as hits 16, 16, 17, 20, 21, 21; 19 shots fired as deliberate misses at 300 energy each. That is 36 of 174 aimed Missile shots thrown away. The human's own Missiles hit the planet 60 of 71 times (telemetry `shot.hitOtherPlayerPlanet`), so "human parity" at 0.7 is a handicap no human has with guided weapons. A second drift: `AIWeaponTargetingGoal.cs:44` sizes the split from `m_targetableWeapons.Count`, which `AIWeaponTargeting.cs:86-107` only ever grows, so once weapons die the misses are absorbed by dead entries and the planned rate silently changes.
Fix: accuracy per weapon: 1.0 for weapons with a second targeting phase, the configured value for the rest, computed over live powered weapons.

**4. High, CONFIRMED. Guided strikes stack on one building and ignore shadow cities.**
Ours: `AI/Brain/Memory.cs:173-192` picks the weakest Visible city or weapon with no memory of what this turn already allocated; `AI/Patches.cs:447-488` runs once per fired weapon. Data: all 8 `strike` records at T22 name the same weapon building at 330 of 350 HP; 8 x 200 damage on a 330 HP target. Also, because the prefix returns false whenever one Visible weapon exists, it would override the native choice that does consider Shadow cities (`AIThreatAssessment.cs:230-248`). The README's "shadow intel is skipped" rule costs the AI a target class the human's own map shows him.
Fix: per-turn damage ledger keyed by building; include Shadow cities with their frozen population (`IntelMemory.cs:70-86`, `ShadowBuildingFrozenState.cs:56`); prefer cities; move to the next target once allocated damage reaches HP or population.

**5. High, CONFIRMED. The director never values intel or counterintel, and buys an upgrade that cuts intel 25%.**
Ours: `AI/DirectorPatches.cs:1074-1096` prices upgrades by energy, material and science only; `1124-1212` lists projects from the energy and material evaluations plus science multipliers. `UpgradesData.csv`: `upg_RadiotelescopeScienceYield1` is +20 science and `turn_yield_intel_multiplier 0.75`; `upg_RadiotelescopeIntelYield1` is x1.5. Data: 24 ScienceYield1 queued on telescopes, 0 IntelYield1. `ProjectsData.csv`: Quantum Encryption Hub is +50 counterintel per city, permanent, 150 energy and 200 material, unlocked by Self-Adjusting Optics alone (250 science); Behavioral Pattern Analysis is x1.5 intel global for 5 turns and stacks per city (`GlobalStatModifier` per the game-math report). Neither was ever bought; the AI's counterintel stayed 1.0 until T22, and the human's intel of 84 from T19 (telemetry `resources.intel`) saw every AI city all game.
Fix: add intel and counterintel gains to `EconOption`, valued by the gap to the next stealth threshold; never put ScienceYield1 on a telescope while `BlindEnemies > 0`; buy Quantum Encryption Hub in every city with a slot once Optics is done.

**6. High, CONFIRMED. At war the economy buys energy it cannot spend while material idles and cities fall.**
Ours: `Director.cs:84-111` sets energy weight 1 and material 0.25 in material glut, `Director.cs:218` blocks extraction, so the only buys left are plants and energy upgrades; the weapon caps at `DirectorPatches.cs:891` and `569` bind at 30 aimed weapons and 40% of income. Data T26: 21 Floating Plants and 34 Floating yield upgrades queued, 16k material left, 40 energy left; T27-T28: 5.3-6.2k energy idle plus an 8.6k reserve, 20-27k material idle. The human's turns lasted 63-146 s during the war (`human_turns.py`), so the trajectory search had time for more than 30 weapons.
Fix: when `AimedWeapons >= MaxWeapons` or upkeep is at the cap, treat energy as in glut that same turn and route material to city Ions, HP upgrades, repairs and Vertical Farms; raise `MaxWeapons` toward 45 with `AiProcessorResources` 4000 and `WarUpkeepShare` 0.5 when energy idles.

**7. High, CONFIRMED. Defenses are throttled to 3-5 a turn whatever the urgency.**
`Director.cs:276-282` spreads the deficit over 3 turns and halves it by free slots unless `RecentDamage > 0`; `DirectorPatches.cs:440-441`. Data: Ion queued 3, 4, 4, 5, 5 per turn T20-T24 with 11 enemy Missiles visible at T22 and 4.6-12k material unspent. Fix: no throttle on city-cover defenses; treat "enemy guided weapons seen" as under attack.

**8. Medium, CONFIRMED. The defense energy reserve scales with the number of defenses owned.**
`Director.cs:400` floors the rolls at one per own defense, so 49-53 defenses reserved 7.3-8.7k a turn, capped by `DefenseReserveShare` 0.3 of income. Observed need was at most 4.1k (`lastDefenseEnergy` T27), 200 at T28. Fix: floor on expected covered impacts from the ledger, not defense count.

**9. Medium, CONFIRMED. The refinery policy blocks Yield2 on every limited planet, whatever the reserve.**
`DirectorPatches.cs:1043-1057, 1091-1094`. Data: 52 Offshore Efficiency1 and 39 Yield1 bought, 0 Yield2, with 147k of 350k planet material left at T28 and a drain of 15k a turn. Sol v0.14 #2 accepted this and the policy stayed. Fix: apply only while `SustainableMaterialIncome < MaterialIncome`.

**10. Medium, CONFIRMED. No city-growth rule; cities are the only HP that loses the game.**
`BuildingStatHolder.cs:108-116, 417-443` and `CitiesData.csv`: growth is 10 x 1.3 per filled connector x 1.3 when a powered defense circle contains the city circle, connectors unlock at 10/25/50/75/100% of 1,000 and open project slots at 25/50/75%. The AI's population went 1,550 to 2,740 in 22 turns, about the base rate; nothing in the director anchors producers on city connectors or places a defense within radius minus city radius of a centre, and Vertical Farms is never priced. Fix: a city step before economy: fill connectors, one defense inside 3.4-4.0 u of each centre, Vertical Farms when unlocked.

**11. Medium, CONFIRMED. Temporary stacking projects are undervalued.**
`DirectorPatches.cs:1186-1187` scales a project's gain by duration/10 and prices each copy on the current base, so Reactor Trials x1.3 per city and Electromagnetic Jamming x1.25 per city never win. Only permanent projects appear in the match's `queued` records. The human's Jamming cycle roughly tripled his science T12-T16. Fix: value a stacking global at factor^n over its duration against the cheapest alternative source of the same resource.

**12. Medium, CONFIRMED. Counterintel arrives at T20.**
Data Security Hub needs Magnetic Radiation Shielding at plan #17 (`Plugin.cs:153`, chain 650 via 2nd Gen Composites per `TechTreeData.csv`), then 1 per turn (`DirectorPatches.cs:560`). The AI reached x2.2 at T27 against a human reading 145 intel. Fix: see finding 5; Quantum Encryption Hub is the cheap route.

**13. Medium, SUSPECTED. MIRV is the right blind weapon.**
`WeaponGameplayData.cs:187-204`: MIRV warheads pick buildings in a 45-degree cap around the aim point with no visibility filter, 3 x 125 building and 100 population damage per 300 energy, versus a blind Missile's expected 0.22 x 200. Cost 2,000 extra science after Orbital Assembly. Not measured in a match.

**14. Low, CONFIRMED.** `AIResources.cs:47-57` reserves firing cost for every Normal weapon, powered or not; `DirectorPatches.cs:801-806` counts powered only, so repair budgets overstate free energy by a few hundred. **Low, CONFIRMED.** Counter research at `EconomyPatches.cs:488` reads last turn's `EnemyWeapons`, because `AITechQueue.BeginTurn` is the second module (`AICore.cs:103-110`) and the economy postfix runs after all modules; one-turn lag. **Low, SUSPECTED.** `SellDryMines` and the per-player init reset at `Patches.cs:37-39` run again on any repeated `BeginTurn` or load; harmless offline. **Low, CONFIRMED.** `Plugin.cs:318-322` rewrites a deliberate `GrudgeWeight` 0.6 on every load.

## Verified paths and measurement gaps

Checked against `decompiled/` and found consistent: turn ordering (resources, tech and defense reset in `PlayerData.cs:446-461` and intel freeze in `PlayerManager.cs:664-676` all precede `AICore.BeginTurn`, so repairs spend fresh energy and the research prefix sees a post-spend queue); the v0.14.2 `clearPrevious` fix at `EconomyPatches.cs:533`; construction-queue charging and refund (`AIConstructionQueue.cs:42-60, 145-179`, `DirectorPatches.cs:1853-1909`); the power-line limit used by coverage placement matches `PlayerPowerGridHandler.cs:67-68`; `CheckEnergy` and `SpendEnergy` for repairs; the ledger's event counting against `PlayerDefenseHandler.cs:203-251`; hold-fire release and the empty firing lists; per-player keying of all static state for two AIs; both BepInEx logs show 15 of 15 hooks and no hook failure, so no silent fallback fired in the last two matches.

Measurement gaps that would have shortened this diagnosis:
- Where our guided shots landed: log every `HandleSecondPhaseTargeting` outcome (city, weapon, shadow, random point) and the chosen building id, including the native path.
- Our impacts near enemy cities: the ledger records `nearCity` only for AI defenders (`LedgerPatches.cs:54-72`); add the shooter-side mirror.
- Per-city population and connectors filled, own and enemy, per turn.
- Aimed-but-unfired count per turn and the accuracy quota that caused it.
- Our intel versus the enemy's current stealth thresholds, as numbers, each turn.
- Idle energy as one field: `energyLeft + defenseReserve`.
- Tech completed events on the AI side, and cumulative science spent against the plan index.

## Five changes most likely to win the next match

1. **Intel sprint and shadow strikes.** Optics and ASP at plan positions 3-4, six telescopes with IntelYield1 by T6, then strikes allocate Missiles across Visible and Shadow cities. Number: 6 x 8 = 48 intel beats a small city's 45 while the human's counterintel is 1.0 before his T13 hub; 28 Missiles x 150 population is 4,200 per volley against the human's 3,306.
2. **Ion over every city on the first guided weapon seen or landed, outside the cap and the throttle.** Number: two Ions within 4.35 u of a centre stop a Missile with 0.96; the T23 volley of 14 would have passed about 2 instead of 10, and 10 Ions cost 2,500 of the 12,000 material idle at T23.
3. **Accuracy 1.0 for second-phase weapons.** Number: 36 more Missile hits over T23-T28 and 5,700 energy saved.
4. **Quantum Encryption Hub in every city once Optics is done, Data Security Hub tech by T8.** Number: +250 counterintel is x3.5, so a small city needs 158 intel; the human reached 161 only at T29.
5. **Spend the glut on the fight.** Energy counts as glut the turn the weapon caps bind; cap raised to about 45 weapons with the 4000 search budget; idle material to city Ions, HP upgrades and Vertical Farms. Number: 14k energy a turn sat in idle plus reserve at T27-T28, 46 Missile shots' worth, and 27k material, 100 Ions' worth.

Not verified: the installed config values live outside the repo, so defaults are assumed; the human's per-city population is not in the telemetry totals I read; MIRV warhead ordering is from code reading only.
