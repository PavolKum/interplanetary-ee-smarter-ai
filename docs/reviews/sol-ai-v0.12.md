I’ll check the diagnosis against the raw logs and trace the changed hooks through the game code, especially the aiming timer and mine-selling lifecycle. I’ll also distinguish what the tests cover from behavior that needs an in-game check.
Two issues stand out so far: the “sustainable material” value limits energy investment but does not throttle extraction, and the selling guard excludes powered mines as well as power relays. The logs also show that many human turns already lasted over forty seconds, so the longer aiming window would have added no search time on those turns.
Verdict: fix first

**Confidence: High on the code-level findings; Medium on the expected match improvement.** The abort change should recover useful strikes, and stopping surplus power investment is the right direction. However, the dry-planet adapter state defeats the new saturation check, the reachability change permits indefinite commitment to a zero-hit target, and mine selling rejects the powered mines it needs to liquidate.

## High findings

### 1. The dry-planet state bypasses energy saturation—and material projects can still buy nonexistent production

**Evidence**

- On a depleted planet, the adapter explicitly sets `d.MaterialIncome = 0`: `AI/DirectorPatches.cs:406–409`.
- Sustainable income is also zero: `AI/Brain/Director.cs:126–129`.
- `EnergySaturated` immediately returns false when sustainable income is **greater than or equal to** material income: `AI/Brain/Director.cs:119–122`. Thus the actual dry-planet state, `0 >= 0`, is never saturated.
- Without last-turn energy glut, material glut can consequently restore full energy-gain weight: `AI/Brain/Director.cs:84–96`.
- Global material-project gains still use nominal `res.MaterialProductionPerTurn`, without a depletion adjustment: `AI/DirectorPatches.cs:1046–1068`. Unlike material buildings, they have no dry-planet exclusion (`:1103–1104`).
- The new dry-planet test uses `MaterialIncome = 25000`, rather than the adapter’s zero: `AI/Tests/Program.cs:356–357`.

**Failure scenario**

Brother’s actual T26 state is an excellent counterexample: planet material **0**, banked material **75,735**, `materialGlut = true`, `energyGlut = false` (`docs/playtests/data/v0111-ai-decisions.jsonl:280,286`). In that state, the new saturation branch does not stop power purchases.

Separately, the director can value a global material project positively on an empty planet because nominal production remains nonzero. Brother actually queued two such projects at T27 (`…/v0111-ai-decisions.jsonl:323`). The changed weighting does not remove that possibility; a zero energy-gain weight also ignores that project’s negative energy effect.

The 15k economy floor limits the eventual expenditure, but it can still permit wasting most of a substantial remaining bank.

**Concrete fix**

Represent depletion explicitly. On a dry planet:

- Give extraction-only gains zero value, including global and local projects.
- Decide energy investment from usable bank, firing, defense and recovery needs—not nominal mining production.
- Add a test using the adapter’s exact `MaterialIncome = 0`, `SustainableMaterialIncome = 0` state, with material glut and no previous energy glut.

### 2. The reach gate can lock the AI onto an unreachable target indefinitely; max-hold does not reliably rescue it

**Evidence**

- Failures are recorded only when successes reach the configured share: `AI/Brain/Memory.cs:89–114`.
- With zero successes and a positive threshold, no weapon records a failure.
- Strategy includes every weapon not excluded by that memory in each target’s predicted salvo: `AI/StrategyPatches.cs:150–175`.
- Target commitment uses predicted passing shots and stickiness: `AI/Brain/Strategy.cs:179–187`.
- The normal saturated-strike branch runs **before** max-hold: `AI/Brain/Strategy.cs:202–215`.
- Abort exempts only decisions whose reason is exactly `"max-hold"`: `AI/Brain/Strategy.cs:159–162`.
- Aborted strikes increment holds without penalizing the committed target: `AI/StrategyPatches.cs:106–119,237–247`.

