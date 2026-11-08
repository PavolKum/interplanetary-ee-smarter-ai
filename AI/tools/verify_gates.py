"""Evaluate the v0.20 council gates on a decision log (council I1b). Prints PASS / FAIL / UNTESTED per gate with its
denominator; a gate whose opportunity never arose is UNTESTED, never PASS.

Usage:
  python AI/tools/verify_gates.py <ai-decisions.jsonl> [--human jogi] [--ai Name,Name]
  python AI/tools/verify_gates.py --baseline-fa4 <file> [<file> ...]   # reproduce the council's FA4 cross-tab
  python AI/tools/verify_gates.py --selftest                           # every gate must be able to FAIL
Exit code: 1 if any gate FAILs, else 0.
"""
import argparse
import json
import sys
from collections import defaultdict

DO_NO_HARM = {"wait_mean": 10.0, "wait_max": 45.0, "human_first": 0.85, "shortfall": 0.05, "covered": 0.85,
              "collapse": 0.20}
STRIKE_EVERY = 2               # StrategySettings.StrikeEvery
MAX_HOLD_BASELINE = 3 / 186.0  # max-hold share of war strategy records, matches 22-26 (v0.18-v0.19)
LAND_GIVEUP_BASELINE = 114 / 159.0  # v0.21 I9: land placements given up per director record, matches 27-28 (v0.20)
WATER_ONLY = {"bui_OffshoreMine", "bui_FloatingPlant"}
LAND_PLANTS = {"bui_SolarPlant", "bui_NuclearPlant"}
STRANDED_LIMIT = 0.5 * 7 / 30.0  # v0.22 I9b: half of m29's land plants given up with only water anchors per AI-turn


def segments(rows):
    """Fable v0.20 #2: one decisions file per game process can hold several timelines (restart, earlier-save load).
    A new segment starts at an init record that follows non-init records."""
    segs, cur, seen_body = [], [], False
    for r in rows:
        if r.get("e") == "init" and seen_body:
            segs.append(cur)
            cur, seen_body = [], False
        if r.get("e") != "init":
            seen_body = True
        cur.append(r)
    if cur:
        segs.append(cur)
    return segs


def load(path):
    rows = []
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if line:
            try:
                rows.append(json.loads(line))
            except ValueError:
                pass
    return rows


def smart_ais(rows, only=None):
    maxt = defaultdict(int)
    for r in rows:
        if "ai" in r and isinstance(r.get("t"), int):
            maxt[r["ai"]] = max(maxt[r["ai"]], r["t"])
    ais = sorted({r["ai"] for r in rows if r.get("e") == "init" and r.get("smart")} )
    ais = [a for a in ais if maxt[a] > 2]  # drops an aborted lobby restarted at T1
    if only:
        ais = [a for a in ais if a in only]
    return ais


def by(rows, e, ai):
    return [r for r in rows if r.get("e") == e and r.get("ai") == ai]


def gate(results, name, status, detail):
    results.append((name, status, detail))


