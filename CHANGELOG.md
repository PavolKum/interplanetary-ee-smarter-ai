# Changelog

One entry per version, newest first. Match results are in [docs/PROGRESS.md](docs/PROGRESS.md); per-match notes in
`docs/playtests/`, reviews and their dispositions in `docs/reviews/`. Dates are commit dates.

## v0.22.0 - 2026-11-08

Not yet played in a match.

- Floating plants for unpowered offshore buildings: when every unpowered building that can take a power line stands on
  water, a water plant is queued first and placed next to one of them, connected (`[Director] WaterPlantForWater`).
- Land plants whose only anchors are unpowered water buildings are placed among powered land buildings instead.
- A placement step that did not place clears its power connection, so a later fallback cannot keep a stale anchor.
- New log fields for unpowered land and water buildings and water-first plants; new gate in `verify_gates.py`.
- Gemini review: 6 findings, fixes applied.

## v0.21.0 - 2026-11-08

- Land-only buildings prefer land anchors when any exist (`[Director] AnchorMediumMatch`); match 28 had given up 73 land
  placements with offshore mines in the anchor pool.
- One log record per placement search (placed, gave up, still open).
- Gemini review fixes: water anchors chosen by position, the search widens after 60 steps, stricter placement gate.
- Match 29 (1v1): human won at T31.

## v0.20.0 - 2026-11-08

From a five-model review council (decision record in `docs/reviews/2026-11-08-council-v020/`).

- Retaliation latch: from turn 12 a war started by being hit stays a war; before that it ends only after 2 quiet turns.
- Late fire on turns held for a pulse strike, without changing the pulse cadence; every refused hold logs its reason.
- Measurement first: build stamp (commit and dirty flag) and non-default config in the log, more per-turn fields, and a
  gate script (`AI/tools/verify_gates.py`).
- Build: one version source (the build fails if csproj and plugin disagree); deploying into the game is opt-in
  (`-p:Deploy=true`) and refuses while the game runs.
- Matches 27 and 28: human won at T47 and T45.

## v0.19.0 - 2026-11-08

- Late free fire: a turn held only for lack of energy fires when the energy and material actually left after
  construction, the defense reserve and repairs cover the strike.
- The aim watch logs when the human ended his turn.
- Match 25: **first Smarter AI win** (three Smarter AIs, T38; the human tried a nukes-only offense). Match 26: human won
  the rematch at T44.

## v0.18.0 - 2026-11-07

- Aim watch: on a firing turn the AI keeps searching for trajectories past its window while fewer than 80% of its
  weapons have one and new ones keep coming, up to 90 seconds.
- Human first also by reach: the human becomes the target when enough of the AI's shots can reach him.
- Matches 22-24: human won all three.

## v0.17.0 - v0.17.2 - 2026-11-07

- Building placement modelled on a strong human layout: non-defense buildings inside existing defense cover when
  possible, else near a city; water buildings along the shore.
- Rare Material timing: up to 2 during the build-up, none at war.
- Turn guard: exceptions in an AI module's turn start are logged and contained.
- v0.17.1: the Smarter AI runs only on the lobby levels in `[General] SmartLevels` (default Impossible); every other AI
  runs the original code, untouched, and is observed in the log.
- v0.17.2: review fixes (lasers only when every level is smart, setting latched at game start).
- Match 21: human won at T57, the longest match so far.

## v0.16.0 - v0.16.2 - 2026-11-01 to 2026-11-04

- v0.16.0 (from a two-model council): slot engine with an opening cap, economy-first research with a hijack guard,
  target-aware offense (artillery when blind, precision weapons only on visible cities), a paid-shot ledger, bounded
  intel bidding, defenses funded before repairs, Kinetic upgrades, visible-only fairness fixes.
- v0.16.1: Jamming slot reservation, science cap exemption, defense and sight research first, budget-sized intel bids,
  planet drain guard.
- v0.16.2: material first (extraction before defenses when energy is left over), human first with return fire only at
  the attacker, free fire when energy would go unspent.

## v0.15.0 - v0.15.3 - 2026-11-01

