The slot-allocation problem holds up, but several supporting claims need correction. The reconstructed slot count does not support “zero free slots every turn,” Jamming also suppresses population growth, and A5’s stop rule depends on a hidden counterintel threshold. I’m checking the research timeline and defense accounting before casting the ballot.
Two checks materially change my recommendation. A’s accelerated research budget includes 279 science that the game never credits on turn one, and its Robotic Mining payoff is blocked by the director’s existing one-yield-plus-refinery policy on limited planets. The defense formula already estimates 9,250 E for the bad phase; available energy after firing and repairs is the binding limit. Also, I retract my earlier growth-penalty statement: Jamming declares ×0.9 global population yield, but I found no native growth-path consumer.
# Lane B — cross-review and Round-1 ballot

**Verdict: AMEND lane A’s package.** Its strongest contribution is the combination of flexible city-project slots, earlier science acceleration and an earlier useful strike. Those mechanisms are supported. Its precise research timetable, Telescope economics, defense-reserve diagnosis and projected Missile damage are not reliable enough to implement as written.

**Recommended direction:** build a stronger energy/science engine, convert it into target-appropriate firepower, and exploit currently visible precision opportunities without depending on them remaining available.

**Confidence:** High on the code/data corrections below; Medium on the proposed package’s competitive effect. Outcomes remain **unverified on the live game**.

## Scope and evidence convention

I independently inspected the frozen HEAD, **`24683684d9799b9b22db765c251ac811ef8793ae`**, both sealed reports, the charter/packet/issues, the three match-data pairs, the relevant director/combat code, native contracts and Enhanced ruleset fields.

- **D16 / H16:** `docs/playtests/data/v0153-ai-decisions.jsonl` / `v0153-human-telemetry.jsonl`.
- **D14 / H14:** corresponding `v0141-*` files.
- **D15 / H15:** corresponding `v0151-*` files.
- Native source paths below are under `decompiled/`.
- Ruleset IDs and field names identify the transposed CSV entries.

All six data files matched `data-manifest.txt`. Opening and closing Git porcelain status and HEAD were identical. Both sealed-report hashes were identical across my checks. No files were created, edited or deleted; no builds or tests ran.

**Severity:** High = changes the proposed mechanism, feasibility or expected result; Medium = material qualification or accounting correction; Low = local numerical/timing correction.

---

## 1. Blockers requiring resolution

### Blocker F1 — A5’s hidden-threshold decision rule violates the charter

“Stop once counterintel × 45 exceeds reachable intel” requires the enemy’s current counterintel or hidden city stealth. Those are acceptable **postgame analytical inputs**, not automatically permitted runtime inputs.

Native visibility compares own intel against actual building stealth (`PlayerIntelHandler.cs:176–208`; `BuildingStatHolder.cs:123–128`). The revised policy must consume the **permitted visibility result**, not inspect the hidden threshold.

Also, current `DirectorPatches.cs:323–328` enumerates actual remaining enemy cities to calculate `UnseenCities`. Under this council’s strict boundary, the replacement policy must not depend on the hidden surviving-city count.

**Disposition:** reject that implementation detail; retain a spending/outcome stop-loss.

### Blocker F2 — A3’s timetable is not an executable resource proof

The timetable:

1. credits science on turn one that the game never awards;
2. treats historical production as unchanged after removing three early material projects;
3. assumes project-slot availability for overlapping Jamming/SSTAR schedules;
4. expects Robotic Mining to multiply producers that the current limited-planet upgrade policy prevents from receiving their second yield upgrade.

These invalidate the exact completion/production promises. They do **not** invalidate moving the economy/science technologies forward.

### Blocker F3 — Do not multiply temporary-project utility without preserving common units

`gain × duration/10` is a ten-turn-normalized investment comparison. Replacing it with `gain × duration` while permanent projects retain `gain × 1` introduces a tenfold relative bias.

The definite defect is using duration-adjusted utility as projected next-turn income. Fix that separation and queued compounding; compare investment utilities over a common horizon.

---

## 2. Cross-review of lane A

### Shared baseline checks: A §§3.1–3.2 and 3.7