def evaluate(rows, human="jogi", only=None):
    res = []
    ais = smart_ais(rows, only)
    if not ais:
        gate(res, "roster", "UNTESTED", "no smart AI with records beyond T2")
        return res

    # I1a wiring witnesses
    inits = [r for r in rows if r.get("e") == "init" and r.get("ai") in ais]
    ok = [r for r in inits if r.get("build") and isinstance(r.get("config"), dict)]
    gate(res, "I1a init build+config", "PASS" if inits and len(ok) == len(inits) else ("UNTESTED" if not inits else "FAIL"),
         "%d/%d init records carry build and config" % (len(ok), len(inits)))
    aims = [r for r in rows if r.get("e") == "aim-search" and r.get("ai") in ais and r.get("live", 0) > 0]
    have = [r for r in aims if "commitLiveByFacing" in r]
    good = [r for r in have if sum(r["commitLiveByFacing"]) + r.get("commitLiveNoTarget", 0) == r["live"]]
    gate(res, "I1a facing buckets sum to live", "UNTESTED" if not have else ("PASS" if len(good) == len(have) and len(have) == len(aims) else "FAIL"),
         "%d/%d aim-search records (live>0) with commit buckets summing to live" % (len(good), len(aims)))
    dirs = [r for r in rows if r.get("e") == "director" and r.get("ai") in ais]
    wired = [r for r in dirs if "unpoweredWeapons" in r and "searchesOpenAtEnd" in r and "retaliationLatched" in r]
    gate(res, "I1a director fields", "UNTESTED" if not dirs else ("PASS" if len(wired) == len(dirs) else "FAIL"),
         "%d/%d director records with unpoweredWeapons, searchesOpenAtEnd, retaliationLatched" % (len(wired), len(dirs)))

    # v0.21: every started search (director placeSearchesStarted, per AI and turn) has exactly one place-search record.
    counted = [r for r in dirs if "placeSearchesStarted" in r]
    if counted:
        places = [r for r in rows if r.get("e") == "place-search" and r.get("ai") in ais]
        ids = [r.get("searchId") for r in places]
        per = defaultdict(int)
        for r in places:
            per[(r["ai"], r["t"])] += 1
        bad = ["%s T%d %d vs %d" % (r["ai"], r["t"], r["placeSearchesStarted"], per[(r["ai"], r["t"])])
               for r in counted if r["placeSearchesStarted"] != per[(r["ai"], r["t"])]]
        dup = len(ids) - len(set(ids))
        # Gemini v0.21 #2/#3: records on AI-turns without a director record, a director record without the field, and a
        # match with no search at all must not pass.
        keys = {(r["ai"], r["t"]) for r in counted}
        orphans = sum(n for k, n in per.items() if k not in keys)
        started = sum(r["placeSearchesStarted"] for r in counted)
        missing = len(dirs) - len(counted)
        detail = "%d searches started, %d records over %d director records; mismatched AI-turns %d %s; orphans %d; duplicate ids %d; records without the field %d" % (
            started, len(places), len(counted), len(bad), bad[:3], orphans, dup, missing)
        if started == 0 and not places:
            gate(res, "I1a every placement search terminal", "UNTESTED", detail)
        else:
            ok = not bad and not dup and not orphans and not missing and started == len(places)
            gate(res, "I1a every placement search terminal", "PASS" if ok else "FAIL", detail)
    else:
        gate(res, "I1a every placement search terminal", "UNTESTED", "no director record carries placeSearchesStarted (before v0.21)")

    # v0.21 (council I9): land placements given up per AI-turn must not rise above matches 27-28 (114 / 159 = 0.72; defenses
    # and the water-only types are excluded). Predeclared 2026-11-08 before the first v0.21 match.
    if counted:
        given = sum(v for r in counted for k, v in (r.get("placementFailures") or {}).items() if k not in WATER_ONLY and "Defense" not in k)
        rate = given / len(counted)
        gate(res, "I9 land placements given up per AI-turn", "PASS" if rate <= LAND_GIVEUP_BASELINE else "FAIL",
             "%.2f (%d over %d AI-turns; baseline %.2f)" % (rate, given, len(counted), LAND_GIVEUP_BASELINE))
        land = [r for r in rows if r.get("e") == "place-search" and r.get("ai") in ais and r.get("filtered", 0) > 0]
        outcome = defaultdict(int)
        for r in land:
            outcome[r["outcome"]] += 1
        gate(res, "INFO land searches with the medium preference", "INFO",
             "%d searches %s; water anchors skipped %d; steps with only water anchors %d"
             % (len(land), dict(outcome), sum(r.get("skippedAnchors", 0) for r in land), sum(r.get("noSameMedium", 0) for r in land)))
    else:
        gate(res, "I9 land placements given up per AI-turn", "UNTESTED", "before v0.21")

    # v0.22: land plants given up with only water anchors (plants queued for unpowered offshore mines) per AI-turn.
    # m29 (v0.21): 7 over 30 AI-turns = 0.23; predeclared 2026-11-08 before the first v0.22 match: PASS at <= half of that.
    v22 = [r for r in dirs if "unpoweredWater" in r]
    if v22:
        opportunity = [r for r in v22 if r.get("unpoweredWater", 0) > 0 and r.get("unpoweredLand", 0) == 0]
        stranded = [r for r in rows if r.get("e") == "place-search" and r.get("ai") in ais and r.get("outcome") == "gave-up"
                    and r.get("building") in LAND_PLANTS and r.get("filtered", 0) > 0 and r.get("noSameMedium", 0) >= 0.9 * r["filtered"]]
        rate = len(stranded) / len(v22)
        if not opportunity and not stranded:
            gate(res, "I9b land plants stranded at sea per AI-turn", "UNTESTED", "no AI-turn had only water buildings unpowered")
        else:
            gate(res, "I9b land plants stranded at sea per AI-turn", "PASS" if rate <= STRANDED_LIMIT else "FAIL",
                 "%.2f (%d over %d AI-turns; limit %.2f; %d AI-turns with only water unpowered)" % (rate, len(stranded), len(v22), STRANDED_LIMIT, len(opportunity)))
        links = [r for r in rows if r.get("e") == "place-search" and r.get("ai") in ais and r.get("waterLinkSteps", 0) > 0]
        outcome = defaultdict(int)
        for r in links:
            outcome[r["outcome"]] += 1
        gate(res, "INFO water plants for unpowered water buildings", "INFO",
             "%d queued water-first; %d searches tried next to an unpowered water building %s"
             % (sum(r.get("waterFirstPlants", 0) for r in v22), len(links), dict(outcome)))
    else:
        gate(res, "I9b land plants stranded at sea per AI-turn", "UNTESTED", "before v0.22")

    # I2 retaliation latch: no war -> peace after a latch, per AI; opportunity = an AI that latched
    latched_ais, flips, drops = 0, [], []
    for ai in ais:
        recs = sorted(by(rows, "director", ai), key=lambda r: r["t"])
        first = next((r for r in recs if r.get("retaliationLatched")), None)
        if first is None:
            continue
        latch_t = first.get("retaliationLatchTurn", first["t"])
        war_turn = first.get("warTurn", 10 ** 6)
        if latch_t >= war_turn:
            continue  # Fable v0.20 #1: a latch at or after the scheduled war turn changes nothing; not an opportunity
        strat = {r["t"]: r for r in by(rows, "strategy", ai)}
        later = [t for t in strat if t > latch_t]
        if not later:
            continue  # no strategy record after the latch: nothing observed
        latched_ais += 1
        for t in sorted(later):
            if not strat[t].get("war"):
                flips.append((ai, t))
        prev = None
        for r in recs:
            if latch_t <= r["t"] < war_turn:
                if prev is not None and r.get("aimedWeapons", 0) < prev:
                    drops.append((ai, r["t"], prev, r.get("aimedWeapons")))
                prev = r.get("aimedWeapons", 0)
    gate(res, "I2 no peace after latch", "UNTESTED" if latched_ais == 0 else ("PASS" if not flips else "FAIL"),
         "%d AI(s) latched before their war turn; post-latch peace turns: %s" % (latched_ais, flips or "none"))
    gate(res, "I2 aimedWeapons do not fall while latched", "UNTESTED" if latched_ais == 0 else ("PASS" if not drops else "FAIL"),
         "pre-schedule latched turns; drops: %s" % (drops or "none"))

    # S1 late fire: every candidate hold has an outcome record; every late volley paid = firing hits + misses
    holds = [r for r in rows if r.get("e") == "strategy" and r.get("ai") in ais and r.get("war")
             and r.get("reason") in ("below-saturation", "pulse-wait")]
    outcome = {(r["ai"], r["t"]) for r in rows if r.get("e") in ("late-free-fire", "late-free-fire-refused")}
    missing = [(h["ai"], h["t"]) for h in holds if (h["ai"], h["t"]) not in outcome]
    gate(res, "S1 every candidate hold has an outcome", "UNTESTED" if not holds else ("PASS" if not missing else "FAIL"),
         "%d candidate holds, %d without a late-free-fire(-refused) record %s" % (len(holds), len(missing), missing[:5]))
    lates = [r for r in rows if r.get("e") == "late-free-fire" and r.get("ai") in ais]
    paid = defaultdict(int)
    for r in rows:
        if r.get("e") == "fire-paid":
            paid[(r["ai"], r["t"])] += 1
    fires = {(r["ai"], r["t"]): r for r in rows if r.get("e") == "fire"}
    bad = []
    for r in lates:
        f = fires.get((r["ai"], r["t"]))
        planned = (f.get("firingHits", 0) + f.get("firingMisses", 0)) if f else -1
        if paid[(r["ai"], r["t"])] != planned:
            bad.append((r["ai"], r["t"], paid[(r["ai"], r["t"])], planned))
    gate(res, "S1 late volleys paid = firing hits+misses", "UNTESTED" if not lates else ("PASS" if not bad else "FAIL"),
         "%d late volleys (%d pulse), mismatches %s" % (len(lates), sum(1 for r in lates if r.get("held") == "pulse-wait"), bad[:5]))
    odd = [(r["ai"], r["t"], r.get("refusal")) for r in rows if r.get("e") == "late-free-fire-refused" and r.get("ai") in ais
           and r.get("refusal") in ("energy", "material") and r.get("energyLeft", 0) >= r.get("reservation", 0)
           and r.get("materialLeft", 0) >= r.get("materialReservation", 0)]
    refused = [r for r in rows if r.get("e") == "late-free-fire-refused" and r.get("ai") in ais]
    gate(res, "S1 no unexplained energy/material refusal", "UNTESTED" if not refused else ("PASS" if not odd else "FAIL"),
         "%d refusals, %d not explained by the logged inputs %s" % (len(refused), len(odd), odd[:5]))
    strikes = [r for r in rows if r.get("e") == "strategy" and r.get("ai") in ais and r.get("reason") == "saturated"
               and isinstance(r.get("lastStrike"), int) and r["lastStrike"] > -1000]
    early = [(r["ai"], r["t"], r["lastStrike"]) for r in strikes if r["t"] - r["lastStrike"] < STRIKE_EVERY]
    gate(res, "S1 pulse cadence kept", "UNTESTED" if not strikes else ("PASS" if not early else "FAIL"),
         "%d saturated pulses; earlier than every %d turns: %s" % (len(strikes), STRIKE_EVERY, early or "none"))
    war_strat = [r for r in rows if r.get("e") == "strategy" and r.get("ai") in ais and r.get("war")]
    if war_strat:
        mh = sum(1 for r in war_strat if r.get("reason") == "max-hold") / len(war_strat)
        limit = MAX_HOLD_BASELINE * 2 + 0.02
        gate(res, "S1 max-hold not above baseline", "PASS" if mh <= limit else "FAIL",
             "%.1f%% of %d war decisions (baseline %.1f%%, allowed <= %.1f%%)" % (100 * mh, len(war_strat), 100 * MAX_HOLD_BASELINE, 100 * limit))
    else:
        gate(res, "S1 max-hold not above baseline", "UNTESTED", "no war decisions")
    aborts = [r for r in rows if r.get("e") == "abort" and (r.get("ai"), r.get("t")) in {(x["ai"], x["t"]) for x in lates}]
    gate(res, "S1 no aborted late volley", "UNTESTED" if not lates else ("PASS" if not aborts else "FAIL"),
         "%d aborts on late-volley turns" % len(aborts))

    # Do-no-harm (council section 6)
    waits = [r["aimWaitAfterHuman"] for r in aims if r.get("fire") and r.get("aimWaitAfterHuman", -1) >= 0]
    if waits:
        mean, mx = sum(waits) / len(waits), max(waits)
        gate(res, "DNH aim wait after human", "PASS" if mean <= DO_NO_HARM["wait_mean"] and mx <= DO_NO_HARM["wait_max"] else "FAIL",
             "fire turns %d: mean %.1f s (<= %.0f), max %.1f s (<= %.0f)" % (len(waits), mean, DO_NO_HARM["wait_mean"], mx, DO_NO_HARM["wait_max"]))
    else:
        gate(res, "DNH aim wait after human", "UNTESTED", "no fire turn with a recorded human end")
    # Amendment 1 (Pavol, 2026-11-08, after match 27; applies from match 28): the share counts volleys whose commit IS the
    # human, not the humanFirst flag (attrition volleys at the human carry no flag), and return fire at a non-human
    # attacker is reported on its own line instead of counting against the share.
    fire_strat = [r for r in rows if r.get("e") == "strategy" and r.get("ai") in ais and r.get("fire")]
    returns = [r for r in fire_strat if r.get("reason") == "return-fire" and r.get("commit") != human]
    counted = [r for r in fire_strat if r not in returns]
    if counted:
        hf = sum(1 for r in counted if r.get("commit") == human) / len(counted)
        gate(res, "DNH HUMAN-FIRST share of fire turns", "PASS" if hf >= DO_NO_HARM["human_first"] else "FAIL",
             "%.0f%% of %d fire decisions commit to %s (>= %.0f%%; return fire at others excluded)"
             % (100 * hf, len(counted), human, 100 * DO_NO_HARM["human_first"]))
    else:
        gate(res, "DNH HUMAN-FIRST share of fire turns", "UNTESTED", "no fire decisions")
    if fire_strat:
        flag = sum(1 for r in fire_strat if r.get("humanFirst")) / len(fire_strat)
        gate(res, "INFO return fire at a non-human", "INFO",
             "%d of %d fire decisions (%s); pre-amendment flag share %.0f%%"
             % (len(returns), len(fire_strat), ", ".join("%s T%d at %s" % (r["ai"], r["t"], r.get("commit")) for r in returns) or "none", 100 * flag))
    # Amendment 1, second part: a war turn in which the AI has lost more than COLLAPSE of its peak building count so far
    # is a collapse turn; its shortfall is reported, not gated.
    peak, collapse = {}, set()
    for r in sorted((r for r in rows if r.get("e") == "economy" and r.get("ai") in ais and isinstance(r.get("buildings"), dict)),
                    key=lambda r: r["t"]):
        n = sum(r["buildings"].values())
        peak[r["ai"]] = max(peak.get(r["ai"], 0), n)
        if n < (1 - DO_NO_HARM["collapse"]) * peak[r["ai"]]:
            collapse.add((r["ai"], r["t"]))
    all_war = [r for r in dirs if r.get("war")]
    war_dirs = [r for r in all_war if (r.get("ai"), r.get("t")) not in collapse]
    if all_war:
        raw = [r for r in all_war if r.get("reserveShortfall", 0) > 0]
        gate(res, "INFO reserve shortfall incl. collapse", "INFO",
             "%d of %d war records (%.1f%%); %d collapse records excluded from the gate: %s"
             % (len(raw), len(all_war), 100 * len(raw) / len(all_war), len(all_war) - len(war_dirs),
                ", ".join("%s T%d" % (r["ai"], r["t"]) for r in raw if (r["ai"], r["t"]) in collapse) or "none"))
    if war_dirs:
        sf = sum(1 for r in war_dirs if r.get("reserveShortfall", 0) > 0) / len(war_dirs)
        gate(res, "DNH reserve shortfall", "PASS" if sf <= DO_NO_HARM["shortfall"] else "FAIL",
             "%.1f%% of %d non-collapse war records (<= %.0f%%)" % (100 * sf, len(war_dirs), 100 * DO_NO_HARM["shortfall"]))
    elif all_war:
        gate(res, "DNH reserve shortfall", "UNTESTED", "every war record is a collapse turn")
    if all_war:
        # Covered buildings is not amended: it still reads every war record.
        cov = [r["coveredBuildings"] for r in all_war if isinstance(r.get("coveredBuildings"), (int, float)) and r["coveredBuildings"] >= 0]
        cmean = sum(cov) / len(cov) if cov else -1
        gate(res, "DNH covered buildings", "UNTESTED" if not cov else ("PASS" if cmean >= DO_NO_HARM["covered"] else "FAIL"),
             "mean %.3f over %d war records (>= %.2f)" % (cmean, len(cov), DO_NO_HARM["covered"]))
    else:
        gate(res, "DNH reserve shortfall", "UNTESTED", "no war turns")
        gate(res, "DNH covered buildings", "UNTESTED", "no war turns")
    return res


