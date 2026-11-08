# Council Cross-Review: Lane C Reviews Lane D (Fable 5.1)

## 1. Scope and Frozen Target

- **Frozen Target:** Commit `4d7c160` ("Playtest 26 (v0.19.0): human won the rematch at T44"), read-only.
- **Rules Followed:** No file created, edited, or deleted in this repository. No build or test suite executed. No other lane report (`laneA.md`, `laneB.md`, etc.) read.
- **Files Inspected:** `docs/reviews/2026-11-08-council-v020/brief.md`, `docs/reviews/2026-11-08-council-v020/laneD.md`, `docs/ruleset/TechTreeData.csv`, `BuildingsData.csv`, `UpgradesData.csv`, `WeaponsData.csv`, `AI/Brain/Strategy.cs`, `AI/Brain/Director.cs`, `AI/Brain/Tempo.cs`, `AI/DirectorPatches.cs`, `AI/Patches.cs`, `AI/StrategyPatches.cs`, `AI/Tests/Program.cs`, `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs`, `decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs`, `docs/playtests/2026-11-08-v0.19.0-match25.md`, `docs/playtests/2026-11-08-v0.19.0-match26.md`, and raw data across `v018m22..v019m26` (`*-ai-decisions.jsonl`, `*-human-telemetry.jsonl`, `*-monitor.log`).

---

## 2. Verdict and Confidence

Lane C verdict: **Lane D's report can be trusted on shot waste, unspent energy, and pulse-wait holds, but its central thesis—that damage lands only when the human is broke and that the facilitator's placement roadmap should be inverted—is unviable and rests on demonstrably flawed telemetry analysis.** Specifically: (1) Lane D's claim that damage occurred only when human leftover energy was <1.5k is falsified by an off-by-one turn alignment in match 26 (T24 suffered -635 population damage while leftover energy was 8,113) and cherry-picked omission in match 25 (T32 lost -740 population at 6,454 leftover energy, T34 lost -158 at 3,023 energy); (2) Lane D mistook the per-turn logging field `d.WeaponUpgradesThisTurn` for cumulative upgrades, falsely asserting that Sarai, Ash, and Hawking had 0 upgrades when they actually bought 61, 52, and 20 upgrades; and (3) an early T12-T14 Railgun war without fixing placement on crowded planets or the small-arsenal hold thresholds (`MinWeapons = 6`, `MinPassingShots = 12`) burns 35% of early Eiai income against already-established Kinetic defenses. **Confidence: High**, anchored on exact line-by-line verification in `PlayerDefenseHandler.cs`, `DirectorPatches.cs`, `Strategy.cs`, and exact parsing of all 5 playtest telemetry and decision logs.

---

## 3. Detailed Audit of Load-Bearing Claims

### Claim 1 (O1): Railgun has no tech requirement; buildable from T1 at 200 E / 200 M, 100 E/shot; original AIs built first Railgun at T7-T10
- **Independent Reproduction:** Verified. In `docs/ruleset/TechTreeData.csv`, `bui_Railgun` does not appear in row `building_unlock` (only Missile, Coilcannon, Laser, Nuclear Missile, etc.). `BuildingsData.csv` sets Railgun `build_cost_energy = 200`, `build_cost_material = 200`. `WeaponsData.csv` sets `firing_cost_energy = 100`. In decision logs (`original` events), original level-1 AIs built their first Railguns at: Beastie T7 (m25, 1 gun), Spiral T8 (m23, 1 gun), Armiger T9 (m26, 2 guns), Tarkoy T10 (m24, 2 guns).
- **Alternative Explanation / Caveats:** Native game design grants Railguns at T1 as basic kinetic artillery; starting techs only contain Railgun upgrades.
- **Disposition:** **ACCEPT** (Severity: Minor). Accurate baseline fact.

---

### Claim 2 (O2): Weapons are bought only at war, and war waits for Missile tech (`tec_OrbitalAssembly`) or T20
- **Independent Reproduction:** Verified. `AI/DirectorPatches.cs:875, 902` restricts `TryWeapon` strictly to `if (d.War ...)`. `AI/Brain/Tempo.cs:60-64` (`WarStart.Turn`) returns `warFromTurn` (16) only if `orbitalAssemblyDone && energyIncome >= minIncome (12,000)`, else `Math.Max(warFromTurn, fallbackTurn) = 20`. In m24-m26, Orbital Assembly was never completed by T16, so war started at T20 unless damage-triggered.
- **Alternative Explanation / Caveats:** None; code and logs are unambiguous.
- **Disposition:** **ACCEPT** (Severity: Minor). Correctly identifies the code mechanism delaying offensive builds.

