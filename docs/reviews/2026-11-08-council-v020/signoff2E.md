# Sign-off round 2: lane E (Sonnet 5.5) on `decision-record.md` revision 2

Read-only. `sha256sum decision-record.md` = `5d5b648b20460919e8d4cf39edadc06285dfbf4d308a1dae6bfe68d71a44f727`: matches the artifact id.
I read revision 2 cold, then re-checked it against the logs, the source and the ballots (not the other round-2 sign-offs). My round-1 sign-off does not carry over.

## 1. My round-1 findings: applied correctly?

| Round-1 finding | Where applied | Check | Result |
|---|---|---|---|
| F1 FA4 "strongly associated" unsupported | §4 FA4, §3 lane E bullet, §9 | FA4 now says leftover material under 2,000 is the common state (97 of 141 non-idle war turns, 69%), "the 39 of 52 does not by itself show a material constraint", the cap binds alone on 12 of 52, the other 40 unresolved. Numbers re-derived: 193 = 97 + 44 + 39 + 13; 7 of the 39 had >= 200 material; "186 of them war-flagged" also holds (7 of the 193 turns have `strategy.war` false). §9 now marks FA4's causal reading Low | **Applied** |
| F2 I1a gate "buckets sum to live" unsatisfiable | §5 I1a gate | Now three lines: per-gun records with target = commit sum to `live`; all per-gun records sum to usable weapons with a target; every started search reaches a terminal record or cancel; a missing field is UNTESTED, fix and replay. This is the wording I gave, and it is satisfiable (`live` counts commit-assigned guns only, `AimWatch.cs:151-165`) | **Applied** |
| F3 S2 had no gate | §5 S2 | Gate for either path (fellow-Eiai share <= 5%, commit and original-AI shots not below baseline, realized hit fraction within 3 pp, UNTESTED below 20 fire turns) plus the v0.20 condition (hit/miss ratio preserved, red-before-fix on the m25 T23 shape) | **Applied** |
| F4 repair totals presented as verified waste | §5 S2 argument cell | "repair totals ... from all attackers, so the link to Eiai fire is an inference" | **Applied** |
| F7 pre-registered gates and wiring witnesses | §7 last bullet | "Gates and wiring witnesses are fixed in the script before the match they judge (E)". The difference-in-differences / switch-on-turn idea is not recorded | Applied in substance (rest minor) |
| F9 next states | §7 | PASS / FAIL / UNTESTED next states for every feature gate, with Pavol deciding after 3 UNTESTED matches | **Applied** |
| F5, F6, F8, F10 (minor) | not changed | see section 2 | Open, minor |

Read-only claim in §2 re-verified: `git status --porcelain | grep -v council-v020 | sha256sum` still starts `a2f2188c0869c327`, and no tracked file is modified (0 non-untracked entries).

## 2. Findings in revision 2 (none blocks confirmation)

| # | Section | Finding | Evidence | Material? |
|---|---|---|---|---|
| N1 | §5 I8 primary metric | "`plannedHits` at the human by T26 from the `fire` log" cannot be read from the current log. The `fire` record carries `plannedHits` / `plannedMisses` as totals plus `assigned` and `primaryByTarget` per target, not planned hits per target (`Patches.cs:799-804`). The metric as written would be UNTESTED or an estimate. Fix: add per-destination planned hits (`plannedHitsBy`) to I1a, or use `fire-paid` with destination = human as the primary count | Round 1: same class of defect as the I1a gate, but on a v0.21 item whose gates are re-fixed in the script before its match (§7), so not blocking | Should fix; non-blocking |
| N2 | §4 FA5 vs §5 I1a | FA5 says the own/none inference holds "until the classification is logged (I1a)", but I1a lists only "`destination` split into own / neutral / none", not hit/miss-list membership per paid shot. For reference, own + none were 176 of 995 paid records in m25 (17.7%) against 179 planned misses (18.8%), which supports the inference | Add "hit/miss-list membership per paid trajectory" to the I1a list | Minor |
| N3 | §5 S1 gate | "max-hold decisions are not more frequent than control": a late volley resets `HoldTurns` (`StrategyPatches.cs:127-130`), which makes max-hold rarer, not more frequent, so the stated direction cannot detect the effect. Say "differ from control" | Code read | Minor |
| N4 | §6 do-no-harm | "HUMAN-FIRST share >= 85%" equals the m26 baseline (23 of 27 = 85.2%); one extra non-HUMAN-FIRST fire turn fails it by chance (my round-1 F5, not changed). Suggest "not more than 10 pp below the pre-change baseline" | ballotE baselines: 94% (m25), 85% (m26) | Minor |
| N5 | §4 CF1 replacement finding | "m25's two largest losses came with 50+ railgun shots exhausting roll energy" does not hold for the larger one: phase 31 (-740) ended with 6,454 energy left; only phase 32 (-715, 18 left) fits. It is labelled unverified and I13 is "investigate first" with Low confidence, so the damage is bounded. Suggested text: "m25's largest loss (phase 31, -740) came with 6,454 energy left, so energy exhaustion does not explain it" | `cf1.py v019m25 19 38` | Minor |
| N6 | §10 | "(all ACCEPT I7)": lane A voted AMEND on I7 (ballotA, same position: defer, soft score, `w = 0`). Substance is right | ballotA I7 | Minor |
| N7 | §5 I11, I1b | These write outside the council folder (notes, scoreboard, csproj, tools); say they need Pavol's go-ahead | Charter: facilitator writes only in the council folder | Minor |

