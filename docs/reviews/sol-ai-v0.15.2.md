The attack signal is broader than the playtest replay assumes: `recentDamage` is a decaying grudge, so one enemy hit can enable return fire for many later turns without new impacts. I’m checking the resulting energy trade-off and whether the city-ring search can survive the native abandonment limit.
The raw logs contradict two central claims in the diagnosis: two dedicated city-cover Ions placed at T26 and protected two of three cities from T27; only the remaining city’s search kept failing. The AI also became material-starved after T31, leaving thousands of energy unspent despite more than 200k material still in the planet. Return fire has a different cost in that late state than during the opening expansion.
Verdict: fix first

**Return fire is a reasonable experiment, and the city-ring geometry is compatible with the engine. But the attack signal is not limited to the previous action phase, the accuracy fix still admits stale weapons, and the energy guard would leave the motivating T6–T8 opening essentially unchanged.** The playtest diagnosis also mistakes partial city-cover success for total failure and overlooks a late material-recovery collapse.

**Confidence: High on code findings and data corrections; Medium on next-match predictions.** Reviewed the supplied diff, current implementations, historical playtests and raw match data against `decompiled/`. No files were changed. Builds, tests and mutations were not run.

## High findings

### 1. The “live” accuracy count still admits removed weapons when their building objects are recycled

**Evidence**

- `AI/Patches.cs:279–294` accepts a retained weapon when its building is Normal and powered. It does not establish that the `WeaponGameplayData` is still registered with this player.
- The firing diagnostics use the same insufficient test: `AI/Patches.cs:417–423`.
- Native targeting retains old targetable entries and adds newly registered weapon objects: `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:79–110`.
- Destroying a weapon removes its gameplay object from the authoritative registry: `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:395–416`.
- Buildings subsequently return to the object pool, which reuses the same component: `PlayerBuildingHandler.cs:735–750`; `decompiled/TJR_Engine.ObjectManagment/PrefabObjectPoolManager.cs:218–228`.
- The old weapon object retains its building reference: `decompiled/Interplanetary.Weapons/WeaponGameplayData.cs:52–56`.

**The discrepancy is already visible in this match.**

| Turn | Strategy: registered, operational weapons | Accuracy “live” | Firing diagnostic “powered” |
|---|---:|---:|---:|
| T32 | 27 | 33 | 34 |
| T34 | 23 | 31 | 31 |
| T38 | 6 | 13 | 13 |

Sources: `docs/playtests/data/v0151-ai-decisions.jsonl:209–214,230–235,262–267`.

Accuracy and strategy run within the same native turn-start sequence, before construction executes. These differences cannot all be explained by legitimately constructing additional weapons during the turn.

**Failure scenario:** A destroyed Railgun’s pooled building component is reused for a new Railgun, potentially belonging to another player. The old targetable weapon now sees a Normal, powered building again. Both old and new gameplay objects can count toward accuracy and targeting.

This can affect gameplay, not just logging: native first-phase completion recreates a targeting result for an unregistered weapon and charges its firing cost without checking registry membership; completed dictionary entries are then exposed as targeting results. See `PlayerWeaponHandler.cs:230–260,34–46`.

**Concrete fix:** Define operational weapons using the authoritative registry and identity: the weapon must currently belong to `me.WeaponHandler.Weapons`, its building must belong to this player, and it must be Normal and powered. Apply that validity rule **before quota selection**, as well as to counts and diagnostics. Ignore/remove stale goal entries before `DetermineShootingWeapons`; filtering only the final firing list would let stale entries consume legitimate weapons’ hit slots.

The initialization-order fix in v0.15.1 is correct, but “dead entries no longer count” is not yet established.

### 2. `underAttack` remains true long after attacks stop

**Evidence**

- `AI/StrategyPatches.cs:228` uses:
  `recentDamage > 0 || LastDefender(...).Impacts > 0`.
