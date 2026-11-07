"""Live match monitor: one compact line per AI per turn, one per human turn, plus mod errors.

Tails the newest AI decision log (BepInEx/InterplanetaryAI/decisions-*.jsonl), the newest telemetry
recording (BepInEx/plugins/InterplanetaryTelemetry/recordings/*.jsonl) and BepInEx/LogOutput.log.
Starts at the end of the files that exist (nothing old is replayed) and picks up new files as they appear.

Usage: python -u live_monitor.py [--replay]   (--replay prints the current files from the start, for testing)
"""
import glob
import json
import os
import sys
import time

GAME = r"C:\Program Files (x86)\Steam\steamapps\common\Interplanetary Enhanced Edition"
DECISIONS = os.path.join(GAME, "BepInEx", "InterplanetaryAI", "decisions-*.jsonl")
RECORDINGS = os.path.join(GAME, "BepInEx", "plugins", "InterplanetaryTelemetry", "recordings", "*.jsonl")
BEPINEX_LOG = os.path.join(GAME, "BepInEx", "LogOutput.log")


class Tail:
    def __init__(self, pattern, replay):
        self.pattern = pattern
        self.path = None
        self.offset = 0
        self.replay = replay
        self.buffer = ""

    def newest(self):
        files = glob.glob(self.pattern) if any(c in self.pattern for c in "*?") else [self.pattern]
        files = [f for f in files if os.path.isfile(f)]
        return max(files, key=os.path.getmtime) if files else None

    def lines(self):
        newest = self.newest()
        if newest is None:
            return []
        if newest != self.path:
            first = self.path is None
            self.path = newest
            self.offset = 0 if (self.replay or not first) else os.path.getsize(newest)
            self.buffer = ""
            if not first:
                print("monitor: following " + os.path.basename(newest), flush=True)
        size = os.path.getsize(self.path)
        if size < self.offset:  # truncated / rewritten (LogOutput.log on game start)
            self.offset = 0
        if size == self.offset:
            return []
        with open(self.path, "r", encoding="utf-8", errors="replace") as f:
            f.seek(self.offset)
            data = f.read()
            self.offset = f.tell()
        data = self.buffer + data
        parts = data.split("\n")
        self.buffer = parts[-1]
        return [p for p in parts[:-1] if p.strip()]


def short(building_id):
    return building_id.replace("bui_", "").replace("upg_", "").replace("pro_", "")


def describe_queued(queued):
    groups = {}
    for key, n in queued.items():
        role, _, ident = key.partition(":")
        groups.setdefault(role, []).append((short(ident.split("@")[0]), n))
    parts = []
    for role in ("upgrade", "project", "material", "power", "intel", "counterintel", "defense", "weapon", "superweapon", "abandoned"):
        if role in groups:
            items = sorted(groups[role], key=lambda x: -x[1])
            parts.append(role + " " + ",".join(f"{name}x{n}" for name, n in items[:4]))
    return " | ".join(parts) if parts else "nothing"


