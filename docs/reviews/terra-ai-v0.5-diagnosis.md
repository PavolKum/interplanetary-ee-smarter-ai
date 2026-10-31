I’ll reconstruct the timeline from both logs and check the decisive mechanics in the decompiled game before judging the proposed fixes.
The first important correction: “blind” is an aggregate economy/city-visibility flag, not proof that every enemy weapon was invisible. I’m checking the actual per-building thresholds and the construction logs before treating blindness as the sole cause.
## Bottom line

**Fey’s main failure was converting a superior economy into an exposed, poorly defended planet.** The attack began before the dramatic T29 collapse: Fey had no defenses or defense reserve through T22, and its response was capped at three defenses per turn. Later, when visibility of the human’s weapons was lost, it started building Ion defenses against a Railgun attack.

Several parts of the proposed diagnosis are materially wrong, though: **Fey did see Railguns; it kept building telescopes; the Nuclear Missile was bought on T27; and the planet ran dry on T32, not T37.** The logs do not establish how much of Fey’s fire was intercepted.

*References below: `AI log` = `docs/playtests/data/v041-ai-decisions.jsonl`; `human log` = `docs/playtests/data/v041-human-telemetry.jsonl`. AI economy records are turn-start snapshots; human figures below specify turn-end where relevant.*

## Hypothesis verdicts

### H1 — Defense sizing and blindness: **PARTLY**

- Fey had **zero defenses and zero reserve through T22**. Desired defenses were 0 at T22, 3 at T23, 16 at T24 and 40 at T27. Three were queued each turn T23–31; at the **start** of T31, 20 remained. The cap constrained the response (`AI log:107–114, 126–127, 172–173, 197–198`; `AI/DirectorPatches.cs:282–287`).
- **But Fey did not count zero weapons throughout blindness.** It saw 4 Railguns at T23, 10 at T24–25, 13 at T26 and 36 at T31. The T23 target of 3 includes both four visible weapons and 927.5 recent damage, not damage alone (`AI log:111–114, 124–127, 138–141, 153–156, 195–198`; `AI/Brain/Director.cs:123–127`).
- `blindEnemies` means economy **or population/city totals** are unreadable. Individual weapons have separate visibility thresholds; blind does not mean no buildings visible (`AI/DirectorPatches.cs:139–141`; `AI/Patches.cs:140–167`; `decompiled/Interplanetary.PlayerDataManagement/PlayerIntelHandler.cs:176–185`).
- The human had zero Railguns at T20 turn-end, one at T21, four at T22 and 24 at T27 (`human log:127, 140, 154, 367`). The human’s 19 Kinetic defenses already existed at T20.
- “100+ buildings a turn” is unsupported as a **net** loss: Fey fell 279→200→157→157→64 from T28–32, changes of −79, −43, 0 and −93. Gross destruction can be higher because construction continues (`AI log:176, 182, 189, 195, 207`).

**Additional mechanism:** at T27–30 Fey queued three **Ion** defenses each turn while the human’s recorded fire was predominantly Railguns. When no weapons are visible, the fallback spreads across the least-owned defense types (`AI log:172–173, 178–179, 184–185, 191–192`; `AI/DirectorPatches.cs:426–445`).

### H2 — Offense absorbed by defenses: **PARTLY**

Fey had 41 Railguns and 16 Missiles at T26 start; the human had 22 Kinetic and 6 Ion at T26 turn-end, and 22 plus 7 at T27 (`AI log:153`; `human log:257, 367`). Defense is a plausible major factor.

However, **435 `firingHits` are selected firing-list entries, not confirmed impacts or failed interceptions** (`AI/Patches.cs:249–291`). The supplied human-only recorder does not show Fey’s incoming interception outcomes. Human building *counts* rose, but that does not prove no building was destroyed and replaced. Population fell from 3,141 to 2,526 at T21 and 3,115 to 2,970 at T27; T23 also grew less than the preceding turn, so “only two population dips” is not a complete damage measure.

A second problem is demonstrated: Fey’s powered targeting count fell from 57 at T26 to 44 at T27 and 31 at T29 (`AI log:154, 171, 183`). Firepower was being lost or disconnected as the attack developed.

### H3 — Planet exhaustion: **PARTLY**

Planet material was 287,948 at T10, 88,357 at T26, 41,140 at T28, 19,015 at T30, **946 at T31 and zero at T32** (`AI log:37, 153, 176, 189, 195, 207`). Drain peaked at **26,783** between T25 and T26; it was not uniformly 22–25k.

At the T28 balance and preceding 23,688 drain, a fixed-rate counterfactual runs out during T30. That supports **early depletion absent a change in production**, but the exact un-attacked trajectory is unknowable: production and projects would have changed. The observed collapse began while 41k remained, so **attack damage, not exhaustion, triggered it**. At zero, actual material credit stops even though the displayed production stat can remain positive (`decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:161–184`).

### H4 — Superweapon too late: **PARTLY**

Thermonuclear Warheads was queued **T21**, not T27. A Nuclear Missile was **purchased T27** and appears in Fey’s T28 buildings, not first at T37 (`AI log:96, 169–170, 176`). Its individual firing, interception and damage are not identifiable from these logs. It did not prevent the loss; “too late to matter” is not proven.

