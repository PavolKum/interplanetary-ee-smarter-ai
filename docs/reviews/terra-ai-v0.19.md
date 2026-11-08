I’ll trace the changed calculations through the turn lifecycle and native game code, then check the logging and smart-AI guards against the actual patch order.
The main accounting issue is that the two “start” values are not always comparable: the previous snapshot can already exclude a firing reservation, while the current value adds it back. I’m checking how often that can change decisions and whether the new timing labels distinguish actual readiness from the human finishing.
## Findings

### 1. **Major — A previous firing reservation is mistaken for income growth**

**Location:** `AI/Brain/Strategy.cs:299–304`, called from `AI/StrategyPatches.cs:263–267`; previous snapshot at `AI/DirectorPatches.cs:279–286, 641–644`.

`carry - lastUnspent` is not always the change in income. This turn’s calculation adds back the current firing reservation, but last turn’s `EnergyAtStart` was recorded **after** the firing decision:

- If the AI held last turn, the reservation had been released, so `EnergyAtStart` includes it.
- If it fired, `EnergyAtStart` excludes last turn’s reservation.

Consequently, after a firing turn, the new formula treats the prior reservation as growth even when income is flat.

**Failure scenario:** Last turn income was 10,000, the AI reserved 4,000 to fire, used 3,500 on defense/construction and left 2,500. This turn income and firing cost are unchanged. The inputs are `lastUnspent=2,500`, `lastUses=3,500`, and current net energy `6,000`. The new calculation returns **4,500 free**, so free fire passes the 4,000 threshold. Yet only 2,500 was surplus at the comparable prior construction start; there was no income growth. The extra 1,500 comes from counting part of the already-reserved previous salvo as growth.

**Fix:** Record a comparable previous **pre-firing** energy snapshot, or separately record the reservation actually withheld at the previous construction start and normalize the two starts before calculating growth. Retain last turn’s actual construction use separately. Add tests for prior **fire**, **hold**, and **aborted fire**, with flat, rising and falling income, including a case where the incorrect value crosses the firing threshold.

### 2. **Major — The growth heuristic can spend energy needed by this turn’s defense and repairs**

**Location:** `AI/Brain/Strategy.cs:296–304`, `AI/StrategyPatches.cs:263–271`; later demand is calculated at `AI/DirectorPatches.cs:659–683, 1129–1170`.

Even with comparable snapshots, half of income growth is an assumption about surplus, not evidence that it will go unused. The fire decision happens before the director calculates this turn’s defense reserve, forward-defense need and repair budget. Those demands can increase sharply following an attack; they are not bounded by last turn’s use. Firing removes its reservation from the director’s available energy before the director applies its recovery cap.

**Failure scenario with the default 50% share:** The previous held turn started with 8,000, used 7,000 and left 1,000. This turn starts with 12,000 and the salvo costs 3,000. The formula gives `1,000 + 0.5 × 4,000 = 3,000`, authorizing free fire. Suppose the newly observed/forward defense request is 7,000 and damaged buildings need repairs. With the default 20% recovery floor, firing leaves 9,000 and caps the defense reserve at **6,600**; it also leaves no energy above the recovery floor for repairs. Holding leaves 12,000, allowing the **7,000** reserve and some repairs. The “free” shot has therefore reduced both current protection and recovery.

This is a heuristic tradeoff rather than a resource cheat, but it breaks the stated rationale that the firing energy would otherwise go unspent. Match 24 itself shows changing demand: Doc’s reserve rose from **3,556** on T22 to **5,825** on T23, while repair energy rose from **800** to **1,900** (`docs/playtests/data/v018m24-ai-decisions.jsonl:400,448`).

**Fix:** Before authorizing growth-based free fire, account for the **current** defense/forward-defense request and repair requirement using the same budgeting rules as the director, and explicitly preserve whichever recovery/construction floor is intended. If this cannot be evaluated at the decision point, defer the decision or keep growth-based fire off until it can. Add an integration-level budgeting test where growth qualifies for fire but current demand rises, and verify the reserve and repairs remain funded.

### 3. **Minor — “Human turn” is not a reliable stop reason in all ready-frame cases**

