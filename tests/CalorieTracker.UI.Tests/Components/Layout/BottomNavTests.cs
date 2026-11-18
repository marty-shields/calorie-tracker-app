using CalorieTracker.UI.Components.Layout;
using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Components.Layout;

public class BottomNavTests : ComponentTestBase
{
    [Test]
    public void Bar_ShowsTodayAndAddOnly()
    {
        var cut = Context.Render<BottomNav>(p => p.Add(x => x.Current, NavDestination.Today));

        var buttons = cut.FindAll("nav button").Select(b => (b.GetAttribute("data-testid"), b.TextContent.Trim(), b.QuerySelectorAll("svg").Length));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("nav").GetAttribute("aria-label"), Is.EqualTo("Main"));
            Assert.That(buttons, Is.EqualTo(new[] { ("today-link", "Today", 1), ("add-button", "Add", 1) }));
            Assert.That(cut.FindAll("[data-testid=foods-link], [data-testid=scan-link]"), Is.Empty);
        });
    }

    [Test]
    public void Today_IsTheOnlyCurrentPage_AndAddNeverIs()
    {
        var cut = Context.Render<BottomNav>(p => p.Add(x => x.Current, NavDestination.Today));

        var marked = cut.FindAll("[aria-current]").Select(b => (b.GetAttribute("data-testid"), b.GetAttribute("aria-current"), b.ClassList.Contains("current")));

        Assert.Multiple(() =>
        {
            Assert.That(marked, Is.EqualTo(new[] { ("today-link", "page", true) }));
            Assert.That(cut.Find("[data-testid=add-button]").HasAttribute("aria-current"), Is.False);
        });
    }

    [Test]
    public void Add_IsBetweenTheSides_AndOpensADialog()
    {
        var cut = Context.Render<BottomNav>(p => p.Add(x => x.Current, NavDestination.Today));

        var add = cut.Find("[data-testid=add-button]");

        Assert.Multiple(() =>
        {
            Assert.That(add.GetAttribute("aria-haspopup"), Is.EqualTo("dialog"));
            Assert.That(add.HasAttribute("data-sheet-opener"), Is.True);
            Assert.That(add.PreviousElementSibling!.QuerySelector("[data-testid=today-link]"), Is.Not.Null);
        });
    }

    [Test]
    public void TappingAdd_RaisesOnAddAndNavigatesNowhere()
    {
        var navigated = new List<NavDestination>();
        var added = 0;
        var cut = Context.Render<BottomNav>(p => p
            .Add(x => x.Current, NavDestination.Today)
            .Add(x => x.OnNavigate, navigated.Add)
            .Add(x => x.OnAdd, () => added++));

        cut.Find("[data-testid=add-button]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.EqualTo(1));
            Assert.That(navigated, Is.Empty);
        });
    }

    [Test]
    public void TappingTheCurrentItem_DoesNothing()
    {
        var navigated = new List<NavDestination>();
        var cut = Context.Render<BottomNav>(p => p
            .Add(x => x.Current, NavDestination.Today)
            .Add(x => x.OnNavigate, navigated.Add));

        cut.Find("[data-testid=today-link]").Click();

        Assert.That(navigated, Is.Empty);
    }
}