| Claim | Independent check | Disposition |
|---|---|---|
| Production table | All eight listed rows reproduce under A’s stated convention: **human turn-end state versus AI economy record**. Examples: T20 human **12,947 E / 10,869 M**, AI **7,265 / 7,858**; T24 human **29,410 / 14,344**, AI **11,476 / 19,192**. | **ACCEPT**, with phase qualification. |
| Human already had 12.9k spendable production at T20 | H16:112, the T20 round-start snapshot, has **7,215 E**, not 12,947. The latter is H16:123, after that turn’s investments. AI T20’s record has 7,265. | **AMEND — Medium.** Compare like phases when sizing a purchase/strike deadline. |
| Bolide’s much stronger power engine | D14:101,108,115 reproduce **5,545 → 9,305 → 17,740 E** at T18–T20. D14:115 has **48 Floating Plants / 33 Offshore Mines**. | **ACCEPT.** |
| Labo’s T20 plant inventory | D16:94 has **11 Floating Plants / 18 Nuclear Plants / 34 Offshore Mines**. A says twelve Floating Plants. | **AMEND — Low.** |
| “EnergyLeft 5–155 on 20 of 26 turns T6–T26” | T6–T26 contains **21 turns**. Counting the records gives **16 of 21** in that interval. | **REJECT the count — Low.** The broader energy-pressure diagnosis survives. |
| Energy is spent almost to zero | `director.energyLeft` excludes protected defense money. At T24 it is **34**, but `defenseReserve=3,442` and `idleEnergy=3,476` (D16:127). | **AMEND — Medium.** This measures discretionary construction exhaustion, not zero action-phase energy. |
| Material-spending arithmetic | `19,192 − (26,966 − 18,699)=10,925` reproduces, but equals the preceding T23 construction spend. Direct T24 construction spend is `26,966−21,241=5,725`. T26’s stock-difference calculation also includes that turn’s **5,775 M repairs**. | **AMEND — Medium.** The calculations are not clean same-turn spending ratios. |
| Current energy target depresses energy preference | At T24: `0.6×min(19,192,11,062.38)+4,550=11,187.43`, versus 11,476 actual E. At T16, wanted energy is `0.6×5,148=3,088.8`, below 4,625 actual, so the ordinary weights favor material. | **ACCEPT.** `Brain/Director.cs:84–120`. |
| About forty Offshore Mines bought in the cited interval | Summing actual queue entries gives **61 at T14–T24**, or **51 at T16–T24**. Inventories rise **12→73** from T14 to T24. | **AMEND — Medium.** A’s substitution arithmetic understates and misidentifies the historical purchases. |
| Opening sacrificed power growth | Human T1 Solar+Mine and Offshore purchase at T7 reproduce. AI queued its first Floating Plants at **T8**, not T9, and its first Offshore Mine at T9, present T10. AI T8 material income **1,900** versus human turn-end **1,725** reproduces. | **ACCEPT mechanism; AMEND dates — Low.** |

The comparison supports **misallocated investment**, rather than a simple universal shortage of all resources.

### A1 — Project-slot policy and stacked multipliers

**Proposal ballot: AMEND. Severity: High.**

#### What reproduces

- Five permanent Molecular Recycling Plants were queued by T7, and two Quantum Encryption Hubs by T11: D16:19,26,30,34,38,53.
- Native slots unlock at **250/500/750 population**, and old permanent projects cannot be freely cancelled: `BuildingProjects.cs:122–158,190–208,423–447`.
- The population reconstruction gives:

  | Turn | Unlocked slots |
  |---|---:|
  | T1 | 3 |
  | T5 | 4 |
  | T6 | 6 |
  | T11 | 7 |
  | T17 | 8 |
  | T20 | 9 |
  | T22 | 10 |
  | T23 | 11 |
  | T25 | 12 |

  Therefore seven queued permanent projects account for all seven slots at T14. This independently supports the central diagnosis.

- SSTAR’s ruleset values reproduce: **150 E / 300 M**, six turns, **×1.3 energy / ×0.9 material**, one copy per city.
- Global multipliers multiply (`GlobalStatModifier.cs:53–59`):

  - Three copies: `1.3³=2.197`; `0.9³=0.729`.
  - Five copies: `1.3⁵=3.71293`; `0.9⁵=0.59049`.
  - Frozen T20 base: **7,265 → 15,961 E** with three copies, or **26,974 E** with five.
  - Frozen T16 base with two: **7,816 E**.
  - Frozen T24 base with five: **42,610 E**.

These are valid **fixed-base leverage calculations**, not a replay of the revised opening.

#### What needs correction

1. **“Zero free slots on every turn T7–T30” is not established.**  
   At T7, six unlocked slots versus five permanent purchases leaves one unaccounted-for slot. Several subsequent unlock dates are also one turn earlier than A states. Later temporary projects consume additional capacity. Installed identities are absent, so universal occupancy remains **UNVERIFIED**.