**Failure scenario**

Thirty powered weapons repeatedly find zero primary trajectories to X. Because zero is below the 50% success gate, X remains fully reachable in the model. Its predicted salvo can remain above the saturation threshold, so every turn gets reason `"saturated"` and then aborts below three weapons.

Even after five holds, the earlier saturated branch still wins. The decision never becomes `"max-hold"`.

Mixed geometry creates the same problem less dramatically: if only ten of thirty weapons can physically reach X, the other twenty never learn that fact under a 50% gate.

**Concrete fix**

Keep “search inconclusive” distinct from “geometrically unreachable,” but make repeated poor outcomes affect commitment:

- Track per-target search exposure and realized primary yield.
- After repeated adequately searched zero/very-low-yield turns, discount that target and probe an alternative.
- Make the hold limit an actual bound on repeating the same unsuccessful decision, rather than an exemption attached only to one reason string.

A regression test should simulate several consecutive saturated-but-aborted turns and demonstrate a target change or bounded alternative search.

### 3. Mine selling rejects powered leaves, while selling the remaining candidates after native BeginTurn leaves stale power searches

**Evidence**

- Selling excludes `IsPotentiallyDistributingPower`: `AI/DirectorPatches.cs:743–745`.
- That property is true for an ordinary powered building: `BuildingPower.IsDistributingPower` includes `m_availablePowerCount > 0`, and `IsPotentiallyDistributingPower` includes that result (`decompiled/Interplanetary.Prefabs/BuildingPower.cs:95–116`).
- Native power-grid BeginTurn has already populated free-slot caches and created searches for unpowered buildings: `decompiled/Interplanetary.AI/AIPowerGridMaintaining.cs:41–71`.
- The economy/director postfix runs after those module initializations: `AI/EconomyPatches.cs:81–83,222–223`; selling occurs at `AI/DirectorPatches.cs:217`.
- Power searches retain their origin without a live-state check: `decompiled/Interplanetary.AI.Construction/AIPowerLineSearch.cs:23–27,43–54`.
- A completed search schedules a connection to that origin: `decompiled/Interplanetary.AI/AIPowerGridMaintaining.cs:113–124`. Connections to destroyed buildings are rejected by `PlayerPowerGridHandler.cs:46–54`.

**Failure scenario**

A powered mine with one connection and no downstream consumers is rejected even though it is the obvious safe liquidation candidate. Brother had zero unpowered buildings at T26, when its planet was already empty (`…/v0111-ai-decisions.jsonl:286`); the intended large salvage opportunity is therefore excluded.

Conversely, an unpowered mine that passes the guard may already be the origin of a native reconnection search. Selling it leaves that search and `BuildingsRequiringPowerAmount` stale. The AI can then attempt to reconnect a sold building and buy unnecessary recovery power.

The current guard is conservative enough to avoid removing an existing powered relay, but it is **not a useful leaf/relay test**.

**Concrete fix**

Perform sales before native resource and power-grid BeginTurn snapshots. Select non-generating mines using the actual connection graph—initially, powered leaves are a simple conservative choice. Let native resource initialization capture the proceeds, rather than manually crediting them again in that revised lifecycle.

If retaining the postfix, explicitly remove sold buildings from power searches/caches and correct the corresponding counts.

**Other selling checks:** The existing material credit is otherwise correct: native selling credits real material (`PlayerBuildingHandler.cs:646–658`), while AI planning resources and queue allotment were already snapshotted (`AIResources.cs:38–64`; `AIConstructionQueue.cs:132–137`), so adding the measured delta to both planning budgets is appropriate. Single-player does not replay the network sold-building list (`PlayerManager.cs:138–158`). I found no normal-path current-turn upgrade conflict: construction starts in Update, upgrade bookkeeping is cleared at BeginTurn, and the previous construction queue is cleared at EndTurn (`AIConstruction.cs:441–469,477–496`; `AIUpgradeConstruction.cs:12–15`).

