# Council decision record: Smarter AI v0.16

**Question (charter):** Which changes for Smarter AI v0.16 give the AI the best chance to beat Pavol's usual build-out (match 16 shape), within fair information, human-like accuracy and no cheats?

**Claimed end state:** CONSENSUS RECORD, pending sign-off. Both lanes are asked to confirm that this exact file faithfully records the agreements and the open splits. They are not asked to agree with every line. If a lane objects or fails, the state falls back to ADVISORY REVIEW.

**Facilitator:** Claude Opus 5.5. It owns no load-bearing measurement. Every number below cites the lane that derived it.

## 0. Integrity

| Item | Value |
|---|---|
| Frozen target | `24683684d9799b9b22db765c251ac811ef8793ae`. HEAD is unchanged; the later commits are docs only and are not part of this review. |
| Git porcelain | byte-identical to `status-before.txt` after all four passes (five untracked paths, no tracked diff) |
| Data manifest | 6 of 6 files match `data-manifest.txt` (checked after the cross-reviews) |
| laneB.md | sha256 `a06745b58eae7a94ec679947df21e8f0467c8642f77108cadd2556209d1d430a` |
| laneA.md | sha256 `1facc88e348052bdfc28fcfc35243838ccb756d20b9c67ca4f8faace5353709b` |
| xreviewA.md (Fable on B) | sha256 `dc6ac40e8ea44b307c2314f8ff335373ac92b66262a0199e2ee9cd54a90e94e4`, 15,850 B |
| xreviewB.md (Sol on A) | sha256 `9d7f9f4b0d11188c8a90681f2a7d802921aa04451040eed549169ffd204105e7`, 39,762 B |
| Roster | A = Fable, `claude -p --model fable --effort xhigh`, Anthropic. B = GPT-6.1 Sol, opencode `--variant xhigh`, OpenAI. C = Gemini 4 Argon, recused with Pavol's approval (Amendment 1; no output). |
| Label | CROSS-FAMILY CONCURRENCE, two families. Correlation risks: A and the facilitator are the same family, and both lanes share one packet and one corpus. |

## 1. Agreements (both lanes, after cross-review)

1. **The project-slot policy is the main economic defect.**
   - **What happened:** five Molecular Recycling Plants were queued by T7 and two QEH by T11. Seven permanents filled the seven slots at T14. The first SSTAR came at T31.
   - **Who verified it:** both lanes re-derived the slot unlocks at 250/500/750 population per city.
   - **What is still open:** Sol says "zero free slots on every turn T7-T30" is UNVERIFIED, because installed identities are not logged.
2. **Scoring and forecasting temporary projects has a real bug.** The duration-adjusted utility is fed into projected next-turn income (`DirectorPatches.cs:1058`, `:1267-1288`), and queued global modifiers are not compounded. (A: E1 ACCEPT. B: B-Amendment 2.)
3. **Science tempo belongs in the slot policy.**
   - The human's science ran ×2.44 to ×3.06 at T11-T12 and about ×1.95 at T17-T20.
   - That is about 5.1-5.2k extra science over T11-T20.
   - It is consistent with Jamming, but the identities of the installed projects are inferred, not logged.
4. **The visible-city strike window was real and was forfeited.**
   - **T21:** 9 Missiles, 7.65 expected passing shots at 100% estimated passage.
   - **T22:** 13 shots, 10.007 expected passing.
   - **Targets:** two cities were currently visible on both turns; the AI held both turns below MinPassingShots 12.
   - **T23:** no city visible.
   - **Not five targets:** `target.cities 5` is the population-readability gate, not five targetable cities. A: amendment 1. B: A4 table.
   - **Damage at stake:** the Missile portion was worth about 2,550 nominal population damage. Sol rejects lane A's 2,900-3,600 figure; A did not contest that.
5. **All 45 blind Missile strikes, T24-T28, had zero candidates.** Native code then fires at a random surface point (`AIWeaponTargeting.cs:493-497`). The director must not buy Missiles while there are zero candidates (`TryWeapon` has no targetability term, `DirectorPatches.cs:989-992`).
6. **Firing cap: keep `WarUpkeepShare` 0.4 and `MaxWeapons` 30.** Sol withdrew its latched firing roster and reservation release (B-Amendment 3). Both lanes want three correctness fixes:
   - Base-versus-live firing cost (`DirectorPatches.cs:985`, `:1568`, `:883`).
   - Per-class planned hit quotas, 0.7 unguided and 0.85 guided (`Patches.cs:318-328`).
   - No accuracy change.
