using System;
using System.Collections.Generic;
using System.Linq;

namespace MySmartFarm.Core
{
    public struct MortalityStat { public int Deaths, AliveAtStart; public int FromDay, ToDay; public double Ratio => AliveAtStart == 0 ? 0 : (double)Deaths / AliveAtStart; }
    public struct Alert { public string Key; public int Count; }

    /// <summary>Pure simulation (no UnityEngine). All mutations go through here; events/transactions are idempotent by unique id.</summary>
    public class Farm
    {
        public const string Feed = "feed_kg", Water = "water_l";
        public const int CrowdingThreshold = 40;   // simplistic placeholder
        public readonly FarmState S;
        public double MortalityMult = 1, FertilityMult = 1;   // scenario modifiers (not saved)

        public Farm(FarmState s) { S = s; PriceBook.EnsureDefaults(S); }

        // ---------- seasonal needs (simulation defaults, not scientific facts) ----------
        public static double WaterSeasonMult(Season s) => s == Season.Summer ? 1.25 : s == Season.Winter ? 0.9 : 1.0;
        public static double FeedSeasonMult(Season s) => s == Season.Winter ? 1.15 : 1.0;

        // ---------- ids / ledger / events ----------
        string NextId(string p) => p + "-" + (S.NextSeq++);

        public bool AddTransaction(string id, int day, TxCategory c, double amount, string memo)
        {
            if (S.Ledger.Any(t => t.Id == id)) return false;      // idempotent
            S.Ledger.Add(new Transaction { Id = id, Day = day, Category = c, Amount = amount, Memo = memo });
            S.Cash += amount; return true;
        }
        bool AddEvent(string type, int day, string subject, string detail = "")
        {
            string id = type + ":" + day + ":" + subject;
            if (S.Events.Any(e => e.Id == id)) return false;
            S.Events.Add(new FarmEvent { Id = id, Day = day, Type = type, SubjectId = subject, Detail = detail }); return true;
        }
        public double LedgerSum() => S.Ledger.Sum(t => t.Amount);
        public double Price(string key) => PriceBook.Get(S, key);

        // ---------- inventory ----------
        public double StockOf(string key) => S.Stock.FirstOrDefault(i => i.Key == key)?.Qty ?? 0;
        void AddStock(string key, double q) { var it = S.Stock.FirstOrDefault(i => i.Key == key); if (it == null) S.Stock.Add(new StockItem { Key = key, Qty = q }); else it.Qty += q; }
        /// <summary>Starter supply only (no cash movement, no ledger entry).</summary>
        public void GrantStock(string key, double qty) { if (qty > 0) AddStock(key, qty); }
        bool TakeStock(string key, double q)
        {
            var it = S.Stock.FirstOrDefault(i => i.Key == key);
            if (it == null || q < 0 || it.Qty < q) return false;
            it.Qty -= q; return true;
        }
        public bool BuyStock(string key, double qty, double unitPrice, int day, TxCategory cat)
        {
            if (qty <= 0 || unitPrice < 0 || double.IsNaN(qty) || double.IsNaN(unitPrice)) return false;
            double cost = qty * unitPrice; if (cost > S.Cash) return false;
            AddTransaction(NextId("tx"), day, cat, -cost, "buy " + key); AddStock(key, qty); return true;
        }