2. **SSTAR’s score is not A’s positive-energy-only calculation.**  
   The scorer includes its negative material gain and resource weights. At a frozen T16 snapshot:

   ```
   energy utility = 0.5 × 4,625 × 0.3 × 0.6 = 416.25
   material utility = −5,148 × 0.1 × 0.6 = −308.88
   score = 107.37 / (300 + 0.3×150) = 0.311
   ```

   A base Floating Plant under the same weights scores:

   ```
   0.5×100 / (300 + 0.3×250) = 0.133
   ```

   SSTAR can still win, but “ten times better” and “would be bought on any free-slot turn” are not demonstrated. See `DirectorPatches.cs:1266–1288`; `Brain/Director.cs:178–183`.

3. **Five Jammings and three SSTARs cannot be scheduled independently.**  
   With two permanent projects, that combination needs **ten slots**, versus the historical eight at T17. The planner must schedule their overlap, expiry and per-city eligibility.

4. **Less material production does not necessarily mean less planet drain.**  
   SSTAR lowers material yield, not the extraction-cost field. When extraction cost already exceeds production, `max(cost,production)` is unchanged (`PlayerResourceHandler.cs:163–183`). Reject the claimed automatic conservation benefit on a 100k planet.

5. **No enemy Telescope building is not proof that enemy intel is absent.**  
   H16:123 has **77 intel at T20 with no Telescope Array**; the first recorded arrays appear at T24. City projects provide intel. Avoiding permanent counterintel is a defensible slot trade, not evidence that the AI cannot be observed.

6. **Human project identities remain inferred.**  
   H16’s science and energy changes strongly support multiplier use. They do not uniquely establish every active project and placement. The exact “two SSTARs landed at T24” reconstruction is **UNVERIFIED** without project telemetry.

**Replacement mechanism:** reserve flexible capacity per city, schedule Jamming/SSTAR together, separate production forecasts from investment utility, and compound pending global effects correctly. A two-permanent opening ceiling is a reasonable experiment, not a complete slot policy.

### A2 — Energy/material target and earlier material-glut recognition

**Proposal ballot: AMEND. Severity: Medium.**

The **direction** is supported. The exact **1.0 growth / 2.0 war** targets are not justified by the human’s total E:M ratio.

`WantedEnergy` already adds weapon upkeep. At historical T24:

- Existing target: **11,187 E**.
- Ratio 2.0 with the same inputs: `2×11,062.38+4,550=26,674.76 E`.

If upkeep approaches 40% of income, solving `E=2M+0.4E` gives **E:M≈3.33**, not the human’s cited 2.05. This is not necessarily bad, but it is a different policy.

Also:

- Lowering `MaterialGlutRatio` to 1.5 still leaves `MaterialGlutMin=5,000`; it does not trigger at T14–T15.
- Against the historical construction-budget denominator, it would trigger at T17–T18, where the current ratio does not.
- Forty replacement plants’ output cannot simply be added while retaining the mines’ historical income, bank and research-funded investment path.
- Requiring `energyLeft>300` rewards unspent discretionary energy rather than productive conversion.

**Replacement:** initially use **`EnergyToMaterialTarget=1.0` in both phases**, retain the additive live firing requirement, and trial **`MaterialGlutRatio=1.5`**. Measure production and material conversion. A separate 2.0 war coefficient should require evidence from that result.

### A3 — Economy/science-first research order

**Proposal ballot: AMEND. Severity: High.**

#### Science acceleration: supported

H16 turn-end values reproduce:

- T11: `945 / (2,150×0.18) ≈ 2.442`, consistent with four Jammings.
- T12: `1,221 / (2,216×0.18) ≈ 3.061`, consistent with five, allowing native per-city rounding.
- T17–T20: approximately **×1.95**, consistent with three.

Summing turn-end science above `population×0.18` at T11–T20 gives approximately **5,167.5 additional science**, close to A’s “about 5,100.” This is a nominal production reconstruction, not an exact credited-science ledger.

The inferred eight purchases cost **7,200 M and 1,600 E**. Exact installed counts remain inferred.

#### Timetable and payoff: amend

1. **No T1 science credit.**  
   `PlayerResourceHandler.cs:161–185` awards science only when turn >1; H16:4 explicitly records T1 science credit zero.

   Subtracting the erroneously included 279 from A’s cumulative sums gives:

   | Turn | Credited-science proxy |
   |---|---:|
   | T9 | 2,581 |
   | T11 | 3,326 |
   | T12 | 3,715 |
   | T13 | 4,118 |

   The pure EMC chain costs **3,350**, so it does not finish at T11 on that history. A’s listed order also includes Smart Grids, making the total **3,850**: the frozen baseline first covers that at **T13**, not T12.

2. **The remaining dates must move or be re-derived.**  
   Even retaining historical population, four Jammings installed at T13 give roughly `417×1.25⁴≈1,018` science at T14. Combined with 4,118 through T13, that is approximately **5,136**, below the listed 5,200 required through Nano+Microwave. The stated T14 completion does not follow.

