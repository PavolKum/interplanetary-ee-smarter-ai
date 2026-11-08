"""Fair-intel turn brief: human telemetry only (own planet, own shots, visible damage)."""
import glob
import json
import os
import sys

REC = r"C:\Program Files (x86)\Steam\steamapps\common\Interplanetary Enhanced Edition\BepInEx\plugins\InterplanetaryTelemetry\recordings"
path = sys.argv[1] if len(sys.argv) > 1 else max(glob.glob(os.path.join(REC, "*.jsonl")), key=os.path.getmtime)

states = {}      # turn -> last state seen
shots = {}       # turn -> [n, hits]
damage = {}      # turn -> [n, sum]
finished = None
for line in open(path, encoding="utf-8"):
    try:
        d = json.loads(line)
    except ValueError:
        continue
    t = d.get("turn") or 0
    if d.get("state") and d["state"].get("player"):
        states[t] = d["state"]
    if d.get("eventType") == "shot" and d.get("shot"):
        s = shots.setdefault(t, [0, 0])
        s[0] += 1
        s[1] += 1 if d["shot"].get("hitOtherPlayerPlanet") else 0
    if d.get("eventType") == "damage" and d.get("damage"):
        s = damage.setdefault(t, [0, 0])
        s[0] += 1
        s[1] += d["damage"].get("nominalAmount") or 0
    if d.get("eventType") == "game_finished":
        finished = line[:300]

if not states:
    print("no state yet:", os.path.basename(path))
    sys.exit(0)
turns = sorted(states)
cur = states[turns[-1]]
prev = states[turns[-2]] if len(turns) > 1 else None


def summary(st):
    p = st["player"]
    r = p["resources"]
    b = {x["id"]: x["count"] for x in p.get("buildings", [])}
    return p, r, b


p, r, b = summary(cur)
print("session", os.path.basename(path)[:8], "turn", cur.get("turn"), "phase", cur.get("phase"), "status", p.get("status"), p.get("turnStatus"))
print("energy %d/%d (+%d/t)  materials %d/%d (+%d/t)  planet %d/%d" % (
    r["energy"], r["energyCapacity"], r["energyPerTurn"], r["materials"], r["materialsCapacity"], r["materialsPerTurn"],
    r["planetMaterials"], r["planetMaterialsInitial"]))
print("pop %dM (+%d/t)  science +%d/t  intel %s  techs %d" % (
    r["populationMillions"], r["populationGrowthMillionsPerTurn"], r["sciencePerTurn"], r.get("intel"), p.get("technologiesUnlocked", 0)))
cities = {k: v for k, v in b.items() if k.startswith("cit_")}
defs = {k: v for k, v in b.items() if "Defense" in k}
print("cities", cities, " defenses", sum(defs.values()), defs)
print("other", {k: v for k, v in b.items() if k not in cities and k not in defs})
if prev:
    _, _, pb = summary(prev)
    diff = {k: b.get(k, 0) - pb.get(k, 0) for k in set(b) | set(pb) if b.get(k, 0) != pb.get(k, 0)}
    print("change since turn", prev.get("turn"), diff)
for t in sorted(set(shots) | set(damage))[-3:]:
    print("turn", t, "own shots/hit planet", shots.get(t), " visible damage events/sum", damage.get(t))
if finished:
    print("FINISHED", finished)
