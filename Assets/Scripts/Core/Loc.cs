using System.Collections.Generic;

namespace MySmartFarm.Core
{
    /// <summary>Two-language string table. Default Arabic. Use Show() for on-screen text (shapes Arabic).</summary>
    public static class Loc
    {
        public static string Lang = "ar";
        public static bool IsArabic => Lang == "ar";
        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            ["title"] = new[] { "مزرعتي الذكية", "My Smart Farm" },
            ["day"] = new[] { "اليوم", "Day" }, ["year"] = new[] { "السنة", "Year" },
            ["cash"] = new[] { "النقد", "Cash" }, ["feed"] = new[] { "علف", "Feed" }, ["water"] = new[] { "ماء", "Water" },
            ["animals"] = new[] { "حيوانات", "Animals" }, ["eggs"] = new[] { "بيض", "Eggs" },
            ["kg"] = new[] { "كغ", "kg" }, ["l"] = new[] { "لتر", "L" },
            ["Spring"] = new[] { "الربيع", "Spring" }, ["Summer"] = new[] { "الصيف", "Summer" }, ["Autumn"] = new[] { "الخريف", "Autumn" }, ["Winter"] = new[] { "الشتاء", "Winter" },
            ["pause"] = new[] { "إيقاف", "Pause" }, ["resume"] = new[] { "استئناف", "Resume" },
            ["buyFeed"] = new[] { "شراء علف 50 كغ", "Buy feed 50 kg" }, ["buyWater"] = new[] { "شراء ماء 200 لتر", "Buy water 200 L" },
            ["buyPair"] = new[] { "شراء زوج", "Buy pair" }, ["sellEggs"] = new[] { "بيع البيض", "Sell eggs" }, ["incubate"] = new[] { "تفريخ البيض", "Incubate eggs" },
            ["save"] = new[] { "حفظ", "Save" }, ["lang"] = new[] { "English", "عربي" }, ["debugDay"] = new[] { "[تجريبي] +يوم", "[debug] +1 day" },
            ["noMoney"] = new[] { "النقد غير كاف", "Not enough cash" }, ["ok"] = new[] { "تم", "Done" }, ["fail"] = new[] { "تعذر التنفيذ", "Could not do it" },
            ["saved"] = new[] { "تم الحفظ", "Saved" }, ["disclaimer"] = new[] { "أسعار وقيم المحاكاة أمثلة تجريبية وليست حقائق", "Simulation prices/values are examples, not facts" },
            ["offline"] = new[] { "التقدم أثناء الغياب", "Offline progress" }, ["yes"] = new[] { "نعم", "Yes" }, ["no"] = new[] { "لا", "No" },
            ["alertDeficit"] = new[] { "تنبيه: حيوانات تعاني نقص علف/ماء", "Alert: animals short of feed/water" },
            ["noEvents"] = new[] { "لا أحداث بعد", "No events yet" }, ["ok2"] = new[] { "حسنا", "OK" },
            ["ev_birth"] = new[] { "ولادة", "Birth" }, ["ev_death"] = new[] { "نفوق", "Death" }, ["ev_hatch"] = new[] { "فقس", "Hatch" },
            ["ev_purchase"] = new[] { "شراء", "Purchase" }, ["ev_sale"] = new[] { "بيع", "Sale" }, ["ev_conception"] = new[] { "حمل", "Pregnancy" },
            ["ev_incubation_start"] = new[] { "بدء التفريخ", "Incubation" }, ["ev_alert_deficit"] = new[] { "نقص علف/ماء", "Feed/water shortage" },
        };
        public static string Get(string key) { string[] v; return T.TryGetValue(key, out v) ? v[IsArabic ? 0 : 1] : key; }
        public static string SpeciesName(string id) { var s = SpeciesCatalog.All[id]; return IsArabic ? s.NameAr : s.NameEn; }
        /// <summary>Prepare a (possibly multi-line) string for display.</summary>
        public static string Show(string text) => IsArabic ? ArabicShaper.ToVisualLines(text) : text;
    }
}