---

### Claim 3 (O3): The war/peace flip is an unlatched damage trigger in `Director.cs` and `DirectorPatches.cs`
- **Independent Reproduction:** Verified on code and numbers. `AI/Brain/Director.cs:271-274` evaluates `WarPhase = turn >= warFromTurn || recentDamage >= damageTrigger (600)`. In `AI/DirectorPatches.cs:213`, `m.WarStartTurn = turn` is latched **only** `if (turn >= warTurn)`. When damage pushes `recentDamage >= 600` before `warTurn`, it is not latched; if damage decays next turn, `WarPhase` reverts to peace.
  - Ash (m26): T17 war (dmg 865), T18 war (dmg 606), T19 peace (dmg 529), T20 war (turn 20).
  - Eddie (m24): T14 war (dmg 608), T15-T16 peace (dmg 426, 418), T17 war (dmg 680), T18-T19 peace (dmg 476, 333), T20 war.
- **Alternative Explanation / Counter-Evidence:** While the flip mechanism is correctly diagnosed, Lane D's causal deduction—*"Cost: Ash's 5 Railguns bought T18 idled under 'peace' (T19) and 'building' (T20-21); Eddie's 6 idled four turns. One-line fix (latch the damage war too)"*—is incomplete.
  - In `AI/Brain/Strategy.cs:438`, `d.Reason = weapons < s.MinWeapons ? "building" : ...`. Because `MinWeapons = 6`, Ash's 5 Railguns at T20 and T21 idled under `reason="building"` **despite `war=true`**!
  - Eddie at T17 (`war=true`) and T20 (`war=true`) held with `reason="below-saturation"` because 6-8 Railguns cannot satisfy `MinPassingShots = 12f` (`Strategy.cs:64`).
  - Latching the damage war alone would not have made Ash or Eddie fire on those turns.
- **Disposition:** **AMEND** (Severity: Major).
  - *Corrected Statement:* Latching `WarStartTurn` on damage prevents reverting to peace in the director, but weapon idling on early damage wars is co-caused by `MinWeapons = 6` and `MinPassingShots = 12` in `Strategy.cs`, which must be relaxed for early retaliation.

---

### Claim 4 (O4): The human had no guns for 2-10 turns after Eiais went to war, and few defenses before T20
- **Independent Reproduction:** Verified via human telemetry (`turn_end` events).
  - First human weapon: m24 T22 (23 Railguns), m26 T24 (13 Railguns), m22 T27 (13 Railguns), m23 T27 (12 Lasers), m25 T30 (17 Nuclear Missiles).
  - Kinetic defenses: m26 T14=7, T16=11, T17=12, T20=15; m25 T14=0, T16=7, T17=8, T20=16; m24 T14=9, T16=14, T20=31, T22=32.
  - Human energyPerTurn at T14-T16 was 1.6k-3.1k vs Eiai 4.0k-4.6k.
- **Alternative Explanation / Counter-Evidence:** The human had 11 to 14 Kinetic defenses as early as T16 without buying weapons, providing substantial intercept coverage against small volleys.
- **Disposition:** **ACCEPT** (Severity: Minor). Telemetry data confirmed.

---

