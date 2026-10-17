using System;
using System.Collections.Generic;
using System.Linq;

namespace MySmartFarm.Core
{
    /// <summary>User-editable prices. All defaults are EXAMPLE values (JOD), not market prices. Keys: feed_kg, water_l, egg, buy:&lt;species&gt;, sell:&lt;species&gt;.</summary>
    public static class PriceBook
    {
        public static IEnumerable<string> AllKeys()
        {
            yield return "feed_kg"; yield return "water_l"; yield return "egg";
            foreach (var id in SpeciesCatalog.All.Keys) { yield return "buy:" + id; yield return "sell:" + id; }
        }
        public static double Default(string key)
        {
            if (key == "feed_kg") return 0.30; if (key == "water_l") return 0.01; if (key == "egg") return 0.10;
            SpeciesDef sp;
            if (key.StartsWith("buy:") && SpeciesCatalog.All.TryGetValue(key.Substring(4), out sp)) return sp.BuyPriceJod;
            if (key.StartsWith("sell:") && SpeciesCatalog.All.TryGetValue(key.Substring(5), out sp)) return sp.SellPriceJod;
            return 0;
        }
        public static void EnsureDefaults(FarmState s)
        {
            foreach (var k in AllKeys()) if (!s.Prices.Any(p => p.Key == k)) s.Prices.Add(new PriceEntry { Key = k, Value = Default(k), IsExample = true });
        }
        public static double Get(FarmState s, string key) { var e = s.Prices.FirstOrDefault(p => p.Key == key); return e != null ? e.Value : Default(key); }
        public static bool Set(FarmState s, string key, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v > 1000000) return false;
            var e = s.Prices.FirstOrDefault(p => p.Key == key); if (e == null) return false;
            e.Value = Math.Round(v, 2); e.IsExample = false; return true;
        }
    }

    public static class FarmTypes
    {
        public class Def { public string Id, NameAr, NameEn; public string[] Species; public double StartCash; }
        /// <summary>StartCash values are EXAMPLE game values.</summary>
        public static readonly Def[] All =
        {
            new Def { Id = "poultry", NameAr = "مزرعة دواجن", NameEn = "Poultry farm", Species = new[] { "chicken", "duck" }, StartCash = 600 },
            new Def { Id = "rabbits", NameAr = "مزرعة أرانب", NameEn = "Rabbit farm", Species = new[] { "rabbit" }, StartCash = 500 },
            new Def { Id = "ruminants", NameAr = "مزرعة أغنام وماعز وأبقار", NameEn = "Sheep, goat & cattle farm", Species = new[] { "sheep", "goat", "cow" }, StartCash = 3000 },
            new Def { Id = "mixed", NameAr = "مزرعة مختلطة", NameEn = "Mixed farm", Species = new[] { "rabbit", "chicken", "duck", "sheep", "goat", "cow" }, StartCash = 2000 },
        };
        public static Def Get(string id) => All.FirstOrDefault(d => d.Id == id);
        public static bool Allows(string typeId, string speciesId) { if (string.IsNullOrEmpty(typeId)) return true; var d = Get(typeId); return d != null && d.Species.Contains(speciesId); }
    }
}
