using System;

namespace InterplanetaryAI.Brain
{
    // v0.16 (council v0.16, docs/reviews/2026-11-01-council-v016/decision-record-r2.md): the pure rules behind the six
    // items. Every input is the AI's own state or what it currently sees; nothing reads an enemy's hidden values
    // (counterintel, stealth, surviving-city count) or remembers a city once it leaves sight.

    // Item 1: city-project slots. Match 16 queued five permanent Molecular Recycling Plants by T7 and two Quantum
    // Encryption Hubs by T11; seven permanents filled the seven slots at T14 and the first SSTAR (energy x1.3 per copy)
    // came at T31, while the human stacked Jamming (science x1.25) and SSTAR in free slots.
    public static class SlotPolicy
    {
        // Before Lossless Energy Transfer (SSTAR) a permanent project counts against the opening cap. After it, a
        // permanent goes only into a city that keeps `reserve` free slots for temporary projects.
        // cityFreeSlots: that city's free slots, queued projects already taken off.
        public static bool PermanentAllowed(bool losslessDone, int permanentsOwnedOrQueued, int openingCap, int cityFreeSlots, int reserve)
        {
            if (!losslessDone) return openingCap <= 0 || permanentsOwnedOrQueued < openingCap;
            return cityFreeSlots - 1 >= Math.Max(0, reserve);
        }

        // v0.16.1 (match 17: five Rare Material copies took every free slot at T13, no Jamming until T16, science half the
        // human's): a temporary project other than Jamming / SSTAR must leave room for the Jamming copies still wanted.
        public static bool TemporaryAllowed(bool jammingWanted, int jammingActive, int jammingMax, int totalFreeSlots)
        {
            return LeavesRoom(jammingWanted, Math.Max(0, jammingMax - jammingActive), totalFreeSlots);
        }

        // Terra v0.16.1 #4: reserve only the Jamming copies that can still be placed (one per city: jammingNeeded =
        // min(ceiling - active, cities without Jamming)).
        public static bool LeavesRoom(bool jammingWanted, int jammingNeeded, int totalFreeSlots)
        {
            if (!jammingWanted) return true;
            return totalFreeSlots - 1 >= Math.Max(0, jammingNeeded);
        }

        // Jamming while useful research remains, up to `max` active or queued at once.
        public static bool JammingAllowed(int activeOrQueued, int max, float planScienceLeft, float minPlanScience)
        {
            return max > 0 && activeOrQueued < max && planScienceLeft >= minPlanScience;
        }

        // v0.17 (match 20): Rare Material Synthesis (material x1.3, energy x0.95, planet cost x1.3, 6 turns) in the build-up
        // only. Pavol stacked two from T16 to T21, ending as his war began, then flipped to SSTAR: "it's all about timing...
        // you'll need the energy, then you need to flip to lossless energy transfer." A copy may start only if it ends by
        // warTurn - endLead and the war has not started; up to `max` at once (match 17's five copies took the Jamming slots,
        // so the Jamming room rule still applies on top).
        public static bool RareMaterialAllowed(int activeOrQueued, int max, int turn, int duration, int warTurn, int endLead, bool warStarted)
        {
            if (warStarted || max <= 0 || activeOrQueued >= max) return false;
            return turn + Math.Max(1, duration) <= warTurn - Math.Max(0, endLead);
        }
    }

    // Item 4: war from WarFromTurn only with the means to fight (Orbital Assembly and the income to fire), else at the
    // fallback turn. A started war stays started (the adapter latches it).
    public static class WarStart
    {
        public static int Turn(int warFromTurn, int fallbackTurn, bool orbitalAssemblyDone, float energyIncome, float minIncome)
        {
            if (orbitalAssemblyDone && energyIncome >= minIncome) return warFromTurn;
            return Math.Max(warFromTurn, fallbackTurn);
        }
    }

