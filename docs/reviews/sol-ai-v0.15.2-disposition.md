# Sol v0.15.2 review: disposition (AI 0.15.3)

Review: `docs/reviews/sol-ai-v0.15.2.md` (GPT-6.1 Sol, verdict "fix first", 2 High, 5 Medium, 1 Low + data corrections).
Build: 0 warnings. Tests: 443 checks, 0 failed. Mutations: 5 of 5 caught (`scratchpad/mutate0153.py`).

| # | Finding | Disposition |
|---|---|---|
| 1 High | Stale weapon objects (destroyed, building component pooled and reused) still counted as live | **Fixed.** `AiPatches.RegisteredWeapons` (the player's `WeaponHandler.Weapons`) and `IsOperational` (owner, Normal, powered). The accuracy postfix counts registered weapons only. It also **removes stale entries from the goal's `m_weapons` before hit selection**, so they cannot take hit slots or be fired. The firing diagnostics use the same rule, and the `accuracy` record gains `stale`. The adapter can't be unit-tested. Live check: `live` should match the strategy's weapon count. |
| 2 High | `underAttack` uses the decaying grudge, so one hit enables return fire for the rest of the match | **Fixed.** `DefenderStats.EnemyImpacts` counts impacts whose shooter is another player, recorded before the self-shot return. `Decide(..., hitLastPhase)` drives return fire; the grudge-based `underAttack` still drives attrition. Test: grudge without impacts last phase → no return fire. |
| 3 Med | The energy guard leaves the motivating opening unchanged (blind bypass, counterintel projects spend the plant) | **Fixed (bounded).** `EconomyDirector.IntelGuarded`: native BelowMinimum stays exempt; when an enemy is blind, the first intel building of the turn is free and the rest are guarded; unseen-only intel is guarded from the first. Until `IntelSprintUntilTurn`, counterintel city projects must leave `OpeningPlantReserve` (250) energy. Not a full reserve across every priority step; the next opening's T5-T10 energy is the check. |
| 4 Med | The Ion purchase gate still compares all intact Ions with the cap | **Fixed.** The gate uses `AntiNukeNearCities + queued < AntiNukeCap`. |
| 5 Med | Late material collapse: repairs and reconnection take material before new extraction | **Deferred (v0.16).** It needs a recovery mode that protects one useful mine's material and ranks damaged mines above discretionary arsenal repairs. Recorded in the playtest note as loss reason 5. |
| 6 Med | Return fire: no useful-hit check; 0.6/4 uncalibrated; "waiting buys nothing" is false | **Fixed in part.** Return fire is now subject to the firing-time abort (too few weapons aimed at the committed target), and the comment is corrected. 0.6/4 stays provisional. Next-match measurement: human city damage per 1,000 energy paid on the first three return-fire turns. |
| 7 Med | Once-seen cities returned to Invisible; shadows were probably off | **Instrumented.** The `init` record logs `shadows` (`GameSettings.SeenBuildingsShadows`). Remembering coordinates with shadows off (from genuinely visible observations only) waits for that measurement. |
| 8 Low | Ring geometry and same-frame reservations check out; diagnosis and diagnostics need correcting | **Done.** `cover-search` gains `deficient` and `anchors`. Playtest note corrected: T26-T27 cover protected 2 of 3 cities; 7 Telescopes stood; weapons 22 → 6 (Railguns 21 → 11); output lead not continuous; 32 techs at T38; shot counts. A connecting-plant response to repeated no-anchor failures waits for the new log. |

## What to measure next match
1. `accuracy.live` vs the strategy's `shots` (should match) and `accuracy.stale`.
2. Return-fire turns: human city population lost per 1,000 energy the AI paid, on the first three.
3. `init.shadows`; `cover-search` outcomes with `anchors`; T5-T10 energy against v0.14.1 Bolide.