- See cities, strike them, cover our own: an intel sprint until every enemy city has been seen, a strike planner for
  visible cities with a volley ledger, guided-weapon accuracy 0.85 (the human's measured Missile rate), anti-missile cover
  per city, counterintel city projects.
- v0.15.1: accuracy taken from the game's weapon list; city cover must close a real deficit.
- v0.15.2: return fire under attack, city-ring Ion search, intel energy guard.
- v0.15.3: registered weapons only, last-phase attack signal, opening energy reserve.

## v0.14.0 - v0.14.2 - 2026-11-01

- Attrition fire, research order, defense floor 0.25, plant slots, placement backoff, a 3-deep research queue.
- v0.14.2: research queue appends after the first target instead of replacing it.

## v0.13.0 - v0.13.1 - 2026-11-01

- The AI repairs its buildings (the original AI never does).
- Energy cap removed; no extraction during a material glut; weapons sized by free slots.

## v0.12.0 - v0.12.1 - 2026-10-31

- Planet budget: energy cap, material reserve, selling dry mines.
- Fire only with weapons that can aim; reachability fix; weapon cap 30; 40-second aiming window on strike turns.

## v0.11.0 - v0.11.1 - 2026-10-31

- Commit to a target by expected shots through its defenses; abort strikes the engine cannot aim.
- Energy glut spending, counter-research and counter-defenses (Shields against Lasers), damage-per-building upgrades.
- v0.11.1: live projectile counts, urgent research preemption, per-AI refusal logging.

## v0.10.0 - v0.10.1 - 2026-10-31

- Strategy layer: hold fire until a committed salvo saturates the target's defenses, then strike in pulses; the firing
  reservation goes back to construction while holding.
- Energy-first research (Reactor Trials moved early), material glut pricing.
- v0.10.1: per-target reachable salvo with accuracy and overlap, unseen defenses counted, defense gate before war.

## v0.9.0 - v0.9.1 - 2026-10-31

- Weapon upgrades chosen by damage per firing energy; science projects and upgrades valued by research pace; research
  plan reordered.

## v0.8.0 - v0.8.3 - 2026-10-31

- Anti-nuke city defense: Ion defenses over each city after a nuke is seen or lands (one per city from T28), placed so
  they actually cover the city; cap on intact and queued Ions.

## v0.7.0 - v0.7.3 - 2026-10-31

- Coverage-aware defense placement: each new defense goes where the most uncovered value is (cities weigh most).
- v0.7.1-v0.7.3: candidates checked with the game's full placement test and line rule, search widens on failure, fails
  closed, frame budget.

## v0.6.0 - v0.6.1 - 2026-10-31

- Defense ledger: every interception roll is counted per action phase, and the defense energy reserve, weapon choice and
  remembered enemy defenses are driven by what actually happened.
- Floating Plant fallback when land plants cannot be placed.

## v0.5.0 - 2026-10-31

- At war, defenses kept at 15% or more of the AI's own buildings, with a catch-up rate.
- Enemy weapons once seen are remembered while hidden (decaying); Kinetic first when no threat is known; defense
  counters read from the active ruleset (Classic differs).

## v0.4.0 - v0.4.1 - 2026-10-31

- The enemy view is exactly what the intel overlay shows; an enemy the AI cannot read gets an assumed share.
- Focus fire only when the top enemy clearly leads; lower grudge weight.
- Telescope Arrays until every enemy is readable; energy kept for defense rolls; endgame material bank.

## v0.3.0 - v0.3.1 - 2026-10-31

- Economy director replaces the original construction choice: yield upgrades and city projects, mines and offshore mines
  into free power slots, plants to match, energy kept near 0.6x material income plus weapon upkeep.
- Fixes for three original-AI construction bugs (power-plant spiral, water buildings never placed, pre-installed
  upgrades dropped at placement).
- Military spending from turn 15 (earlier if hit hard); weapon type by damage per cost against the target's defenses.

## v0.2.0 - v0.2.2 - 2026-10-30 to 2026-10-31

- Superweapon savings, a build planner (growth first, then a balanced mix), a research plan from a human's recorded
  wins, smart defense (counter the most visible enemy firepower), experimental laser support (off by default).
- v0.2.2: never reserve energy (it resets every turn).

## v0.1.0 - v0.1.2 - 2026-10-30

- Threat-based focus fire, grudges with decay, reachability memory, city-finishing guided strikes.
- Accuracy re-split at a configured hit share; planned misses fired only as verified near-misses.
- Fix for an original-AI bug that left secondary-only weapons silent.
- v0.1.2: human-parity accuracy defaults.
