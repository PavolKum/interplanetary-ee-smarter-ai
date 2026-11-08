# Council Report: Lane C (Gemini 3.8 Flash) — Strategy, Balance, and Experiment Design

### 1. Scope and Frozen Target
- **Frozen Commit:** `4d7c160` (branch HEAD, verified via `git rev-parse --short HEAD`).
- **Files Inspected:**
  - Core Council & Context: `docs/reviews/2026-11-08-council-v020/brief.md`, `charter.md`, `laneC.prompt.txt`, `evidence-manifest.tsv`, `docs/PROGRESS.md`, `CLAUDE.md`.
  - Playtest Logs & Summaries: `docs/playtests/2026-11-07-v0.18.0-match22.md`, `docs/playtests/2026-11-07-v0.18.0-match23.md`, `docs/playtests/2026-11-08-v0.18.0-match24.md`, `docs/playtests/2026-11-08-v0.19.0-match25.md`, `docs/playtests/2026-11-08-v0.19.0-match26.md`.
  - Prior Reviews & Dispositions: `docs/reviews/terra-ai-v0.19-disposition.md`.
  - C# Mod Subsystems: `AI/Brain/Strategy.cs`, `AI/Brain/Director.cs`, `AI/Brain/TargetPlanner.cs`, `AI/Brain/Coverage.cs`, `AI/Brain/Tempo.cs`, `AI/Brain/EconomyPlan.cs`, `AI/Brain/Offense.cs`, `AI/StrategyPatches.cs`, `AI/DirectorPatches.cs`, `AI/AimWatch.cs`, `AI/Plugin.cs`.
  - Analysis Scripts & Decompiled Game Code: `AI/tools/summarize_decisions.py`, `AI/tools/human_research_order.py`, `decompiled/Interplanetary.AI.Construction/AIBuildingPlacementSearch.cs`, `decompiled/Interplanetary.Prefabs/CelestialBody.cs`.
  - Telemetry & Decision Artifacts (read-only Python aggregations): `docs/playtests/data/v018m22-*`, `v018m23-*`, `v018m24-*`, `v019m25-*`, `v019m26-*`.

---

### 2. Verdict and Confidence
**Lane C verdict:** High confidence. The primary strategic bottleneck preventing the Smarter AI from beating Pavol's standard play is **not** economic capacity (AIs generated 48k–52k energy/turn in Matches 25 and 26, outproducing the human peak of 46.5k), but rather **artillery throughput and target coordination**. Because defense rolls in *Interplanetary* cost energy (~150 energy/roll), a skilled human's "fortress" (30–40 defenses) can effortlessly intercept small or sequential salvos (12–20 incoming shots/turn costing ~2,000–3,000 defense energy), but completely collapses when saturated by 85–90+ simultaneous shots/turn (exhausting defender energy, as observed in Match 25). The AI currently fails to achieve saturation against standard play because: (1) target commitment rules let low human early-game threat scores divert fire onto original or allied AIs (Match 26: Ash split fire across Armiger and Hawking T22–T25 while Hawking attacked alone); (2) a hardcoded `MaxWeapons` limit of 30, coupled with severe placement congestion (Ash suffered 207 placement abandonments in Match 26), forces AIs to hoard 33k–39k idle energy per turn instead of building a 40–50 gun arsenal; and (3) unaimed weapons face 99°–120° away on the planet's back hemisphere. Fixing coordination and placement throughput in v0.20, followed by dynamic weapon caps in v0.21, provides a mathematically proven path to defeating standard human play within strict fairness rules.

---

### 3. Observations with Evidence

