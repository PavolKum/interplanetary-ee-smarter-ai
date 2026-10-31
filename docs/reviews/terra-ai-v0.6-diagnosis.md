I’ll reconstruct the turn-by-turn evidence from both logs, then check the defense and weapon mechanics against the decompiled game. I’ll distinguish what the logs establish from what they cannot show.
One important correction is already clear: the telemetry’s shot totals include planet misses, and the AI’s “planned hits” are not confirmed impacts. The defense-energy calculation has to start from planet-hit events and allow for overlapping defenses.
# Diagnosis review

**Bottom line:** Oort was overwhelmed, but the logs do **not** establish that its defenses ran out of energy or that most incoming shots landed. The stronger diagnosis is a combination of limited coverage, a constrained and declining defensive budget, lost power infrastructure, and an offense that failed to exploit a defense mix it had actually seen. Interception and per-weapon firing outcomes were not recorded.

The most consequential corrections are:

- At T21 Oort had **10 Kinetic + 5 Ion**, not 15 Kinetic. At T25 it had **19 Kinetic + 2 Ion**; it reached 21 Kinetic at T26 (`v050-ai-decisions.jsonl:95,121,128`).
- The human’s T28–35 Railgun events include many planet misses: the counts reaching the enemy planet were **49, 46, 51, 84, 96, 66, 27, 70**, respectively. A planet hit is not proof of damage.
- Oort saw **10–16 Kinetic Defenses between T15 and T24**, before losing that view. “Blind, so it could not know” misses an actionable observation (`v050-ai-decisions.jsonl:56,97,116`).
- **271** is the sum of `firingHits` selections, not confirmed hits; `plannedHits` totals **450** (`AI/Patches.cs:249–291`).

## Hypotheses

### H1 — Reserve too small: **PARTLY CONFIRMED**

For the logged planet-hit volumes, the reserve was often too small to fund even one base-cost roll for every incoming Railgun projectile: T28, **49 × 100 = 4,900**, versus a **2,052** director reserve; T29, **4,600 versus 1,440**. T25–27 reserves were 3,675, 3,675, and 2,811 (`v050-ai-decisions.jsonl:123–145`).

But the reserve is a **planning allocation, not a measurement of actual action-phase energy**. Unused firing allocation and construction leftovers may increase the real balance. Conversely, uncovered or unpowered defenses make no paid roll. No interception or Oort end-of-planning balance is logged, so exhaustion and the proportion that got through are **unproven**. Also, the threat estimate was **8 remembered Railguns at T26–27**, not ~22; its damage term and defense floor also feed the capped reserve (`AI/Brain/Director.cs:200–212`).

### H2 — Inadequate coverage: **PLAUSIBLE, UNPROVEN FOR BUILDINGS**

On a normal radius-19.6 planet, assuming defense centers lie on the surface, 21 non-overlapping radius-5.9 circles cover **at most about 48% of spherical surface**; overlap reduces that. This does not prove which buildings or incoming impact points were uncovered. The test is at the **impact point**, and an uncovered impact’s splash can still damage a covered building (`PlayerDefenseHandler.cs:264–275`; `WeaponGameplayData.cs:327–336,362–374`).

Positions, actual powered defenses, upgrades and impact coordinates are absent. Coverage is a serious competing explanation, not a measured cause.

### H3 — Defense and weapons suppressed growth: **PARTLY CONFIRMED**

At T26 Oort had **163 normal buildings and 12,250 E/t**, versus Fey’s **277 and 25,805 E/t** in v0.4.1. Oort was already behind at T20: **8,475 versus 11,895 E/t**, before the main collapse (`v050-ai-decisions.jsonl:88,128`; v041 economy records).

At T25, 12,250 income minus **4,200 initial firing allocation** and 3,675 reserve leaves 4,375 construction energy: **64% allocated away from construction**. The logged `upkeep` of 4,900 includes newly queued weapons; it must not be substituted for the initial 4,200 (`AI/DirectorPatches.cs:255–272,515–519`; `AIResources.cs:38–61`).

This supports a growth tradeoff, not the counterfactual that defenses cost *more than they saved*: different matches, damage, placements and choices confound that comparison. From T24–26 Oort was already taking damage, and by T27 it had lost ten Floating Plants.

### H4 — Kinetic-only human and missed missile opportunity: **PARTLY CONFIRMED**

Human `turn_end` snapshots contain **zero Ion Defenses**, with 15–26 Kinetic in the relevant turns. Enhanced Kinetic does not counter Missiles (`docs/ruleset/embedded/enhanced/DefensesData.csv:5–26`). Oort queued **36 Railguns and seven Missiles**, not zero Missiles; actual firing by type is unavailable.

