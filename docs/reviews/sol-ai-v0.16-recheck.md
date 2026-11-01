The withholding and quota changes run before abort, and removing entries is safe: native `TargetWeapons` accepts an empty input list. Two gaps remain so far: repairs can still reduce the later defense allocation, and the new firing-energy log records selections rather than successful payments.
# v0.16.0 fixes re-check — f0179c0

**DO NOT SHIP yet.** The fixes resolve several original defects, and the firing-list manipulation is compatible with native execution. However, the repair budget still allows repairs to consume the intended defense allocation, blind-fire withholding checks the assigned enemy rather than the actual trajectory destination, and successful firing payments remain unlogged. There is also a confirmed new regression: the new `fire` event crashes the existing decision summarizer.

**Confidence:** High on the source-level findings. Hook execution and gameplay outcomes remain **unverified in Unity**.

Reviewed the supplied review, disposition, fixes diff and corresponding source at HEAD `f0179c01d25cc1978e45fd70c3aa04ddf48cb412`, against `decompiled/`. No files were edited, created or deleted. Ion cover-site reservation is accepted as deferred under `docs/reviews/sol-ai-v0.16-disposition.md:5` and is not counted as a blocker here.

## 1. Finding-by-finding status

These statuses cover the **whole original finding**, including portions omitted from the disposition’s abbreviated descriptions.

| ID | Status | Evidence and assessment |
|---|---|---|
| **F1** | **PARTIAL** | `AddStrikeCandidates` now accepts Visible buildings only: `AI/Patches.cs:748–759`. The zero-candidate branch independently generates a random surface point and suppresses native selection: `:710–716`. **However, the exception path still returns to native targeting:** `:721–724`, which accepts Shadow cities. See remaining finding 1 below. |
| **F2** | **FIXED** | `EnemyView.VisibleCities` is now the count of currently Visible, Normal cities: `AI/Patches.cs:222–226`. Its remaining decision consumers—`AI/DirectorPatches.cs:485–486` and `AI/Brain/TargetPlanner.cs:75`—therefore no longer consume the real surviving-city total. Other enemy-city enumerations used here filter current visibility: `AI/DirectorPatches.cs:418–420`, `AI/StrategyPatches.cs:234–236`. I found no remaining direct aggregate surviving-city-count consumer in `AI/`. |
| **F3** | **FIXED** | `CityFreeSlots` subtracts both native committed projects and queued projects: `AI/DirectorPatches.cs:1630–1650`. The reflected field and calculation match `decompiled/Interplanetary.AI.Construction/AIProjectConstruction.cs:12,24–36`. Policy and purchase validation use that helper: `AI/DirectorPatches.cs:1733–1734,1757`. |
| **F4** | **PARTIAL** | Fresh pending evidence is now read before the roll: `AI/DirectorPatches.cs:1178–1185`; all Normal weapons contribute firing protection: `:1074–1080`. **The repair budget still omits recovery from the protected total**, and the planned hold still uses previous-turn director inputs and omits the explicit Ion floor. `:1186–1192`, `:1111–1112`, versus construction at `:623–633`. |
| **F5** | **FIXED** | Both director intel purchase paths pass the shared budget checks and record spending: `AI/DirectorPatches.cs:693–715`. The independent preferred-intel path is removed: `:818`. Minimum-only spending uses `RecordSpend`, which cannot reset a batch: `AI/Brain/Tempo.cs:105–109`; a waiting batch makes discretionary `Allowed` return zero: `:89–93`. Actual-credit and upkeep limitations remain under M4. |
| **F6** | **FIXED** | Candidate availability is checked for the committed target: `AI/DirectorPatches.cs:945–958`. Ordinary precision purchases are refused at `:1248–1253`; precision superweapon goals are filtered before savings at `AI/EconomyPatches.cs:270–273`; purchase is independently guarded at `AI/DirectorPatches.cs:833–838`. |
| **F7** | **PARTIAL** | Withholding runs before abort and removes blind guided weapons from both lists: `AI/Patches.cs:389–396,521–547,554–575`. **Its candidate lookup uses `TargetEnemy`, not the selected trajectory’s actual destination:** `:375–384`. The new energy fields remain planned selections rather than successful payments: `:435–463`. |
| **F8** | **FIXED** | Class-specific limits are enforced on the final lists: excess hits are removed, and deficits are filled only from the same class, primary before secondary: `AI/Patches.cs:402–430`. Enforcement follows refresh/withholding and precedes abort: `:505,532–546,554–575`. It does not recreate native cross-class quota transfer. F7’s destination-validation gap remains separate. |
| **M1** | **PARTIAL** | Unreachable **weapon types** are excluded from the new damage weighting: `AI/Brain/Strategy.cs:213–224`; precision-reason strikes skip the weapon preference: `AI/Patches.cs:708`, `AI/StrategyPatches.cs:407–411`. But damage is still averaged across all weapons sharing an ID, including unreachable upgraded instances: `AI/StrategyPatches.cs:187–190,203–206,396–403`. There is still no city-specific expected-damage allocation. |
| **M2** | **PARTIAL** | Queued Jamming now compounds projected science: `AI/DirectorPatches.cs:1558–1574`, updated at `:1337`. The broader forecast defect remains: later building/upgrade options use live global multipliers (`:1373–1375,1768–1787`), and installed-project remaining durations are not incorporated into the horizon (`:1544–1545`). |
| **M3** | **PARTIAL** | Exact defense-unlocking heads survive the guard: `AI/EconomyPatches.cs:521–522`. This neither preserves prerequisite-chain provenance nor persists cumulative policy state. Initialization still resets memory (`AI/Patches.cs:37–39`), including intel/war/volley state (`AI/DirectorPatches.cs:212–216`). The unconditional defense-tech exemption also introduces a broader research-guard bypass. |
| **M4** | **PARTIAL** | The Enhanced 50-E Lidar / 200-E array prices are checked separately, minimum intel may buy Lidar, and upgrade/building counters are separated: `AI/DirectorPatches.cs:693–710,897–909`. **Recurring upkeep remains neither valued nor separately logged**, and cumulative credits still use post-boundary production: `:503–506,2690–2695`. |
| **M5** | **FIXED** | Blind upgrades now enter `PlannedWeaponEffects`: `AI/DirectorPatches.cs:1975–1982`, consumed by the generic planner at `:2042–2045`. The blind package is tried before ordinary weapon purchases: `:787–796`. |
| **M6** | **PARTIAL** | The unpowered-defense inclusion is fixed in both cost estimation and upgrade selection: `AI/DirectorPatches.cs:1143–1145,1937–1939`. The rest of the original finding remains: roll cost is an unweighted mean across powered defense types (`:1146–1151`), not the relevant counter/range population, and the requested eligibility/overlap diagnostics are absent. |

