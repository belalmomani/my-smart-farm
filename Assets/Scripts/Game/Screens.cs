using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MySmartFarm.Core;

namespace MySmartFarm.Game
{
    public partial class GameController
    {
        int period = 1;                                   // 0 day, 1 month (30d), 2 year (365d)
        bool scenarioRunning; string scenarioProgress = "";
        readonly List<ScenarioResult> scenarioResults = new List<ScenarioResult>();
        Ui.BarChart budgetChart, reportsChart;
        static readonly string[] Currencies = { "JOD", "USD", "EUR", "SAR", "AED" };

        GameObject ScreenPanel(Transform root, string name, bool full = false)
        { return Ui.MakePanel(root, name, Ui.Bg, 0, full ? 0 : 0.08f, 1, full ? 1 : 0.90f).gameObject; }

        // ---------------- farm overlay ----------------
        void OverlayButton(Transform p, int row, int col, int cols, Func<string> label, Action a)
        {
            const float y0 = 0.08f, y1 = 0.30f; float rh = (y1 - y0) / 3f, top = y1 - row * rh, bot = top - rh, w = 1f / cols;
            Text lt; var b = Ui.MakeButton(p, Ui.Green, out lt, 32); var rt = Ui.Anchored(b.gameObject, col * w, bot, (col + 1) * w, top);
            rt.offsetMin = new Vector2(6, 6); rt.offsetMax = new Vector2(-6, -6); b.onClick.AddListener(() => a()); labels.Add(new LabelBind { T = lt, F = label, Screen = "farm" });
        }

        void BuildFarmOverlay(Transform root)
        {
            var go = new GameObject("FarmOverlay", typeof(RectTransform)); go.transform.SetParent(root, false); Ui.Anchored(go, 0, 0, 1, 1); farmOverlay = go;
            Ui.MakePanel(go.transform, "Log", Ui.Panel, 0, 0.80f, 1, 0.90f, false); Ui.MakePanel(go.transform, "Controls", Ui.Panel, 0, 0.08f, 1, 0.30f);
            logText = Ui.MakeTextAnchored(go.transform, 0.02f, 0.80f, 0.98f, 0.90f, 28, Start1, new Color(1, 1, .8f));
            OverlayButton(go.transform, 0, 0, 4, () => data.Clock.Paused ? Loc.Get("resume") : Loc.Get("pause"), () => data.Clock.Paused = !data.Clock.Paused);
            foreach (var s in new[] { 1, 2, 4 }) { int sp = s; OverlayButton(go.transform, 0, s == 1 ? 1 : s == 2 ? 2 : 3, 4, () => data.Clock.Speed == sp ? "[" + sp + "x]" : sp + "x", () => data.Clock.SetSpeed(sp)); }
            OverlayButton(go.transform, 1, 0, 2, () => Loc.Get("buyFeed") + " | " + N(50 * farm.Price("feed_kg")), BuyFeed);
            OverlayButton(go.transform, 1, 1, 2, () => Loc.Get("buyWater") + " | " + N(200 * farm.Price("water_l")), BuyWater);
            OverlayButton(go.transform, 2, 0, 3, () => Loc.Get("sellEggs"), SellEggs);
            OverlayButton(go.transform, 2, 1, 3, () => Loc.Get("incubate"), Incubate);
            OverlayButton(go.transform, 2, 2, 3, () => Loc.Get("save"), () => Save(true));
        }

        // ---------------- market ----------------
        string PriceName(string key)
        {
            if (key == "feed_kg") return Loc.Get("p_feed_kg"); if (key == "water_l") return Loc.Get("p_water_l"); if (key == "egg") return Loc.Get("p_egg");
            return key.StartsWith("buy:") ? Loc.Get("p_buy") + " " + Loc.SpeciesName(key.Substring(4)) : Loc.Get("p_sell") + " " + Loc.SpeciesName(key.Substring(5));
        }
        string[] AllowedSpecies() { var d = FarmTypes.Get(data.Farm.FarmType); return d != null ? d.Species : SpeciesCatalog.All.Keys.ToArray(); }

