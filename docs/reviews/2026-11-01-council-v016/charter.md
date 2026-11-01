# Council charter: v0.16, beat Pavol's build-out

Requested by Pavol, 1 Nov 2026 18:18: "let's spin up Fable and Sol on xhigh - see what you can cook up together to beat me", plus "you can also add Gemini Argon".

## Decision question
What change set for Smarter AI v0.16 gives the AI the best chance to beat Pavol's usual build-out (match 16 shape), within the constraints below? Output: a ranked list of at most 6 changes. Each has a mechanism, evidence, expected effect, risk and a falsifiable next-match gate. The list also says what not to do.

## Constraints (non-negotiable)
- Fair information only: no hidden enemy values, no omniscience. Pavol, 1 Nov: strict visible-only city targeting; nothing is remembered once a city leaves sight with shadows off. Counterintel stays a hard counter.
- Human-like accuracy (level-1 0.7; guided 0.85 by Pavol's decision). No resource or rule cheats.
- Install only while the game is closed. Implementation needs Pavol's go-ahead after this council.

## Boundary
- **Target:** repo `~/Repos_Local/interplanetary-mods` at commit `24683684d9799b9b22db765c251ac811ef8793ae`.
- **Untracked files:** `docs/original-mechanics.md`, `docs/ruleset/`, two old review attempts; porcelain status sha256 prefix `7c183bf4e6deac66`.
- **Git-ignored inputs, treated as frozen:**
  - `decompiled/` (game source, authoritative);
  - `docs/playtests/data/v0141-*`, `v0151-*`, `v0153-*` (manifest `data-manifest.txt`, sha256 prefix `1b9990d0074cc6ad`);
  - `docs/strategy/`.
- **Lanes are read-only.** Containment is instruction-only (they need the ignored inputs, so there's no detached worktree). The evidence is a before/after porcelain diff.

## End state
- **Target:** CONSENSUS RECORD. Every lane confirms that the exact decision record preserves agreements and splits.
- **Fallback:** ADVISORY REVIEW if a lane fails or sign-off is incomplete.
- NO ACTION is a valid outcome.

## Roster (seated before evidence)
| Lane | Configuration | Family | Lens |
|---|---|---|---|
| A | Fable via `claude -p --model fable --effort xhigh`, read-only tools | Anthropic | Game math and economy: opening build order, energy/material curve, research and military timing against Pavol's curve, from `decompiled/` and the ruleset |
| B | GPT-6.1 Sol via opencode `--variant xhigh`, plan agent | OpenAI | AI code and runtime: which existing systems misfire in the data, code-level changes and their risks |
| C | Gemini 4 Argon via OpenRouter (metered), plan agent | Google | Adversarial player model: how Pavol wins (matches 13-16), his weak windows, a counter-strategy the AI can execute with fair information |
| F | Facilitator: Claude Opus 5.5 (this session) | Anthropic | Charter, ledger, cross-review routing, synthesis. Owns no load-bearing measurement |

**Correlation risks:**
- A and F are the same family.
- All lanes share one prompt packet and corpus.
- Gemini was excluded from reviews since 2026-10-16; this run is Pavol's explicit override.

**Recusal:** only with Pavol's approval. A failed lane is marked failed and is not silently dropped.

## Budget
- **Rounds:** one sealed pass (parallel), one rotated cross-review A→B→C→A that includes Round-1 ballots on the issue matrix, and one sign-off pass.
- **Time limits:** 50 min per pass, 1 retry per lane.
- **Spend:** Gemini is metered per token, capped at these three passes.
- **On overrun:** budget exhaustion ends INCOMPLETE.

## Output gate
A report is accepted only if it is at least 3,000 bytes and ends with the line `END-OF-REPORT`. A report that fails is a failed run, not an abstention.

## Amendment 1 (18:27, before any report sealed)
- **Lane C (Gemini 4 Argon) recused, with Pavol's approval.** OpenRouter credit was $4.38 against an estimated $3-8 lane cost. A lane that runs out of credit returns an empty review. Pavol chose "Drop Argon this time". The first launch was stopped at 18:24 before producing output: `laneC.md` is 0 bytes and its process is gone.
- **Council is now two lanes:** A (Anthropic) and B (OpenAI). That still qualifies as CROSS-FAMILY CONCURRENCE.
- **Lane C's lens is unassigned.** Cross-review asks both lanes to cover it explicitly: Pavol's weak windows and his likely adaptation.
