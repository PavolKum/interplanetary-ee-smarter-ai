I’ll check Lane B’s claims against the frozen brief, match data, and code, then assess each recommendation and exit gate.
Two material issues have emerged: the report treats paid trajectories as landed impacts, and its four-factor “prediction” uses the outcome itself. I’m checking how far those errors affect the rankings and gates.
The interception percentages also use the wrong denominator: intercepted impacts are already included in total impacts. Reconstructing the three-turn windows gives materially higher rates, so the report’s defense and timing premises need revision.
# Cross-review of Lane B

**verdict: Low trust for diagnosis and ranking; several raw counts reproduce, but circular analysis, a wrong interception denominator, and treating paid trajectories as landed invalidate the main conclusion.**

**Scope:** HEAD is `4d7c160`. I read the brief and Lane B in full, the five specified playtest notes and JSONL datasets, relevant AI code and selected decompiled game code. I did not read another lane report. The raw datasets are git-ignored, so the commit alone does not establish their contents were frozen.

## Claim checks

### O1 — War and first-shot timing: **AMEND — major**

First war flags and first paid-shot turns reproduce: first paid shots range from **T19 to T23**. But first war flags range from **T13 to T20**, and similar first-shot timing does not establish that timing was unimportant.

**Corrected statement:** “First paid shots clustered at T19–T23 across these ten AI records; whether earlier effective fire would change outcomes is untested.”

**Alternative:** The human’s defenses were already substantial by T21; a shared delay could hurt all five matches. Source: `strategy` and `fire-paid` events in the five decision files; human `turn_end` snapshots.

### O2 — Aim and “realized hit rate”: **AMEND — major**

The reported found/live denominators are **all `aim-search` records**, including holds, not firing turns. For Eiai, all-search aim is **216/294 = 73%**; restricting to turns with paid shots gives **78/103 = 76%**.

`firingHits` and `firingMisses` are selected firing-list classifications, **not realized impacts** (`AI/Patches.cs:740–804`). Eiai’s 81/91 is a firing-list share, not a measured hit rate.

**Alternative:** Differences in trajectory availability, selected weapon mix, interceptions, and actual damage could matter even when aggregate aim-search ratios look similar.

### O3 — Four-factor prediction: **REJECT — blocker**

The equation is an identity:

`fire-record count × powered/fire-record count × paid/powered × paid-at-human/paid = paid-at-human`.

It uses the outcome in the final factor and cannot independently predict it or establish which factor is causal. The table’s `warTurns` are actually **`fire` event counts**: Arc has 18 `fire` events but 20 war-flagged strategy records; Eddie has 19 versus 16. Eiai’s displayed factors calculate to **63**, not the reported predicted **61**; 61 versus 63 also misses the claimed ±2% tolerance.

**Alternative:** The factors describe an accounting decomposition. Matched comparisons or an intervention are needed to rank bottlenecks.

### O4 — Holds and late free fire: **AMEND — major**

Eiai had **16 held / 22 war-flagged strategy turns**, but only **10** were `below-saturation`; four were `building` and two `no-weapons`. On T21 the `fire` record shows **13/13 primary trajectories found and zero firing-list entries**; on T35 it shows **11/22**, not the claimed minimum of 13. The hold mechanism clears firing lists (`AI/Patches.cs:632–644`).

Zenon’s six late-free-fire turns paid **145/342 = 42%** of his total paid shots, not about 51%. Across match 25 they account for **145/995 = 15%**. Hawking paid 28 on three late-fire turns.

**Alternative:** Late free fire helped these individuals, but these different matches do not prove it “recovered most” of the earlier shortfall or caused the win.

### O5 — Energy never bound: **REJECT — blocker**

