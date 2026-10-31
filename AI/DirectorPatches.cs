using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Actions;
using Interplanetary.AI.Construction;
using Interplanetary.GameDataFiles;
using Interplanetary.GameEngine;
using Interplanetary.GameSession;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;
using InterplanetaryAI.Brain;
using TJR_Engine.FileManagement;
using UnityEngine;

namespace InterplanetaryAI
{
    // v0.3 economy director: replaces the native construction choice (AIConstruction.tryConstructNewBuilding).
    // Native bugs it routes around (31 Oct 2026 playtest, decompiled source):
    //  1. A non-power building with no free power slot only raises PowerSlotNeed and reports success; the need
    //     branch then builds power plants on every call and never lowers the need (AIConstructionModule.
    //     tryBuildAffordableBuilding, AIConstruction.tryConstructNewBuilding). Result: Solar Plant spam, 1-3 mines.
    //  2. Water-only buildings are placed next to a random land building (AIBuildingPlacementSearch), so they
    //     almost never fit; the search never gives up. Result: 0 Floating Plants in 40 turns with the tech at T9.
    //  3. A queued building's pre-installed upgrades are dropped at placement (AIConstructBuildingAction passes
    //     {null, null}) though their cost was budgeted. The director buys upgrades separately.
    // Every queued item is logged; the game executes the queue as native does.
    internal static class DirectorPatches
    {
        internal sealed class DirectorState
        {
            // this turn
            public int Turn = -1;
            public bool Started;
            public bool War;
            public bool Defensive;        // v0.10: defense floor and reserve from DefenseFromTurn (before weapons)
            public int SlotsAtStart;
            public int SlotsLeft;
            public int Unpowered;
            public int PlantsForUnpowered;
            public int PreferredIntelAttempts;
            public int DefensesThisTurn;
            public int WeaponsThisTurn;
            public int WeaponEnergySpent;
            public int TurnStartEnergy;
            public int TurnStartMaterial;
            public float EnergyIncome;
            public float MaterialIncome;
            public float Upkeep;
            public int OwnDefenses;
            public int DesiredDefenses;
            public int DefenseTurnCap;
            public float ObservedDefenseNeed;   // v0.6: energy our defenses needed in the last action phase (x margin)
            public int LastImpacts, LastIntercepted, LastNoEligible, LastUnpaid, LastEnergySpent, LastNearCity, LastNoDefenseNearby;
            public float CoverageShare;
            public float ScienceWeight = 1.5f; // v0.9: raised while the research plan is behind schedule
            public float RemainingPlanScience;
            public int WeaponUpgradesThisTurn, WeaponUpgradeMaterial;
            public readonly Dictionary<BaseBuilding, List<UpgradeEffect>> PlannedWeaponEffects = new Dictionary<BaseBuilding, List<UpgradeEffect>>();
            public bool NukeThreat;          // v0.8: an enemy Nuclear Missile seen/remembered or recently landed
            public int AntiNukeWanted;       // v0.8.1: defenses still missing over the cities (per-city deficit)
            public int AntiNukeOwned;        // working (normal, powered) anti-nuke defenses
            public int AntiNukeThisTurn;
            public int AntiNukeIntact, AntiNukeCap;
            public int CitiesProtected;
            public string AntiNukeId;        // the defense that stops Nuclear Missiles best (Ion in Enhanced)
            public int CitiesCovered, CitiesTotal;
            public Dictionary<string, float> ObservedPass = new Dictionary<string, float>();
            public string TargetName = "";
            public readonly Dictionary<string, int> TargetDefenses = new Dictionary<string, int>();
            public readonly Dictionary<BaseBuilding, int> PlannedUpgrades = new Dictionary<BaseBuilding, int>();
            public readonly HashSet<string> PlannedUpgradeKeys = new HashSet<string>();
            public readonly HashSet<string> FailedThisTurn = new HashSet<string>();
            public readonly Dictionary<string, int> Actions = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PlacementFailures = new Dictionary<string, int>();
            public int Refunds;
            public int Steps;
            public bool Waiting;          // nothing left to buy, queued placements still resolving
            public bool Reopened;         // a placement was abandoned and refunded: decide again
            public float RecentDamage;
            public int OwnCounterintel;
            public int CounterintelThisTurn;
            public int NonCityBuildings;
            public readonly Dictionary<BaseBuilding, float[]> PlannedMultipliers = new Dictionary<BaseBuilding, float[]>();
            public int EnemiesAlive;
            public int VisibleEnemyWeapons;
            public int BlindEnemies;       // enemies whose production or population our intel cannot read
            public int IntelBuildings;
            public int IntelThisTurn;
            public int DefenseReserve;
            public int ReleasedEnergy;    // v0.10: firing reservation given to construction on a hold-fire turn
            public bool HoldingFire;
            public bool MaterialGlut;
            public bool EnergyGlut;
            public int EndEnergyLeft;     // energy unspent when the turn's construction ended
            public int LastEnergyLeft, LastEnergyBudget;
            public readonly Dictionary<string, int> AbandonedLastTurn = new Dictionary<string, int>();
            public readonly Dictionary<string, int> QueuedById = new Dictionary<string, int>();
            // across turns
            public int WaterCooldownUntil;
        }

        private static readonly Dictionary<int, DirectorState> s_states = new Dictionary<int, DirectorState>();
        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();

        private static readonly FieldInfo s_freeSlots = AccessTools.Field(typeof(AIPowerGridMaintaining), "m_freePowerSlotsByBuilding");
        private static readonly FieldInfo s_queueList = AccessTools.Field(typeof(AIConstructionQueue), "m_constructionQueue");
        private static readonly FieldInfo s_queueFinder = AccessTools.Field(typeof(AIConstructionQueue), "m_buildingPlacementFinder");
        private static readonly FieldInfo s_finderSearches = AccessTools.Field(typeof(AIBuildingPlacementFinder), "m_ongoingSearches");
        private static readonly FieldInfo s_searchCore = AccessTools.Field(typeof(AIBuildingPlacementSearch), "m_core");
        private static readonly FieldInfo s_searchBuilding = AccessTools.Field(typeof(AIBuildingPlacementSearch), "m_buildingData");
        private static readonly FieldInfo s_searchSteps = AccessTools.Field(typeof(AIBuildingPlacementSearch), "m_steps");
        private static readonly MethodInfo s_placeRandom = AccessTools.Method(typeof(AIBuildingPlacementSearch), "tryPlaceToRandomPosition");
        private static readonly MethodInfo s_placeNextTo = AccessTools.Method(typeof(AIBuildingPlacementSearch), "tryPlaceBuildingNextToExistingBuilding");
        private static readonly FieldInfo s_materialModule = AccessTools.Field(typeof(AIConstruction), "m_materialConstruction");
        private static readonly MethodInfo s_tryProject = AccessTools.Method(typeof(AIConstructionModule), "tryBuildAffordableProject");

        private static readonly string[] s_economyBuildings = { "bui_SolarPlant", "bui_FloatingPlant", "bui_NuclearPlant", "bui_Mine", "bui_OffshoreMine" };

        internal static void Reset()
        {
            s_states.Clear();
            s_coverage.Clear();
            LoadDefenseCounters();
        }

        // Which defense stops which weapon, from the active ruleset (Classic: Kinetic also stops Missiles, no Ion).
        private static void LoadDefenseCounters()
        {
            try
            {
                BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
                DefensesData defenses = GameDataFileManager.GetDataFile<DefensesData>();
                if (buildings == null || defenses == null) return;
                Dictionary<string, Dictionary<string, float>> counters = new Dictionary<string, Dictionary<string, float>>();
                foreach (string id in buildings.GetBuildingIds())
                {
                    BuildingData data = buildings.GetBuilding(id);
                    if (data == null || id.StartsWith("cit_") || !data.defense_functionality.IsValueSet) continue;
                    DefenseData defense = defenses.GetDefense(data.defense_functionality.Value);
                    if (defense == null || !defense.defends_against.IsValueSet || !defense.defense_probabilities.IsValueSet) continue;
                    List<string> weapons = defense.defends_against.Value;
                    List<float> chances = defense.defense_probabilities.Value;
                    Dictionary<string, float> stops = new Dictionary<string, float>();
                    for (int i = 0; i < weapons.Count && i < chances.Count; i++) stops[weapons[i]] = chances[i];
                    if (stops.Count > 0) counters[id] = stops;
                }
                DefensePlanner.SetCounters(counters);
                List<string> summary = new List<string>();
                foreach (KeyValuePair<string, Dictionary<string, float>> pair in DefensePlanner.Counters)
                {
                    List<string> stops = new List<string>();
                    foreach (KeyValuePair<string, float> stop in pair.Value) stops.Add(stop.Key + " " + stop.Value);
                    summary.Add(pair.Key + ": " + string.Join(", ", stops.ToArray()));
                }
                Plugin.Log.LogInfo("Defense counters: " + string.Join("; ", summary.ToArray()));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Defense counters not loaded from the ruleset; the Enhanced table is used: " + e.Message);
            }
        }

        private static DirectorState StateOf(PlayerData ai)
        {
            DirectorState state;
            if (!s_states.TryGetValue(ai.PlayerID, out state))
            {
                state = new DirectorState();
                s_states[ai.PlayerID] = state;
            }
            return state;
        }

        // ---- turn start (from EconomyPatches.CoreBeginTurnPostfix) ----------------------------------