**Disposition clarification for F5:** minimum intel does not *always* avoid opening a batch. When `bidAllowed > 0`, it uses `Record` (`AI/DirectorPatches.cs:714`), even if minimum intel also requested the purchase. This does **not** recreate the waiting-batch restart defect, because `Allowed` returns zero while that batch awaits evaluation.

## 2. New or remaining findings, ranked

### 1. BLOCKER — F1 retains a fail-open path to Shadow targeting

**Locations:** `AI/Patches.cs:721–724`; native selection at `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:418–443,483–485`.

The normal paths are repaired:

- Shadow entries cannot enter `AddStrikeCandidates`.
- Random-surface fallback uses no city record.
- The native action called by that fallback merely stores the supplied point: `decompiled/Interplanetary.AI.Actions/AISecondPhaseTargetWeaponAction.cs:23–29` and `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:313–315`. It does not snap the point to a remembered city.

**But any exception in the selection/action block still returns `true`, authorizing the native handler.** That handler rejects only `Invisible` cities, so Shadow records can again select precision coordinates.

This is a conditional remaining fairness failure; I did not reproduce a runtime exception. The random-surface branch itself is clean.

**Concrete fix:** make the active smart-strike hook fail closed. On selection failure, use a guarded information-independent fallback or suppress second-phase targeting; never return to the native building selector. Keep action submission separate from subsequent bookkeeping so recovery cannot enqueue a duplicate action.

---

### 2. HIGH — F4’s repair arithmetic still consumes defense room

**Locations:** `AI/DirectorPatches.cs:1111–1112,1186–1192,623–633`.

The corrected ordering is:

1. Repairs read `PendingDefender` in the prefix.
2. Native `AICore.BeginTurn` snapshots resources and reserves firing costs: `decompiled/Interplanetary.AI/AICore.cs:162–167`, `decompiled/Interplanetary.AI/AIResources.cs:44–62`.
3. The postfix rolls the ledger: `AI/EconomyPatches.cs:94`.
4. Construction applies its defense reserve and recovery cap.

