using NUnit.Framework;
using MySmartFarm.Core;

namespace MySmartFarm.Tests
{
    public class TextTests
    {
        [Test] public void Shaper_Isolated_Initial_Final_Ligature()
        {
            Assert.AreEqual("\uFE8F", ArabicShaper.Shape("\u0628"));
            Assert.AreEqual("\uFE91\uFE90", ArabicShaper.Shape("\u0628\u0628"));
            Assert.AreEqual("\uFEFB", ArabicShaper.Shape("\u0644\u0627"));
            Assert.AreEqual("\uFE91\uFE8E", ArabicShaper.Shape("\u0628\u0627"));   // beh + alef: initial beh, final alef
        }
        [Test] public void Visual_ReversesArabic_KeepsNumbersAndLatin()
        {
            Assert.AreEqual("\uFE90\uFE91", ArabicShaper.ToVisual("\u0628\u0628"));
            Assert.AreEqual("1000.00 JOD", ArabicShaper.ToVisual("1000.00 JOD"));
            Assert.AreEqual("12 \uFE8D", ArabicShaper.ToVisual("\u0627 12"));
            Assert.AreEqual("1000.00 JOD \uFE8D", ArabicShaper.ToVisual("\u0627 1000.00 JOD"));
        }
        [Test] public void Loc_AllSpeciesHaveArabicNames()
        { Loc.Lang = "ar"; foreach (var k in SpeciesCatalog.All.Keys) Assert.IsNotEmpty(Loc.SpeciesName(k)); Loc.Lang = "ar"; }
    }
}