- `recentDamage` is the sum of enemies’ **grudges**, not damage from the last action phase: `AI/EconomyPatches.cs:97–105`.
- Grudges decay exponentially without an expiry or meaningful-positive threshold: `AI/Brain/Memory.cs:37–52`. The default decay is 0.7: `AI/Plugin.cs:293`.
- Return fire ignores the pulse whenever this Boolean is true: `AI/Brain/Strategy.cs:189–193,258–262`.

**Failure scenario:** One enemy hit enables return fire for the rest of an ordinary-length match, provided the salvo continues meeting 0.6/4. The opponent can stop firing and invest while the AI continues paying firing costs and withholding that energy from construction.

**The supplied replay is therefore wrong at T19.** The actual code also fires there:

- `passing = 4.478`, `passShare = 0.8`, eight weapons;
- previous-phase impacts = zero;
- `recentDamage = 1,983.601`.

Source: `docs/playtests/data/v0151-ai-decisions.jsonl:92–95`.

On the recorded held states, the actual predicate qualifies **T19–T23, T31 and T34–T37: ten turns**, rather than the stated nine. This is predicate evaluation on historical states, not a causal replay of a changed match.

**Self/repair answer:** Grudges reject self-damage, non-positive damage and negative shooter IDs (`AI/Brain/Memory.cs:23–25`). Repairs use a separate path; they do not create these grudges. However, defender impact totals are incremented **before** the self-shot exclusion (`AI/Brain/DefenseLedger.cs:49–71`), so self impacts can activate the other half of the predicate.

**Concrete fix:** Use previous-action-phase **enemy-origin** impacts/damage for this tactical signal. Keep decaying grudges for strategic hostility. Track the last enemy-damage turn if damage outside the impact hook must count.

## Medium findings

### 3. The energy guard does not fix the opening that motivated it

**Evidence**

- The guard is bypassed when `BlindEnemies > 0` or native intel is BelowMinimum: `AI/DirectorPatches.cs:579–582`.
- `BlindEnemies` includes unreadable aggregate economy/population: `AI/DirectorPatches.cs:389–392`; `AI/Patches.cs:199–219`.
- T6 and T7 both recorded `blindEnemies = 1`; T8 recorded zero: `docs/playtests/data/v0151-ai-decisions.jsonl:30,34,41`.
- Counterintel projects still precede economy: `AI/DirectorPatches.cs:621–658`.

**Replay of the relevant purchases**

- **T6–T7:** Blindness bypasses the new energy guard. The same priority telescope purchases remain possible.
- **T8:** Starting energy is 825. Two Telescopes pass the guard at 825 and 625, leaving 425. Two counterintel projects cost another 300, leaving **125**, still insufficient for a 250-energy plant.

That is the recorded purchase sequence at `v0151-ai-decisions.jsonl:40–41`.

**Failure scenario:** Intel meets its gate, but later priority purchases spend the supposed plant allowance. The policy checks a balance; it does not reserve an investment.

**Concrete fix:** Protect one affordable core-economy investment across the **whole priority sequence**, or queue that investment first during the opening. Apply an explicit bounded allowance to blindness-driven intel too; preserve an emergency minimum without allowing it to consume every growth opportunity.

The material guard is similarly a pre-purchase sprint test, not a persistent project reserve (`AI/Brain/Director.cs:272–276`). It did not restrict the recorded opening, but late material abundance should not be inferred from that.

**Acquisition timing:** This guard probably preserves approximately the current T9 acquisition in this particular opening precisely because it changes so little. A stronger guard needs a joint economy/intel replay. The sprint deadline at T14 and ordinary two-per-turn fallback remain intact; neither proves that acquisition will beat an earlier counterintel opening.

### 4. The anti-nuke purchasing gate still counts irrelevant Ions against the cap

**Evidence**

- Demand now uses intact Ions near cities: `AI/DirectorPatches.cs:376–383`.
- Actual purchase permission still requires:
  `d.AntiNukeIntact + ionsQueuedThisTurn < d.AntiNukeCap`
  at `AI/DirectorPatches.cs:592–594`.
- `AntiNukeIntact` includes every intact Ion, wherever placed: `AI/DirectorPatches.cs:350–358,373`.

