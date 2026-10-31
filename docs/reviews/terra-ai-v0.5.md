I’m tracing the new memory through the actual turn lifecycle and checking the defense economics against the game code and both rulesets.
Two material issues have emerged: the proactive defenses can have no energy reserved against a first unseen attack, and the hard-coded counter table does not match Classic’s defenses. I’m checking the multiplayer-memory and placement consequences before ranking them.
## Verdict: **Fix first** — confidence: high

The direction is sound, but the proactive defenses can be unfunded against the first unseen attack. The counter table is also wrong in Classic, and aggregate memory misrepresents multi-enemy games.

## Findings

### High — Blind defense floor does not reserve energy for a blind attack

`DefenseReserve` returns zero when no weapons are known and no damage has been taken, **even after the new defenses exist** (`AI/Brain/Director.cs:201–207`). The director therefore may spend the available energy before the first unseen volley. Defenses require sufficient remaining energy **for each attempt**, including failed rolls (`decompiled/Interplanetary.PlayerDataManagement/PlayerDefenseHandler.cs:215–247, 283–287`).

This is more persistent than the disposition’s “resolves the turn after the first ones finish”: existing defenses alone do not create a reserve.

**Fix:** Give proactive defenses a bounded reserve even without known weapons or damage, including planned defenses when they can be active for the incoming action phase. Test a blind, undamaged AI with existing defenses and one with newly queued defenses.

### High — Classic uses the wrong counter table

The hard-coded Enhanced table (`AI/Brain/DefensePlan.cs:9–17`) is also used for Classic. In Classic, **Kinetic counters both Railgun and Missile at 0.8**, while **Shield counters Railgun and Laser at 1.0, but not Missile** (`docs/ruleset/embedded/classic/DefensesData.csv:10–15`). Consequently, against a visible Missile, the planner can choose Shield, which cannot intercept it; the offense scorer also incorrectly assumes Missiles bypass Kinetic (`AI/DirectorPatches.cs:429, 476`).

**Fix:** Build the counter map from the active ruleset’s defense and building data, and use the same map for defense and offense scoring. Guard missing IDs with `HasBuilding`: Classic has no Ion, and `GetBuilding` logs an error for missing IDs (`decompiled/Interplanetary.GameDataFiles/BuildingsData.cs:23–34`).

### Medium — Aggregate memory loses enemy identity and mixes observations incorrectly

Weapons are summed across enemies **before** remembering (`AI/EconomyPatches.cs:90–103`). If A’s 20 Railguns become hidden while B’s 20 become visible, the aggregate remains 20 rather than remembering A’s 19 plus B’s 20. Conversely, after A is defeated, A’s weapons linger in the aggregate and can drive defense type, target and reserve for many turns. Dead enemies disappear from `EnemyPlanets`, but not from `WeaponMemory` (`decompiled/Interplanetary.AI/AICore.cs:114–123`; `AI/Brain/DefensePlan.cs:88–97`). A 40-count memory still rounds to about 14 after 20 unseen turns.

The same max rule also retains old counts when destruction is actually observable.

**Fix:** Track observations per enemy and weapon type, preferably per observed building ID where useful; discard an eliminated enemy’s memory and reconcile **observable** destruction or fully observable counts. Sum surviving enemies’ estimates afterward. Do not treat merely hidden buildings as destroyed; elimination is publicly logged (`decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:399–412`).

### Medium — Catch-up can crowd out offense and growth on constrained planets

Defenses are attempted repeatedly before counterintel, weapons and economy (`AI/DirectorPatches.cs:285–311`). With a 60-defense deficit, the cap permits **20 in one turn**: in Enhanced, 4,000 energy, at least 4,000 material and 20 occupied power slots, before placement constraints (`docs/ruleset/embedded/enhanced/BuildingsData.csv:13–16, 28`). This can consume all scarce slots or construction resources on a small or material-depleted planet. It is a real design risk, not evidence that a particular match will stall.

