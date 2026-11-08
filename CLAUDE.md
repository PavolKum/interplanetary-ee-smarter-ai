# interplanetary-mods - working notes for agents

BepInEx mods for Interplanetary Enhanced Edition. The main one is `AI/` (Smarter AI): read `AI/README.md` first,
then the latest `docs/playtests/` note and `docs/reviews/*-disposition.md`.

## Hard rules

- **Never deploy over a running game.** Since v0.20 a plain `dotnet build AI -c Release` never deploys; install with `-p:Deploy=true`, which fails while `Interplanetary.exe` runs.
  A plain `dotnet build AI -c Release` copies the DLL into the game's `BepInEx/plugins/InterplanetaryAI/`. Tell Pavol before installing.
- Commit, never push unless asked.
- `decompiled/` is the source of truth for game behaviour. Check a claim there (file:line) before coding on it.
- A new test suite that is green on its first run is suspect: break the code on purpose (mutation script pattern:
  apply a one-line change to `AI/Brain/*.cs`, run `dotnet run --project AI/Tests`, restore) and see it go red.

## Playtest loop

1. Pavol plays an offline match against the AIs.
2. AI side: `python AI/tools/summarize_decisions.py` (reads the newest `BepInEx/InterplanetaryAI/decisions-*.jsonl`).
3. Human side: the telemetry recording `BepInEx/plugins/InterplanetaryTelemetry/recordings/<session>.jsonl`
   (per-turn resources, buildings, research queue). Debriefs from the recorder live in `~/Repos_Local/ScheduleImodes/interplanetary/debriefs/`.
4. Write the findings to `docs/playtests/<date>-<version>.md`, fix, review, install before the next match.

## Second-opinion review (GPT-6.1 Terra, authorized by Pavol)

Command (read-only, from the repo root; save the prompt under `docs/reviews/` first):

    python AI/tools/terra_review.py <name>

It wraps `opencode run -m openai/gpt-6.1-terra --variant xhigh --agent plan --dir .` with guards, because Terra fails
silently (31 Oct 2026: two runs hung with nothing written; two exited 0 after two progress notes when a read outside the
repo was auto-rejected). It kills a run whose trace stays empty for 3 minutes, accepts only a report with a "verdict" line,
retries once, keeps failed attempts as `<name>.attemptN.*`, and exits non-zero without an accepted report. Put "Stay
inside this repository" in every prompt, and describe any external file it would want (e.g. the telemetry recorder).

Then verify each finding against `decompiled/`, fix, and write `docs/reviews/<name>-disposition.md`.
Track record (Oct 2026): every finding held up when checked; it misses domain assumptions it is not told about (v0.2 missed
"energy resets every turn"); its fixes tend to be heavier than needed.

**Do next time (Pavol, 31 Oct 2026): review the diagnosis, not just the code.** Before writing code from a playtest, give
Terra the match logs (decision log + telemetry recording + the playtest note) and ask it to check the diagnosis
independently: what limited the AIs, what the human did differently, which game rules the planned fix depends on.
And in every code review prompt, list the game-rule assumptions the design rests on so it can test them too.

## Design principle: what "smarter" means (Pavol, 2026-10-31)

- Accuracy is a difficulty knob, not intelligence. Keep the AI's hit rate a setting matched to the player's recorded rate,
  and put the "smarter" effort into economy, research and superweapon timing. Make misses situational rather than
  constant: a uniformly near-perfect hit rate reads as a chess engine, not a human.

## Game facts that have bitten us

- Energy does not carry over: `PlayerResourceHandler.TurnStartInit` sets it to production every turn. Material accumulates.
- AI usable energy = production minus all weapons' firing costs (`AIResources.BeginTurn`).
- Cities give power slots only once a plant powers them; turn 1 can build plants only.
- Native AI construction bugs (see AI/README v0.3): no-slot builds spiral into power plants, water buildings are never placed,
  pre-installed upgrades are dropped at placement. `AIResourceConstruction` is dead code.