This contradicts the disposition’s assertion that irrelevant Ions no longer block city-cover purchases: `docs/reviews/sol-ai-v0.15-disposition.md:9`.

**Failure scenario:** One surviving city needs two protecting Ions. Six intact but irrelevant Ions satisfy the three-times-per-city cap. `AntiNukeWanted` can be positive while the purchase branch refuses every purchase.

**Concrete fix:** Align the purchase gate with the near-city inventory policy, including appropriate queued commitments. If a separate total-inventory safeguard is retained, give it an explicit bounded replacement/recovery path. Changing only the demand calculation does not remove the old obstruction.

This was not the cause of the T27–T33 failures—the match had only two Ions—but remains a real residual defect.

### 5. The missing late-game diagnosis is destruction/disconnection of material production, followed by a repair-and-reconnection trap

**Evidence**

| Turn | Material income | Material available to director | Director energy left | Planet material remaining |
|---|---:|---:|---:|---:|
| T30 | 4,317 | 10,468 | 10 | 230,896 |
| T32 | 3,177 | 1,507 | 2,290 | 222,904 |
| T34 | 1,532 | 1,016 | 2,825 | 216,697 |
| T36 | 635 | 418 | 2,265 | 213,286 |
| T37 | 197 | 115 | 3,043 | 212,525 |

Sources: `docs/playtests/data/v0151-ai-decisions.jsonl:195–197,213–215,234–236,250–252,258–260`.

The energy column excludes the separately protected defense reserve.

- Repairs execute before construction budgets are snapshotted: `AI/DirectorPatches.cs:853–859,904–919`.
- Repair priority puts mines behind weapons, defenses, power and intel: `AI/DirectorPatches.cs:889–894`.
- Reconnection plants precede ordinary economy: `AI/DirectorPatches.cs:558–568`.
- At T34–T36, the remaining construction purchases were principally plants; T37 queued nothing.

**Failure scenario:** Sustained Railgun pressure damages the extraction network. Repairs and reconnecting the existing arsenal consume the remaining material, while replacement extraction cannot be funded. Holding releases energy but does not solve the missing material throughput.

**Concrete fix:** Add a bounded non-dry recovery mode: when material production collapses and the stock cannot fund recovery, protect the material needed to restore a useful powered mine. Promote strategically important damaged mines above discretionary arsenal repairs, and prioritize reconnection by recovered yield rather than unpowered-building count alone.

This is a better explanation for the late idle energy than insufficient saturation. Return fire is comparatively cheap in that material-constrained state, but it does not itself restore the economy.

### 6. Return fire needs an outcome/cost check; 0.6/4 is plausible but not calibrated

**Energy contract**

- Native AI construction starts after reserving all Normal weapons’ firing costs: `decompiled/Interplanetary.AI/AIResources.cs:38–62`.
- Holding releases that reservation to both planning budgets: `AI/StrategyPatches.cs:279–288`.
- Firing leaves it reserved; only actually targeted weapons subsequently pay: `PlayerWeaponHandler.cs:230–260`.
- Defense rolls use the real energy left after planning/firing: `PlayerDefenseHandler.cs:215–247`.
- The director’s defense allowance can shrink when less energy is available, while retaining a recovery allowance: `AI/DirectorPatches.cs:513–523`; `AI/Brain/Director.cs:438–443`.

**Consequently, firing every turn can reduce both construction and funded interception.** Conversely, firing a small subset does not automatically charge the entire reservation; unused firing energy remains available for defense, although construction has already missed its opportunity.

There is good evidence for trying early return fire here: human turn-end energy at T19–T24 was **95, 55, 75, 105, 80 and 5**. With 100-energy Kinetic activations, the human’s four defenses were mostly unfunded. Source: `docs/playtests/data/v0151-human-telemetry.jsonl:163,187,205,238,258,279`; activation cost: `docs/ruleset/embedded/enhanced/DefensesData.csv:7`.

But “a high pass share means waiting buys nothing” (`AI/Brain/Strategy.cs:79`) is false: waiting can still buy plants, upgrades and a larger future arsenal.

