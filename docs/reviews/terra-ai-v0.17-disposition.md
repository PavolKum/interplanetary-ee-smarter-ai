# Terra v0.17 review - disposition

Review: `terra-ai-v0.17.md` (GPT-6.1 Terra, read-only, 7 Nov 2026), verdict **SHIP AFTER FIXES**. Diff reviewed:
`terra-ai-v0.17.diff` (working tree against 22b55b9 plus `AI/TurnGuard.cs`). Tests went from 554 to 586 checks. 15
mutants (each new Brain rule broken on purpose) were all killed. The fixes were not re-reviewed.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| H1 | High | The turn guard swallowed exceptions in online games and with the mod disabled | **Fixed.** Both finalizers return the original exception when `!Plugin.Active` (enabled and offline). |
| H2 | High | Swallowing a module failure leaves that module half-begun; a core-level swallow happens after the postfix was already skipped | **Partly fixed.** Core level: logged and **re-thrown** (no proven recovery path). Module level: still logged and swallowed. Propagating stops every later module, and in match 20 that meant no construction for ten turns, which is worse than one half-begun module. The stack trace now reaches LogOutput.log once per module and exception type, and a `turn-start-error` record goes to the decision log every time, so the next occurrence names the module. Per-module safe states wait until we know which module fails. |
| H3 | High | Placement fills the ground near cities that their defenses need (Pavol: defenses first, then fill) | **Fixed.** Keep-out zone: a non-defense spot within `ShelterCityKeepOut` (0.5) × the defense radius of a city's edge is skipped during the first `ShelterCoveredSteps` (6) steps and taken only in the fallback steps. Closeness now counts from the city's edge (`Shelter.NearestEdge`), so the best uncovered spot sits just outside the keep-out. |
| M1 | Medium | Rare Material can overlap an early war (`WarTurnFor` moves from T20 to T16 when Orbital Assembly lands); lead 0 is not "a few turns before" | **Accepted, logged.** Pavol's own copies ran until his war began (T16-T21), so lead 0 matches what he did. If the war arrives early, the overlap costs ×0.95 energy for a few turns. The director log has `rareMaterial`, `war` and `warTurn` to measure it. `RareMaterialEndLead` is configurable. |
| M2 | Medium | Geometric cover counts unpowered or wrong-type defenses | **Accepted as a caveat.** Shelter needs the ground that defenses reach; most unpowered defenses are this turn's queue and get power. The log fields are named `coveredBuildings` and `coveredCities` (geometric reach), not "protected". |
| M3 | Medium | Shelter and defense searches shared one per-frame candidate budget, so shelter searches could delay defenses; the model was rebuilt every step | **Fixed.** Shelter has its own budget (60 candidates a frame) and a snapshot per AI per frame (`ShelterModelFor`). The defense budget (120) is untouched. |

## Notes
- Terra confirmed the following:
  - The anchor-method signatures and the plant power registration match the native code.
  - Connected candidates use the game's anchor eligibility plus the line-length check.
  - An allowed Rare Material copy still falls through to the v0.16.1 Jamming-room rule.
  - Placement adds no new enemy information.
- **Unverified in game:**
  - placement time per turn;
  - whether the ring-anchor mix lowers the city-cover give-ups (match 20: 5 of 9);
  - how full the base gets.

  The director log fields `coveredBuildings`, `coveredCities`, `shelteredPlaced` and `unshelteredPlaced`, plus the
  `cover-search` records, measure them.
