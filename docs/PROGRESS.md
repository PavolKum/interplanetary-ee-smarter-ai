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
| 10 | 0.11.1 | 2 AIs | T47 | 61.5k vs 23.7k (T29) | Commit by expected shots through; abort strikes without trajectories; live projectile counts; energy glut; counter research and counter-defenses (Shields); placement throttle | Both AIs mined their planets dry by T26-27 (human: T38), then could not build or repair; only 21% of weapons found a trajectory; 13 of 26 strikes aborted; target flip-flopped every turn (reachability memory misreads search time-outs) |

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
   engine's trajectory search (time-bound during the human's turn) aimed only a fifth of their weapons.
5. **v0.12 (next): budget the planet, fire what can aim.** Material is a whole-game budget, not a flow.

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