1. **[FACT] Energy generation exceeds the human peak, but 60%–75% sits unspent late game.**
   - *Evidence:* In Match 25, peak AI energy generation reached 52.0k (Zenon, T34), 48.9k (Donnels, T34), and 41.4k (Sarai, T30) vs. human 20.5k (T29) (`docs/playtests/2026-11-08-v0.19.0-match25.md:18-19`). In Match 26, Ash peaked at 52.1k (T35) and Hawking at 50.4k (T22) vs. human 46.5k (T40) (`docs/playtests/2026-11-08-v0.19.0-match26.md:12`).
   - *Evidence (Denominator):* In Match 26, Hawking held 36k–39k energy idle per turn at the weapon cap (`docs/playtests/2026-11-08-v0.19.0-match26.md:19`). Ash held 33k–37k unspent energy and 25k–38k materials from T33 to T39 (`docs/playtests/2026-11-08-v0.19.0-match26.md:53-54`).
   - *Code Reference:* `AI/Plugin.cs:482` binds `MaxWeapons` to a static 30 (`Config.Bind("Director", "MaxWeapons", 30)`). In `AI/DirectorPatches.cs:1292,1316`, `atCap = Plugin.MaxWeapons.Value > 0 && d.AimedWeapons >= Plugin.MaxWeapons.Value` causes `TryWeapon` to exit immediately, blocking further weapon construction despite massive energy surpluses.

2. **[FACT] Saturation threshold determines human defeat vs. total human recovery.**
   - *Evidence:* In Match 25, three Smarter AIs focused the human simultaneously (774 planned hits across 3 AIs; 85–90 shots arriving per turn). The human's defense energy broke ("it just takes more energy to defend than I have"), collapsing human population from 2,589 (T20) to 266 (T37) and resulting in an AI victory at T38 (`docs/playtests/2026-11-08-v0.19.0-match25.md:30-45`).
   - *Evidence:* In Match 22, Eiai fired alone against Pavol's 40 defenses: 6 volleys landed 81 hits across 38 turns (~13.5 hits/volley), destroying **0 buildings** and causing only an 80M temporary population dip (`docs/playtests/2026-11-07-v0.18.0-match22.md:18,31`).
   - *Evidence:* In Match 26, Hawking fired alone on T22–T29 (174 planned hits across 13 volleys = ~13.4 hits/turn), reducing human population from 3,009 to 1,966 (`docs/playtests/2026-11-08-v0.19.0-match26.md:20-21`). Once Hawking ran out of steam and Ash failed to join, Pavol fully recovered to 5,000 population and destroyed both AIs (`docs/playtests/2026-11-08-v0.19.0-match26.md:21-28`).

3. **[FACT] Target selection rules caused catastrophic fire splitting in Match 26.**
   - *Evidence:* In Match 26, Ash committed to Armiger (original AI) on T22 and T24 (`commit: Armiger`), and to Hawking (fellow Smarter AI) on T25 (`commit: Hawking`), failing to target the human until T26 (`docs/playtests/data/v019m26-ai-decisions.jsonl`, T22, T24, T25 strategy records). Ash delivered only 358 hits on the human all match compared to 774 hits in Match 25 (`docs/playtests/2026-11-08-v0.19.0-match26.md:43`).
   - *Code Reference:* In `AI/Brain/Strategy.cs:313-323`, `HumanFirstHolds` requires `human.Score >= s.HumanFirstShare * maxScore` (0.90) or `human.Score >= s.HumanFirstReachMinScore * maxScore` (0.35).
   - *Data Denominator:* On T22 of Match 26, `scoreBy` was `{Armiger: 454, Hawking: 1102, jogi: 240}`. Jogi's ratio to max score was `240 / 1102 = 0.217` (< 0.35). On T25, `scoreBy` was `{Armiger: 235, Hawking: 1450, jogi: 357}`, ratio `357 / 1450 = 0.246` (< 0.35). Because Pavol played quiet economy without early aggressive firing, his threat score remained below 0.35 of Hawking's score. `HumanFirstHolds` evaluated to `false`, allowing Ash to target Armiger and Hawking instead.

