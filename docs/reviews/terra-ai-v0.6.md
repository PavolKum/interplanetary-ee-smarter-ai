I’ll trace the interception hook through the decompiled game first, then check the turn lifecycle, decision logic, and tests against it. I’ll keep the review strictlyThe hook is observing impacts at simulatThe hook captures real defense events, but two interpretation risks are emerging: failed “energy” events can have another cause, and the shooter’s samples are pooled across opponents. I’m checking their consequences and the placement fallback against the game’s actual turn and power-grid mechanics.
# Verdict: **fix first**

The hook captured useful data in the v0.6 playtest, but the adaptive decisions have three material problems: the reserve can halt recovery, weapon observations are mixed across opponents, and the advertised water-plant fallback can be exhausted before it runs. **Confidence: high** in the code findings; medium in their match-level impact.

## High

### 1. Defense reservation can prevent recovery after collapse

`AI/DirectorPatches.cs:274–283` reserves up to all *usable* energy after native firing upkeep; `AI/Brain/Director.cs:218–223` caps observed need against **income**, not the remaining construction budget. Thus income 2,500, upkeep 1,500 and observed need ≥1,000 leaves zero for plants, upgrades or new weapons. This happened in the v0.6 log: Turing T33 and X T36 had zero `energyStart` and queued nothing (`docs/playtests/data/v060-ai-decisions.jsonl:363,389`).

`BuyPending` is protected **only if already affordable** (`AI/EconomyPatches.cs:155–167`; `AI/DirectorPatches.cs:278–279`). If the goal is not affordable, the reserve can also prevent the energy growth or slot-producing plant needed to make it affordable.

**Fix:** reserve a minimum recovery budget or guarantee an affordable power/reconnection action before applying the observed reserve; explicitly test the unaffordable-superweapon and small-income cases.

### 2. Shooter observations combine different enemies

`AI/Brain/DefenseLedger.cs:28–30,63–76,108–125` keys shooter history by weapon, not defender. `AI/DirectorPatches.cs:182–206,533` applies that pooled rate to the selected target’s defense estimate. Shots at a lightly defended AI can therefore make Railguns look effective against a heavily defended human, or vice versa; self-planet hits are pooled too. The v0.6 match has multiple opponents and changing targets (`docs/playtests/data/v060-ai-decisions.jsonl:152,155,179`).

**Fix:** key samples by shooter, actual defender and weapon; exclude or separately classify self-hits. Apply only the selected target’s samples, falling back to its remembered-defense estimate when insufficient.

### 3. Land-placement failures can consume the fallback quota

The unpowered-plant branch increments `PlantsForUnpowered` **when queued** (`AI/DirectorPatches.cs:317–325`). Failed searches refund and reopen construction, but `Abandon` does not decrement that counter (`AI/DirectorPatches.cs:976–1020`). If the AI queues its five allowed Solar Plants and they fail, reopening does not enter the branch to try a Floating Plant. The T30 X log shows five Solar Plants queued, four abandoned, and no Floating Plant (`docs/playtests/data/v060-ai-decisions.jsonl:315`).

**Fix:** track which queued plants were for recovery and decrement their count on abandonment; then retry the fallback with refunded funds. Separately, random water placement does **not** guarantee reconnection: power lines require proximity and free connectors (`decompiled/Interplanetary.AI.Construction/AIPowerLineSearch.cs:43–55`).

## Medium

### 4. “Unpaid rolls” are not the energy needed to intercept

`FailedNoEnergy` also means **insufficient material**: the game checks both resources before labeling the failure (`decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:229–247,283–287`). Moreover, after energy runs out, *each* overlapping defense can report failure for one impact; with funding, an earlier defense might succeed and prevent later attempts. Multiplying all failures by 100 (`AI/Brain/Director.cs:226–229`) can substantially overstate need. It can also understate individual costs: Enhanced Ion costs 200 and city defenses 250 (`docs/ruleset/embedded/enhanced/DefensesData.csv:5–8`).

**Fix:** treat this as a bounded demand estimate, not measured need. Capture actual candidate cost and resource shortfall, and estimate at most the expected sequential attempts per impact. Log uncertainty and both resources.

### 5. City defenses are observed but can receive no reserve

City projects register defenses through the same handler (`decompiled/Interplanetary.Prefabs/BuildingProjectStats.cs:47–52`), but own-defense counting explicitly skips cities (`AI/EconomyPatches.cs:135–140`), and `DefenseReserve` returns zero when the resulting count is zero (`AI/Brain/Director.cs:218`). A city barrier alone can spend 250 per attempt yet receive no observed reserve.