        GameObject BuildMarket(Transform root)
        {
            var panel = ScreenPanel(root, "Market"); var c = Ui.MakeScroll(panel.transform, 0, 0, 1, 1);
            AddLabel(c, () => Loc.Get("supplies"), 36, new Color(1, .9f, .4f));
            var r = Ui.Row(c, 110);
            AddBtn(r, Ui.Green, () => Loc.Get("buyFeed") + "\n" + Money(50 * farm.Price("feed_kg")), BuyFeed, 30);
            AddBtn(r, Ui.Green, () => Loc.Get("buyWater") + "\n" + Money(200 * farm.Price("water_l")), BuyWater, 30);
            AddLabel(c, () => Loc.Get("animalsMarket"), 36, new Color(1, .9f, .4f));
            foreach (var id in AllowedSpecies())
            {
                string sid = id; var row = Ui.Row(c, 120);
                AddBtn(row, Ui.Green, () => Loc.Get("buyPair") + " " + Loc.SpeciesName(sid) + "\n" + Money(2 * farm.Price("buy:" + sid)), () => BuyPair(sid), 28);
                AddBtn(row, Ui.Blue, () => Loc.Get("sellAdult") + " " + Loc.SpeciesName(sid) + "\n" + Money(farm.Price("sell:" + sid)), () => SellAdult(sid), 28);
            }
            AddLabel(c, () => Loc.Get("eggsMarket"), 36, new Color(1, .9f, .4f));
            var er = Ui.Row(c, 100);
            AddBtn(er, Ui.Blue, () => Loc.Get("sellEggs") + "\n" + farm.S.InfertileEggs + " x " + N(farm.Price("egg")), SellEggs, 28);
            AddBtn(er, Ui.Green, () => Loc.Get("incubate") + "\n" + farm.S.FertileEggs, Incubate, 28);
            AddLabel(c, () => Loc.Get("breeding"), 36, new Color(1, .9f, .4f));
            foreach (var id in AllowedSpecies().Where(i => SpeciesCatalog.All[i].GestationDays > 0))
            { string sid = id; var br = Ui.Row(c, 100); AddBtn(br, Ui.Green, () => Loc.Get("breed") + " " + Loc.SpeciesName(sid) + "\n" + Loc.Get("pregnant") + ": " + farm.PregnantCount(sid), () => Breed(sid), 28); }
            AddLabel(c, () => Loc.Get("prices") + "\n" + Loc.Get("currencyNote"), 32, new Color(1, .9f, .4f));
            var keys = new List<string> { "feed_kg", "water_l", "egg" }; foreach (var id in AllowedSpecies()) { keys.Add("buy:" + id); keys.Add("sell:" + id); }
            foreach (var key in keys)
            {
                string k = key; var row = Ui.Row(c, 90);
                AddBtn(row, Ui.Red, () => "-", () => AdjustPrice(k, -1), 40, 0, 130);
                var t = Ui.MakeText(row, 28, TextAnchor.MiddleCenter); Ui.Flex(t.gameObject, 3);
                labels.Add(new LabelBind { T = t, Screen = "market", F = () => PriceName(k) + "\n" + Money(farm.Price(k)) + (farm.S.Prices.Any(p => p.Key == k && p.IsExample) ? " " + Loc.Get("example") : "") });
                AddBtn(row, Ui.Green, () => "+", () => AdjustPrice(k, +1), 40, 0, 130);
            }
            return panel;
        }
        void AdjustPrice(string key, int dir)
        {
            double cur = farm.Price(key), step = Math.Max(0.01, Math.Round(cur * 0.1, 2));
            if (!PriceBook.Set(farm.S, key, Math.Max(0, cur + dir * step))) Toast(Loc.Get("fail"));
        }

