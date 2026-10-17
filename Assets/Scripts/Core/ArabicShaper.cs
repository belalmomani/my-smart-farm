using System;
using System.Collections.Generic;
using System.Text;

namespace MySmartFarm.Core
{
    /// <summary>Minimal Arabic shaper for Unity legacy uGUI Text (which does not shape/reorder RTL).
    /// Converts letters to Presentation Forms-B and returns a VISUAL-order string (reversed, LTR runs kept).
    /// Limits: no diacritics (stripped), simplified bidi (Latin/digit runs only), no bracket mirroring, call per line (no auto-wrap).
    /// The font must contain U+FE70..U+FEFF glyphs (verify on a real device).</summary>
    public static class ArabicShaper
    {
        // base letter -> {isolated, final, initial, medial}; 0 = form does not exist (generated + verified against Unicode names)
        static readonly Dictionary<char, int[]> Forms = new Dictionary<char, int[]>
        {
            {'\u0621',new[]{0xFE80,0x0,0x0,0x0}},
            {'\u0622',new[]{0xFE81,0xFE82,0x0,0x0}},
            {'\u0623',new[]{0xFE83,0xFE84,0x0,0x0}},
            {'\u0624',new[]{0xFE85,0xFE86,0x0,0x0}},
            {'\u0625',new[]{0xFE87,0xFE88,0x0,0x0}},
            {'\u0626',new[]{0xFE89,0xFE8A,0xFE8B,0xFE8C}},
            {'\u0627',new[]{0xFE8D,0xFE8E,0x0,0x0}},
            {'\u0628',new[]{0xFE8F,0xFE90,0xFE91,0xFE92}},
            {'\u0629',new[]{0xFE93,0xFE94,0x0,0x0}},
            {'\u062A',new[]{0xFE95,0xFE96,0xFE97,0xFE98}},
            {'\u062B',new[]{0xFE99,0xFE9A,0xFE9B,0xFE9C}},
            {'\u062C',new[]{0xFE9D,0xFE9E,0xFE9F,0xFEA0}},
            {'\u062D',new[]{0xFEA1,0xFEA2,0xFEA3,0xFEA4}},
            {'\u062E',new[]{0xFEA5,0xFEA6,0xFEA7,0xFEA8}},
            {'\u062F',new[]{0xFEA9,0xFEAA,0x0,0x0}},
            {'\u0630',new[]{0xFEAB,0xFEAC,0x0,0x0}},
            {'\u0631',new[]{0xFEAD,0xFEAE,0x0,0x0}},
            {'\u0632',new[]{0xFEAF,0xFEB0,0x0,0x0}},
            {'\u0633',new[]{0xFEB1,0xFEB2,0xFEB3,0xFEB4}},
            {'\u0634',new[]{0xFEB5,0xFEB6,0xFEB7,0xFEB8}},
            {'\u0635',new[]{0xFEB9,0xFEBA,0xFEBB,0xFEBC}},
            {'\u0636',new[]{0xFEBD,0xFEBE,0xFEBF,0xFEC0}},
            {'\u0637',new[]{0xFEC1,0xFEC2,0xFEC3,0xFEC4}},
            {'\u0638',new[]{0xFEC5,0xFEC6,0xFEC7,0xFEC8}},
            {'\u0639',new[]{0xFEC9,0xFECA,0xFECB,0xFECC}},
            {'\u063A',new[]{0xFECD,0xFECE,0xFECF,0xFED0}},
            {'\u0641',new[]{0xFED1,0xFED2,0xFED3,0xFED4}},
            {'\u0642',new[]{0xFED5,0xFED6,0xFED7,0xFED8}},
            {'\u0643',new[]{0xFED9,0xFEDA,0xFEDB,0xFEDC}},
            {'\u0644',new[]{0xFEDD,0xFEDE,0xFEDF,0xFEE0}},
            {'\u0645',new[]{0xFEE1,0xFEE2,0xFEE3,0xFEE4}},
            {'\u0646',new[]{0xFEE5,0xFEE6,0xFEE7,0xFEE8}},
            {'\u0647',new[]{0xFEE9,0xFEEA,0xFEEB,0xFEEC}},
            {'\u0648',new[]{0xFEED,0xFEEE,0x0,0x0}},
            {'\u0649',new[]{0xFEEF,0xFEF0,0x0,0x0}},
            {'\u064A',new[]{0xFEF1,0xFEF2,0xFEF3,0xFEF4}}
        };

        static bool IsLetter(char c) => Forms.ContainsKey(c);
        static bool JoinsNext(char c) { int[] f; return Forms.TryGetValue(c, out f) && f[2] != 0; }
        static bool JoinsPrev(char c) { int[] f; return Forms.TryGetValue(c, out f) && f[1] != 0; }
        public static bool ContainsArabic(string s) { foreach (char c in s) if (c >= '\u0600' && c <= '\u06FF') return true; return false; }

        static int LamAlef(char alef, bool final)
        {
            switch (alef)
            {
                case '\u0627': return final ? 0xFEFC : 0xFEFB;
                case '\u0623': return final ? 0xFEF8 : 0xFEF7;
                case '\u0625': return final ? 0xFEFA : 0xFEF9;
                case '\u0622': return final ? 0xFEF6 : 0xFEF5;
                default: return 0;
            }
        }

        public static string Shape(string line)
        {
            var sb = new StringBuilder(line.Length);
            var s = new StringBuilder();
            foreach (char c in line) if (!(c >= '\u064B' && c <= '\u065F')) s.Append(c);   // strip diacritics
            string t = s.ToString();
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (!IsLetter(c)) { sb.Append(c); continue; }
                bool prevJ = i > 0 && JoinsNext(t[i - 1]);
                if (c == '\u0644' && i + 1 < t.Length && LamAlef(t[i + 1], false) != 0)
                { sb.Append((char)LamAlef(t[i + 1], prevJ)); i++; continue; }
                bool nextJ = i + 1 < t.Length && JoinsPrev(t[i + 1]);
                int[] f = Forms[c];
                bool cp = prevJ && f[1] != 0, cn = nextJ && f[2] != 0;
                int code = cp && cn ? f[3] : cp ? f[1] : cn ? f[2] : f[0];
                sb.Append((char)code);
            }
            return sb.ToString();
        }

        static bool IsLtr(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == ',' || c == '%' || c == '-' || c == '+' || c == '/';

        /// <summary>Shape + reorder one logical line for a left-to-right renderer.</summary>
        public static string ToVisual(string line)
        {
            if (string.IsNullOrEmpty(line) || !ContainsArabic(line)) return line;
            char[] a = Shape(line).ToCharArray(); Array.Reverse(a); int n = a.Length, i = 0;
            while (i < n)
            {
                if (!IsLtr(a[i])) { i++; continue; }
                int j = i;
                while (j < n && (IsLtr(a[j]) || (a[j] == ' ' && j > i && j + 1 < n && IsLtr(a[j + 1])))) j++;
                Array.Reverse(a, i, j - i); i = j;
            }
            return new string(a);
        }

        public static string ToVisualLines(string text)
        {
            var lines = text.Split('\n'); for (int k = 0; k < lines.Length; k++) lines[k] = ToVisual(lines[k]);
            return string.Join("\n", lines);
        }
    }
}