### H5 — Intel race: **PARTLY; “stopped adding” REFUTED**

Fey queued two telescopes at T21–23, T25 and T27–31 whenever the blind condition applied. The apparent plateau was partly **replacement against losses**: e.g. 11 existing at T28 start plus two queued, but eight existing at T29 start (`AI log:101, 107, 113, 140, 172, 178, 182–184`). T27 logged Solar Plant failures, not telescope failures. Slots remained plentiful through T31; the **two-per-turn throttle did bind**, while the 30-building cap did not (`AI/DirectorPatches.cs:271–280`). At T32 no telescope was queued despite blindness; the log alone does not distinguish affordability or another failed eligibility check.

## Q1 — What the defenses imply

Each **in-range, powered** defense rolls independently, spending energy on **every attempt**, including failures. A base Kinetic defense has 80% interception against Railguns for 100 energy; Ion has 80% against Missiles and Nuclear Missiles for 200. With *n* overlapping unupgraded eligible defenses and sufficient energy, interception is **1 − 0.2ⁿ**; zero coverage means zero interception. Coverage, upgrades, phase ordering and actual remaining energy matter (`PlayerDefenseHandler.cs:215–250, 264–287`; `docs/ruleset/DefensesData.csv:5–41`).

At T27 the human ended planning with 7,835 energy—at most about **78 base Kinetic attempts or 39 Ion attempts**, fewer if mixed. That is not a fixed number of shots stopped: failed rolls cost energy too. T26 was 5,337 and T28 was 3,517, so 7–11k was not consistent (`human log:257, 367, 409`).

- **Missiles** bypass Kinetic but face Ion; at T15–21 Fey could see 16–19 Kinetic and no Ion in its target view, making earlier Missiles a reasonable test. Once Ion appeared, the advantage narrowed (`AI log:56, 101, 113`).
- **Lasers** bypass Kinetic and Ion but are stopped by Shield; the plugin’s laser support is experimental and disabled by default (`AI/DirectorPatches.cs:461–467`).
- **Nuclear Missiles face Ion**; they are not an automatic bypass. Saturation is plausible but not established here: actual coverage, successful trajectory counts, firing order and budget decide whether a volley exhausts energy. Instrument interception and damage before claiming a winning mix.

## Q2–Q4 — Fair changes, ranked

**1. Proactive defense plus a usable reserve — highest confidence.** As a *testable starting rule*, after T15, if an enemy becomes blind after previously being visible, target `min(20, ceil(0.15 × own non-city buildings))` defenses, subject to available tech, slots and a per-turn spending budget; allow a faster catch-up than three per turn. Fey had 140 non-city buildings at T21 start, so the target would be 20. Its 113 free slots and 8,270 pre-reserve usable energy make 20 base Kinetic buildings plus a 2,000-energy reserve **arithmetically feasible**, not a placement/power guarantee (`AI log:99, 101`; defense cost: `docs/original-mechanics.md:298`).

Use visible weapons and remembered last-seen counts; observed attack/projectile types can inform the mix, but the current damage hook records **damage and shooter, not weapon type** (`AI/Patches.cs:369–375`). Add a matching reserve for *planned* defenses: currently zero existing defenses forces zero reserve, even when construction is planned (`AI/Brain/Director.cs:188–195`). In a continuously visible peaceful game this blind-triggered floor adds zero; a peaceful counterintel user would still trigger the bounded insurance cost. Test that tradeoff rather than claiming no overbuild.

**Rules to test:** visibility per building versus aggregate blindness; construction completion and power timing; activation coverage/cost; energy reset; what projectile information is actually observable.

**2. Preserve and use observed weapon/defense intelligence; prioritize the observed threat.** Cache last-seen counts and types with age and uncertainty, rather than replacing them with empty dictionaries. In this match, remembering Railguns would avoid the Ion fallback. Separately, remember the visible Kinetic wall and test Missile-heavy or laser tactics against it. Last-seen **zero** is not proof of a peaceful current arsenal; enemy tech era must be observed or inferred, never read from hidden state.

**Rules to test:** shadow/intel semantics, observation freshness, weapon-type event visibility, each defense’s weapon list and radius, research unlocks, targetability and laser support.

**3. Protect powered firepower and measure real outcomes.** After damage, favor reconnecting critical weapons and defenses, and log actual shot type, interception and damage on fair observable events. T28 had 48 powered targeting entries out of 73; T29 had 31 of 82. Those totals are **not necessarily live weapon counts**: native targeting retains a targetable list while removing destroyed entries from the weapon handler, so investigate before using them as fleet denominators (`decompiled/Interplanetary.AI/AIWeaponTargeting.cs:55–110`). This instrumentation is prerequisite to choosing saturation, research or weapon-mix changes confidently.

**Rules to test:** power-grid reconnection and placement, targeting-list lifecycle, projectile/action-phase ordering, defense energy accounting and information available to the player.

**Follow-up, not top three:** reassess mine pacing using `max(planet drain, material production)` and retained material. Refineries do not extend planet life when production already exceeds drain (`AI/DirectorPatches.cs:618–629`; `PlayerResourceHandler.cs:163–184`). One match cannot establish whether slower extraction would improve win probability.
