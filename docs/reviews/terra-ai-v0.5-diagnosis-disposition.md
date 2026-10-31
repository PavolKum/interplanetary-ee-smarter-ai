# v0.4.1 match diagnosis review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.5-diagnosis.md`. Prompt: `terra-ai-v0.5-diagnosis.prompt.txt`. Data: `docs/playtests/data/v041-*.jsonl`.

## Corrections to my write-up (accepted)
- Fey was not blind to weapons all along: `blindEnemies` is the aggregate economy/city flag. It saw 4 Railguns at T23,
  10 at T24-25, 13 at T26, 36 at T31 (per-building visibility differs from aggregate totals).
- The human had 0 Railguns at T20, 1 at T21, 4 at T22, 24 at T27.
- Net building loss was -79, -43, 0, -93 (T28-32), not "100+ a turn".
- Fey kept building telescopes (two per turn whenever blind); the plateau was replacement of losses. H5 "stopped adding" refuted.
- Nuclear Missile was bought T27 (Thermonuclear queued T21), not first seen at T37. Planet ran dry T32, not T37.
- "435 hits" are firing-list selections, not confirmed impacts: interception of Fey's fire is not measured.

## New findings (accepted)
- T27-30 Fey queued **Ion** defenses against a Railgun attack: when no weapons were visible, the fallback spread over the
  fewest-owned types.
- Powered targeting entries fell 57 (T26) -> 44 (T27) -> 31 (T29): firepower lost or disconnected (needs investigation;
  the targeting list is not a live weapon count).

## v0.5.0 changes (Terra's #1 and #2)
| Change | Detail |
|---|---|
| Defense floor at war | `DesiredDefenses` >= ceil(DefenseFloorShare 0.15 x own non-city buildings) once at war. T21 Fey (140 buildings) -> 21. Terra proposed a floor only when an enemy goes blind after being visible; I made it unconditional at war because the human who won built 19 Kinetic before any weapon, and every game reaches war. Peaceful-game overbuild accepted as insurance cost (Terra's caveat noted). |
| Catch-up rate | per-turn cap = max(DefensesPerTurn 3, ceil(deficit / DefenseCatchUpTurns 3)) |
| Weapon memory | enemy weapons once seen are remembered per type, max(visible, memory x 0.95/turn); feeds defense count, defense type and reserve |
| Blind type choice | no known threat -> Kinetic 2:1 over Ion (Shield 0.5), not fewest-owned |
| MaxDefenses | 40 -> 60 (unchanged v0.4 default migrated) |

## Deferred
- Terra #3: instrument real outcomes (shot type, interception, damage) and investigate the powered-weapon drop. Needed
  before changing the offense mix or volley tactics.
- Reserve for planned defenses (currently 0 while no defense exists; resolves the turn after the first ones finish).
- Planet pacing (`max(drain, production)`; refinery no help when production > drain).