### Claim 5 (O5): Core Causal Thesis — damage landed ONLY when human energy was exhausted (`energyBefore` < 1.5k), independent of shot count or facing
- **Independent Reproduction & Error Identification:** **FALSIFIED.** Lane D claims:
  - *"m26 T22 1,195 and T23 595 -> population -408 and -635 with 21-22 paid shots a turn; T24-T29 leftover 7.7-12.3k -> population +28 to +476 with 16-26 paid shots a turn."*
  - *"m25 T23 166 (pop -55), T30-T37 leftover 18-874 while 39-58 paid shots a turn -> 2,257 to 0"*
  - *"INFERENCE: whether a volley hurts is decided by the human's roll budget that turn, not by the shot count or the guns' facing"*

  **Direct Counter-Evidence from Telemetry:**
  1. **Match 26 Off-By-One Misalignment:**
     In `v019m26-human-telemetry.jsonl`:
     - T22: `energyBefore = 1,195`; `turn_end` population = 3,009 (+115 growth).
     - T23: `energyBefore = 595`; `turn_end` population = 2,601 (**-408** drop from T22).
     - T24: `energyBefore = 8,113`; `turn_end` population = 1,966 (**-635** drop from T23).
     - T25: `energyBefore = 9,062`; `turn_end` population = 1,994 (+28 growth).
     Lane D took T22 (1,195) and T23 (595), paired them with the -408 and -635 drops, and then claimed T24-T29 had leftover 7.7-12.3k with population growth (+28 to +476). **T24 suffered the match's worst damage (-635 population) while entering round start with 8,113 leftover energy!**
  2. **Match 25 Cherry-Picked Energy Window:**
     In `v019m25-human-telemetry.jsonl`:
     - T31: `energyBefore = 184`; population = 2,132 (-125).
     - T32: `energyBefore = 6,454`; population = 1,392 (**-740** catastrophic drop).
     - T33: `energyBefore = 18`; population = 677 (-715).
     - T34: `energyBefore = 3,023`; population = 519 (**-158** drop).
     Lane D asserted that T30-T37 leftover energy was "18-874". In reality, **T32 had 6,454 leftover energy and lost 740 population, and T34 had 3,023 leftover energy and lost 158 population.**
  3. **Circular Causality & Game Mechanics:**
     In `decompiled/PlayerDefenseHandler.cs:231-287`, each defense interception roll checks `m_currentEnergy >= EnergyActivationCost (150)`. High shot volume (saturation) is precisely what drains defense energy and penetrates unrolled defense coverage. Claiming that shot count and facing do not matter because damage correlates with energy exhaustion reverses cause and effect.
- **Disposition:** **REJECT** (Severity: **BLOCKER**). The data directly contradicts the claim; massive damage landed when human leftover energy was 6.4k-8.1k.

---

### Claim 6 (O6): Idle energy at war is 28-57% of income; two binding constraints (cap vs placement)
- **Independent Reproduction:** Verified.
  - War-turn unspent energy share (`sum(energyLeft) / sum(energyPerTurn)`): m22 Eiai 29.7%; m23 Beastie 47.3%, Arc 29.6%; m24 Eddie 37.8%, Doc 23.2%; m25 Zenon 28.4%, Donnels 44.6%, Sarai 34.2%; m26 Ash 42.5%, Hawking 57.4%.
  - Hawking (m26, T22-T26): held 24 Railguns + 6 Missiles = 30 weapons (`MaxWeapons`), leaving 34.3k to 39.6k unspent energy per turn.
  - Ash (m26, T35-T37): held 14-15 Railguns, 135-139 slots left, and abandoned 23 Railgun placements with 32.0k-36.8k unspent energy.
- **Alternative Explanation / Counter-Evidence:** None. Accurately separates the 30-weapon cap bottleneck from the placement abandonment bottleneck.
- **Disposition:** **ACCEPT** (Severity: Minor).

---

### Claim 7 (O7): A third of paid shots did not go at the human (35-42% elsewhere); Eiai-on-Eiai fire caused heavy repairs
- **Independent Reproduction:** Verified to the exact shot and resource unit.
  - `fire-paid.destination`:
    - m25 (995 total): jogi 650 (65.3%), fellow Eiais 146 (14.7%), own/none 176 (17.7%), original AI 23 (2.3%).
    - m26 (479 total): jogi 302 (63.0%), fellow Eiais 53 (11.1%), own/none 82 (17.1%), original AI 42 (8.8%).
    - m23 (318 total): jogi 191 (60.1%), fellow Eiais 55 (17.3%), own/none 60 (18.9%), original AI 12 (3.8%).
    - m24 (166 total): jogi 97 (58.4%), fellow Eiais 16 (9.6%), own/none 28 (16.9%), original AI 25 (15.1%).
  - In m25 T23, Zenon and Sarai flipped commit to Beastie with aim 3/30 and 6/29, firing 17 (Zenon) + 13 (Sarai) = 30 secondary shots into fellow Eiais (Donnels, Sarai, Zenon).
  - Total repairs: m25 Zenon 357 (35.7k E, 54.0k M), Donnels 277, Sarai 106; m26 Ash 338 (33.8k E, 50.1k M), Hawking 119.
  - Code check: `AI/Patches.cs:605` logs `"none"` when trajectory `HitCelestialBody == null` (missed into deep space) and `"own"` when hitting non-enemy or own planet. Both deduct full 100 E firing cost.
