using System.Collections.Generic;
using UnityEngine;
using MySmartFarm.Core;

namespace MySmartFarm.Game
{
    /// <summary>Builds the placeholder 3D farm from primitives and mirrors Farm state (animals, eggs, season, daylight). No pathfinding/collision avoidance yet.</summary>
    public class FarmView : MonoBehaviour
    {
        public const float Half = 14f;
        public Transform Feeder, Drinker, Nest;
        public bool FeedAvailable, WaterAvailable;
        Farm farm; SimClock clock; Light sun; Renderer ground; readonly Dictionary<string, AnimalAgent> agents = new Dictionary<string, AnimalAgent>();
        readonly List<GameObject> eggs = new List<GameObject>(); float syncT;

        public Vector3 RandomPoint() => new Vector3(Random.Range(-Half + 2, Half - 2), 0, Random.Range(-Half + 2, Half - 5));

        GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Color c)
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(transform, false); g.transform.position = pos; g.transform.localScale = scale; g.GetComponent<Renderer>().material.color = c; return g;
        }

        public void Bind(Farm f, SimClock c)
        {
            farm = f; clock = c;
            foreach (var kv in agents) if (kv.Value != null) Destroy(kv.Value.gameObject);
            agents.Clear(); if (sun != null) Sync();
        }
        public void SetShadows(int quality)
        {
            if (sun == null) return;
            sun.shadows = quality == 0 ? LightShadows.None : quality == 1 ? LightShadows.Hard : LightShadows.Soft;
            QualitySettings.shadowDistance = quality == 0 ? 0 : quality == 1 ? 25 : 45;
        }
        public void SetVisible(bool v) { foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v; }

        public void Init(Farm f, SimClock c)
        {
            farm = f; clock = c;
            var cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .75f, .95f); cam.gameObject.AddComponent<CameraRig>();
            var lg = new GameObject("Sun"); sun = lg.AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Hard; QualitySettings.shadowDistance = 45;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane); Destroy(g.GetComponent<Collider>()); g.name = "Ground"; g.transform.SetParent(transform, false); g.transform.localScale = new Vector3(5, 1, 5); ground = g.GetComponent<Renderer>();
            var wood = new Color(.45f, .3f, .15f);
            Prim(PrimitiveType.Cube, "FenceN", new Vector3(0, .6f, Half), new Vector3(Half * 2, .15f, .15f), wood); Prim(PrimitiveType.Cube, "FenceS", new Vector3(0, .6f, -Half), new Vector3(Half * 2, .15f, .15f), wood);
            Prim(PrimitiveType.Cube, "FenceE", new Vector3(Half, .6f, 0), new Vector3(.15f, .15f, Half * 2), wood); Prim(PrimitiveType.Cube, "FenceW", new Vector3(-Half, .6f, 0), new Vector3(.15f, .15f, Half * 2), wood);
            for (float p = -Half; p <= Half; p += 4f) { Prim(PrimitiveType.Cube, "Post", new Vector3(p, .5f, Half), new Vector3(.25f, 1f, .25f), wood); Prim(PrimitiveType.Cube, "Post", new Vector3(p, .5f, -Half), new Vector3(.25f, 1f, .25f), wood);
                Prim(PrimitiveType.Cube, "Post", new Vector3(Half, .5f, p), new Vector3(.25f, 1f, .25f), wood); Prim(PrimitiveType.Cube, "Post", new Vector3(-Half, .5f, p), new Vector3(.25f, 1f, .25f), wood); }
            Prim(PrimitiveType.Cube, "Barn", new Vector3(0, 2, Half - 3), new Vector3(8, 4, 4), new Color(.6f, .25f, .2f));
            Feeder = Prim(PrimitiveType.Cube, "Feeder", new Vector3(-6, .3f, 3), new Vector3(2.5f, .6f, 1f), new Color(.55f, .4f, .2f)).transform;
            Drinker = Prim(PrimitiveType.Cylinder, "Drinker", new Vector3(6, .15f, 3), new Vector3(2f, .15f, 2f), new Color(.2f, .5f, .9f)).transform;
            Nest = Prim(PrimitiveType.Cylinder, "Nest", new Vector3(0, .05f, -8), new Vector3(3.5f, .05f, 3.5f), new Color(.85f, .7f, .3f)).transform;
            Prim(PrimitiveType.Cube, "Storage", new Vector3(10, 1, -9), new Vector3(3, 2, 3), new Color(.5f, .5f, .55f));
            for (int i = 0; i < 30; i++) { var e = Prim(PrimitiveType.Sphere, "Egg", Nest.position + new Vector3(Mathf.Cos(i) * 1.2f * (1 + i % 3 * .3f), .25f, Mathf.Sin(i) * 1.2f * (1 + i % 3 * .3f)), new Vector3(.22f, .3f, .22f), Color.white); e.SetActive(false); eggs.Add(e); }
            Sync();
        }

        static Color SeasonColor(Season s) { switch (s) { case Season.Spring: return new Color(.35f, .65f, .3f); case Season.Summer: return new Color(.6f, .65f, .25f); case Season.Autumn: return new Color(.65f, .45f, .2f); default: return new Color(.85f, .9f, .95f); } }

        void Update()
        {
            float frac = (float)(clock.VirtualDay - System.Math.Floor(clock.VirtualDay));
            float inten = Mathf.Clamp01(Mathf.Sin((frac - 0.25f) * 2f * Mathf.PI));
            sun.transform.rotation = Quaternion.Euler(frac * 360f - 90f, 30f, 0); sun.intensity = inten * 1.1f;
            RenderSettings.ambientLight = Color.Lerp(new Color(.08f, .08f, .15f), new Color(.6f, .6f, .6f), inten);
            ground.material.color = SeasonColor(Seasons.Of(clock.WholeDay));
            syncT -= Time.deltaTime; if (syncT <= 0) { syncT = 0.5f; Sync(); }
        }

        void Sync()
        {
            int day = clock.WholeDay; FeedAvailable = farm.StockOf(Farm.Feed) > 0; WaterAvailable = farm.StockOf(Farm.Water) > 0;
            foreach (var a in farm.S.Animals)
            {
                if (!a.Alive) continue;
                AnimalAgent ag;
                if (!agents.TryGetValue(a.Id, out ag)) { var go = new GameObject("Animal_" + a.SpeciesId); go.transform.SetParent(transform, false); ag = go.AddComponent<AnimalAgent>(); ag.Init(a, this); agents[a.Id] = ag; }
                ag.UpdateScale(day);
            }
            var gone = new List<string>();
            foreach (var kv in agents) if (!kv.Value.Data.Alive) gone.Add(kv.Key);
            foreach (var id in gone) { Destroy(agents[id].gameObject); agents.Remove(id); }
            int n = Mathf.Min(eggs.Count, farm.S.FertileEggs + farm.S.InfertileEggs);
            for (int i = 0; i < eggs.Count; i++) eggs[i].SetActive(i < n);
        }
    }
}