3. **The human’s listed T12–T21 technologies total 10,150 science, not 11,150.**  
   I summed the actual completed IDs against `TechTreeData.csv.science_cost`; H16:54–131 also shows Robotic Mining completed at T15.

4. **Robotic Mining does not automatically double the AI’s current mines.**  
   `DirectorPatches.cs:1124–1173` deliberately keeps one yield upgrade plus efficiency on limited planets. Existing two-slot producers may have no room, and the scorer rejects another yield multiplier after the first. Researching Robotic Mining alone does not implement A’s payoff.

5. **A retaliation trigger does not unlock missing defenses.**  
   The revised order must preserve an observed-threat counter-research escape. `WarDamageTrigger` alone cannot substitute for Maglev/Plasma availability.

**Replacement:** advance the economy/science chain, but replace the exact Gantt chart with logged completion gates. Include prerequisite closure, real science credits, slot scheduling and the actual selected mine-upgrade policy. Do not automatically fund Robotic Mining when its upgrades cannot be installed.

### A4 — Earlier weapons and the visible-city strike window

**Proposal ballot: AMEND. Severity: High.**

#### Opportunity: reproduced

- T21: nine Missiles, **7.65 expected arrivals/passing**, 100% estimated passage, held below twelve.
- T22: eleven Missiles plus two Railguns, **10.007 passing**, held again.
- Both turns had **two currently visible cities**, not five continuously visible cities.
- T23 had no currently visible city.

Sources: D16:97–119. `target.cities=5` at T21–T22 is aggregate population readability, not five legal coordinate targets.

The actual Missile portions represent:

```
T21: 9×0.85×150  = 1,147.5 nominal expected population damage
T22: 11×0.85×150 = 1,402.5
Total            = 2,550
```

That supports a serious focused opportunity. It does not prove two kills or destruction of the whole population.

A’s larger **2,900–3,600** estimate:

- includes T23’s already-blind salvo;
- treats mixed-weapon passing counts as Missile population damage;
- applies accuracy again to estimates that already include accuracy.

**Reject that damage calculation.**

#### Arsenal feasibility: conditional

Ruleset costs independently give:

| Arsenal | Build E | Build M | Firing E |
|---|---:|---:|---:|
| 25 Missiles | 6,250 | 11,250 | 7,500 |
| 30 Missiles | 7,500 | 13,500 | 9,000 |

At the retained 0.4 upkeep share, these require **18,750–22,500 E income**, not merely the proposed 12,000 precondition.

The aim budget gives approximately `2,600/30≈87` steps per weapon, but successful trajectories for this proposed arsenal are **UNVERIFIED**.

Finally, T21–T23’s holds released **10,000 E** for investment. Earlier firing has an opportunity cost.

**Replacement:** allow early arming from T16 when technology, projected unexpired income and useful current targets justify it. Add a **funded visible-target opportunity exception** to the twelve-shot rule. Retain artillery as the plan when visibility closes; do not require twenty-five Missiles by T19 as proof of the entire design.

### A5 — Telescope upgrade, ceiling and stop-loss

**Proposal ballot: AMEND. Severity: High; fairness blocker F1 applies.**

#### Upgrade omission: reproduced

`AddUpgradeOptions` values energy, material and science gains but not intel (`DirectorPatches.cs:1154–1176`). The intel upgrade therefore receives no positive option value.

Its ruleset effect is:

- **50 E / 175 M**;
- **+4 intel** on a base eight-intel array;
- upkeep **20→32 E**, because `turn_cost_energy_multiplier=1.6`.

#### Package economics: A’s saving is wrong

| Package | Build/upgrade E | Build/upgrade M | Upkeep E/turn | Intel |
|---|---:|---:|---:|---:|
| 30 base arrays | 6,000 | 9,000 | 600 | 240 |
| 20 upgraded arrays | 5,000 | 9,500 | 640 | 240 |

The proposed replacement saves **1,000 construction E and ten footprints**, but costs **500 more M and 40 more upkeep E/turn**. It does not save 3,000 M and 200 recurring E.

#### Visibility dates: amend

A’s threshold series mixes human turn-end counterintel with AI decision turns and ignores city-specific project stealth modifiers. For example, H16 T20 round-start counterintel is **4.65**, versus **5.35** at turn-end. Projects can also multiply a particular city’s base stealth by 0.75.

Even A’s unmodified-small-city calculation has:

```
45×5.35 = 240.75 → native rounded stealth 241
20 upgraded arrays = 240
```

Thus its own calculation does not guarantee visibility there. Exact extended visibility for every city is **UNVERIFIED**.

**Replacement:** price upgrades and new arrays together, including upkeep, under one visibility-acquisition budget. Time the bid to a prospective useful precision arsenal. Stop after unsuccessful marginal batches; probe sparingly. Never read hidden counterintel to decide whether to stop.

