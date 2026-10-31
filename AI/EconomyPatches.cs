using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interplanetary.AI;
using Interplanetary.AI.Evaluation;
using Interplanetary.GameDataFiles;
using Interplanetary.GameEngine;
using Interplanetary.GameSession;
using Interplanetary.PlayerDataManagement;
using Interplanetary.Prefabs;
using InterplanetaryAI.Brain;

namespace InterplanetaryAI
{
    // v0.2 hooks: saving for and buying superweapons, the construction mix, and the research plan.
    internal static class EconomyPatches
    {
        internal sealed class AiEconomyState
        {
            public int ReservedEnergy;
            public int ReservedMaterial;
            public AIBuildingEntity Goal;
            public int GoalEnergyCost;
            public int GoalMaterialCost;
            public bool BuyPending;
            public int BuyAttempts;
            public bool Bought;
            public bool NeedSlotForGoal;  // goal affordable but no free power slot: build one generator, buy next turn
            public bool SlotAttempted;
            public bool EnergyPush;       // superweapon material saved but usable energy short: build power
            public int Bank;              // v0.4: material banked for the superweapon line before the planet runs dry
            public float[] Weights;
            public bool Growth;
            public readonly int[] Picks = new int[5];
            public readonly int[] PickSuccesses = new int[5];
            public readonly Dictionary<string, int> EnemyWeapons = new Dictionary<string, int>();   // known: visible now or remembered (v0.5)
            public readonly Dictionary<string, int> VisibleWeapons = new Dictionary<string, int>(); // visible this turn, all enemies
            public readonly Dictionary<int, Dictionary<string, int>> VisibleByEnemy = new Dictionary<int, Dictionary<string, int>>();
            public readonly Dictionary<string, float> WeaponMemory = new Dictionary<string, float>();
            public readonly Dictionary<string, int> OwnDefenses = new Dictionary<string, int>();
            public readonly Dictionary<string, int> DefensesQueued = new Dictionary<string, int>();
        }

        internal static readonly Dictionary<int, AiEconomyState> States = new Dictionary<int, AiEconomyState>();

        private static readonly FieldInfo s_moduleCore = AccessTools.Field(typeof(AIModule), "m_core");
        private static readonly FieldInfo s_queueList = AccessTools.Field(typeof(AIConstructionQueue), "m_constructionQueue");
        private static readonly FieldInfo[] s_categoryModules =
        {
            AccessTools.Field(typeof(AIConstruction), "m_weaponConstruction"),
            AccessTools.Field(typeof(AIConstruction), "m_energyConstruction"),
            AccessTools.Field(typeof(AIConstruction), "m_materialConstruction"),
            AccessTools.Field(typeof(AIConstruction), "m_defenseConstruction"),
            AccessTools.Field(typeof(AIConstruction), "m_counterintelConstruction"),
        };
        private static readonly MethodInfo s_tryBuildAffordable = AccessTools.Method(typeof(AIConstructionModule), "tryBuildAffordableBuilding");
        private static readonly MethodInfo s_tryPowerGenerator = AccessTools.Method(typeof(AIConstruction), "tryConstructPowerGeneratingBuilding");
        private static readonly FieldInfo s_intelModule = AccessTools.Field(typeof(AIConstruction), "m_intelConstruction");
        private static readonly MethodInfo s_moreIntelTechRequired = AccessTools.PropertyGetter(typeof(AIShortTermStrategy), "IsMoreIntelTechRequired");

        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();

        private static AiEconomyState StateOf(PlayerData ai)
        {
            AiEconomyState state;
            if (!States.TryGetValue(ai.PlayerID, out state))
            {
                state = new AiEconomyState();
                States[ai.PlayerID] = state;
            }
            return state;
        }

        // ---- turn start: savings goal, reserve, construction weights, economy log ----------------

