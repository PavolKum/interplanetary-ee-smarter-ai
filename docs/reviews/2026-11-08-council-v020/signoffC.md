# Council Sign-Off: Lane C (Gemini 3.8 Flash)

### Hash Verification
- Target Artifact: `docs/reviews/2026-11-08-council-v020/decision-record.md`
- Target Hash: `sha256:d01bdb5b59637ef3491ac8fd864b6fe9f2efd8d0030615772947ca8946cc8423`
- Verified Command: `sha256sum docs/reviews/2026-11-08-council-v020/decision-record.md`
- Observed Hash: `d01bdb5b59637ef3491ac8fd864b6fe9f2efd8d0030615772947ca8946cc8423` (Exact match).

---

## 1. Findings

### Finding 1: Fidelity to Lane C's Analysis, Cross-Review, and Ballot
- **Evidence:** 
  - Section 2 and Section 3 accurately record Lane C's identity, verified evidence contributions (reproduction of F1 score ratios, war flips, facing angle means, the standardized multi-match testing protocol, and the `HUMAN-FIRST` reach floor), and Lane B's cross-review corrections (that the "saturation path" was demonstrated against a self-handicapped nukes-only defender rather than standard play, and that an unlatched damage trigger requires an upkeep guard to avoid the premature-war trap).
  - Section 4 accurately reflects Lane C's refutation of CF1 (`xreviewC-on-D.md`, `ballotC.md`) and the turn-index sequence ordering.
  - Section 5 and Section 10 accurately capture Lane C's ballot votes: ACCEPT on I1, I2, I10, I11; v0.20 inclusion votes on Split S1 (I3) and Split S2 (I4 Form A); deferral of I5 to v0.21; and the explicit withdrawal of Lane C's initial v0.20 facing-placement ranking in favor of per-gun telemetry in v0.21 (I7).
- **Materiality:** Not material to confirmation (the record faithfully reflects Lane C's inputs and consensus shifts).

### Finding 2: Internal Consistency and Exit Gate Completeness
- **Evidence:** 
  - Across Sections 1, 5, 6, and 7, gates are structured with falsifiable PASS / FAIL / UNTESTED outcomes and explicit denominators. 
  - Split S1 (I3) specifies an explicit fallback state ("Otherwise it is instrumented in v0.20 and tested in v0.21").
  - Section 6 defines explicit do-no-harm stop and revert triggers (`aimWaitAfterHuman`, `HUMAN-FIRST` commit share, `reserveShortfall`, and `coveredBuildings`).
  - Section 7 enforces multi-match evaluation with fixed lobby conditions, eliminating single-match win/loss gates.
- **Materiality:** Not material to confirmation (all gates and next-states are closed and executable).

### Finding 3: Rigorous Handling of Unverified Claims and Blocker Dispositions
- **Evidence:** 
  - Lane D's replacement thesis for CF1 (guided missiles on visible cities without ion coverage) is explicitly quarantined as unverified in Section 4, Section 5 (I13), and Section 9 (Confidence table).
  - Section 10 provides explicit dispositions for all cross-review blockers (A on B, B on C, C on D, D on E, E on A) and accurately preserves all lane dissents (D on I5, A on I3, A & E on I4).
- **Materiality:** Not material to confirmation (unverified claims are properly isolated; all blockers have resolved dispositions).

### Finding 4: Adherence to Read-Only and Authority Boundaries
- **Evidence:** 
  - Section 2 confirms read-only constraints held across all lanes (untracked status digest `a2f2188c` unchanged, no tracked files modified).
  - Section 1 and Section 8 clearly distinguish technical consensus from policy choices, reserving alliance-like behavior (Form B) and release scope splits (S1, S2, protocol commitment, or no-action baseline) strictly for Pavol.
- **Materiality:** Not material to confirmation (authority limits strictly respected).

---

## 2. Positions on Open Details

### (a) I2 Latch Scope: Strategy-Only vs. Shared State with Upkeep Guard
- **Position:** Prefer **shared state with the upkeep guard** (A/B proposal adopted by facilitator in Section 5, lines 148–149).
- **Rationale:** A strategy-only latch leaves `director.War` unasserted during damage-triggered wars, which suppresses `TryWeapon` (`DirectorPatches.cs:875, 902`) and halts weapon replenishment. Conversely, latching director war without an upkeep guard prematurely raises `RetaliationUpkeepShare` from 0.15 to 0.40, draining economic growth before T20. Latching a shared match-scoped state from T12 while holding upkeep and defense postures to retaliation levels allows weapon replacement without triggering the premature-war trap.

### (b) Facilitator's Proposed S1 Resolution (Late Pulse-Wait Volleys Do Not Update `LastStrikeTurn`)
- **Position:** Prefer **adoption in v0.20 with the cadence-preserving rule**.
- **Rationale:** Lane A's technical objection to shipping I3 in v0.20 was that `LateFreeFire` advances `LastStrikeTurn = turn`, which postpones or dilutes the subsequent synchronized pulse (`turn - lastStrikeTurn >= StrikeEvery`, `Strategy.cs:395`). Ensuring that late pulse-wait volleys do not mutate `LastStrikeTurn` eliminates pulse cadence interference while capturing verified unspent energy (e.g., Donnels T33 8.8k, Zenon T33 6.5k) that is otherwise permanently discarded at round refresh.

---

CONFIRM decision-record.md @ sha256:d01bdb5b59637ef3491ac8fd864b6fe9f2efd8d0030615772947ca8946cc8423

verdict: CONFIRM

END OF REPORT
