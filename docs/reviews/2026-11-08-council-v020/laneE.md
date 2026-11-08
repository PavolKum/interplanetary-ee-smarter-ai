# Lane E (Sonnet 5.5): verification, risk and cost, for v0.20-v0.22

Read-only pass. Nothing in the repository was edited, built, installed, tested or committed. All analysis scripts live
in my scratch directory outside the repo (`C:\Users\pavol\AppData\Local\Temp\claude\f7ed064ede66cb0e\7f41b2d4-b65d-48c7-ad45-5f629c9dc1c0\scratchpad\`);
the central one is reproduced in section 7 because Temp is not durable.

## 1. Scope and frozen target

- **Commit:** `4d7c160161e0ea0037ef44474077bd0ff9666a31` (`git rev-parse HEAD` confirmed; I did not modify tracked files).
- **Files read (whole or in the cited ranges):**
  - `docs/PROGRESS.md`; the five playtest notes `docs/playtests/2026-11-07-v0.18.0-match22.md`, `...match23.md`,
    `2026-11-08-v0.18.0-match24.md`, `2026-11-08-v0.19.0-match25.md`, `...match26.md`.
  - `docs/reviews/terra-ai-v0.19-disposition.md`; `docs/reviews/2026-11-01-council-v016/decision-record-r2.md` (lines 52-80 only);
    this council's `charter.md` and `brief.md` (facilitator files, not lane reports). I did not open any other `laneX.md`.
  - Code: `AI/InterplanetaryAI.csproj`, `AI/Plugin.cs` (config blocks), `AI/DirectorPatches.cs` (weapons, war turn, placement,
    shelter, Abandon, EndTurn), `AI/StrategyPatches.cs` (LateFreeFire, war flag), `AI/Patches.cs` (aim budget, fire-paid),
    `AI/AimWatch.cs`, `AI/LaserPatches.cs` (guards), `AI/Brain/{Coverage,Offense,Tempo,Director,Strategy}.cs` (cited functions),
    `AI/Tests/Program.cs` (Main, Check, structure), `CLAUDE.md`, `AI/README.md` (build section).
  - Data: all `*-ai-decisions.jsonl` for matches 22-26, `*-human-telemetry.jsonl` (coverage and event schema only), the
    BepInEx logs (header lines only), `evidence-manifest.tsv` (head).
- **Not done:** I did not run the unit tests (the rule forbids runs), so "620 checks" is the brief's number, not mine. The source has
  613 `Check(` call sites and 51 test functions, all called from `Main` (orphan scan: none).
- **Definitions I use.** *Smart AI* = an AI with `init.smart == true` and records beyond T2 (drops the aborted lobby in m25).
  *War-phase AI-turn* = a `director` record at or after that AI's first `strategy.war == true` turn: **193** such turns over
  **10 AI-lobbies** (m22: 1, m23: 2, m24: 2, m25: 3, m26: 2). *Fire turn* = `aim-search.fire == true` with `live >= 5`: **99**.

## 2. Verdict and confidence

**Verdict.** The verdict is: do not open v0.20 with placement. The idle-energy problem the notes attribute to a crowded planet is, in the
logs, mostly a configuration ceiling (the 30 aimed-weapon cap `MaxWeapons`) and, secondarily, a material shortage. v0.20 should
be small and mostly measurement: (a) instrumentation, build provenance and a script that evaluates the gates, with no behaviour
change; (b) one behaviour experiment, the weapon cap relaxed only under energy glut, run as per-AI arms inside the same match so
2-3 matches yield paired evidence. Facing (v0.21) and placement failures (v0.21-v0.22) become conditional on what (a) shows. The
war/peace flip is real but costs about one or two turns of 5-8 guns; fix it only when touching that code anyway.

**Confidence: Medium.** High on the arithmetic of the idle-energy decomposition (it is a count over 193 turns that anyone can
reproduce in one script) and on the process findings. Low on whether more guns raise damage (the found-trajectory count already
plateaus near 20-28 and 35% of fire turns end with 38-44% of guns unfound because of the time cap or stall budget). Low-Medium on
the facing ceiling (inferred from means, not per-weapon data). Anchored on: `director.energyLeft/aimedWeapons/materialLeft`,
`aim-search.live/found/stop/foundFacingDeg/unfoundFacingDeg`, `director.placementFailures`, `cover-search`, `strategy.war`.

## 3. Observations with evidence

**O1. FACT. Idle energy is mostly the weapon cap.** Of 193 war-phase AI-turns, 52 ended construction with `energyLeft >= 10,000`.
33 of the 52 (63%) had `aimedWeapons == 30` (37 of 52, 71%, at `>= 29`); 18 of the remainder had `materialLeft < 2,000`; 1 had
neither (Ash T38, 27/30 guns just after losses). 69 of the 193 war turns (36%) were at the cap at all. Command: `python repro_idle.py`
(section 7) printed `193 52 33 18`. Caveat: on held turns `energyLeft` includes the released firing reservation (2,000-3,200 in
`late-free-fire` records), so true idle is up to ~3k lower; the 10k threshold absorbs that.

**O2. FACT. The match 26 note's "more guns could not be placed" is contradicted for T33-T37.** Ash `aimedWeapons` was 30 on
T33-T37 (15 Railgun + 8 Missile + 7 Coilcannon at T35) with `energyLeft` 36,800 / 33,395 / 32,059 and `materialLeft` 13-20k
(`python ashrg.py`). The note (match26.md:53-54) attributes this to placement. Placement did fail for Ash earlier: 19 Railgun
abandonments in T23-T31 (T29 queued 6, abandoned 6; the count is 18, not the 30 in the note, which is the whole-match total), but in
that same window `materialLeft` was 2-1,719 on seven of nine turns (T23-T29), so placement and material starvation are confounded there.

**O3. FACT. The cap was a deliberate deferral whose premise has since changed.** Plugin.cs:482-483 (`MaxWeapons`, default 30, "aimed
2-15 of ~50"); `decision-record-r2.md:74` lists "Any increase to MaxWeapons" as not-in-v0.16; the notes asked to lift it at
v0.14.1 (`2026-11-01-v0.14.1.md:38`) and flagged "at the 30-weapon cap, with nothing to buy" at match 21
(`2026-11-07-v0.17.1-match21.md:86`) and match 26 (`match26.md:19`). Since the aim watch (v0.18) the aimed ratio is **1,609 of 2,147
live guns (75%)** on fire turns, versus 4-13 of ~30 in match 21. The cap was never retested after the premise changed.

**O4. FACT + INFERENCE. Volley size is search-limited as well as gun-limited.** On fire turns, found guns per turn: live 21-26 ->
mean found 19.1 (n=40); live 27+ -> 20.2 (n=22). Stop reasons (n=99): enough 26 (found/live 0.87), human turn 32 (0.77), cap 24
(0.62), stall budget 11 (0.56), all aimed 6 (1.0); 35 turns (35%) stopped with <= 62% found. 48% of fire turns had found/live < 0.8.
INFERENCE: adding guns without more search time may buy much less than +33%; the experiment must measure `found`, not just `live`.
(Command: `python plateau.py`, `python stops.py`.)

**O5. FACT. A third confounder between matches 25 and 26: aim depth.** `AimBudget.StepsPerWeapon(total, weapons)` divides 2,600 by
the total weapons of all AIs (Patches.cs:103, Brain/Offense.cs:92; floor 30, cap 100). m25 sat at the **30-step floor from T23 to
T37** (97-110 AI weapons); m26 ran 45-55 steps in T22-T35; m22 60-100. The notes compare m25 and m26 on lobby size and the human's
strategy only. Steps matter moderately: found/live is 0.725 at 30 steps (n=32) vs 0.836 at 86-100 (n=10). A larger cap lowers steps
per weapon in 2-Eiai lobbies (about 50 -> about 35), and is already at the floor in 3-Eiai lobbies. (`python budget.py`.)

**O6. FACT + INFERENCE + JUDGMENT. The facing evidence is weaker than the notes imply.**
- FACT: weapon-weighted mean angle between a gun's outward normal and the target bearing, over the 178 AI-turns with live >= 5:
  **all live guns 88.1 deg** (n=3,520 gun-turns), found 81.4 (n=2,586), unfound 106.5 (n=934). On the 99 fire turns alone: all 86.7
  (n=2,147), found 80.3, unfound 105.9; of the 538 unfound guns, 385 (72%) face away (> 90 deg). (`python facing.py`, `python foundsteps.py`.)
- FACT: unfound-gun angle is 104-109 deg for every stop reason (enough, stall, cap, human turn), so the means do not separate "cannot
  reach" from "search ran out of time".
- INFERENCE: placement is effectively target-blind (88 deg is the 90 deg expected from uniform placement), and angle is a weak
  predictor: found guns average 81 deg, so many far-side guns do find paths. The notes quote 99-111 deg without the 90 deg chance
  baseline. A ceiling: only the 25% unfound guns can gain, only the angle-explained part of them; I estimate at most +8-12% found per
  volley if every far-side gun found at the near-side rate. Treat as an upper bound.
- FACT: planets orbit (`decompiled/Interplanetary.Prefabs/CelestialBody.cs:49-51`, `Orbit`), no bearing is logged per turn, and the
  target changes (m25 T23: Zenon 3/30 and Sarai 6/29 found after switching to Beastie). Facing at build time may not hold at fire time.
- JUDGMENT: facing is an M-effort change for a bounded and unproven gain, and it competes with shelter scoring (O7).

**O7. FACT. Placement failures are a full-planet effect concentrated in a few types and AIs.** 502 abandoned searches against
3,223 queued building attempts (16%) in five matches (three independent counters agree: `placementFailures`, `queued.abandoned:*` and
`refunds`: 107 in m25, 229 in m26). Fail share of queued: NuclearPlant 125/214 (58%), IonDefense 136/288 (47%), SolarPlant 91/265
(34%), Coilcannon 12/45 (27%), Missile 8/48 (17%), Railgun 44/306 (14%), KineticDefense 31/423 (7%), TelescopeArray 14/241 (6%),
OffshoreMine 11/611 (2%). The `cover-search` records (defense placement) show the mechanism: 235 searches, 117 gave up; in the
gave-up ones 74% of tries were `unplaced` (no legal spot) and 23% `noAnchor`; outcomes by phase placed/gave-up: T<=20 72/19,
T21-30 28/71, T31+ 8/27 (`python cover.py`). Water buildings
(OffshoreMine 611 + FloatingPlant 467) are 33% of all queued buildings. In m26, Ash alone has 203 of 229 failures. Note headline
numbers drift: the m25 note says "127 times" but its own breakdown sums to 107, which matches the log (`python placefail.py`, `aband.py`).

**O8. FACT + INFERENCE. Power-plant failures are probably not the binding constraint.** The AI is already energy-idle in 52/193 war
turns, so more plants add energy it cannot spend. Unknown: `unpowered > 0` on 69 of 221 AI-turns from T15, and 20-30 unpowered
buildings at T33-T36 in lost lobbies (m22 Eiai T35: 25, T36: 30); the log does not say whether those are weapons. A weapon without
power is not usable (`AimWatch.Usable`, AimWatch.cs:204-209). Missing field: `unpoweredWeapons`.

**O9. FACT. The war/peace flip is the damage trigger, not tech.**
- `WarPhase = turn >= warFromTurn || recentDamage >= damageTrigger` (Brain/Director.cs:271; trigger 600, Plugin.cs:542). The latch
  (DirectorPatches.cs:210) holds only the turn path.
- 4 W->peace transitions in 10 AI-lobbies: m22 Eiai T18; m24 Eddie T15 and T18; m26 Ash T19. In each, `recentDamage` straddles 600
  (Ash 605.7 -> 529.0; Eddie 608.3 -> 425.8 and 679.5 -> 475.7). Cost: 1-2 turns with 5-8 railguns on "peace" (Eddie shots 6-8).
- `director.warTurn` was 20 for all 10 AIs: that is `WarFallbackTurn` (Plugin.cs:659). The early war (T16) needs Orbital Assembly done
  and income >= 12,000 (Brain/Tempo.cs:60); Orbital Assembly reached the research head only at T18-T20. So "war starts late because tech
  comes late" is partly a config fallback, and a research-order trade-off against economy-first. (`python flip.py`.)

**O10. FACT. "Strongest when focused" is confounded.** HUMAN-FIRST fire turns: m25 34 of 36, m26 23 of 27. Even so, the share of
each AI's paid shots that land on the human is only 56-71% over the match (the rest are planned misses and secondary targets;
`destination: own` also mixes own planet and non-player bodies, Patches.cs:605). The human fired 16 shots in the whole of m25 against 2,028 in m26 (telemetry `shot` counts), so the
win in m25 cannot be attributed to focus. (`python dest.py`, `python tele.py`.)

**O11. FACT. The "late free fire paid in full" check compares the ledger with itself.** In the six m25 and three m26 late volleys,
paid equals `firingHits + firingMisses` (25=21+4, 17=17+0, 27=20+7; 4=3+1, 11=9+2, 13=10+3) but not `plannedHits + plannedMisses`
(29, 30, 29; 10, 15, 16). The note's wording ("planned hits + misses") is wrong; the numbers it quotes are the firing ones. Any gate
must name `firing*`. 9 volleys, 0 aborts. (`python lffjoin.py`.)

**O12. FACT. Baselines for do-no-harm gates (m25 + m26).** Late free fire 9 volleys / 0 aborts. `aimWaitAfterHuman` on fire turns:
m25 mean 7.2 s, max 24.7 (zero on 15/36); m26 mean 5.5, max 38.5 (zero on 20/27). HUMAN-FIRST share of fire turns 94% and 85%.
`reserveShortfall > 0` on 2/54 and 1/40 director records (T>=20); `lastUnpaidRolls > 0` on 0. `coveredBuildings` mean 0.886 (min 0.76,
n=94), `coveredCities` 0.98. (`python baseline.py`, `python covbase.py`.)

**O13. FACT. Process and tooling.**
- About 76% of code by bytes (458.5 KB of 606 KB) is game-bound patch code outside the unit-test project, which links `Brain/*.cs`
  only (`AI/Tests/BrainTests.csproj`). The v0.19 round-2 blocker ("the call runs too late") was found by Terra reading code, not by
  a test (`terra-ai-v0.19-disposition.md`, round 2 #1).
- No mutation script exists (grep over `*.py/*.ps1/*.sh`): the mutation step is manual and its results are prose in dispositions.
- `AI/InterplanetaryAI.csproj:53` deploys to the game folder after every Release build unless `-p:SkipDeploy=true`; the only guard
  is prose at `CLAUDE.md:8`. `<Version>0.4.1</Version>` (csproj:11) vs `Plugin.VERSION "0.19.0"`: two sources of truth.
- The decision log has no build or config fingerprint (`init` event, Patches.cs:48). The BepInEx log gives the version only.
- The raw evidence (89 MB) is git-ignored (`.gitignore:9`), a single local copy; the sha256 manifest exists only for this council.
- Static scan: 22 `*Prefix/*Postfix` methods, 19 check `IsSmart/SmartModule/SmartGoal/Plugin.Active`; the 3 unguarded are
  `LaserPatches` (installed only if `EnableLasers`, default off), so "original AIs untouched" holds at default config
  (`python smartguard.py`; heuristic: first 2,500 chars of each body).
- n is small: 5 matches, 10 AI-lobbies; turn-level samples inside an AI are autocorrelated, so effective n is closer to the number of
  AI-lobbies than to 99 fire turns.

## 4. Risks, ranked

1. **Acting on one win.** m25 is one match with three Eiais and a nukes-only human; m26 changed two variables at once and a third
   (aim steps) silently. Any outcome-based gate ("AI wins") is unfalsifiable at n of 1-3.
2. **A cap raise that does not convert.** If search throughput is the real ceiling (O4), more guns add upkeep and wait but no
   damage. Mitigation: the A/B arm and a payoff gate on `found`.
3. **Regression to v0.19 features** from a larger cap: late free fire needs `energyLeft >= reservation` (reservation grows with gun count);
   `AimTargetShare 0.8` of more guns lengthens the extension (up to the 90 s cap, `AimingWindowMax`); steps per weapon fall in 2-Eiai lobbies;
   weapon material spend may starve repairs and the defense reserve.
4. **A facing term colliding with shelter** (`Shelter.Score` = cover first, Coverage.cs:370): raising Railgun give-ups on a crowded planet
   and lowering `coveredBuildings` (0.886), the property the human's layout wins on.
5. **Wiring bugs invisible to the 620 checks** (76% of code is untested game-bound code), so a green suite proves little about a patch.
6. **Wrong build in the game.** Default deploy plus no provenance in the log: a data set cannot prove which tree produced it.
7. **Fairness drift.** The new code touches planet bearings, an arm label and idle classification. All are public or own state, but a
   careless "likely target" could read hidden enemy values; any new patch must be `IsSmart`-gated. Human-experience cost (extra AI wait)
   is a UX cost, not a fairness violation, but is Pavol's call.
8. **Evidence loss.** The only copy of the raw logs is local and git-ignored; hand-typed note numbers already drifted.

## 5. Recommendations, ranked

Candidate matrix first (my lens: test, verify, fairness, regression, effort). "Pure fn" = what to extract into `AI/Brain/` so
`AI/Tests` can test it.

| Candidate | Pure fn to extract and test | Verify in game (decision-log fields) | Fairness | Regression risk | Effort |
|---|---|---|---|---|---|
| Idle-spend diagnosis | `IdleReason.Classify(aimed, cap, material, freeSlots, failures)` table test | new `idleReason` on `director` | none (own state) | none (logging) | S |
| Room for guns (cap) | `WeaponCap.Allowed(aimed, cap, glutBonus, glut)`, `Arms.Pick(id, seed)` | `director.aimedWeapons/energyLeft`, `aim-search.live/found/stop/seconds`, `arm` | none (own resources, no accuracy change) | medium (O3 list) | S |
| Placement facing | `Facing.AngleDeg(centre, gun, target)`, `Facing.Bonus(angle, w)`; golden test `w=0 == Shelter.Score` | facing buckets, `foundFacingDeg`, `coveredBuildings`, Railgun give-up share | low if target = existing planner output and only planet positions are used | medium-high (shelter, give-ups) | M |
| Placement failures: Ion | tests around `Coverage` model | `cover-search` outcome shares by phase | none | medium | M |
| Placement failures: plants | existing `PlantsForUnpowered` logic | new `unpoweredWeapons` | none | low value if energy stays idle | S-M |
| War flip | `WarLatch.Update(ref latched, ...)` | `strategy.war` series, count of W->peace with shots > 0 | none | low-medium (earlier war posture) | S |
| Earlier war | `WarStart.Turn` already pure and tested; arm `WarFallbackTurn` | first fire turn, energy at T28-T32 | none | medium (growth) | S (config) |
| Focus fire | `Strategy.HumanFirstHolds` already tested | `humanFirst` share, `destination` shares | none | medium | S-M |

**R1 (v0.20, S). Instrument, stamp, and make the gates executable. No behaviour change.**
- *Evidence answered:* O1/O2 were classified by hand after the match; O6 cannot separate reach from time; O8 cannot say if unpowered
  buildings are guns; O13 provenance and gates by script.
- *Mechanism:* log `idleReason` ({weaponCap, material, placement, slots, other}) when `energyLeft >= 10,000`; log `faceBuckets`
  (live and found per 45-degree bucket) and the bearing to each opponent per turn in `aim-search`; log `unpoweredWeapons`; add a
  `config` event at `init` with build id (git short hash + dirty flag), non-default config values and the arm; split `fire-paid`
  `destination` into `own` / `neutral` / `none`; add `AI/tools/verify_gates.py` that prints each gate as PASS, FAIL or UNTESTED with
  its denominator (the v0.16 council rule 11: a gate whose opportunity never arises is UNTESTED, not passed).
- *Files:* `AI/DirectorPatches.cs` (EndTurn), `AI/AimWatch.cs` (Log), `AI/Patches.cs` (init, fire-paid), `AI/Plugin.cs`, new `AI/Brain/IdleReason.cs`, `AI/tools/verify_gates.py`.
- *Exit gate (falsifiable):* in the first v0.20 match, (a) every smart-AI `init` has `build` and `arm`; (b) >= 95% of war-phase records with
  `energyLeft >= 10,000` carry `idleReason`; (c) `faceBuckets` sum to `live` on 100% of `aim-search` events; (d) run on the m22-m26 files,
  `verify_gates.py` reproduces `193 / 52 / 33 / 18` within +-2; (e) fed a synthetic log with the cap-bound turns removed, it returns FAIL
  for the cap gate (it can fail).
- *Fairness check:* only own state and public planet geometry; no enemy hidden value is read.

**R2 (v0.20, S-M). Cap experiment: `MaxWeapons` 30 -> 42 only under energy glut, A/B per AI.**
- *Evidence answered:* O1, O3, O4.
- *Mechanism:* pure `WeaponCap.Allowed(aimed, cap, glutCap, energyGlut)` using the existing `d.EnergyGlut`
  (`IsEnergyGlut`, 30% of budget unspent and >= 5,000 left) so the cap rises only when energy is idle; `Arms.Pick(playerId, matchSeed)`
  assigns control (30) or treatment (42) per smart AI, deterministic from the nickname so it is reproducible and logged in `init.arm`.
  In a 2-Eiai lobby one arm each; in 3, split 1:2 and alternate next match.
- *Files:* `AI/Plugin.cs` (config, per-player lookup), `AI/DirectorPatches.cs` lines 1292 and 1337 (`atCap`, `slotsBind`), new
  `AI/Brain/Offense.cs` types, `AI/Tests/Program.cs` (about 25 checks).
- *Exit gates* (on fire turns with live >= 5; need >= 20 per arm across at most 3 matches, otherwise report UNTESTED and keep playing):
  - G1 wiring: treatment has `aimedWeapons >= 31` on >= 5 AI-turns; control never exceeds 30.
  - G2 mechanism: share of war turns with `energyLeft >= 10,000` is <= 15% in treatment (baseline 52/193 = 27%, of which 33 at the cap).
  - G3 payoff: median found per fire turn in treatment >= control + 3 (baseline median 16, p25 11, p75 21). If G1 and G2 pass and G3 fails,
    record "guns are not the limit; search throughput is" and move v0.21 to search depth/time, not facing.
  - G4 do-no-harm (m25+m26 baselines): `aimWaitAfterHuman` fire-turn mean <= 10 s and max <= 45 s (7.2/24.7 and 5.5/38.5);
    every late-free-fire volley has paid == `firingHits + firingMisses` and 0 aborts; HUMAN-FIRST share >= 85% (94%, 85%);
    `reserveShortfall > 0` on <= 5% of records (3.7%, 2.5%); `coveredBuildings` mean >= 0.85 (0.886); steps per weapon never below 30 (floor).
- *Fairness check:* the AI spends its own energy and material on its own buildings; accuracy by level untouched; the human has no gun cap
  (34 railguns + 18 lasers by T33 in m22), so 42 is closer to human parity; Smarter AIs only.

**R3 (v0.20, S). Deploy and provenance hygiene.**
- *Evidence:* O13. *Mechanism:* make deployment opt-in (`-p:Deploy=true`), fail the build if `Interplanetary.exe` is running, take the version from one
  place (read `Plugin.VERSION` or set both from one property), stamp commit and dirty flag into the BepInEx log line and `init`. Back up
  `docs/playtests/data/` (89 MB, git-ignored) outside Temp after each match and record its sha256 in the note.
- *Files:* `AI/InterplanetaryAI.csproj`, `AI/Plugin.cs`. *Exit gate:* a plain `dotnet build AI -c Release` leaves the game folder untouched
  (compare the DLL hash before and after); with the game running, the deploying build fails loudly; the log shows the commit.
- *Fairness:* n/a. *Effort:* S.

**R4 (v0.20 tooling, S-M). Scripted mutation list.**
- *Evidence:* O13 (manual mutation claims in prose). *Mechanism:* `AI/tools/mutants.tsv` (file, old line, new line, expected failing check) and a runner
  that copies `AI/Brain` and `AI/Tests` to a scratch directory, applies each mutant, runs the tests and records killed/survived. Each new pure
  function in R1/R2 gets at least two mutants (boundary at the cap, glut flag ignored, arm swapped).
- *Exit gate:* every R1/R2 mutant is killed; the runner exits non-zero on a survivor. *Fairness:* n/a. *Effort:* S-M.

**R5 (v0.21, M, conditional). Weapon facing as a soft term.**
- *Go condition from R1 data:* far-bucket (135-180 deg) found-rate <= 0.6x near-bucket (0-45 deg) on >= 30 fire turns, and bearing to the
  usual target changes < 40 deg over 10 turns. Otherwise drop it.
- *Mechanism:* `Facing.Bonus(angle, w)` added to the weapon branch of `ShelterStepUnsafe` score with `w` below one defense of cover; `w = 0`
  must equal v0.19 for the whole grid (golden test); target from the existing planner (visible-only).
- *Files:* `AI/Brain/Coverage.cs` (new pure type), `AI/DirectorPatches.cs` (ShelterStepUnsafe, ~line 2794), tests.
- *Exit gate:* for guns built after the change, weapon-weighted mean angle at fire time <= 70 deg (baseline 87-88); found/live >= 0.80
  (baseline 0.749, 1,609/2,147); Railgun give-ups <= 14% of queued (baseline 44/306); `coveredBuildings` >= 0.85.
- *Fairness:* planet positions are public; no hidden value; Smarter AIs only. *Effort:* M.

**R6 (v0.21-v0.22, M). Ion-defense placement and surface budget, not power plants.**
- *Evidence:* O7 (IonDefense 47% fail, cover-search success T21-30 28 of 99, 74% `unplaced`, water buildings 33% of queued).
- *Mechanism:* cap water buildings once material is no longer the constraint, reserve defense sites before T20, and sell or replace
  dry mines earlier (exists); judge by `cover-search`. Power plants wait for `unpoweredWeapons` evidence (R1).
- *Files:* `AI/DirectorPatches.cs` (coverage step, economy options), `AI/Brain/Coverage.cs`. *Exit gate:* `cover-search` gave-up share at
  T21-30 <= 40% (baseline 71 of 99 = 72%) and IonDefense abandoned/queued <= 25% (baseline 136/288 = 47% over the five matches), with
  `coveredBuildings` and `coveredCities` not lower than baseline (0.886, 0.98). *Fairness:* none. *Effort:* M-L.

**R7 (v0.22, S). War latch and, separately, an earlier-war arm.**
- *Latch:* pure `WarLatch.Update` used by `StrategyPatches` only (line 162), so the economic posture is untouched. Gate: zero W->peace
  transitions in `strategy.war` on turns with shots > 0; baseline 4 in 10 AI-lobbies. Effort S; value is small (O9).
- *Earlier war:* arm `WarFallbackTurn` 20 vs 17 per AI. Gate: first fire turn earlier by >= 2 and peak energy at T28-T32 not more than 10%
  below the control arm. Effort S (config). *Fairness:* none.

**Not recommended for v0.20:** changing focus-fire rules (O10), power-plant placement (O8), any accuracy or target change, any outcome-based gate.

**Recommended v0.20 scope, sized for 2-3 matches: R1 + R2 + R3 (R4 alongside).** About 6 small edits and ~25 new checks. Match 1
validates the instruments and starts the arms; matches 2-3 add paired data. At that point the decision is explicit: PASS, FAIL or UNTESTED
per gate; nothing is concluded from a win or loss. A saved game at T18-T19 (the project used a T14 save in match 11, PROGRESS row 11)
lets later versions replay the same state; the human still varies, so judge on mechanism fields only.

**Process changes to the loop (playtest -> note -> review -> mutation -> commit):**
- Generate the numeric part of each playtest note from the log by script (placement totals, idle counts, stop reasons) and stop hand-typing
  counts: m25 "127" is 107 in the log; "paid = planned" is wrong wording (O11); "105 deg" lacks the 90 deg baseline (O6).
- Review the diagnosis, not only the code (the CLAUDE.md rule from 31 Oct): this council is that step; require the next Terra review prompt to
  carry O1, O4, O5 and O6 so it tests them.
- Treat every adapter as unproven until its log field appears in a real match (wiring witness): `arm`, `idleReason`, `weapon-cap` counters.
  v0.19 shipped with "game lifecycle ... unverified in game" and was verified only by m25/m26; keep the explicit UNVERIFIED tag until then.

## 6. The strongest reason I may be wrong

I may be over-reading a count. The decomposition in O1 shows where the idle energy sat, not what it would have bought: if the trajectory
search throughput (90 s window, 30 steps per weapon in 3-Eiai lobbies, 35% of fire turns cut short) is the true ceiling, raising the cap
yields nothing, and a facing change that lifts "found" per gun could still be the better lever. My facing ceiling comes from means (88 deg
overall, 81 found, 106 unfound) not per-weapon data; a steep nonlinear angle effect hidden inside those means would make facing worth
more than +8-12%. The 10-AI-lobby sample, autocorrelated turns, and the per-AI variance (median found 9 for Eddie, 19 for Zenon) mean
2-3 matches can confirm a mechanism but not a size. Finally, `materialLeft < 2,000` after refunds may under-state the material the
queue actually had, which would shift some "material-starved" turns toward placement.

## 7. Reproduction and unknowns

**Commands run (all read-only).** From `C:\Users\pavol\Repos_Local\interplanetary-mods`: `git rev-parse HEAD`, `git log --oneline`,
`git check-ignore -v docs/playtests/data/v019m26-ai-decisions.jsonl`, `grep -n` for the cited lines. Python analysis, each a file in my scratch
directory: `schema.py`, `timeline.py v019m26 Ash`, `cap.py`, `idle.py`, `idle2.py`, `idlemat.py`, `plateau.py`, `foundsteps.py`, `facing.py`,
`stops.py`, `stopangle.py`, `budget.py`, `flip.py`, `placefail.py`, `cover.py`, `aband.py`, `pfai.py`, `unpow.py`, `dest.py`, `tele.py`,
`lffjoin.py`, `baseline.py`, `covbase.py`, `orphans.py`, `smartguard.py`, `ashrg.py`. Central script (prints `193 52 33 18`, verified):

```python
import json, collections
D = r"C:\Users\pavol\Repos_Local\interplanetary-mods\docs\playtests\data"
def load(m):
    return [json.loads(l) for l in open(D + "\\" + m + "-ai-decisions.jsonl", encoding="utf-8") if l.strip()]
tot = idle = cap = mat = 0
for m in ["v018m22", "v018m23", "v018m24", "v019m25", "v019m26"]:
    rows = load(m)
    maxt = collections.defaultdict(int)
    smart = {o["ai"] for o in rows if o["e"] == "init" and o.get("smart")}
    for o in rows:
        if "ai" in o: maxt[o["ai"]] = max(maxt[o["ai"]], o["t"])
    for ai in (a for a in smart if maxt[a] > 2):
        wt = next((o["t"] for o in rows if o.get("ai") == ai and o["e"] == "strategy" and o.get("war")), None)
        for o in rows:
            if o.get("ai") == ai and o["e"] == "director" and wt is not None and o["t"] >= wt:
                tot += 1
                if o["energyLeft"] >= 10000:
                    idle += 1
                    if o["aimedWeapons"] >= 30: cap += 1
                    elif o["materialLeft"] < 2000: mat += 1
print(tot, idle, cap, mat)
```

**Unknowns (each is a named gap, not a guess).**
- Per-weapon found/unfound by angle bucket and the bearing drift between build and fire (R1 adds both).
- Whether unpowered buildings at T33-T36 are weapons (`unpoweredWeapons`).
- Whether more guns raise `found` (R2 G3) and what they cost in aim wait, steps per weapon and repair funding.
- Realized hits per volley: the notes' "hits" are `plannedHits` (the accuracy roll plan); the telemetry `damage` events are unattributed
  (`kind: building_hp`, no attacker), and `observedImpactsBy/observedInterceptedBy` (m25: 74/106/101 impacts, 25-27 intercepted) are a ledger whose window I did not
  verify. A realized-outcome gate needs one of them defined first.
- Order of `director.energyLeft` relative to the late-free-fire deduction (read from the code and the Terra disposition, not observed).
- Whether original AIs' weapon totals in `AimBudget` (all AI weapons count) matter beyond what I measured.
- Unverified on the game surface: everything above is derived from logs and source; nothing was observed live, and I did not run the test project.

END OF REPORT