    // Item 5: intel as a bounded bid judged by its own visibility results (Pavol's P2: no enemy city count).
    public sealed class IntelBidState
    {
        public float SpentEnergy;        // intel construction + upgrade energy so far
        public float CreditedEnergy;     // energy income credited so far (sum of turn incomes)
        public int BatchTurn = -1000;    // turn the open batch started
        public int BatchBought;          // purchases in the open batch (0 = none open)
        public int BatchVisibleBefore;   // enemy cities currently visible when the batch started
        public int BatchBlindBefore;     // enemies we could not read when the batch started
        public int FailedBatches;        // judged batches in a row that showed nothing new
        public bool Stopped;             // stop-loss tripped
        public int Probes;               // single purchases after the stop-loss
        public int LastProbeTurn = -1000;
        public string LastVerdict = "";
        public int BatchSize;            // v0.16.1: this batch's size (budget-scaled)
    }

    // v0.16.1 (match 17: 86 Offshore Mines in one turn at T22 and Rare Material x3 drained ~50k a turn; the planet went
    // 235k -> 72k in eight turns): extraction may grow only up to what the planet can sustain to the material horizon.
    public static class PlanetGuard
    {
        public static bool ExtractionAllowed(float materialIncome, float materialGain, float sustainableIncome)
        {
            if (sustainableIncome < 0f || materialGain <= 0f) return true;
            return materialIncome + materialGain <= sustainableIncome;
        }

        // Terra v0.16.1 #2: the game drains max(planet cost, production) from the planet pool. Extra planet cost (a mine, a
        // yield upgrade, a project's multiplier) is allowed only if today's drain to the guard horizon plus the extra drain
        // for its duration (0 = permanent) fits in the remaining planet pool. The bank is not planet supply. -1 pool =
        // unlimited planet. After losses the drain falls and room returns by itself.
        public static bool DrainAllowed(float drain, float extraDrain, int extraTurns, int planetPool, int turnsLeft)
        {
            if (planetPool < 0 || extraDrain <= 0f) return true;
            int left = Math.Max(1, turnsLeft);
            int dur = extraTurns <= 0 ? left : Math.Min(extraTurns, left);
            return Math.Max(0f, drain) * left + extraDrain * dur <= planetPool;
        }
    }

    public static class IntelBudget
    {
        public const int ProbeGapTurns = 3;

        // Judge a batch `evalTurns` after it started: success when more enemy cities are visible now or fewer enemies
        // are unreadable. A success clears the failure run (and a stop-loss: a probe found something).
        // v0.16.1 (match 17: the stop-loss tripped after 1,000 E of a ~27,000 E budget): the stop needs both the failure run
        // and at least stopMinUse of the budget spent. budgetShare 0 = no spend condition (v0.16 behaviour).
        public static void Evaluate(IntelBidState s, int turn, int visibleCities, int blindEnemies, int evalTurns, int failedBatchesStop,
            float budgetShare = 0f, float stopMinUse = 0f)
        {
            if (s.BatchBought <= 0 || turn < s.BatchTurn + Math.Max(1, evalTurns)) return;
            bool success = visibleCities > s.BatchVisibleBefore || blindEnemies < s.BatchBlindBefore;
            if (success)
            {
                s.FailedBatches = 0;
                s.Stopped = false;
                s.LastVerdict = "success";
            }
            else
            {
                s.FailedBatches++;
                bool spentEnough = budgetShare <= 0f || s.SpentEnergy >= stopMinUse * budgetShare * s.CreditedEnergy;
                // Terra v0.16.1 #3: twice the failure run stops regardless of spend, so the stop-loss is always reachable.
                if (failedBatchesStop > 0 && s.FailedBatches >= failedBatchesStop && (spentEnough || s.FailedBatches >= 2 * failedBatchesStop))
                    s.Stopped = true;
                s.LastVerdict = s.Stopped ? "stop-loss" : "failed";
            }
            s.BatchBought = 0;
        }

        // Intel purchases allowed now for vision (native minimum intel is separate). wantVision: no enemy city visible
        // now, or an enemy we cannot read.
        // v0.16.1: with unitCost > 0 a batch is sized to the budget room (room / unitCost), capped at `batch`: a 3-array batch
        // could never close a 3-5x counterintel gap, so it failed by construction (match 17).
        public static int BatchSize(IntelBidState s, float budgetShare, float unitCost, int batchMax)
        {
            if (unitCost <= 0f || budgetShare <= 0f) return batchMax;
            int fit = (int)Math.Floor((budgetShare * s.CreditedEnergy - s.SpentEnergy) / unitCost);
            return Math.Max(1, Math.Min(batchMax, fit));
        }