        internal static void BeginTurn(AICore core, EconomyPatches.AiEconomyState economy, float recentDamage, int turn)
        {
            DirectorState d = StateOf(core.PlayerData);
            int waterCooldown = d.WaterCooldownUntil;
            // v0.11 carried from the turn just before: unspent energy (energy glut) and abandoned placements (a full planet).
            bool consecutive = d.Turn == turn - 1;
            int lastLeft = consecutive ? d.EndEnergyLeft : 0, lastBudget = consecutive ? d.TurnStartEnergy : 0;
            Dictionary<string, int> lastAbandons = consecutive ? new Dictionary<string, int>(d.PlacementFailures) : new Dictionary<string, int>();
            d = new DirectorState { WaterCooldownUntil = waterCooldown, Turn = turn, LastEnergyLeft = lastLeft, LastEnergyBudget = lastBudget };
            foreach (KeyValuePair<string, int> pair in lastAbandons) d.AbandonedLastTurn[pair.Key] = pair.Value;
            s_states[core.PlayerData.PlayerID] = d;

            PlayerData me = core.PlayerData;
            d.RecentDamage = recentDamage;
            foreach (BaseBuilding building in me.BuildingHandler.Buildings)
            {
                if (building == null || building.IsCity || building.BuildingData == null || building.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                d.NonCityBuildings++;
                if (Value(building.BuildingData.turn_yield_counterintel) > 0) d.OwnCounterintel++;
                if (Value(building.BuildingData.turn_yield_intel) > 0) d.IntelBuildings++;
            }
            d.War = EconomyDirector.WarPhase(turn, Plugin.WarFromTurn.Value, recentDamage, Plugin.WarDamageTrigger.Value);
            d.EnergyGlut = Plugin.EnergyGlutShare.Value > 0f
                && EconomyDirector.IsEnergyGlut(d.LastEnergyLeft, d.LastEnergyBudget, Plugin.EnergyGlutShare.Value, Plugin.EnergyGlutMin.Value);
            d.Defensive = d.War || EconomyDirector.WarPhase(turn, Plugin.DefenseFromTurn.Value, recentDamage, Plugin.WarDamageTrigger.Value);
            int visibleEnemyWeapons = 0;
            foreach (int n in economy.EnemyWeapons.Values) visibleEnemyWeapons += n;
            d.VisibleEnemyWeapons = visibleEnemyWeapons;
            d.EnemiesAlive = core.EnemyPlanets.Count;
            foreach (int n in economy.OwnDefenses.Values) d.OwnDefenses += n;
            DefenderStats last = Plugin.Ledger.LastDefender(me.PlayerID);
            d.LastImpacts = last.Impacts;
            d.LastIntercepted = last.Intercepted;
            d.LastNoEligible = last.NoEligibleDefense;
            d.LastUnpaid = last.NoResource;
            d.LastNearCity = last.NearCity;
            d.LastNoDefenseNearby = last.NoDefenseNearby;
            try { CoverageShares(core, out d.CoverageShare, out d.CitiesCovered, out d.CitiesTotal); }
            catch (Exception e) { Plugin.Log.LogError("Coverage log failed: " + e.Message); }
            d.LastEnergySpent = last.EnergySpent;
            d.ObservedDefenseNeed = EconomyDirector.ObservedDefenseNeed(last.EnergySpent, last.NoResource, last.Impacts - last.NoEligibleDefense, 100f, Plugin.DefenseObservedMargin.Value);
            d.DesiredDefenses = EconomyDirector.DesiredDefenses(visibleEnemyWeapons, recentDamage, Plugin.DefensePerEnemyWeapon.Value, Plugin.DamagePerDefense.Value, Plugin.MaxDefenses.Value,
                d.Defensive, d.NonCityBuildings, Plugin.DefenseFloorShare.Value);
            // v0.9 science: value it by how far the research plan is behind schedule.
            try
            {
                PlayerTechnologyHandler tech = me.TechnologyHandler;
                float remaining = 0f;
                foreach (string id in Plugin.ResearchPlanEnabled.Value ? Plugin.ResearchPlanIds : new List<string>())
                {
                    GameData_TechData t = tech.GetTechnologyById(id);
                    if (t == null || !t.science_cost.IsValueSet || tech.GetTechnologyCompletionById(id)) continue;
                    remaining += Math.Max(0, t.science_cost.Value - tech.GetTechnologyProgressById(id));
                }
                d.RemainingPlanScience = remaining;
                d.ScienceWeight = ScienceValue.Weight(Plugin.ScienceWeight.Value, remaining, me.ResourceHandler.ScienceProductionPerTurn, turn,
                    Plugin.ScienceTargetTurn.Value, Plugin.ScienceMaxMultiplier.Value);
            }
            catch (Exception e)
            {
                d.ScienceWeight = Plugin.ScienceWeight.Value;
                ReportOnce("science-weight", e);
            }

            // v0.8 anti-nuke: a Nuclear Missile seen (visible or remembered) or one that landed on us recently.
            int nukeHits;
            if (last.ImpactsByWeapon.TryGetValue(NuclearWeaponId, out nukeHits) && nukeHits > 0) economy.LastNukeHitTurn = turn;
            int knownNukes;
            economy.EnemyWeapons.TryGetValue(NuclearWeaponId, out knownNukes);
            d.NukeThreat = knownNukes > 0 || turn - economy.LastNukeHitTurn <= Plugin.NukeMemoryTurns.Value;
            d.AntiNukeId = AntiNukeDefenseId();
            if (d.AntiNukeId != null && Plugin.AntiNukePerCity.Value > 0)
            {
                // Per city: working anti-nuke defenses whose radius holds the whole city (Terra v0.8 #2, #3). Built on
                // their own gate, independent of the total-defense target (Terra v0.8 #1).
                int perCity = EconomyDirector.AntiNukePerCity(d.NukeThreat, turn, Plugin.AntiNukePriorTurn.Value, Plugin.AntiNukePerCity.Value, Plugin.AntiNukePriorPerCity.Value);
                List<Point> cityPoints = new List<Point>(), defensePoints = new List<Point>();
                List<float> cityRadii = new List<float>(), defenseRadii = new List<float>();
                foreach (BaseBuilding city in me.BuildingHandler.GetRemainingCities())
                {
                    Vector3 p = city.transform.position;
                    cityPoints.Add(new Point(p.x, p.y, p.z));
                    cityRadii.Add(city.Radius);
                }
                int intactAntiNuke = 0;
                foreach (BaseBuilding b in me.BuildingHandler.Buildings)
                {
                    if (b == null || b.IsCity || b.BuildingData == null || b.BuildingData.id.Value != d.AntiNukeId) continue;
                    if (b.CurrentState != BaseBuilding.BuildingState.Ruins && b.CurrentState != BaseBuilding.BuildingState.Destroyed) intactAntiNuke++;
                    if (b.CurrentState != BaseBuilding.BuildingState.Normal || !b.Power.HasPower) continue;
                    Vector3 p = b.transform.position;
                    defensePoints.Add(new Point(p.x, p.y, p.z));
                    defenseRadii.Add(b.Stats.Defenses.Radius);
                }
                d.AntiNukeOwned = defensePoints.Count;
                int deficit = DefenseCoverage.CityProtectionDeficit(cityPoints, cityRadii, defensePoints, defenseRadii, perCity, out d.CitiesProtected);
                // Cap on every intact Ion, powered or not (Terra v0.8.2 #2); this turn's queue is checked in the step.
                d.AntiNukeIntact = intactAntiNuke;
                d.AntiNukeWanted = EconomyDirector.AntiNukeMissing(deficit, intactAntiNuke, cityPoints.Count, perCity, 2f);
                d.AntiNukeCap = (int)Math.Ceiling(2f * perCity * cityPoints.Count);
            }

            // Weapon choice is made against the strongest visible economy: the enemy most worth hitting.
            float bestEconomy = -1f;
            PlayerData target = null;
            foreach (CelestialBody planet in core.EnemyPlanets)
            {
                EnemyView view = AiPatches.BuildEnemyView(me, planet.Player, turn);
                if (!view.EconomyKnown || view.VisibleCities == 0) d.BlindEnemies++;
                if (view.Economy > bestEconomy)
                {
                    bestEconomy = view.Economy;
                    target = planet.Player;
                }
            }
            if (target != null)
            {
                d.TargetName = target.UserNickName;
                // The defenses we saw (remembered while counterintel hides them), not only what is visible now.
                Dictionary<string, int> known;
                if (economy.KnownDefenses.TryGetValue(target.PlayerID, out known))
                {
                    foreach (KeyValuePair<string, int> pair in known) d.TargetDefenses[pair.Key] = pair.Value;
                }
                // Our own shots at THIS target (shots at other opponents say nothing about its defenses), blended with the
                // estimate from the defenses we remember so a handful of shots do not decide.
                if (Plugin.ObservedMinImpacts.Value > 0)
                {
                    foreach (KeyValuePair<string, int[]> seen in Plugin.Ledger.ObservedAll(me.PlayerID, target.PlayerID, Plugin.ObservedMinImpacts.Value))
                    {
                        float prior = EconomyDirector.PassChance(seen.Key, d.TargetDefenses, DefensePlanner.Counters, Plugin.DefensesForFullCover.Value);
                        d.ObservedPass[seen.Key] = EconomyDirector.BlendedPass(seen.Value[0], seen.Value[1], prior, Plugin.ObservedPriorWeight.Value);
                    }
                }
            }
        }

        // ---- construction (from EconomyPatches.TryConstructPrefix) --------------------------------

        // Returns the Harmony prefix result: false = native skipped. One queued item per call, like native.
        internal static bool TryConstruct(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, ref bool result)
        {
            try
            {
                DirectorState d = StateOf(core.PlayerData);
                if (d.Turn != GameManager.CurrentTurn) BeginTurn(core, economy, 0f, GameManager.CurrentTurn);
                if (!d.Started) StartConstruction(core, d, economy);
                if (d.Waiting)
                {
                    // Construction stays open while queued placements resolve, so the cost of an abandoned
                    // placement (refunded by FinderUpdatePrefix) is spent this turn instead of lost.
                    if (!d.Reopened)
                    {
                        result = !construction.ConstructionQueue.IsReady;
                        return false;
                    }
                    d.Waiting = false;
                    d.Reopened = false;
                }
                d.Steps++;
                if (d.Steps > 2000)
                {
                    // Safety valve: every step queues something, so this many means a loop; end the turn.
                    result = false;
                    return false;
                }
                if (Step(construction, core, economy, d))
                {
                    result = true;
                    return false;
                }
                d.Waiting = !construction.ConstructionQueue.IsReady;
                result = d.Waiting;
                return false;
            }
            catch (Exception e)
            {
                ReportOnce("director", e);
                // End construction rather than fall back to native, whose need branch can loop on power plants.
                result = false;
                return false;
            }
        }

        private static void StartConstruction(AICore core, DirectorState d, EconomyPatches.AiEconomyState economy)
        {
            d.Started = true;
            d.SlotsAtStart = CountFreeSlots(core);
            d.SlotsLeft = d.SlotsAtStart;
            d.DefenseTurnCap = EconomyDirector.DefensesPerTurnCap(d.DesiredDefenses, d.OwnDefenses, Plugin.DefensesPerTurn.Value,
                Plugin.DefenseCatchUpTurns.Value, d.SlotsAtStart, d.RecentDamage > 0f);
            d.Unpowered = core.PowerGrid.BuildingsRequiringPowerAmount;
            d.TurnStartEnergy = core.Resources.Energy;
            d.TurnStartMaterial = core.Resources.Material;
            PlayerResourceHandler res = core.PlayerData.ResourceHandler;
            d.EnergyIncome = res.EnergyProductionPerTurn;
            d.MaterialIncome = res.PlanetMaterial > 0 ? res.MaterialProductionPerTurn : 0;
            d.Upkeep = core.Resources.EnergyUpkeep;
            // v0.10 hold fire: StrategyPatches already gave the reserved firing cost back (Resources include it here).
            d.HoldingFire = StrategyPatches.HoldFire(core.PlayerData.PlayerID);
            d.ReleasedEnergy = StrategyPatches.ReleasedEnergy(core.PlayerData.PlayerID);
            // v0.4: at war, keep energy back for our defense rolls (each costs the defender 100-200 energy from
            // what is left that turn; PlayerDefenseHandler.IsHitIntercepted). v0.3 spent it all.
            // v0.10 (Terra #3): defensive readiness keeps its own, earlier gate (DefenseFromTurn) now that weapons wait.
            if (d.Defensive)
            {
                int reserve = EconomyDirector.DefenseReserve(d.OwnDefenses, d.VisibleEnemyWeapons, d.EnemiesAlive, d.RecentDamage,
                    150f, 125f, Plugin.DefenseReserveShare.Value, d.EnergyIncome, 1f, d.ObservedDefenseNeed, Plugin.DefenseHardCapShare.Value);
                reserve = Math.Max(0, Math.Min(reserve, core.Resources.Energy));
                // Under a nuke threat every working anti-nuke defense gets one 200-energy roll (Terra v0.8 #5).
                if (d.NukeThreat) reserve = Math.Max(reserve, (int)Math.Min(d.AntiNukeOwned * 200f, 0.4f * d.EnergyIncome));
                reserve = Math.Max(0, Math.Min(reserve, core.Resources.Energy));
                reserve = EconomyDirector.CapReserveForRecovery(reserve, core.Resources.Energy, d.EnergyIncome, Plugin.DefenseRecoveryShare.Value);
                if (economy.BuyPending && !economy.Bought) reserve = Math.Min(reserve, Math.Max(0, core.Resources.Energy - economy.GoalEnergyCost));
                core.Resources.Energy -= reserve;
                core.Construction.ConstructionQueue.AllottedEnergy -= reserve;
                d.DefenseReserve = reserve;
                d.TurnStartEnergy = core.Resources.Energy;
            }
        }

        // Free power slots on buildings that distribute power, after this turn's power-line connections.
        private static int CountFreeSlots(AICore core)
        {
            Dictionary<BaseBuilding, int> free = s_freeSlots.GetValue(core.PowerGrid) as Dictionary<BaseBuilding, int>;
            int slots = 0;
            foreach (BaseBuilding building in core.PlayerData.BuildingHandler.Buildings)
            {
                if (building == null || !building.Power.IsPotentiallyDistributingPower) continue;
                int n;
                if (free != null && free.TryGetValue(building, out n)) slots += Math.Max(0, n);
            }
            return slots;
        }

        private static string ObservedPassText(Dictionary<string, float> pass)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, float> pair in pass) parts.Add(pair.Key + "=" + pair.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(";", parts.ToArray());
        }