        // ---------- animals ----------
        public Animal BuyAnimal(string speciesId, Sex sex, int ageDays, int day)
        {
            if (!FarmTypes.Allows(S.FarmType, speciesId)) return null;
            double price = Price("buy:" + speciesId);
            if (S.Cash < price) return null;
            var a = new Animal { Id = NextId("an"), SpeciesId = speciesId, Sex = sex, BirthDay = day - Math.Max(0, ageDays) };
            S.Animals.Add(a);
            AddTransaction("buy:" + a.Id, day, TxCategory.AnimalPurchase, -price, speciesId);
            AddEvent("purchase", day, a.Id); return a;
        }
        public bool SellAnimal(string id, int day, double? price = null)
        {
            var a = S.Animals.FirstOrDefault(x => x.Id == id && x.Alive); if (a == null) return false;
            a.Alive = false; a.DeathDay = day; a.CauseOfDeath = "sold";
            AddTransaction("sell:" + a.Id, day, TxCategory.AnimalSale, price ?? Price("sell:" + a.SpeciesId), a.SpeciesId);
            AddEvent("sale", day, a.Id); return true;
        }
        public int AgeDays(Animal a, int day) => day - a.BirthDay;
        public bool IsAdult(Animal a, int day) => AgeDays(a, day) >= SpeciesCatalog.All[a.SpeciesId].MaturityDays;

        public bool TryBreed(string femaleId, string maleId, int day, out string reason)
        {
            reason = null;
            var f = S.Animals.FirstOrDefault(a => a.Id == femaleId && a.Alive); var m = S.Animals.FirstOrDefault(a => a.Id == maleId && a.Alive);
            if (f == null || m == null) { reason = "animal not found"; return false; }
            if (f.SpeciesId != m.SpeciesId) { reason = "different species"; return false; }
            var sp = SpeciesCatalog.All[f.SpeciesId];
            if (sp.GestationDays <= 0) { reason = "species does not gestate"; return false; }
            if (f.Sex != Sex.Female || m.Sex != Sex.Male) { reason = "need female + male"; return false; }
            if (!IsAdult(f, day) || !IsAdult(m, day)) { reason = "not mature"; return false; }
            if (f.PregnantUntilDay >= 0) { reason = "already pregnant"; return false; }
            if (day - f.LastBirthDay < sp.RecoveryDays) { reason = "recovering"; return false; }
            if (f.Health < 60 || m.Health < 60) { reason = "poor health"; return false; }
            f.PregnantUntilDay = day + sp.GestationDays; AddEvent("conception", day, f.Id, "sire=" + m.Id); return true;
        }

        /// <summary>Pairs the longest-waiting eligible adult female with any healthy adult male of the species. reasonKey is a Loc key.</summary>
        public bool TryBreedAny(string speciesId, int day, out string reasonKey)
        {
            reasonKey = "fail"; var sp = SpeciesCatalog.All[speciesId];
            if (sp.GestationDays <= 0) { reasonKey = "br_no_gest"; return false; }
            var adults = S.Animals.Where(a => a.Alive && a.SpeciesId == speciesId && IsAdult(a, day) && a.Health >= 60).ToList();
            var female = adults.Where(a => a.Sex == Sex.Female && a.PregnantUntilDay < 0 && day - a.LastBirthDay >= sp.RecoveryDays).OrderBy(a => a.LastBirthDay).FirstOrDefault();
            if (female == null) { reasonKey = "br_no_female"; return false; }
            var male = adults.FirstOrDefault(a => a.Sex == Sex.Male);
            if (male == null) { reasonKey = "br_no_male"; return false; }
            string r; if (!TryBreed(female.Id, male.Id, day, out r)) return false;
            reasonKey = "br_ok"; return true;
        }
        public int PregnantCount(string speciesId) => S.Animals.Count(a => a.Alive && a.SpeciesId == speciesId && a.PregnantUntilDay >= 0);

        public int SetIncubation(string speciesId, int count, int day)
        {
            var sp = SpeciesCatalog.All[speciesId]; if (!sp.LaysEggs || count <= 0) return 0;
            int n = Math.Min(count, S.FertileEggs); if (n == 0) return 0;
            S.FertileEggs -= n;
            S.Incubations.Add(new Incubation { Id = NextId("inc"), SpeciesId = speciesId, Count = n, HatchDay = day + sp.IncubationDays });
            AddEvent("incubation_start", day, S.Incubations.Last().Id, "n=" + n); return n;
        }
        public bool SellEggs(int count, bool fertile, double unitPrice, int day)
        {
            if (count <= 0) return false;
            if (fertile) { if (S.FertileEggs < count) return false; S.FertileEggs -= count; }
            else { if (S.InfertileEggs < count) return false; S.InfertileEggs -= count; }
            return AddTransaction(NextId("tx"), day, TxCategory.EggSale, count * unitPrice, "eggs");
        }