        // ---------------- budget ----------------
        string BudgetText()
        {
            var S = farm.S; int day = Day, from = Mathf.Max(0, day - 29); var r = Reports.Period(S, from, day); double nf, nw; farm.NeedsPerDay(day, out nf, out nw);
            var sb = new StringBuilder();
            sb.AppendLine(Loc.Get("cash") + ": " + Money(S.Cash));
            sb.AppendLine(Loc.Get("netWorth") + ": " + Money(farm.NetWorth()));
            sb.AppendLine(Loc.Get("animalValue") + ": " + Money(farm.AnimalValue()));
            sb.AppendLine(Loc.Get("stockValue") + ": " + Money(farm.StockValue()));
            sb.AppendLine();
            sb.AppendLine(Loc.Get("last30") + ": " + (from + 1) + " - " + (day + 1));
            sb.AppendLine(Loc.Get("netCash") + ": " + Money(r.NetCashFlow));
            sb.AppendLine(Loc.Get("opProfit") + ": " + Money(r.OperatingProfit));
            sb.AppendLine(Loc.Get("revenue") + ": " + Money(r.ProductRevenue));
            sb.AppendLine(Loc.Get("opex") + ": " + Money(r.OperatingExpenses));
            sb.AppendLine(Loc.Get("animalBuys") + ": " + Money(r.AnimalPurchases));
            sb.AppendLine(Loc.Get("animalSells") + ": " + Money(r.AnimalSales));
            sb.AppendLine(Loc.Get("capitalNote"));
            sb.AppendLine();
            sb.AppendLine(Loc.Get("needsPerDay") + ": " + N(nf, "F1") + " " + Loc.Get("kg") + " | " + N(nw, "F1") + " " + Loc.Get("l"));
            sb.AppendLine(Loc.Get("stockDays") + ": " + (nf > 0 ? N(farm.StockOf(Farm.Feed) / nf, "F1") : "-") + " | " + (nw > 0 ? N(farm.StockOf(Farm.Water) / nw, "F1") : "-"));
            sb.AppendLine();
            sb.AppendLine(Loc.Get("composition"));
            var comp = Reports.Composition(S, day);
            if (comp.Count == 0) sb.AppendLine(Loc.Get("none"));
            foreach (var cc in comp) sb.AppendLine(Loc.SpeciesName(cc.SpeciesId) + ": " + Loc.Get("males") + " " + cc.Males + " | " + Loc.Get("females") + " " + cc.Females + " | " + Loc.Get("juveniles") + " " + cc.Juveniles);
            return sb.ToString().TrimEnd();
        }
        GameObject BuildBudget(Transform root)
        {
            var panel = ScreenPanel(root, "Budget"); var c = Ui.MakeScroll(panel.transform, 0, 0, 1, 1);
            AddLabel(c, BudgetText, 30);
            AddLabel(c, () => Loc.Get("chartCash"), 28, new Color(1, 1, .8f));
            budgetChart = new Ui.BarChart(c, 320, new Color(.3f, .8f, .4f), new Color(.8f, .3f, .3f));
            return panel;
        }
        void UpdateBudgetChart()
        {
            if (budgetChart == null) return; int day = Day, from = Mathf.Max(0, day - 29); double[] rev, exp, cash; Reports.DailySeries(farm.S, from, day, out rev, out exp, out cash); budgetChart.Set(cash);
        }

