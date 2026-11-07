# Terra v0.17.1 review - disposition

Review: `terra-ai-v0.17.1.md` (GPT-6.1 Terra, read-only, 7 Nov 2026), verdict **SHIP AFTER FIXES**. Diff: `terra-ai-v0.17.1.diff`
(the level gate, against 3294356). Terra confirmed that with a stable level assignment and lasers off, every registered hook
leaves an original AI on native behaviour. The pre-fix build (md5 8c0de4ec) is installed for match 21; the fixes ship as 0.17.2.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | High | The laser patches (transpiled bans, three prefixes) have no owner check, so `EnableLasers` would change the original AIs | **Fixed.** Lasers install only when `SmartLevels` covers all four levels; otherwise a warning is logged. Lasers are off by default. |
| 2 | High | A misplaced comment dropped `acc` from the `init` record; `summarize_decisions.py` raised a KeyError on it | **Fixed.** The field is restored, and the summarizer tolerates its absence (match 21's log) and labels original AIs. |
| 3 | Medium | A live `SmartLevels` change could leave a half-smart AI (aim window, released firing energy) | **Fixed.** Read once at game start; the description says a restart is needed. |
| 4 | Medium | A propagated exception was reported as "swallowed"; the dedupe key could hide a later smart-AI stack trace | **Fixed.** `swallowed` is in the record, the log line and the monitor; the dedupe key includes AI and disposition. |
| 5 | Low | Null `PlayerData` on the owner in the finalizer | **Fixed.** |

**Caveat kept (no change):** with lasers off, the processor budget still counts original AIs and their weapons when it
splits the time budget for the Smarter AI. That's a lobby-size effect, not a behaviour change for the originals.
Native Hard aims at 0.9 and our AI at 0.7/0.85, so the comparison is between the configured lobby opponents.
