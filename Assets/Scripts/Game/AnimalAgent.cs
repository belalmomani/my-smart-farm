using UnityEngine;
using MySmartFarm.Core;

namespace MySmartFarm.Game
{
    /// <summary>Visual-only behaviour of one animal (placeholder primitives). Simulation truth lives in Farm.</summary>
    public class AnimalAgent : MonoBehaviour
    {
        enum St { Idle, Walk, Eat, Drink, Rest }
        public Animal Data; SpeciesDef sp; FarmView view; Transform body; St st = St.Idle, arrive = St.Idle;
        Vector3 target; float timer, lastAte, lastDrank, baseSize;

        public void Init(Animal a, FarmView v)
        {
            Data = a; view = v; sp = SpeciesCatalog.All[a.SpeciesId]; baseSize = SizeOf(a.SpeciesId);
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(bodyGo.GetComponent<Collider>());
            bodyGo.transform.SetParent(transform, false); body = bodyGo.transform; body.localScale = new Vector3(0.7f, 0.6f, 1f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(transform, false); head.transform.localPosition = new Vector3(0, 0.25f, 0.6f); head.transform.localScale = Vector3.one * 0.4f;
            var c = ColorOf(a.SpeciesId) * (a.Sex == Sex.Male ? 0.8f : 1f); c.a = 1;
            bodyGo.GetComponent<Renderer>().material.color = c; head.GetComponent<Renderer>().material.color = c;
            transform.position = view.RandomPoint(); lastAte = Time.time - Random.value * 30; lastDrank = Time.time - Random.value * 30; timer = Random.value * 3;
            UpdateScale(0);
        }

        static float SizeOf(string id) { switch (id) { case "rabbit": return 0.6f; case "chicken": return 0.5f; case "duck": return 0.55f; case "sheep": return 1.1f; case "goat": return 1f; default: return 1.7f; } }
        static Color ColorOf(string id)
        {
            switch (id) { case "rabbit": return new Color(.75f, .75f, .75f); case "chicken": return new Color(.95f, .9f, .8f); case "duck": return new Color(.9f, .8f, .3f);
                case "sheep": return new Color(.95f, .95f, .95f); case "goat": return new Color(.55f, .4f, .25f); default: return new Color(.25f, .2f, .2f); }
        }
        /// <summary>Juveniles are smaller; grows linearly to adult size at maturity.</summary>
        public void UpdateScale(int day)
        {
            float t = Mathf.Clamp01((float)(day - Data.BirthDay) / Mathf.Max(1, sp.MaturityDays));
            transform.localScale = Vector3.one * baseSize * (0.4f + 0.6f * t);
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (st == St.Walk)
            {
                var d = target - transform.position; d.y = 0;
                if (d.magnitude < 0.2f) { st = arrive; timer = Random.Range(3f, 8f); if (st == St.Eat) lastAte = Time.time; if (st == St.Drink) lastDrank = Time.time; }
                else { transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 6f * Time.deltaTime); transform.position += d.normalized * 1.5f * Time.deltaTime; }
            }
            else if (timer <= 0) Choose();
            float bob = (st == St.Eat || st == St.Drink) ? Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.08f : 0;
            body.localPosition = new Vector3(0, 0.3f + bob, 0);
            body.localScale = new Vector3(0.7f, st == St.Rest ? 0.4f : 0.6f, 1f);
        }

        void Go(Vector3 p, St after) { target = p; arrive = after; st = St.Walk; }
        void Choose()
        {
            float r = Random.value;
            if (Time.time - lastAte > 40f && r < 0.6f) Go(view.Feeder.position + Random.insideUnitSphere * 1.5f, view.FeedAvailable ? St.Eat : St.Idle);
            else if (Time.time - lastDrank > 50f && r < 0.6f) Go(view.Drinker.position + Random.insideUnitSphere * 1.2f, view.WaterAvailable ? St.Drink : St.Idle);
            else if (sp.LaysEggs && Data.Sex == Sex.Female && r < 0.15f) Go(view.Nest.position + Random.insideUnitSphere * 1.2f, St.Rest);
            else if (r < 0.3f) { st = St.Rest; timer = Random.Range(4f, 9f); }
            else Go(view.RandomPoint(), St.Idle);
            target.y = 0;
        }
    }
}
