# Sol v0.14 review: disposition (AI 0.14.1)

Verdict was **fix first** (GPT-6.1 Sol). All three High findings accepted: two changes reverted and replaced, one
reworked; Medium findings 4-6 addressed; Low 7 deferred.

| # | Finding | Disposition |
|---|---|---|
| 1 High | D credited a queued plant's connectors, but placement searches see existing buildings only; the plant is an action executed at turn end (AIActions.EndTurn), so a Mine queued after it could be placed unconnected | **Reverted.** Replaced by `EconomyDirector.SlotStarved`: in the opening (T1-T3), with no free slot, energy not short and a plant already queued this turn, no further plants; the material waits for next turn's Mines (T1 Guts: 3 Solar Plants for 1,200 of 1,350 material). Later turns unchanged. A real plant-Mine dependency is deferred. |
| 2 High | Robotic Mining unlocks Yield2, but on a limited planet the director keeps the second upgrade slot for the refinery and rejects a second yield multiplier, so the 1,100-science detour buys nothing | **Accepted; the research reorder is reverted** (v0.13 order). "Upgrades every existing building" was wrong: research unlocks, the director installs, and this path is blocked by policy. |
| 3 High | The reorder pushed Missiles to ~T24, Optics/telescopes and Ultracapacitors later; the single-target queue left 1.3-1.6k science banked | **Fixed.** Order reverted; `Research.QueueDepth` 3 queues three plan targets (with prerequisites) at each empty-queue boundary, so surplus science advances (Sol: Missile T24 -> T22 from continuity alone, under the reordered plan). |
| 4 Med | 0.25 is a reasonable interim floor, but desired already included remembered weapons and damage; quantity alone is not established as the limit (71.6% building-value coverage vs 13/16 impacts with no defense nearby) | **Kept 0.25 as interim; diagnosis corrected** (desired 27 at T22 was mostly damage-driven). Next: coverage of the critical core (cities, power, weapons) by impact relevance, Kinetic range upgrade (5.9 -> 7.67, +69% area for 50 energy / 250 material), funded rolls. |
| 5 Med | Attrition detects lost operational capacity (unpowered, sold), not destruction | **Fixed.** Attrition now also requires an attack last action phase (recent damage or impacts on us). Still capacity-loss fire by design. |
| 6 Med | AiProcessorResources description wrong (game budget 1200 total, not 200; 1000 would be worse than native at 26 weapons; does not affect placement) | **Fixed.** Description corrected; default 2600 (Pavol's decision, frame-rate trade-off); each AI turn logs `aim-budget` (total, AI weapons, steps, native steps). |
| 7 Low | Backoff release counts "queued with no failure", not confirmed placement; end-turn cancellation is unreported | **Deferred** (bounded effect: at most two queued a turn while throttled). |

Ranked next moves (Sol): 1) protect an operational core before T20 (coverage by impact relevance, Kinetic range upgrade,
funded rolls); 2) a usable research critical path (multi-queue done; buy science projects; drop blocked detours);
3) a real connected plant-Mine opening; 4) measure trajectory work and accepted shots under controlled budgets, keep
Missile access and bounded arsenal replacement; 5) value intel upgrades/projects against visibility thresholds
(Telescope 8 intel, x1.5 upgrade 12; the human's counterintel reached x7.64).

Tests: 395 checks; mutations of the v0.14.1 rules caught (v0.14.0: one equivalent mutant).
