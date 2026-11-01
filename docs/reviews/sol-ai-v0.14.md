The queued-slot fix has a lifecycle problem: placement searches see existing buildings, while a queued plant remains only a planned action until turn end. I’m tracing the resulting Mine placement and refund paths; the arithmetic test alone does not establish that the T1 opening works.

The logs also qualify two parts of the diagnosis: Guts’s war gate was T20 in this match, and the human fired 100–120 projectiles at T22–24, then fewer. Those timings matter for both defense catch-up and the research reorder.
The research reorder has a second blocking mismatch: the director fills limited-planet mines with Yield1 plus a refinery, then explicitly rejects a second yield upgrade. Moving Robotic Mining earlier therefore does not give those mines the human’s ×2 upgrade.

Using Guts’s recorded science, the new single-target queue completes Robotic Mining at T20 and Magnetic Radiation Shielding at T21, but pushes Missiles to T24, Ultracapacitors to T25 and Ion Defense to T26. The queue’s one-tech-per-turn behavior adds delay even while science is banked.
Verdict: fix first

**Confidence: High on the code/lifecycle findings; Medium on counterfactual match outcomes.**

The diagnosis correctly identifies inadequate defensive coverage, a poor opening, delayed economic upgrades and ineffective strikes. However, **D does not make a queued plant usable as a same-turn placement anchor**, and **B prioritizes Robotic Mining while the director prevents existing upgraded mines from using it**. The research reorder also delays Missiles until approximately T24 under Guts’s recorded science—after the human’s decisive offensive buildup.

## High findings

### 1. D credits connectors that placement searches cannot use

**Evidence**

- The new credit is applied immediately on purchase: `AI/DirectorPatches.cs:1308–1325`.
- A purchased building enters a queue. Finding its position creates an action, rather than an actual building:  
  `decompiled/Interplanetary.AI/AIConstructionQueue.cs:145–159`.
- That action is executed later by `AIActions.EndTurn`:  
  `decompiled/Interplanetary.AI/AIActions.cs:43–50,69–93`.
- Placement searches enumerate **existing** `BuildingHandler.Buildings` and the existing power-slot dictionary:  
  `decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs:94–126`;  
  `decompiled/Interplanetary.AI/AIPowerGridMaintaining.cs:27–33,41–61`.
- Defense placement likewise requires an existing, Normal anchor: `AI/DirectorPatches.cs:1687–1697`.

**Failure scenario**

At T1, the director can now purchase a Solar Plant followed by a Mine. But the Mine search cannot connect to that planned Solar Plant.

Importantly, the likely failure is **not necessarily abandonment and refund**:

- With no available powered anchor, the native search tries a random position.
- Random placement can succeed without setting `PowerGridConnection`:  
  `…/AIBuildingPlacementSearch.cs:109–115,167–192`.
- The resulting Mine action can therefore place an **unconnected Mine**. Subsequent turns must reconnect it or build another nearby plant.

The plant completing its search first does not solve this: it still remains an action, not an existing anchor. Successful terrain placement is also insufficient to establish a usable power line.

**Connector accounting qualifications**

- A Floating Plant placed independently actually retains all three connectors; subtracting one is conservative numerically: `AI/DirectorPatches.cs:1807–1813`.
- Its water connectors are nevertheless not interchangeable with reachable land capacity.
- A recovery plant usually spends one connector connecting the unpowered anchor. Additional capacity is spatially constrained; reconnecting that anchor may also expose further connectors.
- Real line creation checks both endpoint capacities and distance:  
  `decompiled/Interplanetary.PlayerDataManagement/PlayerPowerGridHandler.cs:63–72`.
- Subtracting the credit when a plant is abandoned reverses the scalar accounting, but does not cancel or repair dependent placements already accepted.

**Concrete fix**

Represent planned plants as **placement dependencies**, with reserved positions and connector capacity. Dependent construction actions must resolve the newly created plant’s actual `BuildingPower`, validate reach and connect in execution order.

Until that exists, do not treat speculative connectors as generally usable slots. A focused T1 plant–Mine compound placement would be a smaller implementation than a general planned-grid system.

The acceptance check must establish **a connected, productive Mine**, not merely that `Best()` selects one. The current tests at `AI/Tests/Program.cs:385–393` establish only the arithmetic and purchase choice.

