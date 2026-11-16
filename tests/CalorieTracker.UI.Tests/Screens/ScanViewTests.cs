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

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-not-found]").TextContent, Does.StartWith("Unable to find this product"));
            Assert.That(cut.Find("[data-testid=scan-not-found-message]").TextContent, Is.EqualTo("Unable to find this product"));
            Assert.That(cut.Find("[data-testid=scan-not-found-hint]").TextContent, Is.EqualTo("Try another barcode, or search by name."));
            Assert.That(cut.FindAll("[data-testid=scan-again]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-search]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-add]"), Is.Empty);
        });
    }

    [Test]
    public void NotFound_SearchByName_RaisesOnSearchByName()
    {
        var searches = 0;
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.NotFound).Add(x => x.OnSearchByName, () => searches++));

        Assert.That(cut.Find("[data-testid=scan-search]").TextContent, Is.EqualTo("Search by name"));
        cut.Find("[data-testid=scan-search]").Click();

        Assert.That(searches, Is.EqualTo(1));
    }

    [Test]
    public void Found_WithAFoodId_OffersAddToDiaryAboveScanAgain()
    {
        var adds = 0;
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent with { FoodId = 5 }).Add(x => x.OnAddToDiary, () => adds++));

        Assert.That(cut.FindAll("[data-testid=scan-add], [data-testid=scan-again]").Select(e => e.GetAttribute("data-testid")), Is.EqualTo(new[] { "scan-add", "scan-again" }));
        Assert.That(cut.Find("[data-testid=scan-add]").TextContent, Is.EqualTo("Add to diary"));
        Assert.That(cut.FindAll("[data-testid=scan-search]"), Is.Empty);
        cut.Find("[data-testid=scan-add]").Click();

        Assert.That(adds, Is.EqualTo(1));
    }

    [Test]
    public void Found_WithoutAFoodId_OffersSearchByNameInsteadOfAdd()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent));

        Assert.That(cut.FindAll("[data-testid=scan-add]"), Is.Empty);
        Assert.That(cut.FindAll("[data-testid=scan-search], [data-testid=scan-again]").Select(e => e.GetAttribute("data-testid")), Is.EqualTo(new[] { "scan-search", "scan-again" }));
    }

    [Test]
    public void Found_WithNoProduct_OffersSearchByNameAndNoAdd()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found));

        Assert.That(cut.FindAll("[data-testid=scan-add]"), Is.Empty);
        Assert.That(cut.FindAll("[data-testid=scan-search]"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("[data-testid=scan-again]"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Found_WithAServing_ShowsItInGramsOrMl()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent with { ServingLabel = "1 biscuit", ServingAmount = 15 }));
        Assert.That(cut.Find("[data-testid=scan-serving]").TextContent, Is.EqualTo("Serving: 1 biscuit (15 g)"));

        cut.Render(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent with { IsLiquid = true, ServingLabel = "1 glass", ServingAmount = 200 }));
        Assert.That(cut.Find("[data-testid=scan-serving]").TextContent, Is.EqualTo("Serving: 1 glass (200 ml)"));
    }

    [Test]
    public void Found_WithoutAServing_ShowsNoServingLine()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent));

        Assert.That(cut.FindAll("[data-testid=scan-serving]"), Is.Empty);
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

    [Test]
    public void CameraUnavailable_ShowsLoadFailedWithTryAgainAndNoScanAgain()
    {
        var tries = 0;
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.CameraUnavailable).Add(x => x.OnScanAgain, () => tries++));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-camera-unavailable-error]").TextContent, Is.EqualTo("Couldn't open the camera"));
            Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-again]"), Is.Empty);
        });

        cut.Find("[data-testid=scan-camera-unavailable-retry]").Click();

        Assert.That(tries, Is.EqualTo(1));
    }

    [Test]
    public void Found_WithExtraNutrients_ShowsTheirRows()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found)
            .Add(x => x.Product, Consistent with { Fibre = 7, Sugars = 1.1m, Saturates = 1.4m, Salt = 0.3m }));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-fibre]").TextContent, Is.EqualTo("7 g"));
            Assert.That(cut.Find("[data-testid=scan-sugars]").TextContent, Is.EqualTo("1.1 g"));
            Assert.That(cut.Find("[data-testid=scan-saturates]").TextContent, Is.EqualTo("1.4 g"));
            Assert.That(cut.Find("[data-testid=scan-salt]").TextContent, Is.EqualTo("0.3 g"));
        });
    }

    [Test]
    public void Found_WithoutExtraNutrients_ShowsOnlyTheFourRows()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Found).Add(x => x.Product, Consistent));

        Assert.That(cut.FindAll("dt").Select(e => e.TextContent), Is.EqualTo(new[] { "Energy", "Protein", "Carbs", "Fat" }));
    }

    [Test]
    public void OpeningCamera_ShowsALabelledSpinnerAndNoViewfinder()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.OpeningCamera));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-opening-camera]").TextContent, Does.Contain("Opening camera"));
            Assert.That(cut.FindAll("[data-testid=scan-camera]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-hint]"), Is.Empty);
        });
    }

    [Test]
    public void Closing_ShowsANeutralSpinnerLabelNotOpeningCamera()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.Closing));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-closing-camera]").TextContent, Does.Contain("Closing camera"));
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Is.Empty);
            Assert.That(cut.Markup, Does.Not.Contain("Opening camera"));
        });
    }

    [Test]
    public void IsATopLevelScreen_WithScanCurrentInTheNavigationBar()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.OpeningCamera));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-link]").GetAttribute("aria-current"), Is.EqualTo("page"));
            Assert.That(cut.FindAll("[data-testid=back]"), Is.Empty);
        });
    }
}