**Faithfulness to lane E:** accurate. My report, cross-review and ballot are represented correctly, including: the process findings; the aim-step confounder; my self-disclosed cap overstatement and its follow-up (the base rate); G2 withdrawn; the I2 strategy-only preference recorded as a dissent (§10) and as a non-blocking preference (§5); I3 as a "no-regret S" with the S1 support; I4 form A as a v0.21 arm (A and E); I5 and I8 as v0.21 arms with my gate wording; the 4-1 pre-T12 hysteresis (A dissents, which matches ballotA: "before T12 retain current behavior").
**Internal consistency:** the vote labels, next states and dispositions cohere. The stated reservation "Not in v0.20: any accuracy change" is reconciled with S2 by the S2 condition (hit/miss ratio preserved). Every blocker in §10 has a disposition. No step exceeds read-only authority: the record proposes work and reserves the choices to Pavol.

## 3. The two open details

**(a) I2 latch scope: I still prefer strategy-only for v0.20 and accept the record's default (shared state with the upkeep guard unless Pavol prefers strategy-only).** The measured loss is fire turns on peace (4 flips in 10 AI-lobbies, 5-8 guns, 1-2 turns each). A shared latch that keeps "pre-war posture" is in effect strategy-only plus a second director flag, so it adds code surface (and an unmeasured posture risk if the guard is imperfect) for no measured gain; the do-no-harm check "`aimedWeapons` does not fall on latched turns" and the upkeep-guard tests in §5 cover the risk adequately. If implemented shared, log `warLatched` beside `strategy.war` and `director.war` so the two can be compared.

**(b) S1 resolution (a late pulse-wait volley does not update `LastStrikeTurn`): I support it, as a hypothesis, which is how the record now words it.** `pulseReady = turn - lastStrikeTurn >= StrikeEvery (2)` (`Strategy.cs:395`) and `ReportFired` sets `LastStrikeTurn` on any fire (`StrategyPatches.cs:127-130`), so an updating late volley postpones the next scheduled pulse while a non-updating one leaves the scheduled cadence intact. Add (not blocking) an outcome check: the following scheduled pulse's `firingHits + firingMisses` not lower than control, so dilution is measured; and log a `lateKind` field so the existing below-saturation late path (which still updates `LastStrikeTurn`) can be compared. Rare opportunity (4 qualifying holds in 193 war turns), so UNTESTED after 3 matches is the likely outcome and the record's UNTESTED rule handles it.

## 4. Reproduction

`sha256sum docs/reviews/2026-11-08-council-v020/decision-record.md`; `python assoc.py` (idle x material contingency); a one-off count of `strategy.war` false among the 193 turns (7); `python cf1.py v019m25 19 38`; `sed -n 799,804p AI/Patches.cs`; `sed -n 120,132p AI/StrategyPatches.cs`; `git status --porcelain | grep -v council-v020 | sha256sum`.
Not verified by me: D's guided-strike finding behind I13; the lane A, B and C claims in §3 beyond what their ballots state.

CONFIRM decision-record.md @ sha256:5d5b648b20460919e8d4cf39edadc06285dfbf4d308a1dae6bfe68d71a44f727

verdict: revision 2 applies all three of my material round-1 findings correctly and preserves lane E's positions and every split accurately; the open items (N1-N7) are wording and log-field fixes to be made before the matches they judge, none of which changes what was agreed.

END OF REPORT