---

### 2. B’s Robotic Mining investment conflicts with the existing upgrade policy

**Evidence**

- Robotic Mining moves to position 13: `AI/Plugin.cs:153–157`.
- It unlocks the second Mine/Offshore yield upgrades:  
  `docs/ruleset/embedded/enhanced/TechTreeData.csv:32–33`.
- On limited planets, the director reserves the second upgrade slot for a refinery and explicitly rejects another yield multiplier after one is installed:  
  `AI/DirectorPatches.cs:1037–1089`.
- Full upgrade slots exclude the building even earlier: `AI/DirectorPatches.cs:1026–1028`.
- This match repeatedly installs Yield1 plus efficiency upgrades—for example:  
  `docs/playtests/data/v0131-ai-decisions.jsonl:66,70,75,90,95`.

**Failure scenario**

Guts researches Robotic Mining for **1,100 science**, but its established mining estate already has Yield1 and a refinery. Those mines neither have a free slot nor pass the second-yield filter.

The comment that these technologies “upgrade every existing building” is therefore wrong in two respects:

1. Research unlocks upgrades; it does not install them.
2. The director deliberately prevents this particular upgrade path on existing upgraded mines.

New, unupgraded mines could select Yield2, but that does not provide the human’s estate-wide transition. Material glut also suppresses new extraction from T17 in several recorded states.

**Concrete fix**

Choose one consistent policy:

- **Smallest fix:** move Robotic Mining behind the useful near-term military/intel unlocks while the current limited-planet policy remains.
- **If the human-style yield transition is intended:** change upgrade planning deliberately—reserve room for Yield2 or support an explicit upgrade replacement policy, evaluated against remaining planet material and payback.

Do not simply remove the refinery rule globally. Yield2 also multiplies planet consumption by ×2; that earlier depletion concern remains real.

---

### 3. B delays the combat tools past the critical window, amplified by a one-target research queue

**Evidence**

- The new ordering puts four technologies between Experimental Matter Control and the telescope/Missile chain: `AI/Plugin.cs:153–158`.
- The adapter queues one target and returns while that queue remains nonempty: `AI/EconomyPatches.cs:504–537`.
- The game can spend science across multiple queued technologies, but stops at the end of the queue and retains excess science:  
  `decompiled/Interplanetary.PlayerDataManagement/PlayerTechnologyHandler.cs:149–167,405–412`.
- Recorded science and research transitions:  
  `docs/playtests/data/v0131-ai-decisions.jsonl:44–97,105,113,124,131,138`.

**Walk-through**

Using the recorded per-turn science, retained excess science and the current single-target queue gives the following schedule. This reconstruction also reproduces the old plan’s recorded queue transitions.

| Technology | Cumulative plan cost | New queue turn | New completion turn | Old-plan completion |
|---|---:|---:|---:|---:|
| Maglev Servos / Kinetic | 4,500 | T13 | **T14** | T14 |
| Experimental Matter Control | 5,800 | T14 | **T16** | T16 |
| Nanostructuring | 6,400 | T16 | **T17** | T20 |
| Microwave Energy Transmission | 7,150 | T17 | **T18** | T21 |
| Robotic Mining | 8,250 | T19 | **T20** | Not reached |
| Magnetic Radiation Shielding | 8,650 | T20 | **T21** | T23 |
| Self-Adjusting Optics | 8,900 | T21 | **T22** | T17 |
| Advanced Signal Processing | 9,300 | T22 | **T23** | T18 |
| Orbital Assembly / Missile | 9,950 | T23 | **T24** | T19 |
| Ultracapacitors | 10,750 | T24 | **T25** | T24 |
| Plasma Channel Induction / Ion | 11,150 | T25 | **T26** | T22 |

These are **fixed-science counterfactuals**, not a replay prediction: different projects, buildings and combat losses would change science.

The distinction between queue and completion turns matters. The playtest’s “Nanostructuring T19, Microwave T20, Magnetic Radiation Shielding T22” are **target-queue turns**, unlike the human summary’s completed-tech observations.

**Failure scenario**

