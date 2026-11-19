using CalorieTracker.Core.Data;
using CalorieTracker.UI.Models.Search;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Stories.Stories.Screens;

namespace CalorieTracker.UI.Tests.Screens;

public class FoodSearchViewTests : ComponentTestBase
{
    private IRenderedComponent<FoodSearchView> Render(string query = "cola", bool matches = true, OnlineSearchState state = OnlineSearchState.Idle,
        bool notice = false, bool loading = false, bool failed = false, Action? onSearch = null) =>
        Context.Render<FoodSearchView>(p => p
            .Add(x => x.Meal, Meal.Lunch)
            .Add(x => x.Today, new DateOnly(2026, 11, 19))
            .Add(x => x.Query, query)
            .Add(x => x.Groups, matches ? FoodSearchSamples.ColaStored : [])
            .Add(x => x.OnlineState, state)
            .Add(x => x.OnlineProducts, state == OnlineSearchState.Results ? FoodSearchSamples.OnlineFew : [])
            .Add(x => x.OnlineNoticeVisible, notice)
            .Add(x => x.Loading, loading)
            .Add(x => x.Failed, failed)
            .Add(x => x.OnSearchOnline, () => onSearch?.Invoke()));

    private static string Bar(IRenderedComponent<FoodSearchView> cut) => cut.Find("footer [data-testid=food-search-online-bar]").InnerHtml;

    [Test]
    public void TypedWithLocalMatches_PinsASecondaryButtonInTheFooter_NotInTheList()
    {
        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("footer [data-testid=food-search-online]").ClassList, Does.Contain("secondary"));
            Assert.That(cut.FindAll("main [data-testid=food-search-online]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online-notice]"), Is.Empty);
        });
    }

    [Test]
    public void TypedWithNoLocalMatches_PinsAPrimaryButton()
    {
        var cut = Render(matches: false);

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("footer [data-testid=food-search-online]").ClassList, Does.Contain("primary"));
            Assert.That(cut.Find("main [data-testid=food-search-none]"), Is.Not.Null);
        });
    }

    [Test]
    public void FirstUse_ShowsTheNoticeInTheBar_AboveTheButton()
    {
        var html = Bar(Render(notice: true));

        Assert.That(html.IndexOf("food-search-online-notice", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0)
            .And.LessThan(html.IndexOf("food-search-online\"", StringComparison.Ordinal)));
    }

    [Test]
    public void AfterTheNotice_TheBarHoldsOnlyTheButton()
    {
        var cut = Render();

        Assert.That(cut.Find("footer [data-testid=food-search-online-bar]").Children.Select(c => c.GetAttribute("data-testid")), Is.EqualTo(new[] { "food-search-online" }));
    }

    [Test]
    public void TappingTheButton_RaisesOnSearchOnline()
    {
        var taps = 0;
        var cut = Render(onSearch: () => taps++);

        cut.Find("footer [data-testid=food-search-online]").Click();

        Assert.That(taps, Is.EqualTo(1));
    }

    [Test]
    public void EmptyField_ShowsNoBar()
    {
        var cut = Render(query: "", matches: false);

        Assert.That(cut.FindAll("footer"), Is.Empty);
    }

    [Test]
    public void Searching_DropsTheBar_AndKeepsTheSpinnerInTheList()
    {
        var cut = Render(state: OnlineSearchState.Searching, notice: true);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("footer"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online]"), Is.Empty);
            Assert.That(cut.Find("main [data-testid=food-search-online-loading]"), Is.Not.Null);
        });
    }

    [Test]
    public void OnlineResults_DropTheBar_AndShowTheGroupInTheList()
    {
        var cut = Render(state: OnlineSearchState.Results);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("footer"), Is.Empty);
            Assert.That(cut.Find("main [data-testid=food-group-online]"), Is.Not.Null);
        });
    }

    [Test]
    public void LoadingOrFailed_ShowNoBar()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Render(loading: true).FindAll("footer"), Is.Empty);
            Assert.That(Render(failed: true).FindAll("footer"), Is.Empty);
        });
    }

    [Test]
    public void ChangingTheTextBackToIdle_BringsTheBarBack()
    {
        var cut = Render(state: OnlineSearchState.Results);

        cut.Render(p => p.Add(x => x.Query, "cola z").Add(x => x.OnlineState, OnlineSearchState.Idle));

        Assert.That(cut.FindAll("footer [data-testid=food-search-online]"), Has.Count.EqualTo(1));
    }
}
