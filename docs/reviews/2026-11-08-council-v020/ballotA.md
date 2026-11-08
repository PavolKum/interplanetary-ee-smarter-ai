I’ll check the matrix against the underlying reports and repository evidence, then return a ballot for every issue.
One apparent matrix error is already clear: the cross-reviews disagree on match 24’s placement total. I’m checking the raw records and the game’s turn order before deciding whether the contested energy claim is actually resolved.
# Council ballot

## 1. Objections to agreed facts

- **FA2:** The Orbital Assembly/income gate and T20 fallback describe the **scheduled** war start. Damage can trigger war earlier, as FA1 records (`AI/Brain/Tempo.cs:56–64`; `AI/Brain/Director.cs:271–274`).
- **FA4:** The denominator of 193 means turns **at or after first war**, not 193 war-flagged turns: seven were peace turns, leaving 186 war-flagged turns. The 39/52 low-material count is a useful association, but “material is the main binding constraint” is not established: seven of those 39 had at least 200 material, the basic Railgun cost, and cap, upkeep, slots and purchase ordering can overlap (`xreviewE-on-A.md:22,61–64`; `AI/DirectorPatches.cs:864–905,1288–1318`).
- **FA6:** The reported facing means are per-record aggregates, not a per-gun comparison, and cover all assigned targets while `live/found` are commit-filtered. Uniform *candidate sampling* does not establish that final placements follow a 90° chance baseline (`AI/AimWatch.cs:151–165,220–254`; `xreviewE-on-A.md:59–64`).
- **FA10:** Match 24’s placement total is **78**, as the note states, not 67. I summed `director.placementFailures` in `v018m24-ai-decisions.jsonl`: Doc **52** plus Eddie **26**. The other placement corrections stand.

I accept the remaining facts.

## 2. CF1 — **Refuted**

**Turn-index semantics:** action phase **T** follows `turn_end(T)`; `resource_credit.energyBefore(T+1)` is the remaining balance before the next refresh. Population at `turn_end(T+1)` reflects the preceding action phase **and** subsequent population growth. It is not gross damage.

In match 26, `turn_end(T23)` recorded **10,913 energy** and population **2,601**. Before the T24 refresh, `energyBefore(T24)` was **8,113**; by `turn_end(T24)`, population was **1,966**, a net fall of 635. The human therefore sustained substantial population loss across action phase T23 while substantial energy remained afterward. The exact energy at each impact and damage attribution are not logged.

The ordering follows `decompiled/Interplanetary.GameEngine/PlayerManager.cs:150–158`, `ActionphaseManager.cs:723–738`, `GameManager.cs:330–373`, and `PlayerData.cs:446–454`. Defense rolls use the current resource balance and spend against it (`PlayerDefenseHandler.cs:215–247`). Lane D’s `energyBefore(T23) = 595` is the **previous** phase’s leftover, not the starting budget for action T23.

## 3. Issue votes

### I1 — **AMEND**
A single idle label cannot establish causation when constraints overlap, and a build hash alone does not identify a dirty binary (`AI/DirectorPatches.cs:864–905,1288–1318`).

**Replacement:** “In v0.20, log independently observable idle constraints and purchase refusals, per-step material spending, per-gun target/facing/found data, unpowered weapons, late-fire eligibility and refusal reasons, distinct own/neutral/none destinations, and placement-search starts and terminal outcomes with IDs and reason counts. Stamp the actual build fingerprint, dirty state, effective configuration and experiment arm. Generate note numbers and PASS/FAIL/UNTESTED gates with denominators by script; add a scripted mutation runner. Make deployment opt-in and refuse a deploying build if the game is running; ordinary non-deploying builds remain available.”

### I2 — **AMEND**
Reusing the scheduled-war latch for retaliation would also change the upkeep cap from retaliation to full-war treatment (`AI/DirectorPatches.cs:206–214,864–866`).

**Replacement:** “In v0.20, latch damage-triggered war from T12 onward in separate match-scoped state shared by strategy and director; before T12 retain current behavior. Preserve the separately scheduled war turn and pre-schedule retaliation upkeep cap, and reset correctly on new matches and earlier-save loads. Keep `MinWeapons` and `MinPassingShots` unchanged. Gate on a red-before-fix Ash/Eddie regression, correct reset and upkeep tests, and zero post-latch reversions in two matches **with observed latch opportunities**; otherwise report UNTESTED.”

### I3 — **AMEND**
A late volley updates `LastStrikeTurn`, so firing during a pulse wait can postpone or dilute the intended subsequent pulse; the initial `strategy` record also remains a hold even if late fire occurs (`AI/StrategyPatches.cs:127–130,338–366`).

**Replacement:** “In v0.20, instrument pulse-wait eligibility and refusals. In v0.21, test pulse-wait late fire as an opt-in controlled arm, retaining all existing funding, minimum-weapon, passing and abort guards. Join hold, late-fire, fire, paid and abort records by AI and turn; compare paid fire, subsequent pulse timing and observed impact outcomes with control. Report UNTESTED when no eligible opportunity occurs.”

