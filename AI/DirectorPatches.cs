using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Construction;
using Interplanetary.GameDataFiles;
using Interplanetary.GameEngine;
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
            public int IntelAttempts;
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
            public string TargetName = "";
            public readonly Dictionary<string, int> TargetDefenses = new Dictionary<string, int>();
            public readonly Dictionary<BaseBuilding, int> PlannedUpgrades = new Dictionary<BaseBuilding, int>();
            public readonly HashSet<string> PlannedUpgradeKeys = new HashSet<string>();
            public readonly HashSet<string> FailedThisTurn = new HashSet<string>();
            public readonly Dictionary<string, int> Actions = new Dictionary<string, int>();
            public readonly Dictionary<string, int> PlacementFailures = new Dictionary<string, int>();
            public int Refunds;
            public int Calls;
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
        private static readonly FieldInfo s_intelModule = AccessTools.Field(typeof(AIConstruction), "m_intelConstruction");
        private static readonly FieldInfo s_materialModule = AccessTools.Field(typeof(AIConstruction), "m_materialConstruction");
        private static readonly MethodInfo s_tryProject = AccessTools.Method(typeof(AIConstructionModule), "tryBuildAffordableProject");

        private static readonly string[] s_economyBuildings = { "bui_SolarPlant", "bui_FloatingPlant", "bui_NuclearPlant", "bui_Mine", "bui_OffshoreMine" };

        internal static void Reset()
        {
            s_states.Clear();
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
            d.War = EconomyDirector.WarPhase(turn, Plugin.WarFromTurn.Value, recentDamage, Plugin.WarDamageTrigger.Value);
            int visibleEnemyWeapons = 0;
            foreach (int n in economy.EnemyWeapons.Values) visibleEnemyWeapons += n;
            foreach (int n in economy.OwnDefenses.Values) d.OwnDefenses += n;
            d.DesiredDefenses = EconomyDirector.DesiredDefenses(visibleEnemyWeapons, recentDamage, Plugin.DefensePerEnemyWeapon.Value, Plugin.DamagePerDefense.Value, Plugin.MaxDefenses.Value);

            // Weapon choice is made against the strongest visible economy: the enemy most worth hitting.
            float bestEconomy = -1f;
            PlayerData target = null;
            foreach (CelestialBody planet in core.EnemyPlanets)
            {
                EnemyView view = AiPatches.BuildEnemyView(me, planet.Player, turn);
                if (view.Economy > bestEconomy)
                {
                    bestEconomy = view.Economy;
                    target = planet.Player;
                }
            }
            if (target != null)
            {
                d.TargetName = target.UserNickName;
                foreach (BaseBuilding building in target.BuildingHandler.Buildings)
                {
                    if (!AiPatches.IsVisibleIntact(me, building) || building.IsCity) continue;
                    string id = building.BuildingData.id.Value;
                    if (DefensePlanner.Counters.ContainsKey(id)) Increment(d.TargetDefenses, id);
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
                if (!d.Started) StartConstruction(core, d);
                d.Calls++;
                if (d.Calls > 400)
                {
                    // Safety valve: every call queues something, so this means a loop; end the turn.
                    result = false;
                    return false;
                }
                result = Step(construction, core, economy, d);
                return false;
            }
            catch (Exception e)
            {
                ReportOnce("director", e);
                return true; // native for this call
            }
        }

        private static void StartConstruction(AICore core, DirectorState d)
        {
            d.Started = true;
            d.SlotsAtStart = CountFreeSlots(core);
            d.SlotsLeft = d.SlotsAtStart;
            d.Unpowered = core.PowerGrid.BuildingsRequiringPowerAmount;
            d.TurnStartEnergy = core.Resources.Energy;
            d.TurnStartMaterial = core.Resources.Material;
            PlayerResourceHandler res = core.PlayerData.ResourceHandler;
            d.EnergyIncome = res.EnergyProductionPerTurn;
            d.MaterialIncome = res.PlanetMaterial > 0 ? res.MaterialProductionPerTurn : 0;
            d.Upkeep = core.Resources.EnergyUpkeep;
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

        private static bool Step(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, DirectorState d)
        {
            // 1. Superweapon purchase prepared by the savings logic.
            if (economy.BuyPending && !economy.Bought && economy.Goal != null && !d.FailedThisTurn.Contains("superweapon"))
            {
                if (TryBuySuperweapon(construction, core, economy, d)) return true;
                d.FailedThisTurn.Add("superweapon");
            }

            // 2. Power for unpowered buildings (one plant each, at most 2 a turn) or for the superweapon's slot.
            bool needPlant = (d.Unpowered > 0 && d.PlantsForUnpowered < Math.Min(d.Unpowered, 2))
                || (economy.NeedSlotForGoal && d.PlantsForUnpowered == 0);
            if (needPlant && !d.FailedThisTurn.Contains("unpowered-plant"))
            {
                BuildingData plant = PickLandPlant(core, d);
                if (plant != null && QueueBuilding(construction, core, d, plant, "power"))
                {
                    d.PlantsForUnpowered++;
                    return true;
                }
                d.FailedThisTurn.Add("unpowered-plant");
            }

            // 3. Minimum intel: weapons need to see targets.
            IntelEvaluationState intel = IntelState(core);
            if (intel == IntelEvaluationState.BelowMinimum && d.IntelAttempts < 1 && d.SlotsLeft > 0)
            {
                d.IntelAttempts++;
                if (TryIntel(construction, core, d)) return true;
            }

            // 4. Defenses against visible threats.
            if (d.OwnDefenses + d.DefensesThisTurn < d.DesiredDefenses && d.DefensesThisTurn < Plugin.DefensesPerTurn.Value && d.SlotsLeft > 0 && !d.FailedThisTurn.Contains("defense"))
            {
                if (TryDefense(construction, core, economy, d)) return true;
                d.FailedThisTurn.Add("defense");
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
            if (d.War && intel == IntelEvaluationState.BelowPreferred && d.PreferredIntelAttempts < 1 && d.SlotsLeft > 0)
            {
                d.PreferredIntelAttempts++;
                if (TryIntel(construction, core, d)) return true;
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
            if (core.Resources.Energy < economy.GoalEnergyCost || core.Resources.Material < economy.GoalMaterialCost) return false;
            bool needsSlot = !(economy.Goal.Building.distribute_power.IsValueSet && economy.Goal.Building.distribute_power.Value);
            if (needsSlot && d.SlotsLeft <= 0)
            {
                economy.NeedSlotForGoal = true; // build a plant; buy next turn
                return false;
            }
            if (!QueueBuilding(construction, core, d, economy.Goal.Building, "superweapon")) return false;
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

        // The native intel module picks the best intel building or upgrade. A free slot is checked first, so
        // the module never takes its no-slot path (which only raises PowerSlotNeed).
        private static bool TryIntel(AIConstruction construction, AICore core, DirectorState d)
        {
            int before = QueueCount(construction);
            int energyBefore = core.Resources.Energy, materialBefore = core.Resources.Material;
            AIIntelConstruction module = (AIIntelConstruction)s_intelModule.GetValue(construction);
            module.TryConstructIntel();
            if (QueueCount(construction) <= before) return false;
            object last = LastQueued(construction);
            AIQueuedBuildingEntity building = last as AIQueuedBuildingEntity;
            if (building != null)
            {
                d.SlotsLeft--;
                Record(d, "intel:" + building.Building.id.Value);
            }
            else
            {
                AIQueuedUpgradeEntity upgrade = last as AIQueuedUpgradeEntity;
                if (upgrade != null)
                {
                    // Upgrades are not charged to the decision budget by the native queue.
                    core.Resources.Energy -= upgrade.EnergyCost;
                    core.Resources.Material -= upgrade.MaterialCost;
                    Record(d, "intel:" + upgrade.Upgrade.id.Value);
                }
            }
            return core.Resources.Energy != energyBefore || core.Resources.Material != materialBefore || building != null;
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
                if (!Affordable(core, data)) continue;
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
                if (!available.IsBuildingAvailable(data) || !Affordable(core, data)) continue;
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
            WeaponStats pick = EconomyDirector.PickWeapon(options, d.TargetDefenses, DefensePlanner.Counters, Plugin.DefensesForFullCover.Value, 4f, 0.6f, Plugin.Rng);
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
            };
            for (int attempt = 0; attempt < 6; attempt++)
            {
                EconOption best = EconomyDirector.Best(options, balance, d.SlotsLeft);
                if (best == null) return false;
                if (Execute(construction, core, d, best))
                {
                    d.EnergyIncome += best.EnergyGain;
                    d.MaterialIncome += best.MaterialGain;
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
                if (!building.Power.HasPower) continue; // unpowered buildings produce nothing
                int planned;
                d.PlannedUpgrades.TryGetValue(building, out planned);
                if (building.Upgrades.FreeSlots - planned <= 0) continue;

                float energyMultiplier = 1f, materialMultiplier = 1f;
                string installed = "";
                foreach (UpgradeData current in building.Upgrades.CurrentUpgrades)
                {
                    if (current == null) continue;
                    energyMultiplier *= FloatValue(current.turn_yield_energy_multiplier, 1f);
                    materialMultiplier *= FloatValue(current.turn_yield_material_multiplier, 1f);
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
                        Payload = upgrade,
                        Target = building,
                    });
                }
            }
        }

        private static void AddProjectOptions(AIConstruction construction, AICore core, DirectorState d, List<EconOption> options)
        {
            PlayerResourceHandler res = core.PlayerData.ResourceHandler;
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
                float span = (p.permanent.IsValueSet && p.permanent.Value) ? 1f : Math.Min(10, Math.Max(0, Value(p.duration))) / 10f;
                float energyGain = Value(p.turn_yield_energy_addition) + res.EnergyProductionPerTurn * (FloatValue(p.turn_yield_energy_multiplier_global, 1f) - 1f);
                float materialGain = Value(p.turn_yield_material_addition) + res.MaterialProductionPerTurn * (FloatValue(p.turn_yield_material_multiplier_global, 1f) - 1f);
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

        // ---- queue primitives ----------------------------------------------------------------------

        private static bool QueueBuilding(AIConstruction construction, AICore core, DirectorState d, BuildingData data, string role)
        {
            if (data == null || !Affordable(core, data)) return false;
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

                bool needsPower = __instance.m_needsPowerGrid;
                List<BaseBuilding> anchors = new List<BaseBuilding>();
                foreach (BaseBuilding building in core.PlayerData.BuildingHandler.Buildings)
                {
                    if (IsWaterAnchor(core, building, true)) anchors.Add(building);
                }
                if (needsPower && anchors.Count == 0) return true; // native: try the land neighbours (coastal luck)

                s_searchSteps.SetValue(__instance, (int)s_searchSteps.GetValue(__instance) + 1);
                if (anchors.Count > 0 && (needsPower || Plugin.Rng.NextDouble() < 0.7))
                {
                    s_placeNextTo.Invoke(__instance, new object[] { anchors });
                }
                else
                {
                    s_placeRandom.Invoke(__instance, null);
                }
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
                .Num("upkeep", d.Upkeep).Int("ownDefenses", d.OwnDefenses).Int("desiredDefenses", d.DesiredDefenses)
                .Int("upgradesInstalled", upgradesInstalled).Str("weaponTarget", d.TargetName).Map("targetDefenses", d.TargetDefenses)
                .Int("refunds", d.Refunds).Map("placementFailures", d.PlacementFailures).Int("calls", d.Calls)
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

        private static object LastQueued(AIConstruction construction)
        {
            System.Collections.IList list = s_queueList.GetValue(construction.ConstructionQueue) as System.Collections.IList;
            return list != null && list.Count > 0 ? list[list.Count - 1] : null;
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
