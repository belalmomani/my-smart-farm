using System;
using System.Collections.Generic;
using System.Linq;

namespace MySmartFarm.Core
{
    public class Scenario
    {
        public string Id, NameAr, NameEn, DescAr, DescEn;
        public double MortalityMult, FertilityMult, PriceMult, FeedCostMult;
    }
    public struct Range { public double Min, Median, Max; }
    public class ScenarioResult { public Scenario Scenario; public int Days, Runs; public double StartNetWorth; public Range NetWorthEnd, Change; }

    public static class Scenarios
    {
        /// <summary>Multipliers are illustrative game assumptions, not forecasts.</summary>
        public static readonly Scenario[] All =
        {
            new Scenario { Id = "optimistic", NameAr = "متفائل", NameEn = "Optimistic", DescAr = "نفوق أقل 40%، خصوبة +10%، أسعار البيع +10%، تكلفة العلف -10%", DescEn = "Mortality -40%, fertility +10%, sale prices +10%, feed cost -10%", MortalityMult = 0.6, FertilityMult = 1.1, PriceMult = 1.1, FeedCostMult = 0.9 },
            new Scenario { Id = "base", NameAr = "أساسي", NameEn = "Base", DescAr = "القيم الافتراضية كما هي", DescEn = "Default values unchanged", MortalityMult = 1, FertilityMult = 1, PriceMult = 1, FeedCostMult = 1 },
            new Scenario { Id = "pessimistic", NameAr = "متشائم", NameEn = "Pessimistic", DescAr = "نفوق أعلى 60%، خصوبة -15%، أسعار البيع -15%، تكلفة العلف +20%", DescEn = "Mortality +60%, fertility -15%, sale prices -15%, feed cost +20%", MortalityMult = 1.6, FertilityMult = 0.85, PriceMult = 0.85, FeedCostMult = 1.2 },
        };

        public static FarmState Clone(FarmState s)
        {
            var c = new FarmState
            {
                SchemaVersion = s.SchemaVersion, Seed = s.Seed, LastProcessedDay = s.LastProcessedDay, NextSeq = s.NextSeq, Cash = s.Cash, FertileEggs = s.FertileEggs, InfertileEggs = s.InfertileEggs,
                FarmType = s.FarmType, FarmName = s.FarmName, Currency = s.Currency, RandomEvents = s.RandomEvents
            };
            foreach (var a in s.Animals) c.Animals.Add(new Animal { Id = a.Id, SpeciesId = a.SpeciesId, Sex = a.Sex, BirthDay = a.BirthDay, Health = a.Health, DeficitDays = a.DeficitDays, PregnantUntilDay = a.PregnantUntilDay, LastBirthDay = a.LastBirthDay, Alive = a.Alive, DeathDay = a.DeathDay, CauseOfDeath = a.CauseOfDeath });
            foreach (var i in s.Stock) c.Stock.Add(new StockItem { Key = i.Key, Qty = i.Qty });
            foreach (var t in s.Ledger) c.Ledger.Add(new Transaction { Id = t.Id, Day = t.Day, Category = t.Category, Amount = t.Amount, Memo = t.Memo });
            foreach (var e in s.Events) c.Events.Add(new FarmEvent { Id = e.Id, Day = e.Day, Type = e.Type, SubjectId = e.SubjectId, Detail = e.Detail });
            foreach (var i in s.Incubations) c.Incubations.Add(new Incubation { Id = i.Id, SpeciesId = i.SpeciesId, Count = i.Count, HatchDay = i.HatchDay });
            foreach (var p in s.Prices) c.Prices.Add(new PriceEntry { Key = p.Key, Value = p.Value, IsExample = p.IsExample });
            return c;
        }

        static Range Summarize(List<double> v) { v.Sort(); return new Range { Min = v[0], Median = v[v.Count / 2], Max = v[v.Count - 1] }; }

        /// <summary>One simulated run: a simple auto-manager keeps ~2 weeks of feed/water (if cash allows) and sells infertile eggs daily. Returns end net worth.</summary>
        public static double RunOnce(FarmState start, Scenario sc, int days, int seed)
        {
            var st = Clone(start); st.Seed = seed;
            var f = new Farm(st) { MortalityMult = sc.MortalityMult, FertilityMult = sc.FertilityMult };
            double feedP = f.Price("feed_kg") * sc.FeedCostMult, waterP = f.Price("water_l"), eggP = f.Price("egg") * sc.PriceMult;
            for (int i = 0; i < days; i++)
            {
                int d = st.LastProcessedDay + 1; double nf, nw; f.NeedsPerDay(d, out nf, out nw);
                if (f.StockOf(Farm.Feed) < nf * 7) f.BuyStock(Farm.Feed, nf * 14, feedP, d, TxCategory.FeedPurchase);
                if (f.StockOf(Farm.Water) < nw * 7) f.BuyStock(Farm.Water, nw * 14, waterP, d, TxCategory.WaterPurchase);
                f.ProcessUntil(d);
                if (st.InfertileEggs > 0) f.SellEggs(st.InfertileEggs, false, eggP, d);
            }
            return f.NetWorth(sc.PriceMult);
        }

        /// <summary>Returns a RANGE (min/median/max over runs), never a single number. Not a guarantee of profit or a feasibility study.</summary>
        public static ScenarioResult Run(FarmState start, Scenario sc, int days, int runs, int seed)
        {
            var f0 = new Farm(Clone(start)); double startNw = f0.NetWorth();
            var ends = new List<double>(); for (int r = 0; r < runs; r++) ends.Add(RunOnce(start, sc, days, seed + r * 101));
            return Summarize2(sc, days, runs, startNw, ends);
        }
        public static ScenarioResult Summarize2(Scenario sc, int days, int runs, double startNw, List<double> ends)
        {
            var end = Summarize(new List<double>(ends));
            return new ScenarioResult { Scenario = sc, Days = days, Runs = runs, StartNetWorth = startNw, NetWorthEnd = end, Change = new Range { Min = end.Min - startNw, Median = end.Median - startNw, Max = end.Max - startNw } };
        }
    }
}