**Fix:** count active own city defense functionality separately, or allow observed spending to fund a reserve even with zero normal defense buildings.

### 6. Pass estimates need exploration and uncertainty

The ratio is **non-intercepted planet impacts**, not probability of a shot hitting useful targets or dealing damage. Fragments share one projectile and thus are correlated (`decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:180–205,312–336`); fallout can bypass a successful interception (`:337–356`). Twelve fragments are not necessarily twelve independent trials. Upgrades, target changes and city barriers also affect results.

The three-turn window eventually expires if a weapon stops producing impacts, so a 0.05 Railgun score is **not inherently permanent** (`AI/Brain/DefenseLedger.cs:87–97,108–125`). But surviving Railguns can continuously renew the low score; an unbuilt weapon may never be sampled, and the 0.6 selection band can exclude it (`AI/Brain/Director.cs:174–192`).

**Fix:** track distinct projectiles and target-specific outcomes, blend small samples with the prior, and allocate bounded exploratory picks. Treat 12 and three turns as tuning hypotheses, not validated thresholds.

### 7. Defense memory is not pruned for eliminated enemies

The per-enemy memory is updated only for current `EnemyPlanets` (`AI/EconomyPatches.cs:98–130`); unlike weapon memory, entries for departed IDs are never removed. Target selection uses only current enemies, so this is mainly stale state and potential ID-reuse contamination, not an immediate attack on a dead target.

**Fix:** remove absent IDs from both `DefenseMemory` and `KnownDefenses` each turn. Remembered counts decay by 0.95, but partial visibility only supplies a lower bound (`AI/Brain/DefensePlan.cs:116–131`); `coverageFull` saturates at ten defenses per type (`AI/Brain/Director.cs:154–162`). Do not interpret that as measured geographic coverage.

## Low

### 8. “Uncovered” is weapon-relative, not geographic

No events means no **matching, normal-state defense within range**; there may be nearby defenses that cannot counter that weapon (`decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:223–228,264–270`). The v0.6 playtest’s “no defense in range” wording (`docs/playtests/2026-10-31-v0.6.0.md:9–10`) overstates the geographic conclusion.

**Fix:** rename the metric `noEligibleDefense` and qualify the playtest interpretation.

### 9. Tests omit integration and failure behavior

`AI/Tests/Program.cs:204–269` checks ledger arithmetic, expiration and a synthetic weapon pick, but does not exercise Harmony, overlapping attempts, opponent isolation, city defenses, save/load, recovery starvation or failed-placement fallback.

**Fix:** add behavioral scenarios for the findings above and an in-game hook smoke test. Tests were **not run**: this review was strictly read-only.

## Checked and fine

- **Hook signature and accounting:** parameter names match `IsHitIntercepted`; the prefix/postfix share `__state` in the same patch class (`AI/LedgerPatches.cs:20–46`; `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:203–247`). Each matching candidate adds one event, including disconnected and failed rolls; success stops the loop. Both success and probability failure consume the event’s energy. The v0.6 playtest reports 14/14 hooks and nonzero ledger results (`docs/playtests/2026-10-31-v0.6.0.md:7–10`).
- **Scope:** the sole decompiled `IsHitIntercepted` call is once per generated planet-impact fragment, including registered city defenses (`decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:312–336`). Planet misses do not enter it; independent fallout damage is outside this metric.
- **Timing:** in normal offline rounds, all planning finishes before one action-phase simulation; each local AI begins once when its turn status changes (`decompiled/Interplanetary.GameEngine/PlayerManager.cs:123–158`; `decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:314–320`). Samples are generated during simulation and rolled at the next AI start. Match/load initialization clears memory (`AI/Patches.cs:32–39`), so the first post-load window is empty; mid-phase interruption/reload behavior remains **unverified in-game**.
- **Fairness:** successful interceptions are logged for the shooter and failures for the defender (`decompiled/Interplanetary.GameEngine.Actionphase/ActionphaseDefenseActivationEvent.cs:89–135`); projectile impacts have visible playback events (`ActionphaseProjectileHitEvent.cs:42–65`). The code does not use failed enemy defense statuses in shooter scoring. Exact UI-level attribution of every fragment to a weapon is **unverified in-game**.
- **Rulesets:** Enhanced has `bui_FloatingPlant`; Classic does not (`docs/ruleset/embedded/enhanced/BuildingsData.csv:2`; `docs/ruleset/embedded/classic/BuildingsData.csv:1–2`). `HasBuilding` safely handles its absence; availability and cooldown are checked (`AI/DirectorPatches.cs:804–811`). The existing placement cap and cooldown bound retries.