- Kinetic is not delayed by this change; it still completes T14, with defensive construction gated from T15.
- Missiles are delayed **five turns**, leaving the AI building Railguns through its T20–23 arming window.
- Self-Adjusting Optics—the Railgun building-damage upgrade—and telescope availability are also delayed five turns.
- Ultracapacitors arrives T25, after the recorded arsenal collapse begins.
- Ion at T26 is late for the T24 precautionary anti-nuke program, although this human used no Missiles or nukes, so that delay is less costly in this specific match.

The single-target queue imposes additional idle time. The reconstruction leaves approximately **1,345 science banked at T22** and **1,634 at T23**. With a properly ordered multi-tech queue, the same cumulative science could complete Magnetic Radiation Shielding at T20, Orbital Assembly at T22 and Ultracapacitors at T23.

**Concrete fix**

Queue a short, prerequisite-valid sequence so surplus science advances immediately, preserving urgent counter-research preemption.

Then revise the critical path: keep the genuinely useful energy unlocks early, remove the blocked Robotic Mining detour, and preserve timely telescope/Missile access. Research priorities should reflect **usable purchases and combat deadlines**, rather than copying unlock names alone.

## Medium findings

### 4. C is a reasonable interim increase, but inventory is not the decisive defensive objective

**Evidence and diagnosis corrections**

Desired defenses already include remembered/inferred weapons and damage—not just visible weapons:

- Formula: `AI/Brain/Director.cs:231–236`.
- Adapter inputs: `AI/DirectorPatches.cs:252–269`.
- At T22, desired 27 is principally `13 × 0.4 + 10,849.44 / 500 ≈ 27`, rather than the 15% floor.
- At T24–25, damage already drives desired defenses to 45 and 60:  
  `docs/playtests/data/v0131-ai-decisions.jsonl:111–134`.

Thus “no visible weapons” does **not** mean the desired count was floor-only.

The logged Normal non-city counts are **39–156** over T15–24, rather than 44–150; cities must be excluded.

**Fixed-state application of 0.25**

The following holds each recorded state—including its recorded owned-defense count—constant:

| Turn | Non-city buildings | Desired: old → new | New ordinary-defense turn cap |
|---|---:|---:|---:|
| 15 | 39 | 6 → **10** | 4 |
| 16 | 49 | 8 → **13** | 4 |
| 17 | 60 | 9 → **15** | 3 |
| 18 | 75 | 12 → **19** | 4 |
| 19 | 94 | 15 → **24** | 4 |
| 20 | 122 | 19 → **31** | 6 |
| 21 | 133 | 20 → **34** | 6 |
| 22 | 141 | 27 → **36** | 6 |
| 23 | 156 | 25 → **39** | 6 |
| 24 | 150 | 45 → **45** | 9 |
| 25 | 95 | 60 → **60** | 14 |

Sources: `…/v0131-ai-decisions.jsonl:69–134`; catch-up formula: `AI/Brain/Director.cs:268–273`.

This helps **before** the collapse. It does not change the late damage-driven target.

**Energy and slot consequences**

- Each Kinetic costs **200 energy, 200 material and one connector**: `docs/ruleset/embedded/enhanced/BuildingsData.csv:13–16`.
- At T20, the target increases by 12 defenses: **2,400 energy/material and 12 slots** in total. The state has 73 free slots and 12.8k starting material, so neither aggregate material nor connector count is the obvious constraint.
- Increasing the turn cap from three to six adds **600 energy** of immediate construction demand.
- Energy was already almost fully spent. More defenses therefore displace upgrades, growth or weapons; idle material does not make them free.

The reserve uses **owned defenses at turn start**, not the desired count or newly queued defenses: `AI/DirectorPatches.cs:454–467`.

Its planned component is capped at 30% of income:

- T20: at most **2,578**, versus the recorded 2,250.
- T23: at most **3,986**, already the recorded reserve.
- Observed need can raise it toward the 80% hard cap, subject to firing/repair availability and the recovery floor: `AI/Brain/Director.cs:382–413`.

Consequently, C does not automatically create an uncapped reserve starvation problem. But newly built coverage has no explicit same-turn firing allocation, and the observed reserve responds only after demand is encountered.

**Coverage versus stacking**

The interception diagnosis is substantially correct:

- Every eligible defense is tried until success; no per-defense shot limit exists:  
  `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:223–249`.
- Base Kinetic rolls cost 100 energy at probability 0.8:  
  `docs/ruleset/embedded/enhanced/DefensesData.csv:7,26–27`.
- Across director records T23–29: **426 arrivals, 326 with no defense nearby, 87 interceptions and zero unpaid rolls**. Among arrivals with a matching defense in range, **87/96 = 90.6%** were intercepted.

That strongly favors **coverage first**, not blanket two-deep stacking.

However, building-center coverage is not impact coverage. T23 reports **71.6% building-value coverage**, while 13 of the previous 16 arrivals had no defense nearby: `…/v0131-ai-decisions.jsonl:119`. This prevents concluding that quantity alone is established as the limit.

Ordinary placement assigns `p = 1`, making additional coverage of an already covered target worth zero: `AI/DirectorPatches.cs:1580–1587`; `AI/Brain/Coverage.cs:175–182`.

**Concrete fix**

Retain 0.25 as an interim floor, but drive catch-up from impact-relevant coverage of cities, generators and weapons. Add bounded second-layer protection for that critical core after first-layer gaps are addressed.

Fund the resulting roll demand. For **120 planet arrivals**, one base Kinetic layer requires 12k energy and stops an expected 96; two layers stop 115.2 but require approximately **14.4k energy**. Guts’s peak income was only 13.3k before other spending. Two-deep coverage everywhere is not affordable in this state.

---

### 5. A detects lost operational capacity, not specifically destruction

**Evidence**

The count includes only non-Laser, Normal, powered weapons: `AI/StrategyPatches.cs:169–173`. The trigger uses only the change in that count: `AI/Brain/Strategy.cs:177–181,234–238`.

**Assessment**

- **Recorded collapse:** appropriate. It triggers at **26 → 17** and **17 → 11**, but not **11 → 10**.
- **Lost plants:** unpowered weapons count as lost even if recoverable. This may justify emergency fire, but it is not proof those weapons were destroyed.
- **Sales:** a sold weapon would also count. The current mod’s liquidation path sells mines, so this is not an observed weapon-sale failure.
- **First war turn:** peace-turn counts are retained; losses before that first war decision can trigger it.
- **Earlier saves:** backward/skipped-turn handling clears `LastWeapons`; match resets also clear state. The ordinary earlier-save case is guarded: `AI/StrategyPatches.cs:144–146`.

**Failure scenario**

A temporary connectivity loss reduces the count, causing an otherwise unsaturating strike. That retains firing reservations instead of releasing energy for reconnection. The exemption also permits a strike with only one aimed weapon.

**Concrete fix**

Track intact and powered counts separately and log the loss reason. Prefer the emergency override when operational decline accompanies recent hostile impacts/destruction; prioritize reconnection when most of the decline is recoverable.

**Do not require positive HP damage alone:** intercepted attacks and imminent threat can justify firing. A’s broad emergency behavior is defensible for this match, but should be described and measured as **capacity-loss fire**, not destruction detection.

---

### 6. F’s postfix timing is correct, but its configuration guidance can make aiming worse

**Correctness checks passed**

The postfix is early enough:

1. Native `BeginTurn` creates new entities and registers them.
2. Native code already overwrites their initial allowance at line 109.
3. The postfix reallocates again before normal targeting updates.

References:

- `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:100–109,143–152`.
- `decompiled/Interplanetary.Weapons.Trajectories/TrajectoryCalculator.cs:119–130`.

`ReallocateDesignatedStepAmount` reaches the registered new entities as well as old ones.

`GetTotalAIWeaponAmount` matches the native denominator, but counts WeaponHandler entries—not just powered gravity-search weapons: `decompiled/Interplanetary.GameEngine/PlayerManager.cs:243–254`. Lasers, unpowered weapons and cleanup ordering can therefore make it differ from the logged live count.

**Failure scenario**

The config describes a native budget of **200**, recommends **200–1000**, and claims to affect placement searches: `AI/Plugin.cs:404–405`.

In fact:

- `AICore.BeginTurn` assigns a total budget of **1,200**: `decompiled/Interplanetary.AI/AICore.cs:134–164`.
- Placement retains its separate allowance of **100**: `…/AIConstructionQueue.cs:132–136`.
- At 26 counted weapons, setting 1,000 gives approximately **38 steps**, below the native 46. Setting 200 reaches the 30-step floor.

**Concrete fix**

Correct the description, units and example to a **global trajectory budget**; explain that 2,600 gives 100 steps only when the denominator is at most 26. Log the denominator and effective allowance.

Default zero is a valid opt-in choice, but means v0.14 leaves the default aiming bottleneck unchanged. The match establishes poor aiming yield, not that CPU allowance alone caused it: geometry and search randomness remain confounded.

## Low finding

### 7. E implements the specified backoff, but “no failure” is weaker than confirmed placement

**Evidence**

- T14 failures create throttling through T19, covering the recorded T18 bulk retry: `AI/Brain/Memory.cs:201–235`.
- `Blocked` limits attempts to two, while same-turn abandonment blocks further attempts: `AI/DirectorPatches.cs:1285–1292,1859–1864`.
- Release requires a positive queue count and zero recorded failures—not a success count: `AI/Brain/Memory.cs:220–226`.
- Native end-turn cancellation stops outstanding searches without reporting abandonment: `decompiled/Interplanetary.AI/AIConstructionQueue.cs:139–143`.

**Failure scenario**

A queued probe that remains unresolved when the turn ends can release the backoff without proving that the type fits. Also, repeated one- or two-failure turns do not renew the five-turn dictionary expiry; continued throttling partly relies on the older consecutive-failure mechanism.

**Concrete fix**

Track confirmed placements separately and use successful resolution for release. Keep the existing five-turn behavior otherwise; it is a useful bounded improvement for the observed Offshore Mine retry.

## Ranked next moves

1. **Protect an operational core before T20.**  
   **326/426 arrivals** in the sampled attack period had no defense nearby; unpaid rolls were zero. Target coverage of cities, power and weapons, then bounded overlap. Evaluate the Kinetic range upgrade: **5.9 → 7.67 radius**, approximately **69% more local disk area**, for 50 energy/250 material per upgrade. Broad coverage has higher immediate value than blanket stacking.

2. **Make the research critical path usable and continuous.**  
   At T12 the human had **1,227 science/turn versus 473**, but this was a burst—not a persistent 2× lead every turn. Guts bought only **two permanent science projects** and no global science project in the recorded match. Remove the blocked 1,100-science Robotic detour, queue multiple technologies, and budget science toward dated unlocks. Queue continuity alone moves the fixed-science Missile estimate **T24 → T22**.

3. **Repair the opening with a real connected plant–Mine dependency.**  
   Guts spent **1,200/1,350 material on three Solar Plants**, then left **750–975 energy unused at T2–6**. The human had one Mine at T1 and three at T2. Prove powered production next turn; speculative connector arithmetic does not recover this compounding loss.

4. **Convert weapons into reliable launches and counter-weapon damage.**  
   The decisive strike turns found **6/26 and 9/26 primary trajectories** despite human planning times of **62.4 and 44.3 seconds**. Measure completed trajectory work and accepted shots by weapon type under controlled budgets; then choose an effective default. Preserve timely Missile access and bounded arsenal replacement so the thirty-slot cap does not lock in Railguns. Attrition fire recovers opportunities at T25–26, but cannot compensate for losing the initiative.

5. **Buy intel capability against measured visibility thresholds.**  
   Seven human Security Hubs reached **counterintel ×7.6375**, while Guts peaked at nine telescopes and still reported no readable cities. A telescope yields **8 intel**; its available ×1.5 upgrade yields **12**, but pure-intel upgrades have no positive energy/material/science value in the current economy-option filter: `AI/DirectorPatches.cs:1023–1024,1070–1092`. Value intel upgrades and projects explicitly, stop marginal spending that cannot cross a useful threshold, and measure visible guided-target candidates. Earlier defensive counterintel alone does not cure offensive blindness.

**Verification:** read-only source inspection, both supplied human-summary scripts, and in-memory JSON/CSV calculations. `git diff --check ff7e2cf` passed. No builds, tests, mutations or in-game execution were performed; runtime placement, CPU cost and match outcomes remain **unverified in-game**.
