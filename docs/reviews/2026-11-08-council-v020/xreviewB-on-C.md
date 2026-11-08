I'll verify lane C's claims systematically. Starting with the playtest notes and cited code lines.
Playtest notes match several claims already. Now the cited code lines:
Code citations 1/3 check out so far. Now the remaining code lines and the settings defaults:
Code checks largely confirm. Now checking `ShelterCoveredSteps` default, the war-phase computation site, and AimWatch:
Code citations verified. Now let me inspect the data format and re-derive the key numbers from the JSONL logs:
The scoreBy values and ratios reproduce exactly. Note: `passingBy` shows jogi at 6/9 (T22) and 10/10-max (T25), so the reach condition held — the score floor was the binding constraint, which supports the fix logic. Now war flips, facing angles, and placement counts:
War-flip data reproduces exactly (Eddie T14 war / T15-16 peace / T17 war / T18-19 peace; Ash T17-18 war / T19 peace / T20 war). Now the facing-angle means and placement failures:
Mismatch — my naive mean (45°) differs from the note's 99–120°. The note's number must be conditioned differently. Let me inspect the aim-search records:
`-1` is a sentinel. Re-running with the filter, plus checking per-turn vs pooled aggregation:
Facing angles reproduce exactly (M22 120°/19, M23 108°/29, M24 111°/29, M25 99°/51, M26 105°/34). Now placement failures and M25/M26 volley counts:
The tool says Ash 203 / Hawking 26 (total 229), but the playtest note says 230/207 and laneC repeats 207. Let me check how the tool counts and where the discrepancy comes from:
The tool sums per-turn `placementFailures` dicts. My earlier "last record" read was wrong. Recounting properly:
Ash is 203 (not 207) and the M26 total is 229 (not 230) — the playtest note's totals are slightly off, and laneC inherited them. Now Match 25 volleys and the human's peak energy from telemetry:
M25 superweapon turns (T26/T31/T31/T34) and the 71%/76% trajectory-finding rates verify. Now the human peak-energy claims from telemetry:
Human peaks check out (M25 ~21.7k at T29 vs note's "20.5k"; M26 46,483 at T39-40 vs "46.5k"). Now the "~150 energy per defense roll" figure that anchors the verdict's saturation math:
The 150 E/roll figure is corroborated (`game-math-report.md:24`: Kinetic 100, Ion 200, Shield 150; the mod reserves 150). Now checking the "85–90 shots/turn at the human" number and the PROGRESS.md history claims:
85–90 shots/turn at the human verified (T24–T37: 86–93). Now the PROGRESS.md history claims behind observation 8:
PROGRESS.md citations verify (row 8: "War from T15 ate ~60% of energy"; row 9: 67.1k vs 23.4k). Note row 25 also carries the 230/207 figures I measured as 229/203. Final checks — Ash's full commit trace, the decompiled finder citation, and git HEAD:
The cited line range doesn't exist in `AIBuildingPlacementFinder.cs` (59 lines total). Checking `AIBuildingPlacementSearch.cs:195-244` where the claim likely belongs:
All verification is done. Here is my cross-review.

---

# Lane B cross-review of Lane C (Gemini 3.8 Flash)

**Scope:** I re-ran every numeric claim I could against `docs/playtests/data/*` and the cited code at commit `4d7c160` (= current HEAD, verified via `git rev-parse --short HEAD`). I read `brief.md`, `laneC.md`, the three playtest notes laneC leans on, `PROGRESS.md`, and the cited code files. I did not read any other laneX.md. The deliverable is this message only; nothing was written to the repo.

## Claim-by-claim verification

| # | Lane C claim | My independent check | Result |
|---|---|---|---|
| 1 | Energy peaks: M25 Zenon 52.0k (T34), Donnels 48.9k (T34), Sarai 41.4k (T30), human 20.5k (T29); M26 Ash 52.1k (T35), Hawking 50.4k (T22), human 46.5k (T40) | Playtest notes match exactly (match25.md:18, match26.md:12). Telemetry re-derivation: M26 human peak 46,483 E/turn at T39; M25 human peak 21,732 at T29 (note said 20.5k — same ballpark, likely a different checkpoint) | **ACCEPT** (minor: "48k–52k in Matches 25 and 26" overstates — Sarai peaked 41.4k) |
| 2 | Idle energy: Hawking 36–39k unspent at weapon cap; Ash 33–37k E + 25–38k M, T33–T39 | match26.md:19,53-54 verbatim | **ACCEPT** |
| 3 | `MaxWeapons=30` blocks weapon building (`Plugin.cs:482`, `DirectorPatches.cs:1292,1316`) | Both lines verified: `atCap = Plugin.MaxWeapons.Value > 0 && d.AimedWeapons >= Plugin.MaxWeapons.Value`, then `if (atCap && IsAimedWeapon(data)) continue;` | **ACCEPT** with one wording fix: it blocks *aimed* weapons, not all construction ("exit immediately" overstates) |
| 4 | M25 saturation: 774 planned hits, 85–90 shots/turn, pop 2,589→266, AI win T38 | 774 ✓ (262+253+259). Shots at the human per turn re-derived from `fire` records: T24–T37 = 86–93 ✓ (T21–T23 lower: 52/62/30). Pop and victory ✓ note:43-45. Nukes queued T26/T31/T31/T34 ✓ (4 total) | **ACCEPT** |
| 5 | M22: 6 volleys, 81 hits, 0 buildings, ~80M dip; "Pavol built 52 weapons" | match22.md:18,23,31 ✓ (81/6 = 13.5 ✓; 34 railguns + 18 lasers = 52 ✓) | **ACCEPT** |
| 6 | M26 split fire: scoreBy T22 {Armiger 454, Hawking 1102, jogi 240}, ratio 0.217; T25 {235, 1450, 357}, ratio 0.246; commits T22/T24 Armiger, T25 Hawking | Re-derived from `v019m26-ai-decisions.jsonl`: scoreBy and ratios reproduce exactly (0.2178, 0.2462) ✓. Commits ✓ — but incomplete: Ash also committed to Armiger T19, T21 and Hawking T20; and T23 *was* human-first (jogi), so "failed to target the human until T26" is slightly wrong | **AMEND** (evidence understates the split; T23 exception) |
| 7 | "Ash delivered only 358 hits on the human" | 358 is the *combined* planned hits of Ash (184) + Hawking (174) per match26.md:20,24,43. Ash alone = 184 | **AMEND** (misattribution; the comparison is still directionally valid) |
| 8 | `HumanFirstHolds` thresholds 0.90 / 0.35 (`Strategy.cs:313-323`) | Verified; defaults confirmed (`Plugin.cs:710,714`: 0.9f, 0.35f). **Omission:** the reach path *also* requires `human.Passing >= HumanFirstReachShare (0.4) × maxPassing` — laneC describes it as score-only. In the cited turns passing held (jogi 6/9 = 0.67 at T22; 10 vs max 10 at T25), so the score floor was indeed binding and the conclusion survives | **AMEND** (add the passing condition; conclusion unaffected) |
| 9 | M26 placement: 230 total, 207 Ash; breakdown 38 Nuclear / 35 Ion / 30 Railgun / 25 Kinetic / 16 Solar | Breakdown reproduces exactly from `director.placementFailures`. Totals do **not**: I measure **Ash 203, Hawking 26, lobby 229** (tool agrees: refunds 203/26). The playtest note and PROGRESS.md row 25 both carry 230/207 — the error originates upstream of laneC | **AMEND** (minor; immaterial to the conclusion) |
| 10 | Placement code chain: `DirectorPatches.cs:2848-2850, 2800 (2×6+2=14), 2985-2994; Plugin.cs:647 (1500)` | All verified, incl. `ShelterCoveredSteps` default 6 → 14 ✓. One wrong filename: the native-search citation "AIBuildingPlacementFinder.cs:195-244" — that file is 59 lines; the mechanism (random anchor-adjacent placement loop) is at **`AIBuildingPlacementSearch.cs:195-244`** | **AMEND** (citation only; mechanism correct) |
| 11 | Unaimed guns face 120/108/111/99/105° away (19/29/29/51/34 samples, M22–M26) | Re-derived with `unfoundFacingDeg >= 0` filter: 119.6/19, 107.7/29, 111.4/29, 99.5/51, 105.1/34 — all five match. `AimWatch.cs:233-236` and the uniform-random angle at `DirectorPatches.cs:2856` verified | **ACCEPT** (fully reproduced, including sample counts) |
| 12 | War flips: M24 Eddie war T14 / peace T15-16 / war T17 / peace T18-19; M26 Ash war T17-18 / peace T19 / war T20; mechanism = `Director.cs:271-274` + latch only at `DirectorPatches.cs:213` | Re-derived from both JSONLs: exact match, turn for turn. Code mechanism verified: `WarTurnFor` latches only on `turn >= warTurn`; damage-triggered war is never latched, and `WarPhase` re-evaluates `recentDamage >= damageTrigger` each turn (`DirectorPatches.cs:391-392`) | **ACCEPT** |
| 13 | ~150 energy per defense roll | `docs/strategy/game-math-report.md:24`: Kinetic 100, Ion 200, Shield 150; mod reserves 150 (`DirectorPatches.cs:514` per signoff2A). "~150" is a fair mid-range | **ACCEPT** |
| 14 | Trajectory finding 71–76%; 20–30% of weapons can't find trajectories | Tool output: M25 71%, M26 76% ✓ | **ACCEPT** |
| 15 | Early-war history: T15 war ate ~60% of energy (v0.9.1); 67k vs 23k at T30 after v0.10 | `PROGRESS.md:27,28,52,54` — all verbatim ✓ | **ACCEPT** |
| 16 | M25 nukes queued individually T26/T31/T34 without synchronization | Tool: T26 Sarai, T31 Donnels, T31 Zenon, T34 Zenon ✓ | **ACCEPT** |

## The verdict paragraph's causal claim — the big one

**Saturation→defeat is demonstrated only against a debilitated defender.** In M25 the human ran nukes-only (peak 20.5–21.7k E/turn) and over-committed at T32. Lane C's own numbers show why this matters: 85–90 shots/turn × 100–150 E/roll ≈ 9–13.5k E/turn of defense demand — against a *standard-play* human at 46.5k E/turn (M26) that is under a third of income, and in M26 the human **won** while never losing a building. Additionally, per `game-math-report.md:237`, defense rolls only trigger for impacts within a defense cap (covered fraction), so raw shot counts overstate the energy actually demanded. Lane C knows this (its Risk 4 says off-meta wins give false confidence) and then contradicts itself by calling the path "mathematically proven" against "standard human play." It is not proven; it is demonstrated against one self-crippled human and refuted in the one standard-play sample. **AMEND / major.**

## Recommendations

1. **Target commitment fix — ACCEPT rank, AMEND mechanism.** Evidence solid (stronger than stated — T19–T21 splits omitted). Passing-condition omission noted above; a fix that only lowers the score floor can still fail when the human's passing share is low. The AI-on-AI return-fire suppression needs care (M25 note: "Do not over-tune toward the human now"). Gate is measurable from `strategy`/`fire` records and can fail ✓.
2. **War latch — AMEND.** The finding (no latch) is fully verified and the gate is measurable. But the mechanism as written conflicts with laneC's own Risk 1: latching `recentDamage >= damageTrigger` makes *any* stray early hit start permanent war — exactly the "Premature War Trap" (PROGRESS row 8 shows what early war cost). Amend: latch only within a window around the configured war turn, or floor the latch at e.g. `WarFallbackTurn − N`.
3. **Target-biased facing — ACCEPT rank, gate fair.** Evidence reproduced to the degree. Lane C's own "strongest reason I may be wrong" (axial rotation making fixed facing stale) is a genuine threat and the gate would catch it. Fairness claim (planet orbital positions are public) is plausible but I did not verify it in `decompiled/` — flag as unverified premise.
4. **Placement congestion — ACCEPT.** Evidence verified (with the 203 correction). Gates measurable and falsifiable. Note Hawking hoarded 36–39k with only 26 failures — the cap (Rec 5), not congestion, binds there; the two mechanisms should not be conflated.
5. **Dynamic weapon cap — ACCEPT for v0.21.** Watch the config's own warning: the cap exists because the trajectory search is time-bound (v0.11.1 aimed 2–15 of ~50). Raising to 60 increases aim time; the aim-watch telemetry gives the gate data.
6. **Superweapon sync — REJECT the gate, ACCEPT the direction.** The gate ("interception rate of nukes drops ≥ 40%") is **not measurable from the logs** — match25.md:49-50 says whether nukes landed is not visible, and laneC's own Unknown 3 admits detonation attribution is unavailable. A recommendation whose exit gate cannot be evaluated from any recorded surface should not be ranked as a commitment; amend the gate (e.g. timing-only gate: nuke launch turn = a turn with ≥ N conventional planned shots) or add the logging first.

**Experiment-design section (tiers, N≥3, locked crust):** ACCEPT — it directly addresses the confounding both notes already acknowledge.

## Blockers

- **B1:** The verdict's "mathematically proven path to defeating standard human play" — saturation evidence comes solely from the nukes-only match; the single standard-play match refutes it. (major)
- **B2:** Rec 6's exit gate is unmeasurable from the recorded data (contradicts laneC's own Unknown 3). (major for that recommendation)
- **B3:** Rec 2's mechanism as written creates the premature-war failure mode laneC itself ranks Risk #1. (major for that recommendation)

## Non-blocking

- 358 misattributed to Ash alone (combined figure).
- 230/207 totals should read 229/203.
- `AIBuildingPlacementFinder.cs:195-244` → `AIBuildingPlacementSearch.cs:195-244`.
- `HumanFirstHolds` description omits the passing condition.
- "48k–52k energy/turn in Matches 25 and 26" (Sarai 41.4k); M25 human "20.5k" vs telemetry 21.7k.

verdict: Lane C's data work is unusually solid — every number I re-derived (facing angles, war flips, score ratios, shot rates, code mechanics) reproduced exactly, with only inherited upstream slips (207/230, one wrong filename) — but its headline causal claim outruns its evidence, and its #1-ranked saturation thesis is proven only against a self-crippled defender, so trust the observations and Recommendations 1–5, discount the "mathematically proven" framing, and fix Rec 2's latch scope and Rec 6's gate before acting on them.

END OF REPORT
