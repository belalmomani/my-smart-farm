using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MySmartFarm.Core;

namespace MySmartFarm.Game
{
    sealed class JsonSer : ISaveSerializer
    {
        public string Serialize(SaveData d) => JsonUtility.ToJson(d);
        public SaveData Deserialize(string j) => JsonUtility.FromJson<SaveData>(j);
    }

    /// <summary>EXAMPLE prices in JOD (not market prices). Make user-editable in a later version.</summary>
    public static class Prices { public const double FeedPerKg = 0.30, WaterPerL = 0.01, EggPerUnit = 0.10; }

    public class GameController : MonoBehaviour
    {
        SaveData data; Farm farm; SaveManager saves; Canvas canvas; Font font;
        Text topText, logText, toast; float toastT, uiT, autosaveT;
        readonly List<KeyValuePair<Text, Func<string>>> labels = new List<KeyValuePair<Text, Func<string>>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindFirstObjectByType<GameController>() == null) new GameObject("Game").AddComponent<GameController>(); }

        static string N(double v, string f = "F2") => v.ToString(f, CultureInfo.InvariantCulture);
        int Day => data.Clock.WholeDay;

        void Awake()
        {
            Application.targetFrameRate = 30;
            saves = new SaveManager(Path.Combine(Application.persistentDataPath, "farm.json"), new JsonSer());
            try { data = saves.Load(); } catch (Exception e) { Debug.LogWarning("Save not loadable: " + e.Message); }
            if (data == null)
            {
                data = new SaveData(); data.Farm.Seed = Environment.TickCount; data.Farm.Cash = 1000;   // example starting cash
                data.Clock.LastUpdateUtcTicks = DateTime.UtcNow.Ticks;
            }
            else data.Clock.ApplyOffline(DateTime.UtcNow);
            Loc.Lang = data.Language ?? "ar";
            farm = new Farm(data.Farm); farm.ProcessUntil(data.Clock.WholeDay);
            gameObject.AddComponent<FarmView>().Init(farm, data.Clock);
            BuildUi(); Refresh();
        }

        void Update()
        {
            data.Clock.Advance(Mathf.Min(Time.unscaledDeltaTime, 0.5f), DateTime.UtcNow);
            farm.ProcessUntil(data.Clock.WholeDay);
            uiT -= Time.unscaledDeltaTime; if (uiT <= 0) { uiT = 0.25f; Refresh(); }
            autosaveT += Time.unscaledDeltaTime; if (autosaveT > 60) { autosaveT = 0; Save(false); }
            if (toastT > 0) { toastT -= Time.unscaledDeltaTime; if (toastT <= 0) toast.text = ""; }
        }

        void OnApplicationPause(bool paused)
        {
            if (data == null) return;
            if (paused) Save(false);
            else { data.Clock.ApplyOffline(DateTime.UtcNow); farm.ProcessUntil(data.Clock.WholeDay); }
        }
        void OnApplicationQuit() { if (data != null) Save(false); }

        void Save(bool notify)
        {
            try { data.Language = Loc.Lang; saves.Save(data); if (notify) Toast(Loc.Get("saved")); }
            catch (Exception e) { Debug.LogError("Save failed: " + e.Message); if (notify) Toast(Loc.Get("fail")); }
        }
        void Toast(string s) { toast.text = Loc.Show(s); toastT = 2f; }

        // ---------------- actions ----------------
        void BuyStock(string key, double qty, double price, TxCategory cat) => Toast(farm.BuyStock(key, qty, price, Day, cat) ? Loc.Get("ok") : Loc.Get("noMoney"));
        void BuyPair(string id)
        {
            var sp = SpeciesCatalog.All[id];
            if (farm.S.Cash < 2 * sp.BuyPriceJod) { Toast(Loc.Get("noMoney")); return; }
            farm.BuyAnimal(id, Sex.Male, sp.MaturityDays + 30, Day); farm.BuyAnimal(id, Sex.Female, sp.MaturityDays + 30, Day); Toast(Loc.Get("ok"));
        }
        void SellEggs() { int n = farm.S.InfertileEggs; Toast(n > 0 && farm.SellEggs(n, false, Prices.EggPerUnit, Day) ? Loc.Get("ok") : Loc.Get("fail")); }
        void Incubate() { Toast(farm.SetIncubation("chicken", farm.S.FertileEggs, Day) > 0 ? Loc.Get("ok") : Loc.Get("fail")); }

        // ---------------- UI ----------------
        Font GetFont()
        {
            try { var f = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans Arabic", "Noto Naskh Arabic", "Droid Arabic Naskh", "Tahoma", "Arial" }, 40); if (f != null) return f; } catch { }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        Text MakeText(Transform p, float x0, float y0, float x1, float y1, int size, TextAnchor a)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
        }

        void MakeButton(Transform p, int row, int col, int cols, Func<string> label, Action onClick)
        {
            const float panelTop = 0.40f, rows = 6f; float rh = panelTop / rows, y1 = panelTop - row * rh, y0 = y1 - rh, w = 1f / cols;
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = new Vector2(col * w, y0); rt.anchorMax = new Vector2((col + 1) * w, y1); rt.offsetMin = new Vector2(6, 6); rt.offsetMax = new Vector2(-6, -6);
            go.GetComponent<Image>().color = new Color(.15f, .45f, .25f, .95f); go.GetComponent<Button>().onClick.AddListener(() => onClick());
            var t = MakeText(go.transform, 0, 0, 1, 1, 36, TextAnchor.MiddleCenter); labels.Add(new KeyValuePair<Text, Func<string>>(t, label));
        }

        void BuildUi()
        {
            if (font == null) font = GetFont();
            labels.Clear();
            var cg = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1920); sc.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Image Panel(float y0, float y1) { var g = new GameObject("Panel", typeof(RectTransform), typeof(Image)); g.transform.SetParent(cg.transform, false);
                var r = (RectTransform)g.transform; r.anchorMin = new Vector2(0, y0); r.anchorMax = new Vector2(1, y1); r.offsetMin = r.offsetMax = Vector2.zero; var im = g.GetComponent<Image>(); im.color = new Color(0, 0, 0, .55f); im.raycastTarget = true; return im; }
            Panel(0.80f, 1f); Panel(0f, 0.40f);
            var al = Loc.IsArabic ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            topText = MakeText(cg.transform, 0.02f, 0.89f, 0.98f, 0.995f, 38, al);
            logText = MakeText(cg.transform, 0.02f, 0.80f, 0.98f, 0.89f, 30, al); logText.color = new Color(1, 1, .8f);
            toast = MakeText(cg.transform, 0, 0.42f, 1, 0.5f, 44, TextAnchor.MiddleCenter); toast.color = new Color(1, .9f, .4f);

            MakeButton(cg.transform, 0, 0, 4, () => data.Clock.Paused ? Loc.Get("resume") : Loc.Get("pause"), () => data.Clock.Paused = !data.Clock.Paused);
            foreach (var s in new[] { 1, 2, 4 }) { int sp = s; int col = s == 1 ? 1 : s == 2 ? 2 : 3; MakeButton(cg.transform, 0, col, 4, () => (data.Clock.Speed == sp ? "[" + sp + "x]" : sp + "x"), () => data.Clock.SetSpeed(sp)); }
            MakeButton(cg.transform, 1, 0, 2, () => Loc.Get("buyFeed"), () => BuyStock(Farm.Feed, 50, Prices.FeedPerKg, TxCategory.FeedPurchase));
            MakeButton(cg.transform, 1, 1, 2, () => Loc.Get("buyWater"), () => BuyStock(Farm.Water, 200, Prices.WaterPerL, TxCategory.WaterPurchase));
            var ids = new[] { "rabbit", "chicken", "duck", "sheep", "goat", "cow" };
            for (int i = 0; i < 6; i++) { string id = ids[i]; MakeButton(cg.transform, 2 + i / 3, i % 3, 3, () => Loc.Get("buyPair") + " " + Loc.SpeciesName(id), () => BuyPair(id)); }
            MakeButton(cg.transform, 4, 0, 3, () => Loc.Get("sellEggs"), SellEggs);
            MakeButton(cg.transform, 4, 1, 3, () => Loc.Get("incubate"), Incubate);
            MakeButton(cg.transform, 4, 2, 3, () => Loc.Get("offline") + ": " + (data.Clock.OfflineProgressEnabled ? Loc.Get("yes") : Loc.Get("no")), () => data.Clock.OfflineProgressEnabled = !data.Clock.OfflineProgressEnabled);
            bool dbg = Debug.isDebugBuild || Application.isEditor; int cols = dbg ? 3 : 2;
            MakeButton(cg.transform, 5, 0, cols, () => Loc.Get("save"), () => Save(true));
            MakeButton(cg.transform, 5, 1, cols, () => Loc.Get("lang"), () => { Loc.Lang = Loc.IsArabic ? "en" : "ar"; data.Language = Loc.Lang; Destroy(canvas.gameObject); BuildUi(); Refresh(); });
            if (dbg) MakeButton(cg.transform, 5, 2, cols, () => Loc.Get("debugDay"), () => data.Clock.VirtualDay += 1);
        }

        static string Num(string detail, string key)
        {
            foreach (var part in detail.Split(';')) { var kv = part.Split('='); if (kv.Length == 2 && kv[0] == key) return kv[1]; }
            return "";
        }

        void Refresh()
        {
            var S = farm.S; int day = Day; int alive = S.Animals.Count(a => a.Alive); int hungry = S.Animals.Count(a => a.Alive && a.DeficitDays > 0);
            string line1 = Loc.Get("day") + " " + (day + 1) + " | " + Loc.Get("year") + " " + Seasons.Year(day) + " | " + Loc.Get(Seasons.Of(day).ToString());
            string line2 = Loc.Get("cash") + ": " + N(S.Cash) + " JOD | " + Loc.Get("feed") + ": " + N(farm.StockOf(Farm.Feed), "F0") + " " + Loc.Get("kg") + " | " + Loc.Get("water") + ": " + N(farm.StockOf(Farm.Water), "F0") + " " + Loc.Get("l");
            string line3 = Loc.Get("animals") + ": " + alive + " | " + Loc.Get("eggs") + ": " + (S.FertileEggs + S.InfertileEggs) + (hungry > 0 ? "\n" + Loc.Get("alertDeficit") + " (" + hungry + ")" : "");
            topText.text = Loc.Show(line1 + "\n" + line2 + "\n" + line3);
            var evs = S.Events.Where(e => e.Type != "purchase").Reverse().Take(4).Select(e =>
            { string n = e.Type == "birth" ? Num(e.Detail, "actual") : e.Type == "hatch" ? Num(e.Detail, "hatched") : ""; return Loc.Get("ev_" + e.Type) + " " + n + " | " + Loc.Get("day") + " " + (e.Day + 1); }).ToList();
            logText.text = Loc.Show(evs.Count == 0 ? Loc.Get("noEvents") + "\n" + Loc.Get("disclaimer") : string.Join("\n", evs));
            foreach (var kv in labels) kv.Key.text = Loc.Show(kv.Value());
        }
    }
}