def fa4(rows):
    """The council's FA4 cross-tab: war turns (at or after first war), idle (energyLeft >= 10k), cap, material < 2k."""
    tot = idle = cap_mat = below_mat = cap_free = neither = 0
    for ai in smart_ais(rows):
        wt = next((r["t"] for r in rows if r.get("ai") == ai and r.get("e") == "strategy" and r.get("war")), None)
        if wt is None:
            continue
        for r in rows:
            if r.get("ai") == ai and r.get("e") == "director" and r["t"] >= wt:
                tot += 1
                if r["energyLeft"] >= 10000:
                    idle += 1
                    at_cap, short = r["aimedWeapons"] >= 30, r["materialLeft"] < 2000
                    cap_mat += at_cap and short
                    below_mat += (not at_cap) and short
                    cap_free += at_cap and not short
                    neither += (not at_cap) and not short
    return tot, idle, cap_mat, below_mat, cap_free, neither


def selftest():
    """Each gate must FAIL on a doctored log (a check that cannot fail proves nothing)."""
    base = [{"e": "init", "ai": "X", "t": 1, "smart": True, "build": "0.20.0+abc", "config": {}},
            {"e": "director", "ai": "X", "t": 12, "war": True, "retaliationLatched": True, "retaliationLatchTurn": 12, "warTurn": 20, "unpoweredWeapons": 0, "searchesOpenAtEnd": 0,
             "reserveShortfall": 0, "coveredBuildings": 0.9, "energyLeft": 0, "materialLeft": 0, "aimedWeapons": 10},
            {"e": "strategy", "ai": "X", "t": 12, "war": True, "fire": True, "humanFirst": True, "reason": "free-fire", "commit": "jogi"},
            {"e": "aim-search", "ai": "X", "t": 12, "live": 3, "fire": True, "aimWaitAfterHuman": 1.0, "commitLiveByFacing": [1, 1, 1, 0]}]
    def status(rows, name):
        return dict((n, s) for n, s, _ in evaluate(rows, only=None))[name]
    checks = []
    clean = base + [{"e": "strategy", "ai": "X", "t": 13, "war": True, "fire": False, "reason": "pulse-wait"},
                    {"e": "late-free-fire-refused", "ai": "X", "t": 13, "refusal": "few-passing"}]
    checks.append(("clean log passes I2", status(clean, "I2 no peace after latch") == "PASS"))
    flip = base + [{"e": "strategy", "ai": "X", "t": 13, "war": False, "fire": False, "reason": "peace"},
                   {"e": "director", "ai": "X", "t": 13, "war": False, "retaliationLatched": True, "unpoweredWeapons": 0, "searchesOpenAtEnd": 0}]
    checks.append(("post-latch peace FAILs I2", status(flip, "I2 no peace after latch") == "FAIL"))
    bucket = [dict(r) for r in base]; bucket[3]["commitLiveByFacing"] = [1, 1, 0, 0]
    checks.append(("short buckets FAIL I1a", status(bucket, "I1a facing buckets sum to live") == "FAIL"))
    nobuild = [dict(r) for r in base]; nobuild[0] = {k: v for k, v in nobuild[0].items() if k != "build"}
    checks.append(("missing build FAILs I1a", status(nobuild, "I1a init build+config") == "FAIL"))
    hold = base + [{"e": "strategy", "ai": "X", "t": 14, "war": True, "reason": "pulse-wait"}]
    checks.append(("hold without outcome FAILs S1", status(hold, "S1 every candidate hold has an outcome") == "FAIL"))
    late = base + [{"e": "late-free-fire", "ai": "X", "t": 15, "held": "pulse-wait"},
                   {"e": "fire", "ai": "X", "t": 15, "firingHits": 3, "firingMisses": 1},
                   {"e": "fire-paid", "ai": "X", "t": 15}]
    checks.append(("paid != firing FAILs S1", status(late, "S1 late volleys paid = firing hits+misses") == "FAIL"))
    slow = [dict(r) for r in base]; slow[3]["aimWaitAfterHuman"] = 60.0
    checks.append(("60 s wait FAILs DNH", status(slow, "DNH aim wait after human") == "FAIL"))
    checks.append(("no latch is UNTESTED", status([base[0], dict(base[1], retaliationLatched=False)], "I2 no peace after latch") == "UNTESTED"))
    d12 = dict(base[1], warTurn=20, retaliationLatchTurn=12, aimedWeapons=10)
    d13 = dict(base[1], t=13, warTurn=20, retaliationLatchTurn=12, aimedWeapons=6)
    drop = [base[0], d12, d13, base[2], dict(base[2], t=13)]
    checks.append(("falling aimedWeapons FAILs I2", status(drop, "I2 aimedWeapons do not fall while latched") == "FAIL"))
    late_latch = [base[0], dict(base[1], warTurn=12, retaliationLatchTurn=12), base[2], dict(base[2], t=13, war=False)]
    checks.append(("post-schedule latch is UNTESTED", status(late_latch, "I2 no peace after latch") == "UNTESTED"))
    odd = base + [{"e": "late-free-fire-refused", "ai": "X", "t": 16, "refusal": "energy", "energyLeft": 5000, "reservation": 3000,
                   "materialLeft": 0, "materialReservation": 0}]
    checks.append(("unexplained refusal FAILs S1", status(odd, "S1 no unexplained energy/material refusal") == "FAIL"))
    fast = base + [{"e": "strategy", "ai": "X", "t": 17, "war": True, "reason": "saturated", "lastStrike": 16}]
    checks.append(("pulse 1 turn after strike FAILs S1", status(fast, "S1 pulse cadence kept") == "FAIL"))
    hold = base + [{"e": "strategy", "ai": "X", "t": 20 + i, "war": True, "reason": "max-hold"} for i in range(5)]
    checks.append(("max-hold flood FAILs S1", status(hold, "S1 max-hold not above baseline") == "FAIL"))
    checks.append(("a restart splits the log into 2 segments", len(segments(base + base)) == 2))
    hf = "DNH HUMAN-FIRST share of fire turns"
    checks.append(("clean log passes HUMAN-FIRST", status(base, hf) == "PASS"))
    other = base + [dict(base[2], t=13 + i, commit="Bot", humanFirst=False, reason="attrition") for i in range(2)]
    checks.append(("attrition at a non-human FAILs HUMAN-FIRST", status(other, hf) == "FAIL"))
    ret = base + [dict(base[2], t=13 + i, commit="Bot", humanFirst=False, reason="return-fire") for i in range(2)]
    checks.append(("return fire at a non-human does not FAIL HUMAN-FIRST", status(ret, hf) == "PASS"))
    unflagged = base + [dict(base[2], t=13 + i, humanFirst=False, reason="attrition") for i in range(2)]
    checks.append(("unflagged volleys at the human pass HUMAN-FIRST", status(unflagged, hf) == "PASS"))
    sf = "DNH reserve shortfall"
    eco = lambda t, n: {"e": "economy", "ai": "X", "t": t, "buildings": {"city": n}}
    short = [base[0], dict(base[1], reserveShortfall=50), base[2], base[3], eco(11, 100), eco(12, 95)]
    checks.append(("shortfall on a non-collapse turn FAILs DNH", status(short, sf) == "FAIL"))
    crash = [base[0], dict(base[1], reserveShortfall=50), dict(base[1], t=11), base[2], base[3], eco(11, 100), eco(12, 70)]
    checks.append(("shortfall on a collapse turn is excluded", status(crash, sf) == "PASS"))
    term = "I1a every placement search terminal"
    ps = lambda i, out: {"e": "place-search", "ai": "X", "t": 12, "searchId": i, "outcome": out, "filtered": 1}
    two = [base[0], dict(base[1], placeSearchesStarted=2), base[2], base[3], ps(1, "placed"), ps(2, "gave-up")]
    checks.append(("2 started, 2 terminal: placement PASS", status(two, term) == "PASS"))
    checks.append(("2 started, 1 terminal: placement FAILs", status(two[:-1], term) == "FAIL"))
    checks.append(("a duplicate terminal FAILs", status(two[:-1] + [ps(1, "open")], term) == "FAIL"))
    checks.append(("before v0.21 the placement gate is UNTESTED", status(base, term) == "UNTESTED"))
    checks.append(("an orphan record on an AI-turn without a director record FAILs", status(two + [dict(ps(3, "open"), t=13)], term) == "FAIL"))
    checks.append(("no search started is UNTESTED", status([base[0], dict(base[1], placeSearchesStarted=0), base[2], base[3]], term) == "UNTESTED"))
    nofield = [base[0], dict(base[1], placeSearchesStarted=1), dict(base[1], t=13), base[2], base[3], ps(1, "placed")]
    checks.append(("a director record without the field FAILs", status(nofield, term) == "FAIL"))
    i9 = "I9 land placements given up per AI-turn"
    many = [base[0], dict(base[1], placeSearchesStarted=0, placementFailures={"bui_SolarPlant": 3}), base[2], base[3]]
    checks.append(("3 solar given up in 1 AI-turn FAILs I9", status(many, i9) == "FAIL"))
    i9b = "I9b land plants stranded at sea per AI-turn"
    strand = lambda i: {"e": "place-search", "ai": "X", "t": 12, "searchId": i, "outcome": "gave-up", "building": "bui_SolarPlant", "filtered": 100, "noSameMedium": 100}
    wet = dict(base[1], placeSearchesStarted=1, unpoweredWater=3, unpoweredLand=0)
    checks.append(("a stranded solar plant in 1 AI-turn FAILs I9b", status([base[0], wet, base[2], base[3], strand(1)], i9b) == "FAIL"))
    checks.append(("water unpowered and nothing stranded PASSes I9b", status([base[0], wet, base[2], base[3]], i9b) == "PASS"))
    checks.append(("no water-only unpowered turn is UNTESTED", status([base[0], dict(wet, unpoweredWater=0), base[2], base[3]], i9b) == "UNTESTED"))
    checks.append(("a crowded plant (not only water anchors) does not count for I9b",
                   status([base[0], wet, base[2], base[3], dict(strand(1), noSameMedium=10)], i9b) == "PASS"))
    checks.append(("water and defense give-ups do not count for I9",
                   status([base[0], dict(base[1], placeSearchesStarted=0, placementFailures={"bui_OffshoreMine": 3, "bui_IonDefense": 4}), base[2], base[3]], i9) == "PASS"))
    failed = [n for n, ok in checks if not ok]
    for n, ok in checks:
        print(("ok    " if ok else "FAIL  ") + n)
    return 0 if not failed else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="*")
    ap.add_argument("--human", default="jogi")
    ap.add_argument("--ai", help="comma-separated smart AI nicknames to include")
    ap.add_argument("--baseline-fa4", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    a = ap.parse_args()
    if a.selftest:
        return selftest()
    if not a.files:
        ap.error("give a decision log")
    if a.baseline_fa4:
        tot = [0] * 6
        for f in a.files:
            for i, v in enumerate(fa4(load(f))):
                tot[i] += v
        print("war turns %d; idle (>=10k E) %d: at cap + material<2k %d, below cap + material<2k %d, at cap with material %d, neither %d"
              % tuple(tot))
        return 0
    any_fail = False
    for f in a.files:
        print("== " + f)
        segs = segments(load(f))
        for i, seg in enumerate(segs):
            ts = [r["t"] for r in seg if isinstance(r.get("t"), int)]
            print("-- segment %d/%d (T%s-T%s, %d records)" % (i + 1, len(segs), min(ts) if ts else "?", max(ts) if ts else "?", len(seg)))
            for name, status, detail in evaluate(seg, a.human, set(a.ai.split(",")) if a.ai else None):
                print("%-9s %-42s %s" % (status, name, detail))
                any_fail |= status == "FAIL"
    return 1 if any_fail else 0


if __name__ == "__main__":
    sys.exit(main())
