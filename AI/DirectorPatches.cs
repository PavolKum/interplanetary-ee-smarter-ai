using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Construction;
using Interplanetary.GameDataFiles;
using Interplanetary.GameEngine;
using Interplanetary.GameSession;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;
using InterplanetaryAI.Brain;
using TJR_Engine.FileManagement;

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
            public int LastImpacts, LastIntercepted, LastUncovered, LastNoEnergy, LastEnergySpent;
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
            d = new DirectorState { WaterCooldownUntil = waterCooldown, Turn = turn };
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
            int visibleEnemyWeapons = 0;
            foreach (int n in economy.EnemyWeapons.Values) visibleEnemyWeapons += n;
            d.VisibleEnemyWeapons = visibleEnemyWeapons;
            d.EnemiesAlive = core.EnemyPlanets.Count;
            foreach (int n in economy.OwnDefenses.Values) d.OwnDefenses += n;
            DefenderStats last = Plugin.Ledger.LastDefender(me.PlayerID);
            d.LastImpacts = last.Impacts;
            d.LastIntercepted = last.Intercepted;
            d.LastUncovered = last.Uncovered;
            d.LastNoEnergy = last.NoEnergy;
            d.LastEnergySpent = last.EnergySpent;
            d.ObservedDefenseNeed = EconomyDirector.ObservedDefenseNeed(last.EnergySpent, last.NoEnergy, 100f, Plugin.DefenseObservedMargin.Value);
            d.ObservedPass = Plugin.ObservedMinImpacts.Value > 0 ? Plugin.Ledger.ObservedPassAll(me.PlayerID, Plugin.ObservedMinImpacts.Value) : new Dictionary<string, float>();
            d.DesiredDefenses = EconomyDirector.DesiredDefenses(visibleEnemyWeapons, recentDamage, Plugin.DefensePerEnemyWeapon.Value, Plugin.DamagePerDefense.Value, Plugin.MaxDefenses.Value,
                d.War, d.NonCityBuildings, Plugin.DefenseFloorShare.Value);

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
            // v0.4: at war, keep energy back for our defense rolls (each costs the defender 100-200 energy from
            // what is left that turn; PlayerDefenseHandler.IsHitIntercepted). v0.3 spent it all.
            if (d.War)
            {
                int reserve = EconomyDirector.DefenseReserve(d.OwnDefenses, d.VisibleEnemyWeapons, d.EnemiesAlive, d.RecentDamage,
                    150f, 125f, Plugin.DefenseReserveShare.Value, d.EnergyIncome, 1f, d.ObservedDefenseNeed, Plugin.DefenseHardCapShare.Value);
                reserve = Math.Max(0, Math.Min(reserve, core.Resources.Energy));
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
                ScienceWeight = Plugin.ScienceWeight.Value,
            };
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
            // Identical options (same building type, same installed upgrades) are listed once.
            HashSet<string> seen = new HashSet<string>();
            foreach (BaseBuilding building in me.BuildingHandler.Buildings)
            {
                if (building == null || building.IsCity || building.CurrentState != BaseBuilding.BuildingState.Normal || building.BuildingData == null) continue;
                BuildingData data = building.BuildingData;
                int baseEnergy = Value(data.turn_yield_energy), baseMaterial = Value(data.turn_yield_material);
                if (baseEnergy <= 0 && baseMaterial <= 0) continue;
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
                    if (energyGain <= 0f && materialGain <= 0f) continue;
                    if (!seen.Add(data.id.Value + "|" + installed + "|" + upgrade.id.Value + "|" + planned)) continue;
                    options.Add(new EconOption
                    {
                        Id = upgrade.id.Value,
                        Kind = OptionKind.Upgrade,
                        Energy = Value(upgrade.build_cost_energy),
                        Material = Value(upgrade.build_cost_material),
                        EnergyGain = energyGain,
                        MaterialGain = materialGain,
                        SavesReserve = savesReserve,
                        Payload = upgrade,
                        Target = building,
                    });
                }
            }
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
                float scienceGain = res.ScienceProductionPerTurn / (float)cities * (FloatValue(p.turn_yield_science_multiplier, 1f) - 1f)
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
            bool needsSlot = !(data.distribute_power.IsValueSet && data.distribute_power.Value);
            if (needsSlot && d.SlotsLeft <= 0) return false;
            construction.ConstructionQueue.AddBuildingToQueue(data); // charges core.Resources
            if (needsSlot)
            {
                d.SlotsLeft--;
                construction.PowerSlotNeed++; // keep the native bookkeeping consistent
            }
            Record(d, role + ":" + data.id.Value);
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
        private static bool SearchStepPrefix(AIBuildingPlacementSearch __instance)
        {
            if (!Plugin.Active || !Plugin.DirectorEnabled.Value) return true;
            try
            {
                BuildingData data = (BuildingData)s_searchBuilding.GetValue(__instance);
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
                .Int("lastImpacts", d.LastImpacts).Int("lastIntercepted", d.LastIntercepted).Int("lastUncovered", d.LastUncovered)
                .Int("lastNoEnergy", d.LastNoEnergy).Int("lastDefenseEnergy", d.LastEnergySpent).Int("observedNeed", (int)d.ObservedDefenseNeed)
                .Str("observedPass", ObservedPassText(d.ObservedPass))
                .Int("upgradesInstalled", upgradesInstalled).Str("weaponTarget", d.TargetName).Map("targetDefenses", d.TargetDefenses)
                .Int("intelBuildings", d.IntelBuildings + d.IntelThisTurn).Int("blindEnemies", d.BlindEnemies).Int("defenseReserve", d.DefenseReserve)
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
