# Council decision record: Smarter AI v0.16 (revision 2)

**Question (charter):** What change set for Smarter AI v0.16 gives the AI the best chance to beat Pavol's usual build-out (match 16 shape)? It must stay within fair information, human-like accuracy and no cheats.

**State:** pending re-sign-off of this revision, which uses the charter's one retry per lane. If either lane objects again or fails, the result is ADVISORY REVIEW.

**History:**
- Revision 1 (`decision-record.md`, sha256 `888f8bb0…63fe`) is kept unchanged.
- Lane A confirmed revision 1 with seven accepted corrections (`signoffA.md`).
- Lane B objected on attribution and on one omitted part of its non-negotiable (`signoffB.md`).
- Section 8 lists every change in this revision and its source.

**Facilitator:** Claude Opus 5.5. It owns no load-bearing measurement, and every number cites the lane that derived it.

## 0. Integrity

| Item | Value |
|---|---|
| Frozen target | `24683684d9799b9b22db765c251ac811ef8793ae`. HEAD is unchanged; later commits are docs only and out of scope. |
| Git porcelain | Byte-identical to `status-before.txt` after every pass: five untracked paths, no tracked diff. |
| Data manifest | 6 of 6 files match `data-manifest.txt`. |
| Sealed files | `laneB.md` a06745b5…430a, `laneA.md` 1facc88e…709b, `xreviewA.md` dc6ac40e…94e4, `xreviewB.md` 9d7f9f4b…05e7, `signoffB.md` 330bf75d…6e38, `signoffA.md` afa5770c…6d19. Full hashes are in `seals.txt`. |
| Roster | A = Fable (Anthropic). B = GPT-6.1 Sol (OpenAI). C = Gemini 4 Argon, recused with Pavol's approval; it produced no output. |
| Label | CROSS-FAMILY CONCURRENCE, two families. Correlation risks: A and the facilitator are the same family; one packet and one corpus are shared. |

## 1. Agreements (both lanes, after cross-review and sign-off)

1. **The project-slot policy is the main economic defect.**
   - Five Molecular Recycling Plants were queued by T7 and two QEH by T11.
   - These seven queued permanent purchases account for all seven unlocked slots at T14.
   - Actual installed occupancy is a strong reconstruction only, because installed project identities are not logged.
   - The first recorded SSTAR purchase was at T31.
   - Universal zero free capacity on every turn T7-T30 remains UNVERIFIED.
2. **Temporary-project forecasting has a real defect** (`DirectorPatches.cs:1058`, `:1267-1288`).
   - Duration-adjusted investment utility is fed into projected next-turn income.
   - Queued global modifiers are not compounded.
   - The definite bugs are these two, not the 60% duration normalization itself (B-Amendment 2).
3. **Science tempo belongs in the slot policy.**
   - Human turn-end science production was about ×2.44-×3.06 baseline at T11-T12 and ×1.95 at T17-T20.
   - Summing turn-end production above population × 0.18 gives about 5,167.5 nominal extra science over T11-T20. This is not an exact credited-science ledger.
   - Jamming identities and installed counts remain inferred.
4. **The T21-T22 visible-city strike window was real and was forfeited.**
   - **T21:** 9 Missiles, 7.65 expected passing at 100% estimated passage.
   - **T22:** 13 shots, 10.007 passing.
   - **Targets:** two cities were currently visible on both turns, and the AI held both turns below MinPassingShots 12.
   - **T23:** no city visible.
   - `target.cities 5` is the population-readability gate, not five targetable cities.
   - **Lane A withdraws its 2,900-3,600 damage figure.** It double-counted accuracy, because the logged 7.65 and 10.007 already include the 0.85 hit rate, and it included T23. A accepts Sol's figure of about 2,550 nominal population damage (7.65 at T21 plus 11 guided × 0.85 at T22, × 150).
