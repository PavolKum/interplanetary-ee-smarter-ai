# Sol v0.12 review: disposition (AI 0.12.1)

Verdict was **fix first** (GPT-6.1 Sol, strategy and code). Three High findings fixed, two Medium fixed, two Medium
deferred with reasons, the Low finding fixed.

| # | Finding | Disposition |
|---|---|---|
| 1 High | The dry-planet state (adapter material income 0) never counted as saturated; global material projects still valued nominal production on a dry planet | **Fixed.** `EnergySaturated` treats income <= 0 as binding; sustainable material now includes the bank ((planet + stock) / turns to T45), and on a dry planet the bank rate is what energy must convert. `PlanetMaterialFactor` is 0 on a dry planet and now scales project extraction gains too. Test with Brother's exact T26 state (income 0, 75.7k banked, material glut, no energy glut). |
| 2 High | The reach gate can lock the commit on a target nothing aims at; max-hold never rescues it (the saturated branch wins first) | **Fixed.** Per-enemy low-yield memory: a turn with >= MinWeapons pointed at the commit and < AbortBelowWeapons primary trajectories counts (hold turns included); after 2 running, the target's value x0.25. Max-hold is checked before saturated, so after 5 held/aborted turns the strike fires whatever aims (exempt from the abort). |
| 3 High | Mine selling rejected every powered mine (IsPotentiallyDistributingPower is true for any powered building) and ran after the native power grid built its caches and searches | **Fixed.** Selling moved to a prefix on `AICore.BeginTurn`: before `AIResources` snapshots material (proceeds counted once, natively) and before `AIPowerGridMaintaining` builds caches. Leaf test: no generation, at most one power connection (`ConnectedPowerSlots <= 1`). Hook count 15. |
| 4 Med | "Keep the planet to T45" is not a whole-game budget; the 15k floor binds only `Best`, military branches bypass it; depletion itself is not fatal if the material stays banked | **Partly addressed, rest deferred.** The bank now enters the energy requirement (#1). A rolling 6-10 turn spending forecast with separate operating and emergency allocations is the next step; measure material at depletion and at T35/T40 in the next match first. Repair and ruin clearing remain native stubs. |
| 5 Med | Zero energy-gain weight under saturation could trigger before war and reject Reactor Trials | **Fixed.** Saturation is no longer a weight: it filters only energy-only options (power plants, energy yield upgrades) in `Best`. Projects (Trials) are never filtered. Zero energy weight remains only for energy glut (energy actually unspent last turn). |
| 6 Med | The 40 s window adds little: 18 of 26 strike decisions were on human turns already longer than 40 s; per-weapon steps stay at the 30 floor | **Accepted, kept as an experiment.** The real lever is steps per frame (`ReallocateDesignatedStepAmount`), which costs frame rate on the human's machine; not changed without measuring. Next: log search work per turn. Diagnosis corrected in the playtest note (aim rate 36% of live weapons, not 21% of retained entries; allowance clamp(1200 / total AI weapons, 30, 100)). |
| 7 Med | Abandoned weapon placements kept phantom cap slots | **Fixed.** Abandonment decrements `AimedWeapons`, blocks that type for the turn and reopens the weapon branches. Ranking upgrades by damage per building at the cap: deferred. |
| 8 Low | Fire log `weapons` still counted retained entries; tests cover Brain only | **Fixed** (`retainedEntries` renamed; `powered` counts live weapons). Adapter states (selling, timers, cap refunds) remain unverified in game. |

Next moves from the review (for v0.13): bank-aware allocation with a recovery reserve; an arsenal the search can serve
(measure search work and accepted firing actions); targets by progress toward winning, including the third-player
consequence; recovery around the fighting core rather than building count; controlled comparisons with fixed thinking budgets.

Tests: 324 checks; 29 mutations of the v0.12 rules all caught.