        // ---------- valuation / needs / alerts ----------
        /// <summary>Valuation method: alive animals at the user-editable "sell:" price; feed/water stock at current purchase price; no depreciation (no equipment modelled).</summary>
        public double AnimalValue() => S.Animals.Where(a => a.Alive).Sum(a => Price("sell:" + a.SpeciesId));
        public double StockValue() => StockOf(Feed) * Price("feed_kg") + StockOf(Water) * Price("water_l");
        public double NetWorth(double priceMult = 1) => S.Cash + AnimalValue() * priceMult + StockValue();

        double FeedNeed(Animal a, SpeciesDef sp, int day) => (IsAdult(a, day) ? sp.FeedKgAdult : sp.FeedKgJuvenile) * FeedSeasonMult(Seasons.Of(day));
        double WaterNeed(Animal a, SpeciesDef sp, int day) => (IsAdult(a, day) ? sp.WaterLAdult : sp.WaterLJuvenile) * WaterSeasonMult(Seasons.Of(day));
        public void NeedsPerDay(int day, out double feed, out double water)
        {
            feed = 0; water = 0;
            foreach (var a in S.Animals) if (a.Alive) { var sp = SpeciesCatalog.All[a.SpeciesId]; feed += FeedNeed(a, sp, day); water += WaterNeed(a, sp, day); }
        }
        public List<Alert> Alerts(int day)
        {
            var l = new List<Alert>(); double nf, nw; NeedsPerDay(day, out nf, out nw);
            if (nf > 0 && StockOf(Feed) < nf * 3) l.Add(new Alert { Key = "alert_low_feed", Count = (int)Math.Floor(StockOf(Feed) / nf) });
            if (nw > 0 && StockOf(Water) < nw * 3) l.Add(new Alert { Key = "alert_low_water", Count = (int)Math.Floor(StockOf(Water) / nw) });
            int hungry = S.Animals.Count(a => a.Alive && a.DeficitDays > 0); if (hungry > 0) l.Add(new Alert { Key = "alert_deficit", Count = hungry });
            int alive = S.Animals.Count(a => a.Alive); if (alive > CrowdingThreshold) l.Add(new Alert { Key = "alert_crowding", Count = alive });
            return l;
        }

        // ---------- time ----------
        public int ProcessUntil(int targetDay)
        {
            int n = 0;
            while (S.LastProcessedDay < targetDay) { S.LastProcessedDay++; StepDay(S.LastProcessedDay); n++; }
            return n;
        }

