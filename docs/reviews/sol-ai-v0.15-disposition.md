# Sol v0.15 review: disposition (AI 0.15.1)

Review: `docs/reviews/sol-ai-v0.15.md` (GPT-6.1 Sol, verdict "fix first", 2 High, 7 Medium, 1 Low).
Build: 0 warnings. Tests: 424 checks, 0 failed (13 new in `V0151Rules`). Mutations: 7 of 7 caught (`scratchpad/mutate0151.py`).

| # | Finding | Disposition |
|---|---|---|
| 1 High | Accuracy count read `AITrajectoryWeapon.TargetableWeapon`, which is null for new weapons during the postfix | **Fixed.** The postfix now takes the native `List<AITargetableWeapon> weapons` argument (its `Weapon` is set in the constructor). If nothing is operational it leaves the native counts alone. `Hits` and `Misses` are bounded by min(total, live), so dead entries no longer hold miss slots. The adapter can't be unit-tested (Unity types). Live check: the `accuracy` record's `guided` must be > 0 on the first Missile volley. |
| 2 High | A city-cover Ion purchase need not cover an exposed city | **Fixed.** (a) A placement is accepted only if its actual position closes a city deficit (`DefenseCoverage.ClosesCityDeficit`, same reach rule as the deficit); otherwise the search continues and the abandon valve refunds. (b) Anchor scoring weights cities below the target; everything else is ×0.05 and only breaks ties. (c) The runaway guard now counts intact Ions near a city, powered or not (cap 3 × perCity × cities), so Ions placed elsewhere no longer block the last exposed city. (d) The director log gains `minCityCover` (Sol's "single fastest measurement"), `antiNukeNearCities` and `antiNukePerCity`. **Not done:** bounded replacement of irrelevant Ions, and per-city reservations across two searches running in the same frame (a queued Ion counts once it is queued). `TryDefense`/`TryCounterDefense` stay outside the anti-nuke cap by design: they are value defenses. |
| 3 Med | One weighted quota does not deliver per-class rates; native order can starve late weapons | **Deferred (v0.16).** The aggregate rate stays and is now described as aggregate. Per-class slots need control of native hit selection (insertion order); superweapon priority was already on the second-tier list. |
| 4 Med | Strategy estimated guided salvos at the level rate | **Fixed.** `Strategy.Decide`/`EstimateSalvo` take an optional per-weapon-id rate map. `StrategyPatches` fills it for guided ids with the same classifier as firing (`AiPatches.IsGuided`). |
| 5 Med | The intel sprint can displace the opening's material projects | **Fixed (bounded).** The sprint's extra intel buildings wait while material < `IntelSprintMaterialReserve` (650 = Telescope 300 + Molecular Recycling 350); the normal intel limit is unchanged. Sol's timing correction is accepted: Telescope Array becomes available around T5, not T4. Validate in the next opening (T4-T8 income vs v0.14.1). |
| 6 Med | `TryCityProject` let the native search pick a ruined city | **Fixed for every director project.** `CityForProject` returns the living city: Normal state, free slot, not installed, not queued, prerequisite projects installed. `QueueProject` enqueues against that city with `AddProjectToQueue`, no longer through the native `tryBuildAffordableProject`. |
| 7 Med | Volley ledger survived match start/load | **Fixed.** `AiPatches.ResetCombatMemory()` is called from `Plugin.ResetMatchMemory`. |
| 8 Med | Guided threat inherits incoming-impact memory from eliminated shooters | **Deferred (v0.16).** Incoming memory is keyed by weapon type, not shooter. In a 1v1 an eliminated shooter ends the match, and the memory decays with `WeaponMemoryDecay`. The fix needs per-shooter memory in the defense ledger. |
| 9 Med | Ledger reserves nominal damage against frozen durability; interception not modelled | **Deferred (v0.16).** It needs observation age and a conservative pass probability (fair information only). The 1.25 overkill stays as a crude allowance. The new strike `weapon` field lets the next match measure delivered vs reserved damage. |
| 10 Low | Logs cannot reconcile assignment with damage | **Partly fixed.** Strike records gain `weapon` (building id) and `wid`. `ownCityPop` is now `id=pop` per city. `idleEnergy` is a planning balance that includes the defense reserve, not unused energy after the action phase. An assignment-to-impact link is still open. |
| - | Config text listed MIRV as second-phase guided | **Fixed.** MIRV has no second phase in this engine and keeps the level rate; it stays in the defensive threat list. |

## Native-contract answers (no change needed)

Confirmed by Sol: `GetShadowBuildingState` is the right frozen source (city HP = population). A Shadow city's transform position is valid for `SecondPhaseTargetWeapon`. `PopulationDamage` includes upgrades. The prefix never returns false without queuing. `Invisible` is a sound never-seen test while shadows are on. The research append fix is correct. Sol independently confirmed the human calibration (60 of 71 Missiles hit the planet).

## Next match: what to measure

1. `minCityCover` in the director record just before the human's first Missile volley. Below 2 falsifies the per-city policy.
2. The `accuracy` record on the AI's first volley after building Missiles: `guided` > 0 and `hits` ≈ 0.85 × guided + level × unguided.
3. Opening T4-T8 material and income vs v0.14.1, and the turn of the first city seen (`unseenCities` dropping).