4. **[FACT] Placement search failures abandon construction on crowded planets.**
   - *Evidence:* In Match 26, placement gave up 230 times across the lobby, 207 of them on Ash's planet alone (`docs/playtests/2026-11-08-v0.19.0-match26.md:50-52`). Ash's failures included 38 Nuclear Plants, 35 Ion Defenses, 30 Railguns, 25 Kinetic Defenses, and 16 Solar Plants (`docs/playtests/data/v019m26-ai-decisions.jsonl`).
   - *Code Reference:* In `AI/DirectorPatches.cs:2848-2850`, `ShelterStepUnsafe` restricts anchor selection to `covered.Count > 0 && n % 3 != 2 ? covered : anchors`. As the city center fills, candidates generated within line-limit distance hit existing structures, failing `IsBuildingPlacementValid`. After `2 * coveredSteps + 2 = 14` steps (`AI/DirectorPatches.cs:2800`), it falls back to native search, which loops on available power slots (`AIBuildingPlacementFinder.cs:195-244`). Once steps hit `PlacementStepCap` (1500, `AI/Plugin.cs:647`), `FinderUpdatePrefix` abandons the search and refunds the building (`AI/DirectorPatches.cs:2985-2994`).

5. **[FACT] Unaimed weapons face 99° to 120° away on average across all playtests.**
   - *Evidence:* Across matches 22–26, `aim-search` decision logs show unfound weapons facing away:
     - Match 22: mean 120° away (19 samples) (`docs/playtests/2026-11-07-v0.18.0-match22.md:38`).
     - Match 23: mean 108° away (29 samples) (`docs/playtests/2026-11-07-v0.18.0-match23.md:29`).
     - Match 24: mean 111° away (29 samples) (`docs/playtests/2026-11-08-v0.18.0-match24.md:38`).
     - Match 25: mean 99° away (51 samples) (`docs/playtests/2026-11-08-v0.19.0-match25.md:66`).
     - Match 26: mean 105° away (34 samples) (`docs/playtests/2026-11-08-v0.19.0-match26.md:55`).
   - *Code Reference:* In `AI/AimWatch.cs:233-236`, the angle is computed as `Vector2.Angle(normal, toTarget)` in the orbital X-Z plane. Angles > 90° place weapons on the rear hemisphere facing away from the target planet. `AI/DirectorPatches.cs:2856` chooses placement angles via pure uniform random `(float)(Plugin.Rng.NextDouble() * Math.PI * 2.0)`, ignoring orbital orientation.

6. **[FACT] Damage-triggered war flips back to peacetime posture without latching.**
   - *Evidence:* In Match 24, Eddie entered war at T14, flipped to `peace` on T15 and T16, re-entered war at T17, and flipped to `peace` again on T18 and T19 (`docs/playtests/data/v018m24-ai-decisions.jsonl`). In Match 26, Ash entered war at T17 and T18, flipped back to `peace` on T19, and returned to war at T20 (`docs/playtests/data/v019m26-ai-decisions.jsonl`).
   - *Code Reference:* In `AI/Brain/Director.cs:271-274`, `WarPhase(turn, warFromTurn, recentDamage, damageTrigger)` returns `turn >= warFromTurn || recentDamage >= damageTrigger`. In `AI/DirectorPatches.cs:207-215`, `WarTurnFor` only latches `m.WarStartTurn = turn` when `turn >= warTurn`. If war is triggered early by `recentDamage >= damageTrigger`, `m.WarStartTurn` is never updated. When damage decays below the threshold, `WarPhase` reverts to `false`, discarding target aim and halting weapon builds.

7. **[INFERENCE] N=1 playtest iterations conflate mod changes with match-to-match orbital noise and human play variation.**
   - *Evidence:* In Match 25, three variables were altered simultaneously: 3 Smarter AIs were fielded (first time in project history), mod version was upgraded to v0.19.0, and the human played a self-crippling experimental nukes-only offense (`docs/playtests/2026-11-08-v0.19.0-match25.md:6-9`). Match 26 immediately reverted to 2 Smarter AIs and standard human play (`docs/playtests/2026-11-08-v0.19.0-match26.md:38-46`). The playtest note explicitly recorded: *"The two results do not isolate the AI's strength. Lobby size and the human's strategy changed together"* (`docs/playtests/2026-11-08-v0.19.0-match26.md:46`).

