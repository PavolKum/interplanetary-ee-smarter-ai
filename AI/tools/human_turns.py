"""Human turn timing and early builds from the telemetry.

Usage: python AI/tools/human_turns.py docs/playtests/data/v0131-human-telemetry.jsonl
Prints, per turn: seconds from round_start to turn_end (the human's planning time, during which the AI aims), and the
human's building counts for the first turns.
"""
import datetime
import json
import sys


def ts(s):
    s = s.rstrip("Z")
    if "." in s:
        head, frac = s.split(".")
        s = head + "." + frac[:6]
    return datetime.datetime.fromisoformat(s)


starts, ends, builds = {}, {}, {}
for line in open(sys.argv[1], encoding="utf-8"):
    d = json.loads(line)
    t, kind = d.get("turn"), d.get("eventType")
    if kind == "round_start" and t not in starts:
        starts[t] = ts(d["observedAtUtc"])
    if kind == "turn_end":
        ends[t] = ts(d["observedAtUtc"])
    st = d.get("state")
    if st and st.get("player") and t is not None and t <= 4:
        builds[t] = {b["id"]: b["count"] for b in st["player"].get("buildings", []) if not b["id"].startswith("cit_")}

print("turn  human_seconds")
for t in sorted(ends):
    if t in starts:
        print("%4d  %6.1f" % (t, (ends[t] - starts[t]).total_seconds()))
print()
for t in sorted(builds):
    print("T%d builds: %s" % (t, builds[t]))
