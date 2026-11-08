# Terra review of AI v0.19.0 (first slice): disposition

Three rounds: `terra-ai-v0.19.md` (ship with fixes), `terra-ai-v0.19b.md` (do not ship), `terra-ai-v0.19c.md` (ship
with fixes). The answers below cover the third round. All three diffs and prompts are kept beside them.

## Round 1 (growth share in FreeEnergy)

- **#1 major, prior reservation counted as growth:** accepted. Growth share withdrawn.
- **#2 major, growth spends energy needed for this turn's defense and repairs:** accepted. Replaced by late free fire,
  decided from the energy actually left after construction.
- **#3 and #4, aim-watch labels and fields:** HumanDoneAt is recorded only with a playing human; timeoutAtReady is
  logged; waitAfterHuman is renamed aimWaitAfterHuman. The stop-label ambiguity (#3) is accepted as a known limitation:
  it affects logging only.

## Round 2 (late free fire at CoreEndTurnPostfix)

- **#1 blocker, too late (the goal runs inside AIWeaponTargeting.EndTurn):** fixed. The call moved to the start of
  AiPatches.GoalTargetWeaponsPrefix, before the HoldFire check.
- **#2 major, director funding not guaranteed:** fixed. New DirectorState.Funded gate; a pending superweapon purchase's
  cost is kept back.
- **#4 minor, fixture values:** fixed (material 16, 9 primary trajectories; the material case is labelled synthetic).

## Round 3

- **#1 major, the take-back distorts next turn's FreeFireInputs:** fixed. DirectorPatches.NoteLateFire lowers this
  turn's EnergyAtStart by the reservation taken back, so the next turn sees the shape of a planned firing turn.
  - Terra's example: start 15,000, end 7,000, take-back 3,000. lastUses is now 8,000, so 4,000 is free next turn, as
    it would be without the take-back.
  - An abort after a late flip keeps the reservation for defense rolls. This is the existing rule for a planned firing
    turn, and it is deliberately not changed here.
  - Partial volleys stay conservative, as on planned firing turns.
- **#2 minor, Funded set after a failed repair stage:** fixed. RepairAfterReserve returns true when it completes
  (disabled repairs or no candidates count as complete) and false on an exception; Funded takes that value.
- **Savings double subtraction (checks section):** fixed. ReservedEnergy and ReservedMaterial are already outside
  core.Resources, so they are no longer subtracted again. The pending goal cost is still kept back.
- **#3 minor, a fire record is a plan, not a paid shot:** accepted for the playtest. Verify late free fire by joining
  `late-free-fire`, `fire` and `fire-paid` for the same AI and turn, and check for `abort`.

## Not re-reviewed

The round-3 fixes were applied exactly as Terra prescribed and were not sent back for a fourth round.

- 620 unit checks pass.
- Removing the reason, energy or material guard in Strategy.LateFreeFire each fails one check.
- Game lifecycle, actual paid shots and next-turn budgets are **unverified in game.**
