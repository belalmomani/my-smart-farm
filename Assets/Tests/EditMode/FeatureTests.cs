using System.Linq;
using NUnit.Framework;
using MySmartFarm.Core;

namespace MySmartFarm.Tests
{
    public class FeatureTests
    {
        static Farm New(double cash = 10000, string type = "") => new Farm(new FarmState { Cash = cash, FarmType = type });
        static void Supplies(Farm f, double feed = 5000, double water = 5000) { f.GrantStock(Farm.Feed, feed); f.GrantStock(Farm.Water, water); }

        [Test] public void Prices_AreEditable_AndValidated()
        {
            var f = New();
            Assert.IsTrue(PriceBook.Set(f.S, "feed_kg", 0.5)); Assert.AreEqual(0.5, f.Price("feed_kg"), 1e-9);
            Assert.IsFalse(PriceBook.Set(f.S, "feed_kg", -1)); Assert.IsFalse(PriceBook.Set(f.S, "feed_kg", double.NaN)); Assert.IsFalse(PriceBook.Set(f.S, "nope", 1));
            Assert.AreEqual(0.5, f.Price("feed_kg"), 1e-9);
        }
        [Test] public void BuyAnimal_UsesEditedPrice()
        {
            var f = New(1000); PriceBook.Set(f.S, "buy:rabbit", 25); f.BuyAnimal("rabbit", Sex.Female, 10, 0);
            Assert.AreEqual(975, f.S.Cash, 1e-9);
        }
        [Test] public void FarmType_RestrictsSpecies()
        {
            var f = New(100000, "poultry");
            Assert.IsNull(f.BuyAnimal("cow", Sex.Female, 600, 0)); Assert.IsNotNull(f.BuyAnimal("chicken", Sex.Female, 200, 0));
        }
        [Test] public void Migration_V1_GetsPriceBookAndVersion2()
        {
            var d = new SaveData { SchemaVersion = 1 }; d.Farm.Prices.Clear();
            SaveMigrator.Migrate(d); Assert.AreEqual(2, d.SchemaVersion); Assert.IsTrue(d.Farm.Prices.Any(p => p.Key == "egg"));
        }
        [Test] public void SeasonalNeeds_SummerWaterHigher_WinterFeedHigher()
        {
            var f = New(); f.BuyAnimal("sheep", Sex.Female, 600, 0);
            double fSp, wSp, fSu, wSu, fWi, wWi; f.NeedsPerDay(10, out fSp, out wSp); f.NeedsPerDay(150, out fSu, out wSu); f.NeedsPerDay(300, out fWi, out wWi);
            Assert.Greater(wSu, wSp); Assert.Greater(fWi, fSp);
        }
        [Test] public void Alerts_LowFeed_AndDeficit()
        {
            var f = New(); f.BuyAnimal("sheep", Sex.Female, 600, 0); f.GrantStock(Farm.Feed, 1); f.GrantStock(Farm.Water, 1000);
            Assert.IsTrue(f.Alerts(1).Any(a => a.Key == "alert_low_feed"));
            f.S.RandomEvents = false; f.ProcessUntil(3); Assert.IsTrue(f.Alerts(3).Any(a => a.Key == "alert_deficit"));
        }
        [Test] public void RandomEvents_CanBeDisabled()
        {
            var off = New(); off.S.RandomEvents = false; Supplies(off); off.BuyAnimal("goat", Sex.Female, 700, 0); off.ProcessUntil(2000);
            Assert.IsFalse(off.S.Events.Any(e => e.Type.StartsWith("env_")));
            var on = New(); Supplies(on, 1e7, 1e7); on.BuyAnimal("goat", Sex.Female, 700, 0); on.ProcessUntil(2000);
            Assert.IsTrue(on.S.Events.Any(e => e.Type.StartsWith("env_")));
        }
        [Test] public void Reports_SeparateOperatingProfitFromCashFlow()
        {
            var f = New(1000); f.BuyAnimal("chicken", Sex.Female, 200, 0);                     // capital: -4
            f.BuyStock(Farm.Feed, 10, 0.3, 0, TxCategory.FeedPurchase);                         // opex: -3
            f.S.InfertileEggs = 10; f.SellEggs(10, false, 0.1, 1);                              // revenue: +1
            var r = Reports.Period(f.S, 0, 5);
            Assert.AreEqual(1, r.ProductRevenue, 1e-9); Assert.AreEqual(3, r.OperatingExpenses, 1e-9); Assert.AreEqual(-2, r.OperatingProfit, 1e-9);
            Assert.AreEqual(4, r.AnimalPurchases, 1e-9); Assert.AreEqual(-6, r.NetCashFlow, 1e-9);
        }
        [Test] public void DailySeries_CashMatchesBalance()
        {
            var f = New(1000); f.BuyStock(Farm.Feed, 10, 1, 2, TxCategory.FeedPurchase);
            double[] rev, exp, cash; Reports.DailySeries(f.S, 0, 4, out rev, out exp, out cash);
            Assert.AreEqual(1000, cash[1], 1e-9); Assert.AreEqual(990, cash[2], 1e-9); Assert.AreEqual(990, cash[4], 1e-9); Assert.AreEqual(10, exp[2], 1e-9);
        }
        [Test] public void Csv_EscapesCommasAndQuotes()
        {
            var f = New(); f.AddTransaction("t1", 0, TxCategory.Other, 1.5, "a,\"b\""); var csv = Csv.Ledger(f.S);
            StringAssert.Contains("\"a,\"\"b\"\"\"", csv); StringAssert.Contains(",1.50,", csv);
        }
        [Test] public void Scenarios_AreRanges_AndOrderedByPriceAssumptions()
        {
            var f = New(5000); Supplies(f, 100, 100); f.S.RandomEvents = false; for (int i = 0; i < 3; i++) { f.BuyAnimal("goat", Sex.Female, 700, 0); f.BuyAnimal("goat", Sex.Male, 700, 0); }
            var o = Scenarios.Run(f.S, Scenarios.All[0], 120, 5, 1); var b = Scenarios.Run(f.S, Scenarios.All[1], 120, 5, 1); var p = Scenarios.Run(f.S, Scenarios.All[2], 120, 5, 1);
            Assert.LessOrEqual(b.NetWorthEnd.Min, b.NetWorthEnd.Median); Assert.LessOrEqual(b.NetWorthEnd.Median, b.NetWorthEnd.Max);
            Assert.GreaterOrEqual(o.NetWorthEnd.Median, b.NetWorthEnd.Median - 1e-6); Assert.GreaterOrEqual(b.NetWorthEnd.Median, p.NetWorthEnd.Median - 1e-6);
        }
        [Test] public void Scenarios_DoNotMutateRealFarm()
        {
            var f = New(5000); Supplies(f, 100, 100); f.BuyAnimal("goat", Sex.Female, 700, 0); int day = f.S.LastProcessedDay; double cash = f.S.Cash; int n = f.S.Ledger.Count;
            Scenarios.Run(f.S, Scenarios.All[1], 60, 3, 1);
            Assert.AreEqual(day, f.S.LastProcessedDay); Assert.AreEqual(cash, f.S.Cash, 1e-9); Assert.AreEqual(n, f.S.Ledger.Count);
        }
        [Test] public void BreedAny_PicksEligiblePair_AndReportsReasons()
        {
            var f = New(); string k;
            f.BuyAnimal("rabbit", Sex.Female, 200, 0);
            Assert.IsFalse(f.TryBreedAny("rabbit", 0, out k)); Assert.AreEqual("br_no_male", k);
            f.BuyAnimal("rabbit", Sex.Male, 200, 0);
            Assert.IsTrue(f.TryBreedAny("rabbit", 0, out k)); Assert.AreEqual("br_ok", k); Assert.AreEqual(1, f.PregnantCount("rabbit"));
            Assert.IsFalse(f.TryBreedAny("rabbit", 1, out k)); Assert.AreEqual("br_no_female", k);
        }
        [Test] public void BreedAny_RejectsEggSpecies_AndImmatureFemales()
        {
            var f = New(); string k; f.BuyAnimal("chicken", Sex.Female, 300, 0); f.BuyAnimal("chicken", Sex.Male, 300, 0);
            Assert.IsFalse(f.TryBreedAny("chicken", 0, out k)); Assert.AreEqual("br_no_gest", k);
            f.BuyAnimal("goat", Sex.Female, 10, 0); f.BuyAnimal("goat", Sex.Male, 400, 0);
            Assert.IsFalse(f.TryBreedAny("goat", 0, out k)); Assert.AreEqual("br_no_female", k);
        }
    }
}