More importantly, Oort **saw** the Kinetic wall through T24, yet queued substantial Railguns; its scoring may still favor their cheap splash damage (`AI/DirectorPatches.cs:480–515`). It could fairly remember observed defenses, record its own intercepted shots and compare outcomes by weapon type. It could **not** infer “no Ion anywhere” from blindness or an empty visible-defense map. A missile-heavy response is promising, not proven optimal: damage area, availability, targeting and any unseen defenses matter.

### H5 — Nuclear Missile: **PARTLY; “placed once” unsupported**

Five queues occurred at T26, 27, 29, 33 and 34; T33 explicitly abandoned placement. Solar Plant searches were abandoned 14 times, consistent with difficult land placement, but not proof that every nuclear attempt failed for lack of land.

The normal-building snapshots show a Nuclear Missile at **T28, T30–32, and T35**, absent at T29 and T33–34 (`v050-ai-decisions.jsonl:143,149,156,162`; `AI/EconomyPatches.cs:515–526`). Without building IDs or placement events, those episodes do **not** establish exactly how many distinct buildings were placed. There is no per-weapon fire record, so neither “never fired” nor power/trajectory failure is established. Collapse was already underway when the first appears.

### H6 — Human’s retained energy stopped Oort: **PARTLY CONFIRMED**

Human T27 ended planning with **15,339 energy**; next turn’s `energyBefore` was **14,339**. T28 similarly fell **7,781 → 6,181** before refresh. These action-phase reductions are consistent with defense spending while substantial energy remained, not an exhausted defense budget. Interceptions and other action-phase expenditures are not separately recorded (`v050-human-telemetry.jsonl`, sequences 433/648 and 654/919).

Oort’s offense was **ineffective overall**, not completely stopped: human population fell on some earlier turns, and guided strikes were selected at T23–24. Oort’s powered targeting entries fell **31 at T24 → 23 T26 → 11 T29**; the list is not a live building census (`v050-ai-decisions.jsonl:115,130,151`). A base-cost 15,000-energy budget funds **150 Kinetic attempts**, so saturation would require a very large *covered* volley; missiles bypass Kinetic, lasers bypass Kinetic but experimental AI laser support is off, and a Nuclear Missile bypasses Kinetic but can meet Ion or city barriers. None is a demonstrated win.

## Q1 — Exact defense resolution and budget

For each impact/fragment, the game selects normal defenses registered against that weapon whose building-center **3D distance is strictly less** than their current radius. It sorts by **energy + material cost ascending**, then usable interception probability descending. Each powered, affordable candidate spends its activation cost **before** rolling; success stops the chain, failure tries the next. Insufficient energy or material causes a failed event without spending; at zero energy, positive-cost defenses cannot roll (`PlayerDefenseHandler.cs:203–251,259–307`). Balance is shared across the phase.

At base **100 energy and 80%**, T28’s 49 planet-hit events would require **4,900** for one attempt each *if all were covered and powered*. With abundant overlapping defenses, the expected attempts per impact approach **1.25**, or about **6,125**. T32’s 96 would similarly mean **9,600** or approximately **12,000**. These are scenarios, not observed spending or guaranteed protection; upgrades change cost, radius and probability.

## Q2–Q4 — Signals and ranked changes

1. **Measure and adapt offense, retaining fair observations.** Remember previously visible defenses; log the AI’s own fired weapon type, planet hit, interception and observable damage. Test missiles against the remembered Kinetic wall and adjust by outcomes. The game exposes shooter-side interception events (`ActionphaseDefenseActivationEvent.cs:99–110`); restrict learning to what a player can observe. **Rules to test:** active-ruleset counters, projectile versus fragment, visibility, guided targeting, and whether an event reveals type or merely interception.

2. **Budget defenses from observed incoming impacts and actual spending**, with a configurable prior while blind. Use own inbound projectile/defense events and recent damage by type where observable; remember seen weapons, account for multi-projectile upgrades, and respond to an upward trend rather than dividing aggregate splash damage by 125 as though it were independent hits (`AI/Brain/Director.cs:205–212`). **Rules:** energy resets, actual leftover after targeting, per-attempt cost, overlapping defenses, power, and coverage. Higher reserves compete with construction and firing; tune against measured results.

3. **Protect and maintain the power network while pressure is rising.** Measure powered coverage and prioritize reconnecting critical defenses and generators before an unrecoverable cascade; investigate why land-plant placements fail and whether water placement can relieve it. Oort’s unpowered count rose **7 → 13 → 22** at T25–27, while its Floating Plants fell **28 → 18 → 10** by T28. **Rules:** construction completion timing, power connections, placement masks, and whether a proposed replacement can actually power the intended buildings.

**Confidence:** high in the logged counts and defense rules; medium in the ranked interventions; low in any claim that a particular intervention would have won this match. The next decisive evidence is per-impact coverage, defense attempts and energy, plus weapon-specific outbound outcomes.
