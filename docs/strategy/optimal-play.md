# Interplanetary EE - optimal play, from the game code (31 Oct 2026)

Builds on two earlier documents and does not repeat them:
- `docs/original-mechanics.md` - full mechanics spec with code references (energy, planet material, population, power, damage, defenses, intel).
- `~/Repos_Local/ScheduleImodes/interplanetary/strategy/STRATEGY-NOTES.md` - weapon/defense tables and a one-turn nuke-volley plan (25 Oct).

New here: verified against `decompiled/` this session, an economy ranking by payback, the energy-for-defense budget,
the planet-material budget, what changes now that the AIs run v0.3, and the AI backlog that follows.

## Bottom line

1. **Energy is throughput, material is the stock, planet material is the real budget.** Energy resets every turn,
   material banks, and every unit of material ever produced comes out of a finite planet reserve.
2. **Upgrade before you build.** A second yield upgrade on an upgraded plant or mine pays back in ~1 turn; a new
   building in 3. City projects (Molecular Recycling Plant, Orbital Solar Plant) are the best buys of all early.
3. **Unspent energy is your defense.** Every defense roll costs the defender 100-200 energy from what is left that
   turn; at 0 the defense fails. Spending everything is a choice to be undefended.
4. **Only city population loses the game.** Population is both a city's health and its science; grow it (power
   connectors, defense cover) and kill it (population-damage weapons, precise hits).
5. **In a 1-vs-4, look poor and let them fight.** v0.3 AIs focus the visible economic leader. Counterintel hides
   buildings, and AIs shooting each other is free for you.

## 1. Rules that decide strategy (verified in code this session)

| Rule | Evidence | Consequence |
|---|---|---|
| Energy is set to production every turn | `PlayerResourceHandler.TurnStartInit` | Unspent energy is lost, except as defense budget (below) |
| Planet reserve drains by max(cost, production) per turn; at 0 all material income stops (cities too) | `PlayerResourceHandler.cs:161-184` | Total material in a game <= planet reserve. Mines convert 100 per 150 drained (0.67) |
| Defense rolls cost the defender's leftover energy (Kinetic 100, Ion 200, Shield 150); 0 left = "failed, no energy" | `PlayerDefenseHandler.IsHitIntercepted`, `DefensesData.csv` | Keep energy back on turns you expect fire |
| Yield multipliers stack multiplicatively (1.75 x 2 = 3.5) | `NyaStat`, `UpgradesData.csv` | Second upgrade is worth more than the first |
| Unpowered buildings yield nothing and pay no upkeep; unpowered defenses don't fire | `PlayerResourceHandler.cs:249-252`, `PlayerDefenseHandler.CheckPower` | Protect plants near defenses; a lost plant can switch off a cluster |
| Defeat = every city at 0 population | mechanics spec 9.1 | Buildings are means, not ends |
| City growth = 10 x 1.3^(filled connectors) x 1.3 if fully inside a powered defense's radius | `BuildingStatHolder.cs:411-515` | 13 -> 48 pop/turn per city; science = 0.18 x pop |
| Project slots open at 25/50/75% population and close again (project lost, no refund) if population falls | `BuildingProjects.cs:348-448` | Cities that get hit lose their projects |

## 2. Economy: what to buy, in order of payback

Payback = material cost / material-equivalent gain per turn (energy and material treated 1:1 for ranking).
Base yields; global modifiers scale all of them alike.

| Buy | Cost E / M | Gain per turn | Payback (turns) | Notes |
|---|---|---|---|---|
| Mine/Offshore Yield2 on a Yield1 building | 50 / 200 | +175 M (+219 offshore) | **1.1** | Quantum Computing / Robotic Mining |
| Floating/Solar Yield2 on a Yield1 plant | 50 / 200-250 | +175 E | **1.1-1.4** | Microwave / Nanostructuring |
| Molecular Recycling Plant (city project) | 150 / 350 | +200 M | **1.75** | Molecular Recycling (400 sci). Needs a project slot (pop >= 25%) |
| Orbital Solar Plant (city project) | 150 / 350 | +200 E | **1.75** | Microwave Energy Transmission |
| Mine/Offshore Yield1 | 50 / 150 | +75 M (+94) | 2.0 | Also raises planet drain x1.75 |
| Floating/Solar Yield1 | 50 / 150-200 | +75 E | 2.0-2.7 | |
| Offshore Mine | 300 / 350 | +125 M | 2.8 | Water, needs a power slot |
| Mine | 300 / 300 | +100 M | 3.0 | Needs a power slot |
| Floating Plant | 250 / 300 | +100 E, 3 power slots | 3.0 | Water; the platform for two upgrades |
| Nuclear Plant | 250 / 450 | +200 E, -50 M | 3.0 | Land-locked fallback |
| Solar Plant | 250 / 400 | +100 E, 3 slots | 4.0 | Only before Smart Grids or with no water |

