# AI v0.7.0 code review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.7.md` (verdict: fix first). First review run through `AI/tools/terra_review.py` (accepted after 91 s;
the unguarded run before it had hung). Fixed in v0.7.1.

| # | Finding | Disposition |
|---|---|---|
| High 1 | Same-turn defenses do not see each other's planned positions | **Fixed**: this turn's queued placements (`core.Actions.ConstructedBuildings`, their `BuildingWorldPosition`) count as targets, and those of the same id as coverage |
| High 2 | Placement can have no usable power connection (stale anchors; native defense branch samples up to the defense radius, beyond line reach, e.g. 5.9 vs 5.5) | **Fixed**: own placement step. Anchors rebuilt every step (normal, distributing, free slot); sampling ring = the game's connector ring capped at the defense radius, so never beyond line reach. Not testable offline |
| Medium 3 | Anchor score is not the coverage gained | **Fixed**: 12 candidate spots per step, each checked with the game's `IsBuildingPlacementValid` and raycast to the surface, scored by the value it newly covers at that point; the best is placed. Overlap for multiple interception attempts not modelled (deferred) |
| Medium 4 | "Covered" counts unbuilt/unpowered defenses with base radius; weapon-agnostic | **Fixed in part**: working (Normal, powered) defenses with `Stats.Defenses.Radius` (upgrades included), plus this turn's queued ones; log uses the same. Still by building id, not by weapon and probability (Shield/city defenses vs Railguns) - deferred |
| Medium 5 | Impact geometry scans every defender incl. the human's hidden buildings | **Fixed**: AI defenders only, squared distances, each defense's own radius; named proxies in comments |
| Low 6 | Removing the early return also advances the shared RNG | Kept the early return (it was never removed); noted |

Untested in-game: the placement step (reflection on `m_result`, `IsComplete` setter, `TryPlaceBuilding`). It gives up to the
game's own step after 60 steps, when nothing uncovered is near any anchor, or on any exception.
171 checks pass.

## Focused re-review of v0.7.1 (`terra-ai-v0.7.1.md`, fix first) - fixed in v0.7.2

| # | Finding | Disposition |
|---|---|---|
| High 1 | Fallback to the game's own defense search reintroduces unconnected defenses | **Fixed**: no fallback for defenses; with nothing left to cover the same safe path places anywhere legal and connected; a hopeless search is refunded by the abandon valve |
| High 2 | Best-scoring candidate may not be placeable (IsBuildingPlacementValid checks only queued overlaps) | **Fixed**: every candidate runs the game's full TryPlaceBuilding check (places nothing; allocates a collision object only); best legal one wins |
| Medium 3 | Ring is a margin, not a guarantee of the line | **Fixed**: the game's exact rule is checked on the result (world centre distance <= both radii + longer connector range) |
| Medium 4 | 12 anchor scorings per step | **Fixed**: `PickAnchors` scores once and draws 12. Frame cost still unmeasured |
| Low 5 | Queued defenses weighted 1 | **Fixed**: 0.25 like existing defenses |

174 checks. Surviving mutant (removing the early return in PickAnchors) is output-equivalent (all weights zero -> no draws); it only advances the RNG.

## Confirming review of v0.7.2 (`terra-ai-v0.7.2.md`, fix first) - fixed in v0.7.3 (installed without a further review, at Pavol's go)

| # | Finding | Disposition |
|---|---|---|
| High 1 | Blocked best anchors can trap the search until abandonment | **Fixed**: widening by failed steps - steps 1-3 near-best band, 4-8 any anchor with uncovered value, then any anchor (coverage still scored) |
| Medium 2 | Exceptions / missing radius fall back to the unsafe native search | **Fixed**: defense searches fail closed - the step counter is pushed to the abandon cap (refund next finder update) |
| Medium 3 | Unbounded per-frame work | **Fixed in part**: 120 candidates per frame (excess waits a frame, uncounted), 150-step cap per defense search. Frame cost still unmeasured; a first search can hold the budget for a few frames |
