I’ll recompute the match evidence from the JSONL records, then check the visibility and combat rules against the decompiled game before ranking changes.
The raw logs already overturn two important parts of the write-up: all four AIs were blind to every opponent, not selectively to the human, and two planets reached zero material on turn 25 while still holding large stockpiles. I’m checking research completion, actual shot outcomes and which apparent score information is genuinely public.
## Bottom line

The main failure was **blind, self-reinforcing target selection**, followed by a failure to convert the AIs’ enormous economic advantage into durable, effective offense. The original write-up’s explanation that AIs *saw and attacked each other’s economies* is wrong: **they saw none of the economies**.

The second major correction: **Thermonuclear Warheads did finish for three AIs**. They had already spent their material by the time the weapon became available.

**Confidence:** high in the logged observations and cited mechanics; medium in the ranking of fixes, because this is one match and the counterfactuals have not been tested.

## Hypothesis verdicts

**H1 — Blind AIs: CONFIRMED.** Across 95 `target` records and 349 enemy entries, every economy, city population, city count, leader and finish term is zero. All 155 `economy` records have `maxEnemyEconomy=0`; v0.2 has 42 nonzero records, first at T21. This proves the *planner inputs* were blind, not necessarily that every possible game UI signal was unavailable.

The proposed cause is supported: `IntelNeed` is the fraction of weapons with second-phase targeting (`decompiled/Interplanetary.AI/AIIntel.cs:53-73`); `IntelState` treats zero need as enough (`AI/DirectorPatches.cs:335-341`). Advanced Signal Processing was queued T21 for Am and T22 for the others—queueing is not completion. Railgun-only inventories explain zero need, but the log does not directly record `IntelNeed`.

**H2 — Grudge vendettas: PARTLY CONFIRMED.** There were **788/12,026 = 6.6%** target assignments to jogi: Am 242/4,233; Core 395/3,387; Shiver 125/3,155; Rebel 26/1,251. These are *weapon-turn assignments*, not shots or hits. Among powered weapon-turns, 342/5,998 were assigned to jogi; only 84 primary trajectories were found for him.

With leader and finish terms zero, scores were base plus grudge. At T17, Rebel scored Core 0.7 and jogi 0.1 (`v031-ai-decisions.jsonl:255`). Human grudge was zero through T31, then rose: Am scored jogi 0.693 at T39; Core scored him 0.7 at T42. Thus “stayed at base” is false late. Focus-fire, equal-score ordering, trajectory rerouting and orbit geometry also affect assignments (`AI/Brain/TargetPlanner.cs:56-97,115-122`). **The log supports a feedback loop, not proof that blindness alone caused the outcome.**

**H3 — Depletion: PARTLY CONFIRMED.** Am and Shiver first logged zero planet material at **T25**, Core at **T28**; Rebel never did. At depletion they still held **90,022, 120,327 and 53,309** material respectively (`v031-ai-decisions.jsonl:469,476,555`). They could still buy until those banks were spent; by T30 their stocks were 72, 27 and 8,309. Mine-yield upgrades also multiply drain (`docs/ruleset/embedded/enhanced/UpgradesData.csv:95`). At zero reserve, new material income stops, including city income, but banked material remains spendable (`decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:161-184`).

**H4 — Science/no superweapon: PARTLY REFUTED.** Science was often lower, but **Am completed the nuclear line by T32, Shiver by T36 and Core by T37**, evidenced by their `bui_NuclearMissile` savings goals. Am had only **72 material** when its goal appeared (`v031-ai-decisions.jsonl:644`); the base weapon costs 2,000. No superweapon purchase is logged. Science timing matters, but **spending the bank before unlock is the more immediate demonstrated blocker**, not failure to finish research.

**H5 — Railguns versus kinetic defense: CONFIRMED as a mismatch; impact unquantified.** AI economy records show Railguns as their only weapon type. Jogi had 7 Kinetic Defenses at T15 round start, 17 at T25 and 24 at T35 (`v031-human-telemetry.jsonl:93,170,355`). A powered, funded kinetic defense has an 80% base interception probability against a covered Railgun impact; coverage, overlapping defenses and available energy determine actual results (`docs/original-mechanics.md:294-313`). We cannot infer how many shots it stopped from these records.

