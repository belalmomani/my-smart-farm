using System;
using System.IO;

namespace MySmartFarm.Core
{
    [Serializable] public class SaveData
    {
        public int SchemaVersion = SaveMigrator.Current;
        public FarmState Farm = new FarmState();
        public SimClock Clock = new SimClock();
        public string Language = "ar";
        public int Quality = 1;          // 0 low, 1 medium, 2 high (schema v2)
        public int ScenarioIndex = 1;    // index into Scenarios.All (schema v2)
    }

    public static class SaveMigrator
    {
        public const int Current = 2;
        /// <summary>Upgrade older saves step by step. Add a case per version bump. Newer-than-known saves are rejected, never silently loaded.</summary>
        public static SaveData Migrate(SaveData d)
        {
            if (d == null || d.Farm == null || d.Clock == null) throw new InvalidDataException("corrupt save");
            if (d.SchemaVersion > Current) throw new NotSupportedException("save is from a newer app version");
            if (d.SchemaVersion < 2)   // v1 -> v2: price book, farm type/currency fields (defaults come from field initializers)
            {
                PriceBook.EnsureDefaults(d.Farm); if (d.Quality < 0 || d.Quality > 2) d.Quality = 1; d.SchemaVersion = 2;
            }
            return d;
        }
    }

    public interface ISaveSerializer { string Serialize(SaveData d); SaveData Deserialize(string json); }

    /// <summary>Crash-safe file save: write tmp, keep previous as .bak, then swap. Load falls back to .bak.</summary>
    public class SaveManager
    {
        readonly string path; readonly ISaveSerializer ser;
        public SaveManager(string path, ISaveSerializer ser) { this.path = path; this.ser = ser; }
        public bool Exists => File.Exists(path) || File.Exists(path + ".bak");

        public void Save(SaveData d)
        {
            d.SchemaVersion = SaveMigrator.Current;
            string json = ser.Serialize(d), tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) { File.Copy(path, path + ".bak", true); File.Delete(path); }
            File.Move(tmp, path);
        }

        public SaveData Load()
        {
            foreach (var p in new[] { path, path + ".bak" })
            {
                if (!File.Exists(p)) continue;
                try { return SaveMigrator.Migrate(ser.Deserialize(File.ReadAllText(p))); }
                catch (NotSupportedException) { throw; }
                catch (Exception) { /* corrupt: try backup */ }
            }
            return null;
        }
        public void Delete() { foreach (var p in new[] { path, path + ".bak", path + ".tmp" }) if (File.Exists(p)) File.Delete(p); }
    }
}
