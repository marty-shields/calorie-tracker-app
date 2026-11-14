using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class ScanViewTests : ComponentTestBase
{
    private static readonly ScannedProduct Consistent = new("Oats", "Mill", 372, 11, 60, 8);

    [TestCase(372, false)]
    [TestCase(300, false)] // 4*11 + 4*60 + 9*8 = 356; 20% = 71.2, so 300 is within.
    [TestCase(250, true)]
    [TestCase(450, true)]
    public void KcalMismatch_ComparesAgainstMacroEnergy(int kcal, bool expected) =>
        Assert.That((Consistent with { Kcal = kcal }).KcalMismatch, Is.EqualTo(expected));

    [Test]
    public void KcalMismatch_NoMacros_DoesNotWarn() =>
        Assert.That(new ScannedProduct("Water", null, 5, 0, 0, 0).KcalMismatch, Is.False);

    [Test]
    public void NotFound_ShowsOneMessageAndScanAgain()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.NotFound));

        Assert.That(cut.Find("[data-testid=scan-not-found]").TextContent, Is.EqualTo("Unable to find this product"));
        Assert.That(cut.FindAll("[data-testid=scan-again]"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Found_WithConsistentValues_ShowsNoWarning()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent));

        Assert.That(cut.FindAll("[data-testid=scan-warning]"), Is.Empty);
        Assert.That(cut.Find("[data-testid=scan-per]").TextContent, Is.EqualTo("Per 100 g"));
    }

    [Test]
    public void Found_WithMismatch_ShowsWarning()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent with { Kcal = 100 }));

        Assert.That(cut.FindAll("[data-testid=scan-warning]"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Found_Liquid_ShowsPer100Ml()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent with { IsLiquid = true }));

        Assert.That(cut.Find("[data-testid=scan-per]").TextContent, Is.EqualTo("Per 100 ml"));
    }
}