5. **The blind Missile phase was wasted.**
   - All 45 Missile assignments at T24-T28 had zero candidates and used the native random-surface fallback (`AIWeaponTargeting.cs:493-497`).
   - Both lanes support adding targetability to purchasing (`TryWeapon` has none, `DirectorPatches.cs:989-992`).
   - **The lanes word the rule differently:**
     - A explicitly proposes no new Missiles at zero candidates.
     - B proposes removing the precision premium and favoring upgraded unguided artillery when no useful currently visible second-phase target exists, while preserving precision options for valuable legal targets.
   - The facilitator adopts the zero-candidate purchase prohibition in item 4.
6. **Firing cap: keep `WarUpkeepShare` 0.4 and `MaxWeapons` 30.**
   - B withdrew its latched roster and reservation release from v0.16 (B-Amendment 3).
   - Fix the base-versus-live firing cost (`DirectorPatches.cs:985`, `:1568`, `:883`).
   - Add per-class planned hit quotas, 0.7 unguided and 0.85 guided (`Patches.cs:318-328`).
   - No accuracy change.
7. **Intel spending is bounded, and the Telescope intel upgrade (Lidar) gets valued.**
   - Unconditional escalation is replaced by a spend-and-outcome budget with a stop-loss, priced with upkeep.
   - Lidar, which `AddUpgradeOptions` ignores today (`DirectorPatches.cs:1154-1176`), is valued: intel ×1.5 on an 8-intel array (+4), 50 E / 175 M, upkeep 20 → 32 E.
   - No threshold may use the enemy's counterintel. A withdrew its own A5 stop rule on this ground.
8. **Defense: Kinetic upgrades plus funded rolls.**
   - **Upgrades:** cost-down needs Maglev, which the AI had at T15; range needs Early Warning AI, 600 science.
   - **Funding:** 40 rolls went unpaid in the 83-impact phase, with 3,400 E spent.
   - **A withdraws its stop count.** Its A6 read the 40 unpaid rolls as about 30 lost stops; A now calls that an upper bound only, because overlapping defenses each report an unpaid roll for the same impact (`Director.cs:437-443` caps them at eligible impacts). A's floor formula was sized on the 56 in-range impacts, not on rolls.
9. **Ion insurance, one per city, needs an executable path.**
   - The prior must be set and executed early.
   - Plasma must come right after Maglev. In match 16 Plasma was set only at T28, so the T24 prior could not execute.
10. **Not in v0.16:**
    - **B's latched roster and reservation release.** Fable: REJECT for v0.16, and a blocker if it is ever implemented without a reservation-reconciliation test, because its failure mode is a resource cheat.
    - **Any increase to `MaxWeapons` and any accuracy change.**
    - **Anything that rescues targeting on a city out of sight.** Counterintel must genuinely remove precision opportunities. Once a city is no longer currently visible, no coordinate, live durability, hidden threshold or remembered city record may rescue its targeting. That includes native frozen per-city snapshots. (B's non-negotiable, in full.)
    - **Hidden enemy values at runtime.** No runtime input from the enemy's counterintel, stealth or true surviving-city count. Every new rule runs on observable inputs only: own spend, own visibility results and currently visible candidates. (A's non-negotiable.)
11. **Gates are experiments, not predictions.** A gate whose opportunity never arises is UNTESTED, not passed.

## 2. New facts surfaced in cross-review and sign-off

