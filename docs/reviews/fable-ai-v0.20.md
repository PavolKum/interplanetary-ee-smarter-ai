# Code review: AI v0.20.0 (Fable 5.1, replacing the Terra pass)

Scope: `docs/reviews/terra-ai-v0.20.diff` (git diff HEAD -- AI) plus the untracked `AI/tools/verify_gates.py`,
`run_mutants.py`, `mutants.tsv`, `backup_match.py`, read against the council decision record rev 2 (sections 5-7) and
`decompiled/` for game behaviour. Read-only; nothing was built or run. Line numbers are those of the working tree.

## What checks out (so the findings below are read in proportion)

- **I2 turn ordering.** `EconomyPatches.CoreBeginTurnPostfix` calls `StrategyPatches.BeginTurn` (`AI/EconomyPatches.cs:161`)
  and then `DirectorPatches.BeginTurn` (`:230`) with the same `recentDamage` local; nothing mutates it in between. The
  strategy is therefore the first caller and the director gets `s.LastWar` back (`AI/Brain/Director.cs:128`). A turn start
  seen twice is safe on both sides (`AI/StrategyPatches.cs:157`, director rebuilds `DirectorState` but the memory answers
  the same). Memory reset on every `AICore.Initialize` (`AI/Patches.cs:40`) and on a turn going backwards
  (`AI/Patches.cs:136`) clears `MatchMemory.Retaliation` with the rest. A turn gap drops a pre-latch retaliation
  (`Director.cs:142`), which is the conservative side. `Latched` is only ever set under `turn >= latchFromTurn`
  (`Director.cs:136-140`): no pre-T12 forever-war. The upkeep cap still keys on `d.Turn >= d.WarTurn`
  (`AI/DirectorPatches.cs:898`) and `d.Defensive` still uses the stateless overload (`:434`), as the record requires.
- **S1 booking.** The `LatePulse` branch of `ReportFired` (`AI/StrategyPatches.cs:131-135`) is the hold branch (`:141-144`)
  verbatim: `HoldTurns++` when war and shots, `LastStrikeTurn` untouched, `LowYield` updated above it in both cases. The
  abort path is identical too (lists cleared, `fired == 0`, `HoldTurns++`). `Strategy.ShouldAbort` (`AI/Brain/Strategy.cs:270`)
  exempts only `max-hold`/`attrition`, so `late-pulse-fire` is abortable exactly like `late-free-fire`. `AimWatch.FiringTurn`
  reads only `Fire` and `CommitTargetId` (`AI/AimWatch.cs:78-83`); `HoldFire`, `PrecisionStrike`, `CommitTarget` do not read
  `Reason`. One `AIWeaponTargetingGoal` per AI (`decompiled/Interplanetary.AI/AIWeaponTargeting.cs:24`), so `LateFreeFire`
  runs once per turn.
- **CoreEndTurnPrefix.** Object path confirmed: `AICore.Construction` (public, `decompiled/.../AICore.cs:44`) ->
  `AIConstructionQueue.m_buildingPlacementFinder` -> `AIBuildingPlacementFinder.m_ongoingSearches : List<AIBuildingPlacementSearch>`
  (`decompiled/Interplanetary.AI.Construction/AIBuildingPlacementFinder.cs:11`). `AIConstructionQueue.EndTurn` calls
  `StopAllSearches()` which clears the list (`AIConstructionQueue.cs:141`), and a Harmony prefix runs before the module loop in
  `AICore.EndTurn` (`AICore.cs:241-245`). Completed searches are removed inside the same `Update` (`Finder.cs:47-57`), so the
  count is cut searches only. Smart-only guard at `AI/EconomyPatches.cs:660`. The hook count is unchanged: 15
  `patched += TryPatch` lines plus `holdHook` = 16 (`AI/Plugin.cs:849`); the EndTurn target is still one `TryPatch` call.
- **Build.** `AI/obj/Release/*AssemblyInfo.cs` carries `AssemblyInformationalVersionAttribute("0.20.0+2f297e2.dirty")` and
  `AI/bin/Release/InterplanetaryAI.dll` is dated 14:03 today, so `StampBuild` and `CheckVersion` evaluate. The DLL in the
  game folder is the 10:55 build: v0.20 is not deployed.
