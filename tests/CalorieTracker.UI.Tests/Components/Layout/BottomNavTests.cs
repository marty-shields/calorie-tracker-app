using CalorieTracker.UI.Components.Layout;
using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Components.Layout;

public class BottomNavTests : ComponentTestBase
{
    [Test]
    public void Items_AreTheDestinationsWithIconAndLabel()
    {
        var cut = Context.Render<BottomNav>(p => p.Add(x => x.Current, NavDestination.Dashboard));

        var items = cut.FindAll("nav button").Select(b => (b.GetAttribute("data-testid"), b.TextContent.Trim(), b.QuerySelectorAll("svg").Length));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("nav").GetAttribute("aria-label"), Is.EqualTo("Main"));
            Assert.That(items, Is.EqualTo(new[] { ("dashboard-link", "Dashboard", 1), ("foods-link", "Foods", 1) }));
        });
    }

    [TestCase(NavDestination.Dashboard, "dashboard-link")]
    [TestCase(NavDestination.Foods, "foods-link")]
    public void CurrentItem_IsTheOnlyCurrentPage(NavDestination current, string testId)
    {
        var cut = Context.Render<BottomNav>(p => p.Add(x => x.Current, current));

        var marked = cut.FindAll("[aria-current]").Select(b => (b.GetAttribute("data-testid"), b.GetAttribute("aria-current"), b.ClassList.Contains("current")));

        Assert.That(marked, Is.EqualTo(new[] { (testId, "page", true) }));
    }

    [Test]
    public void TappingAnotherItem_RaisesItsDestination()
    {
        var navigated = new List<NavDestination>();
        var cut = Context.Render<BottomNav>(p => p
            .Add(x => x.Current, NavDestination.Dashboard)
            .Add(x => x.OnNavigate, navigated.Add));

        cut.Find("[data-testid=foods-link]").Click();

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Foods }));
    }

    [Test]
    public void TappingTheCurrentItem_DoesNothing()
    {
        var navigated = new List<NavDestination>();
        var cut = Context.Render<BottomNav>(p => p
            .Add(x => x.Current, NavDestination.Dashboard)
            .Add(x => x.OnNavigate, navigated.Add));

        cut.Find("[data-testid=dashboard-link]").Click();

        Assert.That(navigated, Is.Empty);
    }
}
