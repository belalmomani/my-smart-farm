using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MySmartFarm.Core
{
    public struct PeriodReport
    {
        public int FromDay, ToDay;
        public double ProductRevenue;      // eggs + meat + milk
        public double OperatingExpenses;   // feed, water, care, maintenance, labor, other
        public double OperatingProfit;     // ProductRevenue - OperatingExpenses (animal trading is capital, excluded)
        public double AnimalPurchases, AnimalSales;
        public double NetCashFlow;         // every transaction in the period
        public int Births, Hatched, Deaths, SalesCount;
        public Dictionary<TxCategory, double> ByCategory;
    }
    public struct SpeciesCount { public string SpeciesId; public int Males, Females, Juveniles; public int Total => Males + Females; }

    public static class Reports
    {
        static bool IsProduct(TxCategory c) => c == TxCategory.EggSale || c == TxCategory.MeatSale || c == TxCategory.MilkSale;

        /// <summary>Inclusive day range. Operating profit excludes animal purchases/sales (capital); net cash flow includes everything. Both are shown, never merged.</summary>
        public static PeriodReport Period(FarmState s, int fromDay, int toDay)
        {
            var r = new PeriodReport { FromDay = fromDay, ToDay = toDay, ByCategory = new Dictionary<TxCategory, double>() };
            foreach (var t in s.Ledger)
            {
                if (t.Day < fromDay || t.Day > toDay) continue;
                double v; r.ByCategory.TryGetValue(t.Category, out v); r.ByCategory[t.Category] = v + t.Amount;
                r.NetCashFlow += t.Amount;
                if (IsProduct(t.Category)) r.ProductRevenue += t.Amount;
                else if (t.Category == TxCategory.AnimalPurchase) r.AnimalPurchases += -t.Amount;
                else if (t.Category == TxCategory.AnimalSale) { r.AnimalSales += t.Amount; r.SalesCount++; }
                else r.OperatingExpenses += -t.Amount;
            }
            r.OperatingProfit = r.ProductRevenue - r.OperatingExpenses;
            foreach (var e in s.Events)
            {
                if (e.Day < fromDay || e.Day > toDay) continue;
                if (e.Type == "death") r.Deaths++;
                else if (e.Type == "birth") r.Births += Num(e.Detail, "actual");
                else if (e.Type == "hatch") r.Hatched += Num(e.Detail, "hatched");
            }
            return r;
        }

        public static int Num(string detail, string key)
        {
            if (string.IsNullOrEmpty(detail)) return 0;
            foreach (var part in detail.Split(';')) { var kv = part.Split('='); int v; if (kv.Length == 2 && kv[0] == key && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v; }
            return 0;
        }

        public static List<SpeciesCount> Composition(FarmState s, int day)
        {
            var l = new List<SpeciesCount>();
            foreach (var id in SpeciesCatalog.All.Keys)
            {
                var sp = SpeciesCatalog.All[id]; var c = new SpeciesCount { SpeciesId = id };
                foreach (var a in s.Animals) if (a.Alive && a.SpeciesId == id)
                { if (day - a.BirthDay < sp.MaturityDays) c.Juveniles++; else if (a.Sex == Sex.Male) c.Males++; else c.Females++; }
                if (c.Total + c.Juveniles > 0) l.Add(c);
            }
            return l;
        }

        /// <summary>Per-day series for days [fromDay..toDay]: revenue (positive txs), expenses (negative txs, as positive numbers), cash balance at end of day.</summary>
        public static void DailySeries(FarmState s, int fromDay, int toDay, out double[] revenue, out double[] expense, out double[] cash)
        {
            int n = Math.Max(1, toDay - fromDay + 1); revenue = new double[n]; expense = new double[n]; cash = new double[n];
            foreach (var t in s.Ledger) { int i = t.Day - fromDay; if (i < 0 || i >= n) continue; if (t.Amount >= 0) revenue[i] += t.Amount; else expense[i] += -t.Amount; }
            for (int i = 0; i < n; i++)
            {
                double after = 0; int d = fromDay + i; foreach (var t in s.Ledger) if (t.Day > d) after += t.Amount;
                cash[i] = s.Cash - after;
            }
        }
    }

    public static class Csv
    {
        static string Esc(string v) { v = v ?? ""; return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v; }
        static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        public static string Ledger(FarmState s)
        {
            var sb = new StringBuilder("id,day,category,amount,currency,memo\n");
            foreach (var t in s.Ledger) sb.Append(Esc(t.Id)).Append(',').Append(t.Day + 1).Append(',').Append(t.Category).Append(',').Append(F(t.Amount)).Append(',').Append(Esc(s.Currency)).Append(',').Append(Esc(t.Memo)).Append('\n');
            return sb.ToString();
        }
        public static string Events(FarmState s)
        {
            var sb = new StringBuilder("id,day,type,subject,detail\n");
            foreach (var e in s.Events) sb.Append(Esc(e.Id)).Append(',').Append(e.Day + 1).Append(',').Append(Esc(e.Type)).Append(',').Append(Esc(e.SubjectId)).Append(',').Append(Esc(e.Detail)).Append('\n');
            return sb.ToString();
        }
    }
}