| ID | Fact | Derived by | Verification |
|---|---|---|---|
| N1 | **Research hijack:** five off-plan research heads at T23-T27 (MassBio, Optics, Uranium Alloys, Induction Coils, MagRad), with no mod `research` record. The mod yields to native research when its queue is non-empty or native urgent-intel research asks (`EconomyPatches.cs:506-508`). | A | not re-run by B or the facilitator |
| N2 | The human's planet material reached 0 at T30. The AI's planet held 134k at T32. | A | not re-run |
| N3 | **Fairness leak in shipped code:** `UnseenCities` iterates the enemy's real `GetRemainingCities()` and counts the invisible ones (`DirectorPatches.cs:323-329`). That is the true hidden surviving-city count. It drives intel escalation (`:575`, `:580`) and upgrade filtering (`:1153`), and has been live since v0.15. | B (blocker F1) | Facilitator confirmed at `DirectorPatches.cs:321-329`. |
| N4 | The game credits no science on T1 (`PlayerResourceHandler.cs:161-186`: the credit sits inside `CurrentTurn > 1`). On the frozen history the EMC chain first completes at T13. | B (F2) | A re-verified at sign-off and accepts it. |
| N5 | **Defense funding around the 83-impact volley.** Two constraints bound together; both lanes' readings are retained. | B (money order), A (estimator lag, at sign-off) | Facilitator read the T25-T28 director records. |
| | • T26 record (`v0153-ai-decisions.jsonl:169`, before the volley): `observedNeed` 1,250, `defenseReserve` 3,080. | | |
| | • T27 record (`:191`, after it): 83 impacts, 40 unpaid, 3,400 E spent, `observedNeed` 9,250, reserve 2,679. | | |
| | • A: the estimator lags one phase, so it asked for only 1,250 on the volley turn. | | |
| | • B: the money order capped the T26 reserve at 3,080 anyway (production 14,725 − 5,100 firing reservation − 3,600 repairs − 0.2 recovery floor). An estimator asking 9,250 would still have been capped there. | | |
| | • The T28 record shows the next phase: 78 impacts and 0 unpaid. | | |
| N6 | Under the director's limited-planet policy, mines get one yield upgrade plus efficiency (`DirectorPatches.cs:1124-1173`). So Robotic Mining alone does not double the AI's mines. | B | not re-run |
| N7 | **Jamming's ×0.9 population multiplier is treated as inert.** No consumer was found on the growth path. | Both lanes | not re-run by the facilitator |
| | • Jamming declares a ×0.9 global population-yield modifier (`ProjectsData`). | | |
| | • A proposed a provisional 3-copy ceiling as a precaution. At sign-off A re-checked and withdrew the growth effect: growth reads only the per-building `TotalBonusModifier` (`BuildingStatHolder.cs:108-116`, `:414`); `applyGlobalModifier` only registers with `PlayerStats` (`BuildingProjectStats.cs:115-119`); no game-logic caller reads `TurnYieldPopulation`, whereas energy's global modifier is consumed at `PlayerResourceHandler.cs:29`. | | |
| | • B withdrew its own growth claim on the same grounds. | | |
| | • B's ≥3 Jammings was a minimum experimental gate, not a ceiling. | | |
| N8 | Lane A's "20 upgraded arrays save 3,000 M and 200 E/turn" is wrong. For equal intel (240), 20 upgraded arrays cost 500 M more and 40 E/turn more upkeep than 30 base arrays. | B; A accepted it at sign-off after checking Telescope Array 200 E / 300 M / 8 intel / 20 E upkeep and Lidar 50 E / 175 M / ×1.5 / upkeep ×1.6 | arithmetic only |
| N9 | **Lidar intel price.** Per intel point, Lidar costs more material and upkeep than a new array (43.75 vs 37.5 M; 3.0 vs 2.5 E/turn) but less build energy (12.5 vs 25 E). | A, at sign-off | arithmetic only |

## 3. Ranked v0.16 list

Each item gives its sources, the adopted wording, the config and code paths, and the next-match gates. Where the lanes differ, the facilitator chose the more evidence-backed position and kept the alternative in section 4.

### 1. Joint flexible-slot engine: Jamming into SSTAR
**Sources:** A1/A3, B1, I1/I2. **Both lanes rank it first.**

**Adopted wording:** B's I1 text, verbatim:
> "Preserve flexible project capacity per city; use an opening ceiling of two permanent purchases as an initial policy, schedule Jamming and SSTAR against actual unlocked/pending capacity, forecast their compounded production and expiry separately from investment utility, and compare temporary/permanent investments over a common horizon."

**Additions:**
- SSTAR up to one per city once Lossless is researched.
- **Jamming ceiling (attribution):** the facilitator adopts A's provisional ceiling of 3 simultaneous Jammings after EMC. A keeps it on slot economy alone (N7). B supports jointly scheduled Jamming/SSTAR capacity and proposed a three-copy *minimum* experimental gate, but did not endorse the ceiling.