        private static bool Step(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, DirectorState d)
        {
            // 1. Superweapon purchase prepared by the savings logic.
            if (economy.BuyPending && !economy.Bought && economy.Goal != null && !d.FailedThisTurn.Contains("superweapon"))
            {
                if (TryBuySuperweapon(construction, core, economy, d)) return true;
                d.FailedThisTurn.Add("superweapon");
            }

            // 2. Power for unpowered buildings (one plant each, at most 2 a turn) or for the superweapon's slot.
            bool needPlant = (d.Unpowered > 0 && d.PlantsForUnpowered < Math.Min(d.Unpowered, Plugin.UnpoweredPlantsPerTurn.Value))
                || (economy.NeedSlotForGoal && d.PlantsForUnpowered == 0);
            if (needPlant && !d.FailedThisTurn.Contains("unpowered-plant"))
            {
                BuildingData plant = PickLandPlant(core, d) ?? PickWaterPlant(core, d);
                if (plant != null && QueueBuilding(construction, core, d, plant, "power"))
                {
                    d.PlantsForUnpowered++;
                    return true;
                }
                d.FailedThisTurn.Add("unpowered-plant");
            }

            // 3. Intel until we can read every enemy's production and population (v0.4). Native intel need
            // counts only guided weapons, so a Railgun army stayed blind all game (31 Oct v0.3.1).
            IntelEvaluationState intel = IntelState(core);
            bool intelRoom = d.IntelBuildings + d.IntelThisTurn < Plugin.MaxIntelBuildings.Value && d.IntelThisTurn < Plugin.IntelPerTurn.Value;
            bool wantIntel = intel == IntelEvaluationState.BelowMinimum || d.BlindEnemies > 0;
            if (wantIntel && intelRoom && d.SlotsLeft > 0 && !d.FailedThisTurn.Contains("intel"))
            {
                if (TryIntelBuilding(construction, core, d)) return true;
                d.FailedThisTurn.Add("intel");
            }

            // 3b. Anti-nuke defenses over the cities (v0.8), on their own gate: nukes take cities, and cities decide the game.
            int ionsQueuedThisTurn = 0;
            if (d.AntiNukeId != null) economy.DefensesQueued.TryGetValue(d.AntiNukeId, out ionsQueuedThisTurn);
            if (d.AntiNukeId != null && d.AntiNukeThisTurn < d.AntiNukeWanted && d.AntiNukeThisTurn < Math.Max(2, d.DefenseTurnCap)
                && d.AntiNukeIntact + ionsQueuedThisTurn < d.AntiNukeCap
                && d.SlotsLeft > 0 && !d.FailedThisTurn.Contains("antinuke"))
            {
                BuildingData antiNuke = GameDataFileManager.GetDataFile<BuildingsData>().GetBuilding(d.AntiNukeId);
                if (antiNuke != null && core.PlayerData.BuildingHandler.AvailableBuildings.IsBuildingAvailable(antiNuke)
                    && QueueBuilding(construction, core, d, antiNuke, "antinuke"))
                {
                    Increment(economy.DefensesQueued, d.AntiNukeId);
                    d.AntiNukeThisTurn++;
                    return true;
                }
                d.FailedThisTurn.Add("antinuke");
            }

            // 4. Defenses: known threats, damage taken, and at war a floor scaled to what we own (v0.5).
            if (d.OwnDefenses + d.DefensesThisTurn < d.DesiredDefenses && d.DefensesThisTurn < d.DefenseTurnCap && d.SlotsLeft > 0 && !d.FailedThisTurn.Contains("defense"))
            {
                if (TryDefense(construction, core, economy, d)) return true;
                d.FailedThisTurn.Add("defense");
            }

            // 4b. Counterintel: hides our buildings from enemy intel (the human who won built 18 Data Security Hubs).
            if (Plugin.CounterintelPerBuildings.Value > 0 && d.Turn >= Plugin.CounterintelFromTurn.Value && d.CounterintelThisTurn < 1 && d.SlotsLeft > 0
                && d.OwnCounterintel + d.CounterintelThisTurn < d.NonCityBuildings / Plugin.CounterintelPerBuildings.Value && !d.FailedThisTurn.Contains("counterintel"))
            {
                if (TryYieldBuilding(construction, core, d, "counterintel")) return true;
                d.FailedThisTurn.Add("counterintel");
            }

            // 5. Weapons, up to this turn's spending share.
            // Before WarFromTurn, war means retaliation for damage taken: a smaller weapon upkeep.
            float upkeepCap = (d.Turn >= Plugin.WarFromTurn.Value ? Plugin.WarUpkeepShare.Value : Plugin.RetaliationUpkeepShare.Value) * d.EnergyIncome;
            bool underSpendShare = d.WeaponEnergySpent < Plugin.WarSpendShare.Value * d.TurnStartEnergy;
            if (d.War && underSpendShare && !d.FailedThisTurn.Contains("weapon"))
            {
                if (TryWeapon(construction, core, d, upkeepCap)) return true;
                d.FailedThisTurn.Add("weapon");
            }

            // 5b. Weapon upgrades (v0.9): damage per firing energy, population damage weighted. Cheap (50 energy +
            // 150-400 material), permanent, and the AI is limited by firing energy at war.
            if (d.War && Plugin.WeaponUpgradesEnabled.Value && d.WeaponUpgradesThisTurn < Plugin.WeaponUpgradesPerTurn.Value
                && d.WeaponUpgradeMaterial < Plugin.WeaponUpgradeShare.Value * d.TurnStartMaterial && !d.FailedThisTurn.Contains("weapon-upgrade"))
            {
                if (TryWeaponUpgrade(construction, core, d, upkeepCap)) return true;
                d.FailedThisTurn.Add("weapon-upgrade");
            }

            // 6. Economy: the best-value upgrade, project or building.
            if (TryEconomy(construction, core, economy, d)) return true;

            // 7. Preferred intel once at war.
            if (d.War && intel == IntelEvaluationState.BelowPreferred && d.PreferredIntelAttempts < 1 && d.SlotsLeft > 0
                && d.IntelBuildings + d.IntelThisTurn < Plugin.MaxIntelBuildings.Value && d.IntelThisTurn < Plugin.IntelPerTurn.Value)
            {
                d.PreferredIntelAttempts++;
                if (TryIntelBuilding(construction, core, d)) return true;
            }

            // 8. Energy left over at war: more weapons (energy does not carry to the next turn).
            if (d.War && !d.FailedThisTurn.Contains("weapon-sink"))
            {
                if (TryWeapon(construction, core, d, upkeepCap)) return true;
                d.FailedThisTurn.Add("weapon-sink");
            }
            return false;
        }

        // ---- superweapon -------------------------------------------------------------------------

        private static bool TryBuySuperweapon(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, DirectorState d)
        {
            if (!construction.IsEntityAvailable(economy.Goal)) return false;
            if (core.Resources.Material < economy.GoalMaterialCost) return false;
            if (core.Resources.Energy < economy.GoalEnergyCost)
            {
                // Material is there, energy is not: hold the material for next turn and ask for power.
                economy.EnergyPush = true;
                core.Resources.Material -= economy.GoalMaterialCost;
                core.Construction.ConstructionQueue.AllottedMaterial -= economy.GoalMaterialCost;
                return false;
            }
            bool needsSlot = !(economy.Goal.Building.distribute_power.IsValueSet && economy.Goal.Building.distribute_power.Value);
            if (needsSlot && d.SlotsLeft <= 0)
            {
                economy.NeedSlotForGoal = true; // build a plant; buy next turn
                return false;
            }
            // Firing it costs 700-800 energy every turn (reserved by AIResources). Keep usable energy positive.
            int firing = FiringCost(economy.Goal.Building);
            if (d.Upkeep + firing > Plugin.SuperweaponUpkeepShare.Value * d.EnergyIncome)
            {
                economy.EnergyPush = true; // build power first
                return false;
            }
            if (!QueueBuilding(construction, core, d, economy.Goal.Building, "superweapon")) return false;
            d.Upkeep += firing;
            economy.Bought = true;
            economy.BuyPending = false;
            economy.ReservedEnergy = 0;
            economy.ReservedMaterial = 0;
            Plugin.WriteDecision(new Json().Str("e", "superweapon").Int("t", GameManager.CurrentTurn).Str("ai", core.PlayerData.UserNickName)
                .Str("building", economy.Goal.Building.id.Value).Int("energyCost", economy.GoalEnergyCost).Int("materialCost", economy.GoalMaterialCost).ToString());
            Plugin.Log.LogInfo("T" + GameManager.CurrentTurn + " " + core.PlayerData.UserNickName + " queued superweapon " + economy.Goal.Building.id.Value);
            return true;
        }

        // ---- intel ---------------------------------------------------------------------------------

        private enum IntelEvaluationState { Enough, BelowPreferred, BelowMinimum }

        private static IntelEvaluationState IntelState(AICore core)
        {
            bool available = core.ConstructionEvaluation.IntelEvaluation.AvailableIntelEntitiesByEfficiency.Count > 0 && core.Intel.IntelNeed > 0f;
            if (!available) return IntelEvaluationState.Enough;
            if (!core.Intel.HasMinimumIntel) return IntelEvaluationState.BelowMinimum;
            if (!core.Intel.HasPreferredIntel) return IntelEvaluationState.BelowPreferred;
            return IntelEvaluationState.Enough;
        }

        // A plain intel building (Telescope Array). The native intel module may pick a building-with-upgrades
        // variant whose upgrades the game drops at placement, so it is not used.
        private static bool TryIntelBuilding(AIConstruction construction, AICore core, DirectorState d)
        {
            return TryYieldBuilding(construction, core, d, "intel");
        }