- **Fairness / original AIs.** Every new runtime path is behind `Plugin.IsSmart` (`EconomyPatches.cs:660`, `Patches.cs:48`
  logs only, `AimWatch.Log` is called from the smart branch of `CoreEndTurnPostfix`). Facing buckets and bearings use own
  weapon positions and enemy planet positions (public map data). The late pulse volley spends only the energy the AI
  actually has (`StrategyPatches.cs:379-381`, unchanged from v0.19). Nothing touches accuracy.

## Findings

### 1. `verify_gates.py` I2 gate accepts a post-schedule latch as an "observed latch opportunity" - major

`AI/Brain/Director.cs:163-164`: `WarPhase` calls `RetaliationWar` even when `turn >= warFromTurn`, so an AI hit at or after
its scheduled war turn sets `Latched` and the director record logs `retaliationLatched: true` (`AI/DirectorPatches.cs:3141`).
`AI/tools/verify_gates.py:72-85` then counts that AI as latched and, since war is true from the schedule anyway, finds no
flips and prints **PASS**. The record's gate (5.3) is "zero post-latch reversions in 2 matches *with an observed latch
opportunity*, else UNTESTED"; a latch at T20+ is not an opportunity. Scenario: both Smarter AIs take their first >= 600
damage after T20 (the usual case when the human is passive early): the script reports PASS for I2 with zero evidence.

Fix (both halves): in `WarPhase`, skip the retaliation evaluation when `turn >= warFromTurn` (return true without touching
the state), or pass `warTurn` into `RetaliationWar` and refuse to latch there; in the script, take `retaliationLatchTurn`
from the director record and count the AI only when `latchTurn < warTurn` (`warTurn` is logged at `DirectorPatches.cs:3160`).
Also require at least one `strategy` record after the latch, else UNTESTED (with the strategy disabled, `flips` is empty by
construction).

### 2. `verify_gates.py` does not segment a log by timeline - major

`AI/Plugin.cs:1002`: one `decisions-<start>.jsonl` per game process. A match restarted, or an earlier save loaded, in the
same process writes a second timeline into the same file with repeated `(ai, t)` keys. `verify_gates.py` sorts by `t`
(`:75, :81`) and keys joins on `(ai, t)` (`:90, :98-99`): after a reload the I2 gate sees a latched turn followed by peace
turns from the second timeline (a legitimate reset, section 5.3 "resets on ... an earlier-save load") and prints **FAIL**;
the S1 paid-vs-firing join (`:101-105`) picks whichever `fire` record came last for that key. `smart_ais` even half-knows
this (`:36`, "drops an aborted lobby restarted at T1") but only for the roster.

Fix: split rows into segments at each `init` burst (or whenever `t` goes backwards for an AI) and evaluate per segment,
printing the segment's turn range in the gate detail; or have the plugin open a new decisions file on `ResetMatchMemory`.

### 3. Gates agreed in the record are missing from the script - major

Section 7 says gates and wiring witnesses are fixed in the script before the match they judge. Not implemented in
`verify_gates.py`:

- I2 (5.3): "`aimedWeapons` does not fall on latched turns" (`aimedWeapons` is logged, `DirectorPatches.cs:3155`).
- S1: "pulse turns occur at the control cadence" and "max-hold decisions are not more frequent than control". Both are
  computable from the `strategy` record's `lastStrike`, `holdTurns` and `reason` (`StrategyPatches.cs:452-453`): assert
  `t - lastStrike >= StrikeEvery` on every `saturated` fire and count `max-hold` fires per war turn against the m22-m26
  baseline. These are the two gates that decide whether the pulse clock really is unchanged in play, which the record calls
  "a hypothesis to test, not a consequence".
- S1: "zero eligible pulse-wait holds left unfired" is only approximated by "every candidate hold has an outcome record"
  (`:88-93`). An eligible hold is one whose refusal is `""`; a `late-free-fire-refused` record with `refusal: "energy"` is an
  outcome but also a hold that *could not* fire. Add: zero `late-free-fire-refused` records whose `energyLeft >= reservation`
  and `materialLeft >= materialReservation` (that is, a refusal the inputs do not explain).
