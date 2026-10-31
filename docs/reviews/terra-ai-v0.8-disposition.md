# AI v0.8.0 code review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.8.md` (fix first; accepted by the wrapper after 109 s). Fixed in v0.8.1.

| # | Finding | Disposition |
|---|---|---|
| High 1 | Anti-nuke shortfall added to a total gate that counts the Ions themselves: construction stops short | **Fixed**: own step (3b) and gate: queue while this turn's anti-nuke < missing, independent of DesiredDefenses |
| High 2 | "2 per city" is an inventory (Ions anywhere, unpowered too) | **Fixed**: per-city deficit from working (normal, powered) Ions whose radius holds the whole city |
| Medium 3 | Centre coverage overstates protection (interception at the impact point; blast radius 3.7) | **Fixed in part**: a city counts as protected only if radius - cityRadius > distance. Blast geometry (impacts near but off the city) not modelled |
| Medium 4 | T28 prior is a heuristic | **Changed**: prior from T24 (config); logs show first sighting/impact via nukeThreat |
| Medium 5 | First-volley funding (Ion rolls 200) | **Fixed**: under a nuke threat the reserve funds one 200 roll per working Ion, up to 40% of income, still under the recovery cap |
| Medium 6 | City projects (DDI, City Barrier: 50% vs nukes) ignored | Deferred to v0.9 |
| Medium 7 | Kinetic placement changed (residual value clusters) | **Fixed**: stacking (p = nuke probability) only for the anti-nuke defense; every other defense keeps the v0.7 uncovered-value rule (p = 1) |
| Low 8 | Tests are formula-level | Partly: per-city deficit tests (radius minus city footprint, Ions elsewhere ignored), 3 more mutants caught. Integration: next match |

Checked and fine (Terra): nuclear impacts reach IsHitIntercepted with the weapon id; base fallout does not bypass interception (only an upgrade does); Ion is the best buildable counter (0.8); Classic has no nuke and no counter (anti-nuke id null).
191 checks.

## v0.8.1 match bug and the v0.8.2 review (`terra-ai-v0.8.2.md`, fix first) - fixed in v0.8.3

v0.8.1 match: citiesProtected = 0 all game for both AIs while they owned up to 21 Ions; the whole-footprint rule (3.3 for
Ion 5.3 / city 2) was nearly unreachable (a defense stands >= ~3.1 away). v0.8.2: protected within radius - cityRadius/2,
cap 2 x perCity x cities. Terra confirmed the radii and units; the log cannot prove geometry was the only cause.

| # | Finding | Disposition |
|---|---|---|
| High 1 | Placement scored existing Ions at full radius, the deficit at radius - cityRadius/2 | **Fixed**: one per-target reach loss shared by CoverCounts, MarginalValue and PickAnchorsMarginal; tests + 2 mutants |
| High 2 | Cap counted only powered Ions; ordinary defense queue can add Ions | **Fixed**: cap on every intact Ion plus this turn's queued Ions (any path) |
| Medium 3 | "Protected" is a heuristic against a 3.7 blast | Labelled heuristic in code; sampled-impact model deferred |
| Medium 4 | Fixed 4.3 shrink vs city-specific 4.25-4.45 | **Fixed**: per-city loss; sampling ring capped at radius - 0.85 (the most generous city) |

200 checks.