**Paths:** `CityForProject`, `TryCityProject`, `AddProjectOptions`, `TryEconomy`, `DirectorPatches.cs:1058` and `:1267-1288`, `Brain/Director.cs`.

**Gates:**
- Log installed, pending, unlocked, free and reserved slots per city, every turn.
- Zero permanent purchases into reserved capacity.
- At least 3 simultaneous Jammings within two turns of EMC, while at least 1,500 science of useful economy research remains.
- At least 3 simultaneous SSTARs by T20.
- At least 15,000 round-start E production at T20.

**Main risk (B):** removing the early material projects changes the bank that funds this plan.

### 2. Research order with a hijack guard
**Sources:** this is an adopted synthesis of B's I4/B5/B6 and A's research-hijack guard (N1, N4, N6). B's proposal to advance Ultracapacitors and Composite Heat Cells is not adopted here; it is preserved under S4/P1.

**Adopted wording:**
> "Reorder research for an executable economy/science tempo: earlier Super Tensile Materials, EMC, Nanostructuring, Microwave and Lossless; Plasma right after Maglev for scheduled Ion cover; Early Warning AI in time for Kinetic range; retain observed-threat counter-research. Use real credits and prerequisite closure, and fund Robotic Mining only when an installable yield-upgrade path is selected. Guard `EconomyPatches.cs:506-508` so every head change is logged with a reason and no off-plan head survives more than one turn, except an observed-threat counter-research head."

**Paths:** `Plugin.DefaultResearchPlan` plus its unchanged-default migration, and `EconomyPatches.cs:506-508`.

**Gates:**
- EMC is the research head by T10 (A; a head can be set before the science exists).
- EMC done by T14.
- Plasma done by T18.
- No research head outside the plan for more than one turn without a logged observed-threat reason.

### 3. Energy-to-material balance
**Sources:** A2, B's I3 (amended). The split was resolved at sign-off, see S2.

**Adopted wording:** B's I3 text:
> "Trial EnergyToMaterialTarget 1.0 in growth and war, retaining the additive live firing requirement, and MaterialGlutRatio 1.5 with the existing minimum stock. Evaluate production and conversion outcomes before introducing a separate higher war coefficient."

**Config:**
- `EnergyToMaterialTarget` 0.6 → 1.0.
- `MaterialGlutRatio` 3 → 1.5.
- `MaterialGlutMin` stays 5,000.

**Gates:**
- At T22, end-of-construction material stock is at most 2 × that turn's round-start E production.
- Zero extraction-only purchases while the glut state is active, unless a logged recovery need applies.
- Log the top affordable economy alternatives and why each was rejected.

### 4. Target-aware offense, the visible-city exception, class-correct accuracy
**Sources:** A4, B2, amended B4, I5/I6/I7.

**Adopted:**
- **A visible-city opportunity exception to `MinPassingShots`** in `Strategy.Decide`. There is no such branch today (`Strategy.cs:246-276`). The predicate is the intersection in S3.
- **`WarFromTurn` 20 → 16**, gated on technology, funded unexpired income and useful current targets.
- **A zero-candidate prohibition in `TryWeapon`:** no Missile or Nuclear purchase on a turn with zero strike candidates. This is the facilitator's adoption of A's wording; see agreement 5 for B's wording.
- **Correctness fixes:** live-cost accounting at `:985`, `:1568` and `:883`, and per-class hit quotas.
- **Unchanged:** `WarUpkeepShare` 0.4, `MaxWeapons` 30, `MinPassingShots` 12 and accuracy 0.7 / 0.85.

**Gates:**
- Zero below-saturation holds on a turn where the logged S3 predicate is true.
- No Missile queued on a turn with zero candidates.
- After blindness, at most 10% of paid firing energy goes to zero-candidate precision weapons.
- Zero funding overruns.
- Per-class planned hits within one weapon of their quotas.