**Historical check:** Applying the new predicate to recorded held decisions identifies six candidates in v0.10.1, five in v0.11.1 and nine in v0.12.1. Those are not controlled counterfactuals. The historical record supports both sides:

- releasing firing energy restored growth in v0.10;
- aborting useful fire was harmful in v0.11;
- partial, dispersed fire still produced little suppression in v0.12.

See `docs/playtests/2026-10-31-v0.10.1.md:7–24`, `2026-10-31-v0.11.1.md:22–38`, and `2026-11-01-v0.12.1.md:20–28`.

**Abort scenario:** Return fire bypasses even the three-aimed-weapon minimum (`AI/Brain/Strategy.cs:179–182`). A forecast of four passing shots can become zero or one useful aimed weapon while safe near-misses still consume energy. The current test explicitly accepts zero: `AI/Tests/Program.cs:446`.

**Concrete fix:** After correcting weapon identity and the attack signal, keep 0.6/4 provisional. Preserve an actual-useful-hit check for ordinary return fire, while retaining unconditional use-or-lose exceptions for genuine attrition/max-hold. Allow pulse bypass preferentially when losses are imminent or released energy has little investable value.

A next-turn-growth-versus-loss model is **not clearly better yet**. It needs trustworthy weapon counts, damage outcomes and an estimate of what released energy could actually buy. Fixing those inputs is more valuable than adding a more elaborate predictor now.

### 7. Persistent city coordinates were not demonstrated; shadows-off is the strong explanation

**Evidence**

- All five cities were acquired by T9, but `unseenCities` already returned to **three at T19**, before the late T28 decline: `v0151-ai-decisions.jsonl:45,95`.
- Native visibility changes Visible → Shadow when `SeenBuildingsShadows` is enabled and preserves Shadow during subsequent hiding: `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:183–189`.
- `RemoveHitShadowBuildings` only removes qualifying **Destroyed** shadows; a surviving hit city does not satisfy that rule. It also does not rewrite the visibility dictionary: `IntelMemory.cs:176–202`.
- Plot-occupation cleanup similarly removes list/observer membership, not dictionary visibility: `IntelMemory.cs:151–173`.

**Conclusion:** During an ordinary uninterrupted match, the recorded surviving cities returning to Invisible strongly indicates **SeenBuildingsShadows was off**. Ordinary hits removing living city shadows do not explain it.

**The actual setting is unverified:** neither supplied journal records the flag. “All 15 selected strikes were visible” is verified; “no Shadow candidate ever existed” is stronger than the strike logs establish because they do not enumerate candidate visibility classes.

**Failure scenario:** The opening spends heavily to acquire coordinates, but counterintel hides the nearly finished city again. At T25–T27 one selected city fell through recorded durability **471 → 338 → 193**; later strikes moved elsewhere (`v0151-ai-decisions.jsonl:149,158,168,178`).

**Concrete fix:** Log the setting and distinguish **ever acquired** from **currently targetable** cities. If persistent observed-coordinate memory is intended with shadows off, cache only genuinely visible observations. Do not use the native frozen-state store as remembered information for Invisible buildings: native code refreshes that store whenever visibility is not Shadow (`PlayerIntelHandler.cs:204–206`).

## Low findings and verification corrections

### 8. B’s geometry and same-frame reservation path check out, but the failure diagnosis and diagnostics need correction

**Geometry: no blocking defect found.**

- Building footprint radius is data radius × building scale, matching placement and `BaseBuilding.Radius`: `PlayerBuildingHandler.cs:92–98`; `BaseBuilding.cs:559–568`.
- Actual defense reach and power-line length use straight world-space distance: `PlayerDefenseHandler.cs:264–275`; `PlayerPowerGridHandler.cs:63–73`.
- The tangent basis is valid. Projection onto the curved surface changes the requested tangent distance, but placement is subsequently checked at the actual returned world position: `AI/DirectorPatches.cs:1931–1955`.
- The ring matches the existing **heuristic** deficit rule. It does not guarantee protection against every city-edge or nearby nuclear impact; that limitation is documented in `AI/Brain/Coverage.cs:221–240`.
- Native collision checking uses summed footprint radii; I found no additional city exclusion zone: `PlayerBuildingHandler.cs:753–770`.

