using System;
using System.Linq;
using NUnit.Framework;
using MySmartFarm.Core;

namespace MySmartFarm.Tests
{
    public class FarmTests
    {
        static Farm New(double cash = 10000) { var f = new Farm(new FarmState { Cash = cash }); return f; }

        [Test] public void Clock_OneHourIsOneDay()
        { var c = new SimClock(); c.Advance(3600, DateTime.UtcNow); Assert.AreEqual(1, c.WholeDay); }
        [Test] public void Clock_SpeedAndPause()
        { var c = new SimClock(); c.SetSpeed(4); c.Advance(900, DateTime.UtcNow); Assert.AreEqual(1, c.WholeDay); c.Paused = true; c.Advance(9999, DateTime.UtcNow); Assert.AreEqual(1, c.WholeDay); }
        [Test] public void Clock_OfflineAppliedOnce_Capped_ClockBackwardsIgnored()
        {
            var c = new SimClock { MaxOfflineHours = 10 }; var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc); c.LastUpdateUtcTicks = t0.Ticks;
            Assert.AreEqual(10, c.ApplyOffline(t0.AddHours(50)), 1e-9);
            Assert.AreEqual(0, c.ApplyOffline(t0.AddHours(50)));
            Assert.AreEqual(0, c.ApplyOffline(t0.AddHours(1)));
        }
        [Test] public void Seasons_CycleAndYear()
        { Assert.AreEqual(Season.Spring, Seasons.Of(0)); Assert.AreEqual(Season.Winter, Seasons.Of(364)); Assert.AreEqual(Season.Spring, Seasons.Of(365)); Assert.AreEqual(2, Seasons.Year(365)); }

        [Test] public void Stock_NeverNegative_AndRejectsBadInput()
        {
            var f = New(100);
            Assert.IsFalse(f.BuyStock("feed_kg", -1, 1, 0, TxCategory.FeedPurchase));
            Assert.IsFalse(f.BuyStock("feed_kg", 1000, 1, 0, TxCategory.FeedPurchase)); // not enough cash
            Assert.IsTrue(f.BuyStock("feed_kg", 10, 1, 0, TxCategory.FeedPurchase));
            Assert.GreaterOrEqual(f.StockOf("feed_kg"), 0);
        }
        [Test] public void Ledger_DuplicateIdIgnored_CashMatchesLedger()
        {
            var f = New(0); Assert.IsTrue(f.AddTransaction("x", 0, TxCategory.Other, 5, "")); Assert.IsFalse(f.AddTransaction("x", 0, TxCategory.Other, 5, ""));
            Assert.AreEqual(5, f.S.Cash); Assert.AreEqual(f.S.Cash, f.LedgerSum());
        }
        [Test] public void ProcessUntil_IsIdempotent()
        { var f = New(); f.BuyAnimal("rabbit", Sex.Female, 200, 0); Assert.AreEqual(10, f.ProcessUntil(10)); Assert.AreEqual(0, f.ProcessUntil(10)); }

        [Test] public void Breeding_RulesEnforced()
        {
            var f = New(); string r;
            var m = f.BuyAnimal("rabbit", Sex.Male, 200, 0); var fe = f.BuyAnimal("rabbit", Sex.Female, 200, 0); var young = f.BuyAnimal("rabbit", Sex.Female, 10, 0);
            var ch = f.BuyAnimal("chicken", Sex.Female, 200, 0);
            Assert.IsFalse(f.TryBreed(young.Id, m.Id, 0, out r)); StringAssert.Contains("mature", r);
            Assert.IsFalse(f.TryBreed(m.Id, fe.Id, 0, out r));
            Assert.IsFalse(f.TryBreed(ch.Id, m.Id, 0, out r));
            Assert.IsTrue(f.TryBreed(fe.Id, m.Id, 0, out r));
            Assert.IsFalse(f.TryBreed(fe.Id, m.Id, 1, out r)); StringAssert.Contains("pregnant", r);
        }
        [Test] public void Birth_HappensOnce_AfterGestation_WithLitterInRange()
        {
            var f = New(); f.BuyStock("feed_kg", 1000, 0.1, 0, TxCategory.FeedPurchase); f.BuyStock("water_l", 1000, 0.01, 0, TxCategory.WaterPurchase);
            var m = f.BuyAnimal("rabbit", Sex.Male, 200, 0); var fe = f.BuyAnimal("rabbit", Sex.Female, 200, 0); string r;
            f.S.Animals.ForEach(a => { }); f.TryBreed(fe.Id, m.Id, 0, out r);
            f.ProcessUntil(31); f.ProcessUntil(31); f.ProcessUntil(60);
            var births = f.S.Events.Where(e => e.Type == "birth").ToList();
            Assert.AreEqual(1, births.Count);
            int kits = f.S.Animals.Count(a => a.BirthDay == 31); Assert.That(kits, Is.InRange(4, 8));
        }
        [Test] public void Eggs_InfertileWithoutRooster_FertileCanHatch()
        {
            var f = New(); f.BuyStock("feed_kg", 5000, 0.1, 0, TxCategory.FeedPurchase); f.BuyStock("water_l", 5000, 0.01, 0, TxCategory.WaterPurchase);
            for (int i = 0; i < 5; i++) f.BuyAnimal("chicken", Sex.Female, 300, 0);
            f.ProcessUntil(30); Assert.AreEqual(0, f.S.FertileEggs); Assert.Greater(f.S.InfertileEggs, 0);
            f.BuyAnimal("chicken", Sex.Male, 300, 30); f.ProcessUntil(60); Assert.Greater(f.S.FertileEggs, 0);
            int set = f.SetIncubation("chicken", f.S.FertileEggs, 60); Assert.Greater(set, 0);
            f.ProcessUntil(60 + 21); Assert.AreEqual(1, f.S.Events.Count(e => e.Type == "hatch"));
        }
        [Test] public void NoFeed_CausesGradualDecline_NotInstantDeath()
        {
            var f = New(); var a = f.BuyAnimal("sheep", Sex.Female, 400, 0); f.ProcessUntil(1);
            Assert.IsTrue(a.Alive); Assert.Less(a.Health, 100); f.ProcessUntil(30); Assert.IsFalse(a.Alive);
        }
        [Test] public void Sale_RecordedOnce_AndSecondSaleRejected()
        { var f = New(); var a = f.BuyAnimal("goat", Sex.Female, 400, 0); double c = f.S.Cash; Assert.IsTrue(f.SellAnimal(a.Id, 1)); Assert.IsFalse(f.SellAnimal(a.Id, 1)); Assert.AreEqual(c + 90, f.S.Cash, 1e-9); }
        [Test] public void LongRun_365Days_LedgerConsistent_NoNegativeStock()
        {
            var f = New(100000); f.BuyStock("feed_kg", 20000, 0.2, 0, TxCategory.FeedPurchase); f.BuyStock("water_l", 50000, 0.01, 0, TxCategory.WaterPurchase);
            foreach (var sp in new[] { "rabbit", "chicken", "duck", "sheep", "goat", "cow" }) { f.BuyAnimal(sp, Sex.Male, 700, 0); f.BuyAnimal(sp, Sex.Female, 700, 0); }
            f.ProcessUntil(365);
            Assert.AreEqual(f.S.Cash, f.LedgerSum(), 1e-6); Assert.IsTrue(f.S.Stock.All(i => i.Qty >= 0));
            Assert.AreEqual(f.S.Events.Count, f.S.Events.Select(e => e.Id).Distinct().Count());
        }
    }
}