### A6 — Fund defense rolls against the mass volley

**Proposal ballot: AMEND. Severity: High.**

The raw events reproduce:

- **83 impacts**;
- **27 with no eligible defense**, hence `83−27=56` with one;
- **23 intercepted**;
- **40 unpaid-roll events**;
- **3,400 E spent**.

Source: D16:191, reporting the preceding phase.

However, the existing implementation already calculates:

```
(3,400 + min(40,56)×100)×1.25 = 9,250 E
```

That is exactly the logged `observedNeed`. See `Brain/Director.cs:437–443` and `DirectorPatches.cs:266–276`.

The binding funding sequence at T26 was:

```
14,725 production
−5,100 existing firing reservation
−3,600 repairs
=6,025 remaining before construction reserve

6,025 − 0.2×14,725 recovery floor = 3,080 maximum reserve
```

D16:167–169 records those values. Raising the planned cap from 0.3 to 0.4 **alone cannot overcome this limit**.

Further corrections:

- `56×100=5,600`; **7,000** is that amount with a 25% margin, not a measured exact requirement.
- Forty unpaid events are not forty distinct preventable impacts. Overlapping defenses can report several failures for one impact.
- “About thirty more stops” is **UNVERIFIED**.
- “Seventeen fewer lost structures” does not follow from a damage-events-per-impact ratio. Damage events are not destroyed buildings.

**Replacement:** retain the existing observed-demand estimator, improve live-cost/overlap diagnostics, and protect a defense allocation before discretionary repairs consume it. Combine funding with range/cost upgrades; evaluate the result against an actual large volley.

### Other load-bearing claims and cautions

- **“All five cities visible T8–T12” reproduces; continuous visibility afterward does not.** D16 also has zero unseen cities at **T15**, an exception omitted by A’s later summary.
- **No human Ion defenses:** no Ion is recorded in H16’s captured inventories. This is postgame evidence, not permission to assume hidden Ion absence at runtime.
- **Upkeep pressure:** historical completed arsenals sit close to `0.4×income`; the restriction is real. It is not proof that every possible weapon purchase was solely cap-bound.
- **Blind Missile failure:** independently reproduced as **45 zero-candidate assignments**, `13+13+14+4+1`, T24–T28. Native fallback is a random surface point (`AIWeaponTargeting.cs:493–496`).
- **A’s fallback “6,000 HP per 1,000 E through thirty Kinetics”: UNVERIFIED.** The cited study’s Triple/Airburst row gives **4,409 before interception and accuracy**, under its assumed structure distribution. I cannot reproduce A’s stronger number from that model.
- **Do not increase MaxWeapons blindly:** ACCEPT. Thirty upgraded Railguns already produce ninety projectiles; more aimed buildings divide the trajectory budget.
- **Do not buy extraction merely because material exists:** ACCEPT as a marginal-investment rule, not a prohibition on maintaining a useful war/recovery bank.

---

## 3. Amendments to my sealed lane-B report

These amendments supersede only the named parts; the sealed file remains unchanged.

### B-Amendment 1 — B1/E1: explicitly include science tempo

**Supersedes:** B1’s predominantly SSTAR-focused framing and B1–B4 implementation priority.

A correctly identifies an important omission: **Jamming and the research sequence belong inside the flexible-slot design**. Earlier EMC/Nano/Microwave deserve more prominence than my sealed package gave them.

Use a jointly scheduled Jamming/SSTAR policy rather than independent copy targets.

### B-Amendment 2 — E1/B1: utility normalization versus forecast error

**Supersedes:** any interpretation that the 60% duration normalization is inherently erroneous.

The definite bugs are:

- feeding normalized utility into projected next-turn income;
- failing to compound pending global modifiers correctly.

Investment utility must use a common horizon. Simply giving temporary projects ten times their present relative score is not the correction.

### B-Amendment 3 — B4: defer the general selective-firing roster

**Supersedes:** making the full latched-roster architecture part of the initial v0.16 package.

A’s income argument is persuasive enough to try the simpler route first. At **20k income**, the existing 0.4 cap allows 8k firing E: enough for twenty-four Triple/Airburst Railguns at **6,720 E**.

For the initial package:

- retain the 0.4 share and thirty-weapon cap;
- correct base-versus-live firing-cost bookkeeping;
- add the visible-target opportunity exception;
- fix per-class planned hit selection.

Defer generalized arsenal ownership versus selected firing rosters until logs show it remains binding after the economy changes. My original B4 adapter-risk warning remains valid.

### B-Amendment 4 — B3: acquisition timing and all-in intel cost