The claimed 9.3k–26.5k “mean unspent” appears to conflate an **early-turn AI budget** with end-of-turn surplus. Eiai’s war-turn mean `economy.energy` is **9,269**, while mean `director.energyLeft` is **2,719**; the latter was below 1,000 on 10 of 22 war-flagged director records. These snapshots have different timing and reservations (`AI/EconomyPatches.cs:159–171`, `AI/DirectorPatches.cs:3103`).

Late-free-fire `energyLeft` is **before** retaking the firing reservation. The nine logged late-fire events have **315–14,503** left after subtracting that reservation, not 4.3k–16.5k. Energy resets each turn; it is not a carry-over stock (`decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:152–159`).

**Corrected statement:** “Some turns had substantial surplus; energy and competing reservations were binding on other turns.”

**Alternative:** Material, defense reserves, construction, and firing costs vary by turn. Aggregate surplus cannot rule out a local energy constraint.

### O6 — Gun counts and Ash’s placement: **AMEND — major**

The table’s counts are **T21 powered-weapon snapshots**, not counts at each AI’s first war turn. Ash first entered war at T17 with `aim-search.live = 0`; Hawking’s first war turn was T20, also with live 0, though its director counted 10 aimed weapons including construction. At T43 Ash had **6 powered/live** in `fire`/`aim-search`, versus **8 director aimed weapons**. Doc’s peak powered count was **26**, outside the claimed 27–31 range.

Ash’s 203 lifetime failures are real, but **only 16 occurred by T21**, including two railguns; the other 187 came later. They do not establish that failures halved his guns at war start.

**Alternative:** Research, build order, power, and construction timing may explain early weapon counts; late crowding may explain many later failures. Director counts include under-construction buildings (`AI/DirectorPatches.cs:372–376`).

### O7 — Placement totals: **AMEND — major**

Summing `director.placementFailures` for the named smart AIs gives:

| Match | Per-AI totals | Combined |
|---|---|---:|
| 22 | Eiai 24 | 24 |
| 23 | Arc 37, Beastie 27 | **64** |
| 24 | Doc 52, Eddie 26 | **78** |
| 25 | Donnels 24, Sarai 44, Zenon 39 | **107** |
| 26 | Ash 203, Hawking 26 | **229** |

Thus Lane B’s **69 for match 23 and 67 for match 24 are wrong**; the match 24 note’s 78 is correct. The match 23 note’s listed breakdown also sums to 64, not its stated 59. Match 25’s breakdown sums to 107, not the note’s 127.

**Alternative:** These are abandoned searches, not proof of impossible geometry. The mod abandons a search at a step cap and refunds it (`AI/DirectorPatches.cs:2976–3016`).

### O8 — Human telemetry: **AMEND — major**

The reported shot-event counts and T21/T24 defense counts reproduce when filtering `turn_end` and summing the four named defense types. But “the human doubled defenses” is false for standard-play matches 24 and 26: **31→32** and **25→28** respectively.

“Shots at AI planets” is too specific: `hitOtherPlayerPlanet` does not identify which opponent. Match 23’s 185 recorded shot events are **all lasers**, a material coverage/weapon-mix caveat.

**Alternative:** Defense coverage, weapon-specific counters, resources available for defense rolls, and human offense matter in addition to building counts.

### O9 — Nine nukes “landed” and were decisive: **REJECT — blocker**

There were **13 paid nuclear first-phase trajectories**, nine with destination `jogi`: T31, T33, T34, T35×2, T36×2, **T37×2**. Each logged 700 energy. This establishes payment and calculated destination—not launch, interception, final impact, or damage. The event is emitted at the first-phase targeting payment boundary (`AI/Patches.cs:519–522,594–607`; `decompiled/Interplanetary.PlayerDataManagement/PlayerWeaponHandler.cs:230–262`).

**Corrected statement:** “Nine paid nuclear trajectories named jogi; their final outcomes and contribution to the population collapse are unverified.”

**Alternative:** Numerous upgraded railgun projectiles and the human’s constrained defense budget could explain the collapse. Temporal coincidence cannot isolate the nukes.