        // ---------------- reports ----------------
        string ReportsText()
        {
            var S = farm.S; int day = Day, span = period == 0 ? 0 : period == 1 ? 29 : 364, from = Mathf.Max(0, day - span);
            var r = Reports.Period(S, from, day); var m = farm.Mortality(from, day); var sb = new StringBuilder();
            sb.AppendLine(Loc.Get("period") + ": " + (from + 1) + " - " + (day + 1));
            sb.AppendLine(Loc.Get("births") + ": " + r.Births + " | " + Loc.Get("hatched") + ": " + r.Hatched);
            sb.AppendLine(Loc.Get("deaths") + ": " + m.Deaths + " | " + Loc.Get("mortality") + ": " + (m.AliveAtStart > 0 ? N(m.Ratio * 100, "F1") + "% " + Loc.Get("of") + " " + m.AliveAtStart : Loc.Get("na")));
            sb.AppendLine(Loc.Get("mortalityNote"));
            sb.AppendLine(Loc.Get("eggs") + ": " + S.FertileEggs + " + " + S.InfertileEggs);
            sb.AppendLine();
            sb.AppendLine(Loc.Get("byCategory"));
            foreach (var kv in r.ByCategory.OrderBy(k => k.Key)) sb.AppendLine(Loc.Get("cat_" + kv.Key) + ": " + Money(kv.Value));
            sb.AppendLine(Loc.Get("netCash") + ": " + Money(r.NetCashFlow) + " | " + Loc.Get("opProfit") + ": " + Money(r.OperatingProfit));
            return sb.ToString().TrimEnd();
        }
        string ScenarioText()
        {
            var sb = new StringBuilder(); sb.AppendLine(Loc.Get("scenNote"));
            if (scenarioRunning) sb.AppendLine(Loc.Get("running") + " " + scenarioProgress);
            foreach (var res in scenarioResults)
            {
                var s = res.Scenario; sb.AppendLine();
                sb.AppendLine(Loc.IsArabic ? s.NameAr : s.NameEn);
                sb.AppendLine((Loc.IsArabic ? s.DescAr : s.DescEn).Replace("، ", "\n").Replace(", ", "\n"));
                sb.AppendLine(Loc.Get("scenRange") + " " + res.Days + " " + Loc.Get("days") + ":");
                sb.AppendLine(N(res.NetWorthEnd.Min, "F0") + " ... " + N(res.NetWorthEnd.Max, "F0") + " " + farm.S.Currency + " | " + Loc.Get("median") + " " + N(res.NetWorthEnd.Median, "F0"));
                sb.AppendLine(Loc.Get("scenChange") + ": " + N(res.Change.Min, "F0") + " ... " + N(res.Change.Max, "F0"));
            }
            return sb.ToString().TrimEnd();
        }
        GameObject BuildReports(Transform root)
        {
            var panel = ScreenPanel(root, "Reports"); var c = Ui.MakeScroll(panel.transform, 0, 0, 1, 1);
            var pr = Ui.Row(c, 90); string[] pk = { "period_day", "period_month", "period_year" };
            for (int i = 0; i < 3; i++) { int ix = i; string key = pk[i]; AddBtn(pr, Ui.Gray, () => (period == ix ? "> " : "") + Loc.Get(key), () => period = ix, 30); }
            AddLabel(c, ReportsText, 30);
            AddLabel(c, () => Loc.Get("chartRevExp"), 28, new Color(1, 1, .8f));
            reportsChart = new Ui.BarChart(c, 320, new Color(.3f, .8f, .4f), new Color(.8f, .3f, .3f));
            var ex = Ui.Row(c, 100);
            AddBtn(ex, Ui.Blue, () => Loc.Get("exportLedger"), () => CopyCsv(true), 28); AddBtn(ex, Ui.Blue, () => Loc.Get("exportEvents"), () => CopyCsv(false), 28);
            AddLabel(c, () => Loc.Get("scenTitle"), 36, new Color(1, .9f, .4f));
            var sr = Ui.Row(c, 100); AddBtn(sr, Ui.Green, () => scenarioRunning ? Loc.Get("running") : Loc.Get("runScen"), () => { if (!scenarioRunning) StartCoroutine(RunScenarios()); }, 30);
            AddLabel(c, ScenarioText, 28);
            AddLabel(c, () => Loc.Get("recentEvents") + "\n" + string.Join("\n", farm.S.Events.Where(e => e.Type != "purchase").Reverse().Take(12).Select(EventLine)), 28);
            return panel;
        }
        void UpdateReportsChart()
        {
            if (reportsChart == null) return; int day = Day, from = Mathf.Max(0, day - 29); double[] rev, exp, cash; Reports.DailySeries(farm.S, from, day, out rev, out exp, out cash); reportsChart.Set(rev, exp);
        }
        IEnumerator RunScenarios()
        {
            scenarioRunning = true; scenarioResults.Clear(); const int runs = 8, days = 365; double startNw = farm.NetWorth();
            foreach (var sc in Scenarios.All)
            {
                var ends = new List<double>();
                for (int r = 0; r < runs; r++) { scenarioProgress = (r + 1) + "/" + runs; ends.Add(Scenarios.RunOnce(data.Farm, sc, days, data.Farm.Seed + r * 101)); yield return null; }
                scenarioResults.Add(Scenarios.Summarize2(sc, days, runs, startNw, ends));
            }
            scenarioRunning = false;
        }