        // Runs after every module's BeginTurn (AICore.BeginTurn calls Resources.BeginTurn, then each module),
        // so the reserve lands after the construction queue took its allotment and before any construction.
        private static void CoreBeginTurnPostfix(AICore __instance)
        {
            if (!Plugin.Active || !__instance.PlayerData.AIPlayer) return;
            try
            {
                AICore core = __instance;
                PlayerData me = core.PlayerData;
                AiEconomyState state = StateOf(me);
                int turn = GameManager.CurrentTurn;

                // Enemy picture for the construction mix (same visible-only data as targeting).
                float maxEnemyEconomy = 0f, recentDamage = 0f;
                state.VisibleWeapons.Clear();
                state.VisibleByEnemy.Clear();
                List<int> alive = new List<int>();
                foreach (CelestialBody planet in core.EnemyPlanets)
                {
                    EnemyView view = AiPatches.BuildEnemyView(me, planet.Player, turn);
                    maxEnemyEconomy = Math.Max(maxEnemyEconomy, view.Economy);
                    recentDamage += view.Grudge;
                    alive.Add(planet.Player.PlayerID);
                    Dictionary<string, int> seen = new Dictionary<string, int>();
                    state.VisibleByEnemy[planet.Player.PlayerID] = seen;
                    foreach (BaseBuilding building in planet.Player.BuildingHandler.Buildings)
                    {
                        if (!AiPatches.IsVisibleIntact(me, building) || building.IsCity || !building.BuildingData.weapon_functionality.IsValueSet) continue;
                        Increment(seen, building.BuildingData.weapon_functionality.Value);
                        Increment(state.VisibleWeapons, building.BuildingData.weapon_functionality.Value);
                    }
                }
                // Known weapons = visible now or remembered from earlier sight, per living enemy (fair: our own
                // past observations; eliminations are public).
                DefensePlanner.Remember(state.WeaponMemory, state.VisibleByEnemy, alive, Math.Max(0f, Math.Min(1f, Plugin.WeaponMemoryDecay.Value)), state.EnemyWeapons);
                state.OwnDefenses.Clear();
                state.DefensesQueued.Clear();
                foreach (BaseBuilding building in me.BuildingHandler.Buildings)
                {
                    if (building == null || building.IsCity || building.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                    if (DefensePlanner.Counters.ContainsKey(building.BuildingData.id.Value)) Increment(state.OwnDefenses, building.BuildingData.id.Value);
                }
                float ownEconomy = me.ResourceHandler.EnergyProductionPerTurn + me.ResourceHandler.MaterialProductionPerTurn + me.ResourceHandler.ScienceProductionPerTurn;
                BuildSituation situation = new BuildSituation { Turn = turn, OwnEconomy = ownEconomy, MaxEnemyEconomy = maxEnemyEconomy, RecentDamage = recentDamage };
                state.Growth = BuildPlanner.IsGrowthPhase(situation, Plugin.Build);
                state.Weights = BuildPlanner.Weights(situation, Plugin.Build);
                for (int i = 0; i < 5; i++) { state.Picks[i] = 0; state.PickSuccesses[i] = 0; }

                // Superweapon goal.
                state.BuyPending = false;
                state.BuyAttempts = 0;
                state.Bought = false;
                state.NeedSlotForGoal = false;
                state.SlotAttempted = false;
                state.EnergyPush = false;
                int stockEnergy = core.Resources.Energy, stockMaterial = core.Resources.Material;
                int reserveEnergy = 0, reserveMaterial = 0;
                state.Goal = Plugin.SuperweaponSavings.Value ? FindSuperweaponGoal(core) : null;
                if (state.Goal != null)
                {
                    state.GoalEnergyCost = AIWeaponConstruction.GetWeaponEnergyCost(state.Goal, core);
                    state.GoalMaterialCost = AIWeaponConstruction.GetWeaponMaterialCost(state.Goal, core);
                    bool needsSlot = !(state.Goal.Building.distribute_power.IsValueSet && state.Goal.Building.distribute_power.Value);
                    bool slotFree = core.Construction.PowerSlotNeed < 0; // AIConstruction.BeginTurn: negative = free slots
                    bool affordable = Savings.CanBuy(stockEnergy, stockMaterial, state.GoalEnergyCost, state.GoalMaterialCost);
                    if (affordable && (slotFree || !needsSlot))
                    {
                        state.BuyPending = true; // buy first thing in construction; nothing reserved
                    }
                    else if (affordable)
                    {
                        // Affordable but no free power slot. Buying now would raise PowerSlotNeed, and the native need
                        // branch never lowers it within a turn, so the savings would go on power plants
                        // (AIConstruction.tryConstructPowerGeneratingBuilding). Keep the goal reserved, build one
                        // generator from what is left, and buy next turn when the slot exists.
                        state.NeedSlotForGoal = true;
                        reserveMaterial = Math.Min(stockMaterial, state.GoalMaterialCost);
                        core.Resources.Material -= reserveMaterial;
                        core.Construction.ConstructionQueue.AllottedMaterial -= reserveMaterial;
                    }
                    else
                    {
                        // Only material is saved. Energy does not carry over (PlayerResourceHandler.TurnStartInit sets
                        // Energy = EnergyProductionPerTurn every turn), so an energy reserve only starves construction:
                        // in the 31 Oct match it froze all four AIs' building from turn 25 to the end.
                        reserveMaterial = Savings.NextReserve(state.ReservedMaterial, stockMaterial, state.GoalMaterialCost, Plugin.SaveShare.Value);
                        core.Resources.Material -= reserveMaterial;
                        core.Construction.ConstructionQueue.AllottedMaterial -= reserveMaterial;
                        // Material is ready but this turn's usable energy (income minus weapon firing upkeep) is short:
                        // the fix is more energy income, so tilt construction toward power.
                        if (reserveMaterial >= state.GoalMaterialCost && stockEnergy < state.GoalEnergyCost)
                        {
                            state.Weights[(int)BuildCategory.Energy] *= 3f;
                            state.EnergyPush = true;
                        }
                    }
                }
                // v0.4 endgame bank: before a superweapon is available, keep material back once the planet is
                // about to run dry (after that only the bank is left to buy it with).
                state.Bank = 0;
                if (Plugin.DirectorEnabled.Value && Plugin.SuperweaponSavings.Value && state.Goal == null && GameSessionManager.CurrentGameSettings.PlanetMaterial != -1
                    && SuperweaponResearchQueued(me))
                {
                    PlayerResourceHandler res = me.ResourceHandler;
                    float drain = Math.Max(res.PlanetMaterialCost, res.MaterialProductionPerTurn);
                    // Never more than half the stock: the AI keeps an operating budget while it waits.
                    state.Bank = EconomyDirector.EndgameBank(res.PlanetMaterial, drain, Plugin.BankWarningTurns.Value, Plugin.EndgameBankMaterial.Value, Math.Max(0, core.Resources.Material) / 2);
                    core.Resources.Material -= state.Bank;
                    core.Construction.ConstructionQueue.AllottedMaterial -= state.Bank;
                }
                state.ReservedEnergy = reserveEnergy;
                state.ReservedMaterial = reserveMaterial;

                LogEconomy(core, turn, state, stockEnergy, stockMaterial, ownEconomy, maxEnemyEconomy, recentDamage);
                if (Plugin.DirectorEnabled.Value) DirectorPatches.BeginTurn(core, state, recentDamage, turn);
            }
            catch (Exception e)
            {
                ReportOnce("economy-begin-turn", e);
            }
        }

        // True while research toward a not-yet-available superweapon is queued: the bank waits only for a
        // weapon that is actually coming (a disabled, capped or unreachable one never releases it otherwise).
        private static bool SuperweaponResearchQueued(PlayerData me)
        {
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            PlayerTechnologyHandler tech = me.TechnologyHandler;
            foreach (string id in Plugin.SuperweaponIds)
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data == null || me.BuildingHandler.AvailableBuildings.IsBuildingAvailable(data)) continue;
                List<GameData_TechData> unlocks = tech.GetBuildingUnlockRequirementList(id);
                if (unlocks == null) continue;
                foreach (GameData_TechData t in unlocks)
                {
                    if (t != null && tech.GetTechnologyIsInQueue(t.id.Value)) return true;
                }
            }
            return false;
        }