**H6 — Placement loop: CONFIRMED, impact on match turns uncertain.** Core logged **241** abandoned Railgun searches and Shiver **196**, concentrated at T26–30. Core abandoned 105 on T26 (`v031-ai-decisions.jsonl:518`). `Abandon` records the failed ID and refunds it, but `TryWeapon` does not exclude that ID (`AI/DirectorPatches.cs:415-452,837-853`). This proves repeated searches and churn **within planning turns**, not that entire game turns were lost or the planet was definitively full.

## Q1 — Visibility thresholds

With intelligence enabled, a structure is visible when **observer intel ≥ rounded building stealth × target counterintel**; destroyed structures and ruins have special handling, and prior sightings can become shadows (`PlayerIntelHandler.cs:153-189`; `decompiled/Interplanetary.Prefabs/BuildingStatHolder.cs:123-127`). The table assumes base city stealth, no city-specific stealth modifier, and the human’s **round-start** telemetry:

| Turn | Counterintel | Big city | Medium | Small | Energy/material totals | Total population/city count |
|---|---:|---:|---:|---:|---:|---:|
| 15 | 1.00 | 30 | 40 | 45 | 5 | 20 |
| 25 | 1.70 | 51 | 68 | 76 | 8 | 34 |
| 35 | 2.89 | 87 | 116 | 130 | 14 | 58 |

Thresholds use Unity’s rounding; T25’s 8.5 and 76.5 round to even. Science and tech require base 25 × counterintel: **25, 42, 72**. Aggregate values are separate from seeing individual structures (`FrozenIntelState.cs:63-73`; `decompiled/Interplanetary.Screens.Components/IntelOverlayIntelThermometerComponent.cs:313-373`). Intelligence-off settings bypass the ordinary gate.

## Q2 — What is public?

- **Player identity and defeated/alive status:** visible in the player list (`decompiled/Interplanetary.Screens.Components/PlayerListOverlayPlayerComponent.cs:42-60`).
- **Total city count and population:** the intel screen gates both at the population threshold; do not read live totals below it (`IntelOverlayIntelThermometerComponent.cs:369-373`).
- **Economy, science and technology:** gated as above. Visible individual buildings and appropriately frozen shadow information are distinct from hidden live values.
- **Full rankings and statistics:** shown by `EndGameScreen`, including remaining cities and population (`decompiled/Interplanetary.Screens/EndGameScreen.cs:50-55,196-208`). I found no evidence that these are a live, public scoreboard during play; **unverified in the live UI**.

The existing `BuildEnemyView` reads live per-building generation and city population whenever a building is visible (`AI/Patches.cs:127-157`). Whether each exact value is exposed to a human for every visible enemy building deserves a separate UI check before treating it as unquestionably fair.

## Q3–Q4 — Ranked changes and rules to test

1. **Buy intel independently of weapon type; make targeting uncertainty-aware.** Unlock telescopes earlier, obtain enough intel to compare opponents, and reduce blind grudge lock-in. Use visible information, legitimate memory and an explicit unknown state—not hidden enemy economy or an arbitrary human bonus.  
   **Depends on:** intel thresholds, counterintel, construction/power timing, shadows versus live visibility, and what the human UI actually reveals.

2. **Preserve offense and defense, not just production.** Reserve a bounded defense-energy budget when threatened; use intelligence to select defenses and develop a practical precision/counter-defense weapon. Test whether positioning, power and trajectory success make the resulting weapons effective.  
   **Depends on:** energy resetting each turn, defense activation spending leftover energy, coverage and counter-type probabilities, weapon targeting phases, and only observable incoming threats.

3. **Budget finite material through the unlock window.** Forecast depletion, bank enough for a chosen weapon *before* its unlock, stop buying production with no useful payback, and skip failed placement types. Evaluate refinery upgrades against their opportunity cost.  
   **Depends on:** reserve-drain and yield modifiers, bank persistence, research prerequisites, superweapon limits/costs, placement/refund behavior and available power slots.

**Also investigate trajectory/power before declaring victory on targeting:** only **5,998 of 12,026 weapon-turns were powered**, and 1,321 primary trajectories were found. These are diagnostic counts, not realized hit rates. Reachability memory records “no primary trajectory” as failure even when a secondary trajectory exists (`AI/Patches.cs:254-273`; `AI/Brain/Memory.cs:61-92`); its effect on future assignments needs a focused test.
