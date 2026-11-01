The new payment hook has a registration problem: `CompleteFirstPhaseTargeting` has two overloads, but `TryPatch` looks it up by name alone. I’m checking that alongside the refusal flow and whether the repair hold now matches construction’s current-turn request.
# v0.16 round-2 re-check — `46c28e9`

**DO NOT SHIP yet.** F1 and the summarizer regression are resolved. The new payment hook is registered through an ambiguous overloaded-method lookup, so its destination guard and paid-energy logging cannot be relied upon. F4’s recovery subtraction is corrected, but repairs still protect a stale defense request.

**Confidence: High on source-level findings.** Unity execution remains unverified. No files were edited, created or deleted; all inspection stayed inside this repository.

## 1. Blocker-by-blocker status

| Item | Status | Evidence |
|---|---|---|
| **F1 — fail-open second-phase targeting** | **RESOLVED** | Once active smart strikes enter the replacement, invalid inputs and exceptions return `false`, suppressing native selection: `AI/Patches.cs:788,808–817,832`. Recovery uses an information-independent random surface point. The `queued` flag prevents recovery from adding another action after successful submission: `:799–806,812–815`. Candidate construction remains Visible-only: `:841–852`. |
| **F4 — repair arithmetic/protected defense allocation** | **NOT RESOLVED** | The missing recovery subtraction is fixed at `AI/DirectorPatches.cs:1115–1118`. However, the protected request still uses `prev.VisibleEnemyWeapons`, `prev.EnemiesAlive`, `prev.RecentDamage`, and previous-turn Ion threat/count: `:1200–1205`. Construction uses current-turn inputs at `:624–634`. Thus repairs can still reduce the allocation construction subsequently requests. See below. |
| **F7 — assigned enemy versus actual destination** | **NOT RESOLVED** | The initial-list correction is sound: secondary hits and misses use stored trajectory destinations at `AI/Patches.cs:379–414`; quota refill excludes unresolved/blind hit destinations at `:621–622`. But final trajectories are recalculated before action submission (`decompiled/Interplanetary.AI/AITargetWeapons.cs:70–74,142–151`). The intended final guard at `AI/Patches.cs:498–529` has the registration defect described below. |
| **F7 — successful paid firing energy** | **NOT RESOLVED** | The postfix itself reads the successful result and actual energy delta correctly: `AI/Patches.cs:531–544`, matching native targeting/payment at `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:236–262`. However, the same registration defect prevents accepting this as an installed payment ledger. |
| **Summarizer regression** | **RESOLVED** | The new selection event is now `fire-plan`: `AI/Patches.cs:479–483`. The legacy `fire` producer retains the required fields: `:732–737`; its consumer remains `AI/tools/summarize_decisions.py:54–62`. An in-memory check accepted legacy `fire` alongside the three new event types. Reintroducing the old incompatible `fire` label reproduced `KeyError: 'primaryByTarget'`. |

### F4: corrected subtraction, still different requests

Own defenses are now counted live, and the old **14,725-E example** is repaired: its repair budget becomes **790 E**, so the previously permitted 3,600-E repairs are refused.

But the remaining stale-input gap was also identified in round 1. For example, using the reserve formula at `AI/Brain/Director.cs:430–444`:

- Energy/income **10,000**, firing protection **3,000**, recovery **2,000**.
- Ten own defenses; previous known enemy weapons **0**, current known weapons **80**, one enemy; no observed/forward demand.
- Repair hold uses the previous picture: **1,500 E**.
- Repairs receive a budget of **2,500 E**.
- A **2,400-E** repair selection leaves construction **4,600 E** after firing protection.
- Construction now requests **4,000 E**, but recovery clips it to **2,600 E**. Without those repairs, all 4,000 E was affordable.

Current known weapons are refreshed before construction (`AI/EconomyPatches.cs:96–145`; `AI/DirectorPatches.cs:346–350`). They are not shared with the repair hold. Previous-turn Ion inputs have the same mismatch.