        public static int Allowed(IntelBidState s, int turn, bool wantVision, int bidFromTurn, float budgetShare, float nextCost,
            int batch, int evalTurns, int probeMax, float unitCost = 0f)
        {
            if (!wantVision || turn < bidFromTurn || batch <= 0) return 0;
            if (s.BatchBought == 0 || s.BatchTurn != turn) s.BatchSize = BatchSize(s, budgetShare, unitCost, batch);
            if (s.BatchSize > 0) batch = s.BatchSize;
            if (budgetShare > 0f && s.SpentEnergy + nextCost > budgetShare * s.CreditedEnergy) return 0;
            if (s.BatchBought > 0)
            {
                if (s.BatchTurn == turn) return Math.Max(0, (s.Stopped ? 1 : batch) - s.BatchBought);
                if (turn < s.BatchTurn + Math.Max(1, evalTurns)) return 0; // wait for the open batch's result
            }
            if (s.Stopped) return s.Probes < probeMax && turn >= s.LastProbeTurn + ProbeGapTurns ? 1 : 0;
            return batch;
        }

        // v0.16 (Sol v0.16 F5): one spend policy for every intel purchase. Native minimum intel skips the bid's timing and
        // batches but not the budget share.
        public static bool BudgetRoom(IntelBidState s, float budgetShare, float cost)
        {
            return budgetShare <= 0f || s.SpentEnergy + Math.Max(0f, cost) <= budgetShare * s.CreditedEnergy;
        }

        // Spend outside a bid batch (native minimum): counted against the budget, never opens or restarts a batch.
        public static void RecordSpend(IntelBidState s, float energy)
        {
            s.SpentEnergy += Math.Max(0f, energy);
        }

        public static void Record(IntelBidState s, int turn, float energy, int visibleCities, int blindEnemies)
        {
            s.SpentEnergy += Math.Max(0f, energy);
            if (s.BatchBought == 0 || s.BatchTurn != turn)
            {
                s.BatchTurn = turn;
                s.BatchBought = 0;
                s.BatchVisibleBefore = visibleCities;
                s.BatchBlindBefore = blindEnemies;
                if (s.Stopped)
                {
                    s.Probes++;
                    s.LastProbeTurn = turn;
                }
            }
            s.BatchBought++;
        }
    }

    // Item 6: once enemy fire has landed, ask for the rolls the largest volley seen would need, before it comes again
    // (the observed-need estimator lags one phase: T26 asked 1,250 E before the 83-impact volley). Bounded by the hard cap.
    public static class ForwardDefense
    {
        public static int Need(int peakImpacts, float eligibleShare, float rollCost, float margin, float energyIncome, float hardCapShare)
        {
            if (peakImpacts <= 0 || margin <= 0f || rollCost <= 0f) return 0;
            float share = Math.Max(0f, Math.Min(1f, eligibleShare));
            float need = peakImpacts * share * rollCost * margin;
            if (hardCapShare > 0f) need = Math.Min(need, hardCapShare * Math.Max(0f, energyIncome));
            return (int)Math.Round(Math.Max(0f, need));
        }
    }

    // Item 4 (S3): the visible-city opportunity that may fire below MinPassingShots. Both conditions hold (the
    // intersection the council recorded): lane A's floor (a city visible now, guided shots through, pass share) and
    // lane B's funded credible kill (guided population damage through reaches the smallest visible city's population,
    // and the guided firing fits the war firing budget).
    public static class PrecisionOpportunity
    {
        public static bool Holds(int visibleCities, float guidedPassing, float passShare, float guidedPopDamagePerShot,
            int smallestVisibleCityPop, bool funded, float minGuided, float minShare, float killShare)
        {
            if (visibleCities <= 0 || killShare <= 0f || !funded) return false;
            if (guidedPassing < minGuided || passShare < minShare) return false;
            if (smallestVisibleCityPop <= 0) return false;
            return guidedPassing * guidedPopDamagePerShot >= killShare * smallestVisibleCityPop;
        }
    }
}
