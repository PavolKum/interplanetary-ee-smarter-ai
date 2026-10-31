"""List which tech unlocks which building / upgrade / project, with science cost and full prerequisite chain.

Usage: python tech_unlocks.py [substring ...]   (filters unlock ids; no args = all)
"""
import csv
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
CSV = os.path.join(HERE, "..", "..", "docs", "ruleset", "TechTreeData.csv")


def load():
    with open(CSV, encoding="utf-8") as f:
        rows = {r[0]: r[1:] for r in csv.reader(f) if r}
    ids = rows["id"]
    techs = {}
    for i, tid in enumerate(ids):
        if not tid.startswith("tec_"):
            continue

        def cell(name):
            v = rows.get(name, [])
            return v[i].strip() if i < len(v) else ""

        def split(s):
            return [x.strip().strip('"') for x in s.replace("\n", ",").split(",") if x.strip().strip('"')]

        techs[tid] = {
            "cost": int(cell("science_cost") or 0),
            "parents": split(cell("parent_techs")),
            "unlocks": split(cell("building_unlock")) + split(cell("upgrade_unlock")) + split(cell("project_unlock")),
        }
    return techs


def chain(techs, tid, seen=None):
    seen = seen if seen is not None else []
    for p in techs[tid]["parents"]:
        if p in techs and p not in seen:
            chain(techs, p, seen)
    if tid not in seen:
        seen.append(tid)
    return seen


def main():
    techs = load()
    filters = sys.argv[1:]
    for tid, t in techs.items():
        hits = [u for u in t["unlocks"] if not filters or any(f in u for f in filters)]
        if not hits:
            continue
        c = chain(techs, tid)
        total = sum(techs[x]["cost"] for x in c)
        print("%-34s %5d sci (chain %5d, %d techs)  -> %s" % (tid, t["cost"], total, len(c), ", ".join(hits)))


if __name__ == "__main__":
    main()