### O10 — Non-human destinations: **AMEND — major**

Of **995 paid events**, 650 named jogi, 169 another AI, 98 `own`, and 78 `none`. Thus **345/995 = 34.7%** did not name jogi, not 27%. These are calculated first-phase destinations, not proven landed shots.

The other AIs are opponents, not established allies. On T23 Sarai and Zenon explicitly committed to Beastie; **9 of the 169** other-AI destinations matched that commitment. The rest are not proven planned misses: target assignment can reroute individual weapons (`AI/Patches.cs:142–149`), and `fire-paid` does not log hit/miss-list membership.

**Alternative:** Some off-commit trajectories may be intentional reachable-target fallbacks, secondary trajectories, or misses; the current log does not distinguish them.

### O11 — Interception rates: **REJECT — blocker**

`observedImpactsBy` already **includes intercepted events**; `observedInterceptedBy` is a subset (`AI/Brain/DefenseLedger.cs:61–76,88–96`). Lane B’s rates match dividing interceptions by **impacts + interceptions**, double-counting the numerator.

I independently reconstructed the non-overlapping per-turn increments from the contiguous three-turn strategy snapshots. For events at jogi, through each AI’s last logged strategy turn:

| Match | Intercepted / observed impacts | Share |
|---|---:|---:|
| 22 | 52 / 68 | **76.5%** |
| 23 | 101 / 173 | **58.4%** |
| 24 | 68 / 100 | **68.0%** |
| 25 | 282 / 844 | **33.4%** |
| 26 | 253 / 454 | **55.7%** |

These are **observed interception-check events**, not percentages of paid weapons; final action phases after the last strategy snapshot may be missing. The three-turn history is established by `AI/Plugin.cs:295` and `AI/Brain/DefenseLedger.cs:99–118`.

**Alternative:** Match 25 still has the lowest measured share, but weapon mix, coverage, available defense resources, and lobby size confound the comparison; it does not prove defense count caused it.

### O12 — War/peace flip: **AMEND — major**

Ash was war T17–18, peace T19, war T20. Eddie’s early war was interrupted **T15–16 and T18–19**, so “one turn per match” understates the observed reversions.

`WarPhase` allows either scheduled war or recent-damage retaliation (`AI/Brain/Director.cs:271–274`); the scheduled start is latched separately (`AI/DirectorPatches.cs:206–214`). Early retaliation reverting to peace may therefore be **intended policy**, not necessarily a defect.

**Alternative:** A policy change may still be desirable, but its impact and intended semantics need testing before labeling it a bug.

### O13 — Peaks, angles, and missing record: **AMEND — minor**

The eight named peak `energyPerTurn` values reproduce. The unfound-facing means also reproduce as averages of **51 and 34 per-record means**, not averages weighted by individual guns. Eiai’s 81+10 and match 23’s 103+18 and 155+42 reproduce as **firing-list counts**, not confirmed hits and misses.

Eiai’s economy, strategy, and director records all end at T36; T37 is absent, but that alone does not establish a logging failure.

**Alternative:** The per-record angle mean is sensitive to how many guns each record summarizes. Eiai’s absence may reflect elimination rather than dropped telemetry.

## Recommendations and exit gates

