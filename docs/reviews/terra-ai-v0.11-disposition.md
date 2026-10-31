# Terra v0.11 review: disposition (AI 0.11.1)

Verdict was **fix first**. Five of six findings fixed in 0.11.1; one deferred.

| # | Finding | Disposition |
|---|---|---|
| 1 High | "Aim first" rests on a false premise: the engine resets every trajectory search each turn | **Fixed.** Aim-first removed; a new target is struck the same turn. Stickiness kept; the firing-time abort gate judges this turn's trajectories. |
| 2 High | Salvo model read base projectile/fragment counts, blind to upgrades | **Fixed.** Live `ShootingModule.ProjectileAmount` x `SurfaceModule.SplitFragments`, base data as fallback. |
| 3 Med | Shield research does not ensure Shields are built (total defense cap) | **Fixed.** Step 3c counter-defense: per known enemy weapon type, min(6, ceil(0.25 x count)) defenses stopping it at >= 0.5, built even at the cap, up to 2 a turn (`DefensePlanner.CounterDeficit`). |
| 4 Med | Placement throttle not applied before picking; released after a capped turn | **Fixed.** `Blocked()` used in every candidate filter; a throttled type stays throttled while its placements still fail. |
| 5 Med | Urgent counter research waits for the whole queued chain; refusals match-wide | **Fixed.** An urgent counter not yet queued replaces the queue once per tech (progress is kept per tech; `SetTechnologyQueue` only rebuilds the list); refusals keyed per AI. |
| 6 Med | Firing history counts list entries, not executed shots | **Deferred.** Only wrong when a whole firing action fails after the lists are built (timeouts, rejected actions); needs a hook in `AITargetWeapons`. Watch `fire` vs `strategy` records. |

Tests: 282 checks; 23 mutations of the v0.10-v0.11 rules all caught. Unverified in game: abort gate, energy glut, counter-defense placement, research preemption.
