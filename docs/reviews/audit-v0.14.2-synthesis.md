# v0.14.2 deep audit: Sol + Fable synthesis

Two independent read-only audits of the whole mod at v0.14.2 (`sol-ai-v0.14.2-audit.md`, GPT-6.1 Sol, 33 findings;
`fable-ai-v0.14.2-audit.md`, Fable xhigh via claude -p, 14 findings). Both verdicts: **fix first**. Both: the economy can
now win; the mod lacks the conversion from production to city kills and from defense inventory to city survival.

## Agreed by both (highest confidence)

| # | Finding | Sol | Fable |
|---|---|---|---|
| A | Blind on cities: telescope techs at plan #11-12, first telescopes T16 after the human's first hub (T13); six telescopes (48 intel >= small city 45) before counterintel would have seen every city | #2, move 1 | #1, move 1 |
| B | Guided targeting takes only Visible cities/weapons; Shadow cities (remembered, `PlayerIntelHandler.cs:183-207`) are ignored, and with no candidate native falls back to a random point on the sphere (`AIWeaponTargeting.cs:483-497`) | #3 | #1, #4 |
| C | No volley damage allocation: 8 strikes at T22 on one 330 HP weapon | #4, move 3 | #4 |
| D | Per-city Ion policy fires for Nuclear Missiles only; ordinary Missiles add Ions anywhere; total-Ion cap and 3-5/turn throttle | #1, move 2 | #2, #7, move 2 |
| E | Intel and counterintel never valued; Telescope ScienceYield1 (x0.75 intel) bought 24 times; Quantum Encryption Hub (+50 counterintel per city, 200 M) never | #2 | #5, #12 |
| F | Accuracy quota sized from retained (incl. dead) targeting entries | #10 | #3 |
| G | Glut at the 30-weapon cap: 5-6k energy and 20-27k material idle T27-28; but a blind cap raise recreates the aiming collapse (2600 budget: 30 weapons 87 steps, 40 -> 65, 60 -> 43) | #9, move 4 | #6 |
| H | Refinery policy blocks Yield II unconditionally | #21 | #9 |
| I | No city-growth / combat-project policy (connectors x1.3, defended x1.3, Vertical Farms x1.5, population damage x1.1 per city) | #14, move 5 | #10, #11 |
| J | Counter research reads last turn's threats | #12 | #14 |
| K | Measurement: log guided assignments (Visible/Shadow/fallback), impacts and damage per target, per-city population, intel vs thresholds, idle energy | #29 | gaps list |

Sol correction accepted: `target.cities = 0` measures population readability, not city coordinates. The blindness
diagnosis rests on the strike records (no city candidate on any fire turn) and the counterintel timeline.

## One reviewer only (verify before acting)

- Sol: superweapons starved by native first-in-list hit selection (#5); repair protects base, not live, firing cost (#6);
  City Barrier outside construction and counter research (#7); laser far-side occlusion (#8, lasers off by default);
  superweapon purchase can zero the defense reserve (#11); defense upgrades never selected (#13: Kinetic range +69% area,
  Ion +82%); global projects forecast additively (#15); upgrade scoring omits operating costs (#16); projects can land on
  a ruined city (#17); recovery Floating Plants placed unconnected (#18); abandon rollback reverses uncredited gains (#19);
  reserve blocks the energy needed to spend it (#20); salvo model uses the strongest defense type for all (#22);
  disconnected defenses count as counters (#23); laser integration (#24); small superweapon arsenals wait (#25); incoming
  memory survives the shooter (#26); multi-AI kingmaking (#27); persisted v0.14.0 plan not migrated (#28: v0.14.0 was
  never installed, so moot here); backoff released by cancellation (#30); diagnostics survive reset (#31).
- Fable: guided weapons fire deliberate misses at the 0.7 level-1 accuracy (36 of 174 aimed Missile shots T23-T28;
  the human's own Missiles hit the planet 60 of 71) (#3); defense energy reserve scales with defense count (#8); MIRV as
  the blind weapon (#13, suspected); GrudgeWeight rewritten on every load (#14).

## Proposed v0.15 scope

Core (agreed, match-deciding):
1. Intel sprint: Self-Adjusting Optics + Advanced Signal Processing at plan #3-4; up to 6 telescopes early while any enemy
   city is unseen; no ScienceYield on telescopes while blind; value IntelYield.
2. Guided targeting over Visible and Shadow cities (frozen observations only), cities first, with a per-volley damage
   allocation ledger; log every assignment and fallback.
3. Per-city anti-missile cover: on any guided enemy weapon seen or landed (Missile, MIRV, Nuke), 2 powered Ions per city,
   counted per city (not planet-wide), exempt from the defense throttle.
4. Quantum Encryption Hub in every city once Optics is done.
5. Instrumentation (K).

Second tier: accuracy per weapon class and quota from live weapons (F; guided accuracy is Pavol's fairness call);
superweapon priority inside the hit quota; live firing costs in repair; bounded arsenal replacement and measured cap
expansion (G); defense range upgrades.

Later: growth and combat projects (I), refinery horizon (H), City Barrier, forecast and scoring corrections, multi-AI.