**Main risks:**
- Precision opportunities may close before the arsenal is operational.
- Earlier firing has an investment cost: the T21-T23 holds released 10,000 E for construction (B).

The blind-phase weapon posture is S4, reserved for Pavol.

### 5. Bounded intel acquisition, all-in priced
**Sources:** B3, A5 (amended), I8, N3, N8, N9.

**Adopted wording:** B's I8 text:
> "Use one budget for intel buildings and upgrades, including recurring upkeep, timed to useful prospective precision fire. Escalate only after measuring permitted current visibility outcomes; suspend after two unsuccessful marginal batches and allow bounded probes. Do not use hidden counterintel, hidden city stealth or the true hidden surviving-city count."

**Additions:**
- Value Lidar in `AddUpgradeOptions` by its build energy at the window and by array count, not as a saving (N9).
- `MaxIntelBuildings` 30 → 21 as a secondary safety cap (A). B does not reject a secondary ceiling.
- **Replace `UnseenCities` (N3) with a permitted input.** Which one is choice P2.

**Gates:**
- Through T20, intel construction and upgrade energy is at most 10% of cumulative own energy credits; upkeep is logged separately.
- After two unsuccessful batches, a stop-loss is logged and no more than 2 further arrays are bought through T25.
- Runtime decision inputs contain no hidden counterintel, stealth or surviving-city values.

### 6. Defense: Kinetic upgrades, protected funding, executable Ion insurance
**Sources:** A6, B5, B6, I9, I10, N5.

**Adopted wording:** B's I9 text:
> "Combine powered Kinetic range/cost upgrades with a funded defense allocation protected before discretionary repairs. Retain the existing observed-demand estimator, improve distinct-impact/live-cost diagnostics, and evaluate reserve caps together with recovery and firing commitments."

**Additions:**
- **A forward signal, from A's sign-off note, which addresses N5's estimator lag.** After the first observed enemy impacts, fund the full roll capacity of the in-range Kinetics. It uses observable inputs only. This was added after B's sign-off, so B has not reviewed it yet.
- `DefenseReserveShare` 0.3 → 0.4. B notes this does nothing on its own without the reordering.
- Kinetic cost-down now; range once Early Warning AI is researched.

**Ion insurance:**
- `AntiNukePriorTurn` 24 → 18, one per city.
- Reserve feasible cover sites before ordinary construction blocks them.
- One terminal log result per search ID.

**Gates:**
- At least 10 Kinetics upgraded by T22 (A). B's "at least 20 powered and range-upgraded by T24" is the stretch gate.
- In the first qualifying volley of at least 60 impacts: at least 50% intercepted and zero unpaid rolls.
- Every reserve shortfall carries a logged allocation reason.
- Minimum operational city Ion cover of at least 1 by T20.

### Overall outcome gate (B, unopposed)
In the next usual build-out match, the AI must:
- survive the first qualifying mass-volley phase without losing more than one city;
- show persistent enemy losses from its earlier strikes, not only nominal arrivals.

Human telemetry is used only in the postgame evaluation, never at runtime.

## 4. Splits, each steelmanned (status after sign-off)