**Nearest-anchor selection is safe against the alleged ordinary same-frame race.** Search success immediately calls the construction callback, which reserves the anchor slot and adds the placement to `ConstructedBuildings` before the next search runs. Subsequent coverage models include those actions. Sources: `AIBuildingPlacementSearch.cs:80–86`; `AIConstructionQueue.cs:154–159`; `AIActions.cs:82–92`; `AI/DirectorPatches.cs:1735–1751`.

Cities can be anchors when they are actually distributing/potentially distributing power and have a slot. Merely being a city does not make one a distributor: `BuildingPower.cs:71–115`.

**What B still cannot solve:** A deficient city may have no legal land footprint with an accessible free powered connector. More samples do not create an anchor or clear ruins. The supplied logs cannot establish which obstacle blocked the remaining city.

At defaults, the coverage-specific 150-step cap precedes the general 1,500-step cap. Frame-budget waits do not increment the search counter (`AI/DirectorPatches.cs:1805–1825`; `AI/Plugin.cs:552`). I found no default cap interaction that necessarily kills this search prematurely.

**Concrete fix:** Include city identity, available-anchor count and a final reason in search outcomes, including zero-anchor and general-finder abandonment paths. Repeated no-anchor failures should trigger a bounded connecting-plant/relay response.

**Corrections to the playtest note**

| Claim | Verified correction |
|---|---|
| Every Ion search T26–T33 was refunded; two Ions came from counter-defense | **T26 queued three dedicated anti-nuke Ions and refunded one. T27 had two powered Ions protecting two of three cities.** Later attempts failed to cover the remaining city. `v0151-ai-decisions.jsonl:156,164–166`. |
| `minCityCover = 0` meant no city cover happened | It meant **at least one city remained uncovered**. Coverage became 1 at T35 when cities fell from three to two, with no new Ion purchase. `:236,244`. |
| Eight Telescopes T6–T8 | Eight were queued, one refunded; **seven stood at T9**. `:30,34,41,44`. |
| Human destroyed 16 of 22 Railguns at T38 | The strategy’s operational **all-weapon** count fell 22 → 6. Intact Railguns fell **21 → 11**; disconnection also mattered. The T38 snapshot reflects the preceding action phase. `:257–268`. |
| AI out-produced human from T13 through T31 | Not continuously. At T26 AI energy was 5,105 versus human 7,394 at round start; at T30 8,014 versus 11,618. `v0151-ai-decisions.jsonl:154,195`; human telemetry `:334,513`. |
| Human had 30 techs by T38 | It had **32 at T38**, with 30 already recorded at T35. Human telemetry `:982,1424`. |
| Late pressure was 53–101 shots every turn from T28 | Several turns were much smaller: T29 = 20, T32 = 19, T35 = 47. T37 was 101 Railgun shots plus four Missiles. |

The central finding that the AI withheld substantial usable fire is supported. Its precise loss counts, coverage diagnosis and economic explanation need the corrections above.

## Next moves

1. **Fix weapon identity validation first**, including quota selection and firing diagnostics; verify destroy → pool reuse → replacement, including reuse by another player.
2. **Make the attack signal phase-bounded and enemy-only**, then evaluate 0.6/4 with actual aimed/paid shots rather than forecast salvos alone.
3. **Protect one real opening economy investment across priority purchases**, and confirm acquisition timing alongside T5–T10 income.
4. **Align the Ion purchase cap and instrument the remaining deficient city**, adding connector recovery only when the failure evidence calls for it.
5. **Most likely next loss:** sustained Railgun pressure again collapses extraction/reconnection faster than the AI finishes human cities. **Single fastest measurement:** actual human city-population damage per 1,000 energy paid by registered AI weapons on the first three return-fire turns. That directly tests whether the new firing expenditure buys the irreversible suppression needed to prevent another economic collapse.
