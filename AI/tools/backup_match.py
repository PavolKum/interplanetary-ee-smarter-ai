"""Back up one match's evidence files (git-ignored docs/playtests/data/<prefix>-*) with a sha256 manifest (council I1b).

The raw logs were a single local copy; playtest notes and gates use them as baselines. This copies them to a durable
folder (default: OneDrive, so it leaves the machine) and verifies every copy against the manifest.

Usage:  python AI/tools/backup_match.py v020m27 [--dest <folder>]
Exit code: 0 when every file was copied and verified; 1 otherwise.
"""
import argparse
import glob
import hashlib
import os
import shutil
import sys

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DATA = os.path.join(REPO, "docs", "playtests", "data")
DEFAULT_DEST = os.path.join(os.path.expanduser("~"), "OneDrive", "Backups", "interplanetary-playtests")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prefix", help="match prefix, e.g. v019m26")
    ap.add_argument("--dest", default=DEFAULT_DEST)
    a = ap.parse_args()
    files = sorted(glob.glob(os.path.join(DATA, a.prefix + "-*")))
    if not files:
        print("no files for prefix %s in %s" % (a.prefix, DATA))
        return 1
    dest = os.path.join(a.dest, a.prefix)
    os.makedirs(dest, exist_ok=True)
    lines, ok = [], True
    for src in files:
        name = os.path.basename(src)
        digest = sha256(src)
        target = os.path.join(dest, name)
        shutil.copy2(src, target)
        copied = sha256(target)
        status = "ok" if copied == digest else "MISMATCH"
        ok &= copied == digest
        lines.append("%s\tsha256:%s\t%d" % (name, digest, os.path.getsize(src)))
        print("%-8s %s %s" % (status, digest[:16], name))
    manifest = os.path.join(dest, "manifest.tsv")
    with open(manifest, "w", encoding="utf-8", newline="\n") as f:
        f.write("# file\tsha256\tbytes\n" + "\n".join(lines) + "\n")
    print("manifest: %s (sha256:%s)" % (manifest, sha256(manifest)))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