- **Alternative Explanation / Counter-Evidence:** Secondary targeting is native game behavior when primary trajectory fails. In a free-for-all match, fellow Eiais are valid game-engine enemies; suppressing fire specifically against fellow Eiais creates an explicit multi-agent alliance.
- **Disposition:** **ACCEPT** (Severity: Minor).

---

### Claim 8 (O8): Pulse waits with unspent energy left
- **Independent Reproduction:** Verified.
  - In m25: 7 pulse-waits with unspent energy (Donnels T31 227, T33 8,828; Sarai T24 2,878, T26 3,068, T28 92; Zenon T24 2,198, T33 6,544). In m26: Ash T34 with 2,599.
  - Code check: `AI/Brain/Strategy.cs:290` explicitly enforces `d.Reason != "below-saturation"` -> returning false for `pulse-wait`. Unit test at `AI/Tests/Program.cs:1706` explicitly checks `!Strategy.LateFreeFire(pw, ...)`.
- **Alternative Explanation / Counter-Evidence:** Energy resets each turn (`PlayerResourceHandler.TurnStartInit`); unspent energy cannot be saved for the next turn. Holding fire during pulse-wait when construction/repairs are finished wastes the energy.
- **Disposition:** **ACCEPT** (Severity: Minor). Clean, verified opportunity.

---

### Claim 9 (O9): Aim is already 72-78%; facing gain is bounded at +22-28%
- **Independent Reproduction:** Verified. Firing-turn `aim-search` found/live ratios: m22 78/103 (75.7%), m23 287/370 (77.6%), m24 168/220 (76.4%), m25 765/1052 (72.7%), m26 478/614 (77.9%). Unfound facing angle averages 91-129 degrees.
- **Alternative Explanation / Counter-Evidence:** While the gain in trajectory availability for *existing* guns is bounded at ~25%, facing-aware placement also dictates the impact angle relative to defense coverage sectors and enables placing *more* guns on crowded planets by selecting target-facing hemispheres rather than random anchors that fail.
- **Disposition:** **ACCEPT** fact, **AMEND** implication (Severity: Minor).

---

### Claim 10 (O10): Weapon upgrades uneven; Sarai, Ash, Hawking, Eddie, Beastie had 0 upgrades
- **Independent Reproduction & Error Identification:** **FALSIFIED.** Lane D claims:
  - *"`director.weaponUpgrades` at the last record: Zenon 6, Donnels 3, Sarai 0, Ash 0, Hawking 0, Doc 6, Arc 6, Eddie 0, Beastie 0... Sarai sat on 26.8k M with 0 upgrades."*
  - **Fatal Code Misreading:** In `AI/DirectorPatches.cs:3106`, the logged field is:
    ```csharp
    .Int("weaponUpgrades", d.WeaponUpgradesThisTurn)
    ```
    This logs **upgrades bought in that specific turn**, NOT cumulative upgrades across the match!
  - **Actual Cumulative Upgrades Across Match:**
    - Match 25: Zenon **68**, Donnels **66**, Sarai **61**!
    - Match 26: Ash **52**, Hawking **20**!
    - Match 24: Doc **51**, Eddie **30**!
    - Match 23: Arc **46**, Beastie **36**!
  - Sarai did not sit on 26.8k M with 0 upgrades; Sarai bought 61 upgrades. Ash bought 52 upgrades. Lane D read the final turn's record (where `WeaponUpgradesThisTurn` was 0) and mistook it for a game total.
- **Disposition:** **REJECT** (Severity: **MAJOR**). Flawed metric interpretation produces a false diagnosis.

---

