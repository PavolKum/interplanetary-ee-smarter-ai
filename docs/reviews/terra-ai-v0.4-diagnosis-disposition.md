# v0.3.1 match diagnosis - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

First "diagnosis before code" review (project CLAUDE.md): Terra recomputed the match from the raw logs
(`docs/playtests/data/`, not committed) before v0.4 was written. Report: `terra-ai-v0.4-diagnosis.md`.

| Hypothesis | Terra verdict | What changed in v0.4 |
|---|---|---|
| H1 AIs blind (no telescopes; native intel need counts only guided weapons) | Confirmed (0 of 155 economy records saw an enemy economy; v0.2 had 42) | Intel policy independent of weapon type; telescope chain early in research |
| H2 Grudge-only targeting -> AI-vs-AI vendettas (~6% at the human) | Partly: 6.6% of assignments; human grudge rose late (T39-42); other factors too | Unknown enemies get an explicit economy share; grudge weight 0.35; enemy view from the intel overlay |
| H3 Planet ran dry T26-28 | Partly: dry at T25/T25/T28, but **banks of 90k/120k/53k remained**; spent before the nuke unlocked | Endgame bank before the superweapon unlocks; refinery instead of Yield2 on mines |
| H4 Science too low, nuke never finished | **Partly refuted: Thermonuclear finished T32/T36/T37**; material was gone by then | Bank (above); science weight 1.5; nuke chain earlier |
| H5 Railgun-only vs Kinetic | Confirmed as a mismatch, impact unquantified | Missiles (Orbital Assembly) earlier; weapon choice now sees defenses once intel exists |
| H6 Placement loop | Confirmed (241 / 196 abandoned) | Failed types not requeued the same turn |
| (new) Only 5,998 of 12,026 weapon-turns powered | Raised by Terra | Up to 5 plants a turn for unpowered buildings; deeper look after the next match |
| (new) Fairness: v0.3 read live per-building stats of visible buildings | Raised by Terra | Replaced by the intel overlay's aggregate numbers, same gate as the UI |

My write-up was wrong on H1's mechanism ("saw each other's economies") and on H4; both corrections came from the logs.
