"""Summarize an InterplanetaryAI decision log (BepInEx/InterplanetaryAI/decisions-*.jsonl).

Usage: python summarize_decisions.py [path-to-jsonl]   (default: newest log in the game folder)
"""
import glob
import json
import os
import sys
from collections import Counter, defaultdict

GAME_LOGS = r"C:\Program Files (x86)\Steam\steamapps\common\Interplanetary Enhanced Edition\BepInEx\InterplanetaryAI"


def newest_log():
    files = glob.glob(os.path.join(GAME_LOGS, "decisions-*.jsonl"))
    if not files:
        sys.exit("No decision logs in " + GAME_LOGS)
    return max(files, key=os.path.getmtime)


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else newest_log()
    rows = [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()]
    print("log:", path, "-", len(rows), "records")

    # Only the last match in the file: everything after the last block of init records.
    last_init = max((i for i, r in enumerate(rows) if r["e"] == "init"), default=-1)
    first_init = last_init
    while first_init > 0 and rows[first_init - 1]["e"] == "init":
        first_init -= 1
    if first_init >= 0:
        for r in rows[first_init:last_init + 1]:
            print(f"  init {r['ai']:<8} id={r['id']} lvl={r['lvl']} active={r['active']} acc {r.get('nativeAcc')} -> {r.get('acc', 'n/a') if r.get('smart', True) else 'native'}" + (" ORIGINAL AI" if r.get('smart') is False else ""))
        rows = rows[last_init + 1:]

    turns = sorted({r["t"] for r in rows})
    if not turns:
        print("no turn records yet")
        return
    print(f"turns {turns[0]}-{turns[-1]}")

    assigned = defaultdict(Counter)       # ai -> target -> weapons assigned
    primary = defaultdict(Counter)        # ai -> target -> primary trajectories found
    fired = Counter()
    planned = Counter()
    strikes = Counter()
    human_names = set()
    for r in rows:
        if r["e"] == "target":
            for e in r["enemies"]:
                assigned[r["ai"]][e["name"]] += e["assigned"]
                if e["human"]:
                    human_names.add(e["name"])
        elif r["e"] == "fire":
            for name, n in r["primaryByTarget"].items():
                primary[r["ai"]][name] += n
            fired["hits"] += r["firingHits"]
            fired["misses"] += r["firingMisses"]
            planned["hits"] += r["plannedHits"]
            planned["powered"] += r["powered"]
            planned["primaryFound"] += r["primaryFound"]
            planned["secondaryOnly"] += r["secondaryOnly"]
        elif r["e"] == "strike":
            strikes[(r["target"], r["building"])] += 1

    print("\nweapon-turns assigned per target (primary trajectory found):")
    for ai in sorted(assigned):
        parts = [f"{t}{'*' if t in human_names else ''} {n} ({primary[ai][t]})" for t, n in assigned[ai].most_common()]
        print(f"  {ai:<8} " + ", ".join(parts))
    print("  (* = human)")

    powered = planned["powered"] or 1
    print(f"\npowered weapon-turns {planned['powered']}: primary found {planned['primaryFound']} "
          f"({100 * planned['primaryFound'] / powered:.0f}%), secondary only {planned['secondaryOnly']}")
    print(f"planned hits {planned['hits']}, fired as hits {fired['hits']}, fired as misses {fired['misses']}")
    if strikes:
        print("\nguided strikes:", ", ".join(f"{t}/{b} {n}" for (t, b), n in strikes.most_common()))

    last = [r for r in rows if r["e"] == "target" and r["t"] == turns[-1]]
    if last:
        print(f"\nthreat scores at T{turns[-1]} (score: leader / grudge / finish):")
        for r in last:
            cells = [f"{e['name']} {e['score']:.2f} ({e['leader']:.2f}/{e['grudgeTerm']:.2f}/{e['finish']:.2f})" for e in r["enemies"]]
            print(f"  {r['ai']:<8} " + "; ".join(cells))

    summarize_economy(rows)


