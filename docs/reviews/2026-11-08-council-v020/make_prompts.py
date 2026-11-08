"""Build per-lane prompts = shared brief + lens. Writes laneX.prompt.txt next to this file and prints their sha256."""
import hashlib
import os

HERE = os.path.dirname(os.path.abspath(__file__))
brief = open(os.path.join(HERE, "brief.md"), encoding="utf-8").read()

LENSES = {
    "laneA": ("Lane A (GPT-6.1 Terra) - code and game mechanics",
              "Trace each claimed limit to code. Where does weapon placement choose a position and facing (search AI/ for "
              "placement, shelter, cover-search, PlacementFailures, abandoned)? Why do weapons end up facing away from the "
              "target they later get (aim-search unfoundFacingDeg)? Why do power plants, ion defenses and railguns fail to "
              "place, and is it a real lack of space, a bad search, or a rule (use decompiled/ placement rules)? What caps the "
              "gun count while energy sits idle? What causes the war -> 'peace' flip (StrategyPatches/Strategy/Director war "
              "turn)? For each, propose the smallest code change that fixes it, and say what would falsify it."),
    "laneB": ("Lane B (GLM-5.3 Flash) - measurement",
              "Re-derive the numbers yourself from the jsonl files with read-only python; do not trust the notes. Per match "
              "22-26 and per smart AI: war start turn, weapons count over time, volleys and planned hits at the human, "
              "found/live aim ratios on firing turns, unspent energy and stock material on war turns, placement failures by "
              "building type, and the human's population/buildings per turn. Then answer: which bottleneck best explains the "
              "AI's low damage in matches 22, 24 and 26 versus match 25 - aim, gun count, war timing, target choice, or "
              "energy? Quantify it. Flag every number in the playtest notes you could not reproduce."),
    "laneC": ("Lane C (Gemini 3.8 Flash) - strategy, balance and experiment design",
              "Think as a strong player of this kind of game (economy into artillery, defense rolls cost energy, nukes, "
              "intel/visibility). From the evidence, what would make the AI beat Pavol's standard play, within the fairness "
              "rules? Rank the levers by expected impact. Then design the test plan: which lobbies (1, 2 or 3 Smarter AIs; "
              "original AIs present or not), how many matches per version before a conclusion, what to hold fixed (human "
              "strategy, crust size), and which metrics from the decision log decide success. Be explicit about how weak "
              "one match per configuration is."),
    "laneD": ("Lane D (Fable 5.1) - adversarial roadmap",
              "Attack the facilitator's ranking. Steelman at least three alternatives to 'placement first' (for example: "
              "earlier war/weapon tech, focus fire across Smarter AIs, defense-economy pressure that makes the human spend "
              "energy on rolls, superweapon timing, planet budget/crust, spending the idle energy and materials). Use the "
              "data to decide which lever has the largest measured upside. Propose a v0.20 / v0.21 / v0.22 roadmap and say "
              "what result in the next matches should change it."),
    "laneE": ("Lane E (Sonnet 5.5) - verification, risk and cost",
              "For each candidate change (placement facing, room for more guns, placement failures, war flip, earlier war, "
              "focus fire, idle-resource spending), assess: how it can be unit-tested in AI/Tests (what pure function to "
              "extract), what must be verified in game and how (which decision-log fields), its fairness risk, regression "
              "risk to things that work now (late free fire, aim watch, HUMAN-FIRST, defense reserve), and effort. Review the "
              "process too: what in the current loop (playtest -> note -> Terra review -> mutation tests -> commit) is weak, "
              "e.g. single-match conclusions, the build auto-deploying into the game folder. Recommend a v0.20 scope that "
              "is small enough to verify in 2-3 matches."),
}

for name, (title, lens) in LENSES.items():
    text = brief + "\n\n## Your lens: " + title + "\n\n" + lens + "\n\nYour report goes to the facilitator only. Other lanes " \
        "are working in parallel and you will not see their reports before yours is sealed.\n"
    path = os.path.join(HERE, name + ".prompt.txt")
    open(path, "w", encoding="utf-8", newline="\n").write(text)
    print(name, hashlib.sha256(text.encode("utf-8")).hexdigest()[:16], len(text))