        void RandomEvent(Random rng, int day)
        {
            var season = Seasons.Of(day); double r = rng.NextDouble();
            if (season == Season.Summer && r < 0.4)
            { foreach (var a in S.Animals) if (a.Alive) a.Health -= 5; AddEvent("env_heatwave", day, "farm", "cause=random;health=-5"); }
            else if (season == Season.Winter && r < 0.4)
            { foreach (var a in S.Animals) if (a.Alive) a.Health -= 5; AddEvent("env_coldsnap", day, "farm", "cause=random;health=-5"); }
            else
            { double lost = StockOf(Feed) * 0.10; if (lost > 0) TakeStock(Feed, lost); AddEvent("env_spoilage", day, "farm", "cause=random;feed_lost=" + Math.Round(lost, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        }

        void StepDay(int day)
        {
            var rng = new Random(unchecked(S.Seed * 7919 + day));   // reproducible after reload; no RNG state to save
            if (S.RandomEvents && rng.NextDouble() < 0.01) RandomEvent(rng, day);
            var alive = S.Animals.Where(a => a.Alive).ToList();
            double crowd = alive.Count > CrowdingThreshold ? 1 + (alive.Count - CrowdingThreshold) * 0.02 : 1;

            foreach (var a in alive)
            {
                var sp = SpeciesCatalog.All[a.SpeciesId]; bool adult = IsAdult(a, day);
                bool ok = TakeStock(Feed, FeedNeed(a, sp, day)) & TakeStock(Water, WaterNeed(a, sp, day));
                if (!ok) { a.DeficitDays++; a.Health -= Math.Min(10, 2 * a.DeficitDays); }
                else { a.DeficitDays = 0; a.Health = Math.Min(100, a.Health + 1); }
                if (a.DeficitDays == 3) AddEvent("alert_deficit", day, a.Id, "feed/water shortage");

                string cause = null;
                if (a.Health <= 0) cause = "health";
                else
                {
                    double p = sp.BaseDailyMortality * (1 + (100 - a.Health) / 20.0) * (1 + a.DeficitDays * 0.5) * crowd * MortalityMult;
                    if (AgeDays(a, day) > sp.MaxAgeDays) p += 0.01;
                    if (rng.NextDouble() < p) cause = "chance";
                }
                if (cause != null) { a.Alive = false; a.DeathDay = day; a.CauseOfDeath = cause; AddEvent("death", day, a.Id, cause); continue; }

                if (sp.LaysEggs && a.Sex == Sex.Female && adult && a.Health >= 50 && a.DeficitDays == 0 && rng.NextDouble() < sp.EggsPerLayingDay)
                {
                    bool rooster = alive.Any(x => x.Alive && x.SpeciesId == a.SpeciesId && x.Sex == Sex.Male && IsAdult(x, day) && x.Health >= 50);
                    if (rooster && rng.NextDouble() < Math.Min(1.0, sp.FertilityRate * FertilityMult)) S.FertileEggs++; else S.InfertileEggs++;
                }
                if (a.PregnantUntilDay >= 0 && day >= a.PregnantUntilDay)
                {
                    int lit = rng.Next(sp.LitterMin, sp.LitterMax + 1); a.PregnantUntilDay = -1; a.LastBirthDay = day;
                    for (int i = 0; i < lit; i++) S.Animals.Add(new Animal { Id = NextId("an"), SpeciesId = a.SpeciesId, Sex = rng.Next(2) == 0 ? Sex.Male : Sex.Female, BirthDay = day });
                    AddEvent("birth", day, a.Id, "expected=" + ((sp.LitterMin + sp.LitterMax) / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture) + ";actual=" + lit);
                }
            }
            foreach (var inc in S.Incubations.Where(i => i.Count > 0 && day >= i.HatchDay).ToList())
            {
                var rngH = new Random(unchecked(S.Seed * 31 + day)); int hatched = 0;
                for (int i = 0; i < inc.Count; i++) if (rngH.NextDouble() < 0.85)   // default hatch rate (simulation default)
                { hatched++; S.Animals.Add(new Animal { Id = NextId("an"), SpeciesId = inc.SpeciesId, Sex = rngH.Next(2) == 0 ? Sex.Male : Sex.Female, BirthDay = day }); }
                AddEvent("hatch", day, inc.Id, "set=" + inc.Count + ";hatched=" + hatched); inc.Count = 0;
            }
            S.Incubations.RemoveAll(i => i.Count == 0);
        }

        public MortalityStat Mortality(int fromDay, int toDay)
        {
            int deaths = S.Events.Count(e => e.Type == "death" && e.Day >= fromDay && e.Day <= toDay);
            int start = S.Animals.Count(a => a.BirthDay < fromDay && (a.DeathDay < 0 || a.DeathDay >= fromDay));
            return new MortalityStat { Deaths = deaths, AliveAtStart = start, FromDay = fromDay, ToDay = toDay };
        }
    }
}
