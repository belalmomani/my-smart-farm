using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using MySmartFarm.Core;

namespace MySmartFarm.Tests
{
    /// <summary>Engine-free tests of the file logic (atomic write, .bak fallback, schema guard) using a fake serializer.</summary>
    public class SaveManagerTests
    {
        class FakeSer : ISaveSerializer
        {
            readonly Dictionary<string, SaveData> store = new Dictionary<string, SaveData>(); int n;
            public string Serialize(SaveData d) { string t = "tok" + (n++); store[t] = d; return t; }
            public SaveData Deserialize(string j) { SaveData d; if (!store.TryGetValue(j, out d)) throw new InvalidDataException("corrupt"); return d; }
        }
        string dir;
        [SetUp] public void Up() { dir = Path.Combine(Path.GetTempPath(), "msf_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); }
        [TearDown] public void Down() { Directory.Delete(dir, true); }

        [Test] public void Load_NoFile_ReturnsNull() { Assert.IsNull(new SaveManager(Path.Combine(dir, "x.json"), new FakeSer()).Load()); }
        [Test] public void Save_ThenLoad_ReturnsSameData()
        {
            var m = new SaveManager(Path.Combine(dir, "s.json"), new FakeSer()); var d = new SaveData(); d.Farm.Cash = 12; m.Save(d);
            Assert.AreEqual(12, m.Load().Farm.Cash, 1e-9); Assert.IsFalse(File.Exists(Path.Combine(dir, "s.json.tmp")));
        }
        [Test] public void Load_FallsBackToBackup_WhenMainCorrupt()
        {
            var p = Path.Combine(dir, "s.json"); var m = new SaveManager(p, new FakeSer()); var d = new SaveData(); d.Farm.Cash = 77; m.Save(d); m.Save(d);
            File.WriteAllText(p, "garbage"); Assert.AreEqual(77, m.Load().Farm.Cash, 1e-9);
        }
        [Test] public void Load_RejectsNewerSchema()
        {
            var p = Path.Combine(dir, "s.json"); var ser = new FakeSer(); var d = new SaveData(); d.SchemaVersion = 99;
            File.WriteAllText(p, ser.Serialize(d)); Assert.Throws<NotSupportedException>(() => new SaveManager(p, ser).Load());
        }
        [Test] public void Save_StampsCurrentSchemaVersion()
        { var m = new SaveManager(Path.Combine(dir, "s.json"), new FakeSer()); var d = new SaveData { SchemaVersion = 1 }; m.Save(d); Assert.AreEqual(SaveMigrator.Current, d.SchemaVersion); }
    }
}
