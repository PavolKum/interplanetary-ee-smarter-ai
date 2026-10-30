"""Consensus research order from the human's recorded winning matches (recorder debriefs).

Each tech gets the mean of its normalized position (0 = first, 1 = last) across the matches where it was
researched; techs researched in fewer than half the matches are dropped. Prints a comma-separated list
for the [Research] Plan config entry.

Usage: python human_research_order.py [debriefs-dir]
"""
import glob
import json
import os
import sys
from collections import defaultdict

DEFAULT_DIR = os.path.expanduser(r"~\Repos_Local\ScheduleImodes\interplanetary\debriefs")


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_DIR
    orders = []
    for path in sorted(glob.glob(os.path.join(root, "*", "facts.json"))):
        data = json.load(open(path, encoding="utf-8"))
        if "meta" not in data or data["meta"].get("outcome") != "Victorious":
            continue
        techs = [t for e in data["events"] if e["kind"] == "research" for t in e.get("techIds", [])]
        if techs:
            orders.append((os.path.basename(os.path.dirname(path)), techs))

    positions = defaultdict(list)
    for _, techs in orders:
        for i, tech in enumerate(techs):
            positions[tech].append(i / max(1, len(techs) - 1))

    keep = [t for t, p in positions.items() if len(p) * 2 >= len(orders)]
    keep.sort(key=lambda t: sum(positions[t]) / len(positions[t]))
    print(f"{len(orders)} winning matches: " + ", ".join(f"{name} ({len(t)})" for name, t in orders), file=sys.stderr)
    print(f"{len(keep)} techs in at least half of them", file=sys.stderr)
    print(",".join(keep))


if __name__ == "__main__":
    main()
