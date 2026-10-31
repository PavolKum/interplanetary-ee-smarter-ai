The raw telemetry changes one key premise: the human already had 20 Railguns and recorded shots at T22, then reached 51 guns by T27. The stronger distinction is selective firing versus the AI’s automatic firing budget. I’m also checking whether the energy spikes came from projects; building counts alone cannot establish that.
Verdict: Replace turn-15 continuous war with a growth-first phase controller that secures energy multipliers, preserves investment capacity, and launches concentrated volleys when expected damage justifies the cost.

## A. Diagnosis

**High confidence in the mechanisms; medium confidence in their relative contribution.** These causes reinforce one another, so assigning additive percentages would be false precision.

Below, **D** means `docs/playtests/data/v091-ai-decisions.jsonl`; **H** means `docs/playtests/data/v091-human-telemetry.jsonl`.

**1. The AI misses the economic breakthrough, then cannot finance growth, defense and offense simultaneously.** At T30, human energy production was 15,002 versus Guts’ 6,800: a **2.21× gap**. The human’s T29 jump from 9,677 to 16,354, with unchanged buildings, is exactly **×1.3²**; material production simultaneously falls from 7,758 to 6,284, exactly **×0.9²**. That is compelling evidence of two SSTAR Reactor Trials projects, including their material penalty (H:601–604).

Lossless is #31 in the AI plan, behind extensive military research; the human completed it by T15. The prerequisite reactors plus Lossless cost only **1,300 science** after Mass Biochemical Processing (`AI/Plugin.cs:102–110`; `docs/ruleset/embedded/enhanced/TechTreeData.csv:4–10`).

**2. Automatic military commitments crowd out investment.** Across Guts T18–28, 69,400 energy income became:

- 24,050 reserved for existing weapons: **34.7%**.
- 20,844 defense reserve: **30.0%**.
- 24,506 construction budget: **35.3%**, before military purchases, intel and repairs to the power network.

Guts queued **no projects during those eleven turns**. Construction residuals totaled only 256 energy. This is primarily an allocation problem, not inability to spend available construction energy (D, economy/director T18–28).

**3. Fighting splits force and damages the investment engine.** During T16–28, **442/801 powered-weapon assignments—55%—were AI-versus-AI**. These are assignments, not confirmed shots. Knight’s science fell from 516 at T18 to 113 at T29. The third player benefited while both rivals paid.

**4. Defense is a funded service, not a building count.** Guts’ T33 director record reports the preceding action phase: 73 incoming impacts, 19 intercepted, 26 with no eligible defense, and 36 unpaid defense-roll events. Unpaid rolls are not distinct projectiles. Both coverage holes and exhausted resources mattered (D:358). Thirty Kinetic Defenses did not mean thirty permanently available interceptors.

Two premise corrections: the human had **20 Railguns by T22**, recorded shots at T22, and reached 51 guns by T27—not an entirely weaponless opening until T28 (H:129–151,192). Also, AI “powered” counts overstate firing: Guts T28 had 41 powered entries but only 13 firing-list entries (D:286).

Finally, economy alone cannot explain all nine losses: v0.4.1 had **25.8k versus 11.8k human energy at T26** and still lost (`docs/playtests/2026-10-31-v0.4.1.md:6–38`).

## B. Strategic posture and fire discipline

**Unconditional continuous war is wrong; unconditional turtling would also be wrong.** Early fire is worthwhile when it kills exposed cities, suppresses a dangerous economy, or cheaply finishes an opponent. Otherwise, unsuccessful harassment sacrifices compounding growth.

**Unfired weapons have no standing energy/material upkeep in the Enhanced building data.** Firing costs are charged when targeting is committed (`decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:186–225`). The native AI nevertheless subtracts every normal weapon’s firing cost before construction (`decompiled/Interplanetary.AI/AIResources.cs:38–62`).

Therefore, skipping shots must also release their **planning reservation**. Merely withholding fire after construction leaves the investment opportunity unused.

Choose a strike using:

- Powered weapons, projectile multiplicity, reachable trajectories and human-parity accuracy.
- Visible/remembered defense types and locations, plus observed interception outcomes.
- A range for enemy defensive energy, inferred from intel-visible round-start production and past behavior.

Do not read current hidden spending or orders. Round-start production is observable under the existing intel gates (`AI/Patches.cs:136–155`).

Strike when conservative expected city damage or economic suppression exceeds the foregone investment and retaliation cost. Saturation concerns **projectiles reaching the defended region versus funded rolls**, not guns versus defense buildings. Energy resets each turn; waiting accumulates an arsenal and productive capacity, not stored firing energy.

## C. Economy

Material piles up because energy is the binding conversion constraint, and higher-priority military/intel steps consume construction throughput. At T32, Guts retained **37,214 material and 65 power slots**, but only 41 construction-budget energy (D:343).

The balance rule also targets energy from **material income**, not accumulated surplus, and excludes defensive reserve from its target (`AI/Brain/Director.cs:67–94`). A planet with abundant material stock can consequently keep valuing additional extraction too highly.

The immediate conversion opportunities are:

- Yield upgrades and appropriately placed, subsequently upgraded plants.
- Permanent/local energy projects.
- Global energy projects, renewed and staggered around expiry.
- Science projects when they advance a valuable unlock.