That part is sound. The arithmetic is not.

`RepairDefenseHold` caps the hold using room **after recovery**, but `RepairDamaged` subtracts only firing and that hold when calculating repair affordability. It consequently treats the recovery floor as repair money. Construction then subtracts recovery again from what remains, reducing the defense allocation.

A concrete case using the original figures:

```text
Energy credited/current:             14,725
Firing reservation:                   5,100
Recovery floor, 20%:                   2,945
Planned defense request, 40%:          5,890

RepairDefenseHold = min(5,890, 14,725 − 5,100 − 2,945)
                  = 5,890

Repair budget = min(25% × 14,725, 14,725 − 5,100 − 5,890)
              = 3,681
```

Thus **3,600 E of repairs is still permitted**. After those repairs:

```text
Native usable energy: 14,725 − 3,600 − 5,100 = 6,025
Construction reserve cap: 6,025 − 2,945 = 3,080
```

The motivating underfunding therefore remains possible despite nominally “holding” 5,890 E.

There are also mismatched requests: the repair hold uses `prev.OwnDefenses`, `prev.VisibleEnemyWeapons` and `prev.RecentDamage`, whereas construction uses current inputs; the repair hold omits construction’s explicit Ion demand at `:630`.

**Concrete fix:** prepare one current intended defense request—including planned, observed, forward and applicable Ion demand—and protect **both its affordable allocation and recovery** before repairs:

```text
repairRoom = max(0, currentEnergy − firing − recovery − protectedDefense)
```

Use the same prepared request at construction and record any later reduction.

---

### 3. HIGH — F7 withholds against the wrong enemy for secondary trajectories

**Locations:** `AI/Patches.cs:375–395,422–426,540–546`.

`CandidatesFor` examines `w.TargetEnemy`. That is the assigned target, not necessarily the planet hit by `w.Hit`.

Native trajectory discovery explicitly permits secondary hits on another planet: `decompiled/Interplanetary.AI/AIWeaponTargeting.cs:365,382–384`. Final targeting calls second-phase selection for **`completedResults.HitCelestialBody`**: `decompiled/Interplanetary.AI/AITargetWeapons.cs:142–151`.

**Concrete counterexample:**

- Assigned enemy A has a visible candidate.
- Enemy B has no visible candidates.
- A guided weapon has only a secondary trajectory hitting B.
- Withholding sees A’s candidates and retains it; quota filling can select it.
- Native completion targets B, where the repaired second-phase hook chooses a random surface point.

Zero-candidate precision firing is therefore still possible, and the new target/candidate diagnostics attribute it to A.

**Concrete fix:** evaluate the destination of the selected hit/miss trajectory, and recheck the completed trajectory’s actual destination immediately before authorizing first-phase targeting/payment. Fail closed for unresolved destinations. Use that same actual destination in firing diagnostics.

---

### 4. HIGH — F7’s paid-energy gate remains unimplemented

**Locations:** `AI/Patches.cs:435–463,547,571–575`.

The new record sums live costs from selected lists **before abort, end-turn trajectory completion, action acceptance and payment**. It can report firing energy for a salvo that subsequently aborts or fails to pay.

The disposition’s statement that native payment failure is not observable is incorrect:

- The action invokes `CompleteFirstPhaseTargeting`: `decompiled/Interplanetary.AI.Actions/AIFirstPhaseTargetWeaponAction.cs:27–33`.
- That method clears `IsFirstPhaseTargeted`, checks affordability, and returns early on failure: `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:230–247`.
- Success sets the flag and spends the actual firing costs: `:248–262`.

**Concrete fix:** retain the current record as explicitly **planned** firing. Add a successful-payment record at the first-phase completion boundary, with weapon identity, class, actual destination, candidate count and actual energy delta. Keep withholding/quota enforcement before abort; emit final planned counts after abort or mark their aborted status explicitly.

The adopted loss-per-1,000-**paid**-E gate still cannot be computed faithfully.

---

### 5. MEDIUM — NEW: incompatible `fire` schema crashes the summarizer

**Locations:** new producer `AI/Patches.cs:459–463`; existing producer `:651–656`; consumer `AI/tools/summarize_decisions.py:54–62`.

The new event also uses `"e": "fire"`, but lacks fields required by the existing consumer, starting with `primaryByTarget`.

**Confirmed with an entirely in-memory reproduction:** the legacy event was accepted; the new event produced `KeyError: 'primaryByTarget'`. No log or output file was created.