8. **[JUDGMENT] Early war before T16–T18 is strategically counterproductive.**
   - *Evidence:* The facilitator noted that "war starts late (T17–T20) because weapon tech comes late". However, historical data from v0.5–v0.9 showed that declaring war at T15 consumed ~60% of turn energy on premature firing and defense reservations, stifling economic growth (`docs/PROGRESS.md:27,51`). Moving Reactor Trials earlier in v0.10 recovered the economic lead (67k vs 23k energy at T30) (`docs/PROGRESS.md:28,54`). Delaying weapon queueing until Orbital Assembly finishes at T18–T20 is the exact mechanism that allowed the AI to outproduce the human in Matches 23, 25, and 26.

---

### 4. Risks, Ranked

1. **Premature War Trap (Strategic Regression):**
   - Forcing weapon tech or war start earlier (e.g., T12–T14) will drain energy from economic expansion (plants, mines, yield upgrades) into low-yield, easily intercepted salvos, destroying the 50k energy lead that makes late-game saturation possible.
2. **Friendly-Fire & Vendetta Cycling in Multi-AI Lobbies:**
   - Over-penalizing or misclassifying incoming fire from other AIs causes return-fire cascades (as seen in Match 26 when Ash targeted Hawking on T25 after taking stray hits).
3. **Overcrowding & Total Placement Deadlock:**
   - If placement algorithms attempt to force all weapons and power plants onto the forward hemisphere without expanding anchor connectivity, the search will instantly exhaust candidate limits, worsening placement abandonment.
4. **False Confidence from Off-Meta Playtest Wins:**
   - Evaluating AI balance against non-standard human builds (such as the nukes-only experiment in Match 25) generates false positives, obscuring severe flaws against standard human fortress play.
5. **Fairness Violations via Omniscient Orbital Geometry:**
   - Giving the AI predictive target facing that tracks enemy planet coordinates through fog-of-war or uses hidden enemy values violates core fairness constraints.

---

### 5. Recommendations, Ranked

#### Recommendation 1: Fix Target Commitment & Multi-AI Focus Fire
- **Evidence it answers:** Match 26 Ash split fire onto Armiger (T22, T24) and Hawking (T25) because Pavol's threat score ratio was 0.217–0.246, falling below `HumanFirstReachMinScore = 0.35f` (`docs/playtests/data/v019m26-ai-decisions.jsonl`).
- **Mechanism:**
  1. Lower `HumanFirstReachMinScore` in `StrategySettings` from 0.35 to 0.15, ensuring a quiet, economically booming human is not abandoned as a target when reachable.
  2. In multi-AI lobbies, suppress AI-on-AI return fire against fellow Smarter AIs (`Plugin.IsSmart`) during the opening war turns (T20–T26) unless no viable trajectory to the leading human/game leader exists.
- **Files/subsystems touched:** `AI/Brain/Strategy.cs:313-323`, `AI/StrategyPatches.cs:386-394`, `AI/Brain/TargetPlanner.cs:56-88`.
- **Falsifiable exit gate:** In a 2-Smarter-AI lobby, >= 85% of primary trajectories across both AIs commit to the designated leader/human on turns T21–T28. Fire splitting onto secondary AIs drops to 0 on turns where trajectories to the primary target exist.
- **Fairness check:** Strictly visible. Uses only visible planet existence, known city counts, and observed incoming hits.
- **Effort:** **S** (Small).