**Supersedes:** treating six-to-eight arrays as a generally desirable early package.

Strict visible-only play makes a T8 sighting worthless for a T20 precision strike if the city has disappeared. Acquisition should be timed to a prospective useful arsenal and priced with **upkeep**, including the upgrade’s additional twelve E/turn.

A fixed twenty-array target is not a better universal replacement.

### B-Amendment 5 — B5: reserve priority is part of readiness

**Supersedes:** any reading that defense upgrades plus higher production alone complete B5.

The existing reserve estimator already reacts strongly to unpaid rolls. Repair/firing/construction allocation must expose and resolve the funding conflict. Range upgrades increase the demand for funded rolls.

### B-Amendment 6 — clarification of my progress commentary

I initially said Jamming suppresses population growth. The ruleset declares **×0.9 global population yield**, and `BuildingProjectStats.cs:79` registers it, but the inspected native growth path does not consume that modifier:

- `BuildingStatHolder.cs:108–115,411–414`;
- `PlayerBuildingHandler.cs:322–329`;
- `PlayerData.cs:446–455`.

I therefore withdraw the asserted implemented growth penalty. Its runtime effect is **UNVERIFIED**; do not add a new penalty as part of this AI update.

---

## 4. Round-1 ballot: I1–I10

### I1 — **AMEND**

**Evidence:** seven early permanent purchases account for T14’s seven slots, while the adapter conflates duration-adjusted utility with projected production.

**Exact replacement wording:**

> Preserve flexible project capacity per city; use an opening ceiling of two permanent purchases as an initial policy, schedule Jamming and SSTAR against actual unlocked/pending capacity, forecast their compounded production and expiry separately from investment utility, and compare temporary/permanent investments over a common horizon.

### I2 — **ACCEPT**

**Evidence:** H16’s approximately ×2.44–×3.05 science intervals and roughly 5.17k nominal additional science support earlier Jamming/EMC, subject to I1’s scheduling and real resource credits.

### I3 — **AMEND**

**Evidence:** the existing target already adds firing upkeep, so a 2.0 war coefficient does not reproduce the human’s total 2.05 E:M ratio.

**Exact replacement wording:**

> Trial EnergyToMaterialTarget 1.0 in growth and war, retaining the additive live firing requirement, and MaterialGlutRatio 1.5 with the existing minimum stock. Evaluate production and conversion outcomes before introducing a separate higher war coefficient.

### I4 — **AMEND**

**Evidence:** the 3,850-science opening first fits the frozen credited-science proxy at T13, and Robotic Mining’s promised second-yield payoff conflicts with the current limited-planet upgrade policy.

**Exact replacement wording:**

> Reorder research for an executable economy/science tempo: earlier Super Tensile Materials, EMC, Nanostructuring, Microwave and Lossless; advance Ultracapacitors/Composite Heat Cells for artillery and Plasma for scheduled Ion cover; retain observed-threat counter-research. Use real credits and prerequisite closure, and fund Robotic Mining only when an installable yield-upgrade path is selected.

### I5 — **AMEND**

**Evidence:** T21–T22 had useful visible-city opportunities below twelve expected passing shots, but T23 did not, and 12k income cannot support twenty-five base Missiles at the retained cap.

**Exact replacement wording:**

> Permit early arming from T16 when technology and funded, unexpired income support it. Bypass MinPassingShots for a funded attack whose current visible targets and allocated expected damage satisfy a credible kill rule; do not bypass it for passage probability alone. Retain retaliation and a blind-artillery fallback.

### I6 — **AMEND**

**Evidence:** Railguns scatter natively without second-phase targeting, but an invisible city does not make a currently visible weapon target useless.

**Exact replacement wording:**

> When no useful currently visible second-phase target exists, remove the precision premium from Missile/Nuclear purchasing and favor deliberate upgraded unguided artillery, initially Triple Barrel plus Optimized Airburst. Preserve precision options when current visible cities or other valuable legal targets justify them.

### I7 — **AMEND**

**Evidence:** twenty-four upgraded Railguns fit a 20k-income 0.4 cap, while my generalized roster introduces significant reservation risk.

**Exact replacement wording:**

> Retain WarUpkeepShare 0.4 and MaxWeapons 30 for the initial package; correct live firing-cost accounting and enforce separate 0.7/0.85 planned class quotas. Defer generalized selective firing rosters until post-economy logs demonstrate that blanket ownership reservation remains the limiting factor.

### I8 — **AMEND**

**Evidence:** twenty upgraded arrays cost more material and recurring energy than thirty base arrays at equal intel, and hidden counterintel cannot drive the stop rule.

**Exact replacement wording:**

