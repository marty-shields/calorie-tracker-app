using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Navigation;

public class NavScreenTests : ComponentTestBase
{
    private IRenderedComponent<NavScreen> Render(NavDestination destination, Action<ComponentParameterCollectionBuilder<NavScreen>>? more = null) =>
        Context.Render<NavScreen>(p =>
        {
            p.Add(x => x.Title, "Screen").Add(x => x.Destination, destination);
            more?.Invoke(p);
        });

    [Test]
    public void Bar_ShowsTodayAddAndTrends_WithTheirTestIds()
    {
        var cut = Render(NavDestination.Today);

        var buttons = cut.FindAll("nav button").Select(b => (b.GetAttribute("data-testid"), b.TextContent.Trim(), b.QuerySelectorAll("svg").Length));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("nav").GetAttribute("data-testid"), Is.EqualTo("bottom-nav"));
            Assert.That(buttons, Is.EqualTo(new[] { ("today-link", "Today", 1), ("add-button", "Add", 1), ("trends-link", "Trends", 1) }));
        });
    }

    [TestCase(NavDestination.Today, "today-link")]
    [TestCase(NavDestination.Trends, "trends-link")]
    public void TheScreensItem_IsTheOnlyCurrentPage(NavDestination destination, string testId)
    {
        var cut = Render(destination);

        Assert.That(cut.FindAll("[aria-current=page]").Select(b => b.GetAttribute("data-testid")), Is.EqualTo(new[] { testId }));
    }

    [Test]
    public void TheAddButton_OpensADialog_IsTheSheetOpener_AndRaisesOnAdd()
    {
        var added = 0;
        var cut = Render(NavDestination.Today, p => p.Add(x => x.OnAdd, () => added++));
        var add = cut.Find("[data-testid=add-button]");

        add.Click();

        Assert.Multiple(() =>
        {
            Assert.That(add.GetAttribute("aria-haspopup"), Is.EqualTo("dialog"));
            Assert.That(add.HasAttribute("data-sheet-opener"), Is.True);
            Assert.That(add.HasAttribute("aria-current"), Is.False);
            Assert.That(added, Is.EqualTo(1));
        });
    }

    [Test]
    public void TappingTrends_RaisesItsDestination_AndTappingTheCurrentOneDoesNot()
    {
        var navigated = new List<NavDestination>();
        var cut = Render(NavDestination.Today, p => p.Add(x => x.OnNavigate, navigated.Add));

        cut.Find("[data-testid=today-link]").Click();
        cut.Find("[data-testid=trends-link]").Click();

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Trends }));
    }

    [Test]
    public void Start_IsTheFirstTab_BecauseTheKitSendsAndroidBackThere()
    {
        Assert.That(NavDestinations.Start, Is.EqualTo(NavDestinations.All[0].Destination));
        Assert.That(NavDestinations.DestinationOf(NavDestinations.Tabs[0]), Is.EqualTo(NavDestinations.Start));
    }

    [Test]
    public async Task OnTrends_AndroidBackGoesToTheStartDestination_WithNoBackArrow()
    {
        var navigated = new List<NavDestination>();
        var cut = Render(NavDestination.Trends, p => p.Add(x => x.OnNavigate, navigated.Add));

        await BackButton.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(navigated, Is.EqualTo(new[] { NavDestinations.Start }));
            Assert.That(cut.FindAll("[data-testid=back]"), Is.Empty);
        });
    }

    [Test]
    public void OnToday_TheScreenDoesNotTakeAndroidBack()
    {
        Render(NavDestination.Today, p => p.Add(x => x.OnNavigate, (NavDestination _) => { }));

        Assert.That(BackButton.HasHandler, Is.False);
    }
}