Rules of thumb:
- **Each turn: upgrades and projects first, then producers into free power slots, then plants for next turn's slots.**
  Buildings bought this turn only take upgrades next turn.
- **Energy is the per-turn cap on how much you can buy** (every building costs 250-300 energy, an upgrade 50). Material
  idles in the bank; energy idle at end of turn is either defense budget or waste.
- **Planet material:** on a 350k planet, a fully upgraded mine drains 525/turn for 350 material. The human emptied his
  planet at turn 31 in the 31 Oct match. The Multi-Stage Refinery (planet drain x0.7) raises total extractable material
  ~40% but costs an upgrade slot: worth it only on long games or small reserves (the default reserve is 100k).
  Plan the material you need for the endgame volley before the planet runs dry; after that only the bank is left.

## 3. Defense: energy, placement, growth

- **Energy reserve:** keep about (shots expected at you this turn) x 150 unspent. A shot can trigger several defenses
  in range (cheapest first, each roll paid), so dense defense clusters burn energy faster than they stop shots.
- **Placement:** within ~5 units of cities (cities are what you lose) and fully covering the city circle, which also
  gives the x1.3 growth bonus. Defend the plants that power the defenses.
- **Match the counter to what you see:** Kinetic vs Railgun/Coilcannon (0.8), Ion vs Missile/MIRV/Nuke (0.8),
  Shield only vs lasers, City Barrier (project) vs Asteroid 0.7 / Solar Laser 0.5 / Nuke 0.5.
- v0.3 AIs now pick weapons against your visible defenses: 30 Kinetics make them switch to missiles. A mixed
  Kinetic+Ion cover near cities is more robust than a wall of one type.

## 4. Offense: killing population

- Population damage per firing energy, if the hit lands on a city: Railgun 1.25, Coilcannon 1.0 per fragment (x3),
  Missile 0.5, Laser 0.58, Nuke 0.71 + fallout, Solar Laser 0.88, Asteroid up to 11.4 (10 x 800 / 700).
  The Asteroid's fragments land at random (the 25 Oct notes estimate ~13% chance per fragment to touch a given city),
  so its realistic figure is a few times lower, still the best per energy, and only City Barrier stops it. Nukes and missiles land exactly where aimed (second-phase targeting), on any side of the planet.
- **Saturation beats interception:** each enemy defense roll costs the enemy energy; a volley larger than
  (enemy leftover energy / roll cost) goes through unopposed. AI leftover energy is small (see 5).
- **Precision on full cities:** a centred hit does full damage; a 1,000-pop city takes two nukes or ~6 missiles.
- **Superweapon limit setting:** with the limit on, one of each per player. With it off they are unlimited
  (STRATEGY-NOTES fact 2), and a one-turn mass volley (~48 nukes, ~23k energy that turn) ends a game.

## 5. Playing against the v0.3 AIs (what changed)

- They now boom: in this match each AI was at 2,125 E / 3,225 M per turn on turn 11 (human 1,800 / 2,914 on turn 12).
  Expect them to be economically level with you through the midgame.
- **They hold weapons until turn 15** unless hit hard (>= 600 recent damage), then spend up to 40% of energy on weapons.
- **They spend almost all their energy** (25-125 left per turn so far): **their defenses will mostly fail for lack of
  energy.** Volume fire works against them. (Backlog item 1.)
- They target the strongest visible economy with weapon choice and focus fire: counterintel (Data Security Hubs)
  makes you look poorer and harder to target.
- They save for one Nuclear Missile (Asteroid second) and buy it only when its 700/turn firing cost fits their income.
- Accuracy is human-level (0.7 planned) by default.

## 6. AI backlog that follows from this (v0.3.2+)

1. **Defense energy reserve** (high): keep about visible enemy weapons aimed at us x 150 energy unspent from war
   onward; today the director spends it all and its defenses fail.
2. **Don't overbuy energy early**: turn 1 bought 3 Solar Plants (1,200 material), turn 2 then left 750 energy unspent.
   Value energy gains at ~0 when last turn's energy went unspent.
3. **Population growth**: anchor new buildings on cities' free power connectors (x1.3 growth each) and cover cities
   with defenses (x1.3); this is science and city health.
4. **Superweapons with the limit off**: plan a volley (Asteroid/Nuke count vs enemy cities), not one at a time.
5. **City Barrier / Direct Defense Initiative** projects against visible superweapons.
6. **Planet-material plan**: Multi-Stage Refinery on small reserves; stockpile before depletion.

## Open questions (need a match or a test)

- Do the same global project in several cities stack? (STRATEGY-NOTES open question.)
- How many defense rolls does a typical shot trigger in a dense cluster? (Energy drain per shot.)
- Does the AI's targeting really go after the human once he leads (LeaderWeight) - check `target` records.