| Recommendation | Rank | Exit gate | Assessment |
|---|---|---|---|
| **R1 Placement** | **AMEND** — reasonable first investigation, not proven the dominant lever | **REJECT** | `≤20` mixes per-AI gates with match totals; failures can fall by attempting fewer builds. `unfoundFacingDeg` is conditional on failed trajectories and may have no samples. `≥15` at “war start” ignores early retaliation and tech. Compare matched scenarios using attempts, successful placements, powered guns, coverage, and paid/observed output; preserve visibility and legal placement. |
| **R2 Destination hygiene** | **REJECT** as a v0.20 co-change | **REJECT** | Other AIs are not established allies, and paid events do not identify misses. Instrument assigned target, hit/miss classification and destination first. Preserve configured accuracy and intentional reachable-target fallbacks; assess changes against matched baselines. |
| **R3 Late free fire** | **AMEND** to targeted investigation | **REJECT** | Sarai’s held war turns were `pulse-wait` or `no-weapons`, not missed eligible `below-saturation` opportunities. Donnels’s two below-saturation holds had end energy below the released reservation. Existing eligibility also checks material, minimum weapons, expected passing, funded defense and pending purchases (`AI/Brain/Strategy.cs:283–292`; `AI/StrategyPatches.cs:343–361`). Log eligibility and refusal reasons, then gate eligible reachable salvos without overriding those constraints. |
| **R4 Earlier strike** | **AMEND** — experiment, not established priority | **REJECT** | `first paid ≤T18` can reward a token shot; `≤25%` interception uses the wrong baseline and depends on opponent defenses and weapon mix. Compare matched lobbies, tracking first *effective* volley, cost, observed impacts and population damage by fixed turns. |
| **R5 Nukes and cap** | **AMEND** — speculative, later | **REJECT** | Nuclear impact and decisiveness are unverified. `idle ≤8k` rewards waste; “three nukes by T30” ignores unlocks, affordability, visible candidates and interception. The 30-weapon cap exists because trajectory search is time-bound (`AI/Plugin.cs:482–483`); test any cap change against aim, paid fire, damage and wait time. |
| **R6 War/peace** | **AMEND** — rank after establishing intended early-retaliation policy | **REJECT: absent** | Decide whether early retaliation should latch. Gate the chosen behavior using logged war, recent damage and scheduled war turn; separately check construction and firing consequences. Do not assume each reversal is a bug. |

## Blockers

1. **Withdraw the landed/decisive-nukes conclusion.**
2. **Correct interception denominators and distinguish observed interception events from paid shots.**
3. **Remove the circular “prediction” as evidence of causality or rank.**
4. **Replace the universal “energy never bound” claim with phase- and reservation-aware measurements.**

## Non-blocking improvements

- Correct placement totals, turn windows, paid-versus-landed labels, defense-growth claims, and the T21 versus first-war gun counts.
- State the actual sample filters and missing final-action-phase coverage.
- Make gates conditional on eligible opportunities, with denominators and matched scenarios; record when a metric has no observations.

## Reproduction

From the repository root in Bash, read-only:

```bash
python -B -c 'import json,collections; a=[json.loads(s) for s in open("docs/playtests/data/v019m25-ai-decisions.jsonl",encoding="utf-8")]; p=[r for r in a if r.get("e")=="fire-paid" and r.get("wid")=="wea_NuclearMissile" and r.get("ai") in ("Donnels","Sarai","Zenon")]; print(len(p),collections.Counter(r["destination"] for r in p)); print([(r["t"],r["ai"],r["destination"],r["energyPaid"]) for r in p])'
```

```bash
python -B -c 'import json; fs=["v018m22","v018m23","v018m24","v019m25","v019m26"]; ns=[("Eiai",),("Arc","Beastie"),("Doc","Eddie"),("Donnels","Sarai","Zenon"),("Ash","Hawking")]; print([(f,[(n,sum(sum(r.get("placementFailures",{}).values()) for r in a if r.get("e")=="director" and r.get("ai")==n)) for n in names]) for f,names in zip(fs,ns) for a in [[json.loads(s) for s in open("docs/playtests/data/"+f+"-ai-decisions.jsonl",encoding="utf-8")]]])'
```

For the interception reconstruction, each contiguous three-turn snapshot `W[t]` yields the current increment as `x[t] = W[t] - W[t-1] + x[t-3]`, with zero initial history. Apply separately to `observedImpactsBy["jogi"]` and `observedInterceptedBy["jogi"]`, per named AI; sum increments only through its last strategy record.

END OF REPORT
