I’m checking the actual queue and resource accounting against the game code; the main risk is whether the new guard measures the same quantity the planet actually drains.
# AI v0.16.1 review

**verdict: SHIP AFTER FIXES** — the science-slot reservation and planet guard both have defects that can undermine the match-17 fixes. The intel stop-loss also needs a reachable stopping condition.

**Confidence:** High on the source-level defects; medium on their competitive impact. The changes were not run in Unity.

## Findings

### High — Science Institutes can consume Jamming slots *before* EMC

**`AI/DirectorPatches.cs:1718–1736`; `docs/ruleset/embedded/enhanced/TechTreeData.csv:10`**

The science exemption only applies `leavesJamming` when `EmcDone` is true. Quantum Computing unlocks the permanent Science Institute **before** EMC in the research plan. Before EMC, `jammingWanted` is false, so Institutes can fill the remaining city slots permanently—the same structural failure the slot policy was intended to prevent. Match 17 had three free slots at T8–T10 and five at T11–T13; whether the scorer actually buys them all is unverified, but the policy permits it.

Ordinary permanents can also consume a wanted Jamming slot before Lossless if the opening cap has not yet been reached: their branch does not check `leavesJamming`.

**Fix:** Reserve viable future Jamming capacity *before* EMC as well as after it, including against exempt science and ordinary permanents. Make the reservation per eligible city: a city already holding or committed to Jamming cannot host another copy. Test the Quantum→EMC transition and a low-city-count position.

### High — Planet guard compares incompatible quantities and misses extraction upgrades

**`AI/DirectorPatches.cs:304–310, 1413–1434, 1532–1545, 1781–1785`; `AI/Brain/Director.cs:158–164, 482–487`; `decompiled/Interplanetary.PlayerDataManagement/PlayerResourceHandler.cs:163–183`**

`SustainableMaterial` is **(remaining planet + banked material) / remaining turns**. It is a spending-capacity estimate, not sustainable *planet drain*. The game drains `max(planet cost, material production)`; mines cost 150 planet material for 100 production, or 185 for 125 offshore. The guard instead compares production plus a **discounted** production gain against the bank-inclusive figure. It can therefore approve an extraction rate that dries the planet before the horizon. The `planetFactor` discount makes the comparison still less representative near exhaustion.

Yield upgrades bypass the guard entirely (`AddUpgradeOptions`), despite increasing mine yield and drain. Conversely, treating a six-turn temporary project as though its gain lasts until T45 can reject a useful short-term purchase, including after combat losses.

**Fix:** Forecast queued next-turn production **and actual planet cost**, including upgrades and project multipliers; compare projected `max(cost, production)` against the remaining **planet pool**, with project duration/expiry accounted for. Treat banked stock separately as spending capacity. Add a bounded, explicit recovery exception for losses.

### High — The 50% intel stop-loss can be unreachable

**`AI/Brain/Tempo.cs:85–103`; `AI/DirectorPatches.cs:701–723, 905–917`; `AI/Plugin.cs:631–660`**

The threshold grows with cumulative energy credits, while the normal purchase opportunities are capped. Using the match-17 budget of approximately **27,000 E**, stopping requires **13,500 E** spent. Even 21 intact arrays plus one Lidar per array cost only **5,250 E** at Enhanced prices. With no losses, two failed batches can therefore become many failed batches without ever reaching the threshold. This defeats the outcome-based stop-loss; native-minimum purchases remain a separate, budgeted exception.

**Fix:** Judge a sufficiently sized *marginal* push against its visibility outcome, with an independently reachable maximum number or cost of failed pushes. Retain the 10% ceiling and bounded probes. Log both the failed-push cost and the stop reason.

### Medium — Reservation can block useful projects without a feasible Jamming purchase

**`AI/DirectorPatches.cs:1721–1728, 1742–1759`; `AI/Brain/Tempo.cs:25–34`**

After EMC, “wanted” checks only research remaining and the global active count. It does not check availability, affordability, or how many distinct cities can actually accept Jamming. With one surviving city already holding Jamming and spare slots, the policy can reserve slots for two impossible additional copies and block every other non-SSTAR temporary for as long as the condition persists. Unaffordability can likewise cause prolonged blocking.

**Fix:** Reserve against feasible distinct-city placements, and release or time-bound reservation when no Jamming can be bought. Do not use enemy information to make that decision.

### Medium — The 10% intel bound is not a whole-match guarantee after reload

**`AI/DirectorPatches.cs:157–174, 220–222, 504–508`; `AI/Brain/Tempo.cs:135–148`**

During an uninterrupted match, the Enhanced array and Lidar paths check and record construction energy against the shared budget. But the previously deferred save/load issue resets the in-memory spend and credit ledger. The resulting post-load budget cannot establish a whole-match 10% limit. Upkeep is separate from this construction budget.

**Fix:** Persist or reconstruct own cumulative spend and credits, or explicitly enforce and report a conservative post-load allowance. This is a pre-existing limitation, not introduced by the larger batch.

## Answers to the remaining checks

- **Research tempo:** The reordered sequence costs **650 science to Kinetic**, **1,050 to Ion**, and **1,700 to Telescopes** after EMC. Match 17 logged 418, 432, 444, 570 and 586 science/turn at T14–T18. That makes earlier Kinetic and Telescopes plausible within the stalled window, depending on carried science and changed project purchases; exact completion turns are not proven. The tradeoff is real: Nano/Floating Plant yield, Microwave, Nuclear Plants and SSTAR move later. Match 17’s strong energy curve depended on the old timing, so it cannot be assumed unchanged.
- **Queue accounting:** `ProjectSlotsFree` is summed across living cities at turn start; `CityFreeSlots` subtracts queued and committed actions, consistent with native `AIProjectConstruction.cs:24–36`. Each successful mod queue decrements the aggregate once. The issue is **aggregate capacity versus eligible per-city capacity**, not an established double decrement.
- **Planet edge cases:** Unlimited planets use the `-1` sentinel and bypass the new guard. At zero planet material, native production stops; refusing new mines is sensible, but the guard cannot restore a depleted pool. On a nearly dry planet or after losses, the distorted horizon test can prevent useful recovery.
- **Fairness:** The new batch sizing and stop condition use own credits, own spend and current visibility outcomes. I found no new hidden-enemy input, targeting leak, resource cheat or crash path in this diff.
- **Migration:** The sequential independent checks correctly carry `0.14 → 0.16 → 0.16.1`; unchanged old research plans migrate directly to the new plan. Custom plans remain untouched. A value of `IntelBatch = 3` is indistinguishable from an intentional user choice and is migrated to 8.

No files were changed. Builds, tests and mutation checks were not run under the read-only constraint.
