"""Per-turn summary of the human telemetry: weapons, defenses, shots fired, research completed, counterintel.

Usage: python AI/tools/human_summary.py docs/playtests/data/v0131-human-telemetry.jsonl
"""
import collections
import json
import sys

path = sys.argv[1]
last_state = {}
shots = collections.Counter()
hits_planet = collections.Counter()
completed_at = {}

for line in open(path, encoding="utf-8"):
    d = json.loads(line)
    t = d.get("turn")
    if d.get("eventType") == "shot":
        s = d.get("shot") or {}
        shots[(t, s.get("weaponType"))] += 1
        if s.get("hitOtherPlayerPlanet"):
            hits_planet[t] += 1
    state = d.get("state")
    if state and state.get("player"):
        last_state[t] = state["player"]
        for r in state["player"].get("research", []):
            if r.get("completed") and r["id"] not in completed_at:
                completed_at[r["id"]] = t

keys = ("bui_Railgun", "bui_Coilcannon", "bui_Missile", "bui_Laser", "bui_KineticDefense", "bui_IonDefense",
        "bui_ShieldDefense", "bui_DataSecurityHub", "bui_TelescopeArray")
print("turn  " + " ".join(k.replace("bui_", "")[:8].rjust(8) for k in keys) + "   shots(by type)  ci_mult")
for t in sorted(last_state):
    p = last_state[t]
    b = {x["id"]: x["count"] for x in p.get("buildings", [])}
    shot_str = ",".join("%s:%d" % (w.replace("type_", ""), n) for (tt, w), n in sorted(shots.items()) if tt == t)
    print("%4d  " % t + " ".join(str(b.get(k, 0)).rjust(8) for k in keys) + "   " + (shot_str or "-")
          + "  " + str(p.get("resources", {}).get("counterintelMultiplier")))

print("\nresearch completed (turn first seen):")
for tech, t in sorted(completed_at.items(), key=lambda x: (x[1], x[0])):
    print("  T%d %s" % (t, tech))

# Upgrades, if the snapshot carries them
p = last_state[max(last_state)]
for key in ("upgrades", "installedUpgrades"):
    if key in p:
        print("\n%s at end:" % key, p[key])