**Location:** `AI/AimWatch.cs:174–186`, particularly `:179–181`.

The new classification comes after `all aimed` and `native ready`. The watcher counts only usable weapons aimed at the commit (`:148–164`), while native readiness depends on **all modules** and the native targeting module’s complete weapon set (`decompiled/Interplanetary.AI/AICore.cs:177–201`; `AIWeaponTargeting.cs:26–40,143–156`). Thus, for example, all commit-facing weapons can be aimed while another module or fallback-target weapon is unfinished; if the human finishes after timeout, native timeout can end the search, but the record says **“all aimed.”**

Also, the prefix can extend the timeout on the same frame the human finishes (`AI/AimWatch.cs:110–119`), so comparing `HumanDoneAt` with the **final effective timeout** describes which gate was later, not whether the human exceeded the original/base window. That distinction matters when interpreting a long turn with a short post-human extension.

**Fix:** Log the facts independently: human-finished time, base and effective timeout, and whether the native modules were all ready on the transition frame. Derive a stop label from the actual ready conditions, allowing a combined/ambiguous cause when both become true on the same frame. Add a case with all commit weapons aimed but another native module unfinished, plus a same-frame timeout extension.

### 4. **Minor — The new timing fields overstate what has been measured**

**Location:** `AI/AimWatch.cs:144, 240–241`; `AI/tools/live_monitor.py:151–154`.

- `HaveAllHumanPlayersFinishedTurn` returns true vacuously when there are **no playing humans**, including after the last human is defeated or surrendered (`decompiled/Interplanetary.GameEngine/PlayerManager.cs:44–57`). Such a turn is logged as though a human finished at the first observed AI frame.
- `waitAfterHuman` ends when `AICore.IsReady` becomes true. The AI then still runs its end-turn targeting phase, which has its own readiness and timeout (`decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:327–341`; `decompiled/Interplanetary.AI/AICore.cs:204–248`). Other AIs can also remain unfinished. Therefore the monitor’s **“AI wait after”** is aim-search wait, not necessarily the total wait the human sees after ending the turn.

**Fix:** Record whether any playing human exists and use a distinct unavailable/not-applicable value when none does. Rename the metric to something like `aimWaitAfterHuman`; if the aim is to measure the actual player-visible delay, separately instrument completion of the AI end-turn phase and the transition after all players are ready.

## Checks and scope

- **Economy order:** Energy is reset to production at round start (`PlayerResourceHandler.cs:152–160`, `PlayerData.cs:446–454`). `AIResources.BeginTurn` snapshots that energy and subtracts firing upkeep (`AIResources.cs:38–64`); the strategy postfix runs after native module `BeginTurn` and before the director’s construction-start reserve and repairs. Adding back the **current** reservation reconstructs the current pre-reservation snapshot; the error in finding 1 is the asymmetric **previous** snapshot.
- **Falling-carry branch:** Yes. For valid inputs, `carry <= lastUnspent` returns exactly the old `min(lastUnspent, carry)`, including negative carry. That is a statement about the calculated carry; finding 1 means it does not always faithfully represent falling *income*.
- **Aim watch lifecycle:** `HumanDoneAt` is reset by a fresh per-turn `Watch` (`AI/AimWatch.cs:83–97`). The postfix observes the timer after native `Update` increments it and before `PlayerData.UpdateAI` calls `EndTurn`, so the ready frame is captured (`PlayerData.cs:327–333`). Sampling is frame-granular. Normally, native `AIWeaponTargeting` itself also waits for the human before becoming ready (`AIWeaponTargeting.cs:143–156`).
- **Original AIs and fairness:** I found no new original-AI behaviour change in this diff: strategy is reached through the `IsSmart`-guarded economy postfix, and both aim-watch hooks guard on `IsSmart`. The growth inputs are the AI’s own budget; the diff introduces no hidden-enemy targeting input or resource grant.
- **Verification:** Read-only source and match-log review. I did **not** run the tests or game, and did not independently verify the stated test count or mutation result. In-game timing and gameplay effects remain unverified.

**verdict: ship with fixes** — address the two major budgeting findings before shipping this slice. The timing findings should also be corrected before relying on the new fields to diagnose player-visible wait.