#### Recommendation 2: Latch Damage-Triggered War Phase (War/Peace Flip Fix)
- **Evidence it answers:** Match 24 Eddie flipped war->peace->war->peace on T14–T19; Match 26 Ash flipped war->peace on T18–T19 (`docs/playtests/data/v018m24-ai-decisions.jsonl`, `v019m26-ai-decisions.jsonl`).
- **Mechanism:** In `DirectorPatches.WarTurnFor` and `DirectorPatches.BeginTurn`, latch `m.WarStartTurn = turn` whenever `recentDamage >= damageTrigger`. Once war status is established, it must remain strictly monotonic (`war = true` for all subsequent turns).
- **Files/subsystems touched:** `AI/DirectorPatches.cs:207-215,391-392`, `AI/Brain/Director.cs:271-274`.
- **Falsifiable exit gate:** Zero instances in `decisions-*.jsonl` where `e == "strategy"` records `war: false` or `reason: "peace"` on turn `t > t_war_first`.
- **Fairness check:** Complies with rules; purely fixes an internal state latch.
- **Effort:** **S** (Small).

#### Recommendation 3: Target-Biased Weapon Placement Facing
- **Evidence it answers:** Unaimed weapons faced 99°–120° away on average across matches 22–26 (`AimWatch` data); 20%–30% of weapons cannot find trajectories due to planet-body occlusion.
- **Mechanism:** When placing buildings with `data.weapon_functionality.IsValueSet`:
  1. Calculate the vector to the committed target planet (or the solar system center as default) in the orbital X-Z plane.
  2. Bias candidate placement angles in `ShelterStepUnsafe` within ±75° of the forward hemisphere facing the target vector, rather than `[0, 2*PI]` uniform random.
  3. Allow fallback to full 360° only after 8 failed forward steps.
- **Files/subsystems touched:** `AI/DirectorPatches.cs:2847-2865`, `AI/Brain/Coverage.cs:323-388`.
- **Falsifiable exit gate:** In `aim-search` records, mean `unfoundFacingDeg` drops below 75° (from 105°–120°), and primary trajectory finding rate on firing turns increases from 71%–76% to >= 88%.
- **Fairness check:** Enemy planet positions are fully visible on the celestial map (intel/fog-of-war applies to surface buildings, not planet orbital positions).
- **Effort:** **M** (Medium).

#### Recommendation 4: Relieve Planet Placement Congestion & Expand Power Connectivity
- **Evidence it answers:** Ash suffered 207 placement abandonments in Match 26, failing 38 Nuclear Plants, 35 Ion Defenses, and 30 Railguns (`docs/playtests/2026-11-08-v0.19.0-match26.md:50`).
- **Mechanism:**
  1. In `ShelterStepUnsafe`, when `steps > coveredSteps`, widen candidate sampling distance beyond `lineLimit * 0.9f` to the maximum line connector reach.
  2. For power plants (`!m_needsPowerGrid`), remove the constraint requiring placement adjacent to existing covered anchors; allow expansion outward along available terrain to establish new power nodes.
  3. In `AntiNukeStep`, if `nearAnchor == null`, attempt placement adjacent to the nearest power-distributing node even if outside the primary city cluster.
- **Files/subsystems touched:** `AI/DirectorPatches.cs:2577-2625,2800-2860`.
- **Falsifiable exit gate:** Total placement failures in `director` logs drop from > 200 per match to < 20 per AI; idle unspent energy at T30+ drops below 15k.
- **Fairness check:** Internal building placement logic only.
- **Effort:** **M** (Medium).

#### Recommendation 5 (v0.21): Dynamic Weapon Cap Scaled by Surplus Energy
- **Evidence it answers:** In Match 26, Hawking and Ash held 33k–39k idle energy per turn at the 30-weapon cap (`docs/playtests/2026-11-08-v0.19.0-match26.md:19,53-54`). Pavol built 52 weapons in Match 22 (`docs/playtests/2026-11-07-v0.18.0-match22.md:23`).
- **Mechanism:** Replace static `MaxWeapons = 30` with a dynamic cap: `EffectiveMaxWeapons = Math.Max(30, Math.Min(60, 30 + (EnergyIncome - 25000) / 1000))`. As energy expands past 25k, the AI continues building railguns and lasers up to 60 weapons, matching human late-game arsenal sizes.
- **Files/subsystems touched:** `AI/DirectorPatches.cs:1292,1337`, `AI/Brain/Director.cs:PickWeapon`.
- **Falsifiable exit gate:** AIs with > 40k energy income build and maintain >= 40 active weapons; unspent energy at T35 drops below 10,000.
- **Fairness check:** Standard resource conversion, no cheats.
- **Effort:** **M** (Medium).

