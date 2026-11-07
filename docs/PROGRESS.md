# Smarter AI - progress log

Goal: an AI for *Interplanetary: Enhanced Edition* that beats a skilled human (Pavol) by building, researching and
strategizing better, with fair information (its own intel only) and human-like accuracy. No resource cheats.

Method, every iteration: Pavol plays a match against the current version while a live monitor reads the AI's decision
log and his telemetry; the logs are analysed into a playtest note; a GPT-6.1 model (Terra for code, Sol for strategy)
reviews the diagnosis and then the code; findings are fixed or explicitly deferred; tests are mutation-checked (each
new rule is broken on purpose and a test must catch it); the build is installed only with the game closed.

Sources of truth: per-match notes in `docs/playtests/`, reviews and dispositions in `docs/reviews/`, raw logs in
`docs/playtests/data/` (git-ignored), git history (one commit per version).

## Scoreboard

The human has won every match so far.

| # | Version | Opponents | Lost at | Peak AI energy/turn vs human (turn) | What the version tried | Why it still lost |
|---|---|---|---|---|---|---|
| 1 | 0.2.0 | 4 AIs | T41 | ~3.1k vs - | Targeting, accuracy, superweapon savings, build planner | Energy savings froze construction from T25 (energy does not carry over); weak economies |
| 2 | 0.3.1 | 4 AIs | T43 | 37.6k vs 7.9k (T25) | Economy director: yield upgrades, energy/material balance, research plan | Out-produced the human 4-5x but could not convert it: blind (no intel), vendettas between AIs |
| 3 | 0.4.1 | 1v1 | T38 | 25.8k vs 11.8k (T26) | Intel, endgame bank, research plan from the human's order | 0 defenses until T23; 2x economy did not survive the human's strike |
| 4 | 0.5.0 | 1v1 | T36 | - | Defense floor, defense energy reserve | Lost sooner; defenses present but overwhelmed |
| 5 | 0.6.0 | 2 AIs | T37 | - | Defense ledger (measure interceptions), observed defense need | 53-85% of impacts met no eligible defense: coverage, not energy, was the limit |
| 6 | 0.7.3 | 2 AIs | T41 | 13-15k vs - | Coverage-aware defense placement | Economies held (longest match), not enough offense |
| 7 | 0.8.x | 2 AIs | - | - | Anti-nuke city protection (Ion over cities) | (no separate note) |
| 8 | 0.9.1 | 2 AIs | T39 | ~7k vs 24k (T35) | Science projects, weapon upgrades, research reorder | War from T15 ate ~60% of energy (firing reserve + defense reserve); 15-37k material idle; AIs fought each other |
| 9 | 0.10.1 | 2 AIs | T39 | 67.1k vs 23.4k (T30) | Strategy layer: hold fire and release the firing reservation, saturation strikes, committed target, Reactor Trials research early, material-glut pricing | Kingmaking (first 6 strikes AI vs AI); planet space and material caps (8-59k energy unspent per turn); strikes at the human did nothing (11 of 48 weapons found a trajectory); human's 111 shots/turn met no eligible defense 70-98% of the time; no Shields |
| 10 | 0.11.1 | 2 AIs | T47 | 61.5k vs 23.7k (T29) | Commit by expected shots through; abort strikes without trajectories; live projectile counts; energy glut; counter research and counter-defenses (Shields); placement throttle | Both AIs mined their planets dry by T26-27 (human: T38), then could not build or repair; only 36% of live weapons found a trajectory; 13 of 26 strikes aborted; target flip-flopped every turn (reachability memory misreads search time-outs) |
| 11 | 0.12.1 | 2 AIs (T14 save of #9) | T35 | 24.0k vs 15.5k (T25) | Planet budget (energy cap, material reserve, sell dry mines), fire what can aim, reachability fix, weapon cap 30, 40 s aiming window | Energy cap choked the economy (24k vs 67k from the same save) while 96k material sat unused; strikes did no lasting damage (the human repairs, the AI never does); human's 26 Kinetic never seen; Railguns only |
| 12 | 0.13.1 | 1v1 (new map) | T30 | 13.3k vs 39.2k (T23) | AI repair, material glut stops extraction, weapons vs the committed target, untested weapons share observed pass, hidden weapons counted from impacts | Defenses too few and late (18-21 Kinetic, 3 a turn, 70-80% of 100-120 shots/turn met none in range); research 18 vs 30 techs (Robotic Mining never); T1 bought 3 Solar Plants (no slot for a Mine); 6/26 and 9/26 aimed on strike turns, then held while 26 weapons fell to 10; blind all match |
| 13 | 0.14.1 | 1v1 (new map) | T29 | 28.9k vs 13.2k (T27) | Opening fix, defense floor 0.25, attrition fire, aim budget 2600, placement backoff, 3-deep research queue | Out-produced the human from T7 and aimed 18-29 of 20-30 weapons, but never saw a human city (counterintel x2-11.7 before its first telescope at T16), so its Missiles had no city targets; 2 of 5 cities fell to the human's first 14-Missile volley (Ion covered 2 of 5); 5-9k energy unspent (firing and weapon caps); research queue bug |
| 14 | 0.15.1 | 1v1 (new map) | T39 | 8.0k vs 6.7k (T31) | Intel sprint (all cities seen by T9), shadow/visible strike planner with volley ledger, guided accuracy 0.85, per-city anti-missile cover that must close a deficit, counterintel city projects | Held fire 9 turns at 64-82% through (short of 12 shots) while the human's Railguns hit it every turn; lost 16 Railguns unfired at T38; the sprint cost 3 turns of energy; every Ion search refunded (T26-T33); science 250-450 vs 500-900 |
| 15 | 0.15.3 | 1v1 (new map) | T33 | 12.8k vs 29.4k (T25) | Sol v0.15/v0.15.2 fixes: registered weapons only, return fire on last-phase enemy impacts, city-ring Ion search, opening energy reserve | Shadows off + counterintel x16-27 hid every city from T23, so all 45 Missile strikes went to random points; 30 Telescopes lost the intel race; Ion ring built over (1 of 28 searches placed); economy half the human's at war |
| 16 | 0.16.0 | 1v1 (new map) | T31 | 52.0k vs 31.1k (T24) | Council v0.16: slot engine (opening permanent cap, Jamming/SSTAR), economy-first research + hijack guard, E:M 1.0, target-aware offense (blind = artillery, precision withheld, paid-shot ledger), bounded intel bid, defense funded before repairs + forward reserve, Kinetic upgrades; visible-only fairness fixes | Out-produced the human through T20 (67.6k vs 63.1k E) but half his science; 5 Rare Material copies took the Jamming slots; T16-T19 stall with 7k E idle waiting on Kinetic/Telescope/Missile tech; blind all war (intel 24-44 vs 130-244 needed, bid spent 1k of 27k then stop-loss); 0 unpaid rolls but 35-43% of impacts met no defense; planet drain ~50k/turn after 86 Offshore Mines |
| 17 | 0.16.1 | 3 AIs (new map) | T36 | 41.8k vs 27.6k (T23) | Jamming slot reservation, Rare Material cap 1, Science Institute exempt from the opening cap, Kinetic/Plasma/Telescope tech right after EMC, budget-sized intel batches with a reachable stop-loss, planet drain guard | Tech tempo fixed (Kinetic/Ion at T15, Telescopes T16) and the AIs saw the human at T17-T18, but before any weapon existed; then counterintel x3.8-17 hid him and all 208 shots were blind artillery (no damage); material-starved with 65-70% of the planet unmined, so 57-66% of energy went unspent and Jamming (900 M) was never bought; 36% interception in 60+ volleys (41-72% met no defense) |
| 18 | 0.16.1 | 3 AIs (new map) | T41 | 50.4k vs 39.6k (T26) | (same build, second sample) | Out-produced a slow human 1.4x on energy to T20, but held fire T21-T25 while a human city was visible (7.5-9 of 30 Railgun shots expected through, below the 12-shot gate; no guided weapons for the precision path) and fired blind on max-hold; then the commit rule (passing x up to 1.5 for the leader) turned Asona on the wounded AIs (116 of its 174 aims, T29-T35) although it saw 2 human cities; counterintel x33; 74-81% of energy unspent, planets 55-69% unmined |
| 19 | 0.16.2 | 2 AIs (new map) | T31 | 17.4k vs 26.2k (T22) | Material first (extraction before defenses when energy is left over), human first with return fire only at the attacker, free fire when the energy would go unspent | Material first worked (material 1.7k to 3.1k/turn in two turns, unspent energy 0-6% vs 87-89%) and the AIs led to T14; then the human stacked two global material projects (x1.69) and SSTARs (44k energy/turn by T23 vs Eddie's 17k peak). Eddie fired at the human every war turn T20-T25 (free fire below the 12-shot gate, about 55 hits) with no lasting damage; placement failed for every type (12 of 30 weapons with no trajectory to the human, power plants and defenses abandoned); war only at T19 (Orbital Assembly at T18, science 2.6x behind); Junker's turn start threw T15-T24, so it built nothing and hoarded 51k material |

## The arc so far

1. **v0.2-0.4: the economy can be won.** The native AI never bought yield upgrades. The director (v0.3) fixed that and
   the AIs out-produced the human 2-5x by T25. They still lost: blind, no defenses, no plan for the output.
2. **v0.5-0.9: defense and offense, measured.** Defense floor and reserve (v0.5), a ledger of every interception roll
   (v0.6) showing coverage was the limit, coverage-aware placement (v0.7), anti-nuke (v0.8), science and weapon upgrades
   (v0.9). Each fix worked locally, but the economy lead disappeared: war from T15 consumed the energy it needed to grow.
3. **v0.10: posture, not mechanics.** Sol's strategy review: the human builds quietly, strikes in pulses and invests
   in between. Hold fire returns the firing reservation to construction, and Reactor Trials research moved from #31 to #7.
   The economy lead came back (67k vs 23k energy at T30), but the AIs spent it on each other.
4. **v0.11: convert the lead.** Pick targets by expected damage, not by economy rank; abort strikes the engine cannot
   aim; spend surplus energy on material and per-building damage; counter the human's weapon mix (Shields vs Lasers).
   Result: the longest match (T47) and the best defenses yet, but the AIs mined their planets dry by T27 and the
   engine's trajectory search (bounded by work per frame and geometry) aimed only about a third of their live weapons.
5. **v0.12: budget the planet, fire what can aim.** Targeting became consistent (12 of 12 strikes at the human, 1 abort),
   but the energy cap was backwards: energy is the throughput that spends material. Lost faster from the same save (T35 vs
   T39). The match exposed that the native AI never repairs.

## Recurring lessons

- **Measure before fixing.** The defense ledger (v0.6) and the salvo calibration (v0.11: an eligible impact is stopped
  72% of the time over 38 defender-turns) changed the plan more than any review.
- **Copying the human's cards works, with a lag.** Research order, Reactor Trials, projectile upgrades, pulsed strikes all
  came from his telemetry. To win, the AI also needs edges a human lacks (exact timing, full accounting, memory).
- **Reviews catch wrong premises.** Terra rejected "aim first" in v0.11 (the engine restarts every trajectory search each
  turn) and found the v0.10 salvo model blind to upgrades. Every review and its disposition is kept in `docs/reviews/`.
- **Check the stocks, not only the flows.** Per-turn yields looked healthy in v0.11.1 while the planet's finite
  material hit zero; the human paced his mining and outlasted two economies three times his size.
- **Know the engine's limits before building on them.** Strike planning assumed weapons can aim; the native search is
  bounded by the human's turn time. Read the native code for the constraint before adding a layer over it.
- **Three-player dynamics matter.** Scoring by visible economy turned the two AIs on each other in v0.9.1 and v0.10.1
  while the human grew.

## Before going public

`decompiled/` (game source, copyrighted) is in this repository's history, and `docs/ruleset/` holds extracted game data.
A public repository must be a clean export without them, not a push of this history.