7. **Intel:**
   - Replace unconditional escalation with a spend-and-outcome budget and a stop-loss, priced with upkeep.
   - Value the Telescope (Lidar) intel upgrade, which `AddUpgradeOptions` ignores today (`DirectorPatches.cs:1154-1176`): +4 intel, 50 E / 175 M, upkeep 20 → 32 E.
   - No threshold may use the enemy's counterintel. Fable withdrew its own A5 rule on this point.
8. **Defense:**
   - Upgrade the Kinetics. Cost-down needs Maglev, which the AI already had at T15; range needs Early Warning AI, 600 science.
   - Fund the defense rolls: 40 rolls went unpaid at T26 and 3,400 E was spent.
   - Neither lane treats the 40 unpaid events as 40 distinct stoppable impacts.
9. **Ion insurance:**
   - Place one Ion per city, set as a prior and executed early.
   - Research Plasma right after Maglev. In match 16 Plasma came only at T28, so the T24 prior could not execute.
10. **Not in v0.16:**
    - Sol's latched roster and reservation release (Fable called it a blocker because of the cheat risk).
    - Any increase to `MaxWeapons` or any accuracy change.
    - Any memory of city positions after they leave sight.
    - Any input taken from the enemy's counterintel or stealth values.
11. **Gates are experiments, not predictions.** A gate whose opportunity never arises is UNTESTED, not passed.

## 2. New facts surfaced in cross-review

| ID | Fact | Derived by | Facilitator check |
|---|---|---|---|
| N1 | **Research hijack:** five off-plan research heads at T23-T27 (MassBio, Optics, Uranium Alloys, Induction Coils, MagRad) with no mod `research` record. The mod yields to native research whenever the queue is non-empty or native urgent-intel research asks (`EconomyPatches.cs:506-508`). | A | not re-run |
| N2 | The human's planet material hit 0 at T30; the AI's planet held 134k at T32. | A | not re-run |
| N3 | **Fairness leak in shipped code:** `UnseenCities` iterates the enemy's real `GetRemainingCities()` and counts the invisible ones (`DirectorPatches.cs:323-329`). That is the true number of hidden surviving cities. It drives intel escalation (`:575`, `:580`) and upgrade filtering (`:1153`). It has been live since v0.15. | B (blocker F1) | **Confirmed** by reading `DirectorPatches.cs:321-329`. |
| N4 | The game credits no science on T1 (`PlayerResourceHandler.cs:161-185`). Lane A's opening timetable included 279 that never arrives. On the frozen history, the EMC chain first fits at T13. | B (blocker F2) | not re-run |
| N5 | The defense estimator already asked for 9,250 E at T26, the logged `observedNeed`. The binding limit was the money order: 14,725 production − 5,100 firing reservation − 3,600 repairs − the 0.2 recovery floor left a cap of 3,080. | B | not re-run |
| N6 | Under the director's limited-planet policy (`DirectorPatches.cs:1124-1173`), mines get one yield upgrade plus efficiency. So Robotic Mining alone does not double the AI's mines. | B | not re-run |
| N7 | **Jamming's ×0.9 global population multiplier:** Fable treats it as live and caps Jamming at 3 copies; Sol withdrew its growth claim. A search for `TurnYieldPopulation` finds only registration (`BuildingProjectStats.cs:79`), removal (`PlayerStatHolder.cs:60`) and UI display (`CityInfoPopup.cs:402-509`). | A vs B | **UNVERIFIED.** No consumer was found, but a generic lookup was not excluded. Both lanes land on 3 copies anyway, so this does not change the action. |
| N8 | Lane A's "20 upgraded arrays save 3,000 M and 200 E/turn" is wrong: 20 upgraded arrays cost 500 M more and 40 E/turn more upkeep than 30 base arrays, for equal intel (240). | B, accepted by A in its own amendment 3 | arithmetic only |

## 3. Ranked v0.16 list