**Global energy projects are already priced by the director** (`AI/DirectorPatches.cs:911–933`). The main missing pieces are timely access, budget priority and coordinated scheduling.

Their leverage is enormous: one Trials project costs **150 E + 300 M**, gives ×1.3 global energy, and penalizes material production ×0.9. Multiple cities’ modifiers multiply (`decompiled/Interplanetary.PlayerDataManagement/GlobalStatModifier.cs:53–59`). Three simultaneous projects mean ×2.197 energy, before other changes.

Use a **6–10-turn rolling cash-flow forecast**, not elaborate financial NPV: include activation timing, expiry, renewals, survivability, material depletion and reinvestment. Count future benefits only when they become usable.

Also price **city project slots**. Guts queued five permanent material projects and two permanent science projects early. Slots unlock at 25/50/75% city population and can disappear after damage; permanent projects restrict later flexibility (`decompiled/Interplanetary.Prefabs/BuildingProjects.cs:337–447`). Free power slots do not imply free project slots.

## D. Research

**Copy the human’s economic principles first; develop value-driven research afterward.**

The human reached 1,239 science/turn at T12 versus approximately 513 for the AI—**2.4×**. Later AI science superiority arrived after the important economic timing window.

For the next experiment, use an explicit economy-first opening: early science acceleration, plant upgrades, and reactors/Lossless around the human’s demonstrated timing, while retaining timely basic defense and intel.

Then value **technology packages**, including prerequisites and executable follow-up purchases. A technology’s value is the improvement its unlocks can actually deliver before the relevant deadline—not its position in a consensus order. Science should accelerate selected milestones, rather than chase the entire remaining 33,100-science plan (`AI/Brain/Offense.cs:70–82`).

## E. Three-player target selection

Optimize **our eventual chance of winning**, not punishment of the currently largest economy.

Attack an opponent when we can produce a consequential result: eliminate it cheaply, interrupt an imminent winning threat, or exploit exposed infrastructure. Otherwise, growing while rivals exchange expensive fire can be best.

Replace independent weighted weapon assignments with a **turn-level target commitment**, including “hold fire.” Keep that commitment unless reachability or observed outcomes invalidate it. The current economy/grudge scoring and randomized allocation encourage split pressure (`AI/Brain/TargetPlanner.cs:55–115`).

An AI rival remains a legitimate target. But damaging it is strategically bad if it merely hands the untouched third player a winning position. Neither human bias nor an implicit AI alliance is needed.

## F. Architecture and offline tuning

**Keep the working adapters; replace the strategic decision structure.**

Use one controller with opening, growth, buildup, strike and recovery states. Transitions should depend on survival risk, economic milestones and achievable damage—not fixed dates alone. Its output should specify research objectives, investment priorities, defense reserve, selected firing expenditure and target commitment. Existing placement, coverage, trajectory and accuracy machinery executes those decisions.

This avoids another collection of individually sensible rules competing for the same energy.

**Real-time self-play is feasible in principle without a replacement simulator.** The game already updates local AI players and advances into action-phase simulation (`decompiled/Interplanetary.GameEngine/PlayerManager.cs:103–158`). QoL already accelerates offline playback.

An unattended runner still needs verification: launching/resetting matches, advancing any required human seat, fixed AI thinking time, and reliable results capture. An idle third player changes incentives; prefer genuine AI seats where supported.

Tune a small paired league with seat swaps, multiple seeds, old-version opponents and defensive-boomer opponents. Test both duels and free-for-alls; retain human holdouts to catch self-play co-adaptation.

## G. Next version: highest leverage and follow-ups

**1. Highest leverage: an explicit early energy-multiplier opening.** Bring reactors/Lossless forward, preserve project-slot access, and prioritize executing/renewing Trials.

Expected effect: two projects provide approximately **69% more energy while overlapping**, subject to expiries and other production changes. This directly attacks the measured late-game gap.

Measure: unlock turn, active projects, realized credited energy, and T20/25/30 income against v0.9.1. Risk: weaker early military readiness, material penalties, and a production cliff at expiry.

**2. Add joint construction/firing budgets with a growth/hold-fire state.**

Expected effect: recover part of the roughly **2.2k energy/turn** reserved for Guts’ existing weapons during T18–28, while retaining necessary defenses.

Measure: energy actually redirected into productive investment, subsequent income, city survival and damage inflicted per firing energy. Risk: excessive restraint lets an exposed opponent escape or a rival become unstoppable.

**3. Add committed-target, saturation-aware strike/recovery decisions.**

Expected effect: fewer inconsequential split volleys and better conversion of economic strength into eliminations.

Measure: projectiles reaching the chosen target, interceptions, city-population loss, eliminations, reserve failures and post-strike recovery. Risk: uncertain enemy budgets and changing trajectories invalidate estimates; use ranges and replan each turn.

## What I would not do

I would not raise accuracy, add resource bonuses, force anti-human cooperation, or simply postpone `WarFromTurn` to 28. I would not begin with reinforcement learning, a full simulator rewrite, or more unrelated tactical patches.

The next test should establish whether the AI can **build the economic engine, preserve it, and spend its output decisively**. More buildings or longer survival alone would not establish progress.
