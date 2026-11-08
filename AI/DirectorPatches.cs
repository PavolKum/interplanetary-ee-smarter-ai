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
using Interplanetary.Weapons;
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
            public bool Funded;           // v0.19 (Terra v0.19b #2): StartConstruction finished the defense reserve and repairs
            public bool Ended;            // v0.16.2: EndTurn recorded EndEnergyLeft / EndMaterialLeft
            public bool War;
            public bool Defensive;        // v0.10: defense floor and reserve from DefenseFromTurn (before weapons)
            public int SlotsAtStart;
            public int SlotsLeft;
            public int Unpowered;
            public int UnpoweredWeapons = -1;   // v0.20 (council I1a): weapons without power at end of turn
            public int SearchesOpenAtEnd = -1;  // v0.20 (council I1a): placement searches cut by the end turn
            public int PlantsForUnpowered;
            public int PreferredIntelAttempts;
            public int DefensesThisTurn;
            public int WeaponsThisTurn;
            public int WeaponEnergySpent;
            public int TurnStartEnergy;
            public int TurnStartMaterial;
            public float EnergyIncome;
            public float MaterialIncome;
            public float ScienceIncome = -1f;     // v0.16 (Sol v0.16 M2): projected science, this turn's queued projects included
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
            public int CounterDefensesThisTurn;
            public int EndEnergyLeft;     // energy unspent when the turn's construction ended
            public int LastEnergyLeft, LastEnergyBudget;
            public readonly Dictionary<string, int> AbandonedLastTurn = new Dictionary<string, int>();
            public readonly Dictionary<string, int> QueuedById = new Dictionary<string, int>();
            // v0.12 material budget and weapon cap
            public float SustainableMaterial = -1f; // planet material per turn to MaterialHorizonTurn (-1 unlimited)
            public int MaterialReserve;             // material the economy may not spend this turn
            public bool EnergySaturated;
            public int SoldMines, SoldMaterial;
            public int Repaired, RepairEnergy, RepairMaterial; // v0.13
            public int PlantsQueued;      // v0.14.1: power-distributing buildings queued this turn
            // v0.16 (council v0.16 N3, Pavol's P2): v0.15's UnseenCities counted the enemy's real surviving cities, a hidden
            // value. Replaced by what we see now: enemy cities currently visible (intact, Visible state).
            public int VisibleEnemyCities;
            // v0.16 item 1: project slots
            public bool LosslessDone;
            public int PermanentProjects;          // permanent city projects installed or queued (all our cities)
            public int JammingActive;              // Electromagnetic Jamming installed or queued
            public int SstarActive;                // SSTAR Reactor Trials installed or queued
            public int RareMaterialActive;         // v0.16.1: Rare Material Synthesis installed or queued
            public bool EmcDone;
            public int ExtractionBlocked;          // v0.16.1: extraction options refused by the planet guard
            public float PlanetDrain;              // v0.16.1: max(planet cost, production), this turn's queued additions included
            public int PlanetPool = -1;            // remaining planet material (-1 unlimited)
            public int JammingEligibleCities;      // living cities without Jamming (one copy per city)
            public int ProjectSlotsFree, ProjectSlotsTotal;
            public string ProjectSlots = "";       // per city "free/total:permanent:temporary"
            public int PermanentsBlocked;          // permanent project options refused by the slot policy this turn
            // v0.16 item 5: intel bid
            public int IntelAllowed;
            public int IntelUpgradesThisTurn;      // Lidar upgrades queued this turn
            public int IntelLidar;                 // intel buildings that have Lidar
            public string IntelPolicy = "";
            // v0.16 item 4/6
            public int WarTurn;
            public bool Blind;                     // no strike candidate visible now on the committed target (Sol v0.16 F6)
            public int TargetCandidates;
            public int ForwardDefenseNeed;
            public int RepairHeld;                 // energy repairs left for the defense reserve this turn
            public int ReserveWanted;              // defense reserve asked for before the recovery cap
            public int DefenseUpgradesThisTurn;
            public int GuidedRefused;              // guided weapons not bought: no city visible to strike
            public bool GuidedThreat;     // v0.15: an enemy guided weapon (Missile, MIRV, Nuke) seen or landed
            public int AntiNukeProtecting; // v0.15: anti-missile defenses that protect at least one city
            public int OwnIntel;
            public int CityProjectsThisTurn;
            public int AntiNukePerCityNow; // v0.15.1: per-city target this turn (placement must close it)
            public int MinCityCover = -1;  // v0.15.1: fewest qualifying powered anti-missile defenses over any city
            public int AntiNukeNearCities; // v0.15.1: intact (any state) anti-missile defenses within reach of a city
            public int PowerEnergySpent;  // v0.13.1: energy spent this turn on energy-only purchases (plants, energy upgrades)
            public int AimedWeapons;                // own weapons needing a trajectory (not lasers, not superweapons), queued included
            // v0.16.2 (matches 18-19): material binds (energy left over, material spent down) -> extraction first
            public int EndMaterialLeft;             // material unspent when the turn's construction ended
            public int LastMaterialLeft, LastMaterialBudget;
            public bool MaterialBound;
            public int ExtractionFirstMaterial, ExtractionFirstBought;
            public float ExtractionCeiling = -1f;   // material income the planet sustains to PlanetGuardHorizonTurn (-1 unlimited)
            public readonly Dictionary<string, int> ExtractionFirstQueued = new Dictionary<string, int>(); // building id -> queued by step 2b
            public int EnergyAtStart;               // energy at construction start, before the defense reserve and repairs
            // across turns
            public int WaterCooldownUntil;
            // v0.14: building type -> last turn it stays throttled (at most two queued a turn) after 3+ placements gave up
            public Dictionary<string, int> BackoffUntil = new Dictionary<string, int>();
        }

        private static readonly Dictionary<int, DirectorState> s_states = new Dictionary<int, DirectorState>();
        private static readonly HashSet<string> s_reportedErrors = new HashSet<string>();

        // v0.16: what one AI carries across turns of a match (DirectorState is rebuilt every turn).
        internal sealed class MatchMemory
        {
            public readonly IntelBidState Intel = new IntelBidState();
            public int CreditedTurn = -1;
            public int WarStartTurn = -1;          // latched once war begins
            public readonly EconomyDirector.RetaliationState Retaliation = new EconomyDirector.RetaliationState(); // v0.20 (council I2)
            public int PeakImpacts;                // largest enemy volley (impacts on us) seen so far
            public float PeakEligibleShare = 1f;   // share of that volley that met a defense in range
        }

        private static readonly Dictionary<int, MatchMemory> s_memory = new Dictionary<int, MatchMemory>();

        internal static MatchMemory MemoryOf(int playerId)
        {
            MatchMemory m;
            if (!s_memory.TryGetValue(playerId, out m))
            {
                m = new MatchMemory();
                s_memory[playerId] = m;
            }
            return m;
        }

        internal const string LosslessTech = "tec_LosslessEnergyTransfer";
        internal const string OrbitalAssemblyTech = "tec_OrbitalAssembly";
        internal const string JammingProject = "pro_ScienceYieldShortGlobal1";
        internal const string SstarProject = "pro_EnergyYieldLongGlobal1";
        internal const string RareMaterialProject = "pro_MaterialYieldLongGlobal1"; // v0.16.1
        internal const string EmcTech = "tec_ExperimentalMatterControl";
        internal const string LidarUpgrade = "upg_RadiotelescopeIntelYield1";

        private static bool TechDone(PlayerData me, string id)
        {
            try { return me.TechnologyHandler.GetTechnologyCompletionById(id); }
            catch (Exception) { return false; }
        }

        // v0.16 item 4: the turn war starts for this AI (latched). Used by the director and the strategy alike.
        internal static int WarTurnFor(PlayerData me, int turn)
        {
            MatchMemory m = MemoryOf(me.PlayerID);
            if (m.WarStartTurn >= 0) return m.WarStartTurn;
            int warTurn = WarStart.Turn(Plugin.WarFromTurn.Value, Plugin.WarFallbackTurn.Value, TechDone(me, OrbitalAssemblyTech),
                me.ResourceHandler.EnergyProductionPerTurn, Plugin.WarMinIncome.Value);
            if (turn >= warTurn) m.WarStartTurn = turn;
            return warTurn;
        }

        // v0.20 (council I2): the war flag shared by the director and the strategy. The retaliation memory lives in the
        // match memory (reset on every start or load), so both callers on a turn get the same answer.
        internal static bool WarFor(PlayerData me, int turn, int warTurn, float recentDamage)
        {
            MatchMemory m = MemoryOf(me.PlayerID);
            return EconomyDirector.WarPhase(turn, warTurn, m.Retaliation, recentDamage, Plugin.WarDamageTrigger.Value,
                Plugin.RetaliationLatchFromTurn.Value, Plugin.RetaliationQuietTurns.Value);
        }

        // v0.20 (council I1a): before AICore.EndTurn (whose construction-queue EndTurn stops every open search), count the
        // placement searches still running: abandoned + placed does not cover them, so they were invisible in v0.19.
        internal static void BeforeEndTurn(AICore core)
        {
            DirectorState d;
            if (core == null || !s_states.TryGetValue(core.PlayerData.PlayerID, out d) || d.Turn != GameManager.CurrentTurn) return;
            try
            {
                object finder = s_queueFinder.GetValue(core.Construction.ConstructionQueue);
                List<AIBuildingPlacementSearch> searches = finder != null ? s_finderSearches.GetValue(finder) as List<AIBuildingPlacementSearch> : null;
                d.SearchesOpenAtEnd = searches != null ? searches.Count : 0;
            }
            catch (Exception e) { ReportOnce("open-searches", e); }
        }

        internal static EconomyDirector.RetaliationState RetaliationOf(PlayerData me)
        {
            return MemoryOf(me.PlayerID).Retaliation;
        }

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
            s_memory.Clear();
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

        // v0.16.2 (free fire, Terra v0.16.2 #3): last turn's actual construction surplus (energy-only purchases are spent, not
        // surplus) and what last turn used from its construction start (defense reserve, repairs, construction). Read at the
        // strategy decision, before this turn's director state replaces it. False = unknown (no state, not the turn just
        // before, or the turn never ended).
        // v0.19: this turn's construction start ran to the end (defense reserve withheld, repairs paid).
        internal static bool FundedThisTurn(int playerId)
        {
            DirectorState d;
            return s_states.TryGetValue(playerId, out d) && d.Turn == GameManager.CurrentTurn && d.Started && d.Funded;
        }

        // v0.19 (Terra v0.19c #1): a late free fire took the firing reservation back after construction started as a hold.
        // Take it out of this turn's construction start too, so next turn's FreeFireInputs sees the same shape as a planned
        // firing turn (whose start never held the reservation) instead of counting the salvo as construction use. An abort
        // after it keeps the reservation for defense rolls, as on a planned firing turn.
        internal static void NoteLateFire(int playerId, int reservation)
        {
            DirectorState d;
            if (reservation > 0 && s_states.TryGetValue(playerId, out d) && d.Turn == GameManager.CurrentTurn && d.Started)
                d.EnergyAtStart = Math.Max(0, d.EnergyAtStart - reservation);
        }

        internal static bool FreeFireInputs(PlayerData me, int turn, out int lastUnspent, out int lastUses)
        {
            lastUnspent = -1;
            lastUses = -1;
            DirectorState d;
            if (me == null || !s_states.TryGetValue(me.PlayerID, out d) || !d.Started || !d.Ended || d.Turn != turn - 1) return false;
            lastUnspent = Math.Max(0, d.EndEnergyLeft);
            lastUses = Math.Max(0, d.EnergyAtStart - lastUnspent);
            return true;
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
            // v0.13.1 (Sol v0.13 #1): energy spent on more energy counts as unspent, else buying plants hides the glut that
            // should stop them (v0.11.1 Beam T24: 6.7k of 27.9k left = 24%, under the 30% threshold, while it bought plants).
            int lastLeft = consecutive ? d.EndEnergyLeft + d.PowerEnergySpent : 0, lastBudget = consecutive ? d.TurnStartEnergy : 0;
            // v0.16.2 (Terra v0.16.2 M1): only from a turn whose EndTurn recorded the material left.
            bool ended = consecutive && d.Started && d.Ended;
            int lastMaterialLeft = ended ? d.EndMaterialLeft : 0, lastMaterialBudget = ended ? d.TurnStartMaterial : 0;
            // A throttled type stays throttled while its placements still fail (a capped turn has at most two failures).
            Dictionary<string, int> lastAbandons = new Dictionary<string, int>();
            if (consecutive)
            {
                foreach (KeyValuePair<string, int> pair in d.PlacementFailures)
                {
                    int before;
                    d.AbandonedLastTurn.TryGetValue(pair.Key, out before);
                    lastAbandons[pair.Key] = before >= 3 && pair.Value >= 1 ? Math.Max(3, pair.Value) : pair.Value;
                }
            }
            Dictionary<string, int> backoff = turn > d.Turn ? NextBackoff(d, Plugin.PlacementBackoffTurns.Value) : new Dictionary<string, int>();
            d = new DirectorState { WaterCooldownUntil = waterCooldown, Turn = turn, LastEnergyLeft = lastLeft, LastEnergyBudget = lastBudget,
                LastMaterialLeft = lastMaterialLeft, LastMaterialBudget = lastMaterialBudget, BackoffUntil = backoff };
            foreach (KeyValuePair<string, int> pair in lastAbandons) d.AbandonedLastTurn[pair.Key] = pair.Value;
            s_states[core.PlayerData.PlayerID] = d;

            PlayerData me = core.PlayerData;
            d.RecentDamage = recentDamage;
            // v0.12 material budget: the planet's pool is fixed (drain = max(production, planet cost)).
            try
            {
                PlayerResourceHandler res = me.ResourceHandler;
                bool limited = GameSessionManager.CurrentGameSettings.PlanetMaterial != -1;
                float drain = Math.Max(res.PlanetMaterialCost, res.MaterialProductionPerTurn);
                d.PlanetDrain = drain;
                d.PlanetPool = limited ? Math.Max(0, res.PlanetMaterial) : -1;
                d.SustainableMaterial = EconomyDirector.SustainableMaterialIncome(res.PlanetMaterial, res.Material, limited, turn, Plugin.MaterialHorizonTurn.Value, 3);
                // v0.16.2 (Terra v0.16.2 M2): extraction first runs to the drain guard's horizon (T36), not MaterialHorizonTurn (T45).
                d.ExtractionCeiling = EconomyDirector.SustainableMaterialIncome(res.PlanetMaterial, res.Material, limited, turn, Plugin.PlanetGuardHorizonTurn.Value, 3);
                d.MaterialReserve = EconomyDirector.EconomyReserve(res.PlanetMaterial, drain, limited, Plugin.BankWarningTurns.Value, Plugin.MaterialReserve.Value);
                int[] sold;
                if (s_soldThisTurn.TryGetValue(me.PlayerID, out sold) && sold[0] == turn) { d.SoldMines = sold[1]; d.SoldMaterial = sold[2]; }
            }
            catch (Exception e)
            {
                ReportOnce("material-budget", e);
            }
            foreach (BaseBuilding building in me.BuildingHandler.Buildings)
            {
                if (building == null || building.IsCity || building.BuildingData == null) continue;
                if (IsAimedWeapon(building.BuildingData) && (building.CurrentState == BaseBuilding.BuildingState.Normal
                    || building.CurrentState == BaseBuilding.BuildingState.UnderConstruction)) d.AimedWeapons++;
                if (building.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                d.NonCityBuildings++;
                if (Value(building.BuildingData.turn_yield_counterintel) > 0) d.OwnCounterintel++;
                if (Value(building.BuildingData.turn_yield_intel) > 0)
                {
                    d.IntelBuildings++;
                    try
                    {
                        UpgradeData lidar = GameDataFileManager.GetDataFile<UpgradesData>().GetUpgrade(LidarUpgrade);
                        if (lidar != null && building.Upgrades != null && building.Upgrades.HasUpgrade(lidar)) d.IntelLidar++;
                    }
                    catch (Exception e) { ReportOnce("lidar-count", e); }
                }
            }
            d.WarTurn = WarTurnFor(me, turn); // v0.16 item 4: T16 with Orbital Assembly and WarMinIncome, else WarFallbackTurn
            d.War = WarFor(me, turn, d.WarTurn, recentDamage); // v0.20 (council I2): retaliation latch + hysteresis
            d.LosslessDone = TechDone(me, LosslessTech);
            d.EmcDone = TechDone(me, EmcTech);
            CountProjects(core, d);
            d.EnergyGlut = Plugin.EnergyGlutShare.Value > 0f
                && EconomyDirector.IsEnergyGlut(d.LastEnergyLeft, d.LastEnergyBudget, Plugin.EnergyGlutShare.Value, Plugin.EnergyGlutMin.Value);
            // v0.16.2 (matches 18-19): energy left over while material ran out -> material binds (extraction first, no energy gains).
            d.MaterialBound = turn >= Plugin.MaterialBoundFromTurn.Value
                && EconomyDirector.IsMaterialBound(d.LastEnergyLeft, d.LastEnergyBudget, d.LastMaterialLeft, d.LastMaterialBudget,
                    Plugin.MaterialBoundEnergyShare.Value, Plugin.MaterialBoundStockShare.Value, Plugin.MaterialBoundMinEnergy.Value);
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
            // v0.16 item 6: the largest enemy volley so far, and the share of it a defense could meet.
            MatchMemory memory = MemoryOf(me.PlayerID);
            if (last.EnemyImpacts > memory.PeakImpacts && last.Impacts > 0)
            {
                memory.PeakImpacts = last.EnemyImpacts;
                memory.PeakEligibleShare = Math.Max(0f, last.Impacts - last.NoEligibleDefense) / (float)last.Impacts;
            }
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
            // v0.15 (Sol #1, Fable #2 of the v0.14.2 audits): any guided enemy weapon is a city threat. v0.14.1 saw 11 human
            // Missiles at T22 with Ions over 2 of its 5 cities; the T23 volley of 14 took two cities. The per-city cover then
            // applies to Missiles and MIRVs too (they land exactly where aimed, Kinetic does not stop them).
            if (Plugin.GuidedCityCover.Value)
            {
                foreach (string guidedId in GuidedWeaponIds)
                {
                    int known, hits;
                    economy.EnemyWeapons.TryGetValue(guidedId, out known);
                    last.ImpactsByWeapon.TryGetValue(guidedId, out hits);
                    if (known > 0 || hits > 0) { d.GuidedThreat = true; break; }
                }
                if (d.GuidedThreat) d.NukeThreat = true;
            }
            try
            {
                d.OwnIntel = me.ResourceHandler.Intel;
                // v0.16 (N3, P2): only cities we see now count; the hidden ones add nothing, so their number never leaks.
                foreach (CelestialBody planet in core.EnemyPlanets)
                {
                    if (planet == null || planet.Player == null) continue;
                    foreach (BaseBuilding city in planet.Player.BuildingHandler.GetRemainingCities())
                    {
                        if (city != null && AiPatches.IsVisibleIntact(me, city)) d.VisibleEnemyCities++;
                    }
                }
            }
            catch (Exception e)
            {
                ReportOnce("visible-cities", e);
            }
            // v0.16 (Sol v0.16 F6): blind = no strike candidate on the committed target (set after targeting below).
            d.Blind = d.VisibleEnemyCities == 0;
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
                List<Point> intactPoints = new List<Point>();
                List<float> intactRadii = new List<float>();
                foreach (BaseBuilding b in me.BuildingHandler.Buildings)
                {
                    if (b == null || b.IsCity || b.BuildingData == null || b.BuildingData.id.Value != d.AntiNukeId) continue;
                    if (b.CurrentState != BaseBuilding.BuildingState.Ruins && b.CurrentState != BaseBuilding.BuildingState.Destroyed)
                    {
                        intactAntiNuke++;
                        Vector3 ip = b.transform.position;
                        intactPoints.Add(new Point(ip.x, ip.y, ip.z));
                        intactRadii.Add(DefenseRadius(b.BuildingData));
                    }
                    if (b.CurrentState != BaseBuilding.BuildingState.Normal || !b.Power.HasPower) continue;
                    Vector3 p = b.transform.position;
                    defensePoints.Add(new Point(p.x, p.y, p.z));
                    defenseRadii.Add(b.Stats.Defenses.Radius);
                }
                d.AntiNukeOwned = defensePoints.Count;
                int deficit = DefenseCoverage.CityProtectionDeficit(cityPoints, cityRadii, defensePoints, defenseRadii, perCity, out d.CitiesProtected);
                // v0.15: the cap counts defenses that protect a city (Ions placed elsewhere by value do not); a hard
                // planet-wide cap of 4 x perCity x cities on every intact one stays as the runaway guard (Terra v0.8.2 #2).
                d.AntiNukeProtecting = DefenseCoverage.ProtectingCount(cityPoints, cityRadii, defensePoints, defenseRadii);
                d.AntiNukeIntact = intactAntiNuke;
                d.AntiNukePerCityNow = perCity;
                d.MinCityCover = DefenseCoverage.MinCityCover(cityPoints, cityRadii, defensePoints, defenseRadii);
                // v0.15.1 (Sol v0.15 #2): placement now has to close a city deficit, so Ions placed elsewhere never come from
                // this branch and must not block it (v0.15.0: 29 irrelevant Ions with one city left suppressed every
                // purchase). The runaway guard counts intact Ions near a city, powered or not (unpowered ones protect
                // nothing, so the deficit would never close: Terra v0.8.2 #2).
                d.AntiNukeNearCities = DefenseCoverage.ProtectingCount(cityPoints, cityRadii, intactPoints, intactRadii);
                int hardCap = (int)Math.Ceiling(3f * perCity * cityPoints.Count);
                d.AntiNukeWanted = d.AntiNukeNearCities >= hardCap ? 0 : EconomyDirector.AntiNukeMissing(deficit, d.AntiNukeProtecting, cityPoints.Count, perCity, 2f);
                d.AntiNukeCap = hardCap;
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
            try
            {
                d.TargetCandidates = TargetCandidates(core);
                d.Blind = d.TargetCandidates == 0;
            }
            catch (Exception e)
            {
                ReportOnce("target-candidates", e);
            }
            // v0.16 item 5: credit this turn's energy to the intel budget once, then judge an open batch on what we see now.
            if (memory.CreditedTurn != turn)
            {
                memory.Intel.CreditedEnergy += Math.Max(0, me.ResourceHandler.EnergyProductionPerTurn);
                memory.CreditedTurn = turn;
                IntelBudget.Evaluate(memory.Intel, turn, d.VisibleEnemyCities, d.BlindEnemies, Plugin.IntelEvalTurns.Value, Plugin.IntelFailedBatchesStop.Value,
                    Plugin.IntelBudgetShare.Value, Plugin.IntelStopMinBudgetUse.Value);
            }
            // v0.13: weapons are chosen against the enemy the strategy committed to this turn (v0.12.1: the director chose
            // against the other AI's defenses while every strike went at the human).
            int commit = StrategyPatches.CommitTarget(me.PlayerID);
            foreach (CelestialBody planet in core.EnemyPlanets)
            {
                if (commit >= 0 && planet.Player != null && planet.Player.PlayerID == commit) target = planet.Player;
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
                    // v0.13.1: untested weapon types stopped by the same defenses inherit that evidence (Sol v0.13 #4).
                    try
                    {
                        List<string> weaponIds = new List<string>();
                        BuildingsData all = GameDataFileManager.GetDataFile<BuildingsData>();
                        foreach (string id in all.GetBuildingIds())
                        {
                            BuildingData bd = all.GetBuilding(id);
                            if (bd != null && bd.weapon_functionality.IsValueSet) weaponIds.Add(bd.weapon_functionality.Value);
                        }
                        EconomyDirector.ShareObservedPass(d.ObservedPass, weaponIds, DefensePlanner.Counters, 0.5f);
                    }
                    catch (Exception e)
                    {
                        ReportOnce("share-observed-pass", e);
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
            d.EnergyAtStart = core.Resources.Energy; // v0.16.2: before the defense reserve and repairs (free fire)
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
                // v0.16 item 6: the largest volley seen so far is expected again (the observed need lags one phase).
                d.ForwardDefenseNeed = ForwardNeed(core.PlayerData, Plugin.Ledger.LastDefender(core.PlayerData.PlayerID));
                reserve = Math.Max(reserve, d.ForwardDefenseNeed);
                reserve = Math.Max(0, Math.Min(reserve, core.Resources.Energy));
                // Under a nuke threat every working anti-nuke defense gets one 200-energy roll (Terra v0.8 #5).
                if (d.NukeThreat) reserve = Math.Max(reserve, (int)Math.Min(d.AntiNukeOwned * 200f, 0.4f * d.EnergyIncome));
                reserve = Math.Max(0, Math.Min(reserve, core.Resources.Energy));
                d.ReserveWanted = reserve; // v0.16: what the defense wanted before the recovery cap (logged with the shortfall)
                reserve = EconomyDirector.CapReserveForRecovery(reserve, core.Resources.Energy, d.EnergyIncome, Plugin.DefenseRecoveryShare.Value);
                if (economy.BuyPending && !economy.Bought) reserve = Math.Min(reserve, Math.Max(0, core.Resources.Energy - economy.GoalEnergyCost));
                core.Resources.Energy -= reserve;
                core.Construction.ConstructionQueue.AllottedEnergy -= reserve;
                d.DefenseReserve = reserve;
                d.TurnStartEnergy = core.Resources.Energy;
            }
            // v0.16 (Sol re-check round 3): a pending superweapon purchase keeps its funding through repairs.
            bool goalPending = economy.BuyPending && !economy.Bought;
            bool repaired = RepairAfterReserve(core, d, goalPending ? economy.GoalEnergyCost : 0, goalPending ? economy.GoalMaterialCost : 0);
            d.TurnStartEnergy = core.Resources.Energy;
            d.TurnStartMaterial = core.Resources.Material;
            d.Funded = repaired;
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

            // 3. Intel. v0.16 item 5 (council B3/A5, Pavol's P2): a bounded bid judged by our own visibility results replaces
            // v0.15's escalation while any enemy city was unseen (that count read the enemy's real surviving cities, and
            // match 16 bought 30 Telescopes, 4,940 E of upkeep, against counterintel x16-27). Native BelowMinimum (the
            // intel guided weapons need) stays exempt from the bid but is paid from the same budget.
            IntelEvaluationState intel = IntelState(core);
            bool nativeMinimum = intel == IntelEvaluationState.BelowMinimum;
            MatchMemory memory = MemoryOf(core.PlayerData.PlayerID);
            bool wantVision = d.Blind || d.BlindEnemies > 0;
            int bidAllowed = IntelBudget.Allowed(memory.Intel, d.Turn, wantVision, Plugin.IntelBidFromTurn.Value, Plugin.IntelBudgetShare.Value, 50f, // cheapest candidate; arrays re-checked at 200
                Plugin.IntelBatch.Value, Plugin.IntelEvalTurns.Value, Plugin.IntelProbeMax.Value, 200f);
            if (d.Steps <= 1) d.IntelAllowed = bidAllowed;
            d.IntelPolicy = memory.Intel.Stopped ? "stop-loss" : bidAllowed > 0 ? "bid" : wantVision ? "wait" : "seeing";
            // v0.16 (Sol v0.16 F5): native minimum gets priority but stays inside the budget share; its spend never opens or
            // restarts a bid batch. Each candidate is priced at its own cost (Lidar 50 E, an array 200 E).
            bool nativeRoom = nativeMinimum && d.IntelThisTurn + d.IntelUpgradesThisTurn < Plugin.IntelPerTurn.Value
                && IntelBudget.BudgetRoom(memory.Intel, Plugin.IntelBudgetShare.Value, 50f);
            bool intelGuarded = EconomyDirector.IntelGuarded(nativeMinimum, wantVision, d.IntelThisTurn);
            bool intelEnergy = EconomyDirector.IntelEnergyAllowed(intelGuarded, core.Resources.Energy, Plugin.IntelSprintEnergyReserve.Value);
            if ((nativeRoom || bidAllowed > 0) && intelEnergy && !d.FailedThisTurn.Contains("intel"))
            {
                int energyBefore = core.Resources.Energy;
                // Lidar first: +4 intel for 50 E (an array is 8 intel for 200 E; council N9).
                bool bought = TryIntelUpgrade(construction, core, d);
                if (!bought && d.IntelBuildings + d.IntelThisTurn < Plugin.MaxIntelBuildings.Value && d.SlotsLeft > 0
                    && IntelBudget.BudgetRoom(memory.Intel, Plugin.IntelBudgetShare.Value, 200f))
                    bought = TryIntelBuilding(construction, core, d);
                if (bought)
                {
                    int spent = energyBefore - core.Resources.Energy;
                    if (bidAllowed > 0) IntelBudget.Record(memory.Intel, d.Turn, spent, d.VisibleEnemyCities, d.BlindEnemies);
                    else IntelBudget.RecordSpend(memory.Intel, spent);
                    return true;
                }
                d.FailedThisTurn.Add("intel");
            }

            // 3b. Anti-nuke defenses over the cities (v0.8), on their own gate: nukes take cities, and cities decide the game.
            int ionsQueuedThisTurn = 0;
            if (d.AntiNukeId != null) economy.DefensesQueued.TryGetValue(d.AntiNukeId, out ionsQueuedThisTurn);
            int antiNukeTurnCap = d.GuidedThreat ? d.AntiNukeWanted : Math.Max(2, d.DefenseTurnCap); // v0.15: no throttle on city cover
            if (d.AntiNukeId != null && d.AntiNukeThisTurn < d.AntiNukeWanted && d.AntiNukeThisTurn < antiNukeTurnCap
                && d.AntiNukeNearCities + ionsQueuedThisTurn < d.AntiNukeCap // v0.15.3 (Sol v0.15.2 #4): Ions near cities only
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

            // 3c. Counter-defense (v0.11.1): a known enemy weapon type we barely stop gets its counter, even at the defense cap.
            if (d.CounterDefensesThisTurn < 2 && d.SlotsLeft > 0 && !d.FailedThisTurn.Contains("counter-defense"))
            {
                if (TryCounterDefense(construction, core, economy, d)) return true;
                d.FailedThisTurn.Add("counter-defense");
            }

            // 3d. v0.16.2 (matches 18-19): material binds. Economy comes after defenses and weapons, so mines got the leftovers:
            // match 19 Brain spent 5.8-8.9k material a turn on defenses, weapons and upgrades, bought 0-2 mines T20-T25, and left
            // 13-89% of its energy unspent (match 18: 57-66%) with 55-69% of the planet unmined. While material binds, up to
            // ExtractionFirstShare of the turn's material goes to extraction before defenses and weapons, to the drain guard's
            // horizon (the guard still applies). Terra v0.16.2 #1: after the anti-nuke and counter-defense steps, and a mine
            // must leave MaterialBoundMinSlots power slots for the defenses.
            if (d.MaterialBound && !d.FailedThisTurn.Contains("extraction-first")
                && EconomyDirector.ExtractionFirstRoom(Plugin.ExtractionFirstShare.Value, d.TurnStartMaterial, d.ExtractionFirstMaterial,
                    d.MaterialIncome, d.ExtractionCeiling) > 0)
            {
                if (TryEconomy(construction, core, economy, d, true)) return true;
                d.FailedThisTurn.Add("extraction-first");
            }

            // 4. Defenses: known threats, damage taken, and at war a floor scaled to what we own (v0.5).
            if (d.OwnDefenses + d.DefensesThisTurn < d.DesiredDefenses && d.DefensesThisTurn < d.DefenseTurnCap && d.SlotsLeft > 0 && !d.FailedThisTurn.Contains("defense"))
            {
                if (TryDefense(construction, core, economy, d)) return true;
                d.FailedThisTurn.Add("defense");
            }

            // 4c. v0.16 item 6 (council B5): Kinetic cost-down (Maglev; 70% roll cost) and range (Early Warning AI; x1.3) on the
            // defenses we own. Match 16 left 40 rolls unpaid at T26; the same 3,400 E pays 48 cost-down rolls instead of 34.
            if (d.Defensive && Plugin.DefenseUpgradesPerTurn.Value > 0 && d.DefenseUpgradesThisTurn < Plugin.DefenseUpgradesPerTurn.Value
                && !d.FailedThisTurn.Contains("defense-upgrade"))
            {
                if (TryDefenseUpgrade(construction, core, d)) return true;
                d.FailedThisTurn.Add("defense-upgrade");
            }

            // 4a. v0.15 (Fable #5/#12, Sol #2 of the v0.14.2 audits): counterintel city projects (Quantum Encryption Hub: +50
            // counterintel per city, permanent, 150 energy / 200 material, unlocked by Self-Adjusting Optics). v0.14.1 never bought
            // one; its counterintel stayed x1.0 until T22 while the human saw every city.
            if (Plugin.CounterintelProjects.Value && d.Turn >= Plugin.CounterintelFromTurn.Value && d.CityProjectsThisTurn < 2 && !d.FailedThisTurn.Contains("city-project"))
            {
                // v0.15.3 (Sol v0.15.2 #3): in the opening a counterintel project must leave one plant's energy (v0.15.1 T8:
                // two projects took the last 300 after the Telescopes, and no plant was bought).
                float keep = d.Turn <= Plugin.IntelSprintUntilTurn.Value ? Plugin.OpeningPlantReserve.Value : 0f;
                if (TryCityProject(construction, core, d, "pro_CounterintelYieldPermanent1", keep)) return true;
                d.FailedThisTurn.Add("city-project");
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
            float upkeepCap = (d.Turn >= d.WarTurn ? Plugin.WarUpkeepShare.Value : Plugin.RetaliationUpkeepShare.Value) * d.EnergyIncome;
            bool underSpendShare = d.WeaponEnergySpent < Plugin.WarSpendShare.Value * d.TurnStartEnergy;
            // v0.16 (Sol v0.16 M5): blind, the artillery package is funded before more plain weapons take the upkeep room.
            if (d.War && d.Blind && Plugin.BlindArtillery.Value && Plugin.WeaponUpgradesEnabled.Value && !d.FailedThisTurn.Contains("blind-artillery")
                && d.WeaponUpgradesThisTurn < Plugin.WeaponUpgradesPerTurn.Value)
            {
                if (TryBlindArtilleryUpgrade(construction, core, d, upkeepCap)) return true;
                d.FailedThisTurn.Add("blind-artillery");
            }
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
                // v0.16 item 4 (Pavol's P1): blind, the artillery package first (Optimized Airburst, Triple Barrel).
                if (Plugin.BlindArtillery.Value && d.Blind && !d.FailedThisTurn.Contains("blind-artillery"))
                {
                    if (TryBlindArtilleryUpgrade(construction, core, d, upkeepCap)) return true;
                    d.FailedThisTurn.Add("blind-artillery");
                }
                if (TryWeaponUpgrade(construction, core, d, upkeepCap)) return true;
                d.FailedThisTurn.Add("weapon-upgrade");
            }

            // 6. Economy: the best-value upgrade, project or building.
            if (TryEconomy(construction, core, economy, d)) return true;

            // 7. v0.16 (Sol v0.16 F5): the separate preferred-intel purchase is gone; it bypassed the budget and the stop-loss.

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
            // v0.16 (Sol v0.16 F6): a Nuclear Missile is a precision weapon; no purchase while the target offers no candidate.
            if (Plugin.BlindArtillery.Value && d.Blind && economy.Goal != null && economy.Goal.Building != null
                && economy.Goal.Building.weapon_functionality.IsValueSet && IsPrecisionWeapon(economy.Goal.Building.weapon_functionality.Value))
            {
                d.GuidedRefused++;
                return false;
            }
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

        // v0.16 item 5: the Telescope intel upgrade (Lidar Unit, intel x1.5, upkeep x1.6) on an array that lacks it.
        // AddUpgradeOptions values only energy, material and science, so v0.15 never bought one.
        private static bool TryIntelUpgrade(AIConstruction construction, AICore core, DirectorState d)
        {
            UpgradeData lidar = GameDataFileManager.GetDataFile<UpgradesData>().GetUpgrade(LidarUpgrade);
            if (lidar == null || !core.PlayerData.BuildingHandler.AvailableBuildings.IsUpgradeAvailable(lidar)) return false;
            foreach (BaseBuilding b in core.PlayerData.BuildingHandler.Buildings)
            {
                if (b == null || b.IsCity || b.BuildingData == null || b.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                if (Value(b.BuildingData.turn_yield_intel) <= 0 || b.Upgrades == null || b.Upgrades.HasUpgrade(lidar)) continue;
                if (d.PlannedUpgradeKeys.Contains(b.ObjectId + ":" + LidarUpgrade)) continue;
                if (QueueUpgrade(construction, core, d, b, lidar))
                {
                    d.IntelUpgradesThisTurn++; // v0.16 (Sol v0.16 F5): not counted as a building
                    return true;
                }
            }
            return false;
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
                if (yield <= 0 || !available.IsBuildingAvailable(data) || !Affordable(core, data) || Blocked(d, id)) continue;
                if (!QueueBuilding(construction, core, d, data, kind)) continue;
                if (kind == "counterintel") d.CounterintelThisTurn++;
                if (kind == "intel") d.IntelThisTurn++;
                return true;
            }
            return false;
        }

        private const string NuclearWeaponId = "wea_NuclearMissile";
        private static readonly string[] GuidedWeaponIds = { "wea_Missile", "wea_MIRV", "wea_NuclearMissile" };

        // v0.16 item 4: weapons whose value is second-phase targeting onto a visible target (MIRV has no second phase).
        internal static bool IsPrecisionWeapon(string weaponId)
        {
            return weaponId == "wea_Missile" || weaponId == "wea_NuclearMissile";
        }

        // v0.16 (Sol v0.16 F6): visible strike candidates on the enemy the strategy committed to; with no commitment, the
        // most any enemy offers. Precision purchases (ordinary and superweapon) need at least one.
        internal static int TargetCandidates(AICore core)
        {
            PlayerData me = core.PlayerData;
            int commit = StrategyPatches.CommitTarget(me.PlayerID), best = 0;
            foreach (CelestialBody planet in core.EnemyPlanets)
            {
                if (planet == null || planet.Player == null) continue;
                int n = AiPatches.StrikeCandidateCount(me, planet.Player);
                if (commit >= 0 && planet.Player.PlayerID == commit) return n;
                best = Math.Max(best, n);
            }
            return best;
        }

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
                if (Blocked(d, id) || !Affordable(core, data)) continue; // another type when one fails
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

        private static bool TryCounterDefense(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, DirectorState d)
        {
            if (economy.EnemyWeapons.Count == 0) return false;
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            List<string> buildable = new List<string>();
            foreach (string id in DefensePlanner.Counters.Keys)
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data != null && available.IsBuildingAvailable(data) && Affordable(core, data) && !Blocked(d, id)) buildable.Add(id);
            }
            Dictionary<string, int> owned = new Dictionary<string, int>(economy.OwnDefenses);
            foreach (KeyValuePair<string, int> pair in economy.DefensesQueued)
            {
                int n;
                owned.TryGetValue(pair.Key, out n);
                owned[pair.Key] = n + pair.Value;
            }
            string choice = DefensePlanner.CounterDeficit(economy.EnemyWeapons, owned, buildable);
            if (choice == null) return false;
            if (!QueueBuilding(construction, core, d, buildings.GetBuilding(choice), "defense")) return false;
            Increment(economy.DefensesQueued, choice);
            d.DefensesThisTurn++;
            d.CounterDefensesThisTurn++;
            return true;
        }

        // ---- weapons -------------------------------------------------------------------------------

        // v0.12: a weapon that needs a trajectory search: not a laser (fired directly) and not a superweapon (savings logic).
        private static bool IsAimedWeapon(BuildingData data)
        {
            if (data == null || !data.weapon_functionality.IsValueSet) return false;
            if (data.max_amount_at_once.IsValueSet && data.max_amount_at_once.Value > 0) return false;
            WeaponData weapon = GameDataFileManager.GetDataFile<WeaponsData>().GetWeapon(data.weapon_functionality.Value);
            string type = weapon != null && weapon.type.IsValueSet ? weapon.type.Value : "";
            return type != "type_Laser" && type != "type_EMPLaser" && type != "type_SolarLaser";
        }

        // v0.12: on a dry planet mines yield nothing; selling returns 80% of their material and upgrades
        // (PlayerBuildingHandler.SellBuilding). Prefix on AICore.BeginTurn (Sol v0.12 #3): before AIResources snapshots the
        // material (so the proceeds are counted once, natively) and before AIPowerGridMaintaining builds its caches and
        // reconnection searches. Only leaves are sold: at most one power connection (a relay has two or more), no generation.
        private static readonly Dictionary<int, int[]> s_soldThisTurn = new Dictionary<int, int[]>(); // player -> [turn, mines, material]


        private static void CoreBeginTurnPrefix(AICore __instance)
        {
            if (!Plugin.Active || !Plugin.DirectorEnabled.Value) return;
            PlayerData me = __instance.PlayerData;
            if (me == null || !Plugin.IsSmart(me)) return;
            SellDryMines(me);
            // v0.16 (Sol v0.16 re-check round 2): repairs moved to StartConstruction, after this turn's defense reserve.
        }

        // v0.13: repair damaged buildings before the turn's resources are snapshotted, so construction plans with what is
        // left. Weapons, then defenses, then power; most damaged first; at most RepairEnergyShare of this turn's energy.
        // v0.13 repairs; v0.16 (Sol v0.16 re-check round 2, F4): run inside StartConstruction, after this turn's defense reserve
        // has been taken from the construction budget, so repairs and the reserve use ONE current-turn defense request.
        // core.Resources is native's turn snapshot (firing already reserved, AIResources.BeginTurn); repairs leave the
        // recovery floor and are charged to the snapshot and the queue allotment as well as to the player's stock.
        // v0.19 (Terra v0.19c #2): true when the repair stage completed (disabled or nothing to repair counts); false on an error.
        private static bool RepairAfterReserve(AICore core, DirectorState d, int keepEnergy, int keepMaterial)
        {
            try
            {
                if (!Plugin.RepairEnabled.Value || Plugin.RepairsPerTurn.Value <= 0) return true;
                PlayerData me = core.PlayerData;
                PlayerResourceHandler res = me.ResourceHandler;
                bool dry = GameSessionManager.CurrentGameSettings.PlanetMaterial != -1 && res.PlanetMaterial <= 0;
                List<RepairCandidate> candidates = new List<RepairCandidate>();
                foreach (BaseBuilding b in me.BuildingHandler.Buildings)
                {
                    if (b == null || b.IsCity || b.BuildingData == null || b.CurrentState != BaseBuilding.BuildingState.Normal || b.Stats == null) continue;
                    float max = b.Stats.MaxHP;
                    if (max <= 0f || b.Stats.CurrentHP >= max) continue;
                    BuildingData data = b.BuildingData;
                    bool mine = Value(data.turn_yield_material) > 0;
                    if (mine && dry) continue; // sold, not repaired
                    bool generator = b.Power != null && b.Power.CanPotentiallyGeneratesPower;
                    bool unpowered = !generator && b.Power != null && !b.Power.HasPower;
                    int priority = unpowered ? 6 // repairing it does nothing until it has power again
                        : data.weapon_functionality.IsValueSet ? 0
                        : data.defense_functionality.IsValueSet ? 1
                        : Value(data.turn_yield_energy) > 0 ? 2
                        : Value(data.turn_yield_intel) > 0 || Value(data.turn_yield_counterintel) > 0 ? 3
                        : mine ? 4 : 5;
                    candidates.Add(new RepairCandidate
                    {
                        Priority = priority,
                        HpShare = b.Stats.CurrentHP / max,
                        Energy = Value(data.repair_cost_energy),
                        Material = Value(data.repair_cost_material),
                        Payload = b,
                    });
                }
                d.RepairHeld = d.DefenseReserve;
                if (candidates.Count == 0) return true;
                float recovery = Math.Max(0f, Plugin.DefenseRecoveryShare.Value) * d.EnergyIncome;
                int energyBudget = (int)Math.Min(Math.Max(0f, Plugin.RepairEnergyShare.Value) * core.Resources.Energy,
                    Math.Max(0f, core.Resources.Energy - recovery - Math.Max(0, keepEnergy)));
                int materialBudget = (int)Math.Min(Math.Max(0f, Math.Min(1f, Plugin.RepairMaterialShare.Value)) * core.Resources.Material,
                    Math.Max(0, core.Resources.Material - Math.Max(0, keepMaterial)));
                List<RepairCandidate> picked = RepairPlanner.Pick(candidates, energyBudget, materialBudget, Plugin.RepairsPerTurn.Value, Plugin.RepairBelowHpShare.Value);
                int n = 0, spentE = 0, spentM = 0;
                foreach (RepairCandidate c in picked)
                {
                    if (c.Energy > core.Resources.Energy || c.Material > core.Resources.Material) break;
                    if (!res.CheckEnergy(c.Energy) || !res.CheckMaterial(c.Material)) break;
                    BaseBuilding b = (BaseBuilding)c.Payload;
                    res.SpendEnergy(c.Energy);
                    res.SpendMaterial(c.Material);
                    core.Resources.Energy -= c.Energy;
                    core.Resources.Material -= c.Material;
                    core.Construction.ConstructionQueue.AllottedEnergy -= c.Energy;
                    core.Construction.ConstructionQueue.AllottedMaterial -= c.Material;
                    me.BuildingHandler.RepairBuilding(b, b.RefreshBuildingIcon);
                    n++;
                    spentE += c.Energy;
                    spentM += c.Material;
                }
                d.Repaired = n;
                d.RepairEnergy = spentE;
                d.RepairMaterial = spentM;
                return true;
            }
            catch (Exception e)
            {
                ReportOnce("repair", e);
                return false;
            }
        }

        // v0.16 item 6: the live energy one defense roll costs (upgrades included, Kinetic cost-down x0.7), averaged over
        // our working defenses; 150 (the strategy's assumption) when we own none.
        private static float LiveRollCost(PlayerData me)
        {
            int n = 0;
            float sum = 0f;
            foreach (BaseBuilding b in me.BuildingHandler.Buildings)
            {
                if (b == null || b.IsCity || b.BuildingData == null || !b.BuildingData.defense_functionality.IsValueSet) continue;
                if (b.CurrentState != BaseBuilding.BuildingState.Normal || b.Stats == null || b.Stats.Defenses == null) continue;
                if (b.Power == null || !b.Power.HasPower) continue; // v0.16 (Sol v0.16 M6): working defenses only
                int cost = b.Stats.Defenses.EnergyActivationCost;
                if (cost <= 0) continue;
                sum += cost;
                n++;
            }
            return n > 0 ? sum / n : 150f;
        }

        // v0.16 item 6: the defense demand the evidence supports (the last phase's need, or the largest volley seen so far),
        // bounded by DefenseHardCapShare. Planned shares are not held back from repairs; only observed fire is.
        internal static int ForwardNeed(PlayerData me, DefenderStats last)
        {
            if (Plugin.ForwardDefenseMargin.Value <= 0f) return 0;
            MatchMemory m = MemoryOf(me.PlayerID);
            int peak = m.PeakImpacts;
            float share = m.PeakEligibleShare;
            if (last.EnemyImpacts > peak && last.Impacts > 0)
            {
                peak = last.EnemyImpacts;
                share = Math.Max(0f, last.Impacts - last.NoEligibleDefense) / (float)last.Impacts;
            }
            return ForwardDefense.Need(peak, share, LiveRollCost(me), Plugin.ForwardDefenseMargin.Value,
                me.ResourceHandler.EnergyProductionPerTurn, Plugin.DefenseHardCapShare.Value);
        }

        private static void SellDryMines(PlayerData me)
        {
            try
            {
                if (Plugin.SellDryMinesPerTurn.Value <= 0) return;
                if (GameSessionManager.CurrentGameSettings.PlanetMaterial == -1 || me.ResourceHandler.PlanetMaterial > 0) return;
                List<BaseBuilding> dry = new List<BaseBuilding>();
                foreach (BaseBuilding b in me.BuildingHandler.Buildings)
                {
                    if (dry.Count >= Plugin.SellDryMinesPerTurn.Value) break;
                    if (IsSellableDryMine(b)) dry.Add(b);
                }
                if (dry.Count == 0) return;
                int before = me.ResourceHandler.Material;
                foreach (BaseBuilding b in dry) me.BuildingHandler.SellBuilding(b, true);
                s_soldThisTurn[me.PlayerID] = new[] { GameManager.CurrentTurn, dry.Count, Math.Max(0, me.ResourceHandler.Material - before) };
            }
            catch (Exception e)
            {
                ReportOnce("sell-dry-mines", e);
            }
        }

        private static bool IsSellableDryMine(BaseBuilding b)
        {
            if (b == null || b.IsCity || b.BuildingData == null || b.CurrentState != BaseBuilding.BuildingState.Normal) return false;
            if (Value(b.BuildingData.turn_yield_material) <= 0 || Value(b.BuildingData.turn_cost_planet_material) <= 0) return false;
            if (b.Power == null || b.Power.CanPotentiallyGeneratesPower) return false;
            return b.Power.ConnectedPowerSlots <= 1;
        }

        private static bool TryWeapon(AIConstruction construction, AICore core, DirectorState d, float upkeepCap)
        {
            if (d.SlotsLeft <= 0) return false;
            // v0.12: the trajectory search aims only so many weapons a turn; more add upkeep, not damage.
            bool atCap = Plugin.MaxWeapons.Value > 0 && d.AimedWeapons >= Plugin.MaxWeapons.Value;
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            WeaponsData weapons = GameDataFileManager.GetDataFile<WeaponsData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            List<WeaponStats> options = new List<WeaponStats>();
            // v0.16 item 4: live firing cost (global firing modifiers), as the turn's upkeep d.Upkeep is (council E5).
            float globalFiring = core.PlayerData.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.EnergyFiringCost);
            foreach (string id in buildings.GetBuildingIds())
            {
                BuildingData data = buildings.GetBuilding(id);
                if (data == null || !data.weapon_functionality.IsValueSet) continue;
                if (data.max_amount_at_once.IsValueSet && data.max_amount_at_once.Value > 0) continue; // superweapons: savings logic
                // v0.16 item 4 (Pavol's P1): a precision weapon needs a city to strike. With none visible, match 16's 45
                // Missiles hit random points; buy artillery instead.
                if (Plugin.BlindArtillery.Value && d.Blind && IsPrecisionWeapon(data.weapon_functionality.Value))
                {
                    d.GuidedRefused++;
                    continue;
                }
                if (!available.IsBuildingAvailable(data) || !Affordable(core, data) || Blocked(d, id)) continue;
                WeaponData weapon = weapons.GetWeapon(data.weapon_functionality.Value);
                if (weapon == null) continue;
                string type = weapon.type.IsValueSet ? weapon.type.Value : "";
                if (!Plugin.EnableLasers.Value && (type == "type_Laser" || type == "type_EMPLaser" || type == "type_SolarLaser")) continue;
                if (atCap && IsAimedWeapon(data)) continue;
                int firing = (int)Math.Round((weapon.firing_cost_energy.IsValueSet ? weapon.firing_cost_energy.Value : 0) * globalFiring);
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
            // v0.13.1 (Sol v0.13 #3): rank by damage through per weapon only when weapon slots bind (70% of the cap reached)
            // or energy does not (energy glut); otherwise firing energy binds and cost-effectiveness ranks.
            bool slotsBind = Plugin.MaxWeapons.Value > 0 && (d.AimedWeapons >= 0.7f * Plugin.MaxWeapons.Value || d.EnergyGlut);
            WeaponStats pick = EconomyDirector.PickWeapon(options, d.TargetDefenses, DefensePlanner.Counters, Plugin.DefensesForFullCover.Value, 4f, 0.6f, Plugin.Rng, d.ObservedPass,
                slotsBind);
            if (pick == null) return false;
            if (!QueueBuilding(construction, core, d, buildings.GetBuilding(pick.BuildingId), "weapon")) return false;
            d.Upkeep += pick.FiringEnergy;
            d.WeaponEnergySpent += pick.BuildEnergy;
            d.WeaponsThisTurn++;
            if (IsAimedWeapon(buildings.GetBuilding(pick.BuildingId))) d.AimedWeapons++;
            return true;
        }

        // ---- economy -------------------------------------------------------------------------------

        // v0.16.2: extractionFirst = the material-binds step (2b): extraction-only options, within ExtractionFirstRoom.
        private static bool TryEconomy(AIConstruction construction, AICore core, EconomyPatches.AiEconomyState economy, DirectorState d,
            bool extractionFirst = false)
        {
            List<EconOption> options = new List<EconOption>();
            float planetFactor = EconomyDirector.PlanetMaterialFactor(core.PlayerData.ResourceHandler.PlanetMaterial, d.MaterialIncome, 10);
            AddUpgradeOptions(core, d, options, planetFactor);
            AddProjectOptions(construction, core, d, options, planetFactor);
            AddBuildingOptions(core, d, options, planetFactor);
            int extractionRoom = int.MaxValue;
            if (extractionFirst)
            {
                options.RemoveAll(o => !EconomyDirector.IsExtractionOnly(o));
                extractionRoom = EconomyDirector.ExtractionFirstRoom(Plugin.ExtractionFirstShare.Value, d.TurnStartMaterial, d.ExtractionFirstMaterial,
                    d.MaterialIncome, d.ExtractionCeiling);
                if (options.Count == 0 || extractionRoom <= 0) return false;
            }
            // Terra v0.16.2 #1: a mine queued by step 3d leaves MaterialBoundMinSlots power slots for the defenses after it.
            int slotsForOptions = extractionFirst ? d.SlotsLeft - Math.Max(0, Plugin.MaterialBoundMinSlots.Value) : d.SlotsLeft;

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
                // v0.16.2: while material binds, energy gains are worth nothing (the glut weights) unless power slots run short
                // (Terra v0.16.2 #2: the slot exception holds for the energy glut too, so a plant for slots stays buyable).
                EnergyGlut = extractionFirst
                    || EconomyDirector.UseGlutWeights(d.EnergyGlut, d.MaterialBound, d.SlotsLeft, Plugin.MaterialBoundMinSlots.Value),
                SustainableMaterialIncome = d.SustainableMaterial,
                EnergySaturation = Plugin.EnergySaturation.Value,
                MaterialReserve = d.MaterialReserve,
                SlotsLeft = d.SlotsLeft,
                PlantsQueued = d.PlantsQueued,
                Opening = d.Turn <= 3,
            };
            if (extractionFirst) balance.MaterialBudget = Math.Min(balance.MaterialBudget, extractionRoom + Math.Max(0, d.MaterialReserve));
            else
            {
                d.MaterialGlut = balance.MaterialGlut;
                d.EnergySaturated = EconomyDirector.EnergySaturated(balance);
            }
            for (int attempt = 0; attempt < 6; attempt++)
            {
                EconOption best = EconomyDirector.Best(options, balance, slotsForOptions);
                if (best == null) return false;
                if (Execute(construction, core, d, best))
                {
                    if (!best.SavesReserve)
                    {
                        // v0.16 item 1: production, not investment utility (v0.15 added SSTAR's 0.6-span utility here).
                        d.EnergyIncome += best.NextTurnEnergy;
                        d.MaterialIncome += best.NextTurnMaterial;
                        if (d.ScienceIncome >= 0f) d.ScienceIncome += best.NextTurnScience;
                        d.PlanetDrain += Math.Max(0f, best.PlanetDrainAdd); // v0.16.1
                    }
                    if (EconomyDirector.IsEnergyOnly(best)) d.PowerEnergySpent += best.Energy;
                    if (extractionFirst)
                    {
                        d.ExtractionFirstMaterial += best.Material;
                        d.ExtractionFirstBought++;
                        // Terra v0.16.2 M3: a building can still be refunded if its placement fails (Abandon reverses this).
                        if (best.Kind == OptionKind.Producer || best.Kind == OptionKind.Plant) Increment(d.ExtractionFirstQueued, best.Id);
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
                    // v0.15: no intel-cutting upgrade (Telescope science x0.75 intel) while an enemy city is unseen or blind.
                    if ((d.Blind || d.BlindEnemies > 0) && FloatValue(upgrade.turn_yield_intel_multiplier, 1f) < 1f) continue;
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
                    // Terra v0.16.1 #2: a yield upgrade that multiplies a mine's planet cost is extra drain too.
                    float upgradeDrainAdd = savesReserve ? 0f : Value(data.turn_cost_planet_material)
                        * me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnCostPlanetMaterial)
                        * Math.Max(0f, FloatValue(upgrade.turn_cost_planet_material_multiplier, 1f) - 1f);
                    if (Plugin.PlanetDrainGuard.Value && upgradeDrainAdd > 0f
                        && !PlanetGuard.DrainAllowed(d.PlanetDrain, upgradeDrainAdd, 0, d.PlanetPool, Plugin.PlanetGuardHorizonTurn.Value - d.Turn))
                    {
                        d.ExtractionBlocked++;
                        continue;
                    }
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
                        PlanetDrainAdd = upgradeDrainAdd,
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

        private static void AddProjectOptions(AIConstruction construction, AICore core, DirectorState d, List<EconOption> options, float planetFactor)
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
                if (!HasCityForProject(construction, core, project, d)) { d.PermanentsBlocked += IsPermanent(project.Project) ? 1 : 0; continue; }

                ProjectData p = project.Project;
                // As BuildingProjects.TryInstallProject: no duration means permanent.
                bool permanent = (p.permanent.IsValueSet && p.permanent.Value) || !p.duration.IsValueSet;
                float span = permanent ? 1f : Math.Min(10, Math.Max(1, p.duration.Value)) / 10f;
                // v0.16 item 1 (council E1): a global multiplier scales this turn's projected production, this turn's queued
                // projects and plants included, so a second SSTAR compounds on the first (x1.3 x 1.3) instead of both
                // counting from the live base.
                float energyGain = Value(p.turn_yield_energy_addition) * globalEnergy - Value(p.turn_cost_energy_addition)
                    + Math.Max(0f, d.EnergyIncome) * (FloatValue(p.turn_yield_energy_multiplier_global, 1f) - 1f);
                // v0.12 (Sol #1): extraction gains scale with what the planet can still give (0 when dry).
                float materialGain = (Value(p.turn_yield_material_addition) * globalMaterial
                    + Math.Max(0f, d.MaterialIncome) * (FloatValue(p.turn_yield_material_multiplier_global, 1f) - 1f)) * planetFactor
                    - Value(p.turn_cost_material_addition);
                // Local multiplier: that city's science (population x its rate, Terra v0.9 #5); global: all science.
                float thisCityScience;
                if (!cityScience.TryGetValue(project.City.id.Value, out thisCityScience)) thisCityScience = res.ScienceProductionPerTurn / (float)cities;
                // v0.16 (Sol v0.16 M2): a global science multiplier (Jamming) compounds on this turn's projected science.
                if (d.ScienceIncome < 0f) d.ScienceIncome = res.ScienceProductionPerTurn;
                float scienceGain = thisCityScience * (FloatValue(p.turn_yield_science_multiplier, 1f) - 1f)
                    + d.ScienceIncome * (FloatValue(p.turn_yield_science_multiplier_global, 1f) - 1f);
                if (energyGain * span <= 0f && materialGain * span <= 0f && scienceGain <= 0f) continue;
                float projectDrainAdd = Value(p.turn_cost_planet_material_addition)
                    + Math.Max(0f, d.PlanetDrain) * Math.Max(0f, FloatValue(p.turn_cost_planet_material_multiplier_global, 1f) - 1f);
                if (Plugin.PlanetDrainGuard.Value && projectDrainAdd > 0f
                    && !PlanetGuard.DrainAllowed(d.PlanetDrain, projectDrainAdd, permanent ? 0 : Math.Max(1, p.duration.Value), d.PlanetPool,
                        Plugin.PlanetGuardHorizonTurn.Value - d.Turn))
                {
                    d.ExtractionBlocked++;
                    continue;
                }
                options.Add(new EconOption
                {
                    Id = key,
                    Kind = OptionKind.Project,
                    Energy = project.EnergyCost,
                    Material = project.MaterialCost,
                    EnergyGain = energyGain * span,
                    MaterialGain = materialGain * span,
                    ScienceGain = scienceGain * span,
                    IncomeEnergy = energyGain,
                    IncomeMaterial = materialGain,
                    IncomeScience = scienceGain,
                    PlanetDrainAdd = projectDrainAdd,
                    Payload = project,
                });
            }
        }

        // v0.15: queue one city project by id in the first city type that offers it and has a city with a free slot.
        private static bool TryCityProject(AIConstruction construction, AICore core, DirectorState d, string projectId, float keepEnergy = 0f)
        {
            ProjectsData projectsData = GameDataFileManager.GetDataFile<ProjectsData>();
            PlayerAvailableBuildings availableNow = core.PlayerData.BuildingHandler.AvailableBuildings;
            HashSet<string> types = new HashSet<string>();
            foreach (BaseBuilding city in core.PlayerData.BuildingHandler.GetRemainingCities())
            {
                if (city == null || city.CityData == null || !types.Add(city.CityData.id.Value)) continue;
                foreach (ProjectData p in projectsData.GetAvailableCityProjects(city.CityData.id.Value))
                {
                    if (p == null || p.id.Value != projectId || !availableNow.IsProjectAvailable(p)) continue;
                    AIProjectEntity entity = new AIProjectEntity(core, AIConstructionEntityValueType.EnergyGeneration, p, city.CityData);
                    if (!HasCityForProject(construction, core, entity, d)) continue;
                    if (core.Resources.Energy - entity.EnergyCost < keepEnergy) return false;
                    if (QueueProject(construction, core, d, entity))
                    {
                        d.CityProjectsThisTurn++;
                        return true;
                    }
                }
            }
            return false;
        }

        // v0.15 instrumentation: our cities' population by city id ("1234=620;1240=x"; x = ruined).
        private static string OwnCityPopulations(PlayerData me)
        {
            List<string> parts = new List<string>();
            foreach (BaseBuilding city in me.BuildingHandler.GetBuildingsByType(BaseBuilding.BuildingFunctionalityType.City))
            {
                if (city == null || city.Stats == null) continue;
                // v0.15.1 (Sol v0.15 #10): "id=pop", so strikes and population can be joined by city id.
                parts.Add(city.ObjectId + "=" + (city.CurrentState == BaseBuilding.BuildingState.Normal ? city.Stats.CurrentPopulation.ToString() : "x"));
            }
            return string.Join(";", parts.ToArray());
        }

        private static bool HasCityForProject(AIConstruction construction, AICore core, AIProjectEntity project, DirectorState d)
        {
            return CityForProject(construction, core, project, d) != null;
        }

        // As BuildingProjects.TryInstallProject: no duration means permanent.
        private static bool IsPermanent(ProjectData p)
        {
            return p != null && ((p.permanent.IsValueSet && p.permanent.Value) || !p.duration.IsValueSet);
        }

        // v0.16 item 1: a city's free project slots, projects already queued for it taken off.
        private static int CityFreeSlots(AIConstruction construction, BaseBuilding city)
        {
            if (city == null || city.Projects == null) return 0;
            // v0.16 (Sol v0.16 F3): as native AIProjectConstruction.HasCityFreeSlots: free slots minus projects committed
            // as actions this turn (m_projectsInstalledThisTurn) minus projects still queued.
            int committed = 0;
            try
            {
                Dictionary<BaseBuilding, List<ProjectData>> installing = s_projectsInstalling != null
                    ? s_projectsInstalling.GetValue(construction.Projects) as Dictionary<BaseBuilding, List<ProjectData>> : null;
                List<ProjectData> list;
                if (installing != null && installing.TryGetValue(city, out list) && list != null) committed = list.Count;
            }
            catch (Exception e)
            {
                ReportOnce("committed-projects", e);
            }
            return Math.Max(0, city.Projects.FreeSlots - committed - construction.ConstructionQueue.GetQueuedProjectsCount(city));
        }

        private static readonly FieldInfo s_projectsInstalling = AccessTools.Field(typeof(AIProjectConstruction), "m_projectsInstalledThisTurn");

        // v0.16 item 1: installed (and queued) permanent, Jamming and SSTAR projects over our living cities, and the slot
        // picture logged every turn (council gate: free / reserved capacity per city).
        private static void CountProjects(AICore core, DirectorState d)
        {
            try
            {
                List<string> parts = new List<string>();
                foreach (BaseBuilding city in core.PlayerData.BuildingHandler.GetRemainingCities())
                {
                    if (city == null || city.Projects == null || city.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                    int permanent = 0, temporary = 0;
                    foreach (ProjectData p in city.Projects.CurrentProjects)
                    {
                        if (p == null) continue;
                        if (IsPermanent(p)) permanent++;
                        else temporary++;
                        if (p.id.Value == JammingProject) d.JammingActive++;
                        if (p.id.Value == SstarProject) d.SstarActive++;
                        if (p.id.Value == RareMaterialProject) d.RareMaterialActive++;
                    }
                    bool hasJamming = false;
                    foreach (ProjectData p in city.Projects.CurrentProjects) if (p != null && p.id.Value == JammingProject) hasJamming = true;
                    if (!hasJamming) d.JammingEligibleCities++;
                    int free = CityFreeSlots(core.Construction, city);
                    int total = free + permanent + temporary;
                    d.PermanentProjects += permanent;
                    d.ProjectSlotsFree += free;
                    d.ProjectSlotsTotal += total;
                    parts.Add(city.ObjectId + "=" + free + "/" + total + ":" + permanent + ":" + temporary);
                }
                d.ProjectSlots = string.Join(";", parts.ToArray());
            }
            catch (Exception e)
            {
                ReportOnce("count-projects", e);
            }
        }

        // v0.16 gate logging (Sol v0.16 section 5): completion of the techs the gates name.
        private static readonly string[] s_gateTechs = { "tec_ExperimentalMatterControl", "tec_MicrowaveEnergyTranmission", LosslessTech,
            "tec_MaglevServos", "tec_PlasmaChannelInduction", OrbitalAssemblyTech, "tec_EarlyWarningAI", "tec_Ultracapacitors", "tec_CompositeHeatCells" };

        private static string KeyTechs(PlayerData me)
        {
            List<string> done = new List<string>();
            foreach (string id in s_gateTechs) if (TechDone(me, id)) done.Add(id.Substring(4));
            return string.Join(",", done.ToArray());
        }

        // v0.16 gate logging: powered defenses by installed upgrade ("upg_KineticRange1=12;upg_KineticEnergyCostDown1=20;powered=31").
        private static string UpgradedDefenses(PlayerData me)
        {
            try
            {
                Dictionary<string, int> counts = new Dictionary<string, int>();
                int powered = 0;
                UpgradesData upgrades = GameDataFileManager.GetDataFile<UpgradesData>();
                List<UpgradeData> watched = new List<UpgradeData>();
                foreach (string id in IdList(Plugin.DefenseUpgradeIds.Value)) { UpgradeData u = upgrades.GetUpgrade(id); if (u != null) watched.Add(u); }
                foreach (BaseBuilding b in me.BuildingHandler.Buildings)
                {
                    if (b == null || b.IsCity || b.BuildingData == null || !b.BuildingData.defense_functionality.IsValueSet) continue;
                    if (b.CurrentState != BaseBuilding.BuildingState.Normal || b.Power == null || !b.Power.HasPower || b.Upgrades == null) continue;
                    powered++;
                    foreach (UpgradeData u in watched) if (b.Upgrades.HasUpgrade(u)) Increment(counts, u.id.Value);
                }
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, int> pair in counts) parts.Add(pair.Key + "=" + pair.Value);
                parts.Add("powered=" + powered);
                return string.Join(";", parts.ToArray());
            }
            catch (Exception e)
            {
                ReportOnce("upgraded-defenses", e);
                return "";
            }
        }

        // v0.16 item 1: may this project go into this city now?
        private static bool ProjectAllowed(AIConstruction construction, DirectorState d, ProjectData p, BaseBuilding city)
        {
            if (p == null || city == null) return false;
            if (p.id.Value == JammingProject)
                return SlotPolicy.JammingAllowed(d.JammingActive, Plugin.JammingMax.Value, d.RemainingPlanScience, Plugin.JammingMinPlanScience.Value);
            // v0.16.1: Jamming still wanted (EMC done, research left, under the ceiling) keeps its slots from other projects.
            bool researchLeft = SlotPolicy.JammingAllowed(d.JammingActive, Plugin.JammingMax.Value, d.RemainingPlanScience, Plugin.JammingMinPlanScience.Value);
            int jammingNeeded = Math.Min(Math.Max(0, Plugin.JammingMax.Value - d.JammingActive), d.JammingEligibleCities); // Terra #4: placeable copies
            bool leavesJamming = SlotPolicy.LeavesRoom(d.EmcDone && researchLeft, jammingNeeded, d.ProjectSlotsFree);
            // Terra v0.16.1 #1: Science Institutes (cap-exempt) keep the Jamming room before EMC too.
            bool leavesJammingAlways = SlotPolicy.LeavesRoom(researchLeft, jammingNeeded, d.ProjectSlotsFree);
            if (!IsPermanent(p))
            {
                if (p.id.Value == SstarProject) return true; // SSTAR shares the priority (Jamming into SSTAR)
                if (p.id.Value == RareMaterialProject)
                {
                    // v0.17: build-up only, ending by the war (Pavol's timing), then SSTAR; still leaves the Jamming room.
                    int duration = p.duration != null && p.duration.IsValueSet ? p.duration.Value : 6;
                    if (!SlotPolicy.RareMaterialAllowed(d.RareMaterialActive, Plugin.RareMaterialMax.Value, d.Turn, duration, d.WarTurn,
                        Plugin.RareMaterialEndLead.Value, d.War)) return false;
                }
                return leavesJamming;
            }
            // v0.16.1: a permanent science project skips the opening cap (match 17: half the human's science).
            bool science = Plugin.OpeningCapExemptScience.Value && FloatValue(p.turn_yield_science_multiplier, 1f) > 1f;
            if (science)
                return leavesJammingAlways && (!d.LosslessDone || SlotPolicy.PermanentAllowed(true, d.PermanentProjects, Plugin.OpeningPermanentCap.Value,
                    CityFreeSlots(construction, city), Plugin.ProjectSlotReserve.Value));
            return SlotPolicy.PermanentAllowed(d.LosslessDone, d.PermanentProjects, Plugin.OpeningPermanentCap.Value,
                CityFreeSlots(construction, city), Plugin.ProjectSlotReserve.Value);
        }

        // v0.15.1 (Sol v0.15 #6, v0.14.2 #17): the living city a project goes to, with the native checks (free slot, not
        // installed, not queued, prerequisite projects installed). QueueProject enqueues against this exact city: the
        // native search walks every city of the type without a state check and can pick a ruin, which still charges.
        private static BaseBuilding CityForProject(AIConstruction construction, AICore core, AIProjectEntity project, DirectorState d)
        {
            foreach (BaseBuilding city in core.PlayerData.BuildingHandler.GetBuildingsByID(project.City.id.Value))
            {
                if (city == null || city.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                if (!construction.Projects.HasCityFreeSlots(city)) continue;
                if (construction.Projects.HasCityProject(city, project.Project)) continue;
                if (construction.ConstructionQueue.IsProjectInQueue(project.Project, city)) continue;
                bool prerequisites = true;
                if (project.OtherProjects != null)
                {
                    foreach (ProjectData other in project.OtherProjects)
                    {
                        if (!construction.Projects.HasCityProject(city, other)) { prerequisites = false; break; }
                    }
                }
                if (!prerequisites) continue;
                if (d != null && !ProjectAllowed(construction, d, project.Project, city)) continue; // v0.16 item 1
                return city;
            }
            return null;
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
                if (data == null || !available.IsBuildingAvailable(data) || Blocked(d, id)) continue;
                if (available.GetLimitedBuildingConstructionsLeft(data) == 0) continue;
                bool isPlant = data.distribute_power.IsValueSet && data.distribute_power.Value;
                bool waterOnly = IsWaterOnly(data);
                if (waterOnly && d.Turn < d.WaterCooldownUntil) continue;
                if (waterOnly && !isPlant && !HasWaterAnchor(core)) continue; // an offshore mine needs a powered water neighbour
                float materialGain = Value(data.turn_yield_material) * globalMaterial * planetFactor - Value(data.turn_cost_material);
                if (!isPlant && Value(data.turn_yield_material) > 0 && me.ResourceHandler.PlanetMaterial <= 0) continue;
                // v0.16.1: extraction only up to what the planet sustains to the material horizon (match 17: 86 Offshore Mines at T22).
                float buildingDrainAdd = Value(data.turn_cost_planet_material) * me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnCostPlanetMaterial);
                if (Plugin.PlanetDrainGuard.Value && !isPlant && buildingDrainAdd > 0f
                    && !PlanetGuard.DrainAllowed(d.PlanetDrain, buildingDrainAdd, 0, d.PlanetPool, Plugin.PlanetGuardHorizonTurn.Value - d.Turn))
                {
                    d.ExtractionBlocked++;
                    continue;
                }
                options.Add(new EconOption
                {
                    Id = id,
                    Kind = isPlant ? OptionKind.Plant : OptionKind.Producer,
                    PlanetDrainAdd = isPlant ? 0f : buildingDrainAdd,
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
                if (data != null && available.IsBuildingAvailable(data) && Affordable(core, data) && !Blocked(d, id)) return data;
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
            if (data != null && available.IsBuildingAvailable(data) && Affordable(core, data) && !Blocked(d, "bui_FloatingPlant")) return data;
            return null;
        }

        // ---- queue primitives ----------------------------------------------------------------------

        // A type whose placement gave up this turn, or (v0.11) one throttled because its placements keep failing: placements
        // resolve after many are queued, so a full planet saw 22-55 of one type queued and abandoned every turn (v0.10.1
        // Maverick T24-T28). A type abandoned 3+ times last turn is queued at most twice this turn. Candidate filters use
        // this too, so a throttled type is skipped before it is picked (Terra v0.11 #4).
        private static bool Blocked(DirectorState d, string id)
        {
            if (d.FailedThisTurn.Contains(id)) return true;
            int abandoned, queuedNow;
            d.AbandonedLastTurn.TryGetValue(id, out abandoned);
            d.QueuedById.TryGetValue(id, out queuedNow);
            bool backoff = PlacementBackoff.Throttled(d.BackoffUntil, id, d.Turn);
            return (abandoned >= 3 || backoff) && queuedNow >= 2;
        }

        private static Dictionary<string, int> NextBackoff(DirectorState last, int turns)
        {
            if (last == null) return new Dictionary<string, int>();
            return PlacementBackoff.Next(last.BackoffUntil, last.PlacementFailures, last.QueuedById, last.Turn, turns);
        }

        private static bool QueueBuilding(AIConstruction construction, AICore core, DirectorState d, BuildingData data, string role)
        {
            // A type whose placement gave up this turn is not queued again (v0.3.1: 241 Railgun searches requeued).
            if (data == null || Blocked(d, data.id.Value) || !Affordable(core, data)) return false;
            bool needsSlot = !(data.distribute_power.IsValueSet && data.distribute_power.Value);
            if (needsSlot && d.SlotsLeft <= 0) return false;
            construction.ConstructionQueue.AddBuildingToQueue(data); // charges core.Resources
            if (needsSlot)
            {
                d.SlotsLeft--;
                construction.PowerSlotNeed++; // keep the native bookkeeping consistent
            }
            else
            {
                d.PlantsQueued++; // v0.14.1: slot-starved plant limit (EconomyDirector.SlotStarved)
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
            BaseBuilding city = CityForProject(construction, core, project, d);
            if (city == null) return false;
            int before = QueueCount(construction);
            construction.ConstructionQueue.AddProjectToQueue(project.Project, city); // v0.15.1: the validated living city
            if (QueueCount(construction) <= before) return false;
            core.Resources.Energy -= project.EnergyCost;
            core.Resources.Material -= project.MaterialCost;
            // v0.16 item 1: queued projects count against the slot policy for the rest of the turn.
            int freeBefore = CityFreeSlots(construction, city) + 1; // the queue now holds this project
            if (IsPermanent(project.Project)) d.PermanentProjects++;
            if (project.Project.id.Value == JammingProject) { d.JammingActive++; d.JammingEligibleCities = Math.Max(0, d.JammingEligibleCities - 1); }
            if (project.Project.id.Value == SstarProject) d.SstarActive++;
            if (project.Project.id.Value == RareMaterialProject) d.RareMaterialActive++;
            d.ProjectSlotsFree = Math.Max(0, d.ProjectSlotsFree - 1); // v0.16.1: the Jamming reservation reads this
            Plugin.WriteDecision(new Json().Str("e", "project").Int("t", d.Turn).Str("ai", core.PlayerData.UserNickName)
                .Str("project", project.Project.id.Value).Str("city", city.ObjectId.ToString()).Bool("permanent", IsPermanent(project.Project))
                .Int("cityFreeBefore", freeBefore).Int("reserve", Plugin.ProjectSlotReserve.Value).Bool("lossless", d.LosslessDone)
                .Int("permanentsAfter", d.PermanentProjects).Int("jammingAfter", d.JammingActive).Int("sstarAfter", d.SstarActive).ToString());
            Record(d, "project:" + project.Project.id.Value);
            return true;
        }

        private static List<string> IdList(string csv)
        {
            List<string> ids = new List<string>();
            if (string.IsNullOrEmpty(csv)) return ids;
            foreach (string part in csv.Split(','))
            {
                string id = part.Trim();
                if (id.Length > 0) ids.Add(id);
            }
            return ids;
        }

        // v0.16 item 6: the configured defense upgrades, in order, on working defenses that can take them.
        private static bool TryDefenseUpgrade(AIConstruction construction, AICore core, DirectorState d)
        {
            UpgradesData upgrades = GameDataFileManager.GetDataFile<UpgradesData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            foreach (string upgradeId in IdList(Plugin.DefenseUpgradeIds.Value))
            {
                UpgradeData upgrade = upgrades.GetUpgrade(upgradeId);
                if (upgrade == null || !available.IsUpgradeAvailable(upgrade)) continue;
                int material = Value(upgrade.build_cost_material);
                if (core.Resources.Material - material < d.MaterialReserve) continue;
                foreach (BaseBuilding b in core.PlayerData.BuildingHandler.Buildings)
                {
                    if (b == null || b.IsCity || b.BuildingData == null || !b.BuildingData.defense_functionality.IsValueSet) continue;
                    if (b.CurrentState != BaseBuilding.BuildingState.Normal || b.Upgrades == null || b.Upgrades.HasUpgrade(upgrade)) continue;
                    if (b.Power == null || !b.Power.HasPower) continue; // v0.16 (Sol v0.16 M6)
                    if (d.PlannedUpgradeKeys.Contains(b.ObjectId + ":" + upgradeId)) continue;
                    if (!upgrades.GetAvailableBuildingUpgrades(b.BuildingData.id.Value).Contains(upgrade)) continue;
                    if (QueueUpgrade(construction, core, d, b, upgrade))
                    {
                        d.DefenseUpgradesThisTurn++;
                        return true;
                    }
                }
            }
            return false;
        }

        // v0.16 item 4 (Pavol's P1, lane A's conditions logged in the strike records): while no enemy city is visible, the
        // blind artillery upgrades on our Railguns first, within the war firing cap (Triple Barrel fires at x2.8).
        private static bool TryBlindArtilleryUpgrade(AIConstruction construction, AICore core, DirectorState d, float upkeepCap)
        {
            UpgradesData upgrades = GameDataFileManager.GetDataFile<UpgradesData>();
            PlayerAvailableBuildings available = core.PlayerData.BuildingHandler.AvailableBuildings;
            float materialCap = Plugin.WeaponUpgradeShare.Value * d.TurnStartMaterial;
            foreach (string upgradeId in IdList(Plugin.BlindArtilleryUpgrades.Value))
            {
                UpgradeData upgrade = upgrades.GetUpgrade(upgradeId);
                if (upgrade == null || !available.IsUpgradeAvailable(upgrade)) continue;
                int material = Value(upgrade.build_cost_material);
                if (d.WeaponUpgradeMaterial + material > materialCap) continue;
                float firingMul = FloatValue(upgrade.firing_cost_energy_multiplier, 1f);
                foreach (WeaponGameplayData w in core.PlayerData.WeaponHandler.Weapons)
                {
                    BaseBuilding b = w != null ? w.Building : null;
                    if (b == null || b.BuildingData == null || b.CurrentState != BaseBuilding.BuildingState.Normal || b.Upgrades == null) continue;
                    if (b.Upgrades.HasUpgrade(upgrade) || d.PlannedUpgradeKeys.Contains(b.ObjectId + ":" + upgradeId)) continue;
                    if (!upgrades.GetAvailableBuildingUpgrades(b.BuildingData.id.Value).Contains(upgrade)) continue;
                    float live = b.Stats != null && b.Stats.Weapons != null ? b.Stats.Weapons.EnergyFiringCost : 0f;
                    float delta = live * (firingMul - 1f);
                    if (delta > 0f && d.Upkeep + delta > upkeepCap) continue;
                    if (QueueUpgrade(construction, core, d, b, upgrade))
                    {
                        List<UpgradeEffect> planned; // v0.16 (Sol v0.16 M5): same bookkeeping as the generic planner
                        if (!d.PlannedWeaponEffects.TryGetValue(b, out planned)) { planned = new List<UpgradeEffect>(); d.PlannedWeaponEffects[b] = planned; }
                        planned.Add(EffectOf(upgrade));
                        d.Upkeep += Math.Max(0f, delta);
                        d.WeaponUpgradesThisTurn++;
                        d.WeaponUpgradeMaterial += material;
                        return true;
                    }
                }
            }
            return false;
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
            // v0.15.2 anti-missile search diagnostics: candidates tried and why each failed.
            public int Tries, Invalid, Unplaced, NoAnchor, NotClosing;
            public int Deficient, Anchors; // v0.15.3 (Sol v0.15.2 #8): cities below target, anchors with a free slot
            public readonly int SearchId = ++s_coverSearchIds; // v0.16 item 6: one terminal log record per search
            public bool Logged;
        }

        private static int s_coverSearchIds;

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
            public readonly List<bool> IsCity = new List<bool>(); // v0.15.1
            public int PerCity;      // v0.15.1: anti-missile target per city; a placement must close a city below it
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
                m.IsCity.Add(b.IsCity);
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
                m.IsCity.Add(false);
                BuildingData queued = buildings.HasBuilding(action.BuildingID) ? buildings.GetBuilding(action.BuildingID) : null;
                m.Values.Add(queued != null && queued.defense_functionality.IsValueSet ? 0.25f : 1f);
                if (action.BuildingID == id)
                {
                    defenses.Add(point);
                    radii.Add(m.Radius);
                }
            }
            m.Counts = DefenseCoverage.CoverCounts(m.Targets, defenses, radii, m.Loss);
            if (antiNuke)
            {
                // v0.15.1 (Sol v0.15 #2): anchors and scores lean on the cities still below the target; everything else
                // only breaks ties. The step then refuses any spot that closes no city deficit.
                m.PerCity = Math.Max(1, ds.AntiNukePerCityNow);
                for (int i = 0; i < m.Targets.Count; i++)
                {
                    if (!(m.IsCity[i] && m.Counts[i] < m.PerCity)) m.Values[i] *= 0.05f;
                }
            }
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
                if (model.AntiNuke) LogCoverSearch(core, st, "gave-up");
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
            if (model.AntiNuke) return AntiNukeStep(search, data, core, model, st, bases);

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
                if (model.AntiNuke && !DefenseCoverage.ClosesCityDeficit(new Point(at.x, at.y, at.z), model.Targets, model.Loss, model.IsCity, model.Counts, model.Radius, model.PerCity))
                    continue; // v0.15.1 (Sol v0.15 #2): a city-cover purchase must cover an under-covered city or be refunded
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

        // v0.15.2: city-cover step. v0.15.1 sampled around power anchors in random directions and accepted only spots that
        // closed a city deficit; in the Thistle match every Ion search from T26 to T33 was refunded and no city ever had cover.
        // Now the candidates are drawn on the ring around a city still below the target (CityCoverRing), and the spot is
        // then connected to the nearest anchor with a free slot inside the game's power-line rule.
        private static bool AntiNukeStep(AIBuildingPlacementSearch search, BuildingData data, AICore core, CoverageModel model, CoverageState st,
            List<BaseBuilding> bases)
        {
            List<int> deficient = new List<int>();
            for (int i = 0; i < model.Targets.Count; i++)
            {
                if (model.IsCity[i] && model.Counts[i] < model.PerCity) deficient.Add(i);
            }
            st.Deficient = deficient.Count;
            st.Anchors = bases.Count;
            if (deficient.Count == 0)
            {
                LogCoverSearch(core, st, "no-deficit");
                AbandonSoon(search); // every city already covered (an earlier search closed it): refund
                return true;
            }
            float scale = data.building_scale.IsValueSet ? data.building_scale.Value : 1f;
            float ownRadius = (data.building_radius.IsValueSet ? data.building_radius.Value : 1f) * scale;
            float ownReach = data.power_connector_range.IsValueSet ? data.power_connector_range.Value : 0f;
            Vector3 planetCentre = core.Planet.transform.position;
            float bestScore = -1f;
            BuildingPlacementResult bestResult = null;
            BaseBuilding bestAnchor = null;
            for (int n = 0; n < CoverageCandidatesPerStep; n++)
            {
                int ci = deficient[Plugin.Rng.Next(deficient.Count)];
                Point cp = model.Targets[ci];
                Vector3 cityPos = new Vector3(cp.X, cp.Y, cp.Z);
                float inner, outer;
                DefenseCoverage.CityCoverRing(model.Radius, ownRadius, 2f * model.Loss[ci], 0.1f, out inner, out outer);
                if (outer <= inner) continue; // this defense cannot protect this city from anywhere
                Vector3 up = (cityPos - planetCentre).normalized;
                Vector3 world;
                Ray ray;
                BaseBuilding nearAnchor = Plugin.AnchorRingShare.Value > 0f && Plugin.Rng.NextDouble() < Plugin.AnchorRingShare.Value
                    ? RingAnchor(bases, cityPos, outer, ownRadius, ownReach) : null;
                if (nearAnchor != null)
                {
                    // v0.17: around a power anchor that can reach the ring (match 20: about half the ring spots had no anchor
                    // in line range, and 5 of 9 city-cover searches gave up). ClosesCityDeficit below still decides.
                    float aInner = ownRadius + nearAnchor.Radius;
                    float aOuter = Math.Max(aInner, (ownRadius + nearAnchor.Radius + Math.Max(ownReach, nearAnchor.Power.PowerConnectorRange)) * 0.9f);
                    float aDistance = aInner + (float)Plugin.Rng.NextDouble() * (aOuter - aInner);
                    float aAngle = (float)(Plugin.Rng.NextDouble() * Math.PI * 2.0);
                    world = nearAnchor.transform.TransformPoint(new Vector3((float)Math.Cos(aAngle), 0f, (float)Math.Sin(aAngle)) * aDistance);
                    Vector3 down = (planetCentre - world).normalized;
                    world += -down * 2f;
                    ray = new Ray(world, down);
                }
                else
                {
                    Vector3 t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    Vector3 t2 = Vector3.Cross(up, t1);
                    float angle = (float)(Plugin.Rng.NextDouble() * Math.PI * 2.0);
                    float distance = inner + (float)Plugin.Rng.NextDouble() * (outer - inner);
                    world = cityPos + (t1 * Mathf.Cos(angle) + t2 * Mathf.Sin(angle)) * distance + up * 2f;
                    ray = new Ray(world, -up);
                }
                st.Tries++;
                s_candidatesThisFrame++;
                if (!core.Construction.IsBuildingPlacementValid(data, core.Planet, ray)) { st.Invalid++; continue; }
                BuildingPlacementResult placed = core.PlayerData.BuildingHandler.TryPlaceBuilding(data, ray, core.Planet, null);
                if (placed == null || !placed.IsSuccessful) { st.Unplaced++; continue; }
                Vector3 at = placed.BuildingWorldPosition;
                if (!DefenseCoverage.ClosesCityDeficit(new Point(at.x, at.y, at.z), model.Targets, model.Loss, model.IsCity, model.Counts, model.Radius, model.PerCity))
                {
                    st.NotClosing++;
                    continue;
                }
                BaseBuilding anchor = null;
                float anchorDistance = float.MaxValue;
                foreach (BaseBuilding b in bases)
                {
                    float lineLimit = ownRadius + b.Radius + Math.Max(ownReach, b.Power.PowerConnectorRange);
                    float dist = Vector3.Distance(at, b.transform.position);
                    if (dist <= lineLimit && dist < anchorDistance) { anchor = b; anchorDistance = dist; }
                }
                if (anchor == null) { st.NoAnchor++; continue; }
                float score = DefenseCoverage.MarginalValue(new Point(at.x, at.y, at.z), model.Targets, model.Values, model.Counts, model.Radius, model.P, model.Loss);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestResult = placed;
                    bestAnchor = anchor;
                }
            }
            if (bestResult == null) return true; // nothing legal and connected this step: try again next step

            AIBuildingPlacementResult result = (AIBuildingPlacementResult)s_searchResult.GetValue(search);
            result.Result = bestResult;
            result.PowerGridConnection = bestAnchor.Power;
            s_setSearchComplete.Invoke(search, new object[] { true });
            LogCoverSearch(core, st, "placed");
            return true;
        }

        // v0.17: a random power anchor whose line range reaches the city's cover ring (within outer + its line limit of the
        // city), or null.
        private static BaseBuilding RingAnchor(List<BaseBuilding> bases, Vector3 cityPos, float outer, float ownRadius, float ownReach)
        {
            List<BaseBuilding> near = new List<BaseBuilding>();
            foreach (BaseBuilding b in bases)
            {
                float lineLimit = ownRadius + b.Radius + Math.Max(ownReach, b.Power.PowerConnectorRange);
                if (Vector3.Distance(b.transform.position, cityPos) <= outer + lineLimit) near.Add(b);
            }
            return near.Count > 0 ? near[Plugin.Rng.Next(near.Count)] : null;
        }

        private static void LogCoverSearch(AICore core, CoverageState st, string outcome)
        {
            try
            {
                if (st.Logged) return; // v0.16 item 6 (council B6 gate): exactly one terminal result per search
                st.Logged = true;
                Plugin.WriteDecision(new Json().Str("e", "cover-search").Int("t", GameManager.CurrentTurn).Str("ai", core.PlayerData.UserNickName)
                    .Int("searchId", st.SearchId).Str("outcome", outcome).Int("steps", st.Steps).Int("tries", st.Tries).Int("invalid", st.Invalid).Int("unplaced", st.Unplaced)
                    .Int("notClosing", st.NotClosing).Int("noAnchor", st.NoAnchor).Int("deficient", st.Deficient).Int("anchors", st.Anchors).ToString());
            }
            catch (Exception e)
            {
                ReportOnce("cover-search-log", e);
            }
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

        // ---- v0.17 shelter placement (match 20: Pavol's layout) ------------------------------------------------------
        // Every non-defense building goes inside existing defense cover when a covered, legal, connected spot exists (Brain/
        // Coverage.cs Shelter); with no defenses yet, as close to a city as possible. The anchors are the game's own (the
        // buildings this one would connect to), so power connection works as natively. Water buildings sampled around any
        // own building end up on the shoreline next to the land base instead of on random open water (v0.6 plants) or at
        // the end of an offshore chain. After ShelterCoveredSteps steps without cover, the best uncovered spot is taken;
        // after twice that with no legal spot at all, the old placement runs.
        private static readonly MethodInfo s_findNeedPower = AccessTools.Method(typeof(AIBuildingPlacementSearch), "findBuildingsThatNeedPower");
        private static readonly MethodInfo s_findWithPower = AccessTools.Method(typeof(AIBuildingPlacementSearch), "findBuildingsWithAvailablePower");
        private static readonly Dictionary<AIBuildingPlacementSearch, int> s_shelterSteps = new Dictionary<AIBuildingPlacementSearch, int>();

        private sealed class ShelterModel
        {
            public readonly List<Point> Defenses = new List<Point>();
            public readonly List<float> Radii = new List<float>();
            public readonly List<Point> Cities = new List<Point>();
            public readonly List<float> CityRadii = new List<float>();
            public float RefRadius;
        }

        // Terra v0.17 M3: one snapshot per AI per frame (several searches step in the same frame), and a candidate budget of
        // its own so shelter searches never use up the defense searches' budget.
        private const int ShelterCandidatesPerFrame = 60;
        private static int s_shelterFrame = -1;
        private static int s_shelterThisFrame;
        private static readonly Dictionary<int, ShelterModel> s_shelterModels = new Dictionary<int, ShelterModel>();

        private static ShelterModel ShelterModelFor(AICore core)
        {
            if (Time.frameCount != s_shelterFrame)
            {
                s_shelterFrame = Time.frameCount;
                s_shelterThisFrame = 0;
                s_shelterModels.Clear();
            }
            ShelterModel m;
            if (!s_shelterModels.TryGetValue(core.PlayerData.PlayerID, out m))
            {
                m = BuildShelterModel(core);
                s_shelterModels[core.PlayerData.PlayerID] = m;
            }
            return m;
        }

        private static ShelterModel BuildShelterModel(AICore core)
        {
            ShelterModel m = new ShelterModel();
            BuildingsData buildings = GameDataFileManager.GetDataFile<BuildingsData>();
            foreach (BaseBuilding b in core.PlayerData.BuildingHandler.Buildings)
            {
                if (b == null || b.CurrentState == BaseBuilding.BuildingState.Ruins || b.CurrentState == BaseBuilding.BuildingState.Destroyed) continue;
                Vector3 p = b.transform.position;
                if (b.IsCity) { m.Cities.Add(new Point(p.x, p.y, p.z)); m.CityRadii.Add(b.Radius); continue; }
                if (b.BuildingData == null || !b.BuildingData.defense_functionality.IsValueSet) continue;
                float r = b.Stats != null && b.Stats.Defenses != null ? b.Stats.Defenses.Radius : 0f;
                if (r <= 0f) r = DefenseRadius(b.BuildingData);
                if (r <= 0f) continue;
                m.Defenses.Add(new Point(p.x, p.y, p.z));
                m.Radii.Add(r);
            }
            foreach (AIConstructBuildingAction action in core.Actions.ConstructedBuildings)
            {
                if (action == null || action.BuildingPlacementResult == null || action.BuildingPlacementResult.Result == null) continue;
                BuildingData queued = buildings != null && buildings.HasBuilding(action.BuildingID) ? buildings.GetBuilding(action.BuildingID) : null;
                if (queued == null || !queued.defense_functionality.IsValueSet) continue;
                float r = DefenseRadius(queued);
                if (r <= 0f) continue;
                Vector3 p = action.BuildingPlacementResult.Result.BuildingWorldPosition;
                m.Defenses.Add(new Point(p.x, p.y, p.z));
                m.Radii.Add(r);
            }
            foreach (float r in m.Radii) m.RefRadius = Math.Max(m.RefRadius, r);
            if (m.RefRadius <= 0f && buildings != null && buildings.HasBuilding("bui_KineticDefense"))
                m.RefRadius = DefenseRadius(buildings.GetBuilding("bui_KineticDefense"));
            return m;
        }

        private static bool ShelterStep(AIBuildingPlacementSearch search, BuildingData data, AICore core)
        {
            try
            {
                return ShelterStepUnsafe(search, data, core);
            }
            catch (Exception e)
            {
                ReportOnce("shelter-placement", e);
                return false; // the old placement takes this step
            }
        }

        private static bool ShelterStepUnsafe(AIBuildingPlacementSearch search, BuildingData data, AICore core)
        {
            if (s_shelterSteps.Count > 300) s_shelterSteps.Clear(); // finished searches are never removed otherwise
            int steps;
            s_shelterSteps.TryGetValue(search, out steps);
            int coveredSteps = Math.Max(0, Plugin.ShelterCoveredSteps.Value);
            if (steps > 2 * coveredSteps + 2) return false; // nothing legal for many steps: the old placement from here on

            ShelterModel model = ShelterModelFor(core); // also resets the shelter budget on a new frame
            if (s_shelterThisFrame >= ShelterCandidatesPerFrame) return true; // wait for the next frame (not a step)

            bool water = IsWaterOnly(data);
            bool needsGrid = search.m_needsPowerGrid;
            List<BaseBuilding> all = core.PlayerData.BuildingHandler.Buildings;
            List<BaseBuilding> anchors = new List<BaseBuilding>();
            if (all.Count > 0) (needsGrid ? s_findWithPower : s_findNeedPower).Invoke(search, new object[] { all, anchors });
            // A plant connects to a building that needs power (native). A water plant keeps the v0.6 rule (no power line, so
            // it takes no anchor slot the director counted as free) and so does a land plant with nothing to power.
            bool connect = needsGrid || (!water && anchors.Count > 0);
            if (!connect)
            {
                anchors.Clear();
                foreach (BaseBuilding b in all)
                {
                    if (b != null && b.CurrentState == BaseBuilding.BuildingState.Normal) anchors.Add(b);
                }
            }
            if (anchors.Count == 0) return false; // native: a random spot

            s_shelterSteps[search] = ++steps;
            s_searchSteps.SetValue(search, (int)s_searchSteps.GetValue(search) + 1); // the abandon valve counts every step

            float scale = data.building_scale.IsValueSet ? data.building_scale.Value : 1f;
            float ownRadius = (data.building_radius.IsValueSet ? data.building_radius.Value : 1f) * scale;
            float ownReach = data.power_connector_range.IsValueSet ? data.power_connector_range.Value : 1000f;
            float looseReach = model.RefRadius > 0f ? model.RefRadius : 10f;
            int coverCap = Math.Max(1, Plugin.ShelterCoverCap.Value);
            float keepOut = Math.Max(0f, Plugin.ShelterCityKeepOut.Value) * looseReach;
            Vector3 planetCentre = core.Planet.transform.position;

            // Two thirds of the candidates around anchors that are themselves under cover (the base interior), the rest
            // around any anchor.
            List<BaseBuilding> covered = new List<BaseBuilding>();
            foreach (BaseBuilding b in anchors)
            {
                Vector3 p = b.transform.position;
                if (Shelter.CoverCount(new Point(p.x, p.y, p.z), model.Defenses, model.Radii) > 0) covered.Add(b);
            }

            float bestScore = -1f;
            int bestCover = 0;
            BuildingPlacementResult bestResult = null;
            BaseBuilding bestAnchor = null;
            for (int n = 0; n < CoverageCandidatesPerStep; n++)
            {
                List<BaseBuilding> pool = covered.Count > 0 && n % 3 != 2 ? covered : anchors;
                BaseBuilding anchor = pool[Plugin.Rng.Next(pool.Count)];
                float inner = ownRadius + anchor.Radius;
                float lineLimit = ownRadius + anchor.Radius + Math.Max(ownReach, anchor.Power.PowerConnectorRange);
                float outer = connect ? lineLimit * 0.9f : anchor.Radius + ownRadius + looseReach;
                if (outer < inner) outer = inner;
                float distance = inner + (float)Plugin.Rng.NextDouble() * (outer - inner);
                float angle = (float)(Plugin.Rng.NextDouble() * Math.PI * 2.0);
                Vector3 world = anchor.transform.TransformPoint(new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * distance);
                Vector3 down = (planetCentre - world).normalized;
                world += -down * 2f;
                Ray ray = new Ray(world, down);
                s_shelterThisFrame++;
                if (!core.Construction.IsBuildingPlacementValid(data, core.Planet, ray)) continue;
                BuildingPlacementResult placed = core.PlayerData.BuildingHandler.TryPlaceBuilding(data, ray, core.Planet, null);
                if (placed == null || !placed.IsSuccessful) continue;
                Vector3 at = placed.BuildingWorldPosition;
                if (connect && Vector3.Distance(at, anchor.transform.position) > lineLimit) continue; // the line would be refused
                Point atPoint = new Point(at.x, at.y, at.z);
                float cityEdge = Shelter.NearestEdge(atPoint, model.Cities, model.CityRadii);
                if (steps <= coveredSteps && Shelter.InKeepOut(cityEdge, keepOut)) continue; // the city's own defense ground
                int cover = Shelter.CoverCount(atPoint, model.Defenses, model.Radii);
                float score = Shelter.Score(cover, coverCap, cityEdge, 2f * looseReach);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCover = cover;
                    bestResult = placed;
                    bestAnchor = anchor;
                }
            }
            if (bestResult == null) return true; // nothing legal this step: try again next step
            if (!Shelter.Accept(bestCover, steps, coveredSteps, model.Defenses.Count)) return true;

            AIBuildingPlacementResult result = (AIBuildingPlacementResult)s_searchResult.GetValue(search);
            result.Result = bestResult;
            if (connect)
            {
                result.PowerGridConnection = bestAnchor.Power;
                if (!needsGrid && core.PowerGrid.IsBuildingRequiringPower(bestAnchor)) core.PowerGrid.TryRegisterBuildingPowered(bestAnchor); // as native
            }
            s_setSearchComplete.Invoke(search, new object[] { true });
            s_shelterSteps.Remove(search);
            CountShelter(core.PlayerData.PlayerID, bestCover > 0);
            return true;
        }

        // Per-turn tally for the director log: placements that landed under cover vs. not.
        private static readonly Dictionary<int, int[]> s_shelterTally = new Dictionary<int, int[]>();

        private static void CountShelter(int playerId, bool covered)
        {
            int[] t;
            if (!s_shelterTally.TryGetValue(playerId, out t))
            {
                t = new int[2];
                s_shelterTally[playerId] = t;
            }
            t[covered ? 0 : 1]++;
        }

        // Share of the AI's non-defense, non-city buildings under at least one own defense (the number the human's layout
        // maximises), and the same for cities.
        private static void CoveredShares(AICore core, out float buildings, out float cities)
        {
            ShelterModel model = BuildShelterModel(core);
            int n = 0, inside = 0, nc = 0, insideC = 0;
            foreach (BaseBuilding b in core.PlayerData.BuildingHandler.Buildings)
            {
                if (b == null || b.CurrentState == BaseBuilding.BuildingState.Ruins || b.CurrentState == BaseBuilding.BuildingState.Destroyed) continue;
                if (!b.IsCity && b.BuildingData != null && b.BuildingData.defense_functionality.IsValueSet) continue;
                Vector3 p = b.transform.position;
                bool under = Shelter.CoverCount(new Point(p.x, p.y, p.z), model.Defenses, model.Radii) > 0;
                if (b.IsCity) { nc++; if (under) insideC++; }
                else { n++; if (under) inside++; }
            }
            buildings = n > 0 ? (float)inside / n : 1f;
            cities = nc > 0 ? (float)insideC / nc : 1f;
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
                    if (defenseCore != null && Plugin.IsSmart(defenseCore.PlayerData) && CoverageStep(__instance, data, defenseCore)) return false;
                    return true;
                }
                if (data != null && Plugin.ShelterPlacement.Value && !data.defense_functionality.IsValueSet)
                {
                    AICore shelterCore = (AICore)s_searchCore.GetValue(__instance);
                    if (shelterCore != null && Plugin.IsSmart(shelterCore.PlayerData) && ShelterStep(__instance, data, shelterCore)) return false;
                }
                if (data == null || !IsWaterOnly(data)) return true;
                AICore core = (AICore)s_searchCore.GetValue(__instance);
                if (core == null || !Plugin.IsSmart(core.PlayerData)) return true;

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
                    if (core == null || !Plugin.IsSmart(core.PlayerData)) continue;
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
            else
            {
                d.PowerEnergySpent = Math.Max(0, d.PowerEnergySpent - energy); // a refunded plant was not spent on energy
                d.PlantsQueued = Math.Max(0, d.PlantsQueued - 1);
            }
            d.Refunds++;
            d.Reopened = true;
            Record(d, "abandoned:" + id);
            // v0.16.2 (Terra v0.16.2 M3): a refunded step-3d building gives its share of the extraction-first allowance back.
            int firstQueued;
            if (d.ExtractionFirstQueued.TryGetValue(id, out firstQueued) && firstQueued > 0)
            {
                d.ExtractionFirstQueued[id] = firstQueued - 1;
                d.ExtractionFirstMaterial = Math.Max(0, d.ExtractionFirstMaterial - material);
                d.ExtractionFirstBought = Math.Max(0, d.ExtractionFirstBought - 1);
                d.FailedThisTurn.Remove("extraction-first"); // another extraction type may still place (this one is in FailedThisTurn)
            }
            // Undo what the purchase was expected to change.
            PlayerData me = core.PlayerData;
            d.EnergyIncome -= Value(data.turn_yield_energy) * me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldEnergy);
            d.MaterialIncome -= Value(data.turn_yield_material) * me.StatHandler.GetGlobalStatModifier(GlobalStatModifierID.TurnYieldMaterial);
            if (data.weapon_functionality.IsValueSet)
            {
                d.Upkeep -= FiringCost(data);
                d.WeaponEnergySpent = Math.Max(0, d.WeaponEnergySpent - Value(data.build_cost_energy));
                if (IsAimedWeapon(data)) d.AimedWeapons = Math.Max(0, d.AimedWeapons - 1); // Sol v0.12 #7: no phantom cap slots
                d.FailedThisTurn.Add(id);            // this type found no place this turn; another type may
                d.FailedThisTurn.Remove("weapon");
                d.FailedThisTurn.Remove("weapon-sink");
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
            if (d.Turn != GameManager.CurrentTurn)
            {
                // v0.17 (match 20, Junker T15-T24): this turn's start never reached the director (AICore.BeginTurn threw);
                // say so instead of re-logging a stale turn as if it were this one.
                Plugin.WriteDecision(new Json().Str("e", "turn-start-missed").Int("t", GameManager.CurrentTurn).Str("ai", core.PlayerData.UserNickName)
                    .Int("lastStartedTurn", d.Turn).Int("energy", core.Resources.Energy).Int("material", core.Resources.Material).ToString());
                return;
            }
            d.EndEnergyLeft = core.Resources.Energy;
            d.EndMaterialLeft = core.Resources.Material; // v0.16.2
            d.Ended = true;
            int upgradesInstalled = 0;
            d.UnpoweredWeapons = 0;
            foreach (BaseBuilding building in core.PlayerData.BuildingHandler.Buildings)
            {
                try
                {
                    if (building != null && building.CurrentState == BaseBuilding.BuildingState.Normal && building.BuildingData != null
                        && building.BuildingData.weapon_functionality.IsValueSet && (building.Power == null || !building.Power.HasPower)) d.UnpoweredWeapons++;
                }
                catch (Exception e) { ReportOnce("unpowered-weapons", e); }
                if (building == null || building.IsCity || building.CurrentState != BaseBuilding.BuildingState.Normal) continue;
                upgradesInstalled += 2 - building.Upgrades.FreeSlots;
            }
            float coveredBuildings = -1f, coveredCities = -1f;
            try { CoveredShares(core, out coveredBuildings, out coveredCities); }
            catch (Exception e) { ReportOnce("covered-shares", e); }
            int[] shelter;
            if (!s_shelterTally.TryGetValue(core.PlayerData.PlayerID, out shelter)) shelter = new int[2];
            s_shelterTally.Remove(core.PlayerData.PlayerID);
            Plugin.WriteDecision(new Json().Str("e", "director").Int("t", d.Turn).Str("ai", core.PlayerData.UserNickName)
                .Num("coveredBuildings", coveredBuildings).Num("coveredCities", coveredCities) // v0.17
                .Int("shelteredPlaced", shelter[0]).Int("unshelteredPlaced", shelter[1])
                .Bool("war", d.War).Int("slotsAtStart", d.SlotsAtStart).Int("slotsLeft", d.SlotsLeft).Int("unpowered", d.Unpowered)
                .Bool("retaliationLatched", RetaliationOf(core.PlayerData).Latched).Int("retaliationLatchTurn", RetaliationOf(core.PlayerData).LatchTurn)
                .Int("retaliationQuiet", RetaliationOf(core.PlayerData).QuietTurns) // v0.20 (council I2)
                .Int("unpoweredWeapons", d.UnpoweredWeapons).Int("searchesOpenAtEnd", d.SearchesOpenAtEnd) // v0.20 (council I1a)
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
                .Num("sustainableMaterial", d.SustainableMaterial).Bool("energySaturated", d.EnergySaturated).Int("materialReserve", d.MaterialReserve)
                .Int("soldMines", d.SoldMines).Int("soldMaterial", d.SoldMaterial).Int("aimedWeapons", d.AimedWeapons)
                .Int("repaired", d.Repaired).Int("repairEnergy", d.RepairEnergy).Int("repairMaterial", d.RepairMaterial)
                .Int("refunds", d.Refunds).Map("placementFailures", d.PlacementFailures).Int("steps", d.Steps)
                .Int("visibleCities", d.VisibleEnemyCities).Int("ownIntel", d.OwnIntel).Bool("guidedThreat", d.GuidedThreat)
                // v0.16 gates (council v0.16 decision-record-r2 section 3)
                .Int("warTurn", d.WarTurn).Bool("lossless", d.LosslessDone).Int("permanentProjects", d.PermanentProjects)
                .Int("jamming", d.JammingActive).Int("sstar", d.SstarActive).Int("rareMaterial", d.RareMaterialActive)
                .Int("extractionBlocked", d.ExtractionBlocked).Int("intelBatchSize", MemoryOf(core.PlayerData.PlayerID).Intel.BatchSize).Int("projectSlotsFree", d.ProjectSlotsFree)
                .Bool("materialBound", d.MaterialBound).Int("extractionFirstMaterial", d.ExtractionFirstMaterial).Int("extractionFirstBought", d.ExtractionFirstBought) // v0.16.2
                .Int("projectSlotsTotal", d.ProjectSlotsTotal).Str("projectSlots", d.ProjectSlots).Int("permanentsBlocked", d.PermanentsBlocked)
                .Str("intelPolicy", d.IntelPolicy).Int("intelAllowed", d.IntelAllowed)
                .Num("intelSpent", MemoryOf(core.PlayerData.PlayerID).Intel.SpentEnergy).Num("intelCredited", MemoryOf(core.PlayerData.PlayerID).Intel.CreditedEnergy)
                .Int("intelFailedBatches", MemoryOf(core.PlayerData.PlayerID).Intel.FailedBatches).Str("intelVerdict", MemoryOf(core.PlayerData.PlayerID).Intel.LastVerdict)
                .Int("intelProbes", MemoryOf(core.PlayerData.PlayerID).Intel.Probes).Int("intelLidar", d.IntelLidar)
                .Int("intelUpgradesQueued", d.IntelUpgradesThisTurn).Int("intelBatchTurn", MemoryOf(core.PlayerData.PlayerID).Intel.BatchTurn)
                .Int("intelBatchBought", MemoryOf(core.PlayerData.PlayerID).Intel.BatchBought)
                .Bool("blind", d.Blind).Int("targetCandidates", d.TargetCandidates).Int("guidedRefused", d.GuidedRefused).Int("forwardDefenseNeed", d.ForwardDefenseNeed)
                .Int("repairHeld", d.RepairHeld).Int("reserveWanted", d.ReserveWanted)
                .Int("reserveShortfall", Math.Max(0, d.ReserveWanted - d.DefenseReserve)).Int("defenseUpgrades", d.DefenseUpgradesThisTurn)
                .Str("keyTechs", KeyTechs(core.PlayerData)).Str("upgradedDefenses", UpgradedDefenses(core.PlayerData))
                .Int("peakImpacts", MemoryOf(core.PlayerData.PlayerID).PeakImpacts)
                .Int("antiNukeProtecting", d.AntiNukeProtecting).Int("antiNukeWanted", d.AntiNukeWanted)
                .Int("minCityCover", d.MinCityCover).Int("antiNukeNearCities", d.AntiNukeNearCities).Int("antiNukePerCity", d.AntiNukePerCityNow)
                .Int("idleEnergy", core.Resources.Energy + d.DefenseReserve).Str("ownCityPop", OwnCityPopulations(core.PlayerData))
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
