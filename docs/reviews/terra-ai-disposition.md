# AI v0.1 - review disposition (GPT-6.1 Terra, xhigh, two passes, 30 Oct 2026)

Reports: `terra-ai-v0.1.md` (v0.1.0, verdict fix first), `terra-ai-v0.1.1.md` (fix diff, verdict fix first). Every accepted finding was checked against `decompiled/` before changing code.

| # | Finding | Disposition |
|---|---|---|
| 1.1 | Cities have no `hp_current`/`hp_max` stat; `GetStat` returns null, so both targeting hooks threw on the first visible city and fell back to native | **Fixed** (verified `BuildingStatHolder.cs:161-182`, `NyaStatHolder.cs:15-20`). Cities use population (`DealDamageToCity` ruins at 0), other buildings HP. |
| 1.2 | `!= Invisible` accepted Shadow buildings and read their live stats (hidden information) | **Fixed**: only `Visible` buildings are read; strikes with no visible candidate fall back to native. |
| 1.3 / 2.3 | Miss-only weapons go silent at 100% accuracy | **Changed approach**: planned misses are not fired at all (`FireMisses = false`), because an own-planet impact counts as a miss (2.1). Accepted Terra's correction that misses are not always worthless (Asteroid Diversion fires 10 projectiles); left as a config switch. |
| 1.4 | Native bug: hitting/missing lists rebuilt only on a primary registration, never cleared per turn | **Fixed**: `DetermineShootingWeapons()` called right before `TargetWeapons` (`RefreshFiringList`). |
| 1.5 | Fallback not transactional | **Fixed**: plan staged before mutation, logging in separate try blocks, strike prefix never falls through after queuing. |
| 1.6 | Reachability treats a failed random search as impossibility | **Mitigated**: default memory 2 -> 1 turn. Open: record search confidence and secondary successes. |
| 1.7 | Unknown city health scored as weak | **Fixed** + test (seen red without the fix). |
| 1.8 / 2.2 | Reset per Initialize; turn value at init not proven fresh; guards inconsistent | **Fixed**: reset on every `AICore.Initialize` (match start or load) plus a turn-went-backwards backstop in targeting; all hooks use the offline `Active` guard and an `AIPlayer` guard. Memory does not survive save/load (short-lived by design). |
| 1.9 | Weak all-unreachable test | **Fixed**: one-reachable-enemy case added. |
| 2.1 | Refresh can select self-hitting "misses" | **Fixed** by `FireMisses = false` (miss list emptied before firing). |
| 2.4 | Refresh not power-aware | **Open, minor**: a weapon that loses power after finding a trajectory can still be selected. Native has the same exposure on every primary registration. |
| 2.5 | One visible city among hidden ones looks weak | **Open, minor**: model incomplete city coverage later. |

Not verifiable statically (both passes): Harmony installation against the shipped assembly and all in-game behaviour. First playtest checks: `LogOutput.log` shows "6 of 6 hooks patched" and `T<n> <AI>: <k> weapons -> ...` lines, and no "Hook '...' failed".