def summarize_economy(rows):
    economy = [r for r in rows if r["e"] == "economy"]
    if not economy:
        return
    print("\neconomy per AI (energy / material / science per turn; planet material):")
    by_ai = defaultdict(list)
    for r in economy:
        by_ai[r["ai"]].append(r)
    for ai in sorted(by_ai):
        series = by_ai[ai]
        marks = [r for r in series if r["t"] % 10 == 0] + [series[-1]]
        cells = [f"T{r['t']} {r['energyPerTurn']}/{r['materialPerTurn']}/{r['sciencePerTurn']} pm{r['planetMaterial']}{' G' if r['growth'] else ''}" for r in marks]
        print(f"  {ai:<8} " + "  ".join(cells))
        last = series[-1]
        print(f"           buildings T{last['t']}: " + ", ".join(f"{k} {v}" for k, v in sorted(last["buildings"].items(), key=lambda kv: -kv[1])))
        goal_turns = [r["t"] for r in series if r["goal"]]
        if goal_turns:
            print(f"           saving for {series[-1]['goal'] or 'goal'} from T{goal_turns[0]}; last reserve E{last['reservedEnergy']} M{last['reservedMaterial']}")

    supers = [r for r in rows if r["e"] == "superweapon"]
    print("\nsuperweapons queued:", ", ".join(f"T{r['t']} {r['ai']} {r['building']}" for r in supers) if supers else "none")

    research = defaultdict(list)
    for r in rows:
        if r["e"] == "research":
            research[r["ai"]].append(r["target"].replace("tec_", ""))
    if research:
        print("\nresearch plan targets queued:")
        for ai in sorted(research):
            print(f"  {ai:<8} {len(research[ai])}: " + ", ".join(research[ai][-6:]) + (" (last 6)" if len(research[ai]) > 6 else ""))

    builds = [r for r in rows if r["e"] == "build"]
    if builds:
        picks = [0, 0, 0, 0]
        queued = [0, 0, 0, 0]
        defenses = Counter()
        for r in builds:
            for i in range(4):
                picks[i] += r["picks"][i]
                queued[i] += r["queued"][i]
            defenses.update(r.get("defensesQueued", {}))
        names = ["firepower", "energy", "material", "defense"]
        print("\nbuild planner picks -> module said yes: " + ", ".join(f"{names[i]} {picks[i]}->{queued[i]}" for i in range(4)))
        if defenses:
            print("smart defense queued: " + ", ".join(f"{k.replace('bui_', '')} {v}" for k, v in defenses.most_common()))

    summarize_director(rows)


def summarize_director(rows):
    director = [r for r in rows if r["e"] == "director"]
    if not director:
        return
    print("\ndirector (v0.3) per AI:")
    by_ai = defaultdict(list)
    for r in director:
        by_ai[r["ai"]].append(r)
    for ai in sorted(by_ai):
        series = by_ai[ai]
        totals = Counter()
        failures = Counter()
        for r in series:
            totals.update(r.get("queued", {}))
            failures.update(r.get("placementFailures", {}))
        war_from = next((r["t"] for r in series if r["war"]), None)
        last = series[-1]
        unspent = [r["energyLeft"] for r in series]
        print(f"  {ai:<8} turns {series[0]['t']}-{last['t']}, war from T{war_from}, upgrades installed at end {last['upgradesInstalled']}, "
              f"refunds {sum(r['refunds'] for r in series)}, mean energy left unspent {sum(unspent) // max(1, len(unspent))}")
        print("           queued: " + ", ".join(f"{k.split(':', 1)[1].split('_', 1)[-1]}({k.split(':')[0]}) {v}" for k, v in totals.most_common(14)))
        if failures:
            print("           placement gave up: " + ", ".join(f"{k.replace('bui_', '')} {v}" for k, v in failures.most_common()))
        slots = [f"T{r['t']}:{r['slotsAtStart']}->{r['slotsLeft']}" for r in series if r["t"] % 5 == 0]
        print("           power slots start->left: " + " ".join(slots))


if __name__ == "__main__":
    main()