## Medium findings

### 4. The diagnosis identifies the resource collapse correctly, but “keep the planet until T45” is not yet a whole-game budget

**Evidence**

The native rule is confirmed: energy resets each turn, and material credit drains the planet by the greater of production and planet cost (`decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:158–184`). Yield upgrades also multiply planet cost (`docs/ruleset/embedded/enhanced/UpgradesData.csv:83,95`).

However:

- Sustainable income changes energy valuation; it does not constrain extraction or spending: `AI/Brain/Director.cs:110–138`.
- Material gains remain positively weighted under saturation: `AI/Brain/Director.cs:84–89`.
- The 15k floor applies only to `Best`: `AI/Brain/Director.cs:169–180`.
- Power recovery, intel, defenses, weapons and weapon upgrades execute outside that floor: `AI/DirectorPatches.cs:454–548`.

**Failure scenario**

The AI can continue accelerating extraction, then spend its “recovery reserve” immediately through earlier military or power branches. Conversely, when those branches have no purchase, the floor can strand material that a valuable science project needs.

More importantly, **depleting the planet early is not itself fatal if the extracted material remains banked**. Brother had 75.7k when it ran dry; the human had approximately **60.6k** at its own depletion at T38 (`…/v0111-ai-decisions.jsonl:280`; `…/v0111-human-telemetry.jsonl:3892`). The decisive distinction is what happened to that stock.

T45 is a defensible experimental horizon, but the data does not establish it as an optimal extraction deadline. Likewise, 15k is a useful initial guardrail, not evidence of adequate recovery financing.

**Concrete fix**

Use a short rolling spending forecast covering firing, defensive readiness, critical replacements and committed projects. Preserve a bank against those obligations; suppress extraction investment when existing capacity already supplies expected demand.

Keep separate operating and emergency allocations so the emergency bank neither disappears into routine purchases nor blocks every useful project.

Also, reserving money does not implement repair/rebuild behavior: the native AI action methods for repair and clearing ruins are stubs (`decompiled/Interplanetary.AI/AIActions.cs:118–128`).

### 5. Zero energy-gain weight can engage before war and categorically reject Reactor Trials

**Evidence**

- Saturation overrides both material glut and `EnergyPush`: `AI/Brain/Director.cs:83–100`.
- Wanted energy includes sustainable material income and firing upkeep, but not an explicit defense, recovery or project-renewal requirement: `AI/Brain/Director.cs:110–122`.
- Brother T19 had energy income **10,863**, material income **10,602**, planet material **262,899**, and no weapon upkeep (`…/v0111-ai-decisions.jsonl:178,181`).
- Its sustainable income is approximately **10,111**, giving a saturation threshold of approximately **9,100**. It therefore qualifies before the default T20 war gate.
- Reactor Trials’ energy gain and material penalty enter the same score: `AI/DirectorPatches.cs:1050–1068`; `AI/Brain/Director.cs:154–159`.
- Enhanced Trials is ×1.3 energy, ×0.9 material, lasting six turns (`docs/ruleset/embedded/enhanced/ProjectsData.csv:181,190,193`).

**Failure scenario**

With energy weight zero and material weight positive, Trials has a negative value whenever nominal material production is positive. It is rejected regardless of its cheap purchase price or an approaching military/defensive need.

The new weapon cap can make that more likely by limiting the upkeep term that would otherwise lift the saturation threshold. A prior-turn energy glut can also survive a sudden income loss and continue suppressing recovery investment.

This does **not directly suppress science gains**: their separate weight survives. Science projects are instead vulnerable to the material floor and budget availability.

**Concrete fix**

Zero is appropriate for demonstrably unusable marginal energy—not merely for crossing this proxy threshold. Include current bank conversion, funded defense, imminent firing needs and expiring multipliers in the energy requirement. Permit recovery/critical-project overrides.

