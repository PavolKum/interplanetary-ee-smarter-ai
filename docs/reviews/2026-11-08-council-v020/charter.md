# Council charter: what comes after v0.19.0

**Date:** 2026-11-08 13:10. **Facilitator:** Claude Opus 5.5 (the session running this; it reviews alongside the
lanes and owns no load-bearing measurement). **Accountable human:** Pavol, who decides.

## Question

What should v0.20 change, and the 2-3 versions after it, in what order, and how should each change be tested?

**Output:** a decision record with:
- a ranked v0.20 plan;
- a sketch of v0.21-v0.22;
- a test plan with falsifiable exit gates;
- the decisions reserved for Pavol.

## Boundary

- **In scope:**
  - the Smarter AI mod (`AI/`, its tests and tools);
  - the playtest evidence for matches 22-26;
  - the scoreboard (`docs/PROGRESS.md`);
  - the game rules in `decompiled/` and `docs/ruleset/`, read locally.
- **Out of scope:** telemetry mod changes, public release, the public-repo audit, videos.
- **Fairness rules (non-negotiable for any recommendation):**
  - strict visible-only targeting;
  - no hidden enemy values;
  - human-like accuracy;
  - no resource cheats.
  - Only Smarter AIs (`Plugin.IsSmart`) change; original AIs run native code.

## Authority

- **Read-only on the repository.** No builds, no installs, no test runs that write tracked files, no commits by lanes.
- Lanes may run read-only analysis (python reading the jsonl data, grep, git show/log).
- The facilitator writes only into `docs/reviews/2026-11-08-council-v020/`.
- Spending on OpenRouter/Vercel metered lanes is authorized by Pavol's request for this run.
- **Gemini:** Pavol explicitly requested it, overriding the 2026-10-16 exclusion for this run only.
- **Copyrighted code:** `decompiled/` may be read by lanes; no decompiled excerpts go into the decision record.

## Frozen target

| Item | Value |
|---|---|
| Commit | `4d7c160161e0ea0037ef44474077bd0ff9666a31` (no tracked files modified) |
| Porcelain status digest (untracked only) | `a2f2188c0869c327` (first 16 hex of sha256) |
| Evidence (git-ignored match data, 21 files) | `evidence-manifest.tsv`, `manifest:sha256:824fa979489cce519158b5b93a99e1f14860b82bc4c6cb6cb272aa60818f4b38` |

## Roster (all required signers)

| Lane | Model | Family | Route | Lens |
|---|---|---|---|---|
| A | GPT-6.1 Terra | OpenAI | OpenRouter `openrouter/openai/gpt-6.1-terra` (metered) | Code and game mechanics: where do the claimed limits (placement, gun cap, facing, war flip) live in `AI/` and the game rules, and what fix is feasible? |
| B | GLM-5.3 Flash | Zhipu | Vercel `vercel/zai/glm-5.3-flash` (metered, cheap) | Measurement: re-derive the match 22-26 numbers from the jsonl logs. What actually limits AI damage? Denominators. |
| C | Gemini 3.8 Flash | Google | OpenRouter `openrouter/google/gemini-3.8-flash` (metered) | Strategy, balance and experiment design: what beats a skilled human, and how many matches of which lobby show it? |
| D | Fable 5.1 | Anthropic | native subagent | Adversarial roadmap: is placement really the top lever? Steelman the alternatives (war timing, defense economy, superweapons, planet budget, target choice). |
| E | Sonnet 5.5 | Anthropic | native subagent | Verification and risk: the test harness, in-game verification, fairness regressions, scope control, the cost of each item. |

## Correlation risks

- **Shared inputs:** all lanes get the same brief and the same facilitator-written playtest notes. The notes carry the
  facilitator's framing, for example "placement first".
- **Same family:** D and E are both Anthropic and share the facilitator's family. **D+E agreement does not count as
  cross-family concurrence.**
- **Shared harness:** A, B and C run through opencode.

## Budget

| Round | Count | Wall-clock limit per lane |
|---|---|---|
| Sealed evidence pass | 1 | 40 min |
| Cross-review rotation | 1 | |
| Ballot round | 1 | |
| Strongest-objection round | 1 more, only if a material factual split remains | |

- One retry per failed lane.
- If the budget runs out: `INCOMPLETE`.
- `NO ACTION` (keep v0.19.0 and play more matches) is a valid outcome.

## Claimed end state

The target is a `CONSENSUS RECORD`.

If fewer than 2 non-Anthropic lanes produce sealed reports, the run downgrades to `ADVISORY REVIEW`.

## Success rubric for recommendations

Each recommended action must name all of these:
- the evidence it answers (file and match/turn);
- the mechanism;
- the files/subsystems touched;
- a falsifiable exit gate measurable from the decision log or telemetry;
- its fairness-rule check.

## Output gate

A report counts only if it:
- is at least 2,500 bytes;
- contains a line with "verdict";
- ends with `END OF REPORT`.

Empty or truncated output is a failed lane.