| ID | Issue | Lane A | Lane B | Type | Status and handling |
|---|---|---|---|---|---|
| S1 | EMC timing | EMC head by T10. "Done by T12" is withdrawn; A accepts T13 as the earliest completion on the frozen history and T14 as the gate. Jamming requires EMC, so it cannot move EMC earlier. | Done by T14: no T1 credit, so the 3,850-science opening first fits at T13. | Factual | **Resolved:** EMC head by T10, done by T14. |
| S2 | War E:M ratio and its gate | Withdraws the `energyLeft > 300` gate; accepts deferring 2.0 at war, because the additive upkeep term in `WantedEnergy` makes it overshoot the human's 2.05 total-income ratio. | Trial 1.0 in growth and war, keeping the additive live firing requirement. A 2.0 coefficient is not the human's observed ratio: if firing upkeep approaches 0.4 E, solving E = 2M + 0.4E gives E:M ≈ 3.33. `energyLeft > 300` rewards unspent discretionary energy rather than productive conversion. | Factual and inferential | **Resolved:** 1.0 in both phases; 2.0 deferred to v0.17 evidence. |
| S3 | Visible-city exception predicate | Fire when at least one city is currently visible, the expected passing shots from guided weapons (`d.Passing`, tested at `Strategy.cs:246`) are at least 6, and pass share is at least 0.9. Accepts the combined predicate. | Bypass MinPassingShots only for a funded attack whose currently visible legal targets and allocated expected damage satisfy a credible-kill rule. Passage probability alone is insufficient. | Inferential | **Resolved as an intersection.** Both A's numeric floor and B's funded, current-visible-target credible-kill predicate must hold. The operational threshold is fixed and logged during implementation, then checked against the T21-T22 replay. |
| S4 | Blind-phase posture | Upgraded Railgun artillery only as a fallback, gated on measured enemy loss per 1,000 E. Its dispersion is UNVERIFIED, and 45 blind Missiles showed no measurable loss. A accepts (b) as Pavol's call if two conditions hold: (1) the loss metric is logged from the first blind salvo; (2) Ultracapacitors and Composite Heat Cells (1,550 science) never jump ahead of EMC, Nano, Microwave, Lossless and Plasma. | When no useful currently visible second-phase target exists, deliberate upgraded unguided artillery, initially Triple Barrel plus Optimized Airburst, is the executable offensive branch, with Ultracapacitors and Composite Heat Cells advanced accordingly. Precision options are kept for currently visible cities and other valuable legal targets. Experimental gate: at least 60 live artillery projectiles per planned blind salvo by T24. | Strategic and value | **Open; reserved for Pavol (P1).** |
| S5 | Intel quantity and timing | `MaxIntelBuildings` 30 → 21 as a hard cap; arrays timed to the firing window T15-T19, not T8 (laneA §3.7, A5). The `unseenCities` = 0 on T19 gate is withdrawn under N3. | A T8 sighting cannot supply a T20 precision target once that city disappears. Time acquisition to a prospective useful arsenal, with a spending and outcome stop-loss and recurring upkeep priced in. A fixed six-to-eight or twenty-array package is not a universal policy. B did not reject a secondary safety ceiling. Through T20, construction and upgrade energy is at most 10% of cumulative own energy credits. | Inferential | **Resolved:** B's timing and budget, with A's 21 as a secondary safety cap. Whether the cap ever binds is untested. |
| S6 | Defense reserve floor | Floor = 100 E × in-range impacts × 1.25 when rolls went unpaid. A verified that the T27 record logs `observedNeed` 9,250, so its floor is redundant after the volley; the binding limit on the volley turn was the estimator's one-phase lag. | The existing estimator reacts strongly to unpaid rolls; the binding problem is the order of firing, repairs and recovery protection. | Factual (N5) | **Resolved:** both constraints bound (N5). B's reordering and A's forward signal are both adopted in item 6. |
| S7 | QEH rule | No QEH until an enemy intel building is seen. | The human had 77 intel at T20 with no Telescope, because city projects give intel (H16:123, counterintel ×5.35). | Factual | **Resolved:** B's correction; A verified it. Opening permanents are capped at 2 by count. |

## 5. Choices reserved for Pavol

| ID | Choice | Options | Lane positions | Facilitator recommendation |
|---|---|---|---|---|
| P0 | Build v0.16 | All six items now, a subset, or no action. | Both lanes rank items 1-3 (the economy) highest. | Build all six. Confidence: Medium on competitive effect, High on the code defects. |
| P1 | Blind-phase posture (S4) | (a) Artillery as a gated fallback. (b) Artillery as the primary blind branch, with its research advanced. | A: (a), accepts (b) with its two conditions. B: (b), evaluated by persistent enemy loss per 1,000 paid firing energy. | (b), with A's two conditions written in. |
| P2 | Fair replacement for `UnseenCities` (N3). Strict visible-only covers positions; this is a count. | (a) The enemy city count read through population intel when readable, else the last readable count: a number, never a position. (b) Cities ever seen and currently invisible. (c) No count; budget on own visibility results only. | B prefers (c). If (a) is chosen, B requires authorizing aggregate-number memory only, with no hidden live updates or remembered per-city targeting. A: (a) only if the count sizes the intel budget and never feeds targeting or position inference; (c) is cleanest if (a) is contested. | **(c), changed from revision 1's (a).** Both lanes rate (c) cleanest, and the stop-loss already runs on own visibility outcomes. |