Do not restore a blanket positive weight that allows unlimited plant spam: even a tiny positive score still buys plants when nothing else scores positively. Prefer an explicit useful-capacity ceiling.

### 6. The 40-second field write works, but it adds little search time to most strikes in this match

**Evidence**

The lifecycle is sound:

- Native BeginTurn resets `m_timeOutTimer` before the postfix: `decompiled/Interplanetary.AI/AICore.cs:158–168`.
- The postfix sets `m_timeOut` before Update begins: `AI/StrategyPatches.cs:224–230`.
- Timeout is compared during Update: `AICore.cs:193–200`.
- Updates continue in `WaitingPlayers`, after the human finishes: `decompiled/Interplanetary.GameEngine/PlayerManager.cs:109–119`.
- Targeting becomes ready early only when all powered weapons have primary trajectories and the human has finished: `AIWeaponTargeting.cs:26–40,143–156`.

But the telemetry shows **18 of the 26 strike decisions occurred on human turns already longer than forty seconds**. The other eight decisions were on four turn numbers, lasting approximately 33.7–37.9 seconds (`…/v0111-human-telemetry.jsonl:110,141,506,1842`). Those turns would gain roughly two to six seconds, not twenty.

There is also a diagnosis arithmetic correction: each core receives approximately `1200 / AI count`, then targeting multiplies by AI count before dividing by total weapons (`AICore.cs:125–164`; `AIWeaponTargeting.cs:60–78`). With the same participating AI count, the allowance is approximately:

`clamp(1200 / total AI weapons, 30, 100)`

At two AIs × thirty weapons, it still sits at the **30-step floor**.

**Failure scenario**

The longer minimum is credited with solving throughput even though most failed searches already had more than forty seconds. Reducing the army to thirty also does not automatically increase each surviving weapon’s designated steps.

**Concrete fix**

Keep forty seconds as an experiment, but log search frames, completed trials, allowance and primary success per live weapon. Separate insufficient work from launch-arc/geometry failure; then adjust computational allocation or placement accordingly.

The human’s extra wait is approximately `max(0, 40 − elapsed turn time)` while targeting remains unfinished; the AIs wait concurrently.

The separate five-second end-turn timeout does not shorten the initial search: `EndTurn` resets the timer (`AICore.cs:241–248`). It can still truncate final trajectory recalculation (`:204–238`; `AITargetWeapons.cs:82–100`). Consequently, firing-list counts remain candidates rather than confirmed firing—an inherited, explicitly deferred v0.11 issue.

### 7. Abandoned placements consume phantom weapon-cap slots for the rest of the turn

**Evidence**

- Existing Normal and UnderConstruction aimed weapons are counted at BeginTurn: `AI/DirectorPatches.cs:223–228`.
- Successful queueing increments the cap count: `:795–801`.
- Abandonment restores budgets, slots, upkeep and weapon spending, but not `AimedWeapons`: `:1702–1735`.

**Failure scenario**

At twenty owned weapons, ten queued placements fill the cap. If those placements are abandoned, the AI still believes it has thirty and rejects another aimed weapon type that might fit.

The reset next turn prevents a permanent bookkeeping deadlock. There is also no apparent ordinary queueing path that exceeds the cap: the check and increment are sequential. Lasers are correctly skipped when disabled, and lasers/superweapons are intentionally excluded from the count (`:725–731,766–776`). Existing arsenals above thirty remain above thirty until losses; this is a purchase cap, not retroactive enforcement.

A related strategy interaction remains: reaching the cap does not switch weapon-upgrade ranking to damage per building. Only `EnergyGlut` does that; otherwise upkeep-bound logic may still accept damage-reducing efficiency upgrades (`:1240–1241,1283–1295`).

**Concrete fix**

On abandonment, decrement the aimed count for an aimed weapon and reopen the `"weapon"` / `"weapon-sink"` branches where appropriate. Test one failed type followed by a placeable alternative.

