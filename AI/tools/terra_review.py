"""Run a read-only Terra (GPT-6.1 via OpenCode) review and refuse to accept a silent failure.

Usage (from the repo root):
    python AI/tools/terra_review.py terra-ai-v0.7
reads docs/reviews/terra-ai-v0.7.prompt.txt, writes docs/reviews/terra-ai-v0.7.md (report) and .err (tool trace).

Why: on 31 Oct 2026 Terra failed silently four times. Two runs hung with nothing written (27 and 40 minutes,
cause unknown). Two ended with exit code 0 after two progress notes because a read outside the repo was
auto-rejected in read-only mode. "Exit 0 and a non-empty file" is not a report.

Guards:
  - stall:   the trace (.err) is still empty after --stall seconds -> kill, retry
  - timeout: the run takes longer than --timeout seconds -> kill, retry
  - no report: the output has no "verdict" line or is shorter than --min-bytes -> retry
Rejected tool calls ("auto-rejecting") are reported either way. A failed attempt is kept as <name>.attemptN.md/.err.
Exit code 0 only when an accepted report exists.
"""
import argparse
import os
import re
import shutil
import subprocess
import sys
import time

MODEL = "openai/gpt-6.1-terra"


def find_opencode():
    """The real opencode.exe: running npm's opencode.cmd through cmd.exe mangles a multi-line prompt argument."""
    found = shutil.which("opencode")
    if not found:
        return None
    if found.lower().endswith((".cmd", ".bat", ".ps1")) or not os.path.splitext(found)[1]:
        exe = os.path.join(os.path.dirname(found), "node_modules", "opencode-ai", "bin", "opencode.exe")
        if os.path.isfile(exe):
            return exe
    return found


def run_once(cmd, out_path, err_path, stall, timeout, poll):
    """Run cmd; return (reason or None, seconds). reason is 'stalled' or 'timeout' when the run was killed."""
    start = time.time()
    with open(out_path, "wb") as out, open(err_path, "wb") as err:
        proc = subprocess.Popen(cmd, stdout=out, stderr=err, stdin=subprocess.DEVNULL)
        while True:
            try:
                proc.wait(timeout=poll)
                return None, time.time() - start
            except subprocess.TimeoutExpired:
                pass
            elapsed = time.time() - start
            err.flush()
            if elapsed > stall and os.path.getsize(err_path) == 0:
                proc.kill()
                proc.wait()
                return "stalled (no trace after %ds)" % stall, elapsed
            if elapsed > timeout:
                proc.kill()
                proc.wait()
                return "timeout (%ds)" % timeout, elapsed


def check_report(out_path, err_path, min_bytes):
    """Return (accepted, problems, rejections)."""
    text = open(out_path, encoding="utf-8", errors="replace").read() if os.path.exists(out_path) else ""
    trace = open(err_path, encoding="utf-8", errors="replace").read() if os.path.exists(err_path) else ""
    trace = re.sub(r"\x1b\[[0-9;]*m", "", trace)
    rejections = [line.strip() for line in trace.splitlines() if "auto-rejecting" in line]
    problems = []
    if len(text.encode("utf-8")) < min_bytes:
        problems.append("report only %d bytes (< %d)" % (len(text.encode("utf-8")), min_bytes))
    if not re.search(r"verdict", text, re.IGNORECASE):
        problems.append("no 'verdict' line")
    return not problems, problems, rejections


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("name", help="review name, e.g. terra-ai-v0.7 (docs/reviews/<name>.prompt.txt)")
    ap.add_argument("--dir", default="docs/reviews")
    ap.add_argument("--model", default=MODEL, help="opencode model id, e.g. openai/gpt-6.1-sol")
    ap.add_argument("--retries", type=int, default=1)
    ap.add_argument("--stall", type=int, default=180, help="seconds without any trace before killing")
    ap.add_argument("--timeout", type=int, default=2400)
    ap.add_argument("--min-bytes", type=int, default=1500)
    ap.add_argument("--poll", type=float, default=5.0)
    ap.add_argument("--cmd", nargs=argparse.REMAINDER, help="test only: run this instead of opencode (prompt appended)")
    args = ap.parse_args()

    prompt_path = os.path.join(args.dir, args.name + ".prompt.txt")
    if not os.path.isfile(prompt_path):
        print("FAIL: prompt not found: " + prompt_path)
        return 2
    prompt = open(prompt_path, encoding="utf-8").read()
    if args.cmd:
        cmd = list(args.cmd) + [prompt]
    elif args.model.startswith("agy:"):
        # 2026-11-08 (Pavol): Gemini lanes run through the Antigravity CLI subscription, not OpenRouter credit.
        # "agy:gemini-4-argon-high" / "agy:gemini-3.8-flash-high" (see `agy models`). Plan mode = read-only.
        exe = shutil.which("agy") or os.path.join(os.environ.get("LOCALAPPDATA", ""), "agy", "bin", "agy.exe")
        if not os.path.isfile(exe):
            print("FAIL: agy (Antigravity CLI) not found")
            return 2
        cmd = [exe, "-p", prompt, "--model", args.model[len("agy:"):], "--mode", "plan",
               "--print-timeout", "%ds" % args.timeout]
        args.stall = args.timeout  # agy writes no stderr trace while it works; only the overall timeout applies
    else:
        exe = find_opencode()
        if not exe:
            print("FAIL: opencode not on PATH")
            return 2
        cmd = [exe, "run", "-m", args.model, "--variant", "xhigh", "--agent", "plan", "--dir", ".", prompt]

    out_path = os.path.join(args.dir, args.name + ".md")
    err_path = os.path.join(args.dir, args.name + ".err")
    for attempt in range(1, args.retries + 2):
        print("attempt %d: running (stall %ds, timeout %ds)" % (attempt, args.stall, args.timeout), flush=True)
        killed, seconds = run_once(cmd, out_path, err_path, args.stall, args.timeout, args.poll)
        accepted, problems, rejections = (False, [killed], []) if killed else check_report(out_path, err_path, args.min_bytes)
        if killed:
            _, _, rejections = check_report(out_path, err_path, args.min_bytes)
        for line in rejections:
            print("  rejected tool call: " + line[:200])
        if accepted:
            print("OK: report accepted after %.0fs: %s" % (seconds, out_path))
            return 0
        print("  attempt %d failed after %.0fs: %s" % (attempt, seconds, "; ".join(problems)))
        for path in (out_path, err_path):
            if os.path.exists(path):
                base, ext = os.path.splitext(path)
                shutil.move(path, "%s.attempt%d%s" % (base, attempt, ext))
    print("FAIL: no accepted report after %d attempts (kept as %s.attemptN.*)" % (args.retries + 1, args.name))
    return 1


if __name__ == "__main__":
    sys.exit(main())