**Required fix:** prepare one current-turn defense request—including the applicable Ion floor—and use it for both repair protection and construction allocation.

## 2. New hook: registration, signature and native-state safety

### New blocking defect: overloaded target lookup

Registration is:

`AI/Plugin.cs:663` → `TryPatch(...)` → `AccessTools.Method(type, method)` at `:690`.

The game defines **two** `CompleteFirstPhaseTargeting` overloads:

- Fourth argument `CelestialBody`, returning `bool`:  
  `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:186`.
- Fourth argument `TrajectoryCalculationResults`, returning `PlayerWeaponTargetingResult`:  
  same file, `:230`.

The name-only reflection lookup is ambiguous. `TryPatch` catches installation failures and returns zero (`AI/Plugin.cs:701–704`), leaving the behavior native. A successful compilation does not validate this lookup.

**Required fix:** explicitly select the overload with parameter types:

```text
WeaponGameplayData, float, float, TrajectoryCalculationResults
```

### Is `ref __result` / `out __state` valid?

**Yes, for that explicitly selected trajectory overload.**

`ref PlayerWeaponTargetingResult __result` matches its return type. Prefix `out int __state` and postfix `int __state` are a valid Harmony state pairing in the same patch class. The prefix initializes state on every path (`AI/Patches.cs:498–507`); refusal supplies a result and negative sentinel, which the postfix ignores (`:515–518,534`).

### Does refusal safely match native refusal?

**Yes at source level, assuming the hook is installed correctly.**

Native refusal creates/reuses the dictionary result, clears `IsFirstPhaseTargeted`, and returns without payment (`PlayerWeaponHandler.cs:232–246`). The prefix performs the same consequential operations (`AI/Patches.cs:511–522`).

One important distinction: refusal happens **during queued action execution**, not while `AITargetWeapons` is scheduling actions.

- `AIActions.FirstPhaseTargetWeapon` queues the first action and returns success: `decompiled/Interplanetary.AI/AIActions.cs:130–142`.
- `AITargetWeapons` may therefore already have queued second-phase targeting: `decompiled/Interplanetary.AI/AITargetWeapons.cs:142–151`.
- The first action ignores the returned targeting result: `decompiled/Interplanetary.AI.Actions/AIFirstPhaseTargetWeaponAction.cs:27–34`.
- The queued second action then encounters `IsFirstPhaseTargeted == false`, **logs an error and returns**: `PlayerWeaponHandler.cs:302–306`.

That is also possible after native payment refusal. It causes **no source-visible exception, retry loop, stuck readiness state or double spend**. Action execution continues and clears the queue (`AIActions.cs:43–50`); rejected results are excluded from firing output (`PlayerWeaponHandler.cs:34–46`).

**Human isolation is sound:** non-AI handlers return before state/resource mutation, leaving `__state = -1`, so the postfix exits (`AI/Patches.cs:501–507,534`).

## 3. Other round-2 observations

- **No additional demonstrated normal-path null-reference or hidden-information leak** was found in these changes. The confirmed new defect is hook registration.
- The new final guard itself **fails open on exceptions** (`AI/Patches.cs:524–527`). Unlike the repaired F1 path, this does not restore native Shadow selection, but it can bypass blind-shot refusal. Exceptions during refusal bookkeeping can also leave `__state = -2` while allowing native payment, suppressing the paid record. Keep refusal authoritative once decided and isolate its logging.
- **Laser-enabled accounting caveat:** lasers pay through `CompleteLaserFirstPhaseTargeting`, not this overload (`decompiled/Interplanetary.AI.Actions/AIFirstPhaseTargetLaserWeaponAction.cs:23`; `PlayerWeaponHandler.cs:265–292`). The proposed ledger therefore covers trajectory weapons, not every AI weapon payment.

## Verification boundary

`git diff --check f0179c0..46c28e9 -- AI` passed. The summarizer check ran entirely in memory and demonstrated both the old failure and corrected acceptance. Builds, mutation tests and gameplay were not rerun under this read-only constraint.

**verdict: DO NOT SHIP.**
