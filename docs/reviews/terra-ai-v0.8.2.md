I’m tracing the protection calculation through the game’s actual placement and interception code, then checking whether the queue and cap use the same definition of “owned.”
The log supports the runaway-build symptom, but it does not record defense positions, so it cannot by itself prove the geometry caused every zero. I’ve also found two ways the new safety cap can be bypassed and am checking their practical impact.
## Verdict: **Fix first** — confidence: high

The diagnosis is plausible, but the log does **not** prove that geometry alone caused every zero. The new placement model and safety cap have mismatches that can still leave cities undefended or allow excess Ion construction.

### High

**1. Placement discounts cities that the deficit still considers unprotected.**  
`AI/DirectorPatches.cs:1063–1069, 1074–1101`; `AI/Brain/Coverage.cs:156–177, 223–237`

The new placement radius is 4.3, but existing Ions are counted in placement scoring using their **full 5.3 radius**. An Ion 4.8 from a city therefore suppresses that city’s placement score by 80%, while the deficit correctly counts it as unprotected. Multiple such Ions suppress the score further. The cap can then stop construction with a city still uncovered.

**Fix:** use the same *per-city* protection predicate for existing and queued Ions when scoring city targets. Prioritize cities with an actual deficit. Test an Ion at distance 4.8, a nearby uncovered city, and multiple queued Ions.

**2. The advertised total-Ion cap is not a total cap.**  
`AI/DirectorPatches.cs:219–229, 402–418, 561–586`; `AI/EconomyPatches.cs:138–143`

`antiNukeOwned` counts only **powered** normal Ions. Unpowered Ions are excluded from the cap and may later regain power. Separately, ordinary `TryDefense` can queue Ions without consulting the anti-nuke cap or updating `AntiNukeThisTurn`. Thus the new cap limits the **dedicated queue based on powered inventory**, not all owned or queued Ions.

**Fix:** decide whether the cap governs all Ion construction or only the dedicated anti-nuke objective. For a true total cap, count all intact Ions plus both queue paths, separately from the powered coverage count. If conventional missile defense may exceed it, call and log it as a dedicated-budget cap instead.

### Medium

**3. “Protected” remains a heuristic, not nuclear protection.**  
`AI/Brain/Coverage.cs:217–235`; `decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:223–228, 264–280`; `decompiled/Interplanetary.PlayerDataManagement/PlayerBuildingHandler.cs:302–315`; `docs/ruleset/embedded/enhanced/WeaponsData.csv:11`

Interception tests the **impact point**, while a nuclear blast extends 3.7 and can damage a city from roughly 5.4–5.8 units away. An Ion at distance 4.2 passes the new rule but leaves substantial damaging-impact territory outside its 5.3 reach. Two Ions counted at the city do not imply 96% protection against impacts where only one—or neither—can respond.

**Fix:** label the metric partial/heuristic coverage; for meaningful protection, score sampled damaging impact points and overlapping powered defenses, then validate against actual interceptions and city losses.

**4. The fixed placement shrink disagrees with the city-specific threshold.**  
`AI/DirectorPatches.cs:1068–1069, 1215–1217`; `AI/Brain/Coverage.cs:231–234`; `docs/ruleset/embedded/enhanced/CitiesData.csv:13–14`

For city radii 1.7, 1.9 and 2.1, the deficit thresholds are **4.45, 4.35 and 4.25**; placement always scores within **4.3**. A large-city placement at 4.25–4.3 scores as covering it but does not reduce its deficit.

**Fix:** share the city-specific threshold between scoring and deficit calculation.

### Diagnosis and checked and fine

- All **74 director records** report zero protected cities. Junker T24 has four working Ions and queues three; T32 has 21 and queues three (`docs/playtests/data/v081-ai-decisions.jsonl:245,359`). Queueing was **not** three every turn—for example Maverick T29 queues zero (`:322`). Positions are absent, so the root cause is not established from this log.
- Actual radii are city **1.7–2.1 × 1**, Ion building **1.1 × 1**, defense **5.3 × 1** (`CitiesData.csv:13–14`; `BuildingsData.csv:18–19`; `DefensesData.csv:5–6`). The old whole-footprint threshold leaves idealized placement windows of 0.8 for small, 0.4 for medium, and essentially zero for big cities; it was **nearly unreachable, not universally impossible**. The new thresholds are reachable in principle; terrain, collisions, slots and sampling can prevent a placement (`AI/DirectorPatches.cs:1166–1215`).
- City and defense radii are used in the same world-distance comparison as interception (`decompiled/Interplanetary.Prefabs/BaseBuilding.cs:553–568`; `BuildingDefenseStats.cs:16`; `PlayerDefenseHandler.cs:264–275`). Planet radius scales the model, not these radii (`CelestialBody.cs:117–133`). Living-city selection is appropriate (`PlayerBuildingHandler.cs:435–445`).
- `HasPower` is a legitimate alternative contributor; the log does not identify which specific Ions are unpowered (`BuildingPower.cs:71–80`). Threat includes visible or remembered weapons of **all living enemies**, including the other AI (`AI/EconomyPatches.cs:99–115, 133–137`).
- **Unverified in-game:** actual placements and interception outcomes. Tests were not run because this was a read-only review.