### Claim 11 (O11): Placement give-ups are mostly plants and defenses, not guns; refunded by code
- **Independent Reproduction:** Verified. Monitor log abandons show NuclearPlant (52 in m25, 43 in m26), SolarPlant (32 in m25, 27 in m26), and IonDefense (22 in m25, 40 in m26) dominate. Railguns were 1 in m25 and 23-30 in m26. In `decompiled/`, placement searches reaching step cap refund full building costs to the player.
- **Disposition:** **ACCEPT** (Severity: Minor).

---

### Claim 12 (O12): Precision is intel-gated and dead; visibleCities = 0 at T30
- **Independent Reproduction:** Verified. In m25/m26, human counterintel multiplier reached x31.7 at T30, resulting in `visibleCities = 0` and `precision = false`. `AI/DirectorPatches.cs:1306-1310` (`BlindArtillery`) halts precision missile purchases when blind.
- **Disposition:** **ACCEPT** (Severity: Minor).

---

## 4. Evaluation of Ranked Recommendations and Exit Gates

### Recommendation 1: Latch the damage-triggered war (the flip)
- **Rank:** #1 in v0.20. **ACCEPT Rank.**
- **Exit Gate:** *"zero `strategy` records with `war=false` after the first `war=true` for every Eiai, two matches."*
- **Exit Gate Evaluation:** **AMEND.** The gate is measurable from logs and can fail. However, it does not check if the AI actually *fires*. As proven in Claim 3, latching war still left Ash (T20-T21) and Eddie (T17, T20) holding fire due to `MinWeapons = 6` and `MinPassingShots = 12`.
- **Fairness:** Fully fair (own damage only).

### Recommendation 2: Start the Railgun war by affordability, not by Missile tech
- **Rank:** #2 in v0.20. **REJECT Rank & AMEND.** (Demote to v0.21 behind placement and weapon cap).
- **Exit Gate Evaluation:** **REJECT Gate 3, AMEND Gate 1/2/4.**
  - Gate 3 (*"human `energyBefore` < 2,000 on >= 60% of turns T15-T24"*) is invalid: in v0.19 m26, human `energyBefore` was ALREADY < 2,000 on 90% of turns T15-T23 purely due to economic expansion, not AI pressure!
  - An early war at T14 commits ~35% of early Eiai income to firing. If guns cannot be placed cleanly or are absorbed by existing Kinetic defenses (human had 11-14 KD at T16), it slows Eiai economy for zero damage penetration.
- **Fairness:** Fully fair (uses T1 unlocked building and own income).

### Recommendation 3: Pay only for shots that can reach the commit
- **Rank:** #3 in v0.20. **ACCEPT Rank for Form A (hold unreachable shots); REJECT Form B (alliance against human).**
- **Exit Gate:** *"`fire-paid` with destination in {jogi, original AI} >= 90%; fellow-Eiai share <= 3%; own/none <= 8%; Eiai repairs down by a third."*
- **Exit Gate Evaluation:** **ACCEPT.** Measurable from `fire-paid.destination`, can fail, and saving 100 E per held shot directly funds construction. Form A strictly respects fairness (no hardcoded team logic).

### Recommendation 4: Late free fire also covers pulse waits
- **Rank:** #4 in v0.20. **ACCEPT Rank.**
- **Exit Gate:** *"zero `strategy` records with `reason=pulse-wait` and `director.energyLeft >= firingCost`."*
- **Exit Gate Evaluation:** **ACCEPT.** Measurable, can fail, zero risk to economy (fires only leftover energy after all builds/repairs finish).

### Recommendation 5: Dynamic weapon cap while energy is idle
- **Rank:** #5 in v0.21. **ACCEPT Rank.**
- **Exit Gate:** *"war-turn unspent share <= 20% per Eiai; `aim-search.found` on firing turns >= 0.72 of live; `aimWaitAfterHuman` mean <= 20 s."*
- **Exit Gate Evaluation:** **ACCEPT.** Measurable, directly addresses Hawking wasting 36-39k energy at 30 weapons.

### Recommendation 6: Weapon placement retry before giving up
- **Rank:** #6 in v0.21. **AMEND Rank.** (Promote to v0.20 or early v0.21).
- **Exit Gate:** *"`abandoned Railgun|Coilcannon|Missile` <= 10 per match; live weapons at T30 >= 25 for every Eiai with income >= 20k."*
- **Exit Gate Evaluation:** **ACCEPT.** Measurable and directly unblocks Ash's 23 abandoned weapon placements.

