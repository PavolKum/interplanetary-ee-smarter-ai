# Terra v0.16.2 review - disposition

Review: `terra-ai-v0.16.2.md` (GPT-6.1 Terra, read-only, 4 Nov 2026), verdict **SHIP AFTER FIXES**. Diff reviewed:
`terra-ai-v0.16.2.diff` (working tree against 2584fd0). All six findings were fixed. Tests went from 545 to 554 checks;
15 mutants (each new rule broken on purpose) were all killed. Not re-reviewed after the fixes.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| 1 | High | Extraction first (then step 2b, before intel) could take the last power slots and material that anti-nuke and emergency defenses need | **Fixed.** Moved to step 3d, after intel, anti-nuke (3b) and counter-defense (3c), before the defense floor (4). A mine queued by the step must leave `MaterialBoundMinSlots` (4) slots free (`Best` gets `SlotsLeft - 4`); upgrades need no slot. Material stays capped at 35% of the turn's material. |
| 2 | High | The slot-shortage exception did not override the existing energy glut, so a slot-starved AI in glut could still score plants 0 and stall | **Fixed.** `UseGlutWeights(energyGlut, materialBound, slotsLeft, minSlots)` replaces `MaterialBoundGlut`: below 4 free slots, neither the glut nor material-bound applies the glut weights. This also changes v0.16.1's energy-glut behaviour when slots are short (plants were unbuyable). The saturation filter (planet binds) is unchanged. Combined test: glut + 0 slots now buys the plant. |
| 3 | High | Free fire counted energy spent on plants (`PowerEnergySpent`) as unspent, and trusted last turn without checking this turn | **Fixed.** `FreeFireInputs` returns last turn's actual construction surplus (`EndEnergyLeft`) and its use (energy at construction start minus the surplus). `Strategy.FreeEnergy` = min(surplus, this turn's net energy + firing cost - last turn's use), so an income drop or a larger reserve shrinks it. Also `FreeFireMinPassing` 4: no free fire into defenses that stop almost everything. |
| M1 | Medium | The material-bound carry-over ignored the new `Ended` flag | **Fixed.** The material snapshot carries over only from a consecutive turn that started and ended; otherwise `IsMaterialBound` sees no budget and is false. |
| M2 | Medium | Extraction first stopped at the T45 sustainable rate, not the T36 drain-guard horizon the playtest asked for | **Fixed.** `d.ExtractionCeiling` uses `PlanetGuardHorizonTurn` (36). The drain guard inside the option builders is unchanged and still authoritative. Test: 12.5k income is past the T45 rate (11.6k) but under the T36 rate (18.2k). |
| M3 | Medium | A refunded mine was not returned to the extraction-first allowance, and the step's failure marker stayed set | **Fixed.** `ExtractionFirstQueued` (building id -> count); `Abandon` reverses the material and count and clears the marker. The failed building type stays in `FailedThisTurn`. |

## Answers kept as caveats (no code change)
- **Return fire:** it now fires only when the committed target hit us, and it does not retarget to another attacker. That is
  deliberate. Retargeting to any attacker would bring back the AI-vs-AI vendettas (v0.3.1 grudges, match 19's
  kingmaking). With human first, the commit is usually the human who is firing at us.
- **Free fire does not guarantee damage.** Blind Railgun artillery has not measurably hurt the human in matches 18-19.
  The next match tests whether 5 more turns of fire (T21-T25 in match 19) drain his defense energy enough to matter.
  Aimed artillery (impact points near visible buildings, `TrajectoryCalculationResults.LocalPositionHit`) is the v0.17 lead.
- **No hysteresis on the material-bound trigger.** It may flip on borderline turns. The director log now has
  `materialBound`, `extractionFirstMaterial`, `extractionFirstBought` and the strategy log has `freeEnergy`, `firingCost`,
  `humanFirst`, `hitByCommit`, so the next match can measure this.
- **`IsHuman`:** Terra found it within the fairness rules (the player type is public; no hidden value or city count is read).