Feed the actual binding constraint into upgrade ranking: at the weapon cap, favor delivered damage unless reduced firing cost has a concrete alternative use.

## Low findings

### 8. The headline weapon count remains stale, and the tests do not cover the important adapter states

**Evidence**

- The new filter fixes `powered`, primary counts and reach reporting, but `"weapons"` still writes `weapons.Count`: `AI/Patches.cs:349–381`.
- The match totals are **537 primary successes / 2,568 retained entries**, but **537 / 1,478 powered entries = 36.3%**. The quoted 21% is therefore not the live-powered success rate. It still demonstrates a serious shortfall, but overstates it.
- Tests compile only `Brain/*.cs`: `AI/Tests/BrainTests.csproj:12–14`.
- The new tests cover formulas and isolated decisions, including the mismatched dry state, but not selling, timers, cap refunds or repeated abort/commit behavior: `AI/Tests/Program.cs:334–417`.

**Failure scenario**

The next playtest again treats retained targeting entries as live weapons, or a green Brain suite is taken as evidence that native lifecycle integration is correct.

**Concrete fix**

Rename the retained count to an explicitly raw/retained field and emit separate live and powered counts. Add focused checks for the counterexamples above, including multi-turn reach/abort behavior and queue-refund accounting.

**Verification limit:** I did not run builds, tests or mutations under this read-only instruction. The existing code remains **unverified in-game** for these new lifecycle changes. Static inspection found no additional obvious contradiction in the old Brain assertions; the changed both-gluts expectation is intentional. The default-0.3 migration is correctly chained and preserves other configured values (`AI/Plugin.cs:379–413`).

## Ranked next moves

**The most important missing capability is converting a lifetime material budget into sustained, verified damage while retaining recovery capacity.** More production, more buildings and longer survival have repeatedly failed to establish that.

1. **Implement bank-aware material allocation and recovery financing.**  
   Fix depleted-state valuation and selling first. Forecast six to ten turns of commitments, stop buying extraction that cannot improve useful spending, and preserve an actionable recovery bank. Measure material at depletion and T35/T40, wasteful purchases, and successful critical replacements—not depletion turn alone.

2. **Build an arsenal the trajectory system can actually serve.**  
   Fix the repeated-zero-hit commitment trap. Measure completed search work and accepted firing actions, then improve launch placement/geometry or solver allocation. Compare smaller, upgraded arsenals under equal computation budgets; thirty is a hypothesis, not an established capacity.

3. **Choose targets by progress toward winning, including the third-player consequence.**  
   The current objective remains predicted passing shots multiplied by a bounded economy/grudge score (`AI/Brain/Strategy.cs:179–187`; `AI/Brain/TargetPlanner.cs:57–86`). It does not distinguish decisive city suppression from profitable-looking damage that leaves the human untouched. Use visible population, threatening weapons and observed suppression to value outcomes; penalize attacks that mainly improve the third player’s position. No human bias or implicit AI alliance is necessary.

4. **Make recovery and defense protect the fighting core.**  
   Spend preserved material on connected weapons, critical power and city survival rather than restoring the previous building count. Add actual repair/replacement behavior where valuable, and evaluate defense by prevented damage around those assets. The human’s roughly 190–200 buildings and large surviving bank beat 400–500-building economies; building count is the wrong recovery target.

5. **Run controlled comparisons with fixed thinking budgets.**  
   Separate budget, reach/abort and solver changes across paired seeds and seat swaps, then retain human matches as holdouts. Record actual shots, city/population loss, material bank, recovery and wins. A forty-second minimum tied to human turn duration otherwise makes performance comparisons noisy.

**Bottom line:** the abort adjustment would have released **12 of the 13 previously aborted strikes** under the recorded counts. That is a meaningful improvement. Ship it only after fixing the dry-state valuation, bounded target failure handling, and selling lifecycle; those currently undermine the version’s central claims.
