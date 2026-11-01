# Disposition: Terra review of AI v0.16.1 (docs/reviews/terra-ai-v0.16.1.md)

**Verdict received:** SHIP AFTER FIXES (2026-11-01 21:24). All three highs and the feasibility medium are fixed.

**Verification:**
- Brain tests: 512 checks pass.
- Mutations: 4 of 4 new ones caught, plus 5 of 5 for v0.16.1's first pass.
- Plugin build: clean.
- Adapter code: unverified in-game until the next match.

| Finding | Fix | Status |
|---|---|---|
| High: Science Institutes can take the Jamming slots before EMC; ordinary permanents before Lossless | Cap-exempt science permanents keep the Jamming room before and after EMC (`leavesJammingAlways`). Ordinary permanents stay under the opening cap of 2, which match 17 used at T4-T5 before any Jamming existed | fixed (ordinary permanents bounded by the cap) |
| High: the planet guard compared production against a bank-inclusive figure and missed upgrades | `PlanetGuard.DrainAllowed`: today's drain, max(planet cost, production), to `PlanetGuardHorizonTurn` (36), plus the new drain for its duration, must fit the remaining planet pool. The bank is excluded. Applied to mines, mine yield upgrades with a planet-cost multiplier, and drain-adding projects (6-turn duration for Rare Material). `d.PlanetDrain` grows with each executed option. After losses the drain falls and room returns | fixed |
| High: the 50% intel stop-loss could be unreachable | It now stops after IntelFailedBatchesStop failures with half the budget spent, OR after twice the failures regardless of spend | fixed |
| Medium: the reservation ignored feasibility | It reserves min(ceiling - active, cities without Jamming); queuing a Jamming decrements both | fixed (affordability not modelled; Jamming is 200 E / 900 M) |
| Medium: intel ledger reset on reload | Pre-existing; deferred with the other save/load state | deferred |