- I1a: "every started search reaches a terminal record or an explicit cancel". `cover-search` records with `searchId` and
  outcome exist (`DirectorPatches.cs:2690-2692`) and `searchesOpenAtEnd` is new (`:3143`); the gate is
  `starts - (placed + abandoned) == searchesOpenAtEnd` per AI-turn. Not in the script.

Fix: add the four gates before match 27, each with a selftest row that FAILs on a doctored log, as the existing ones have.

### 4. Latch boundary: a retaliation carried into T12 by hysteresis never latches - minor

`AI/Brain/Director.cs:132-141`: `Latched` is set only in the `triggered` branch. A hit at T11 that decays below the
trigger at T12 (for example 865 / 590 / 529 at T11-T13) gives war, war (quiet 1), **peace** (quiet 2) - the flip pattern
the latch was voted in to remove, one turn before the latch window. The tests pin T8-T11 and T14+ only
(`AI/Tests/Program.cs:1720-1722`), not this boundary.

Fix: after the branch, `if (war && turn >= latchFromTurn && !s.Latched) { s.Latched = true; s.LatchTurn = turn; }`, and a
check `run({11,12,13}, {865,590,529}, 12, 2)` = war, war, war.

### 5. `I1a facing buckets sum to live` compares two snapshots taken at different moments - minor

`live` comes from the last `CoreUpdatePostfix` frame before the AI is ready (`AI/AimWatch.cs:154-168`); the buckets are built
in `Log`, called from `CoreEndTurnPostfix` after every module's `EndTurn` (`AI/AimWatch.cs:235-257`). Between the two, the
power-grid module's `EndTurn` can change `HasPower` (`Usable`, `:206-211`), and the two loops treat a null `TargetEnemy`
differently when `commit < 0` (`:163` counts it, `:239` skips it). Either makes the gate print **FAIL** for a measurement
artefact, and under section 5.1 a FAIL in match 1 forces a replay. `allLiveByFacing` has no independent denominator at all
(it sums to itself).

Fix: compute the buckets in the same loop as `live` (store them on the `Watch` in `CoreUpdatePostfix`) and emit them from
`Log`; or recount `live` inside `Log`'s loop and log both counts so the script can tell the two apart.

### 6. `StampBuild` dirty flag: wrong on two edges - minor

`AI/InterplanetaryAI.csproj:364`: `git status --porcelain --untracked-files=no -- .` ignores a new untracked `.cs` in
`AI/`, which the SDK glob compiles, so such a build is stamped clean. And `Exec`'s `ConsoleOutput` captures stderr as well
as stdout: this repository emits "warning: in the working copy of ... LF will be replaced by CRLF" (seen during this review
on `git diff`), so a clean tree can be stamped `.dirty` and, for `:361`, a warning line could be appended to `GitCommit`.

Fix: `git status --porcelain --untracked-files=all -- *.cs *.csproj 2>nul` and `git rev-parse --short HEAD 2>nul`
(Exec runs through cmd.exe).

### 7. `CheckVersion` fires after compilation - nit

`AI/InterplanetaryAI.csproj:375`: `BeforeTargets="Build"` runs after `Build`'s `DependsOnTargets` (CoreBuild, so CoreCompile
has produced `bin/.../InterplanetaryAI.dll` with the mismatched version) and only then errors. The build still fails and
`DeployToGame` does not run, so the gate holds; the stale DLL in `bin/` is the only leak. `BeforeTargets="CoreCompile"`.

### 8. `run_mutants.py` reports a non-compiling mutant as KILLED - minor