        // The first available, affordable building that yields intel or counterintel.
        private static bool TryYieldBuilding(AIConstruction construction, AICore core, DirectorState d, string kind)
        {
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            foreach (string id in buildings.GetBuildingIds())
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data == null || data.weapon_functionality.IsValueSet) continue;
                int yield = kind == "intel" ? Value(data.turn_yield_intel) : Value(data.turn_yield_counterintel);
                if (yield <= 0 || !available.IsBuildingAvailable(data) || !Affordable(core, data) || d.FailedThisTurn.Contains(id)) continue;
                if (!QueueBuilding(construction, core, d, data, kind)) continue;
                if (kind == "counterintel") d.CounterintelThisTurn++;
                if (kind == "intel") d.IntelThisTurn++;
                return true;
            }
            return false;
        }

        private const string NuclearWeaponId = "wea_NuclearMissile";

        // The defense building that stops Nuclear Missiles best in the active ruleset (Ion in Enhanced), or null.
        private static string AntiNukeDefenseId()
        {
            string best = null;
            float bestP = 0f;
            foreach (KeyValuePair<string, Dictionary<string, float>> pair in DefensePlanner.Counters)
            {
                float p;
                if (pair.Value.TryGetValue(NuclearWeaponId, out p) && p > bestP)
                {
                    best = pair.Key;
                    bestP = p;
                }
            }
            return best;
        }

        // ---- defense -------------------------------------------------------------------------------

        private static bool TryDefense(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, DirectorState d)
        {
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            List<string> candidates = new List<string>();
            foreach (string id in DefensePlanner.Counters.Keys)
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data == null || !available.IsBuildingAvailable(data)) continue;
                if (d.FailedThisTurn.Contains(id) || !Affordable(core, data)) continue; // another type when one fails
                candidates.Add(id);
            }
            Dictionary<string, int> owned = new Dictionary<string, int>(economy.OwnDefenses);
            foreach (KeyValuePair<string, int> pair in economy.DefensesQueued)
            {
                int n;
                owned.TryGetValue(pair.Key, out n);
                owned[pair.Key] = n + pair.Value;
            }
            string choice = DefensePlanner.Choose(economy.EnemyWeapons, owned, candidates);
            // No known threat (the floor, or hit by weapons we never saw): Kinetic first, then Ion.
            if (choice == null) choice = DefensePlanner.ChooseBlind(owned, candidates);
            if (choice == null) return false;
            if (!QueueBuilding(construction, core, d, buildings.GetBuilding(choice), "defense")) return false;
            Increment(economy.DefensesQueued, choice);
            d.DefensesThisTurn++;
            return true;
        }

        // ---- weapons -------------------------------------------------------------------------------

        private static bool TryWeapon(AIConstruction construction, AICore core, DirectorState d, float upkeepCap)
        {
            if (d.SlotsLeft <= 0) return false;
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            WeaponsData weapons = GameDataFileManager.GetDataFile<WeaponsData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            List<WeaponStats> options = new List<WeaponStats>();
            foreach (string id in buildings.GetBuildingIds())
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data == null || !data.weapon_functionality.IsValueSet) continue;
                if (data.max_amount_at_once.IsValueSet && data.max_amount_at_once.Value > 0) continue; // superweapons: savings logic
                if (!available.IsBuildingAvailable(data) || !Affordable(core, data) || d.FailedThisTurn.Contains(id)) continue;
                WeaponData weapon = weapons.GetWeapon(data.weapon_functionality.Value);
                if (weapon == null) continue;
                string type = weapon.type.IsValueSet ? weapon.type.Value : "";
                if (!Plugin.EnableLasers.Value && (type == "type_Laser" || type == "type_EMPLaser" || type == "type_SolarLaser")) continue;
                int firing = weapon.firing_cost_energy.IsValueSet ? weapon.firing_cost_energy.Value : 0;
                if (d.Upkeep + firing > upkeepCap) continue;
                // Damage proxy: per-hit damage x fragments x projectiles, scaled by blast area (a railgun's 6.0
                // radius hits several buildings) and by homing (missiles with a lock-on range hit more often).
                float damage = (Value(weapon.building_damage) + Value(weapon.population_damage))
                    * Math.Max(1, Value(weapon.fragment_amount)) * Math.Max(1, Value(weapon.projectile_amount))
                    * (0.5f + FloatValue(weapon.area_of_effect_radius, 0f) / 6f)
                    * (FloatValue(weapon.lock_on_range, 0f) > 0f ? 1.5f : 1f);
                options.Add(new WeaponStats
                {
                    BuildingId = id,
                    WeaponId = data.weapon_functionality.Value,
                    BuildEnergy = Value(data.build_cost_energy),
                    BuildMaterial = Value(data.build_cost_material),
                    FiringEnergy = firing,
                    Damage = damage,
                });
            }
            WeaponStats pick = EconomyDirector.PickWeapon(options, d.TargetDefenses, DefensePlanner.Counters, Plugin.DefensesForFullCover.Value, 4f, 0.6f, Plugin.Rng, d.ObservedPass);
            if (pick == null) return false;
            if (!QueueBuilding(construction, core, d, buildings.GetBuilding(pick.BuildingId), "weapon")) return false;
            d.Upkeep += pick.FiringEnergy;
            d.WeaponEnergySpent += pick.BuildEnergy;
            d.WeaponsThisTurn++;
            return true;
        }

        // ---- economy -------------------------------------------------------------------------------

        private static bool TryEconomy(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, DirectorState d)
        {
            List<EconOption> options = new List<EconOption>();
            float planetFactor = EconomyDirector.PlanetMaterialFactor(core.PlayerData.ResourceHandler.PlanetMaterial, d.MaterialIncome, 10);
            AddUpgradeOptions(core, d, options, planetFactor);
            AddProjectOptions(construction, core, d, options);
            AddBuildingOptions(core, d, options, planetFactor);

            EconomyBalance balance = new EconomyBalance
            {
                EnergyIncome = d.EnergyIncome,
                MaterialIncome = d.MaterialIncome,
                WeaponUpkeep = d.Upkeep,
                TargetRatio = Plugin.EnergyToMaterialTarget.Value,
                EnergyBudget = core.Resources.Energy,
                MaterialBudget = core.Resources.Material,
                EnergyPush = economy.EnergyPush,
                ScienceWeight = d.ScienceWeight,
                // Turn-start stock against turn-start energy: spending during the turn must not flip it.
                MaterialGlut = Plugin.MaterialGlutRatio.Value > 0f
                    && EconomyDirector.IsMaterialGlut(d.TurnStartMaterial, d.TurnStartEnergy, Plugin.MaterialGlutRatio.Value, Plugin.MaterialGlutMin.Value),
                EnergyGlut = d.EnergyGlut,
            };
            d.MaterialGlut = balance.MaterialGlut;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                EconOption best = EconomyDirector.Best(options, balance, d.SlotsLeft);
                if (best == null) return false;
                if (Execute(construction, core, d, best))
                {
                    if (!best.SavesReserve)
                    {
                        d.EnergyIncome += best.EnergyGain;
                        d.MaterialIncome += best.MaterialGain;
                    }
                    return true;
                }
                options.Remove(best);
                d.FailedThisTurn.Add(OptionKey(best));
            }
            return false;
        }

        private static bool Execute(AIConstruction construction, AICore core, DirectorState d, EconOption option)
        {
            switch (option.Kind)
            {
                case OptionKind.Upgrade:
                    return QueueUpgrade(construction, core, d, (BaseBuilding)option.Target, (UpgradeData)option.Payload);
                case OptionKind.Project:
                    return QueueProject(construction, core, d, (AIProjectEntity)option.Payload);
                default:
                    return QueueBuilding(construction, core, d, (BuildingData)option.Payload, option.Kind == OptionKind.Plant ? "power" : "material");
            }
        }

        private static string OptionKey(EconOption option)
        {
            if (option.Kind == OptionKind.Upgrade) return "upg:" + ((BaseBuilding)option.Target).ObjectId + ":" + option.Id;
            if (option.Kind == OptionKind.Project) return "pro:" + option.Id;
            return option.Id;
        }

        private static void AddUpgradeOptions(AICore core, DirectorState d, List<EconOption> options, float planetFactor)
        {
            PlayerData me = core.PlayerData;
            UpgradesData upgrades = GameDataFileManager.GetDataFile<UpgradesData>();
            PlayerAvailableBuildings available = me.BuildingHandler.AvailableBuildings;
            float globalEnergy = me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldEnergy);
            float globalMaterial = me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldMaterial);
            float globalScience = me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldScience);
            // Identical options (same building type, same installed upgrades) are listed once.
            HashSet<string> seen = new HashSet<string>();
            foreach (BaseBuilding building in me.BuildingHandler.Buildings)
            {
                if (building == null || building.IsCity || building.CurrentState != BaseBuilding.BuildingState.Normal || building.BuildingData == null) continue;
                BuildingData data = building.BuildingData;
                int baseEnergy = Value(data.turn_yield_energy), baseMaterial = Value(data.turn_yield_material);
                // v0.9: telescopes and Data Security Hubs yield nothing but take +20 science upgrades.
                if (baseEnergy <= 0 && baseMaterial <= 0 && !HasScienceUpgrade(upgrades, data)) continue;
                if (!building.Power.HasPotentialPower) continue; // the resource handler counts only (potentially) powered buildings
                int planned;
                d.PlannedUpgrades.TryGetValue(building, out planned);
                if (building.Upgrades.FreeSlots - planned <= 0) continue;

                float energyMultiplier = 1f, materialMultiplier = 1f;
                float[] plannedMultipliers;
                if (d.PlannedMultipliers.TryGetValue(building, out plannedMultipliers))
                {
                    energyMultiplier *= plannedMultipliers[0];
                    materialMultiplier *= plannedMultipliers[1];
                }
                // v0.4: on a limited planet a mine takes Yield1 + Multi-Stage Refinery (planet drain x0.7), not Yield2:
                // both yield upgrades also multiply the drain, so the planet ran dry at turns 25-28 (31 Oct v0.3.1).
                bool drainsPlanet = Value(data.turn_cost_planet_material) > 0;
                bool limitedPlanet = drainsPlanet && Plugin.PlanetEfficiency.Value && GameSessionManager.CurrentGameSettings.PlanetMaterial != -1;
                UpgradeData efficiency = null;
                if (limitedPlanet)
                {
                    foreach (UpgradeData candidate in upgrades.GetAvailableBuildingUpgrades(data.id.Value))
                    {
                        if (candidate == null || !available.IsUpgradeAvailable(candidate)) continue;
                        if (FloatValue(candidate.turn_cost_planet_material_multiplier, 1f) < 1f && FloatValue(candidate.turn_yield_material_multiplier, 1f) <= 1f)
                        {
                            efficiency = candidate;
                            break;
                        }
                    }
                }
                string installed = "";
                float planetDrainMultiplier = 1f;
                foreach (UpgradeData current in building.Upgrades.CurrentUpgrades)
                {
                    if (current == null) continue;
                    energyMultiplier *= FloatValue(current.turn_yield_energy_multiplier, 1f);
                    materialMultiplier *= FloatValue(current.turn_yield_material_multiplier, 1f);
                    planetDrainMultiplier *= FloatValue(current.turn_cost_planet_material_multiplier, 1f);
                    installed += current.id.Value + ";";
                }
                foreach (UpgradeData upgrade in upgrades.GetAvailableBuildingUpgrades(data.id.Value))
                {
                    if (upgrade == null || !available.IsUpgradeAvailable(upgrade)) continue;
                    if (building.Upgrades.HasUpgrade(upgrade)) continue;
                    string key = building.ObjectId + ":" + upgrade.id.Value;
                    if (d.PlannedUpgradeKeys.Contains(key) || d.FailedThisTurn.Contains("upg:" + key)) continue;
                    float energyGain = baseEnergy * globalEnergy * energyMultiplier * (FloatValue(upgrade.turn_yield_energy_multiplier, 1f) - 1f);
                    float materialGain = baseMaterial * globalMaterial * materialMultiplier * (FloatValue(upgrade.turn_yield_material_multiplier, 1f) - 1f) * planetFactor;
                    bool savesReserve = false;
                    if (efficiency != null && upgrade == efficiency)
                    {
                        // The refinery lowers the planet drain, not production. The reserve shrinks by
                        // max(cost, production), so it saves only while cost > production, and nothing on an empty
                        // planet. Value: drain saved x material per unit drained (production / cost).
                        PlayerResourceHandler res = me.ResourceHandler;
                        float cost = res.PlanetMaterialCost, production = res.MaterialProductionPerTurn;
                        if (res.PlanetMaterial <= 0 || cost <= production || cost <= 0f) continue;
                        float buildingDrain = Value(data.turn_cost_planet_material) * planetDrainMultiplier;
                        float saved = Math.Min(buildingDrain * (1f - FloatValue(upgrade.turn_cost_planet_material_multiplier, 1f)), cost - production);
                        energyGain = 0f;
                        materialGain = saved * (production / cost);
                        savesReserve = true;
                    }
                    else if (limitedPlanet && materialMultiplier > 1f && FloatValue(upgrade.turn_yield_material_multiplier, 1f) > 1f)
                    {
                        continue; // one yield upgrade per mine on a limited planet: the other slot is for the refinery
                    }
                    float scienceGain = Value(upgrade.turn_yield_science_addition) * globalScience;
                    if (energyGain <= 0f && materialGain <= 0f && scienceGain <= 0f) continue;
                    if (!seen.Add(data.id.Value + "|" + installed + "|" + upgrade.id.Value + "|" + planned)) continue;
                    options.Add(new EconOption
                    {
                        Id = upgrade.id.Value,
                        Kind = OptionKind.Upgrade,
                        Energy = Value(upgrade.build_cost_energy),
                        Material = Value(upgrade.build_cost_material),
                        EnergyGain = energyGain,
                        MaterialGain = materialGain,
                        ScienceGain = scienceGain,
                        SavesReserve = savesReserve,
                        Payload = upgrade,
                        Target = building,
                    });
                }
            }
        }

        private static bool HasScienceUpgrade(UpgradesData upgrades, BuildingData data)
        {
            foreach (UpgradeData u in upgrades.GetAvailableBuildingUpgrades(data.id.Value))
            {
                if (u != null && Value(u.turn_yield_science_addition) > 0) return true;
            }
            return false;
        }

        private static void AddProjectOptions(AIConstruction construction, AICore core, DirectorState d, List<EconOption> options)
        {
            PlayerResourceHandler res = core.PlayerData.ResourceHandler;
            float globalEnergy = core.PlayerData.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldEnergy);
            float globalMaterial = core.PlayerData.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldMaterial);
            int cities = Math.Max(1, core.PlayerData.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.City).Count);
            HashSet<string> seen = new HashSet<string>();
            List<AIConstructionEntity> entities = new List<AIConstructionEntity>();
            entities.AddRange(core.ConstructionEvaluation.EnergyGenerationEvaluation.AvailableEnergyGenerationEntitiesByEfficiency);
            entities.AddRange(core.ConstructionEvaluation.MaterialGenerationEvaluation.AvailableMaterialGenerationEntitiesByEfficiency);
            // v0.9.1 (Terra v0.9 #1): the native evaluations list only projects that raise energy or material, so science
            // projects (Science Institute etc.) never reached the director. List them from the city data directly.
            Dictionary<string, float> cityScience = new Dictionary<string, float>(); // city type -> average science per city
            try
            {
                ProjectsData projectsData = GameDataFileManager.GetDataFile<ProjectsData>();
                PlayerAvailableBuildings availableNow = core.PlayerData.BuildingHandler.AvailableBuildings;
                float globalScience = core.PlayerData.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldScience);
                Dictionary<string, int[]> populations = new Dictionary<string, int[]>();
                Dictionary<string, CityData> types = new Dictionary<string, CityData>();
                foreach (BaseBuilding city in core.PlayerData.BuildingHandler.GetRemainingCities())
                {
                    CityData cd = city.CityData;
                    if (cd == null) continue;
                    int[] pop;
                    if (!populations.TryGetValue(cd.id.Value, out pop))
                    {
                        pop = new int[2];
                        populations[cd.id.Value] = pop;
                        types[cd.id.Value] = cd;
                    }
                    pop[0] += city.Stats.CurrentPopulation;
                    pop[1]++;
                }
                foreach (KeyValuePair<string, CityData> type in types)
                {
                    int[] pop = populations[type.Key];
                    cityScience[type.Key] = pop[0] / (float)Math.Max(1, pop[1]) * FloatValue(type.Value.science_yield_per_population, 0f) * globalScience;
                    foreach (ProjectData p in projectsData.GetAvailableCityProjects(type.Key))
                    {
                        if (p == null || !availableNow.IsProjectAvailable(p)) continue;
                        if (FloatValue(p.turn_yield_science_multiplier, 1f) <= 1f && FloatValue(p.turn_yield_science_multiplier_global, 1f) <= 1f) continue;
                        if (AIProjectEntity.IsValidForEnergyGeneration(p) || AIProjectEntity.IsValidForMaterialGeneration(p)) continue; // listed natively
                        entities.Add(new AIProjectEntity(core, AIConstructionEntityValueType.EnergyGeneration, p, type.Value));
                    }
                }
            }
            catch (Exception e)
            {
                ReportOnce("science-projects", e);
            }
            foreach (AIConstructionEntity entity in entities)
            {
                AIProjectEntity project = entity as AIProjectEntity;
                if (project == null || project.Project == null || project.City == null) continue;
                if (project.OtherProjects != null && project.OtherProjects.Count > 0) continue;
                string key = project.Project.id.Value + "@" + project.City.id.Value;
                if (!seen.Add(key) || d.FailedThisTurn.Contains("pro:" + key)) continue;
                if (!HasCityForProject(construction, core, project)) continue;

                ProjectData p = project.Project;
                // As BuildingProjects.TryInstallProject: no duration means permanent.
                bool permanent = (p.permanent.IsValueSet && p.permanent.Value) || !p.duration.IsValueSet;
                float span = permanent ? 1f : Math.Min(10, Math.Max(1, p.duration.Value)) / 10f;
                float energyGain = Value(p.turn_yield_energy_addition) * globalEnergy - Value(p.turn_cost_energy_addition)
                    + res.EnergyProductionPerTurn * (FloatValue(p.turn_yield_energy_multiplier_global, 1f) - 1f);
                float materialGain = Value(p.turn_yield_material_addition) * globalMaterial - Value(p.turn_cost_material_addition)
                    + res.MaterialProductionPerTurn * (FloatValue(p.turn_yield_material_multiplier_global, 1f) - 1f);
                // Local multiplier: that city's science (population x its rate, Terra v0.9 #5); global: all science.
                float thisCityScience;
                if (!cityScience.TryGetValue(project.City.id.Value, out thisCityScience)) thisCityScience = res.ScienceProductionPerTurn / (float)cities;
                float scienceGain = thisCityScience * (FloatValue(p.turn_yield_science_multiplier, 1f) - 1f)
                    + res.ScienceProductionPerTurn * (FloatValue(p.turn_yield_science_multiplier_global, 1f) - 1f);
                if (energyGain * span <= 0f && materialGain * span <= 0f && scienceGain <= 0f) continue;
                options.Add(new EconOption
                {
                    Id = key,
                    Kind = OptionKind.Project,
                    Energy = project.EnergyCost,
                    Material = project.MaterialCost,
                    EnergyGain = energyGain * span,
                    MaterialGain = materialGain * span,
                    ScienceGain = scienceGain * span,
                    Payload = project,
                });
            }
        }

        private static bool HasCityForProject(AIConstruction construction, AICore core, AIProjectEntity project)
        {
            foreach (BaseBuilding city in core.PlayerData.BuildingHandler.GetBuildingsByID(project.City.id.Value))
            {
                if (city == null || city.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                if (!construction.Projects.HasCityFreeSlots(city)) continue;
                if (construction.Projects.HasCityProject(city, project.Project)) continue;
                if (construction.ConstructionQueue.IsProjectInQueue(project.Project, city)) continue;
                return true;
            }
            return false;
        }

        private static void AddBuildingOptions(AICore core, DirectorState d, List<EconOption> options, float planetFactor)
        {
            PlayerData me = core.PlayerData;
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            PlayerAvailableBuildings available = me.BuildingHandler.AvailableBuildings;
            float globalEnergy = me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldEnergy);
            float globalMaterial = me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldMaterial);
            foreach (string id in s_economyBuildings)
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data == null || !available.IsBuildingAvailable(data) || d.FailedThisTurn.Contains(id)) continue;
                if (available.GetLimitedBuildingConstructionsLeft(data) == 0) continue;
                bool isPlant = data.distribute_power.IsValueSet && data.distribute_power.Value;
                bool waterOnly = IsWaterOnly(data);
                if (waterOnly && d.Turn < d.WaterCooldownUntil) continue;
                if (waterOnly && !isPlant && !HasWaterAnchor(core)) continue; // an offshore mine needs a powered water neighbour
                float materialGain = Value(data.turn_yield_material) * globalMaterial * planetFactor - Value(data.turn_cost_material);
                if (!isPlant && Value(data.turn_yield_material) > 0 && me.ResourceHandler.PlanetMaterial <= 0) continue;
                options.Add(new EconOption
                {
                    Id = id,
                    Kind = isPlant ? OptionKind.Plant : OptionKind.Producer,
                    Energy = Value(data.build_cost_energy),
                    Material = Value(data.build_cost_material),
                    EnergyGain = Value(data.turn_yield_energy) * globalEnergy,
                    MaterialGain = materialGain,
                    NeedsSlot = !isPlant,
                    Payload = data,
                });
            }
        }

        private static BuildingData PickLandPlant(AICore core, DirectorState d)
        {
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            foreach (string id in new[] { "bui_SolarPlant", "bui_NuclearPlant" })
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data != null && available.IsBuildingAvailable(data) && Affordable(core, data) && !d.FailedThisTurn.Contains(id)) return data;
            }
            return null;
        }

        // v0.6: when land plants cannot be placed (14 abandoned Solar Plants while 7 -> 22 buildings sat unpowered, 31 Oct 1v1),
        // power the grid from the water.
        private static BuildingData PickWaterPlant(AICore core, DirectorState d)
        {
            if (GameManager.CurrentTurn < d.WaterCooldownUntil) return null;
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            BuildingData data = buildings.HasBuilding("bui_FloatingPlant") ? buildings.GetBuilding("bui_FloatingPlant") : null;
            if (data != null && available.IsBuildingAvailable(data) && Affordable(core, data) && !d.FailedThisTurn.Contains("bui_FloatingPlant")) return data;
            return null;
        }

        // ---- queue primitives ----------------------------------------------------------------------

        private static bool QueueBuilding(AIConstruction construction, AICore core, DirectorState d, BuildingData data, string role)
        {
            // A type whose placement gave up this turn is not queued again (v0.3.1: 241 Railgun searches requeued).
            if (data == null || d.FailedThisTurn.Contains(data.id.Value) || !Affordable(core, data)) return false;
            // v0.11: placements resolve after many are queued, so a full planet saw 22-55 of one type queued and abandoned
            // every turn (v0.10.1 Maverick T24-T28). A type abandoned 3+ times last turn is queued at most twice this turn.
            int abandoned, queuedNow;
            d.AbandonedLastTurn.TryGetValue(data.id.Value, out abandoned);
            d.QueuedById.TryGetValue(data.id.Value, out queuedNow);
            if (abandoned >= 3 && queuedNow >= 2) return false;
            bool needsSlot = !(data.distribute_power.IsValueSet && data.distribute_power.Value);
            if (needsSlot && d.SlotsLeft <= 0) return false;
            construction.ConstructionQueue.AddBuildingToQueue(data); // charges core.Resources
            if (needsSlot)
            {
                d.SlotsLeft--;
                construction.PowerSlotNeed++; // keep the native bookkeeping consistent
            }
            Record(d, role + ":" + data.id.Value);
            Increment(d.QueuedById, data.id.Value);
            return true;
        }

        private static bool QueueUpgrade(AIConstruction construction, AICore core, DirectorState d, BaseBuilding building, UpgradeData upgrade)
        {
            int energy = Value(upgrade.build_cost_energy), material = Value(upgrade.build_cost_material);
            if (core.Resources.Energy < energy || core.Resources.Material < material) return false;
            if (building.CurrentState != BaseBuilding.BuildingState.Normal || building.Upgrades.HasUpgrade(upgrade)) return false;
            if (construction.ConstructionQueue.IsUpgradeInQueue(upgrade, building)) return false;
            int planned;
            d.PlannedUpgrades.TryGetValue(building, out planned);
            if (building.Upgrades.FreeSlots - planned <= 0) return false;
            construction.ConstructionQueue.AddUpgradeToQueue(upgrade, building);
            // The native queue does not charge upgrades to the decision budget; do it here.
            core.Resources.Energy -= energy;
            core.Resources.Material -= material;
            d.PlannedUpgrades[building] = planned + 1;
            float[] multipliers;
            if (!d.PlannedMultipliers.TryGetValue(building, out multipliers))
            {
                multipliers = new[] { 1f, 1f };
                d.PlannedMultipliers[building] = multipliers;
            }
            multipliers[0] *= FloatValue(upgrade.turn_yield_energy_multiplier, 1f);
            multipliers[1] *= FloatValue(upgrade.turn_yield_material_multiplier, 1f);
            d.PlannedUpgradeKeys.Add(building.ObjectId + ":" + upgrade.id.Value);
            Record(d, "upgrade:" + upgrade.id.Value);
            return true;
        }

        private static bool QueueProject(AIConstruction construction, AICore core, DirectorState d, AIProjectEntity project)
        {
            if (core.Resources.Energy < project.EnergyCost || core.Resources.Material < project.MaterialCost) return false;
            int before = QueueCount(construction);
            object module = s_materialModule.GetValue(construction);
            s_tryProject.Invoke(module, new object[] { project }); // native per-city slot checks
            if (QueueCount(construction) <= before) return false;
            core.Resources.Energy -= project.EnergyCost;
            core.Resources.Material -= project.MaterialCost;
            Record(d, "project:" + project.Project.id.Value);
            return true;
        }

        private static UpgradeEffect EffectOf(UpgradeData u)
        {
            return new UpgradeEffect
            {
                BuildingDamageMul = FloatValue(u.building_damage_multiplier, 1f),
                PopulationDamageMul = FloatValue(u.population_damage_multiplier, 1f),
                AoeMul = FloatValue(u.area_of_effect_radius_multiplier, 1f),
                FiringMul = FloatValue(u.firing_cost_energy_multiplier, 1f),
                FragmentAdd = Value(u.fragment_amount_addition),
                ProjectileAdd = Value(u.projectile_amount_addition),
            };
        }

        // Weapon upgrades (v0.9.1, Terra v0.9): candidates are filtered by budget, the 30% material share and the upkeep
        // cap before ranking; installed AND this turn's queued upgrades shape the weapon; an upgrade that lowers damage
        // right away (EnergyCostDown) is taken only when firing energy is what limits the AI; ranked by gain in damage
        // per firing energy; the next candidate is tried if queueing fails.
        private static bool TryWeaponUpgrade(AIConstruction construction, AICore core, DirectorState d, float upkeepCap)
        {
            PlayerData me = core.PlayerData;
            UpgradesData upgrades = GameDataFileManager.GetDataFile<UpgradesData>();
            WeaponsData weapons = GameDataFileManager.GetDataFile<WeaponsData>();
            PlayerAvailableBuildings available = me.BuildingHandler.AvailableBuildings;
            float popWeight = Plugin.PopulationDamageWeight.Value;
            float globalFiring = me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.EnergyFiringCost);
            float materialCap = Plugin.WeaponUpgradeShare.Value * d.TurnStartMaterial;
            bool upkeepBound = d.Upkeep >= 0.85f * upkeepCap;
            List<KeyValuePair<float, object[]>> ranked = new List<KeyValuePair<float, object[]>>();
            foreach (BaseBuilding building in me.BuildingHandler.Buildings)
            {
                if (building == null || building.IsCity || building.CurrentState != BaseBuilding.BuildingState.Normal || building.BuildingData == null) continue;
                BuildingData data = building.BuildingData;
                if (!data.weapon_functionality.IsValueSet) continue;
                int planned;
                d.PlannedUpgrades.TryGetValue(building, out planned);
                if (building.Upgrades.FreeSlots - planned <= 0) continue;
                WeaponData w = weapons.GetWeapon(data.weapon_functionality.Value);
                if (w == null) continue;
                WeaponProfile profile = new WeaponProfile
                {
                    BuildingDamage = Value(w.building_damage),
                    PopulationDamage = Value(w.population_damage),
                    Aoe = FloatValue(w.area_of_effect_radius, 0f),
                    FiringEnergy = Value(w.firing_cost_energy),
                    Fragments = Value(w.fragment_amount),
                    Projectiles = Value(w.projectile_amount),
                    LockOn = FloatValue(w.lock_on_range, 0f) > 0f,
                };
                foreach (UpgradeData current in building.Upgrades.CurrentUpgrades)
                {
                    if (current != null) profile = WeaponUpgrades.Apply(profile, EffectOf(current));
                }
                List<UpgradeEffect> queued;
                if (d.PlannedWeaponEffects.TryGetValue(building, out queued))
                {
                    foreach (UpgradeEffect e in queued) profile = WeaponUpgrades.Apply(profile, e);
                }
                foreach (UpgradeData upgrade in upgrades.GetAvailableBuildingUpgrades(data.id.Value))
                {
                    if (upgrade == null || !available.IsUpgradeAvailable(upgrade) || building.Upgrades.HasUpgrade(upgrade)) continue;
                    string key = building.ObjectId + ":" + upgrade.id.Value;
                    if (d.PlannedUpgradeKeys.Contains(key) || d.FailedThisTurn.Contains("upg:" + key)) continue;
                    int energy = Value(upgrade.build_cost_energy), material = Value(upgrade.build_cost_material);
                    if (energy > core.Resources.Energy || material > core.Resources.Material) continue;
                    if (d.WeaponUpgradeMaterial + material > materialCap) continue;
                    UpgradeEffect effect = EffectOf(upgrade);
                    float firingDelta = profile.FiringEnergy * globalFiring * (effect.FiringMul - 1f);
                    if (firingDelta > 0f && d.Upkeep + firingDelta > upkeepCap) continue;
                    float immediate = WeaponUpgrades.ImmediateDamageRatio(profile, effect, popWeight);
                    // v0.11: with energy in glut, firing energy is not what limits us; planet space is. Rank by damage per
                    // building (the human's 37 Railguns fired 111 shots with Projectile Amount upgrades).
                    if (d.EnergyGlut)
                    {
                        if (immediate - 1f < Plugin.WeaponUpgradeMinGain.Value) continue;
                        ranked.Add(new KeyValuePair<float, object[]>(immediate - 1f, new object[] { building, upgrade, effect, firingDelta }));
                        continue;
                    }
                    if (!upkeepBound && immediate < 1f) continue;
                    float gain = WeaponUpgrades.Gain(profile, effect, popWeight);
                    if (gain < Plugin.WeaponUpgradeMinGain.Value) continue;
                    ranked.Add(new KeyValuePair<float, object[]>(gain, new object[] { building, upgrade, effect, firingDelta }));
                }
            }
            ranked.Sort((a, b) => b.Key.CompareTo(a.Key));
            foreach (KeyValuePair<float, object[]> candidate in ranked)
            {
                BaseBuilding building = (BaseBuilding)candidate.Value[0];
                UpgradeData upgrade = (UpgradeData)candidate.Value[1];
                UpgradeEffect effect = (UpgradeEffect)candidate.Value[2];
                float firingDelta = (float)candidate.Value[3];
                if (!QueueUpgrade(construction, core, d, building, upgrade))
                {
                    d.FailedThisTurn.Add("upg:" + building.ObjectId + ":" + upgrade.id.Value);
                    continue;
                }
                List<UpgradeEffect> list;
                if (!d.PlannedWeaponEffects.TryGetValue(building, out list))
                {
                    list = new List<UpgradeEffect>();
                    d.PlannedWeaponEffects[building] = list;
                }
                list.Add(effect);
                d.WeaponUpgradesThisTurn++;
                d.WeaponUpgradeMaterial += Value(upgrade.build_cost_material);
                d.Upkeep += firingDelta;
                if (firingDelta < 0f) d.FailedThisTurn.Remove("weapon"); // freed upkeep: step 5 may build again
                Record(d, "weaponupgrade:" + upgrade.id.Value);
                return true;
            }
            return false;
        }

        private static int FiringCost(BuildingData data)
        {
            if (data == null || !data.weapon_functionality.IsValueSet) return 0;
            WeaponData weapon = GameDataFileManager.GetDataFile<WeaponsData>().GetWeapon(data.weapon_functionality.Value);
            return weapon != null ? Value(weapon.firing_cost_energy) : 0;
        }

        private static bool Affordable(AICore core, BuildingData data)
        {
            return core.Resources.Energy >= Value(data.build_cost_energy) && core.Resources.Material >= Value(data.build_cost_material);
        }

        // ---- placement fixes -------------------------------------------------------------------------

        private static bool IsWaterOnly(BuildingData data)
        {
            bool water = data.buildable_on_water.IsValueSet && data.buildable_on_water.Value;
            bool land = data.buildable_on_land.IsValueSet && data.buildable_on_land.Value;
            return water && !land;
        }

        private static bool HasWaterAnchor(AICore core)
        {
            foreach (BaseBuilding building in core.PlayerData.BuildingHandler.Buildings)
            {
                if (IsWaterAnchor(core, building, true)) return true;
            }
            return false;
        }

        private static bool IsWaterAnchor(AICore core, BaseBuilding building, bool needsFreeSlot)
        {
            if (building == null || building.IsCity || building.BuildingData == null) return false;
            if (building.CurrentState == BaseBuilding.BuildingState.Ruins || building.CurrentState == BaseBuilding.BuildingState.Destroyed) return false;
            if (!IsWaterOnly(building.BuildingData)) return false;
            if (!building.Power.IsPotentiallyDistributingPower) return false;
            return !needsFreeSlot || core.PowerGrid.HasBuildingAvailableSlots(building);
        }

        // Prefix on AIBuildingPlacementSearch.step for water-only buildings: place next to our own water
        // buildings, or (plants only, which need no power line) anywhere on the planet. Native picks a random
        // land neighbour, which almost never has water within reach.
        // ---- coverage-aware defense placement (v0.7) --------------------------------------------------
        // The game puts every defense within its radius of a random city and never looks at what is already covered
        // (31 Oct v0.6: 53-85% of shots met no eligible defense). Here a defense is anchored to the building whose
        // neighbourhood holds the most uncovered value; the game's own placement then runs around that anchor, with the
        // step count raised to its defense timeout so it places within the defense radius of the anchor and skips the
        // "near a city" rule. Any failure or nothing-to-cover falls back to the game's own step.

        private sealed class CoverageState
        {
            public int Steps;
        }

        private static readonly Dictionary<AIBuildingPlacementSearch, CoverageState> s_coverage = new Dictionary<AIBuildingPlacementSearch, CoverageState>();
        private static readonly FieldInfo s_searchResult = AccessTools.Field(typeof(AIBuildingPlacementSearch), "m_result");
        private static readonly MethodInfo s_setSearchComplete = AccessTools.PropertySetter(typeof(AIBuildingPlacementSearch), "IsComplete");
        private const int CoverageCandidatesPerStep = 12;
        private const int CoverageCandidatesPerFrame = 120;
        private const int CoverageStepCap = 150;

        private static float DefenseRadius(BuildingData data)
        {
            DefensesData defenses = GameDataFileManager.GetDataFile<DefensesData>();
            DefenseData defense = defenses != null ? defenses.GetDefense(data.defense_functionality.Value) : null;
            if (defense == null || !defense.defense_radius.IsValueSet) return 0f;
            return defense.defense_radius.Value * (defense.defense_scale.IsValueSet ? defense.defense_scale.Value : 1f);
        }

        // What a new defense of this id would protect: our buildings plus this turn's queued placements as targets; as
        // coverage, our working (normal, powered) defenses of the same id with their current radius, plus this turn's
        // queued ones of that id (Terra v0.7: same-turn placements did not see each other).
        private sealed class CoverageModel
        {
            public readonly List<Point> Targets = new List<Point>();
            public readonly List<float> Values = new List<float>();
            public int[] Counts;     // defenses of this id already over each target (v0.8: stacking)
            public readonly List<float> Loss = new List<float>(); // reach lost per target (anti-nuke: half a city's radius)
            public float Radius;
            public float P;          // this defense's best interception chance
            public bool AntiNuke;
        }

        private static CoverageModel BuildCoverageModel(AICore core, BuildingData data)
        {
            CoverageModel m = new CoverageModel { Radius = DefenseRadius(data) };
            if (m.Radius <= 0f) return null;
            Dictionary<string, float> stops;
            m.P = 0.8f;
            if (DefensePlanner.Counters.TryGetValue(data.id.Value, out stops))
            {
                float bestStop = 0f;
                foreach (float v in stops.Values) bestStop = Math.Max(bestStop, v);
                if (bestStop > 0f) m.P = Math.Min(0.95f, bestStop);
            }
            DirectorState ds;
            // An anti-nuke defense (wanted this turn) is placed for the cities and stacks (p = its chance vs nukes);
            // every other defense keeps the v0.7 rule, "uncovered value" (p = 1: a covered target adds nothing)
            // (Terra v0.8 #7: residual value made Kinetics cluster).
            bool antiNuke = s_states.TryGetValue(core.PlayerData.PlayerID, out ds) && ds.AntiNukeWanted > 0 && data.id.Value == ds.AntiNukeId;
            float cityValue = antiNuke ? Plugin.AntiNukeCityValue.Value : Plugin.CoverageCityValue.Value;
            float nukeStop;
            if (antiNuke && stops != null && stops.TryGetValue(NuclearWeaponId, out nukeStop)) m.P = Math.Min(0.95f, nukeStop);
            else m.P = 1f;
            m.AntiNuke = antiNuke;
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            string id = data.id.Value;
            List<Point> defenses = new List<Point>();
            List<float> radii = new List<float>();
            foreach (BaseBuilding b in core.PlayerData.BuildingHandler.Buildings)
            {
                if (b == null || b.CurrentState == BaseBuilding.BuildingState.Ruins || b.CurrentState == BaseBuilding.BuildingState.Destroyed) continue;
                Vector3 p = b.transform.position;
                m.Targets.Add(new Point(p.x, p.y, p.z));
                bool defense = b.BuildingData != null && b.BuildingData.defense_functionality.IsValueSet;
                m.Values.Add(b.IsCity ? cityValue : (defense ? 0.25f : 1f));
                m.Loss.Add(antiNuke && b.IsCity ? 0.5f * b.Radius : 0f);
                if (b.BuildingData != null && b.BuildingData.id.Value == id && b.CurrentState == BaseBuilding.BuildingState.Normal && b.Power.HasPower)
                {
                    defenses.Add(new Point(p.x, p.y, p.z));
                    radii.Add(b.Stats.Defenses.Radius);
                }
            }
            foreach (AIConstructBuildingAction action in core.Actions.ConstructedBuildings)
            {
                if (action == null || action.BuildingPlacementResult == null || action.BuildingPlacementResult.Result == null) continue;
                Vector3 p = action.BuildingPlacementResult.Result.BuildingWorldPosition;
                Point point = new Point(p.x, p.y, p.z);
                m.Targets.Add(point);
                m.Loss.Add(0f);
                BuildingData queued = buildings.HasBuilding(action.BuildingID) ? buildings.GetBuilding(action.BuildingID) : null;
                m.Values.Add(queued != null && queued.defense_functionality.IsValueSet ? 0.25f : 1f);
                if (action.BuildingID == id)
                {
                    defenses.Add(point);
                    radii.Add(m.Radius);
                }
            }
            m.Counts = DefenseCoverage.CoverCounts(m.Targets, defenses, radii, m.Loss);
            return m;
        }

        // One placement step for a defense. Candidate spots are sampled around our buildings with a free power slot
        // (the game's connector ring, capped at the defense radius). Every candidate goes through the game's own full
        // placement check (TryPlaceBuilding: terrain, collisions, fallout; it places nothing) and the game's exact
        // power-line rule (PlayerPowerGridHandler: centre distance <= both radii + the longer connector range); the legal
        // one covering the most uncovered value wins. With nothing left to cover the same safe path places it anywhere
        // legal and connected: we never hand a defense back to the game's search, which samples past line reach
        // (Terra v0.7.1). No legal spot this step -> try again next step; the abandon valve refunds a hopeless search.
        // Defense searches fail closed (Terra v0.7.2): on an error, a missing radius or too many fruitless steps the search
        // is pushed to the abandon valve (refund) instead of the game's own search, which can leave the defense unconnected.
        private static void AbandonSoon(AIBuildingPlacementSearch search)
        {
            s_searchSteps.SetValue(search, Math.Max((int)s_searchSteps.GetValue(search), Math.Max(100, Plugin.PlacementStepCap.Value)));
        }

        private static bool CoverageStep(AIBuildingPlacementSearch search, BuildingData data, AICore core)
        {
            try
            {
                return CoverageStepUnsafe(search, data, core);
            }
            catch (Exception e)
            {
                ReportOnce("defense-placement", e);
                AbandonSoon(search);
                return true;
            }
        }

        private static int s_budgetFrame = -1;
        private static int s_candidatesThisFrame;

        private static bool CoverageStepUnsafe(AIBuildingPlacementSearch search, BuildingData data, AICore core)
        {
            if (s_coverage.Count > 300) s_coverage.Clear(); // finished searches are never removed otherwise
            CoverageState st;
            if (!s_coverage.TryGetValue(search, out st))
            {
                st = new CoverageState();
                s_coverage[search] = st;
            }
            // Frame budget: the finder may run ~100 steps in one update; candidates beyond the budget wait for the next
            // frame (the step is not counted, so waiting never pushes a search toward abandonment).
            if (Time.frameCount != s_budgetFrame)
            {
                s_budgetFrame = Time.frameCount;
                s_candidatesThisFrame = 0;
            }
            if (s_candidatesThisFrame >= CoverageCandidatesPerFrame) return true;
            CoverageModel model = BuildCoverageModel(core, data);
            if (model == null)
            {
                AbandonSoon(search); // a defense without a radius: nothing safe to plan
                return true;
            }
            if (++st.Steps > CoverageStepCap)
            {
                AbandonSoon(search);
                return true;
            }
            s_searchSteps.SetValue(search, (int)s_searchSteps.GetValue(search) + 1); // the abandon valve counts every step

            List<BaseBuilding> bases = new List<BaseBuilding>();
            List<Point> anchors = new List<Point>();
            foreach (BaseBuilding b in core.PlayerData.BuildingHandler.Buildings)
            {
                if (b == null || b.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                if (!b.Power.IsPotentiallyDistributingPower || !core.PowerGrid.HasBuildingAvailableSlots(b)) continue;
                bases.Add(b);
                Vector3 p = b.transform.position;
                anchors.Add(new Point(p.x, p.y, p.z));
            }
            if (bases.Count == 0) return true; // nothing to connect to yet

            // Widen as steps fail (Terra v0.7.2: a blocked best anchor must not trap the search): first the near-best
            // anchors, then every anchor with something uncovered, then any anchor at all (coverage is still scored).
            List<int> picks = new List<int>();
            if (st.Steps <= 8)
            {
                float band = st.Steps <= 3 ? Plugin.CoverageBand.Value : 0f;
                picks = DefenseCoverage.PickAnchorsMarginal(anchors, model.Targets, model.Values, model.Counts, model.Radius, model.P, band, Plugin.Rng, CoverageCandidatesPerStep, model.Loss);
            }
            if (picks.Count == 0)
            {
                for (int n = 0; n < CoverageCandidatesPerStep; n++) picks.Add(Plugin.Rng.Next(bases.Count)); // any safe spot
            }
            s_candidatesThisFrame += picks.Count;

            float scale = data.building_scale.IsValueSet ? data.building_scale.Value : 1f;
            float ownRadius = (data.building_radius.IsValueSet ? data.building_radius.Value : 1f) * scale;
            float ownReach = data.power_connector_range.IsValueSet ? data.power_connector_range.Value : 0f;
            Vector3 planetCentre = core.Planet.transform.position;
            float bestScore = -1f;
            BuildingPlacementResult bestResult = null;
            BaseBuilding bestAnchor = null;
            foreach (int a in picks)
            {
                BaseBuilding anchor = bases[a];
                float lineLimit = ownRadius + anchor.Radius + Math.Max(ownReach, anchor.Power.PowerConnectorRange);
                float inner = ownRadius + anchor.Radius;
                float outer = Math.Min(lineLimit * 0.9f, model.AntiNuke ? model.Radius - 0.85f : model.Radius); // anti-nuke: inside what can count for a city
                if (outer < inner) outer = inner;
                float distance = inner + (float)Plugin.Rng.NextDouble() * (outer - inner);
                float angle = (float)(Plugin.Rng.NextDouble() * Math.PI * 2.0);
                Vector3 world = anchor.transform.TransformPoint(new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * distance);
                Vector3 down = (planetCentre - world).normalized;
                world += -down * 2f;
                Ray ray = new Ray(world, down);
                if (!core.Construction.IsBuildingPlacementValid(data, core.Planet, ray)) continue; // overlaps a queued building
                BuildingPlacementResult placed = core.PlayerData.BuildingHandler.TryPlaceBuilding(data, ray, core.Planet, null);
                if (placed == null || !placed.IsSuccessful) continue;
                if (Vector3.Distance(placed.BuildingWorldPosition, anchor.transform.position) > lineLimit) continue; // line would be refused
                Vector3 at = placed.BuildingWorldPosition;
                float score = DefenseCoverage.MarginalValue(new Point(at.x, at.y, at.z), model.Targets, model.Values, model.Counts, model.Radius, model.P, model.Loss);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestResult = placed;
                    bestAnchor = anchor;
                }
            }
            if (bestResult == null) return true; // no legal, connected spot this step: try again next step

            AIBuildingPlacementResult result = (AIBuildingPlacementResult)s_searchResult.GetValue(search);
            result.Result = bestResult;
            result.PowerGridConnection = bestAnchor.Power;
            s_setSearchComplete.Invoke(search, new object[] { true });
            return true;
        }

        // Share of our buildings (by value) and number of our cities inside a Kinetic Defense's radius, for the decision log.
        private static void CoverageShares(AICore core, out float share, out int citiesCovered, out int cities)
        {
            share = 0f;
            citiesCovered = 0;
            cities = 0;
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            BuildingData kinetic = buildings.HasBuilding("bui_KineticDefense") ? buildings.GetBuilding("bui_KineticDefense") : null;
            if (kinetic == null || !kinetic.defense_functionality.IsValueSet) return;
            List<Point> targets = new List<Point>(), defenses = new List<Point>();
            List<float> radii = new List<float>();
            List<float> values = new List<float>();
            List<bool> isCity = new List<bool>();
            foreach (BaseBuilding b in core.PlayerData.BuildingHandler.Buildings)
            {
                if (b == null || b.CurrentState == BaseBuilding.BuildingState.Ruins || b.CurrentState == BaseBuilding.BuildingState.Destroyed) continue;
                Vector3 p = b.transform.position;
                targets.Add(new Point(p.x, p.y, p.z));
                values.Add(b.IsCity ? Plugin.CoverageCityValue.Value : 1f);
                isCity.Add(b.IsCity);
                if (b.BuildingData != null && b.BuildingData.id.Value == "bui_KineticDefense" && b.CurrentState == BaseBuilding.BuildingState.Normal && b.Power.HasPower)
                {
                    defenses.Add(new Point(p.x, p.y, p.z));
                    radii.Add(b.Stats.Defenses.Radius);
                }
            }
            bool[] covered = DefenseCoverage.Covered(targets, defenses, radii);
            share = DefenseCoverage.CoveredShare(values, covered);
            for (int i = 0; i < isCity.Count; i++)
            {
                if (!isCity[i]) continue;
                cities++;
                if (covered[i]) citiesCovered++;
            }
        }

        private static bool SearchStepPrefix(AIBuildingPlacementSearch __instance)
        {
            if (!Plugin.Active || !Plugin.DirectorEnabled.Value) return true;
            try
            {
                BuildingData data = (BuildingData)s_searchBuilding.GetValue(__instance);
                if (data != null && Plugin.CoveragePlacement.Value && data.defense_functionality.IsValueSet)
                {
                    AICore defenseCore = (AICore)s_searchCore.GetValue(__instance);
                    if (defenseCore != null && defenseCore.PlayerData.AIPlayer && CoverageStep(__instance, data, defenseCore)) return false;
                    return true;
                }
                if (data == null || !IsWaterOnly(data)) return true;
                AICore core = (AICore)s_searchCore.GetValue(__instance);
                if (core == null || !core.PlayerData.AIPlayer) return true;

                if (!__instance.m_needsPowerGrid)
                {
                    // A plant needs no power line: any water spot. Random attempts never set a power
                    // connection, so the plant takes no anchor slot the director counted as free.
                    s_searchSteps.SetValue(__instance, (int)s_searchSteps.GetValue(__instance) + 1);
                    s_placeRandom.Invoke(__instance, null);
                    return false;
                }
                // Offshore mine: next to our own water building with a free slot, so it is connected.
                List<BaseBuilding> anchors = new List<BaseBuilding>();
                foreach (BaseBuilding building in core.PlayerData.BuildingHandler.Buildings)
                {
                    if (IsWaterAnchor(core, building, true)) anchors.Add(building);
                }
                if (anchors.Count == 0) return true; // native: land neighbours with free slots (coastal luck)
                s_searchSteps.SetValue(__instance, (int)s_searchSteps.GetValue(__instance) + 1);
                s_placeNextTo.Invoke(__instance, new object[] { anchors });
                return false;
            }
            catch (Exception e)
            {
                ReportOnce("water-placement", e);
                return true;
            }
        }

        // Prefix on AIBuildingPlacementFinder.Update: give up on a placement after a step cap instead of
        // searching until the turn ends, and give the construction queue its budget back.
        private static void FinderUpdatePrefix(AIBuildingPlacementFinder __instance)
        {
            if (!Plugin.Active || !Plugin.DirectorEnabled.Value) return;
            try
            {
                List<AIBuildingPlacementSearch> searches = s_finderSearches.GetValue(__instance) as List<AIBuildingPlacementSearch>;
                if (searches == null || searches.Count == 0) return;
                int cap = Math.Max(100, Plugin.PlacementStepCap.Value);
                for (int i = searches.Count - 1; i >= 0; i--)
                {
                    AIBuildingPlacementSearch search = searches[i];
                    if (search.IsComplete || (int)s_searchSteps.GetValue(search) < cap) continue;
                    AICore core = (AICore)s_searchCore.GetValue(search);
                    if (core == null || !core.PlayerData.AIPlayer) continue;
                    searches.RemoveAt(i);
                    Abandon(core, __instance, (BuildingData)s_searchBuilding.GetValue(search));
                }
            }
            catch (Exception e)
            {
                ReportOnce("placement-cap", e);
            }
        }

        private static void Abandon(AICore core, AIBuildingPlacementFinder finder, BuildingData data)
        {
            DirectorState d = StateOf(core.PlayerData);
            string id = data != null ? data.id.Value : "?";
            Increment(d.PlacementFailures, id);
            d.FailedThisTurn.Add(id);
            if ((id == "bui_SolarPlant" || id == "bui_NuclearPlant") && d.PlantsForUnpowered > 0) d.PlantsForUnpowered--; // v0.6.1: lets the water-plant fallback run
            if (data != null && IsWaterOnly(data)) d.WaterCooldownUntil = GameManager.CurrentTurn + 3;
            // Only the construction queue's own searches were paid for from its allotment.
            if (data == null || s_queueFinder.GetValue(core.Construction.ConstructionQueue) != finder) return;
            int energy = Value(data.build_cost_energy), material = Value(data.build_cost_material);
            core.Construction.ConstructionQueue.AllottedEnergy += energy;
            core.Construction.ConstructionQueue.AllottedMaterial += material;
            core.Resources.Energy += energy;
            core.Resources.Material += material;
            if (!(data.distribute_power.IsValueSet && data.distribute_power.Value)) d.SlotsLeft++;
            d.Refunds++;
            d.Reopened = true;
            Record(d, "abandoned:" + id);
            // Undo what the purchase was expected to change.
            PlayerData me = core.PlayerData;
            d.EnergyIncome -= Value(data.turn_yield_energy) * me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldEnergy);
            d.MaterialIncome -= Value(data.turn_yield_material) * me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldMaterial);
            if (data.weapon_functionality.IsValueSet)
            {
                d.Upkeep -= FiringCost(data);
                d.WeaponEnergySpent = Math.Max(0, d.WeaponEnergySpent - Value(data.build_cost_energy));
                EconomyPatches.AiEconomyState economy;
                if (EconomyPatches.States.TryGetValue(me.PlayerID, out economy) && economy.Bought && economy.Goal != null && economy.Goal.Building.id.Value == id)
                {
                    economy.Bought = false; // the savings logic offers it again next turn
                    d.FailedThisTurn.Add("superweapon");
                }
            }
            if (data.defense_functionality.IsValueSet)
            {
                d.DefensesThisTurn = Math.Max(0, d.DefensesThisTurn - 1);
                if (id == d.AntiNukeId) d.AntiNukeThisTurn = Math.Max(0, d.AntiNukeThisTurn - 1);
                EconomyPatches.AiEconomyState economyState;
                int queued;
                if (EconomyPatches.States.TryGetValue(me.PlayerID, out economyState) && economyState.DefensesQueued.TryGetValue(id, out queued))
                {
                    if (queued <= 1) economyState.DefensesQueued.Remove(id);
                    else economyState.DefensesQueued[id] = queued - 1;
                }
            }
            if (Value(data.turn_yield_intel) > 0) d.IntelThisTurn = Math.Max(0, d.IntelThisTurn - 1);
        }

        // ---- logging -------------------------------------------------------------------------------

        internal static void EndTurn(AICore core)
        {
            DirectorState d;
            if (!s_states.TryGetValue(core.PlayerData.PlayerID, out d) || !d.Started) return;
            d.EndEnergyLeft = core.Resources.Energy;
            int upgradesInstalled = 0;
            foreach (BaseBuilding building in core.PlayerData.BuildingHandler.Buildings)
            {
                if (building == null || building.IsCity || building.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                upgradesInstalled += 2 - building.Upgrades.FreeSlots;
            }
            Plugin.WriteDecision(new Json().Str("e", "director").Int("t", d.Turn).Str("ai", core.PlayerData.UserNickName)
                .Bool("war", d.War).Int("slotsAtStart", d.SlotsAtStart).Int("slotsLeft", d.SlotsLeft).Int("unpowered", d.Unpowered)
                .Int("energyStart", d.TurnStartEnergy).Int("materialStart", d.TurnStartMaterial)
                .Int("energyLeft", core.Resources.Energy).Int("materialLeft", core.Resources.Material)
                .Num("upkeep", d.Upkeep).Int("ownDefenses", d.OwnDefenses).Int("desiredDefenses", d.DesiredDefenses).Int("defenseTurnCap", d.DefenseTurnCap)
                .Int("lastImpacts", d.LastImpacts).Int("lastIntercepted", d.LastIntercepted).Int("lastNoEligibleDefense", d.LastNoEligible).Int("lastNearCity", d.LastNearCity).Int("lastNoDefenseNearby", d.LastNoDefenseNearby)
                .Num("coverageKinetic", d.CoverageShare).Bool("nukeThreat", d.NukeThreat).Num("scienceWeight", d.ScienceWeight).Num("planScienceLeft", d.RemainingPlanScience).Int("weaponUpgrades", d.WeaponUpgradesThisTurn).Int("antiNukeMissing", d.AntiNukeWanted).Int("antiNukeOwned", d.AntiNukeOwned).Int("antiNukeQueued", d.AntiNukeThisTurn).Int("citiesProtected", d.CitiesProtected).Int("citiesCovered", d.CitiesCovered).Int("cities", d.CitiesTotal)
                .Int("lastUnpaidRolls", d.LastUnpaid).Int("lastDefenseEnergy", d.LastEnergySpent).Int("observedNeed", (int)d.ObservedDefenseNeed)
                .Str("observedPass", ObservedPassText(d.ObservedPass))
                .Int("upgradesInstalled", upgradesInstalled).Str("weaponTarget", d.TargetName).Map("targetDefenses", d.TargetDefenses)
                .Int("intelBuildings", d.IntelBuildings + d.IntelThisTurn).Int("blindEnemies", d.BlindEnemies).Int("defenseReserve", d.DefenseReserve)
                .Bool("holdingFire", d.HoldingFire).Int("releasedEnergy", d.ReleasedEnergy).Bool("materialGlut", d.MaterialGlut).Bool("energyGlut", d.EnergyGlut)
                .Int("refunds", d.Refunds).Map("placementFailures", d.PlacementFailures).Int("steps", d.Steps)
                .Map("queued", d.Actions).ToString());
        }

        // ---- helpers -------------------------------------------------------------------------------

        private static void Record(DirectorState d, string action)
        {
            Increment(d.Actions, action);
        }

        private static int QueueCount(AIConstruction construction)
        {
            System.Collections.ICollection list = s_queueList.GetValue(construction.ConstructionQueue) as System.Collections.ICollection;
            return list != null ? list.Count : 0;
        }

        private static int Value(GameDataValue<int> value)
        {
            return value != null && value.IsValueSet ? value.Value : 0;
        }

        private static float FloatValue(GameDataValue<float> value, float fallback)
        {
            return value != null && value.IsValueSet ? value.Value : fallback;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            int n;
            counts.TryGetValue(key, out n);
            counts[key] = n + 1;
        }

        private static void ReportOnce(string hook, Exception e)
        {
            if (s_reportedErrors.Add(hook))
            {
                Plugin.Log.LogError("Hook '" + hook + "' failed; native behaviour used for it (reported once): " + e);
            }
        }
    }
}
