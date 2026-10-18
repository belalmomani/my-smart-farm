using System;
using System.Collections;
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

    /// <summary>Lifecycle, time, save/load, actions and navigation. Screens live in Screens.cs (partial).</summary>
    public partial class GameController : MonoBehaviour
    {
        public const string Version = "0.4.0";
        sealed class LabelBind { public Text T; public Func<string> F; public string Screen; }

        SaveData data; Farm farm; SaveManager saves; FarmView view; Canvas canvas; bool started;
        string tab = "farm", building = "*"; float toastT, uiT, autosaveT;
        Text hudText, logText, toast; GameObject hudGo, navGo, farmOverlay;
        readonly List<LabelBind> labels = new List<LabelBind>();
        readonly Dictionary<string, GameObject> screens = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Image> tabImages = new Dictionary<string, Image>();
        bool confirmNewFarm;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindFirstObjectByType<GameController>() == null) new GameObject("Game").AddComponent<GameController>(); }

        static string N(double v, string f = "F2") => v.ToString(f, CultureInfo.InvariantCulture);
        string Money(double v) => N(v) + " " + farm.S.Currency;
        int Day => data.Clock.WholeDay;

        void Awake()
        {
            Application.targetFrameRate = 30;
            saves = new SaveManager(Path.Combine(Application.persistentDataPath, "farm.json"), new JsonSer());
            try { data = saves.Load(); } catch (Exception e) { Debug.LogWarning("Save not loadable: " + e.Message); }
            bool hasSave = data != null;
            if (!hasSave) { data = new SaveData(); data.Farm.Seed = Environment.TickCount; data.Clock.LastUpdateUtcTicks = DateTime.UtcNow.Ticks; }
            else data.Clock.ApplyOffline(DateTime.UtcNow);
            Loc.Lang = data.Language ?? "ar";
            farm = new Farm(data.Farm); if (hasSave) farm.ProcessUntil(data.Clock.WholeDay);
            view = gameObject.AddComponent<FarmView>(); view.Init(farm, data.Clock); ApplyQuality();
            started = hasSave && !string.IsNullOrEmpty(data.Farm.FarmType);
            BuildUi(); Show(started ? "farm" : "start");
        }

        void Update()
        {
            if (toastT > 0) { toastT -= Time.unscaledDeltaTime; if (toastT <= 0) toast.text = ""; }
            if (!started) return;
            data.Clock.Advance(Mathf.Min(Time.unscaledDeltaTime, 0.5f), DateTime.UtcNow);
            farm.ProcessUntil(data.Clock.WholeDay);
            uiT -= Time.unscaledDeltaTime; if (uiT <= 0) { uiT = 0.25f; Refresh(); }
            autosaveT += Time.unscaledDeltaTime; if (autosaveT > 60) { autosaveT = 0; Save(false); }
        }

        void OnApplicationPause(bool paused)
        {
            if (data == null || !started) return;
            if (paused) Save(false); else { data.Clock.ApplyOffline(DateTime.UtcNow); farm.ProcessUntil(data.Clock.WholeDay); }
        }
        void OnApplicationQuit() { if (data != null && started) Save(false); }

        void Save(bool notify)
        {
            try { data.Language = Loc.Lang; saves.Save(data); if (notify) Toast(Loc.Get("saved")); }
            catch (Exception e) { Debug.LogError("Save failed: " + e.Message); if (notify) Toast(Loc.Get("fail")); }
        }
        void Toast(string s) { toast.text = Loc.Show(s); toastT = 2f; }
        void ApplyQuality() { view.SetShadows(data.Quality); QualitySettings.antiAliasing = data.Quality == 2 ? 2 : 0; }

        // ---------------- actions ----------------
        void StartFarm(string typeId)
        {
            var def = FarmTypes.Get(typeId); if (def == null) return;
            string lang = Loc.Lang; int q = data.Quality, sc = data.ScenarioIndex;
            data = new SaveData { Language = lang, Quality = q, ScenarioIndex = sc };
            data.Farm.Seed = Environment.TickCount; data.Farm.FarmType = typeId; data.Farm.Cash = def.StartCash; data.Clock.LastUpdateUtcTicks = DateTime.UtcNow.Ticks;
            farm = new Farm(data.Farm); farm.GrantStock(Farm.Feed, 100); farm.GrantStock(Farm.Water, 400);   // example starter supplies
            view.Bind(farm, data.Clock); started = true; confirmNewFarm = false; Save(false); RebuildUi(); Show("farm");
        }
        void BuyFeed() => Toast(farm.BuyStock(Farm.Feed, 50, farm.Price("feed_kg"), Day, TxCategory.FeedPurchase) ? Loc.Get("ok") : Loc.Get("noMoney"));
        void BuyWater() => Toast(farm.BuyStock(Farm.Water, 200, farm.Price("water_l"), Day, TxCategory.WaterPurchase) ? Loc.Get("ok") : Loc.Get("noMoney"));
        void BuyPair(string id)
        {
            var sp = SpeciesCatalog.All[id];
            if (farm.S.Cash < 2 * farm.Price("buy:" + id)) { Toast(Loc.Get("noMoney")); return; }
            farm.BuyAnimal(id, Sex.Male, sp.MaturityDays + 30, Day); farm.BuyAnimal(id, Sex.Female, sp.MaturityDays + 30, Day); Toast(Loc.Get("ok"));
        }
        void SellAdult(string id)
        {
            int day = Day; var a = farm.S.Animals.Where(x => x.Alive && x.SpeciesId == id && farm.IsAdult(x, day)).OrderBy(x => x.BirthDay).FirstOrDefault();
            Toast(a != null && farm.SellAnimal(a.Id, day) ? Loc.Get("ok") : Loc.Get("fail"));
        }
        void Breed(string id) { string k; farm.TryBreedAny(id, Day, out k); Toast(Loc.Get(k)); }
        void SellEggs() { int n = farm.S.InfertileEggs; Toast(n > 0 && farm.SellEggs(n, false, farm.Price("egg"), Day) ? Loc.Get("ok") : Loc.Get("fail")); }
        void Incubate() { Toast(farm.SetIncubation("chicken", farm.S.FertileEggs, Day) > 0 ? Loc.Get("ok") : Loc.Get("fail")); }
        void ToggleLang() { Loc.Lang = Loc.IsArabic ? "en" : "ar"; data.Language = Loc.Lang; RebuildUi(); Show(tab); }
        void CopyCsv(bool ledger)
        {
            string csv = ledger ? Csv.Ledger(farm.S) : Csv.Events(farm.S); GUIUtility.systemCopyBuffer = csv;
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, ledger ? "ledger.csv" : "events.csv"), csv); } catch (Exception e) { Debug.LogWarning(e.Message); }
            Toast(Loc.Get("copied"));
        }

        // ---------------- UI shell ----------------
        Font GetFont()
        {
            try { var f = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans Arabic", "Noto Naskh Arabic", "Droid Arabic Naskh", "Tahoma", "Arial" }, 40); if (f != null) return f; } catch { }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        TextAnchor Start1 => Loc.IsArabic ? TextAnchor.UpperRight : TextAnchor.UpperLeft;

        Text AddLabel(Transform parent, Func<string> f, int size = 30, Color? c = null)
        { var t = Ui.MakeText(parent, size, Start1, c); labels.Add(new LabelBind { T = t, F = f, Screen = building }); return t; }

        Button AddBtn(Transform parent, Color col, Func<string> f, Action a, int size = 32, float flex = 1, float minW = 0)
        {
            Text lt; var b = Ui.MakeButton(parent, col, out lt, size); b.onClick.AddListener(() => a());
            labels.Add(new LabelBind { T = lt, F = f, Screen = building }); Ui.Flex(b.gameObject, flex, minW); return b;
        }

        void RebuildUi() { if (canvas != null) Destroy(canvas.gameObject); BuildUi(); }

        void BuildUi()
        {
            if (Ui.Font == null) Ui.Font = GetFont();
            labels.Clear(); screens.Clear(); tabImages.Clear();
            var cg = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1920); sc.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var root = cg.transform;

            // screens first (so HUD/nav draw above them)
            building = "farm"; BuildFarmOverlay(root);
            building = "market"; screens["market"] = BuildMarket(root);
            building = "budget"; screens["budget"] = BuildBudget(root);
            building = "reports"; screens["reports"] = BuildReports(root);
            building = "more"; screens["more"] = BuildMore(root);

            // HUD
            var hud = Ui.MakePanel(root, "HUD", Ui.Panel, 0, 0.90f, 1, 1); hudGo = hud.gameObject;
            hudText = Ui.MakeTextAnchored(hud, 0.02f, 0, 0.98f, 1, 32, Start1);
            toast = Ui.MakeTextAnchored(root, 0, 0.30f, 1, 0.37f, 42, TextAnchor.MiddleCenter, new Color(1, .9f, .4f));

            // bottom navigation
            var nav = Ui.MakePanel(root, "Nav", new Color(0, 0, 0, .8f), 0, 0, 1, 0.08f); navGo = nav.gameObject;
            string[] keys = { "farm", "market", "budget", "reports", "more" };
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i]; Text lt; var b = Ui.MakeButton(nav, Ui.Gray, out lt, 30); Ui.Anchored(b.gameObject, i / 5f, 0, (i + 1) / 5f, 1);
                ((RectTransform)b.transform).offsetMin = new Vector2(4, 4); ((RectTransform)b.transform).offsetMax = new Vector2(-4, -4);
                b.onClick.AddListener(() => Show(k)); labels.Add(new LabelBind { T = lt, F = () => Loc.Get("tab_" + k), Screen = "*" }); tabImages[k] = b.GetComponent<Image>();
            }

            building = "start"; screens["start"] = BuildStart(root);
            Refresh(true);
        }

        void Show(string name)
        {
            tab = name; confirmNewFarm = false;
            foreach (var kv in screens) kv.Value.SetActive(kv.Key == name);
            farmOverlay.SetActive(name == "farm"); hudGo.SetActive(name != "start"); navGo.SetActive(name != "start");
            Refresh(true);
        }

        static string Num(string detail, string key) { int v = Reports.Num(detail, key); return v > 0 ? v.ToString() : ""; }
        string EventLine(FarmEvent e)
        {
            string n = e.Type == "birth" ? Num(e.Detail, "actual") : e.Type == "hatch" ? Num(e.Detail, "hatched") : "";
            return Loc.Get("ev_" + e.Type) + " " + n + " | " + Loc.Get("day") + " " + (e.Day + 1);
        }

        void Refresh(bool force = false)
        {
            if (farm == null || hudText == null) return;
            var S = farm.S; int day = Day;
            if (started)
            {
                hudText.text = Loc.Show(Loc.Get("day") + " " + (day + 1) + " | " + Loc.Get("year") + " " + Seasons.Year(day) + " | " + Loc.Get(Seasons.Of(day).ToString()) + (data.Clock.Paused ? " | " + Loc.Get("pause") : "") + "\n" +
                    Loc.Get("cash") + ": " + Money(S.Cash) + "\n" +
                    Loc.Get("feed") + ": " + N(farm.StockOf(Farm.Feed), "F0") + " " + Loc.Get("kg") + " | " + Loc.Get("water") + ": " + N(farm.StockOf(Farm.Water), "F0") + " " + Loc.Get("l") + " | " + Loc.Get("animals") + ": " + S.Animals.Count(a => a.Alive) + " | " + Loc.Get("eggs") + ": " + (S.FertileEggs + S.InfertileEggs));
                if (tab == "farm" && logText != null)
                {
                    var lines = farm.Alerts(day).Select(a => Loc.Get(a.Key) + " " + a.Count).ToList();
                    lines.AddRange(S.Events.Where(e => e.Type != "purchase" && e.Type != "alert_deficit").Reverse().Take(3).Select(EventLine));
                    logText.text = Loc.Show(lines.Count == 0 ? Loc.Get("noEvents") + "\n" + Loc.Get("disclaimer") : string.Join("\n", lines.Take(5)));
                }
            }
            foreach (var l in labels) if (l.Screen == "*" || l.Screen == tab) l.T.text = Loc.Show(l.F());
            foreach (var kv in tabImages) kv.Value.color = kv.Key == tab ? Ui.Green : Ui.Gray;
            if (tab == "budget") UpdateBudgetChart(); else if (tab == "reports") UpdateReportsChart();
        }
    }
}
