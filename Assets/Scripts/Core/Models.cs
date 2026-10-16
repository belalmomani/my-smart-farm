using System;
using System.Collections.Generic;

namespace MySmartFarm.Core
{
    public enum Sex { Male, Female }
    public enum Season { Spring, Summer, Autumn, Winter }
    public enum TxCategory { AnimalPurchase, FeedPurchase, WaterPurchase, Care, Maintenance, Labor, Other, EggSale, MeatSale, MilkSale, AnimalSale }

    [Serializable] public class Animal
    {
        public string Id; public string SpeciesId; public Sex Sex;
        public int BirthDay; public double Health = 100; public int DeficitDays;
        public int PregnantUntilDay = -1; public int LastBirthDay = -100000;
        public bool Alive = true; public int DeathDay = -1; public string CauseOfDeath;
    }
    [Serializable] public class StockItem { public string Key; public double Qty; }
    [Serializable] public class Transaction { public string Id; public int Day; public TxCategory Category; public double Amount; public string Memo; }
    [Serializable] public class FarmEvent { public string Id; public int Day; public string Type; public string SubjectId; public string Detail; }
    [Serializable] public class Incubation { public string Id; public string SpeciesId; public int Count; public int HatchDay; }

    /// <summary>Serializable save state (lists only, so Unity JsonUtility works). Bump SchemaVersion + add a migration on change.</summary>
    [Serializable] public class FarmState
    {
        public int SchemaVersion = 1;
        public int Seed = 12345;
        public int LastProcessedDay;
        public int NextSeq;
        public double Cash;
        public int FertileEggs, InfertileEggs;
        public List<Animal> Animals = new List<Animal>();
        public List<StockItem> Stock = new List<StockItem>();
        public List<Transaction> Ledger = new List<Transaction>();
        public List<FarmEvent> Events = new List<FarmEvent>();
        public List<Incubation> Incubations = new List<Incubation>();
    }

    /// <summary>Biological values here are SIMULATION DEFAULTS (approximate, unsourced). Replace with cited values; see docs/DATA_SOURCES.md.</summary>
    public class SpeciesDef
    {
        public string Id, NameAr, NameEn;
        public int MaturityDays, GestationDays, IncubationDays, RecoveryDays, LitterMin, LitterMax, MaxAgeDays;
        public double FeedKgAdult, FeedKgJuvenile, WaterLAdult, WaterLJuvenile;
        public double BaseDailyMortality, EggsPerLayingDay, FertilityRate, BuyPriceJod, SellPriceJod;
        public bool LaysEggs;
        public string SourceNote = "simulation default; not a verified scientific value";
    }

    public static class SpeciesCatalog
    {
        static SpeciesDef S(string id, string ar, string en, int mat, int gest, int inc, int rec, int lmin, int lmax, int maxAge,
            double fa, double fj, double wa, double wj, double mort, bool eggs, double eggRate, double fert, double buy, double sell) =>
            new SpeciesDef { Id = id, NameAr = ar, NameEn = en, MaturityDays = mat, GestationDays = gest, IncubationDays = inc, RecoveryDays = rec,
                LitterMin = lmin, LitterMax = lmax, MaxAgeDays = maxAge, FeedKgAdult = fa, FeedKgJuvenile = fj, WaterLAdult = wa, WaterLJuvenile = wj,
                BaseDailyMortality = mort, LaysEggs = eggs, EggsPerLayingDay = eggRate, FertilityRate = fert, BuyPriceJod = buy, SellPriceJod = sell };

        public static readonly Dictionary<string, SpeciesDef> All = new Dictionary<string, SpeciesDef>
        {
            ["rabbit"]  = S("rabbit","أرنب","Rabbit",150,31,0,14,4,8,2900,0.15,0.07,0.3,0.15,0.0002,false,0,0,10,6),
            ["chicken"] = S("chicken","دجاج","Chicken",150,0,21,0,0,0,2200,0.11,0.04,0.25,0.08,0.0002,true,0.7,0.9,4,3),
            ["duck"]    = S("duck","بط","Duck",180,0,28,0,0,0,2500,0.15,0.06,0.4,0.15,0.0002,true,0.6,0.85,6,5),
            ["sheep"]   = S("sheep","أغنام","Sheep",300,150,0,60,1,2,3600,2.0,0.8,5,2,0.0001,false,0,0,120,100),
            ["goat"]    = S("goat","ماعز","Goat",300,150,0,60,1,3,3600,2.0,0.8,5,2,0.0001,false,0,0,110,90),
            ["cow"]     = S("cow","أبقار","Cow",540,283,0,90,1,1,5400,12,5,40,15,0.00005,false,0,0,700,600),
        };
    }

    public static class Seasons
    {
        public const int DaysPerYear = 365;
        /// <summary>Configurable start day-of-year for Spring, Summer, Autumn, Winter.</summary>
        public static int[] StartDays = { 0, 91, 182, 273 };
        public static int DayOfYear(int virtualDay) => ((virtualDay % DaysPerYear) + DaysPerYear) % DaysPerYear;
        public static int Year(int virtualDay) => virtualDay / DaysPerYear + 1;
        public static Season Of(int virtualDay)
        {
            int d = DayOfYear(virtualDay);
            for (int i = 3; i >= 0; i--) if (d >= StartDays[i]) return (Season)i;
            return Season.Winter;
        }
    }
}
