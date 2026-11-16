using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Seed;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace CalorieTracker.UI.Tests.Screens;

public class FoodSearchPageTests : ComponentTestBase
{
    private static readonly Food Oats = new() { Id = 1, Name = "Oats, rolled", Unit = FoodUnit.Gram, Kcal = 375, ProteinG = 11, CarbsG = 60, FatG = 9 };

    private FakeSearch search = null!;
    private FakeLoader loader = null!;
    private SeedProgress seed = null!;
    private FakeLogger<FoodSearchPage> logger = null!;

    [SetUp]
    public void SetUp()
    {
        search = new FakeSearch();
        loader = new FakeLoader();
        seed = new SeedProgress(loader);
        logger = new FakeLogger<FoodSearchPage>();
        Context.Services.AddSingleton<IFoodSearch>(search);
        Context.Services.AddSingleton(seed);
        Context.Services.AddSingleton<ILogger<FoodSearchPage>>(logger);
    }

    [Test]
    public void SeedNeverStarted_ShowsThePrompt()
    {
        var cut = Context.Render<FoodSearchPage>();

        Assert.That(cut.FindAll("[data-testid=food-search-idle]"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Typing_ListsTheResults()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.Multiple(() =>
        {
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat" }));
            Assert.That(cut.Find("[data-testid=food-result-1]").TextContent, Does.Contain("Oats, rolled").And.Contain("375 kcal / 100 g"));
        });
    }

    [Test]
    public async Task Typing_NothingFound_SaysNoFoodsMatch()
    {
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("zzz");

        Assert.That(cut.Find("[data-testid=food-search-none]").TextContent, Is.EqualTo("No foods match"));
    }

    [Test]
    public async Task SeedLoading_ShowsLoadingAndSearchesWhatWasTypedOnceItFinishes()
    {
        search.Results = [Oats];
        var load = seed.Start();
        var cut = Context.Render<FoodSearchPage>();
        var duringLoad = cut.FindAll("[data-testid=food-search-loading]").Count;

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        var searchedWhileLoading = search.Queries.Count;
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0, 0));
        await load;
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(duringLoad, Is.EqualTo(1));
            Assert.That(searchedWhileLoading, Is.EqualTo(0));
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat" }));
            Assert.That(cut.FindAll("[data-testid=food-search-loading]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task SeedLoading_NothingTyped_ShowsThePromptWhenItFinishes()
    {
        var load = seed.Start();
        var cut = Context.Render<FoodSearchPage>();

        loader.Complete(new SeedLoadResult(SeedLoadStatus.UpToDate, 1, 0, 0, 0));
        await load;
        await WaitForRendererAsync();

        Assert.That(cut.FindAll("[data-testid=food-search-idle]"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SeedAlreadyLoaded_DoesNotLogAWait()
    {
        var load = seed.Start();
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0, 0));
        await load;

        Context.Render<FoodSearchPage>();

        Assert.That(LoggedEvents.Of(logger), Is.Empty);
    }

    [Test]
    public async Task OlderSearchFinishingLate_DoesNotOverwriteTheNewerResults()
    {
        var older = new TaskCompletionSource<FoodSearchResult>();
        search.Held["oa"] = older;
        var cut = Context.Render<FoodSearchPage>();
        var input = cut.Find("[data-testid=food-search-input]");
        var olderTyped = input.InputAsync("oa");
        search.Results = [Oats];
        await input.InputAsync("oat");

        older.SetResult(FoodSearchResult.Failed());
        await olderTyped;
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-search-error]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Disposed_WhileTheSeedLoads_NeverSearches()
    {
        var load = seed.Start();
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await Context.DisposeComponentsAsync();
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0, 0));
        await load;
        await WaitForRendererAsync();

        Assert.That(search.Queries, Is.Empty);
    }

    [Test]
    public async Task SeedFailed_ShowsTheErrorAndTryAgainLoadsAgain()
    {
        search.Results = [Oats];
        var load = seed.Start();
        var cut = Context.Render<FoodSearchPage>();
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Failed, 0, 0, 0, 0));
        await load;
        await WaitForRendererAsync();
        var errorShown = cut.FindAll("[data-testid=food-search-error]").Count;

        cut.Find("[data-testid=food-search-retry]").Click();
        await WaitForRendererAsync();
        var loadingShown = cut.FindAll("[data-testid=food-search-loading]").Count;
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0, 0));
        await seed.Current!;
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(errorShown, Is.EqualTo(1));
            Assert.That(loadingShown, Is.EqualTo(1));
            Assert.That(loader.Calls, Is.EqualTo(2));
            Assert.That(cut.FindAll("[data-testid=food-search-error]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-idle]"), Has.Count.EqualTo(1));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { UiEvent.FoodSeedWaited, UiEvent.FoodSeedRetried, UiEvent.FoodSeedWaited }));
            Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Has.Some.Contain("Loaded"));
        });
    }

    [Test]
    public async Task SeedAlreadyFailed_ShowsTheErrorAtOnce()
    {
        var load = seed.Start();
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Failed, 0, 0, 0, 0));
        await load;

        var cut = Context.Render<FoodSearchPage>();

        Assert.That(cut.FindAll("[data-testid=food-search-error]"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SearchFailed_ShowsTheErrorAndTryAgainSearchesAgain()
    {
        search.Failing = true;
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        var errorShown = cut.FindAll("[data-testid=food-search-error]").Count;

        search.Failing = false;
        search.Results = [Oats];
        cut.Find("[data-testid=food-search-retry]").Click();
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(errorShown, Is.EqualTo(1));
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat", "oat" }));
            Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TappingAResult_ShowsItsValuesAndBackToResultsReturnsToTheList()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        cut.Find("[data-testid=food-result-1]").Click();
        var kcal = cut.Find("[data-testid=food-detail-kcal]").TextContent;
        cut.Find("[data-testid=food-detail-close]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(kcal, Is.EqualTo("375 kcal"));
            Assert.That(cut.FindAll("[data-testid=food-detail]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task AndroidBack_WithAFoodOpen_ReturnsToTheListNotTheDashboard()
    {
        var navigated = new List<NavDestination>();
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.OnNavigate, navigated.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        cut.Find("[data-testid=food-result-1]").Click();

        await BackButton.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-detail]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
            Assert.That(navigated, Is.Empty);
        });
    }

    [Test]
    public async Task AndroidBack_FromTheList_GoesToTheDashboard()
    {
        var navigated = new List<NavDestination>();
        Context.Render<FoodSearchPage>(p => p.Add(x => x.OnNavigate, navigated.Add));

        await BackButton.HandleAsync();

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Dashboard }));
    }

    [Test]
    public async Task DashboardItem_NavigatesToTheDashboard()
    {
        var navigated = new List<NavDestination>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.OnNavigate, navigated.Add));

        await WaitForRendererAsync();
        await cut.Find("[data-testid=dashboard-link]").ClickAsync(new());

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Dashboard }));
    }

    [Test]
    public async Task Opens_OnIngredientsAndSearchesWithThatType()
    {
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=food-filter-ingredients]").HasAttribute("checked"), Is.True);
            Assert.That(cut.FindAll("[data-testid=food-filter] input").Count(i => i.HasAttribute("checked")), Is.EqualTo(1));
            Assert.That(search.Types, Is.EqualTo(new FoodType?[] { FoodType.Ingredient }));
        });
    }

    [Test]
    public void TypeChips_AreLabelledIngredientsFoodsDrinksAllInThatOrder()
    {
        var cut = Context.Render<FoodSearchPage>();

        var labels = cut.FindAll("[data-testid=food-filter] label").Select(l => l.TextContent.Trim());

        Assert.That(labels, Is.EqualTo(new[] { "Ingredients", "Foods", "Drinks", "All" }));
    }

    [TestCase("food-filter-foods", FoodType.Food)]
    [TestCase("food-filter-drinks", FoodType.Drink)]
    [TestCase("food-filter-all", null)]
    public async Task ChoosingAChip_SearchesAgainWithItsType(string chip, FoodType? expected)
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await cut.Find($"[data-testid={chip}]").ChangeAsync(true);
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat", "oat" }));
            Assert.That(search.Types, Is.EqualTo(new FoodType?[] { FoodType.Ingredient, expected }));
            Assert.That(cut.Find($"[data-testid={chip}]").HasAttribute("checked"), Is.True);
            Assert.That(cut.Find("[data-testid=food-search-input]").GetAttribute("value"), Is.EqualTo("oat"));
        });
    }

    [Test]
    public async Task ResultRow_ShowsTheTypeAndTheKcal()
    {
        search.Results = [new Food { Id = 7, Name = "Oat drink", Brand = "Oatly", Type = FoodType.Drink, Unit = FoodUnit.Millilitre, Kcal = 46 }];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-result-7] .aside"), Is.Empty);
            Assert.That(cut.Find("[data-testid=food-result-7] .detail").TextContent, Is.EqualTo("Drink · Oatly · 46 kcal / 100 ml"));
        });
    }

    [Test]
    public async Task ResultRow_WithoutABrand_ReadsTypeThenKcal()
    {
        search.Results = [new Food { Id = 8, Name = "Cheddar cheese", Type = FoodType.Ingredient, Unit = FoodUnit.Gram, Kcal = 416 }];
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        await cut.Find("[data-testid=food-search-input]").InputAsync("chedd");

        Assert.That(cut.Find("[data-testid=food-result-8] .detail").TextContent, Is.EqualTo("Ingredient · 416 kcal / 100 g"));
    }

    [Test]
    public async Task SelectedFood_PinsAddAndBackToTheBottomBar_NotTheScrollingValues()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=bottom-bar] [data-testid=food-detail-add]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=bottom-bar] [data-testid=food-detail-close]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("main [data-testid=food-detail-add], main [data-testid=food-detail-close]"), Is.Empty);
        });
    }

    [Test]
    public async Task SelectedFood_BackToResultsFromTheBottomBar_ClearsTheSelectionAndTheBar()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        await cut.Find("[data-testid=bottom-bar] [data-testid=food-detail-close]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-detail]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=bottom-bar]"), Is.Empty);
        });
    }

    [Test]
    public async Task ResultsList_HasNoBottomBar()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.That(cut.FindAll("[data-testid=bottom-bar]"), Is.Empty);
    }

    [Test]
    public async Task AddMode_NothingFound_ShowsTheMessageAndHint()
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        await cut.Find("[data-testid=food-search-input]").InputAsync("xyzzy");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=food-search-none-message]").TextContent, Is.EqualTo("No foods match"));
            Assert.That(cut.Find("[data-testid=food-search-none-hint]").TextContent, Is.EqualTo("Try another word, or switch to All."));
        });
    }

    [Test]
    public async Task AddMode_NothingFoundUnderAll_HintsOnlyToTryAnotherWord()
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        await cut.Find("[data-testid=food-filter-all]").ChangeAsync(true);
        await cut.Find("[data-testid=food-search-input]").InputAsync("xyzzy");

        Assert.That(cut.Find("[data-testid=food-search-none-hint]").TextContent, Is.EqualTo("Try another word."));
    }

    [Test]
    public async Task TabMode_NothingFound_StaysTheMessageOnly()
    {
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("xyzzy");

        Assert.That(cut.FindAll("[data-testid=food-search-none-hint]"), Is.Empty);
    }

    [Test]
    public async Task SeedLoading_FilterAndQueryStay_AndTheSearchUsesThemOnceItFinishes()
    {
        search.Results = [Oats];
        var load = seed.Start();
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await cut.Find("[data-testid=food-filter-drinks]").ChangeAsync(true);
        var searchedWhileLoading = search.Queries.Count;
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0, 0));
        await load;
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(searchedWhileLoading, Is.EqualTo(0));
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat" }));
            Assert.That(search.Types, Is.EqualTo(new FoodType?[] { FoodType.Drink }));
            Assert.That(cut.Find("[data-testid=food-filter-drinks]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public async Task Logs_AfterChangingTheFilter_DoNotHoldWhatWasTyped()
    {
        const string Typed = "my-private-meal-xyz";
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync(Typed);

        await cut.Find("[data-testid=food-filter-drinks]").ChangeAsync(true);
        await WaitForRendererAsync();

        Assert.That(search.Types, Is.EqualTo(new FoodType?[] { FoodType.Ingredient, FoodType.Drink }));
        Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Has.None.Contain(Typed));
    }

    [Test]
    public async Task Logs_NeverHoldWhatWasTyped()
    {
        const string Typed = "my-private-meal-xyz";
        search.Failing = true;
        var load = seed.Start();
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync(Typed);
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0, 0));
        await load;
        await WaitForRendererAsync();

        var records = logger.Collector.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { UiEvent.FoodSeedWaited }));
            Assert.That(records.Select(r => r.Message), Has.None.Contain(Typed));
            Assert.That(records.SelectMany(r => r.StructuredState ?? []).Select(p => p.Value), Has.None.Contain(Typed));
        });
    }

    [Test]
    public async Task Selected_AddToDiary_RaisesOnAddWithTheFoodAndTheSearch()
    {
        search.Results = [Oats];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        await cut.Find("[data-testid=food-filter-all]").ChangeAsync(true);
        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        await cut.Find("[data-testid=food-detail-add]").ClickAsync(new());

        Assert.That(requests, Is.EqualTo(new[] { new FoodAddRequest(Oats, "oat", null) }));
    }

    [Test]
    public async Task AddMode_ATappedResult_RaisesOnAddAtOnce_WithNoValuesStep()
    {
        search.Results = [Oats];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(requests, Is.EqualTo(new[] { new FoodAddRequest(Oats, "oat", FoodType.Ingredient) }));
            Assert.That(cut.FindAll("[data-testid=food-detail]"), Is.Empty);
        });
    }

    [Test]
    public async Task AddMode_ADoubleTapOnAResult_AddsOnce()
    {
        search.Results = [Oats];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());
        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        Assert.That(requests, Has.Count.EqualTo(1));
    }

    [Test]
    public void TabMode_ShowsAPostedSnackbar_WhereAddModeLeavesItToTheDashboard()
    {
        Context.Services.GetRequiredService<SnackbarService>().Show("Oats added to Lunch");

        var tab = Context.Render<FoodSearchPage>();
        var add = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        Assert.Multiple(() =>
        {
            Assert.That(tab.FindAll("[data-testid=snackbar]"), Has.Count.EqualTo(1));
            Assert.That(add.FindAll("[data-testid=snackbar]"), Is.Empty);
        });
    }

    [Test]
    public void AddMode_TitlesTheMeal_HasABackArrowAndNoNavigationBar()
    {
        var backed = 0;
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Dinner).Add(x => x.OnBack, () => backed++));

        cut.Find("[data-testid=back]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=screen-title]").TextContent.Trim(), Is.EqualTo("Add to Dinner"));
            Assert.That(cut.FindAll("[data-testid=bottom-nav]"), Is.Empty);
            Assert.That(backed, Is.EqualTo(1));
        });
    }

    [Test]
    public void FoodsTab_IsTitledSearchFoods_WithTheNavigationBar()
    {
        var cut = Context.Render<FoodSearchPage>();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=screen-title]").TextContent.Trim(), Is.EqualTo("Search foods"));
            Assert.That(cut.FindAll("[data-testid=bottom-nav]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void OpeningWithAQueryAndType_SearchesThemRightAway()
    {
        search.Results = [Oats];

        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.InitialQuery, "oat").Add(x => x.InitialFilter, (FoodType?)null));

        Assert.Multiple(() =>
        {
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat" }));
            Assert.That(search.Types, Is.EqualTo(new FoodType?[] { null }));
            Assert.That(cut.Find("[data-testid=food-search-input]").GetAttribute("value"), Is.EqualTo("oat"));
            Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
            Assert.That(cut.Find("[data-testid=food-filter-all]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public void ASnackbarPosted_ShowsAboveTheNavigationBar()
    {
        var cut = Context.Render<FoodSearchPage>();

        Context.Services.GetRequiredService<SnackbarService>().Show("Oats added to Lunch", "View day", () => Task.CompletedTask);

        cut.WaitForState(() => cut.FindAll("[data-testid=snackbar]").Count == 1);
        Assert.That(cut.Find("[data-testid=snackbar-action]").TextContent.Trim(), Is.EqualTo("View day"));
    }

    private sealed class FakeSearch : IFoodSearch
    {
        public List<string> Queries { get; } = [];

        public List<FoodType?> Types { get; } = [];

        public IReadOnlyList<Food> Results { get; set; } = [];

        public bool Failing { get; set; }

        public Dictionary<string, TaskCompletionSource<FoodSearchResult>> Held { get; } = [];

        public Task<FoodSearchResult> SearchAsync(string query, FoodType? type = null, int limit = IFoodSearch.DefaultLimit, CancellationToken ct = default)
        {
            Types.Add(type);
            return Run(query);
        }

        private Task<FoodSearchResult> Run(string query)
        {
            Queries.Add(query);
            if (Held.TryGetValue(query, out var held))
            {
                return held.Task;
            }

            return Task.FromResult(Failing ? FoodSearchResult.Failed() : FoodSearchResult.Found(Results));
        }
    }

    private sealed class FakeLoader : ISeedLoader
    {
        private TaskCompletionSource<SeedLoadResult> pending = new();

        public int Calls { get; private set; }

        public Task<SeedLoadResult> LoadAsync()
        {
            Calls++;
            pending = new TaskCompletionSource<SeedLoadResult>();
            return pending.Task;
        }

        public void Complete(SeedLoadResult result) => pending.SetResult(result);
    }
}