**Fix:** Add a per-turn defense spending/slot budget, with an explicit emergency override for incoming damage; test a small planet and a low-material turn. The superweapon purchase itself is correctly attempted first when pending (`AI/DirectorPatches.cs:251–258`), although its slot or upkeep checks can still defer it.

### Medium — A failed defense placement can suppress other viable types

`TryDefense` includes types already marked failed in its candidate list (`AI/DirectorPatches.cs:415–420`). If it selects one, `QueueBuilding` rejects it; `Step` then marks the **whole defense category** failed for the turn (`AI/DirectorPatches.cs:286–289, 429–435, 747–750`). A placement abandonment also decrements `DefensesThisTurn` but leaves `economy.DefensesQueued` inflated (`AI/DirectorPatches.cs:907–940`).

**Fix:** Exclude failed IDs before choosing, decrement the corresponding queued count on abandonment, and try another viable type. Test a failed Kinetic placement with Ion available.

### Low — Configuration migration overrides a deliberate setting

Every startup changes `MaxDefenses=40` to 60, including a user’s deliberate choice after upgrading (`AI/Plugin.cs:239–244`).

**Fix:** Use a one-time versioned migration marker, or leave existing configured values intact.

### Low — Tests establish formulas, not the integrated behavior

The new checks exercise useful pure functions, but do not drive the Harmony hook, match reset, placement, resource reserve, or actual ruleset data (`AI/Tests/Program.cs:178–230`; `AI/Tests/BrainTests.csproj:12–14`). The “remembered Railguns” choice test supplies a fresh dictionary rather than the output of the memory update.

**Fix:** Add behavioral cases for the high/medium findings, per-AI and per-match isolation, repeated turns, visibility loss/reacquisition, and v0.2 director-off behavior. Mutation-check the relevant tests. Tests were **not run**: the requested review is read-only, and a normal `dotnet run` can write build artifacts.

## Checked and fine

- **Lifecycle:** In the normal offline path, `PlayerData.UpdateAI` changes turn status before calling `AICore.BeginTurn`, once per AI turn (`decompiled/Interplanetary.PlayerDataManagement/PlayerData.cs:314–320`). Memory is per `PlayerID` and cleared on initialization/load through `ResetMatchMemory` → `EconomyPatches.Reset` (`AI/Patches.cs:32–39`; `AI/Plugin.cs:372–379`; `AI/EconomyPatches.cs:44, 63–71, 477–482`). Reload intentionally forgets observations.
- **Fairness:** The new observation path counts only intact buildings passing the observer’s `Visible` check; it does not use shadow buildings’ live weapon data (`AI/EconomyPatches.cs:96–103`; `AI/Patches.cs:162–167`). I found no new hidden-information read in this path.
- **Enhanced IDs:** The three building IDs and ordinary-weapon interception probabilities match Enhanced’s data (`docs/ruleset/embedded/enhanced/BuildingsData.csv:2, 8–9`; `DefensesData.csv:10–33`). City defenses are separate project-derived defenses, not these build candidates.
- **Blind default:** Railgun has no research unlock requirement in either tech tree; Missile is research-gated, so Kinetic-first is a reasonable early prior (`docs/ruleset/embedded/{classic,enhanced}/TechTreeData.csv:9`; `decompiled/Interplanetary.PlayerDataManagement/PlayerAvailableBuildings.cs:113–125`). The exact 2:1 tie behavior depends on candidate order.
- **Feedback and upkeep:** Defenses count in `NonCityBuildings` (`AI/DirectorPatches.cs:121–125`): with 100 other buildings the stable floor is about 18, not 15. This is bounded by the 60 cap, not runaway. Defenses have no fixed per-turn energy cost in the supplied rulesets; activation costs and coverage still matter.
- **v0.2 path:** It consumes remembered `EnemyWeapons` consistently through `Choose`; with no counterable known threat it deliberately falls back to native choice, not `ChooseBlind` (`AI/EconomyPatches.cs:359–401`). Its “visible” comments are stale.