## 6. Pavol's weak windows and likely adaptation (lens C, written by both lanes)

**Windows:**
- **T1-T11:** zero defenses and counterintel ×1.0 (A).
- **T12-T21:** zero Ions all match (also in v0.14.1), and his science runs away under Jamming. B's addendum: the practical precision window was narrower, two visible cities at T21-T22.
- **T22-T24:** Data Security Hubs surge to ×26 counterintel, and 20-35 Railguns sit unfired until T25.
- **T30 on:** his planet material is at 0 (A, N2).

**The exploitable weakness (B):** the transition from protected growth to mass armament.

**Likely adaptations:**
- After a T19-T21 precision strike: Ions within two turns (Plasma is one step from Maglev) and earlier Data Security Hubs.
- After early Railgun harassment: early Kinetics and early return fire, the v0.15.1 shape. That cost him about 80% of his T20 energy; it was a different match, so this is not a causal estimate.
- After a visibly strong AI energy curve: an earlier strike, so defense funding must be in place by T19.

**Conclusion (B):** the robust answer is a flexible economy and a target-aware switch, not a hard-coded T19 Missile rush against assumed zero Ions.

## 7. Sign-off request (retry)

Each lane replies `CONFIRM @ <sha256 of this file>` or `OBJECT @ <sha256>: <reason>`.
- CONFIRM means: this record faithfully states the agreements, the facts with their verification state, and the splits with your position in words you accept.
- It does not mean you accept every adopted choice.

## 8. Changes from revision 1

| Location | Change | Source |
|---|---|---|
| Agreement 1 | Queued versus installed occupancy; "first recorded SSTAR purchase" | signoffB §A |
| Agreement 3 | Turn-end production wording; 5,167.5 as nominal, not a ledger | signoffB §A |
| Agreement 4 | A withdraws 2,900-3,600 and accepts about 2,550 | signoffA 3 |
| Agreement 5 | Lane wordings separated; prohibition marked as the facilitator's adoption | signoffB §C |
| Agreement 8 | A's 30-stop reading stated and withdrawn | signoffA 4 |
| Agreement 10 | Fable's REJECT/blocker wording; B's full non-negotiable, including live durability and frozen snapshots; A's non-negotiable added | signoffA 6, signoffB §2 |
| N5 | Records split by turn (T26 1,250 versus T27 9,250); both constraints retained; T28 added | signoffA note 1; facilitator read of `:148-206` |
| N7 | Both lanes withdraw the growth effect; the ceiling is attributed to A and the minimum gate to B | signoffA 2, signoffB §B |
| N8 | A's acceptance moved to sign-off, with its checks | signoffA 5 |
| N9 | New: Lidar per-intel-point price | signoffA note 2 |
| Item 1 | Jamming-ceiling attribution | signoffB §B |
| Item 2 | Marked as a synthesis; B's artillery research moved to S4/P1; EMC head by T10 gate added | signoffB §D, signoffA 7 |
| Item 4 | Prohibition attributed; investment-cost risk added | signoffB §C, §3 |
| Item 5 | "Never binding" removed; Lidar valued on build energy | signoffB §3, signoffA note 2 |
| Item 6 | Forward signal added (not yet reviewed by B) | signoffA note 1 |
| S1-S7 | Lane cells rewritten in each lane's accepted words; status column added | signoffA 1/7, signoffB §E-H |
| P1, P2 | Lane positions added; P2 recommendation changed (a) → (c) | signoffA note 3/4, signoffB §3 |