Each item gives its sources, the adopted wording, the config and code paths, and the next-match gates. Where the lanes differ, the facilitator picked the more evidence-backed position; the alternative is kept in section 4.

### 1. Joint flexible-slot engine: Jamming into SSTAR
**Sources:** A1, A3; B1; I1, I2. **Both rank it first.**

**Adopted wording:** use Sol's I1 text verbatim:
> "Preserve flexible project capacity per city; use an opening ceiling of two permanent purchases as an initial policy, schedule Jamming and SSTAR against actual unlocked/pending capacity, forecast their compounded production and expiry separately from investment utility, and compare temporary/permanent investments over a common horizon."

**Additions:**
- Jamming up to 3 simultaneous copies once EMC is done (both lanes land on 3).
- SSTAR up to one per city once Lossless is done.

**Paths:** `CityForProject`, `TryCityProject`, `AddProjectOptions`, `TryEconomy`, `DirectorPatches.cs:1058` and `:1267-1288`, `Brain/Director.cs`.

**Gates:**
- Log the installed, pending, unlocked, free and reserved slots per city, every turn.
- Zero permanent purchases into reserved capacity.
- At least 3 simultaneous Jammings within two turns of EMC, while at least 1,500 science of useful economy research remains.
- At least 3 simultaneous SSTARs by T20.
- At least 15,000 round-start E production at T20 (A had T18; B's like-phase correction moves it to round-start).

**Main risk (B):** removing the early material projects changes the bank that funds this plan.

### 2. Research order with a hijack guard
**Sources:** A3; B (I4 amended, B6 Plasma); N1, N4, N6.

**Adopted wording:** use Sol's I4 text, plus Fable's guard:
> "Reorder research for an executable economy/science tempo: earlier Super Tensile Materials, EMC, Nanostructuring, Microwave and Lossless; advance Plasma right after Maglev for scheduled Ion cover; advance Early Warning AI for Kinetic range; retain observed-threat counter-research. Use real credits and prerequisite closure, and fund Robotic Mining only when an installable yield-upgrade path is selected. Guard `EconomyPatches.cs:506-508` so every head change is logged with a reason and no off-plan head survives more than one turn, except an observed-threat counter-research head."

**Paths:**
- `Plugin.DefaultResearchPlan`, plus its unchanged-default migration.
- `EconomyPatches.cs:506-508`.

**Gates:**
- **EMC done by T14.** A said T12; B's T1-credit evidence makes T13 the earliest on the frozen history, so T14 is adopted.
- **Plasma done by T18.** A said T17; adopted as B's T18, in line with item 6.
- No research head outside the plan for more than one turn, unless it carries a logged observed-threat reason.

### 3. Energy-to-material balance
**Sources:** A2; B (I3 amended).

**Adopted wording:** use Sol's I3 text:
> "Trial EnergyToMaterialTarget 1.0 in growth and war, retaining the additive live firing requirement, and MaterialGlutRatio 1.5 with the existing minimum stock. Evaluate production and conversion outcomes before introducing a separate higher war coefficient."

**Config:**
- `EnergyToMaterialTarget` 0.6 → 1.0.
- `MaterialGlutRatio` 3 → 1.5.
- `MaterialGlutMin` stays 5,000.

**Gates:**
- At T22, end-of-construction material stock is at most 2 × round-start E production.
- Zero extraction-only purchases while the glut state is active, unless a logged recovery need applies.
- Log the top affordable economy alternatives and why each was rejected.

**Split:** the 2.0 war coefficient and the `energyLeft > 300` gate are S2 in section 4.

### 4. Target-aware offense, the visible-city exception, class-correct accuracy
**Sources:** A4; B2; amended B4; I5, I6, I7.

**Adopted:**
- **Visible-city opportunity exception to `MinPassingShots`** in `Strategy.Decide`. There is no visible-city branch today (`Strategy.cs:246-276`). The exact predicate is split S3.
- **`WarFromTurn` 20 → 16**, gated on technology, funded unexpired income and useful current targets.
- **Targetability term in `TryWeapon`:** no Missile or Nuclear purchase on a turn with zero strike candidates.
- **Correctness fixes:** live-cost accounting at `:985`, `:1568` and `:883`, and per-class hit quotas.
- **Unchanged:** `WarUpkeepShare` 0.4, `MaxWeapons` 30, `MinPassingShots` 12 and accuracy 0.7 / 0.85.

**Gates:**
- Zero below-saturation holds on a turn where the logged visible-target predicate is true.
- No Missile queued on a turn with zero candidates.
- After blindness, at most 10% of paid firing energy goes to zero-candidate precision weapons.
- Zero funding overruns.
- Per-class planned hits within one weapon of their quotas.

**Split:** the blind-phase weapon posture is S4, reserved for Pavol.

### 5. Bounded intel acquisition, all-in priced
**Sources:** B3; A5 (amended); I8; N3; N8.

**Adopted wording:** use Sol's I8 text:
> "Use one budget for intel buildings and upgrades, including recurring upkeep, timed to useful prospective precision fire. Escalate only after measuring permitted current visibility outcomes; suspend after two unsuccessful marginal batches and allow bounded probes. Do not use hidden counterintel, hidden city stealth or the true hidden surviving-city count."

**Additions:**
- Value the Lidar upgrade in `AddUpgradeOptions`.
- **Replace `UnseenCities` (N3) with a permitted count.** Which count is choice P2.

**Gates:**
- Through T20, intel construction and upgrade energy is at most 10% of cumulative own energy credits; upkeep is logged separately.
- After two unsuccessful batches, a stop-loss is logged and no more than 2 further arrays are bought through T25.
- Runtime decision inputs contain no hidden counterintel, stealth or surviving-city values.

**Split:** the array ceiling and the T8 opening batch are S5.

### 6. Defense: Kinetic upgrades, protected funding, executable Ion insurance
**Sources:** A6; B5; B6; I9; I10; N5.

**Adopted wording:** use Sol's I9 text:
> "Combine powered Kinetic range/cost upgrades with a funded defense allocation protected before discretionary repairs. Retain the existing observed-demand estimator, improve distinct-impact/live-cost diagnostics, and evaluate reserve caps together with recovery and firing commitments."

**Config:**
- `DefenseReserveShare` 0.3 → 0.4. Both lanes agree; Sol notes it does nothing on its own without the reordering.
- Kinetic cost-down now; range once Early Warning AI is researched.

**Ion insurance:**
- `AntiNukePriorTurn` 24 → 18, one per city.
- Reserve feasible cover sites before ordinary construction blocks them.
- One terminal log result per search ID.

**Gates:**
- At least 10 Kinetics upgraded by T22 (A's number). B's "at least 20 powered and range-upgraded by T24" is the stretch gate.
- In the first qualifying volley of 60 or more impacts: at least 50% intercepted and zero unpaid rolls.
- Every reserve shortfall carries a logged allocation reason.
- Minimum operational city Ion cover of at least 1 by T20.

**Split:** the reserve-floor formula is S6.

### Overall outcome gate (B, unopposed)
In the next usual build-out match, the AI must:
- survive the first qualifying mass-volley phase without losing more than one city;
- show persistent enemy losses from its earlier strikes, not only nominal arrivals.

Human telemetry is used only for the postgame evaluation and never at runtime.

## 4. Open splits (each steelmanned)

| ID | Issue | Lane A's position | Lane B's position | Type | Proposed handling |
|---|---|---|---|---|---|
| S1 | EMC timing | EMC head by T10 and done by T12, from economy-first cumulative science | done by T14: no T1 credit, so 3,850 science first fits at T13 on the frozen history | Factual | B's date is adopted as the gate. A may object if the new opening's extra Jamming science changes the arithmetic. |
| S2 | War E:M ratio and its gate | 1.0 in growth and 2.0 at war, glut at 1.5; gate `energyLeft > 300` on 3 or more turns from T14 to T22 | `WantedEnergy` already adds firing upkeep, so 2.0 at war works out to about 3.33 effective; `energyLeft > 300` rewards unspent energy | Factual (the formula) plus inferential | Adopted: 1.0 in both phases; 2.0 deferred to v0.17 evidence; `energyLeft` dropped as a gate. |
| S3 | Visible-city exception predicate | fire when at least one city is visible, guided passing is at least 6 and pass share is at least 0.9 | bypass only for a credible-kill rule on current visible targets, never for passage probability alone | Inferential | **Proposed: both must hold.** That means A's numeric floor and a logged expected-damage-on-visible-targets predicate, with the threshold fixed during implementation and checked against the T21-T22 replay. |
| S4 | Blind-phase posture | upgraded Railgun artillery only as a fallback, gated on measured enemy loss per 1,000 E: its dispersion is UNVERIFIED and 45 blind Missiles showed no measurable loss | upgraded unguided artillery (Triple Barrel + Airburst) is the executable branch once cities go dark; advance Ultracapacitors and Composite Heat Cells; gate 60 or more live projectiles per blind salvo by T24 | Strategic and value | **Reserved for Pavol (P1).** |
| S5 | Intel quantity | `MaxIntelBuildings` 30 → 21; 6 arrays by T8; `unseenCities` = 0 on T19 | a T8 sighting is worthless under strict visible-only; time the bid to the arsenal; no fixed ceiling; spend share at most 10% | Inferential | Adopted: B's timing and budget. A's 21 is kept as a hard cap that is never binding inside B's budget. A's `unseenCities` gate is dropped, because N3 makes that field unfair until it is replaced. |
| S6 | Defense reserve floor | floor = 100 E × in-range impacts × 1.25 when rolls went unpaid | the existing estimator already asks for 9,250; the binding problem is the order of firing, repairs and recovery | Factual (N5) | Adopted: B's reordering. A's floor is redundant with `observedNeed` as logged. |
| S7 | QEH rule | no QEH until an enemy intel building is seen | the human had 77 intel at T20 with no Telescope, because city projects give intel; absence of a Telescope does not mean absence of intel | Factual (H16:123) | Adopted: B's correction. Opening permanents are capped at 2 by count, with no Telescope-sighting rule. |

## 5. Choices reserved for Pavol

| ID | Choice | Options | Facilitator recommendation |
|---|---|---|---|
| P0 | Build v0.16 | Build the six items now, build a subset, or no action | Build all six. Items 1-3 are the economy and carry the most evidence. |
| P1 | Blind-phase posture (S4) | (a) Artillery as a gated fallback (A). (b) Artillery as the primary blind branch, with research advanced (B). | (b) Artillery primary, with A's loss-per-1,000-E metric logged so v0.17 can check it. Reason: the alternative in match 16 was 45 wasted Missiles and an idle bank. Confidence: Medium. |
| P2 | Fair replacement for `UnseenCities` (N3). Strict visible-only covers positions; this is a count. | (a) The enemy's city count as read through population intel when readable, else the last readable count. This remembers a number, never a position. (b) Cities ever seen and currently invisible. (c) Drop the count; budget on own visibility results only. | (a). A human player can read "5 cities" from the population panel and remember it. Confidence: Medium. |

## 6. Pavol's weak windows and likely adaptation (lens C, written by both lanes)

**Windows:**
- **T1-T11:** zero defenses and counterintel ×1.0 (A).
- **T12-T21:** zero Ions all match (also in v0.14.1), and his science runs away under Jamming.
  - Sol's addendum: the practical precision window is narrower, two visible cities at T21-T22 (B).
- **T22-T24:** Data Security Hubs surge to ×26 counterintel, and 20-35 Railguns are unfired until T25.
- **T30 on:** his planet material is at 0 (A, N2).

**What both lanes see as the weakness:** the transition from protected growth to mass armament (B).

**Likely adaptations:**
- After a T19-T21 precision strike: Ions within two turns (Plasma is one step from Maglev) and earlier Data Security Hubs.
- After early Railgun harassment: early Kinetics and early return fire, the v0.15.1 shape. That cost him about 80% of his T20 energy, a different match and not a causal estimate.
- After a visibly strong AI energy curve: an earlier strike, so defense funding must be in place by T19.

**Lane B's conclusion:** the robust answer is a flexible economy plus a target-aware switch, not a hard-coded T19 Missile rush against assumed zero Ions.

## 7. Sign-off request

Each lane replies with one line, `CONFIRM @ <sha256 of this file>` or `OBJECT @ <sha256>: <reason>`.
- CONFIRM means: this record faithfully states the agreements, the facts with their verification state, and the splits with your position in words you accept.
- It does not mean you accept every adopted choice; your dissent stays recorded in section 4.
