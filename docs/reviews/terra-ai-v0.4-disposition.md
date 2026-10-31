# AI v0.4.0 code review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.4.md` (verdict: fix first). Fixed in v0.4.1. Rule verdicts R1-R6 TRUE (R1 with a city-count
caveat, fixed); R7 TRUE for the enhanced ruleset, nine plan ids missing in classic (skipped by the plan logic).

| # | Finding | Disposition |
|---|---|---|
| 1 | Endgame bank can freeze an AI forever (goal never appears; tiny planet) | **Fixed**: bank only while research toward a not-yet-available superweapon is queued (`SuperweaponResearchQueued`), and never more than half the stock |
| 2 | Refinery counted as production income, even on an empty planet | **Fixed**: valued as planet material saved (only while drain cost > production, never on an empty planet), flagged `SavesReserve`, never added to forecast income |
| 3 | Blind "spread fire" still sent 70% at the top score; the test checked scores, not assignments | **Fixed and proven**: focus scales with top/second score ratio (none below 1.5x, full from 2.5x). New assignment test failed on the first fix (attacker 680/1000), passes now (under 600, others >= 200 each); fixed-focus mutation caught |
| 4 | Defense reserve can block a pending superweapon without asking for power | **Fixed**: reserve capped so the pending purchase stays affordable; if energy still falls short, the goal's material is held and an energy push is raised |
| 5 | Refinery slot policy inconsistent (research timing, Yield2 after refinery, offshore) | **Fixed**: on a limited planet a drain building takes at most one yield upgrade; the refinery when it actually saves drain |
| 6 | Intel caps not global; abandoned telescopes still counted | **Fixed**: one cap (MaxIntelBuildings, IntelPerTurn) on every intel branch; abandon decrements the turn count |
| R1 caveat | City count included non-Normal cities; overlay uses `GetRemainingCities()` | **Fixed** |

Untested (adapters, game state): bank release, intel caps, refinery slots, resource ordering at end of turn. Next match is the test.
