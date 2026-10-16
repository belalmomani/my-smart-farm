using System;
using System.Linq;

namespace MySmartFarm.Core
{
    public struct MortalityStat { public int Deaths, AliveAtStart; public int FromDay, ToDay; public double Ratio => AliveAtStart == 0 ? 0 : (double)Deaths / AliveAtStart; }

    /// <summary>Pure simulation (no UnityEngine). All mutations go through here; events/transactions are idempotent by unique id.</summary>
    public class Farm
    {
        public const string Feed = "feed_kg", Water = "water_l";
        public readonly FarmState S;
        public Farm(FarmState s) { S = s; }

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

        // ---------- inventory ----------
        public double StockOf(string key) => S.Stock.FirstOrDefault(i => i.Key == key)?.Qty ?? 0;
        void AddStock(string key, double q)
        {
            var it = S.Stock.FirstOrDefault(i => i.Key == key);
            if (it == null) S.Stock.Add(new StockItem { Key = key, Qty = q }); else it.Qty += q;
        }
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
            var sp = SpeciesCatalog.All[speciesId];
            if (S.Cash < sp.BuyPriceJod) return null;
            var a = new Animal { Id = NextId("an"), SpeciesId = speciesId, Sex = sex, BirthDay = day - Math.Max(0, ageDays) };
            S.Animals.Add(a);
            AddTransaction("buy:" + a.Id, day, TxCategory.AnimalPurchase, -sp.BuyPriceJod, speciesId);
            AddEvent("purchase", day, a.Id); return a;
        }
        public bool SellAnimal(string id, int day, double? price = null)
        {
            var a = S.Animals.FirstOrDefault(x => x.Id == id && x.Alive); if (a == null) return false;
            a.Alive = false; a.DeathDay = day; a.CauseOfDeath = "sold";
            AddTransaction("sell:" + a.Id, day, TxCategory.AnimalSale, price ?? SpeciesCatalog.All[a.SpeciesId].SellPriceJod, a.SpeciesId);
            AddEvent("sale", day, a.Id); return true;
        }
        public int AgeDays(Animal a, int day) => day - a.BirthDay;
        public bool IsAdult(Animal a, int day) => AgeDays(a, day) >= SpeciesCatalog.All[a.SpeciesId].MaturityDays;

        /// <summary>Gestating species only (rabbit/sheep/goat/cow). Returns false with reason.</summary>
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

        /// <summary>Move up to count fertile eggs into incubation (eggs species). Returns eggs actually set.</summary>
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

        // ---------- time ----------
        /// <summary>Process every whole virtual day up to and including targetDay exactly once (LastProcessedDay guards against double-processing).</summary>
        public int ProcessUntil(int targetDay)
        {
            int n = 0;
            while (S.LastProcessedDay < targetDay) { S.LastProcessedDay++; StepDay(S.LastProcessedDay); n++; }
            return n;
        }

        void StepDay(int day)
        {
            var rng = new Random(unchecked(S.Seed * 7919 + day));   // reproducible after reload; no RNG state to save
            var alive = S.Animals.Where(a => a.Alive).ToList();
            double crowd = alive.Count > 40 ? 1 + (alive.Count - 40) * 0.02 : 1;   // simplistic placeholder crowding factor

            foreach (var a in alive)
            {
                var sp = SpeciesCatalog.All[a.SpeciesId]; bool adult = IsAdult(a, day);
                double feed = adult ? sp.FeedKgAdult : sp.FeedKgJuvenile, water = adult ? sp.WaterLAdult : sp.WaterLJuvenile;
                bool ok = TakeStock(Feed, feed) & TakeStock(Water, water);   // non-short-circuit: consume both when available
                if (!ok) { a.DeficitDays++; a.Health -= Math.Min(10, 2 * a.DeficitDays); }
                else { a.DeficitDays = 0; a.Health = Math.Min(100, a.Health + 1); }
                if (a.DeficitDays == 3) AddEvent("alert_deficit", day, a.Id, "feed/water shortage");

                string cause = null;
                if (a.Health <= 0) cause = "deficit";
                else
                {
                    double p = sp.BaseDailyMortality * (1 + (100 - a.Health) / 20.0) * (1 + a.DeficitDays * 0.5) * crowd;
                    if (AgeDays(a, day) > sp.MaxAgeDays) p += 0.01;
                    if (rng.NextDouble() < p) cause = "chance";
                }
                if (cause != null) { a.Alive = false; a.DeathDay = day; a.CauseOfDeath = cause; AddEvent("death", day, a.Id, cause); continue; }

                // eggs
                if (sp.LaysEggs && a.Sex == Sex.Female && adult && a.Health >= 50 && a.DeficitDays == 0 && rng.NextDouble() < sp.EggsPerLayingDay)
                {
                    bool rooster = alive.Any(x => x.Alive && x.SpeciesId == a.SpeciesId && x.Sex == Sex.Male && IsAdult(x, day) && x.Health >= 50);
                    if (rooster && rng.NextDouble() < sp.FertilityRate) S.FertileEggs++; else S.InfertileEggs++;
                }
                // birth (cleared immediately => cannot repeat)
                if (a.PregnantUntilDay >= 0 && day >= a.PregnantUntilDay)
                {
                    int lit = rng.Next(sp.LitterMin, sp.LitterMax + 1); a.PregnantUntilDay = -1; a.LastBirthDay = day;
                    for (int i = 0; i < lit; i++) S.Animals.Add(new Animal { Id = NextId("an"), SpeciesId = a.SpeciesId, Sex = rng.Next(2) == 0 ? Sex.Male : Sex.Female, BirthDay = day });
                    AddEvent("birth", day, a.Id, "expected=" + ((sp.LitterMin + sp.LitterMax) / 2.0) + ";actual=" + lit);
                }
            }
            // hatching
            foreach (var inc in S.Incubations.Where(i => i.Count > 0 && day >= i.HatchDay).ToList())
            {
                var rngH = new Random(unchecked(S.Seed * 31 + day)); int hatched = 0;
                for (int i = 0; i < inc.Count; i++) if (rngH.NextDouble() < 0.85)   // default hatch rate (simulation default)
                { hatched++; S.Animals.Add(new Animal { Id = NextId("an"), SpeciesId = inc.SpeciesId, Sex = rngH.Next(2) == 0 ? Sex.Male : Sex.Female, BirthDay = day }); }
                AddEvent("hatch", day, inc.Id, "set=" + inc.Count + ";hatched=" + hatched); inc.Count = 0;
            }
            S.Incubations.RemoveAll(i => i.Count == 0);
        }

        // ---------- reports ----------
        public MortalityStat Mortality(int fromDay, int toDay)
        {
            int deaths = S.Events.Count(e => e.Type == "death" && e.Day >= fromDay && e.Day <= toDay);
            int start = S.Animals.Count(a => a.BirthDay < fromDay && (a.DeathDay < 0 || a.DeathDay >= fromDay));
            return new MortalityStat { Deaths = deaths, AliveAtStart = start, FromDay = fromDay, ToDay = toDay };
        }
    }
}
