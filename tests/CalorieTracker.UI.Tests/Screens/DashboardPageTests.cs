using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class DashboardPageTests : ComponentTestBase
{
    [Test]
    public void ShowsTheEmptyDashboard()
    {
        var cut = Context.Render<DashboardPage>();

        Assert.That(cut.FindAll("[data-testid=dashboard-empty]"), Has.Count.EqualTo(1));
    }

    [Test]
    public void Buttons_RaiseTheirCallbacks()
    {
        var navigated = new List<NavDestination>();
        var settings = 0;
        var cut = Context.Render<DashboardPage>(p => p
            .Add(x => x.OnNavigate, navigated.Add)
            .Add(x => x.OnSettings, () => settings++));

        cut.Find("[data-testid=dashboard-link]").Click();
        cut.Find("[data-testid=settings]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(navigated, Is.Empty);
            Assert.That(settings, Is.EqualTo(1));
        });
    }

    [Test]
    public void ScanItem_RaisesNavigateToScan()
    {
        var navigated = new List<NavDestination>();
        var cut = Context.Render<DashboardPage>(p => p.Add(x => x.OnNavigate, navigated.Add));

        cut.Find("[data-testid=scan-link]").Click();

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Scan }));
    }

    [Test]
    public void AndroidBack_HasNoHandler()
    {
        Context.Render<DashboardPage>();

        Assert.That(BackButton.HasHandler, Is.False);
    }
}
