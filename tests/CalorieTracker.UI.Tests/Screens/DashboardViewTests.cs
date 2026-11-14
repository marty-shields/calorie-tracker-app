using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class DashboardViewTests : ComponentTestBase
{
    [Test]
    public void Title_IsCalorieTracker()
    {
        var cut = Context.Render<DashboardView>();

        Assert.That(cut.Find("[data-testid=screen-title]").TextContent, Is.EqualTo("Calorie Tracker"));
    }

    [Test]
    public void Content_ShowsTheEmptyState()
    {
        var cut = Context.Render<DashboardView>();

        Assert.That(cut.Find("[data-testid=dashboard-empty-message]").TextContent, Is.EqualTo("Nothing here yet"));
    }

    [Test]
    public void NavigationBar_ShowsTheDashboardAsCurrentWithNoBackArrow()
    {
        var cut = Context.Render<DashboardView>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[aria-current=page]").GetAttribute("data-testid"), Is.EqualTo("dashboard-link"));
            Assert.That(cut.FindAll("[data-testid=back]"), Is.Empty);
        });
    }

    [Test]
    public void SettingsGear_RaisesOnSettings()
    {
        var settings = false;
        var cut = Context.Render<DashboardView>(p => p.Add(x => x.OnSettings, () => settings = true));

        cut.Find("[data-testid=settings]").Click();

        Assert.That(settings, Is.True);
    }
}
