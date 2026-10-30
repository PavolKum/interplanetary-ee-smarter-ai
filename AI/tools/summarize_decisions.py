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
            print(f"  init {r['ai']:<8} id={r['id']} lvl={r['lvl']} active={r['active']} acc {r['nativeAcc']} -> {r['acc']}")
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


if __name__ == "__main__":
    main()