        private static AIBuildingEntity FindSuperweaponGoal(AICore core)
        {
            AIFirepowerEvaluation firepower = core.ConstructionEvaluation.FirepowerEvaluation;
            AIBuildingEntity best = null;
            int bestCost = int.MaxValue;
            foreach (string buildingId in Plugin.SuperweaponIds)
            {
                foreach (AIConstructionEntity entity in firepower.AvailableFirepowerEntitiesByEfficiency)
                {
                    AIBuildingEntity building = entity as AIBuildingEntity;
                    if (building == null || building.Building.id.Value != buildingId) continue;
                    if (!core.Construction.IsEntityAvailable(building)) continue;
                    int cost = AIWeaponConstruction.GetWeaponEnergyCost(building, core) + AIWeaponConstruction.GetWeaponMaterialCost(building, core);
                    if (cost < bestCost)
                    {
                        best = building;
                        bestCost = cost;
                    }
                }
                if (best != null) return best; // list order = priority
            }
            return null;
        }

        // ---- construction: buy the superweapon first, then the planned category mix ----------

        private static bool TryConstructPrefix(AIConstruction __instance, ref bool __result)
        {
            if (!Plugin.Active) return true;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                if (core == null || !core.PlayerData.AIPlayer) return true;
                AiEconomyState state = StateOf(core.PlayerData);
                if (Plugin.DirectorEnabled.Value) return DirectorPatches.TryConstruct(__instance, core, state, ref __result);

                // v0.2 path (director off). Needs (power slots, energy/material shortfalls) stay native.
                if (core.PowerGrid.BuildingsRequiringPowerAmount > 0 || __instance.PowerSlotNeed > 0 || __instance.EnergyNeed > 0 || __instance.MaterialNeed > 0) return true;

                if (state.BuyPending && !state.Bought && state.Goal != null && state.BuyAttempts < 4 && __instance.ConstructionQueue.IsReady)
                {
                    if (TryBuyGoal(core, __instance, state, ref __result)) return false;
                }
                if (state.NeedSlotForGoal && !state.SlotAttempted)
                {
                    state.SlotAttempted = true;
                    if ((bool)s_tryPowerGenerator.Invoke(__instance, null))
                    {
                        __result = true;
                        return false;
                    }
                }

                if (!Plugin.BuildPlannerEnabled.Value || state.Weights == null) return true;
                // Keep the native intel minimum: let native run when it is short of minimum intel.
                bool intelAvailable = core.ConstructionEvaluation.IntelEvaluation.AvailableIntelEntitiesByEfficiency.Count > 0 && core.Intel.IntelNeed > 0f;
                if (intelAvailable && !core.Intel.HasMinimumIntel) return true;
                // Keep the native preferred-intel weight (6 in AIConstruction.tryConstructNewBuilding) alongside the plan.
                if (intelAvailable && !core.Intel.HasPreferredIntel)
                {
                    float total = 0f;
                    foreach (float w in state.Weights) total += w;
                    if (Plugin.Rng.NextDouble() < 6f / (total + 6f))
                    {
                        AIIntelConstruction intelModule = (AIIntelConstruction)s_intelModule.GetValue(__instance);
                        if (intelModule.TryConstructIntel())
                        {
                            __result = true;
                            return false;
                        }
                    }
                }

                BuildCategory category = BuildPlanner.Pick(state.Weights, Plugin.Rng);
                state.Picks[(int)category]++;
                if (TryCategory(__instance, category))
                {
                    state.PickSuccesses[(int)category]++;
                    __result = true;
                    return false;
                }
                // A failed module attempt may have raised energy/material needs and marked entities attempted.
                // Both are kept, as if native had rolled this category: native then takes its need branch.
                return true;
            }
            catch (Exception e)
            {
                ReportOnce("construction", e);
                return true;
            }
        }

        private static bool TryBuyGoal(AICore core, AIConstruction construction, AiEconomyState state, ref bool result)
        {
            state.BuyAttempts++;
            if (!construction.IsEntityAvailable(state.Goal))
            {
                state.BuyPending = false;
                return false;
            }
            if (!Savings.CanBuy(core.Resources.Energy, core.Resources.Material, state.GoalEnergyCost, state.GoalMaterialCost))
            {
                state.BuyPending = false;
                return false;
            }
            bool needsSlot = !(state.Goal.Building.distribute_power.IsValueSet && state.Goal.Building.distribute_power.Value);
            if (needsSlot && construction.PowerSlotNeed >= 0)
            {
                // The free slot was used earlier this turn; buying now would start the power-plant burst. Retry next turn.
                state.BuyPending = false;
                return false;
            }
            object weaponModule = s_categoryModules[(int)BuildCategory.Firepower].GetValue(construction);
            int queuedBefore = QueueCount(construction);
            // Native path: queues the building if a power slot is free, otherwise raises the power-slot need
            // so the next calls build power first; the purchase is retried on a later call.
            s_tryBuildAffordable.Invoke(weaponModule, new object[] { state.Goal });
            if (QueueCount(construction) > queuedBefore)
            {
                state.Bought = true;
                state.BuyPending = false;
                state.ReservedEnergy = 0;
                state.ReservedMaterial = 0;
                Plugin.WriteDecision(new Json().Str("e", "superweapon").Int("t", GameManager.CurrentTurn).Str("ai", core.PlayerData.UserNickName)
                    .Str("building", state.Goal.Building.id.Value).Int("energyCost", state.GoalEnergyCost).Int("materialCost", state.GoalMaterialCost).ToString());
                Plugin.Log.LogInfo("T" + GameManager.CurrentTurn + " " + core.PlayerData.UserNickName + " queued superweapon " + state.Goal.Building.id.Value);
            }
            result = true;
            return true;
        }

        private static bool TryCategory(AIConstruction construction, BuildCategory category)
        {
            object module = s_categoryModules[(int)category].GetValue(construction);
            switch (category)
            {
                case BuildCategory.Firepower: return ((AIWeaponConstruction)module).TryConstructWeapon();
                case BuildCategory.Energy: return ((AIEnergyConstruction)module).TryConstructEnergy();
                case BuildCategory.Material: return ((AIMaterialConstruction)module).TryConstructMaterial();
                case BuildCategory.Defense: return ((AIDefenseConstruction)module).TryConstructDefense();
                default: return ((AICounterintelConstruction)module).TryConstructCounterintel();
            }
        }

        // ---- defense choice by visible threat -------------------------------------------------

        private static readonly FieldInfo s_constructionModuleCore = AccessTools.Field(typeof(AIConstructionModule), "m_core");

        private static bool TryConstructDefensePrefix(AIDefenseConstruction __instance, ref bool __result)
        {
            if (!Plugin.Active || !Plugin.SmartDefense.Value) return true;
            try
            {
                AICore core = (AICore)s_constructionModuleCore.GetValue(__instance);
                if (core == null || !core.PlayerData.AIPlayer) return true;
                AiEconomyState state = StateOf(core.PlayerData);

                // Cheapest affordable, available variant of each threat-relevant defense building.
                Dictionary<string, AIBuildingEntity> options = new Dictionary<string, AIBuildingEntity>();
                Dictionary<string, int> optionCost = new Dictionary<string, int>();
                foreach (AIConstructionEntity entity in core.ConstructionEvaluation.DefenseEvaluation.AvailableDefenseEntitiesByEfficiency)
                {
                    AIBuildingEntity building = entity as AIBuildingEntity;
                    if (building == null) continue;
                    string id = building.Building.id.Value;
                    if (!DefensePlanner.Counters.ContainsKey(id) || !core.Construction.IsEntityAvailable(building)) continue;
                    int energy = AIConstructionModule.GetEnergyCost(building, core), material = AIConstructionModule.GetMaterialCost(building, core);
                    if (core.Resources.Energy < energy || core.Resources.Material < material) continue;
                    int previous;
                    if (!optionCost.TryGetValue(id, out previous) || energy + material < previous)
                    {
                        options[id] = building;
                        optionCost[id] = energy + material;
                    }
                }
                // Count this turn's queued defenses as owned so one turn spreads across threats.
                Dictionary<string, int> owned = new Dictionary<string, int>(state.OwnDefenses);
                foreach (KeyValuePair<string, int> pair in state.DefensesQueued)
                {
                    int n;
                    owned.TryGetValue(pair.Key, out n);
                    owned[pair.Key] = n + pair.Value;
                }
                string choice = DefensePlanner.Choose(state.EnemyWeapons, owned, options.Keys);
                if (choice == null) return true; // no visible threat it counters: native choice

                int queuedBefore = QueueCount(core.Construction);
                s_tryBuildAffordable.Invoke(__instance, new object[] { options[choice] });
                // No free power slot: native raises PowerSlotNeed instead of queueing; count only real queue additions.
                if (QueueCount(core.Construction) > queuedBefore) Increment(state.DefensesQueued, choice);
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                ReportOnce("defense", e);
                return true;
            }
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            int n;
            counts.TryGetValue(key, out n);
            counts[key] = n + 1;
        }

        private static int QueueCount(AIConstruction construction)
        {
            System.Collections.ICollection list = s_queueList.GetValue(construction.ConstructionQueue) as System.Collections.ICollection;
            return list != null ? list.Count : 0;
        }

        // ---- research plan ------------------------------------------------------------------

        // Runs before the native AITechQueue.BeginTurn, which only queues when the queue is empty, so a
        // non-empty planned queue wins and the native random-focus research resumes once the plan is done.
        private static void TechQueueBeginTurnPrefix(AITechQueue __instance)
        {
            if (!Plugin.Active || !Plugin.ResearchPlanEnabled.Value || Plugin.ResearchPlanIds.Count == 0) return;
            try
            {
                AICore core = (AICore)s_moduleCore.GetValue(__instance);
                if (core == null || !core.PlayerData.AIPlayer) return;
                if (GameSessionManager.CurrentGameSettings.TechTree != 1) return;
                PlayerTechnologyHandler tech = core.PlayerData.TechnologyHandler;
                if (tech.IsTechQueued) return;
                // Native urgent intel research (AIShortTermStrategy.IsMoreIntelTechRequired) goes first at queue boundaries.
                if ((bool)s_moreIntelTechRequired.Invoke(core.ShortTermStrategy, null)) return;

                List<string> before = tech.GetTechnologyQueueIdStringList();
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    string next = ResearchPlan.NextTarget(Plugin.ResearchPlanIds, id => SafeCompleted(tech, id), id => tech.GetTechnologyIsInQueue(id) || s_refused.Contains(id));
                    if (next == null) return;
                    bool queued = false;
                    try { queued = tech.AddTechnologyToQueueById(next, true); }
                    catch (Exception) { queued = false; }
                    // Science is spent on the queue head without an availability check; never leave an
                    // unresearchable head (the director learned this the hard way).
                    List<string> after = tech.GetTechnologyQueueIdStringList();
                    if (queued && after.Count > 0 && tech.IsTechnologyAvailable(after[0]))
                    {
                        Plugin.WriteDecision(new Json().Str("e", "research").Int("t", GameManager.CurrentTurn).Str("ai", core.PlayerData.UserNickName)
                            .Str("target", next).Str("head", after[0]).Int("queueLength", after.Count).ToString());
                        return;
                    }
                    tech.SetTechnologyQueue(before.ToArray());
                    s_refused.Add(next);
                    Plugin.Log.LogWarning("Research plan: " + next + " could not be queued for " + core.PlayerData.UserNickName + "; skipping it.");
                }
            }
            catch (Exception e)
            {
                ReportOnce("research", e);
            }
        }

        private static readonly HashSet<string> s_refused = new HashSet<string>();

        private static bool SafeCompleted(PlayerTechnologyHandler tech, string id)
        {
            try { return tech.GetTechnologyCompletionById(id); }
            catch (Exception) { s_refused.Add(id); return true; }
        }

        internal static void Reset()
        {
            States.Clear();
            s_refused.Clear();
            DirectorPatches.Reset();
        }

        // ---- logging ----------------------------------------------------------------------------

        // End of the AI's turn: what the construction mix actually did this turn.
        private static void CoreEndTurnPostfix(AICore __instance)
        {
            if (!Plugin.Active || !__instance.PlayerData.AIPlayer) return;
            try
            {
                AiEconomyState state;
                if (!States.TryGetValue(__instance.PlayerData.PlayerID, out state)) return;
                if (Plugin.DirectorEnabled.Value) DirectorPatches.EndTurn(__instance);
                Plugin.WriteDecision(new Json().Str("e", "build").Int("t", GameManager.CurrentTurn).Str("ai", __instance.PlayerData.UserNickName)
                    .Bool("growth", state.Growth)
                    .Raw("picks", IntArray(state.Picks)).Raw("queued", IntArray(state.PickSuccesses))
                    .Bool("superweaponBought", state.Bought)
                    .Map("enemyWeapons", state.EnemyWeapons).Map("visibleWeapons", state.VisibleWeapons).Map("ownDefenses", state.OwnDefenses).Map("defensesQueued", state.DefensesQueued).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("economy-end-turn", e);
            }
        }

        private static void LogEconomy(AICore core, int turn, AiEconomyState state, int stockEnergy, int stockMaterial, float ownEconomy, float maxEnemyEconomy, float recentDamage)
        {
            PlayerData me = core.PlayerData;
            Dictionary<string, int> buildings = new Dictionary<string, int>();
            foreach (BaseBuilding building in me.BuildingHandler.Buildings)
            {
                if (building == null || building.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                string id = building.IsCity ? "city" : building.BuildingData.id.Value.Replace("bui_", "");
                int n;
                buildings.TryGetValue(id, out n);
                buildings[id] = n + 1;
            }
            List<string> queue = me.TechnologyHandler.GetTechnologyQueueIdStringList();
            Plugin.WriteDecision(new Json().Str("e", "economy").Int("t", turn).Str("ai", me.UserNickName)
                .Int("energy", stockEnergy).Int("material", stockMaterial)
                .Int("energyPerTurn", me.ResourceHandler.EnergyProductionPerTurn).Int("materialPerTurn", me.ResourceHandler.MaterialProductionPerTurn)
                .Int("sciencePerTurn", me.ResourceHandler.ScienceProductionPerTurn).Int("planetMaterial", me.ResourceHandler.PlanetMaterial)
                .Int("population", me.ResourceHandler.TotalPopulation)
                .Num("ownEconomy", ownEconomy).Num("maxEnemyEconomy", maxEnemyEconomy).Num("recentDamage", recentDamage)
                .Bool("growth", state.Growth).Raw("weights", FloatArray(state.Weights))
                .Str("goal", state.Goal != null ? state.Goal.Building.id.Value : "")
                .Int("bank", state.Bank).Int("reservedEnergy", state.ReservedEnergy).Int("reservedMaterial", state.ReservedMaterial).Bool("buyPending", state.BuyPending)
                .Str("researchHead", queue.Count > 0 ? queue[0] : "")
                .Map("buildings", buildings).ToString());
        }

        private static string IntArray(int[] values)
        {
            return "[" + string.Join(",", Array.ConvertAll(values, v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
        }

        private static string FloatArray(float[] values)
        {
            if (values == null) return "[]";
            return "[" + string.Join(",", Array.ConvertAll(values, v => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))) + "]";
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