> Use one budget for intel buildings and upgrades, including recurring upkeep, timed to useful prospective precision fire. Escalate only after measuring permitted current visibility outcomes; suspend after two unsuccessful marginal batches and allow bounded probes. Do not use hidden counterintel, hidden city stealth or the true hidden surviving-city count.

### I9 — **AMEND**

**Evidence:** the current estimator already requested 9,250 E; the historical reserve was constrained by firing, repairs and recovery protection.

**Exact replacement wording:**

> Combine powered Kinetic range/cost upgrades with a funded defense allocation protected before discretionary repairs. Retain the existing observed-demand estimator, improve distinct-impact/live-cost diagnostics, and evaluate reserve caps together with recovery and firing commitments; do not treat unpaid-roll events as distinct stoppable impacts.

### I10 — **ACCEPT**

**Evidence:** D14’s Missile defeat demonstrates the insurance value, while D16 shows that a requested Ion count cannot overcome missing Plasma research or infeasible late placement.

### One non-negotiable

**Counterintel must genuinely remove precision opportunities.** Once a city is no longer currently visible, no coordinate, live durability, hidden threshold or remembered city record may rescue its targeting. Otherwise the proposed win would fail the council’s decision question regardless of its score.

---

## 5. Unassigned lens: Pavol’s weak windows and likely adaptation

**The exploitable weakness is the transition from protected economic growth to mass armament, not an indefinitely undefended opening.**

- **Match 16:** H16 has no weapons through T22, twenty Railguns at T23 and thirty-five at T24; first recorded firing is T25, followed by 105-projectile phases at T26–T27. There are no recorded Ions. The practical precision opening is narrower: D16 still has two visible cities at T21–T22, then none at T23. A useful early strike could remove population, science and project capacity before the mass volley.
- **v0.14.1:** Pavol instead transitioned to Missiles: eleven at T21, first fourteen-shot phase at T23. Bolide’s economic lead did not compensate for exposed cities. Expect Pavol to choose this branch again if the revised AI sacrifices counterintel or guided protection.
- **v0.15.1:** the early-fire variant began at T8 and retained much weaker economic growth. Human T20 **turn-end** energy was **2,575**, versus **10,110** in v0.14.1 and **12,947** in match 16. These are different matches, not a controlled causal estimate, but they demonstrate that disruption has a substantial opportunity cost.

**Likely response after one successful exploit:** earlier counterintel closes the precision window; one funded Ion per city makes base guided passage 20% where covered; earlier low-volume Railguns disrupt the AI’s own multiplier build; stronger funded Kinetic coverage answers blind artillery. The robust answer is a flexible economy and target-aware switch, not a hard-coded T19 Missile rush against assumed zero Ions.

---

## 6. Final combined v0.16 list — ranked, maximum six

The thresholds below are **experimental next-match gates**, not predicted results. An absent qualifying opportunity is **UNTESTED**, not a passed gate.

### 1. Joint flexible-slot engine: Jamming into SSTAR

**Sources:** A1/A3, B1.

**Change paths:** `DirectorPatches.CityForProject`, `TryCityProject`, `AddProjectOptions`, `TryEconomy`; project forecasts; `Plugin.DefaultResearchPlan` and its unchanged-default migration.

- Start with at most two permanent opening projects.
- Reserve capacity by city, including pending projects.
- Schedule Jamming and SSTAR together.
- Separate investment utility, current spending and next-turn production; compound queued modifiers and account for expiry.
- Advance the EMC chain.

**Next-match gates:**

- Log installed/pending projects and unlocked/free/reserved slots per city.
- **Zero permanent purchases into reserved capacity.**
- EMC completed by **T14**.
- When at least 1,500 science of useful economy research remains, **≥3 simultaneous Jammings within two turns of EMC**.
- **≥3 simultaneous SSTARs by T20** and **≥20,000 round-start E production at T22**.

**Main risk:** removing early material projects changes the bank funding this plan; the fixed-base multiplier illustrations cannot hide that trade.

### 2. Correct the energy/material investment balance and yield timing

**Sources:** A2/A3.

**Config changes:** `EnergyToMaterialTarget: 0.6→1.0`; `MaterialGlutRatio: 3→1.5`; retain `MaterialGlutMin=5000` and the additive firing requirement.

**Change paths:** `Brain/Director.WantedEnergy/Weights/Best`, research ordering in `Plugin.cs`/`EconomyPatches.cs`.

Advance Nano/Microwave/Lossless so plants already owned receive their high-return upgrades. Do not promise Robotic Mining output under the current incompatible upgrade-slot policy.

**Next-match gates:**

- **≥15,000 round-start E production at T20**.
- At T22, end-of-construction material stock **≤2×that turn’s round-start E production**.
- Log top affordable economy alternatives and rejection reasons; **zero extraction-only purchases while the revised material-glut state is active**, except an explicitly identified recovery need.