#### Recommendation 6 (v0.22): Coordinated Superweapon & Multi-Salvo Timing
- **Evidence it answers:** In Match 25, 4 nuclear missiles were queued individually (T26, T31, T34) without synchronized artillery fire (`docs/playtests/2026-11-08-v0.19.0-match25.md:49-50`).
- **Mechanism:** Synchronize superweapon launches so they arrive in the same action phase as a maximum-size conventional artillery pulse, forcing defense rolls against conventional fire first and exhausting defender energy before the nuclear missile arrives.
- **Files/subsystems touched:** `AI/Brain/Strategy.cs:395-435`, `AI/Brain/Offense.cs`.
- **Falsifiable exit gate:** Superweapons launch exclusively on scheduled saturated pulse turns; defense interception rate of nuclear missiles against players with low defense energy drops by >= 40%.
- **Fairness check:** Pure timing coordination.
- **Effort:** **L** (Large).

---

### 6. The Strongest Reason I May Be Wrong
The strongest reason this strategic analysis could be wrong is that **planetary rotational velocity and multi-body orbital mechanics may render fixed-azimuth weapon placement ineffective**. In *Interplanetary*, celestial bodies rotate on their axes and follow elliptical orbits. A weapon placed facing the target planet at T15 may rotate away by T22, or find its line-of-fire blocked by the Sun or an asteroid belt. If planetary rotation completes a cycle every 4–8 turns, directional placement clustering could create severe "blind-phase" turns where 100% of an AI's weapons rotate behind the planet simultaneously, causing extreme multi-turn fire stalls. If this dynamic holds, the optimal counter-strategy is not directional clustering, but rather an **equatorial ring distribution** coupled with dynamic multi-target switching.

---

### 7. Reproduction Commands and Unknowns

#### Reproduction Commands
To re-derive the observations and metrics cited in this report:

```bash
# 1. Summarize decision logs and verify weapon assignments and placement failures
python AI/tools/summarize_decisions.py docs/playtests/data/v019m25-ai-decisions.jsonl
python AI/tools/summarize_decisions.py docs/playtests/data/v019m26-ai-decisions.jsonl

# 2. Extract war start turns and detect post-war peace flips across all playtests
python -c "
import json
for m, path in [('M22','docs/playtests/data/v018m22-ai-decisions.jsonl'),
                ('M24','docs/playtests/data/v018m24-ai-decisions.jsonl'),
                ('M25','docs/playtests/data/v019m25-ai-decisions.jsonl'),
                ('M26','docs/playtests/data/v019m26-ai-decisions.jsonl')]:
    with open(path, 'r', encoding='utf-8') as f:
        rows = [json.loads(l) for l in f if l.strip()]
    war_t = {}
    flips = []
    for r in rows:
        ai, t, e = r.get('ai'), r.get('t'), r.get('e')
        if e == 'director' and r.get('war') and ai not in war_t: war_t[ai] = t
        if e == 'strategy' and r.get('reason') == 'peace' and ai in war_t and t > war_t[ai]:
            flips.append((ai, t))
    print(m, 'War starts:', war_t, '| Peace flips:', flips)
"

# 3. Extract exact target scores and commit choices showing fire splitting in Match 26
python -c "
import json
with open('docs/playtests/data/v019m26-ai-decisions.jsonl', 'r', encoding='utf-8') as f:
    for l in f:
        r = json.loads(l)
        if r.get('ai') == 'Ash' and r.get('e') == 'strategy' and r.get('t') in [22, 24, 25]:
            print(f'T{r[\"t\"]}: commit={r.get(\"commit\")}, humanFirst={r.get(\"humanFirst\")}, scoreBy={r.get(\"scoreBy\")}, passingBy={r.get(\"passingBy\")}')
"
```