### I4 — **AMEND**
Secondary trajectories currently fill class hit quotas, so suppression can change the hit/miss mix and can withhold legitimate free-for-all fire (`AI/Patches.cs:446–475,669–695`).

**Replacement:** “After I1 instrumentation, test form A in a v0.21 experiment: withhold only completed off-commit trajectories, including quota-refill paths, while preserving configured class-level accuracy and explicitly logging assigned target, hit/miss class and destination. Compare paid commit-target shots per eligible turn and observed outcomes against control; treat reduced off-commit share as a wiring check, not proof of benefit. Reserve form B for Pavol.”

### I5 — **AMEND**
F1 demonstrates a score-floor exclusion, but `HumanFirstHolds` also requires passing and low-yield conditions; an unconditional 85% commit gate could reward targeting an unreachable human (`AI/Brain/Strategy.cs:313–322`).

**Replacement:** “In v0.21, compare the current 0.35 reach-score floor with an experimental 0.15 floor. Report HUMAN-FIRST commits only among turns satisfying the other reach and low-yield conditions, alongside paid commit-target shots, retargeting losses and observed outcomes against control. Do not introduce a fellow-AI exemption.”

### I6 — **AMEND**
Low end-of-turn material is not by itself proof that material prevented a weapon purchase; cap and affordability overlap (FA4).

**Replacement:** “In v0.20, diagnose material and cap constraints with price-specific affordability, purchase-stage budgets, spending order, repairs, reserves and simultaneous refusal reasons. In v0.21, select a material rebalance or material-conditioned cap experiment only if those observations identify the relevant constraint; evaluate paid and observed output rather than idle energy alone.”

### I7 — **AMEND**
The present angle aggregates mix target populations, and 30 fire turns are clustered within a few AI-lobbies (`AI/AimWatch.cs:151–165,220–254`; `xreviewE-on-A.md:37–38,59–64`).

**Replacement:** “Defer target-facing placement to a v0.21 experiment after per-gun data identify a plausible, persistent bearing effect across multiple matches. Treat far/near found-rate ≤0.6 over at least 30 fire turns as a screening heuristic, not proof or a mandatory causal threshold. Use a bounded soft score with a `w=0` golden test, and compare found trajectories, placement outcomes per started search, coverage and wait time against control.”

### I8 — **AMEND**
“First effective volley” is undefined, while paid trajectories alone are not observed impacts (`AI/Patches.cs:519–522`; `xreviewA-on-B.md:88–94`).

**Replacement:** “Keep earlier war out of the v0.20 default. In v0.21, independently test either Railgun-affordability timing or fallback T17 versus T20 against a matched control. Predefine an effective-volley threshold using observed action-phase impact events, including intercepted events, and report UNTESTED if those events cannot be joined. Require that volley at least two turns earlier without T28–T32 income falling more than 10% or material defense regressions.”

### I9 — **ACCEPT**

### I10 — **AMEND**
Per-AI arms in one match interfere through shared aim budget and each other’s targeting; turns within a match are not independent samples (`xreviewD-on-E.md:17`; `AI/Patches.cs:100–106`).

**Replacement:** “Use the specified two-Smarter-AI, 400k-crust standard-play lobby as primary and three-AI lobbies as stress tests. Run at least three comparable matches per version before a provisional ranking, without treating three as statistical proof. Prefer paired saves or match-level randomized arms for causal comparisons; label within-match per-AI arms as interference-prone. Log human strategy, report gate denominators and UNTESTED cases, and never use win/loss as the sole gate.”

### I11 — **AMEND**
The proposed FA10 baseline incorrectly changes match 24 from the correct 78 to 67.

**Replacement:** “Before using the match 22–26 notes and scoreboard as baselines, reconcile their numeric claims with the raw records and correct FA10, including match 24’s total of 78.”

### I12 — **AMEND**
Pulse-wait firing changes pulse timing, while its net benefit remains unmeasured (I3).

**Replacement:** “v0.20 scope: I1 instrumentation and tooling, the scoped I2 retaliation latch, I10 protocol and I11 baseline corrections. Instrument I3 but defer its behavioral change, I4 form A and the other experiments to v0.21 or later. Review each behavior change against game rules and verify it in a real match.”

## 4. Missing issue

**Evidence durability:** the raw playtest data are git-ignored and the council manifest identifies local files; the reports describe one local copy (`laneE.md:127–140`; `evidence-manifest.tsv`). Add a verified, durable backup and SHA-256 manifest for each match before relying on it as a baseline.

## 5. One non-negotiable

**Do not label a gate PASS without an observed opportunity and the correct lifecycle denominator.** Otherwise a latch that never triggers—or a late-fire path that never runs—can appear verified.

**verdict:** Amend the proposed v0.20 scope: ship measurement, corrected baselines and a separately scoped retaliation latch; treat pulse-wait fire as a controlled later experiment. Confidence **high** on the cited mechanics and corrections, **medium** on the release ranking.

END OF REPORT
