# AI v0.6.0 code review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.6.md` (verdict: fix first; the first run hung, the second finished after being restarted). Fixed in v0.6.1.

| # | Finding | Disposition |
|---|---|---|
| High 1 | Observed reserve can eat the recovery budget (Turing T33 and X T36 had 0 energy to build with) | **Fixed**: `CapReserveForRecovery` leaves construction at least `DefenseRecoveryShare` (20%) of income. Tests + mutant. Superweapon `BuyPending` protection unchanged (only when affordable) |
| High 2 | Shooter samples pooled across opponents | **Fixed**: samples keyed shooter + defender + weapon; shots at oneself excluded; the weapon pick uses only the selected target's samples. Tests + mutant |
| High 3 | Failed land plants use up the fallback quota | **Fixed**: `Abandon` gives the plant count back so the water-plant fallback can run. Not covered by a test (adapter); the next match's queued roles show it. Terra's second point (a random water plant does not guarantee a power-line connection) stands |
| Medium 4 | "Unpaid rolls" are not the energy needed (energy OR material; every overlapping defense reports one) | **Fixed in part**: unpaid rolls count at most once per impact with an eligible defense; named `NoResource`; still a bounded estimate, not a measurement. Per-defense cost (Ion 200, city 250) not yet used |
| Medium 5 | City defenses observed but unfunded | **Fixed**: observed spending can fund a reserve with zero defense buildings |
| Medium 6 | Pass estimate needs uncertainty and exploration | **Fixed in part**: observed pass is blended with the estimate from remembered defenses (`ObservedPriorWeight` 6). Deferred: exploration picks for weapons never sampled, per-projectile (not per-fragment) counting |
| Medium 7 | Defense memory not pruned for eliminated enemies | **Fixed** |
| Low 8 | "Uncovered" is weapon-relative | **Fixed**: renamed `NoEligibleDefense` in code and wording; the log key keeps `lastUncovered` for continuity with the v0.6.0 data |
| Low 9 | Tests omit integration | **Partly**: pure parts covered (155 checks, 7 more mutants); the Harmony hook was exercised by the v0.6.0 match (14/14 hooks, ledger populated) |

Terra's "checked and fine": hook signature and accounting, once per fragment, timing, fairness of shooter-side events, Classic has no Floating Plant (handled by `HasBuilding`).
