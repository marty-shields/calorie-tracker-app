using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
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

    [Test]
    public void EveryGroup_HoldsItsRowsInAListWithItsOwnTestId_InTheSameSectionAsItsHeading()
    {
        var groups = new[]
        {
            new FoodGroup(FoodGroupKind.Favourites, [Listing(1)]),
            new FoodGroup(FoodGroupKind.Usual, [Listing(5)]),
            new FoodGroup(FoodGroupKind.Recent, [Listing(2), Listing(3)]),
            new FoodGroup(FoodGroupKind.Diary, [Listing(4)]),
            new FoodGroup(FoodGroupKind.Common, Enumerable.Range(10, 7).Select(Listing).ToArray()),
            new FoodGroup(FoodGroupKind.Branded, [Listing(30)]),
        };
        var cut = Context.Render<FoodSearchView>(p => p.Add(x => x.Query, "a").Add(x => x.Groups, groups));

        Assert.Multiple(() =>
        {
            foreach (var name in new[] { "favourites", "usual", "recent", "diary", "common", "branded" })
            {
                var list = cut.Find($"ul[data-testid=food-group-{name}-list]");
                Assert.That(cut.Find($"h2[data-testid=food-group-{name}]").Closest("section.group"), Is.SameAs(list.Closest("section.group")), name);
                Assert.That(list.QuerySelectorAll("[data-testid^=food-result-]"), Is.Not.Empty, name);
            }

            Assert.That(cut.FindAll("[data-testid=food-group-recent-list] [data-testid^=food-result-]").Select(r => r.GetAttribute("data-testid")), Is.EqualTo(new[] { "food-result-2", "food-result-3" }));
            Assert.That(cut.FindAll("[data-testid=food-group-common-list] [data-testid^=food-result-]"), Has.Count.EqualTo(5));
            Assert.That(cut.FindAll("[data-testid=food-group-common-list] [data-testid=food-group-common-more]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=food-group-diary-list] [data-testid^=food-result-]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void OnlineGroup_HoldsItsRowsInAListWithItsOwnTestId()
    {
        var cut = Render(state: OnlineSearchState.Results);

        Assert.That(cut.FindAll("[data-testid=food-group-online-list] [data-testid^=food-online-]"), Is.Not.Empty);
    }

    private static FoodListing Listing(int id) => new(new Food { Id = id, Name = $"Food {id}", Unit = FoodUnit.Gram, Kcal = 100 }, null);

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
            Assert.That(cut.FindAll("main [data-testid=food-search-online-section]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online-error]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online-retry]"), Is.Empty);
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
            Assert.That(cut.FindAll("main [data-testid=food-search-online-section]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online-error]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online-retry]"), Is.Empty);
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

    [Test]
    public void TappingTheButton_MovesFocusToTheOnlineSectionOnceItShows()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render();

        cut.Find("footer [data-testid=food-search-online]").Click();
        cut.Render(p => p.Add(x => x.OnlineState, OnlineSearchState.Searching));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("main [data-testid=food-search-online-section]").GetAttribute("tabindex"), Is.EqualTo("-1"));
            Assert.That(Context.JSInterop.Invocations.Select(i => i.Identifier), Has.Some.EqualTo("Blazor._internal.domWrapper.focus"));
        });
    }
}