        // ---------------- more (settings / help / about) ----------------
        GameObject BuildMore(Transform root)
        {
            var panel = ScreenPanel(root, "More"); var c = Ui.MakeScroll(panel.transform, 0, 0, 1, 1); var gold = new Color(1, .9f, .4f);
            AddLabel(c, () => Loc.Get("settings"), 38, gold);
            var r1 = Ui.Row(c, 100);
            AddBtn(r1, Ui.Blue, () => Loc.Get("lang"), ToggleLang, 30);
            AddBtn(r1, Ui.Blue, () => Loc.Get("quality") + ": " + Loc.Get("q" + data.Quality), () => { data.Quality = (data.Quality + 1) % 3; ApplyQuality(); }, 28);
            var r2 = Ui.Row(c, 100);
            AddBtn(r2, Ui.Blue, () => Loc.Get("offline") + ": " + (data.Clock.OfflineProgressEnabled ? Loc.Get("yes") : Loc.Get("no")), () => data.Clock.OfflineProgressEnabled = !data.Clock.OfflineProgressEnabled, 28);
            AddBtn(r2, Ui.Blue, () => Loc.Get("randomEvents") + ": " + (farm.S.RandomEvents ? Loc.Get("yes") : Loc.Get("no")), () => farm.S.RandomEvents = !farm.S.RandomEvents, 28);
            var r3 = Ui.Row(c, 100);
            AddBtn(r3, Ui.Red, () => "-12", () => data.Clock.MaxOfflineHours = Math.Max(0, data.Clock.MaxOfflineHours - 12), 36, 0, 130);
            var mt = Ui.MakeText(r3, 28, TextAnchor.MiddleCenter); Ui.Flex(mt.gameObject, 3); labels.Add(new LabelBind { T = mt, Screen = "more", F = () => Loc.Get("maxOffline") + ": " + N(data.Clock.MaxOfflineHours, "F0") + " " + Loc.Get("hours") });
            AddBtn(r3, Ui.Green, () => "+12", () => data.Clock.MaxOfflineHours = Math.Min(240, data.Clock.MaxOfflineHours + 12), 36, 0, 130);
            var r4 = Ui.Row(c, 100);
            AddBtn(r4, Ui.Blue, () => Loc.Get("currency") + ": " + farm.S.Currency, () => { int i = Array.IndexOf(Currencies, farm.S.Currency); farm.S.Currency = Currencies[(i + 1) % Currencies.Length]; }, 28);
            AddLabel(c, () => Loc.Get("speedNote"), 26);
            AddLabel(c, () => Loc.Get("cloud") + ": " + Loc.Get("cloudOff") + "\n" + Loc.Get("ads") + ": " + Loc.Get("adsOff"), 28);
            AddLabel(c, () => Loc.Get("help") + "\n" + Loc.Get("helpText"), 28, gold);
            AddLabel(c, () => Loc.Get("privacy") + "\n" + Loc.Get("privacyText"), 28, gold);
            AddLabel(c, () => Loc.Get("about") + ": " + Loc.Get("title") + " " + Version, 28);
            var r5 = Ui.Row(c, 110);
            AddBtn(r5, Ui.Red, () => confirmNewFarm ? Loc.Get("confirmAgain") : Loc.Get("newFarm"), () => { if (!confirmNewFarm) { confirmNewFarm = true; return; } confirmNewFarm = false; started = false; Show("start"); }, 30);
            return panel;
        }

        // ---------------- start screen ----------------
        GameObject BuildStart(Transform root)
        {
            var panel = ScreenPanel(root, "Start", true); var c = Ui.MakeScroll(panel.transform, 0, 0, 1, 1);
            var title = Ui.MakeText(c, 64, TextAnchor.MiddleCenter, new Color(1, .9f, .4f)); Ui.Flex(title.gameObject, 1); labels.Add(new LabelBind { T = title, Screen = "start", F = () => Loc.Get("title") });
            title.GetComponent<LayoutElement>().minHeight = 120;
            AddLabel(c, () => Loc.Get("startSub") + "\n" + Loc.Get("localPlay"), 28);
            AddLabel(c, () => Loc.Get("chooseType"), 36, new Color(1, .9f, .4f));
            foreach (var def in FarmTypes.All)
            {
                var d = def; var row = Ui.Row(c, 210);
                AddBtn(row, Ui.Green, () => (Loc.IsArabic ? d.NameAr : d.NameEn) + "\n" + string.Join(" ", d.Species.Select(Loc.SpeciesName)) + "\n" + Loc.Get("cash") + ": " + N(d.StartCash, "F0") + " " + farm.S.Currency + " " + Loc.Get("example"), () => StartFarm(d.Id), 30);
            }
            var r = Ui.Row(c, 100);
            AddBtn(r, Ui.Blue, () => Loc.Get("lang"), ToggleLang, 30);
            if (!string.IsNullOrEmpty(data.Farm.FarmType)) AddBtn(r, Ui.Gray, () => Loc.Get("backToFarm"), () => { started = true; data.Clock.LastUpdateUtcTicks = DateTime.UtcNow.Ticks; Show("farm"); }, 30);
            return panel;
        }
    }
}