#### Unknowns
1. **Exact Rotational Period per Planet:** The exact degrees-per-turn axial rotation rate for standard map generation is not explicitly defined in the mod configs (governed by native `CelestialBody` physics in `decompiled/`).
2. **Defensive Energy Drain Priority:** When a player's energy is insufficient to pay for all defense rolls in a turn, the exact game-engine tie-breaking mechanism (whether rolls fail randomly, by building index, or by projectile proximity) is unmeasured.
3. **True Ground-Truth Damage of Match 25 Nukes:** Match 25 telemetry confirms 4 nuclear missiles queued by AIs, but projectile detonation logs do not isolate how much population damage was inflicted by nukes vs. conventional railgun penetration.

---

### 8. Experiment Design & Rigorous Test Protocol

To avoid the methodological flaws of Matches 21–26 (where lobby size, human strategy, and mod versions were confounded simultaneously), future testing must adhere to the following protocol:

#### 1. Lobby Configuration Matrix
Testing must be partitioned into three standardized tiers:
- **Tier 1 (Solo Saturation Benchmark):** 1 Smarter AI (Impossible) + 2 Original AIs (Level 1) + 1 Human.
  *Purpose:* Isolates solo economic scaling, weapon cap throughput, and placement facing efficiency without multi-agent coordination noise.
- **Tier 2 (Standard Competitive Benchmark — Primary):** 2 Smarter AIs (Impossible) + 1 Original AI (Level 1) + 1 Human.
  *Purpose:* Tests anti-split fire, target commitment, and combined salvo saturation against standard human play.
- **Tier 3 (Multi-Agent Pressure Test):** 3 Smarter AIs (Impossible) + 1 Human.
  *Purpose:* Verifies complete defense exhaustion and multi-AI focus fire.

#### 2. Sample Size & Statistical Validity (The N=1 Trap)
- **The Problem:** In *Interplanetary*, celestial orbits, starting orbital distances, and axial rotation angles are procedurally randomized each match. In an N=1 trial, an AI can appear artificially incompetent because its planet faced away from the enemy during the war window, or artificially brilliant because orbital paths aligned favorably.
- **Requirement:** A version must **never** be accepted or rejected based on a single match. Every version release must be evaluated over a minimum block of **N = 3 matches** under identical Tier 2 configurations before reaching an architectural conclusion.

#### 3. Control Variables (Held Strictly Constant)
- **Crust Material:** Locked at **400,000** for all evaluation matches (eliminating economic distortions seen in Match 22's 350k and Match 23's 500k).
- **Human Posture:** Pavol must play his **standard meta** (economic boom into 40+ defenses, banked repair fund, late 50-gun pulse). Experimental handicaps (e.g., nukes-only) must be logged as invalid test runs.
- **Lobby Roster:** Exactly 2 Smarter AIs + 1 Original AI (Level 1) for all primary balance testing.

#### 4. Primary Decision Log Success Metrics
A release candidate is judged successful if and only if telemetry verifies:
1. **Focus Fire Ratio:** `>= 85%` of primary trajectories across both AIs commit to the leading human on turns T21–T28.
2. **Placement Failure Count:** `< 15` total placement abandonments per AI across the entire match (down from 207).
3. **Aim Directionality:** Average unfound weapon facing angle `< 75°` (down from 105°–120°).
4. **Energy Conversion Efficiency:** `< 20%` unspent energy on turns T25+ (down from 60%–75%).
5. **Zero Peacetime Regressions:** 100% monotonic war status (`war == true` continuously once triggered).

END OF REPORT
