using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MySmartFarm.Game
{
    /// <summary>Tiny uGUI toolkit built in code (no prefabs). Text uses Overflow wrap so RTL lines are never re-wrapped; keep lines short.</summary>
    public static class Ui
    {
        public static Font Font;
        public static readonly Color Bg = new Color(.07f, .09f, .11f, .97f), Panel = new Color(0, 0, 0, .55f), Green = new Color(.15f, .45f, .25f, .95f), Blue = new Color(.15f, .35f, .6f, .95f), Red = new Color(.6f, .2f, .2f, .95f), Gray = new Color(.25f, .28f, .3f, .95f);

        public static RectTransform Anchored(GameObject go, float x0, float y0, float x1, float y1)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
        }

        public static RectTransform MakePanel(Transform parent, string name, Color c, float x0, float y0, float x1, float y1, bool block = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var im = go.GetComponent<Image>(); im.color = c; im.raycastTarget = block; return Anchored(go, x0, y0, x1, y1);
        }

        public static Text MakeText(Transform parent, int size, TextAnchor a, Color? color = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = Font; t.fontSize = size; t.alignment = a; t.color = color ?? Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
        }
        public static Text MakeTextAnchored(Transform parent, float x0, float y0, float x1, float y1, int size, TextAnchor a, Color? color = null)
        { var t = MakeText(parent, size, a, color); Anchored(t.gameObject, x0, y0, x1, y1); return t; }

        public static Button MakeButton(Transform parent, Color c, out Text label, int size = 34)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = c; label = MakeText(go.transform, size, TextAnchor.MiddleCenter); Anchored(label.gameObject, 0, 0, 1, 1); return go.GetComponent<Button>();
        }

        /// <summary>Vertical scroll area; returns the content rect (VerticalLayoutGroup + ContentSizeFitter).</summary>
        public static RectTransform MakeScroll(Transform parent, float x0, float y0, float x1, float y1)
        {
            var vp = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)); vp.transform.SetParent(parent, false);
            vp.GetComponent<Image>().color = new Color(0, 0, 0, .01f); Anchored(vp, x0, y0, x1, y1);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); content.transform.SetParent(vp.transform, false);
            var rt = (RectTransform)content.transform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var v = content.GetComponent<VerticalLayoutGroup>(); v.spacing = 10; v.padding = new RectOffset(16, 16, 16, 40); v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = vp.GetComponent<ScrollRect>(); sr.content = rt; sr.viewport = (RectTransform)vp.transform; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Elastic; sr.scrollSensitivity = 40;
            return rt;
        }

        public static RectTransform Row(Transform content, float height)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement)); go.transform.SetParent(content, false);
            var h = go.GetComponent<HorizontalLayoutGroup>(); h.spacing = 8; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            var le = go.GetComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height; return (RectTransform)go.transform;
        }
        public static void Flex(GameObject go, float w = 1, float minW = 0) { var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>(); le.flexibleWidth = w; le.minWidth = minW; }

        /// <summary>Simple bar chart. Series values may be negative. Optional second series is drawn beside the first.</summary>
        public class BarChart
        {
            readonly RectTransform area; readonly List<Image> bars = new List<Image>(); readonly Color c1, c2;
            public BarChart(Transform parent, float height, Color c1, Color c2)
            {
                var go = new GameObject("Chart", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); go.transform.SetParent(parent, false);
                go.GetComponent<Image>().color = new Color(1, 1, 1, .08f); var le = go.GetComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height; area = (RectTransform)go.transform; this.c1 = c1; this.c2 = c2;
            }
            Image Bar(int i) { while (bars.Count <= i) { var g = new GameObject("Bar", typeof(RectTransform), typeof(Image)); g.transform.SetParent(area, false); g.GetComponent<Image>().raycastTarget = false; bars.Add(g.GetComponent<Image>()); } return bars[i]; }
            public void Set(double[] a, double[] b = null)
            {
                int n = a.Length; double mn = 0, mx = 0.0001;
                foreach (var v in a) { mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
                if (b != null) foreach (var v in b) { mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
                double span = mx - mn; int used = 0;
                for (int i = 0; i < n; i++)
                {
                    float x0 = (float)i / n, x1 = (float)(i + 1) / n, gap = (x1 - x0) * 0.15f;
                    float mid = b == null ? x1 - gap : (x0 + x1) / 2;
                    Place(Bar(used++), x0 + gap, mid, a[i], mn, span, c1);
                    if (b != null) Place(Bar(used++), mid, x1 - gap, b[i], mn, span, c2);
                }
                for (int i = used; i < bars.Count; i++) bars[i].gameObject.SetActive(false);
            }
            static void Place(Image im, float x0, float x1, double v, double mn, double span, Color c)
            {
                im.gameObject.SetActive(true); im.color = c; var rt = im.rectTransform;
                float z = (float)((0 - mn) / span), y = (float)((v - mn) / span);
                rt.anchorMin = new Vector2(x0, Mathf.Min(z, y)); rt.anchorMax = new Vector2(x1, Mathf.Max(z, y) + 0.003f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }
    }
}