def main():
    replay = "--replay" in sys.argv
    decisions = Tail(DECISIONS, replay)
    recordings = Tail(RECORDINGS, replay)
    bepinex = Tail(BEPINEX_LOG, replay)
    economy = {}
    human_turn_seen = set()
    print("monitor: watching AI decisions, human telemetry and the BepInEx log", flush=True)
    while True:
        for line in bepinex.lines():
            if "Smarter AI" in line and "loaded" in line:
                print("MOD: " + line.split("] ", 1)[-1][:220], flush=True)
            elif "Hook '" in line or "Failed to patch" in line or "Hook target not found" in line or ("Lasers:" in line and "Error" in line):
                print("MOD ERROR: " + line[:300], flush=True)
        for line in decisions.lines():
            try:
                r = json.loads(line)
            except ValueError:
                continue
            kind = r.get("e")
            if kind == "economy":
                economy[r["ai"]] = r
            elif kind == "director":
                e = economy.get(r["ai"], {})
                buildings = e.get("buildings", {})
                top = ",".join(f"{k}{v}" for k, v in sorted(buildings.items(), key=lambda kv: -kv[1])[:5])
                fails = r.get("placementFailures") or {}
                print(f"T{r['t']} {r['ai']}: E/t {e.get('energyPerTurn', '?')} M/t {e.get('materialPerTurn', '?')} sci {e.get('sciencePerTurn', '?')} "
                      f"bld {sum(buildings.values())} [{top}] upg {r['upgradesInstalled']} slots {r['slotsAtStart']}"
                      f" intel {r.get('intelBuildings', '?')} blind {r.get('blindEnemies', '?')} reserveE {r.get('defenseReserve', '?')}"
                      f"{' WAR' if r['war'] else ''}{' HOLD+' + str(r.get('releasedEnergy')) + 'E' if r.get('holdingFire') else ''}"
                      f"{' GLUT' if r.get('materialGlut') else ''}{' EGLUT' if r.get('energyGlut') else ''}{' ESAT' if r.get('energySaturated') else ''}"
                      f"{' MBOUND' if r.get('materialBound') else ''}"
                      f"{' XFIRST ' + str(r.get('extractionFirstBought')) + 'x/' + str(r.get('extractionFirstMaterial')) + 'M' if r.get('extractionFirstBought') else ''}"
                      f" planet {e.get('planetMaterial', '?')} stockM {r.get('materialStart', '?')}"
                      f"{' keepM ' + str(r.get('materialReserve')) if r.get('materialReserve') else ''}"
                      f"{' SOLD ' + str(r.get('soldMines')) + ' mines +' + str(r.get('soldMaterial')) + 'M' if r.get('soldMines') else ''}"
                      f" aimedW {r.get('aimedWeapons', '?')}"
                      f"{' COV ' + format(r.get('coveredBuildings'), '.0%') + ' cities ' + format(r.get('coveredCities'), '.0%') if isinstance(r.get('coveredBuildings'), (int, float)) and r.get('coveredBuildings') >= 0 else ''}"
                      f"{' shelter ' + str(r.get('shelteredPlaced')) + '/' + str((r.get('shelteredPlaced') or 0) + (r.get('unshelteredPlaced') or 0)) if (r.get('shelteredPlaced') or 0) + (r.get('unshelteredPlaced') or 0) > 0 else ''}"
                      f"{' REP ' + str(r.get('repaired')) + ' (-' + str(r.get('repairEnergy')) + 'E -' + str(r.get('repairMaterial')) + 'M)' if r.get('repaired') else ''}"
                      f" | {describe_queued(r.get('queued') or {})} | unspent E{r['energyLeft']}"
                      + (f" | placement gave up {fails}" if fails else ""), flush=True)
            elif kind == "fire":
                if (r.get("firingHits", 0) or 0) + (r.get("firingMisses", 0) or 0) > 0 or (r.get("primaryFound", 0) or 0) > 0:
                    print(f"T{r['t']} {r['ai']}: aimed {r.get('primaryFound')}/{r.get('powered')} live weapons {r.get('primaryByTarget')}"
                          f" -> firing {r.get('firingHits')} hits + {r.get('firingMisses')} misses; unreachable marked {r.get('unreachableMarked', '?')}", flush=True)
            elif kind == "strategy":
                passing = r.get("passingBy") or {}
                defenses = r.get("defensesBy") or {}
                per = " ".join(f"{k}:{passing.get(k)}thru/{defenses.get(k)}def" for k in passing)
                print(f"T{r['t']} {r['ai']}: {'FIRE' if r.get('fire') else 'hold'} ({r.get('reason')}) shots {r.get('shots')} "
                      f"-> {r.get('commit') or '-'} ~{r.get('passing')} through ({r.get('passShare')})"
                      f"{' HUMAN-FIRST' if r.get('humanFirst') else ''}"
                      f"{' (free E ' + str(r.get('freeEnergy')) + ' vs fire ' + str(r.get('firingCost')) + ')' if r.get('reason') in ('free-fire', 'below-saturation', 'pulse-wait') and r.get('freeEnergy') is not None else ''}"
                      f" | {per}", flush=True)
            elif kind == "abort":
                print(f"T{r['t']} {r['ai']}: STRIKE ABORTED - {r.get('hitsOnCommit')} of {r.get('shots')} weapons on target (needed {r.get('needed')})", flush=True)
            elif kind == "turn-start-error":
                print(f"T{r['t']} {r['ai']}: TURN-START ERROR in {r.get('module')} (swallowed): {r.get('error')}", flush=True)
            elif kind == "turn-start-missed":
                print(f"T{r['t']} {r['ai']}: TURN START MISSED (last started T{r.get('lastStartedTurn')}; E {r.get('energy')} M {r.get('material')})", flush=True)
            elif kind == "superweapon":
                print(f"T{r['t']} {r['ai']}: SUPERWEAPON queued {short(r['building'])}", flush=True)
            elif kind == "init":
                print(f"AI init: {r.get('ai')} level {r.get('lvl', r.get('level', '?'))}", flush=True)
        for line in recordings.lines():
            try:
                r = json.loads(line)
            except ValueError:
                continue
            if r.get("eventType") == "game_finished":
                print(f"GAME FINISHED at turn {r.get('turn')}", flush=True)
            state = r.get("state") or {}
            player = state.get("player")
            turn = r.get("turn")
            if not player or turn in human_turn_seen or r.get("eventType") not in ("turn_end", "round_start", "checkpoint"):
                continue
            human_turn_seen.add(turn)
            res = player.get("resources", {})
            blds = player.get("buildings") or []
            count = sum(b.get("count", 0) for b in blds if not b.get("id", "").startswith("cit_"))
            print(f"T{turn} YOU: E/t {res.get('energyPerTurn')} M/t {res.get('materialsPerTurn')} sci {res.get('sciencePerTurn')} "
                  f"bld {count} pop {res.get('populationMillions')}", flush=True)
        if replay:
            return
        time.sleep(2)


if __name__ == "__main__":
    main()
