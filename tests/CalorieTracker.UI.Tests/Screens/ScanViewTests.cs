using CalorieTracker.Core.Data;
using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class ScanViewTests : ComponentTestBase
{
    private IRenderedComponent<ScanView> Render(ScanState state, Action<ComponentParameterCollectionBuilder<ScanView>>? more = null) =>
        Context.Render<ScanView>(p =>
        {
            p.Add(x => x.State, state).Add(x => x.Meal, Meal.Dinner).Add(x => x.Date, new DateOnly(2026, 11, 18));
            more?.Invoke(p);
        });

    [TestCase(ScanState.OpeningCamera)]
    [TestCase(ScanState.Closing)]
    [TestCase(ScanState.LookingUp)]
    [TestCase(ScanState.NotFound)]
    [TestCase(ScanState.CameraUnavailable)]
    public void EveryState_ShowsTheMealAndDayChip(ScanState state) =>
        Assert.That(Render(state).Find("[data-testid=scan-adding]").TextContent, Is.EqualTo("Adding to Dinner · 18 Nov"));

    [Test]
    public void TheChip_NamesTheMealAndTheDayItWasGiven()
    {
        var cut = Context.Render<ScanView>(p => p.Add(x => x.State, ScanState.OpeningCamera).Add(x => x.Meal, Meal.Snacks).Add(x => x.Date, new DateOnly(2026, 11, 2)));

        Assert.That(cut.Find("[data-testid=scan-adding]").TextContent, Is.EqualTo("Adding to Snacks · 2 Nov"));
    }

    [Test]
    public void NotFound_ShowsOneMessageSearchByNameInsteadAndScanAgain()
    {
        var cut = Render(ScanState.NotFound);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-not-found]").TextContent, Does.StartWith("Unable to find this product"));
            Assert.That(cut.Find("[data-testid=scan-not-found-message]").TextContent, Is.EqualTo("Unable to find this product"));
            Assert.That(cut.Find("[data-testid=scan-not-found-hint]").TextContent, Is.EqualTo("Try another barcode, or search by name."));
            Assert.That(cut.Find("[data-testid=scan-search]").TextContent, Is.EqualTo("Search by name instead"));
            Assert.That(cut.Find("[data-testid=scan-again]").TextContent, Is.EqualTo("Scan again"));
            Assert.That(cut.FindAll("[data-testid=scan-add]"), Is.Empty);
        });
    }

    [Test]
    public void NotFound_SearchByName_RaisesOnSearchByName()
    {
        var searches = 0;
        var cut = Render(ScanState.NotFound, p => p.Add(x => x.OnSearchByName, () => searches++));

        cut.Find("[data-testid=scan-search]").Click();

        Assert.That(searches, Is.EqualTo(1));
    }

    [Test]
    public void NotFound_ScanAgain_RaisesOnScanAgain()
    {
        var again = 0;
        var cut = Render(ScanState.NotFound, p => p.Add(x => x.OnScanAgain, () => again++));

        cut.Find("[data-testid=scan-again]").Click();

        Assert.That(again, Is.EqualTo(1));
    }

    [Test]
    public void CameraUnavailable_ShowsTryAgainAndSearchByNameButNoScanAgain()
    {
        var tries = 0;
        var searches = 0;
        var cut = Render(ScanState.CameraUnavailable, p => p.Add(x => x.OnScanAgain, () => tries++).Add(x => x.OnSearchByName, () => searches++));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-camera-unavailable-error]").TextContent, Is.EqualTo("Couldn't open the camera"));
            Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-again]"), Is.Empty);
        });

        cut.Find("[data-testid=scan-camera-unavailable-retry]").Click();
        cut.Find("[data-testid=scan-search]").Click();

        Assert.That((tries, searches), Is.EqualTo((1, 1)));
    }

    [Test]
    public void OpeningCamera_ShowsALabelledSpinnerAndNoViewfinder()
    {
        var cut = Render(ScanState.OpeningCamera);

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
        var cut = Render(ScanState.Closing);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-closing-camera]").TextContent, Does.Contain("Closing camera"));
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Is.Empty);
            Assert.That(cut.Markup, Does.Not.Contain("Opening camera"));
        });
    }

    [TestCase(ScanState.OpeningCamera)]
    [TestCase(ScanState.LookingUp)]
    [TestCase(ScanState.NotFound)]
    [TestCase(ScanState.CameraUnavailable)]
    public void NoState_HasABottomBarOrNavigationBar(ScanState state)
    {
        var cut = Render(state);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=bottom-bar]"), Is.Empty);
            Assert.That(cut.FindAll("footer"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=bottom-nav]"), Is.Empty);
        });
    }

    [Test]
    public void HasABackArrow()
    {
        var back = 0;
        var cut = Render(ScanState.OpeningCamera, p => p.Add(x => x.OnBack, () => back++));

        cut.Find("[data-testid=back]").Click();

        Assert.That(back, Is.EqualTo(1));
    }
}