**Main risk:** excessive energy preference can create material starvation; retain the stock and affordability diagnostics.

### 3. Target-aware offense, early opportunity fire and class-correct accuracy

**Sources:** A4, B2, amended B4.

**Config/path changes:**

- `WarFromTurn: 20→16`, with consistent technology/funding/opportunity gates in the director and strategy.
- Retain `WarUpkeepShare=0.4`, `MaxWeapons=30`, `MinPassingShots=12`.
- Add a specific visible-target opportunity exception.
- Change `DirectorPatches.TryWeapon` and deliberate weapon-upgrade selection.
- Advance Ultracapacitors/Composite Heat Cells.
- Correct live-cost accounting in purchasing/repair protection.
- Fix class selection in `Patches.GoalBeginTurnPostfix`/firing-list handling; retain **0.7 unguided / 0.85 guided**.

**Next-match gates:**

- **Zero below-saturation holds** when the logged funded visible-target kill predicate is true.
- By T24, **≥60 live artillery projectiles per planned blind salvo**.
- After blindness, **≤10% paid firing energy on zero-candidate precision weapons**.
- **Zero funding overruns**.
- Per-class planned-hit totals within **one weapon of their carry-adjusted quotas**; log realized arrivals separately.

**Main risk:** precision opportunities may disappear before the arsenal is operational. Artillery must remain an executable branch.

### 4. Bounded intel acquisition with all-in pricing

**Sources:** amended A5/B3.

**Change paths:** director intel priorities, `EconomyDirector.IntelPerTurnLimit`, intel-upgrade options, and `EconomyPatches.cs:507–508` native urgent-intel escape.

Replace unconditional unseen-city escalation with timed bids, result checks and stop-loss. Price the upgrade’s additional upkeep. Protect the opening’s selected core investment across the full priority sequence.

**Next-match gates:**

- Through T20, cumulative intel construction/upgrade energy **≤10% of cumulative own energy credits**; log upkeep separately.
- After **two unsuccessful marginal batches**, log stop-loss and buy **no more than two additional arrays through T25**.
- Runtime decision inputs include **zero hidden counterintel/stealth/surviving-city values**.

**Main risk:** stopping too early can miss an affordable strike window; bounded probes preserve an option without restarting the race.

### 5. Operational Kinetic defense: upgrades and protected funding

**Sources:** amended A6/B5.

**Config/path changes:**

- Trial `DefenseReserveShare: 0.3→0.4`.
- Retain the existing **0.8 observed hard cap** and **0.2 recovery protection**.
- Change repair/reserve ordering so discretionary repairs cannot silently consume the intended defense allocation.
- Add Kinetic range/cost options and timely Early Warning AI research.
- Log powered coverage, live activation costs and funding shortfalls.

**Next-match gates:**

- By T24, **≥20 powered range-upgraded Kinetics**.
- In the first qualifying **≥60-impact Railgun phase**: **≥50% intercepted and zero unpaid-roll events**.
- Every reserve shortfall has a logged allocation reason; no unexplained difference between requested and funded reserve.

**Main risk:** larger coverage increases roll demand. Failure must distinguish insufficient income, geometry and allocation.

### 6. Bounded guided-threat insurance with executable placement

**Source:** B6.

**Config/path changes:** advance Plasma after Maglev for the cover schedule; move `AntiNukePriorTurn` from **24→18** for one-per-city insurance; retain escalation on observed guided threats.

Reserve a small number of feasible own-planet cover sites/connectors before ordinary construction blocks them. Preserve native availability, collision, power and activation timing. Deduplicate terminal search logging.

**Next-match gates:**

- Plasma completed by **T18**.
- **Minimum operational city Ion cover ≥1 by T20**.
- **Exactly one terminal result per search ID**.
- On an observed guided threat, log the funded two-cover response and whether it becomes operational before the next feasible large volley.

**Main risk:** speculative cover competes with the engine. Keep the prior bounded to one per city; do not build ten speculative Ions by default.

### Overall outcome gate

The package has not achieved its purpose merely by meeting its production targets. In the next usual-build-out match, require the AI to **survive the first qualifying mass-volley phase without losing more than one city**, and evaluate whether its earlier attacks produced persistent losses rather than only nominal arrivals. Human telemetry may support that postgame evaluation; it must not feed hidden values into runtime decisions.

**Bottom line:** accept lane A’s multiplier-and-tempo insight; amend its arithmetic, scheduling and fairness details. Keep the first implementation simpler than my sealed B4, and judge it by funded useful damage and survival—not ownership counts, theoretical passing shots or a larger bank.

END-OF-REPORT