`AI/tools/run_mutants.py:86-88`: `res is None` -> "KILLED (does not compile)" and `ok` stays true. A mutant that does not
compile exercises no test; it is an invalid mutant and must fail the run (the record asks for "exits non-zero on a surviving
mutant" - an invalid mutant hides a potential survivor). All nine current mutants compile, so today's "all 9 killed" stands.

Fix: print `INVALID (does not compile)`, set `ok = False`.

### 9. `CoreEndTurnPrefix` swallows every exception silently - nit

`AI/EconomyPatches.cs:663`: `catch (Exception) { }`. `BeforeEndTurn` reports once internally, but a failure in the guard
line itself (`:660`) disappears. Use `ReportOnce("core-end-turn-prefix", e)` like the postfix.

### 10. Dual plumbing for the pulse-wait switch - nit

`StrategySettings.LateFirePulseWait` (`AI/Brain/Strategy.cs:114`) is set from config (`AI/Plugin.cs:912`) but never read;
`LateFreeFireRefusal` takes `allowPulseWait` as a parameter instead (`Strategy.cs:297`) and the caller passes
`Plugin.StrategyCfg.LateFirePulseWait` (`StrategyPatches.cs:372`). Keep one: read `s.LateFirePulseWait` inside and drop the
parameter (the tests then set it on the settings fixture).

### 11. `verify_gates.py` small things - nit

- `--human` (`:198`) is parsed and passed (`:218`) but `evaluate` never uses it (`:50`).
- DNH "HUMAN-FIRST share of fire turns" and "aim wait" count fire turns from the BeginTurn `strategy` record (`:120`), so
  late volleys are not fire turns there; say so in the gate name or union in the `late-free-fire` turns.
- `fa4` indexes `r["energyLeft"]`, `r["aimedWeapons"]`, `r["materialLeft"]` directly (`:152-154`); a director record missing
  one (older logs) raises instead of reporting UNTESTED. Use `.get` and count the skipped records.
- The FA4 reproduction on m22-m26 (record 5.2) is **unverified here** (no runs were made); the script's roster filter
  (`maxt > 2`) and the "first war strategy turn" anchor (`:146`) are plausible but should be compared number-for-number with
  the council's cross-tab once, and the result pasted into the notes.

### 12. The S1 gate set does not look at the quality of a late pulse volley - minor

A pulse-wait hold keeps the native 20 s aim window (`AI/StrategyPatches.cs:307`); a late pulse volley therefore fires with
whatever found in 20 s, while the control's pulse turn gets `AimingWindowSeconds`. The gates check that every shot is paid
and nothing aborted, not where the shots go. Given FA-level evidence that 10-17 % of paid shots already go to fellow Smarter
AIs (S2), add `primaryFound / powered` and the fellow-AI share of `fire-paid` destinations on late-volley turns against pulse
turns to the S1 set, so a cadence-neutral change that dilutes aim is caught rather than inferred later.

### 13. Tests: the red-before-fix claim is a baseline comparison, and the booking is untested - nit

`AI/Tests/Program.cs:1710, :1718-1719` satisfy "a red-before-fix test on the Ash and Eddie sequences" by calling the retained
stateless `WarPhase` overload and asserting it flips; that is a baseline, not a test that failed on pre-fix code. Acceptable,
but the notes should say so. The `ReportFired` `LatePulse` booking (`StrategyPatches.cs:131-135`) has no test because
`StrategyPatches` is game-coupled; the "exactly reproduces the hold" statement rests on the code reading above.

### 14. `backup_match.py` - nit

`AI/tools/backup_match.py:40-47`: `copy2` overwrites an existing backup of the same prefix without a warning, and the
manifest's own hash is only printed. Refuse to overwrite a differing file (or write `<prefix>-<date>/`), and write the
manifest hash to a sibling `manifest.sha256`.

## Verdict

**verdict: ship with fixes.** The two behaviour changes (I2 latch with hysteresis, S1 late fire on pulse-wait holds) are
implemented as the record specifies, the call ordering and memory reset hold up, the late volley is booked exactly as the
hold it replaces, and nothing touches original AIs, accuracy, or hidden information. What is not ready is the judging
apparatus the record makes a precondition: fix findings 1-3 (and ideally 4-5) in `verify_gates.py` and `Director.WarPhase`
before match 27, since section 7 fixes gates in the script before the match they judge and finding 1 lets I2 pass on no
evidence. Findings 6-14 can follow in the same release or the next.

END OF REPORT
