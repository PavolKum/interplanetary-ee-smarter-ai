# AI v0.5.0 code review - disposition (GPT-6.1 Terra xhigh, 31 Oct 2026)

Report: `terra-ai-v0.5.md` (verdict: fix first). All findings fixed in v0.5.0 before install (commit after 85aa072).

| # | Finding | Disposition |
|---|---|---|
| High 1 | Floor defenses get no energy reserve when no weapons are known and no damage taken | **Fixed**: `DefenseReserve` funds at least one roll per own defense (`minRollsPerDefense` 1), still capped by `DefenseReserveShare`. Test: 21 blind undamaged defenses -> 3,150; mutant caught |
| High 2 | Counter table hard-coded for Enhanced; Classic: Kinetic stops Missiles, Shield doesn't, no Ion | **Fixed**: table loaded from the active ruleset (`BuildingsData` defense_functionality -> `DefensesData` defends_against/defense_probabilities) at match reset, logged once; Enhanced table kept as fallback. Same table feeds defense choice and weapon scoring. Untested in Classic (adapter) |
| Medium 3 | Memory summed across enemies; dead enemies' weapons linger | **Fixed**: memory per enemy and weapon type; enemies no longer alive are dropped. Tests: A hidden + B seen = 39 not 20; A eliminated -> forgotten; mutant caught. Not done: reconciling observed destruction (hidden is not destroyed; kept simple) |
| Medium 4 | Catch-up can crowd out offense/growth on a small planet | **Fixed**: without damage taken, catch-up uses at most half the free slots (never below the base rate); under attack the full catch-up. Tests + mutant |
| Medium 5 | A failed defense type blocks the whole category; abandoned defenses stay counted as queued | **Fixed**: failed ids excluded before choosing; abandonment decrements `DefensesQueued` |
| Low 6 | Migration rewrites a deliberate MaxDefenses=40 every startup | **Fixed**: one-time `[General] ConfigMigratedTo` marker |
| Low 7 | Tests check formulas, not integration; choice test used a fresh dictionary | **Partly**: the choice test now uses the memory's output; adapter/integration still untested (next match is the test) |

119 checks pass; 7 mutants (floor, catch-up, slot bound, reserve floor, memory, dead enemies, blind weights) each fail.
