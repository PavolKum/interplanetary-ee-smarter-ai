"""Scripted mutation testing for the Brain unit tests (v0.20, council I1b).

Copies AI/Brain and AI/Tests into a temporary directory, applies each mutant from AI/tools/mutants.tsv to the copy,
runs the tests there and records killed (at least one check failed) or survived. The repository is never modified.

Usage:  python AI/tools/run_mutants.py [--only id1,id2]
Exit code: 0 when every mutant is killed and the unmutated baseline passes; 1 otherwise.
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile

AI_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
MUTANTS = os.path.join(AI_DIR, "tools", "mutants.tsv")
SUMMARY = re.compile(r"(\d+) checks, (\d+) failed")


def load_mutants(only):
    rows = []
    for raw in open(MUTANTS, encoding="utf-8"):
        line = raw.rstrip("\n")
        if not line.strip() or line.startswith("#"):
            continue
        parts = line.split("\t")
        if len(parts) != 5:
            sys.exit("bad mutants.tsv line (need 5 tab-separated fields): " + line)
        mid, rel, old, new, expect = [p.replace("\\t", "\t") for p in parts]
        if only and mid not in only:
            continue
        rows.append((mid, rel, old, new, expect))
    return rows


def run_tests(root):
    proc = subprocess.run(["dotnet", "run", "-c", "Release"], cwd=os.path.join(root, "Tests"),
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = proc.stdout + proc.stderr
    m = SUMMARY.search(out)
    if not m:
        return None, out  # build error or crash: the tests did not run
    fails = [l[len("FAIL: "):] for l in out.splitlines() if l.startswith("FAIL: ")]
    return (int(m.group(1)), int(m.group(2)), fails), out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", help="comma-separated mutant ids")
    a = ap.parse_args()
    only = set(a.only.split(",")) if a.only else None
    mutants = load_mutants(only)
    if not mutants:
        sys.exit("no mutants selected")
    ok = True
    with tempfile.TemporaryDirectory(prefix="ai-mutants-") as tmp:
        work = os.path.join(tmp, "AI")
        shutil.copytree(os.path.join(AI_DIR, "Brain"), os.path.join(work, "Brain"))
        shutil.copytree(os.path.join(AI_DIR, "Tests"), os.path.join(work, "Tests"),
                        ignore=shutil.ignore_patterns("bin", "obj"))
        base, out = run_tests(work)
        if base is None or base[1] != 0:
            print("BASELINE FAILED: the unmutated tests must pass first")
            print(out[-2000:])
            return 1
        print("baseline: %d checks, 0 failed" % base[0])
        for mid, rel, old, new, expect in mutants:
            path = os.path.join(work, rel)
            src = open(path, encoding="utf-8").read()
            n = src.count(old)
            if n != 1:
                print("%-20s INVALID  original text occurs %d times in %s" % (mid, n, rel))
                ok = False
                continue
            open(path, "w", encoding="utf-8", newline="").write(src.replace(old, new))
            try:
                res, out = run_tests(work)
            finally:
                open(path, "w", encoding="utf-8", newline="").write(src)
            if res is None:
                print("%-20s INVALID  does not compile (exercises no test)" % mid)  # Fable v0.20 #8
                ok = False
                continue
            total, failed, fails = res
            if failed == 0:
                print("%-20s SURVIVED %d checks pass" % (mid, total))
                ok = False
                continue
            hit = any(expect in f for f in fails)
            print("%-20s KILLED   %d failed%s; first: %s" % (mid, failed, "" if hit else " (expected check not among them)", fails[0][:80]))
    print("ALL KILLED" if ok else "SURVIVORS OR INVALID MUTANTS")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