### Recommendation 7: Facing-aware weapon anchor
- **Rank:** #7 in v0.21. **AMEND Rank.** (Fold together with Rec 6 into weapon placement overhaul).
- **Exit Gate:** *"`aim-search.found/live` on firing turns >= 0.85 and `unfoundFacingDeg` mean < 90."*
- **Exit Gate Evaluation:** **ACCEPT Gate.** Measurable and can fail.

### Recommendation 8: Weapon upgrades for every Eiai with idle material
- **Rank:** #8 in v0.22. **REJECT Rank and Premise.**
- **Exit Gate:** *"every Eiai at war >= 4 `director.weaponUpgrades` by war turn +5; end-of-match `materialLeft` < 10k."*
- **Exit Gate Evaluation:** **REJECT Gate.** Fails on false premise (Claim 10): Eiais already buy 50-68 weapon upgrades. The gate tests `WeaponUpgradesThisTurn >= 4` on a single turn. Lifting material caps on glut is valid upkeep hygiene, but not a deficit fix.

### Recommendation 9: Plant and defense placement give-ups
- **Rank:** #9 in v0.22. **ACCEPT Rank.**
- **Exit Gate:** *"NuclearPlant + SolarPlant abandons <= 20 per match; IonDefense <= 10."*
- **Exit Gate Evaluation:** **ACCEPT.** Measurable from monitor log.

### Recommendation 10: Intel for a Missile mix
- **Rank:** #10 in v0.22+. **ACCEPT Rank.**
- **Exit Gate:** *"measured intel bid can see one city for two turns running."*
- **Exit Gate Evaluation:** **ACCEPT.** Deferral is justified given counterintel x31.

---

## 5. Blockers vs. Non-Blocking Improvements

### Blockers
1. **Blocker 1 — Telemetry Turn-Index Error & Falsified "Damage Only When Broke" Thesis (O5):**
   Lane D's claim that damage occurred only when human leftover energy was <1.5k is mathematically disproven. In m26 T24, -635 population damage landed when leftover energy was 8,113 (shifted off-by-one by Lane D into T23). In m25 T32, -740 population damage landed when leftover energy was 6,454 (omitted from Lane D's reported 18-874 range). This invalidates the premise that shot volume and facing are secondary to defender poverty.
2. **Blocker 2 — Premature Roadmap Inversion (Rec 2 vs. Placement):**
   Reversing the facilitator's roadmap to prioritize an early T12-T14 Railgun war over weapon placement and facing rests on Blocker 1. An early war without fixing placement retry and facing sends small, easily intercepted salvos (10-15 shots vs 11-14 Kinetic defenses) while consuming 35% of early Eiai income.
3. **Blocker 3 — Misreading Per-Turn Upgrade Field as Match Cumulative (O10, Rec 8):**
   Lane D misread `d.WeaponUpgradesThisTurn` as cumulative total upgrades, falsely reporting that Sarai, Ash, and Hawking had 0 upgrades (actual: 61, 52, 20). Rec 8's premise and exit gate are invalid.

### Non-Blocking Improvements
1. **Rec 1 (Damage War Latch):** Must be paired with relaxing `MinWeapons = 6` / `MinPassingShots = 12` during damage retaliation so early arsenals do not idle.
2. **Rec 2 Exit Gate:** Drop Gate 3 (`energyBefore < 2,000`), which is an artifact of natural human expansion.
3. **Rec 3 (Shot Discipline):** Adopt Form A (hold non-commit shots); reject Form B (explicit anti-human collusion).
4. **Rec 4 & Rec 5 (Late Free Fire on Pulse & Dynamic Weapon Cap):** Both are high-quality, verified, ready-to-implement wins for v0.20/v0.21.

---

Lane C verdict: Lane D's report contains excellent forensic accounting of idle energy, wasted shots, and pulse waits, but its central thesis—that damage only lands when the human is broke and that the facilitator's placement roadmap should be inverted—is fatally undermined by off-by-one telemetry alignment, cherry-picked energy windows, and misreading per-turn upgrade counters as cumulative totals.

END OF REPORT
