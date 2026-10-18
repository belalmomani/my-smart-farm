using System.IO;
using NUnit.Framework;
using UnityEngine;
using MySmartFarm.Core;

namespace MySmartFarm.Tests
{
    class JsonSer : ISaveSerializer
    {
        public string Serialize(SaveData d) => JsonUtility.ToJson(d);
        public SaveData Deserialize(string j) => JsonUtility.FromJson<SaveData>(j);
    }

    /// <summary>Unity-only: uses JsonUtility, so it cannot run in the plain .NET CI project.</summary>
    public class SaveTests
    {
        string dir;
        [SetUp] public void Up() { dir = Path.Combine(Path.GetTempPath(), "msf_" + System.Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); }
        [TearDown] public void Down() { Directory.Delete(dir, true); }

        [Test] public void Save_RoundTrip_ResumesWithoutReplay()
        {
            var d = new SaveData(); var f = new Farm(d.Farm); d.Farm.Cash = 5000;
            f.BuyStock("feed_kg", 100, 0.1, 0, TxCategory.FeedPurchase); f.BuyStock("water_l", 100, 0.01, 0, TxCategory.WaterPurchase);
            f.BuyAnimal("rabbit", Sex.Female, 200, 0); f.ProcessUntil(5);
            var m = new SaveManager(Path.Combine(dir, "s.json"), new JsonSer()); m.Save(d);
            var d2 = m.Load(); var f2 = new Farm(d2.Farm);
            Assert.AreEqual(5, d2.Farm.LastProcessedDay); Assert.AreEqual(d.Farm.Cash, d2.Farm.Cash, 1e-9);
            Assert.AreEqual(d.Farm.Animals.Count, d2.Farm.Animals.Count); Assert.AreEqual(d.Farm.Ledger.Count, d2.Farm.Ledger.Count);
            Assert.AreEqual(0, f2.ProcessUntil(5)); Assert.AreEqual(1, f2.ProcessUntil(6));
        }
        [Test] public void Load_FallsBackToBackup_WhenMainCorrupt()
        {
            var path = Path.Combine(dir, "s.json"); var m = new SaveManager(path, new JsonSer());
            var d = new SaveData(); d.Farm.Cash = 77; m.Save(d); m.Save(d);          // second save creates .bak
            File.WriteAllText(path, "{ not json");
            Assert.AreEqual(77, m.Load().Farm.Cash, 1e-9);
        }
        [Test] public void Load_RejectsNewerSchema()
        {
            var path = Path.Combine(dir, "s.json"); var d = new SaveData { SchemaVersion = 99 };
            File.WriteAllText(path, JsonUtility.ToJson(d)); var m = new SaveManager(path, new JsonSer());
            Assert.Throws<System.NotSupportedException>(() => m.Load());
        }
        [Test] public void Load_NoFile_ReturnsNull() { Assert.IsNull(new SaveManager(Path.Combine(dir, "none.json"), new JsonSer()).Load()); }
    }
}
