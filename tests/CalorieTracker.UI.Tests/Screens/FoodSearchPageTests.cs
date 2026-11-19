using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Seed;
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
        Context.Services.AddSingleton<IOnlineFoodSearch>(new FakeOnlineSearch());
        Context.Services.AddSingleton<IOnlineFoodImport>(new FakeOnlineImport());
        Context.Services.AddSingleton<IOnlineSearchNotice>(new FakeOnlineNotice());
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

        Assert.That(cut.Find("[data-testid=food-search-none-message]").TextContent, Is.EqualTo("No foods match"));
    }

    [Test]
    public async Task TheBarcodeButton_RaisesOnScan_AndSitsAfterTheSearchFieldWithADivider()
    {
        var scans = 0;
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Dinner).Add(x => x.OnScan, (string _) => scans++));

        var button = cut.Find("[data-testid=search-scan]");
        var order = cut.FindAll(".search > *").Select(e => e.GetAttribute("data-testid") ?? e.ClassList.First()).ToList();
        button.Click();

        Assert.Multiple(() =>
        {
            Assert.That(button.GetAttribute("aria-label"), Is.EqualTo("Scan a barcode"));
            Assert.That(order.IndexOf("food-search-input"), Is.LessThan(order.IndexOf("search-divider")));
            Assert.That(order.IndexOf("search-divider"), Is.LessThan(order.IndexOf("search-scan")));
            Assert.That(scans, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TheBarcodeButton_RaisesOnScanWithTheTypedText_NotTheInitialQuery()
    {
        var scans = new List<string>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Dinner).Add(x => x.InitialQuery, "ch").Add(x => x.OnScan, scans.Add));

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        cut.Find("[data-testid=search-scan]").Click();

        Assert.That(scans, Is.EqualTo(new[] { "oat" }));
    }

    [Test]
    public async Task TheBarcodeButton_AfterAResultWasTapped_RaisesNothing()
    {
        search.Results = [Oats];
        var scans = new List<string>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, (FoodAddRequest _) => { }).Add(x => x.OnScan, scans.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        await cut.Find("[data-testid=search-scan]").ClickAsync(new());

        Assert.That(scans, Is.Empty);
    }

    [Test]
    public async Task TheBarcodeButton_StaysWhileTyping()
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.That(cut.FindAll("[data-testid=search-scan]"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AResult_NeverShowsTheFoodsValues_ItRaisesOnAdd()
    {
        search.Results = [Oats];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(requests.Select(r => r.Food.Id), Is.EqualTo(new[] { 1 }));
            Assert.That(cut.FindAll("[data-testid=food-detail]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-detail-add]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=bottom-bar]").SelectMany(bar => bar.Children).Select(c => c.GetAttribute("data-testid")).Distinct(), Is.SubsetOf(new[] { "food-search-online-bar" }));
        });
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
    public async Task AndroidBack_FromTheList_RaisesOnBack()
    {
        var back = 0;
        Context.Render<FoodSearchPage>(p => p.Add(x => x.OnBack, () => back++));

        await BackButton.HandleAsync();

        Assert.That(back, Is.EqualTo(1));
    }

    [Test]
    public async Task ThereIsNoNavigationBar()
    {
        var cut = Context.Render<FoodSearchPage>();

        await WaitForRendererAsync();

        Assert.That(cut.FindAll("[data-testid=bottom-nav]"), Is.Empty);
    }

    [Test]
    public async Task ShowsTheSearchBoxAndResultsOnly_WithNoFoodFilter()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid^=food-filter]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-input]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=food-results] li"), Has.Count.EqualTo(1));
            Assert.That(search.Queries.Last(), Is.EqualTo("oat"));
        });
    }

    [Test]
    public async Task ResultRow_StoredNutella_ShowsNutellaWithFerreroPackAndKcal()
    {
        search.Results = [new Food { Id = 8, Name = "Nutella", Brand = "Ferrero", Brands = "Ferrero, Nutella", PackageLabel = "400 g e", Unit = FoodUnit.Gram, Kcal = 539 }];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("nutella");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=food-result-8] .text").TextContent, Is.EqualTo("Nutella"));
            Assert.That(cut.Find("[data-testid=food-result-8] .detail").TextContent, Is.EqualTo("Ferrero · 400 g e · 539 kcal / 100 g"));
        });
    }

    [Test]
    public async Task ResultRow_ABlankBrand_ShowsTheNameAloneWithNoSeparator()
    {
        search.Results = [new Food { Id = 9, Name = "Oat drink", Brand = " ", Brands = " ", Unit = FoodUnit.Millilitre, Kcal = 46 }];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=food-result-9] .text").TextContent, Is.EqualTo("Oat drink"));
            Assert.That(cut.Find("[data-testid=food-result-9] .detail").TextContent, Is.EqualTo("46 kcal / 100 ml"));
        });
    }

    [Test]
    public async Task ResultRow_ShowsTheTitleBrandLinePackAndTheKcal_NotTheType()
    {
        search.Results = [new Food { Id = 7, Name = "Oat drink", Brand = "Oatly, Alpro", Brands = "Oatly, Alpro", PackageLabel = "1 l", Type = FoodType.Drink, Unit = FoodUnit.Millilitre, Kcal = 46 }];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-result-7] .aside"), Is.Empty);
            Assert.That(cut.Find("[data-testid=food-result-7] .detail").TextContent, Is.EqualTo("Alpro · 1 l · 46 kcal / 100 ml"));
            Assert.That(cut.Find("[data-testid=food-result-7] .text").TextContent, Is.EqualTo("Oatly Oat drink"));
        });
    }

    [Test]
    public async Task ResultRow_WithoutABrand_ReadsKcalOnly()
    {
        search.Results = [new Food { Id = 8, Name = "Cheddar cheese", Type = FoodType.Ingredient, Unit = FoodUnit.Gram, Kcal = 416 }];
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        await cut.Find("[data-testid=food-search-input]").InputAsync("chedd");

        Assert.That(cut.Find("[data-testid=food-result-8] .detail").TextContent, Is.EqualTo("416 kcal / 100 g"));
    }

    [Test]
    public async Task ResultsList_BottomBarHoldsOnlyTheSearchOnlineButton()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.That(cut.Find("[data-testid=bottom-bar]").Children.Select(c => c.GetAttribute("data-testid")), Is.EqualTo(new[] { "food-search-online-bar" }));
    }

    [Test]
    public async Task NothingFound_ShowsTheMessageAndHint()
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        await cut.Find("[data-testid=food-search-input]").InputAsync("xyzzy");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=food-search-none-message]").TextContent, Is.EqualTo("No foods match"));
            Assert.That(cut.Find("[data-testid=food-search-none-hint]").TextContent, Is.EqualTo("Try another word."));
        });
    }

    [Test]
    public async Task SeedLoading_TheQueryStays_AndTheSearchUsesItOnceItFinishes()
    {
        search.Results = [Oats];
        var load = seed.Start();
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        var searchedWhileLoading = search.Queries.Count;
        loader.Complete(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0, 0));
        await load;
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(searchedWhileLoading, Is.EqualTo(0));
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat" }));
            Assert.That(cut.Find("[data-testid=food-search-input]").GetAttribute("value"), Is.EqualTo("oat"));
        });
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

    private static readonly Food Milk = new() { Id = 2, Name = "Milk", Unit = FoodUnit.Millilitre, Kcal = 50 };
    private static readonly Food Brand = new() { Id = 3, Name = "Oat bar", Brand = "Acme", Unit = FoodUnit.Gram, Kcal = 400 };
    private static readonly Food Rice = new() { Id = 4, Name = "Rice", Unit = FoodUnit.Gram, Kcal = 130 };

    private static FoodListing Logged(Food food, double amount, double? servings = null, int times = 1, int? servingId = null) =>
        new(food, new CalorieTracker.Core.History.FoodHistory(food.Id, times, 1, new DateOnly(2031, 7, 19), amount, servings, servingId));

    private static string[] Ids(IRenderedComponent<FoodSearchPage> cut, string selector) =>
        cut.FindAll(selector).Select(e => e.GetAttribute("data-testid")!).ToArray();

    [Test]
    public async Task NothingTyped_ShowsUsualThenRecentFromTheMeal()
    {
        search.Idle = [new FoodGroup(FoodGroupKind.Usual, [Logged(Oats, 40)]), new FoodGroup(FoodGroupKind.Recent, [Logged(Milk, 150)])];

        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Breakfast));
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(search.Meals, Is.EqualTo(new Meal?[] { Meal.Breakfast }));
            Assert.That(cut.FindAll("h2[data-testid^=food-group-]").Select(h => h.TextContent), Is.EqualTo(new[] { "Usual for breakfast", "Recent" }));
            Assert.That(cut.FindAll("[data-testid=food-search-idle]"), Is.Empty);
            Assert.That(cut.Find("[data-testid=food-result-1]").TextContent, Does.Contain("40 g").And.Contain("150 kcal"));
            Assert.That(cut.Find("[data-testid=food-result-2]").TextContent, Does.Contain("150 ml").And.Contain("75 kcal"));
        });
    }

    private static FoodListing[] Favourites(int count) =>
        Enumerable.Range(0, count).Select(i => new FoodListing(new Food { Id = 50 + i, Name = $"Fav {(char)('a' + i)}", Unit = i == 1 ? FoodUnit.Millilitre : FoodUnit.Gram, Kcal = 100 + i }, null)).ToArray();

    [Test]
    public async Task NothingTyped_Favourites_ShowFirstAFiveWithShowMoreThenExpandAndTypingCollapses()
    {
        var favourites = Favourites(9);
        search.Idle = [new FoodGroup(FoodGroupKind.Favourites, favourites), new FoodGroup(FoodGroupKind.Usual, [Logged(Oats, 40)]), new FoodGroup(FoodGroupKind.Recent, [Logged(Milk, 150)])];
        search.Groups = [new FoodGroup(FoodGroupKind.Favourites, favourites)];
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Breakfast));
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("h2[data-testid^=food-group-]").Select(h => h.TextContent), Is.EqualTo(new[] { "Favourites", "Usual for breakfast", "Recent" }));
            Assert.That(cut.Find("h2[data-testid=food-group-favourites]").TagName, Is.EqualTo("H2"));
            Assert.That(cut.FindAll("[data-testid^=food-result-5]"), Has.Count.EqualTo(5));
            Assert.That(cut.Find("[data-testid=food-group-favourites-more]").TextContent, Does.Contain("Show 4 more"));
            Assert.That(cut.Find("[data-testid=food-result-50]").TextContent, Does.Contain("100 kcal / 100 g"));
            Assert.That(cut.Find("[data-testid=food-result-51]").TextContent, Does.Contain("101 kcal / 100 ml"));
        });

        await cut.Find("[data-testid=food-group-favourites-more]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-group-favourites-more]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid^=food-result-5]"), Has.Count.EqualTo(9));
        });

        await cut.Find("[data-testid=food-search-input]").InputAsync("fav");

        Assert.That(cut.Find("[data-testid=food-group-favourites-more]").TextContent, Does.Contain("Show 4 more"));
    }

    [Test]
    public async Task NothingTyped_WithFavourites_ShowsFavouritesThenRecent()
    {
        search.Idle = [new FoodGroup(FoodGroupKind.Favourites, Favourites(2)), new FoodGroup(FoodGroupKind.Recent, [Logged(Milk, 150)])];

        var cut = Context.Render<FoodSearchPage>();
        await WaitForRendererAsync();

        Assert.That(cut.FindAll("h2[data-testid^=food-group-]").Select(h => h.TextContent), Is.EqualTo(new[] { "Favourites", "Recent" }));
        Assert.That(cut.FindAll("[data-testid=food-search-idle]"), Is.Empty);
    }

    [Test]
    public async Task Tapping_ALoggedFavourite_RaisesOnAddWithItsLastAmount()
    {
        var logged = Logged(Oats, 60, servings: 2);
        search.Idle = [new FoodGroup(FoodGroupKind.Favourites, [logged])];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await WaitForRendererAsync();

        Assert.That(cut.Find($"[data-testid=food-result-{Oats.Id}]").TextContent, Does.Contain("2 servings (60 g) · 225 kcal").And.Not.Contain("/ 100"));
        await cut.Find($"[data-testid=food-result-{Oats.Id}]").ClickAsync(new());

        Assert.That(requests, Is.EqualTo(new[] { new FoodAddRequest(Oats, "", 60, 2) }));
    }

    [Test]
    public async Task Typing_FavouritesGroup_ComesBeforeTheOtherGroups()
    {
        search.Groups = [new FoodGroup(FoodGroupKind.Favourites, Favourites(2)), new FoodGroup(FoodGroupKind.Common, [new FoodListing(Oats, null)])];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("o");

        Assert.That(cut.FindAll("h2[data-testid^=food-group-]").Select(h => h.TextContent), Is.EqualTo(new[] { "Favourites", "Common" }));
    }

    [Test]
    public async Task Tapping_AFavouriteWhoseHistoryWasNotRead_LooksUpTheLastAmount()
    {
        search.Idle = [new FoodGroup(FoodGroupKind.Favourites, [new FoodListing(Oats, null)])];
        search.Last[Oats.Id] = new CalorieTracker.Core.History.FoodHistory(Oats.Id, 1, 1, new DateOnly(2031, 7, 19), 45, null, null);
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await WaitForRendererAsync();

        await cut.Find($"[data-testid=food-result-{Oats.Id}]").ClickAsync(new());

        Assert.That(requests, Is.EqualTo(new[] { new FoodAddRequest(Oats, "", 45, null) }));
    }

    [Test]
    public async Task NothingTyped_EmptyDiary_ShowsTheEmptyState()
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Breakfast));
        await WaitForRendererAsync();

        Assert.That(cut.FindAll("[data-testid=food-search-idle]"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task NothingTyped_HistoryReadFails_ShowsTheEmptyStateAndStillSearches()
    {
        search.IdleFailing = true;
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();
        await WaitForRendererAsync();
        var idle = cut.FindAll("[data-testid=food-search-idle]").Count;

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.That(idle, Is.EqualTo(1));
        Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ClearingTheText_ReturnsToTheFoodsBeforeTyping()
    {
        search.Idle = [new FoodGroup(FoodGroupKind.Recent, [Logged(Milk, 150)])];
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await cut.Find("[data-testid=food-search-input]").InputAsync("");

        Assert.That(Ids(cut, "h2[data-testid^=food-group-]"), Is.EqualTo(new[] { "food-group-recent" }));
    }

    [Test]
    public async Task Typing_ShowsGroupsInOrderAndOmitsEmptyOnes()
    {
        search.Groups =
        [
            new FoodGroup(FoodGroupKind.Diary, [Logged(Milk, 150, times: 3)]),
            new FoodGroup(FoodGroupKind.Common, [new FoodListing(Oats, null)]),
            new FoodGroup(FoodGroupKind.Branded, [new FoodListing(Brand, null)]),
        ];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("o");

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("h2[data-testid^=food-group-]").Select(h => h.TextContent), Is.EqualTo(new[] { "From your diary", "Common", "Branded" }));
            Assert.That(cut.Find("[data-testid=food-result-1]").TextContent, Does.Contain("375 kcal / 100 g"));
            Assert.That(cut.Find("[data-testid=food-result-3]").TextContent, Does.Contain("Acme Oat bar").And.Contain("400 kcal / 100 g"));
        });

        search.Groups = [new FoodGroup(FoodGroupKind.Branded, [new FoodListing(Brand, null)])];
        await cut.Find("[data-testid=food-search-input]").InputAsync("oa");

        Assert.That(Ids(cut, "h2[data-testid^=food-group-]"), Is.EqualTo(new[] { "food-group-branded" }));
    }

    [Test]
    public async Task Typing_DiaryRowShowsTheLastAmountAndItsCalories_InServings()
    {
        search.Groups = [new FoodGroup(FoodGroupKind.Diary, [Logged(Brand, 80, servings: 2), Logged(Milk, 250)])];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("o");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=food-result-3]").TextContent, Does.Contain("Acme Oat bar").And.Contain("2 servings (80 g) · 320 kcal"));
            Assert.That(cut.Find("[data-testid=food-result-2]").TextContent, Does.Contain("250 ml · 125 kcal"));
        });
    }

    [Test]
    public void NothingTyped_UsualRowRoundsHalfAwayFromZero_LikeTheAddSheet()
    {
        var half = new Food { Id = 7, Name = "Half", Unit = FoodUnit.Gram, Kcal = 125 };
        search.Idle = [new FoodGroup(FoodGroupKind.Usual, [Logged(half, 50)])];

        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        Assert.That(cut.Find("[data-testid=food-result-7]").TextContent, Does.Contain("63 kcal"));
    }

    [Test]
    public void TheDayShowsUnderTheTitle_TodayWhenNoneIsGiven()
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Dinner));

        Assert.That(cut.Find("[data-testid=screen-subtitle]").TextContent, Is.EqualTo("Today, 1 Jan"));
    }

    [Test]
    public void TheDayShowsUnderTheTitle_ForAnotherDay()
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Dinner).Add(x => x.Date, new DateOnly(1999, 12, 31)));

        Assert.That(cut.Find("[data-testid=screen-subtitle]").TextContent, Is.EqualTo("Yesterday, 31 Dec 1999"));
    }

    [Test]
    public async Task Typing_GroupOfSevenShowsFiveAndShowTwoMore_TappingItShowsTheRestInOrder()
    {
        var foods = Enumerable.Range(10, 7).Select(i => new FoodListing(new Food { Id = i, Name = $"Oat {i}", Unit = FoodUnit.Gram, Kcal = 100 }, null)).ToList();
        search.Groups = [new FoodGroup(FoodGroupKind.Common, foods), new FoodGroup(FoodGroupKind.Branded, [new FoodListing(Brand, null)])];
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        var before = Ids(cut, "[data-testid^=food-result-]");

        Assert.That(cut.Find("[data-testid=food-group-common-more]").TextContent, Does.Contain("Show 2 more"));
        Assert.That(cut.FindAll("[data-testid=food-group-branded-more]"), Is.Empty);
        await cut.Find("[data-testid=food-group-common-more]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo(new[] { "food-result-10", "food-result-11", "food-result-12", "food-result-13", "food-result-14", "food-result-3" }));
            Assert.That(Ids(cut, "[data-testid^=food-result-]"), Is.EqualTo(Enumerable.Range(10, 7).Select(i => $"food-result-{i}").Append("food-result-3")));
            Assert.That(cut.FindAll("[data-testid=food-group-common-more]"), Is.Empty);
        });
    }

    [Test]
    public async Task NothingTyped_MoreThanFiveUsualAndRecentFoods_ShowsAllOfThemWithNoShowMoreRow()
    {
        FoodListing[] Many(int first) => Enumerable.Range(first, 7).Select(i => Logged(new Food { Id = i, Name = $"Oat {i}", Unit = FoodUnit.Gram, Kcal = 100 }, 40)).ToArray();
        search.Idle = [new FoodGroup(FoodGroupKind.Usual, Many(10)), new FoodGroup(FoodGroupKind.Recent, Many(20))];

        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Breakfast));
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid^=food-result-]"), Has.Count.EqualTo(14));
            Assert.That(cut.FindAll("[data-testid=food-group-usual-more]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-group-recent-more]"), Is.Empty);
        });
    }

    [Test]
    public async Task Typing_GroupWithMoreMatchesThanFoods_ShowsHowManyTheRowReveals()
    {
        var foods = Enumerable.Range(10, 50).Select(i => new FoodListing(new Food { Id = i, Name = $"Oat {i}", Unit = FoodUnit.Gram, Kcal = 100 }, null)).ToList();
        search.Groups = [new FoodGroup(FoodGroupKind.Common, foods, 80)];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.That(cut.Find("[data-testid=food-group-common-more]").TextContent, Does.Contain("Show 45 more"));
    }

    [Test]
    public async Task Typing_ANewSearch_CollapsesAnExpandedGroup()
    {
        var foods = Enumerable.Range(10, 7).Select(i => new FoodListing(new Food { Id = i, Name = $"Oat {i}", Unit = FoodUnit.Gram, Kcal = 100 }, null)).ToList();
        search.Groups = [new FoodGroup(FoodGroupKind.Common, foods)];
        var cut = Context.Render<FoodSearchPage>();
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");
        await cut.Find("[data-testid=food-group-common-more]").ClickAsync(new());

        await cut.Find("[data-testid=food-search-input]").InputAsync("oats");

        Assert.That(cut.FindAll("[data-testid=food-group-common-more]"), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Typing_PassesTheMealToTheSearch()
    {
        search.Results = [Oats];
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Dinner));

        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        Assert.That(search.Meals, Is.All.EqualTo(Meal.Dinner));
    }

    [Test]
    public async Task Tapping_ALoggedFood_RaisesOnAddWithItsLastAmountAndServings()
    {
        search.Groups = [new FoodGroup(FoodGroupKind.Diary, [Logged(Brand, 80, servings: 2)]), new FoodGroup(FoodGroupKind.Common, [new FoodListing(Oats, null)])];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("o");

        await cut.Find("[data-testid=food-result-3]").ClickAsync(new());

        Assert.That(requests, Is.EqualTo(new[] { new FoodAddRequest(Brand, "o", 80, 2) }));
    }

    [Test]
    public async Task Tapping_ALoggedFood_CarriesTheServingItWasLoggedWith()
    {
        search.Groups = [new FoodGroup(FoodGroupKind.Diary, [Logged(Brand, 660, servings: 2, servingId: 12)])];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("o");

        await cut.Find("[data-testid=food-result-3]").ClickAsync(new());

        Assert.That(requests, Is.EqualTo(new[] { new FoodAddRequest(Brand, "o", 660, 2, 12) }));
    }

    [Test]
    public async Task Tapping_ANeverLoggedFood_RaisesOnAddWithNoAmount()
    {
        search.Groups = [new FoodGroup(FoodGroupKind.Common, [new FoodListing(Oats, null)])];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("o");

        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        Assert.That(requests.Single(), Has.Property(nameof(FoodAddRequest.LastAmount)).Null.And.Property(nameof(FoodAddRequest.LastServings)).Null.And.Property(nameof(FoodAddRequest.LastServingId)).Null);
    }

    [Test]
    public async Task Page_LogsNothingAboutWhatWasTypedOrLogged()
    {
        search.Groups = [new FoodGroup(FoodGroupKind.Diary, [Logged(Brand, 80)])];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").InputAsync("secretquery");

        Assert.That(LoggedEvents.Of(logger), Is.Empty);
    }

    [Test]
    public async Task ATappedResult_RaisesOnAddAtOnce_WithNoValuesStep()
    {
        search.Results = [Oats];
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("oat");

        await cut.Find("[data-testid=food-result-1]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(requests, Is.EqualTo(new[] { new FoodAddRequest(Oats, "oat") }));
            Assert.That(cut.FindAll("[data-testid=food-detail]"), Is.Empty);
        });
    }

    [Test]
    public async Task ADoubleTapOnAResult_AddsOnce()
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
    public void TitlesTheMeal_HasABackArrowAndNoNavigationBar()
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
    public void OpeningWithAQuery_RestoresItAndItsResultsRightAway()
    {
        search.Results = [Oats];

        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.InitialQuery, "oat"));

        Assert.Multiple(() =>
        {
            Assert.That(search.Queries, Is.EqualTo(new[] { "oat" }));
            Assert.That(cut.Find("[data-testid=food-search-input]").GetAttribute("value"), Is.EqualTo("oat"));
            Assert.That(cut.FindAll("[data-testid=food-result-1]"), Has.Count.EqualTo(1));
        });
    }

    private sealed class FakeSearch : IFoodSearch
    {
        public List<string> Queries { get; } = [];

        public IReadOnlyList<Food> Results { get; set; } = [];

        public bool Failing { get; set; }

        public Dictionary<string, TaskCompletionSource<FoodSearchResult>> Held { get; } = [];

        /// <summary>Set to show these groups instead of <see cref="Results"/> as one Common group.</summary>
        public IReadOnlyList<FoodGroup>? Groups { get; set; }

        public IReadOnlyList<FoodGroup> Idle { get; set; } = [];

        public bool IdleFailing { get; set; }

        public List<Meal?> Meals { get; } = [];

        public int Suggested { get; private set; }

        public Dictionary<int, CalorieTracker.Core.History.FoodHistory> Last { get; } = [];

        public Task<CalorieTracker.Core.History.FoodHistory?> LastLoggedAsync(int foodId, Meal? meal = null) => Task.FromResult(Last.GetValueOrDefault(foodId));

        public Task<FoodSearchResult> SearchAsync(string query, int limit = IFoodSearch.DefaultLimit, CancellationToken ct = default) => throw new NotSupportedException();

        public async Task<FoodGroupsResult> SearchGroupedAsync(string query, Meal? meal = null, CancellationToken ct = default)
        {
            Queries.Add(query);
            Meals.Add(meal);
            if (Held.TryGetValue(query, out var held))
            {
                return (await held.Task).Status == FoodSearchStatus.Failed ? FoodGroupsResult.Failed() : FoodGroupsResult.Found([]);
            }

            return Failing
                ? FoodGroupsResult.Failed()
                : FoodGroupsResult.Found(Groups ?? [new FoodGroup(FoodGroupKind.Common, Results.Select(f => new FoodListing(f, null)).ToList())]);
        }

        public Task<FoodGroupsResult> SuggestAsync(Meal? meal = null, CancellationToken ct = default)
        {
            Suggested++;
            Meals.Add(meal);
            return Task.FromResult(IdleFailing ? FoodGroupsResult.Failed() : FoodGroupsResult.Found(Idle));
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