**Concrete fix:** give the new selection record a distinct event type, such as `fire-plan`, and update its consumers explicitly. Preserve a clear distinction between planned selections, existing trajectory summaries and successful paid shots.

---

### 6. MEDIUM — M1 still averages unreachable upgraded instances into credible-kill damage

**Locations:** `AI/StrategyPatches.cs:187–190,203–206,396–403`; `AI/Brain/Strategy.cs:213–230`.

The correction works when the unreachable heavy weapon has a different weapon ID. It does not work when reachable and unreachable weapons share an ID but have different installed damage upgrades: their damage is averaged before reachability is considered.

The resulting aggregate is still multiplied by total passing shots, without allocating expected damage to a particular visible city.

**Concrete fix:** accumulate reachable damage per weapon instance for each target, apply the relevant accuracy/interception expectation, and allocate enough expected damage to a specific current-Visible city before declaring a credible kill.

---

### 7. MEDIUM — M3’s provenance substitute bypasses the guard in ordinary play

**Locations:** `AI/EconomyPatches.cs:521–524,601–605`.

`s_defenseTechs.ContainsValue(head)` preserves **any** defense-unlocking head, regardless of whether the mod queued it, an observed threat justifies it, or a load occurred. For example, an unowned native `tec_UltrasonicProjection` head now survives while planned research remains.

Conversely, a legitimate loaded counter queue whose current head is a prerequisite rather than one of those exact defense technologies can still be replaced.

**Concrete fix:** preserve/reconstruct ownership of the counter goal **and its prerequisite chain**, scoped to actual counter provenance. Remove the unconditional technology-name exemption. Persist cumulative intel, war-latch and volley-peak state to resolve the remainder of M3.

---

### Other remaining medium findings

| Finding | Evidence | Concrete fix |
|---|---|---|
| **M2 — Incomplete shared forecast** | Live modifiers remain at `AI/DirectorPatches.cs:1373–1375,1768–1787`; project valuation uses nominal duration at `:1544–1545`. | Carry queued global modifiers across project, building and upgrade options; forecast own installed-project expiry over the same horizon. |
| **M4 — Upkeep and actual credits still missing** | Credits use production at `AI/DirectorPatches.cs:503–506`; no separate intel-upkeep field accompanies `:2690–2695`. Native energy credit precedes building/project boundary processing: `decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:449–452`, `PlayerResourceHandler.cs:158–159`. | Capture actual credits at the credit boundary and separately value/log recurring intel upkeep. |
| **M6 — Relevant roll-cost/overlap model incomplete** | `AI/DirectorPatches.cs:1141–1151,1167–1168`. | Estimate costs from powered defenses relevant to the observed incoming types, and log the eligible-share and overlap assumptions. The powered-only correction itself is valid. |

## 3. F7/F8 native-state safety check

**Removing weapons from the two firing lists does not itself break native `TargetWeapons`.**

- Withholding and quota enforcement run before abort: `AI/Patches.cs:521–575`.
- Native `TargetWeapons` builds its inputs from the lists as they stand; it does not require their counts to equal `Hits`/`Misses`: `decompiled/Interplanetary.AI/AIWeaponTargetingGoal.cs:73–104`.
- An empty list is valid: `CalculateEndTurnTrajectories` stores zero inputs, and `UpdateEndTurn` completes it: `decompiled/Interplanetary.AI/AITargetWeapons.cs:38–50,82–101`.
- That is consistent with the existing hold path, which clears both lists.

I found no demonstrated new normal-path null-reference, collection-modification exception or resource double charge in these fixes. The confirmed new exception is in the Python log consumer. The remaining fairness exposure is F1’s native exception fallback; the principal new policy spillover is M3’s unconditional research exemption.

## 4. Release blockers and verification boundary

Remaining blockers are:

1. **F1:** fail-open native Shadow targeting.
2. **F4:** repairs can still shrink the intended defense allocation.
3. **F7:** withholding validates the assigned enemy rather than the actual destination.
4. **F7:** successful paid firing energy is still absent.

The new summarizer schema regression should also be corrected before collecting the next playtest.

`git diff --check` passed. The parser regression was reproduced in memory. Builds, Brain tests and mutation tests were not rerun because they can write artifacts; the disposition’s reported results were not independently revalidated. Unity behavior remains unverified.

**verdict: DO NOT SHIP.**
