using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Seed;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace CalorieTracker.UI.Tests.Screens;

/// <summary>The online part of the search page: nothing is sent until a tap, and a tap on a result stores the product and carries its pack.</summary>
public class FoodSearchOnlinePageTests : ComponentTestBase
{
    private static readonly Food Stored = new() { Id = 7, Name = "Coca Cola", Brand = "Coca-Cola", Unit = FoodUnit.Millilitre, Kcal = 42, ProteinG = 0, CarbsG = 10.6, FatG = 0 };

    private FakeOnlineSearch online = null!;
    private FakeOnlineImport import = null!;
    private FakeOnlineNotice notice = null!;
    private FakeFoodSearch local = null!;

    [SetUp]
    public void SetUp()
    {
        online = new FakeOnlineSearch();
        import = new FakeOnlineImport();
        notice = new FakeOnlineNotice();
        local = new FakeFoodSearch();
        Context.Services.AddSingleton<IFoodSearch>(local);
        Context.Services.AddSingleton<IOnlineFoodSearch>(online);
        Context.Services.AddSingleton<IOnlineFoodImport>(import);
        Context.Services.AddSingleton<IOnlineSearchNotice>(notice);
        Context.Services.AddSingleton(new SeedProgress(new NeverLoader()));
        Context.Services.AddSingleton<ILogger<FoodSearchPage>>(new FakeLogger<FoodSearchPage>());
    }

    private async Task<IRenderedComponent<FoodSearchPage>> Typed(string text, Meal meal = Meal.Lunch)
    {
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, meal));
        await cut.Find("[data-testid=food-search-input]").InputAsync(text);
        return cut;
    }

    private static OnlineSearchResult Found(params OnlineProduct[] products) => OnlineSearchResult.Found(products, "United Kingdom");

    [Test]
    public async Task Typing_SendsNothing_AndShowsTheButtonAndTheFirstUseNotice()
    {
        var cut = await Typed("coca-cola");

        Assert.Multiple(() =>
        {
            Assert.That(online.Queries, Is.Empty);
            Assert.That(import.Barcodes, Is.Empty);
            Assert.That(cut.Find("button[data-testid=food-search-online]").TextContent, Does.Contain("Search online"));
            Assert.That(cut.Find("[data-testid=food-search-online-notice]").TextContent, Is.EqualTo("This sends what you typed to an online database."));
        });
    }

    [Test]
    public void LoadingThePage_SendsNothing_AndShowsNoRowBeforeTyping()
    {
        var cut = Context.Render<FoodSearchPage>();

        Assert.Multiple(() =>
        {
            Assert.That(online.Queries, Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online]"), Is.Empty);
        });
    }

    [Test]
    public async Task LocalResults_ShowSearchOnlineAsASecondaryButton()
    {
        local.Groups = [new FoodGroup(FoodGroupKind.Branded, [new FoodListing(Stored, null)])];

        var cut = await Typed("coca-cola");

        Assert.That(cut.Find("button[data-testid=food-search-online]").ClassList, Does.Contain("secondary"));
    }

    [Test]
    public async Task NothingMatchingLocally_OffersAPrimaryButton()
    {
        var cut = await Typed("zzz");

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=food-search-none-message]").TextContent, Is.EqualTo("No foods match"));
            Assert.That(cut.Find("button[data-testid=food-search-online]").ClassList, Does.Contain("primary"));
        });
    }

    [Test]
    public async Task TappingTheRow_SendsTheTypedText_Once_ShowsTheResultsAndMarksTheNoticeSeen()
    {
        online.Answer(Found(OnlineSamples.Can, OnlineSamples.Product("5000112641981", "Coca Cola", "1.5L", isNew: false)));
        var cut = await Typed("coca-cola");

        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(online.Queries, Is.EqualTo(new[] { "coca-cola" }));
            Assert.That(notice.Marked, Is.EqualTo(1));
            Assert.That(cut.Find("[data-testid=food-group-online]").TextContent, Is.EqualTo("Online · United Kingdom"));
            Assert.That(cut.Find("[data-testid=food-online-5000112637922]").TextContent, Does.Contain("Coca Cola").And.Contain("330 ml").And.Contain("42 kcal / 100 ml").And.Contain("NEW"));
            Assert.That(cut.Find("[data-testid=food-online-5000112641981]").TextContent, Does.Not.Contain("NEW"));
            Assert.That(cut.FindAll("[data-testid=food-search-online]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online-notice]"), Is.Empty);
        });
    }

    [Test]
    public async Task TheNotice_ShowsOnce_NotAgainOnceSeen()
    {
        notice.Shown = true;

        var cut = await Typed("coca-cola");

        Assert.That(cut.FindAll("[data-testid=food-search-online-notice]"), Is.Empty);
    }

    [Test]
    public async Task TheEnterKey_IsTheSameTapAsTheRow()
    {
        online.Answer(Found(OnlineSamples.Can));
        var cut = await Typed("coca-cola");

        await cut.Find("[data-testid=food-search-input]").KeyDownAsync(Key.Enter);

        Assert.Multiple(() =>
        {
            Assert.That(online.Queries, Is.EqualTo(new[] { "coca-cola" }));
            Assert.That(notice.Marked, Is.EqualTo(1), "Enter is the first tap too: the notice counts as seen");
        });
    }

    [Test]
    public async Task AnotherKey_SendsNothing()
    {
        var cut = await Typed("coca-cola");

        await cut.Find("[data-testid=food-search-input]").KeyDownAsync(Key.Tab);

        Assert.That(online.Queries, Is.Empty);
    }

    [Test]
    public async Task WhileSearching_ASpinnerShows_AndAnotherTapSendsNothingMore()
    {
        var held = online.Hold();
        var cut = await Typed("coca-cola");
        var tap = cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await WaitForRendererAsync();

        var spinner = cut.Find("[data-testid=food-search-online-loading]").TextContent;
        await cut.Find("[data-testid=food-search-input]").KeyDownAsync(Key.Enter);
        held.SetResult(Found(OnlineSamples.Can));
        await tap;

        Assert.Multiple(() =>
        {
            Assert.That(spinner, Does.Contain("Searching online…"));
            Assert.That(online.Queries, Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=food-online-5000112637922]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Typing_AfterTheResults_ClearsThem_AndTheSameTextAskedAgainIsTheServicesCacheToServe()
    {
        online.Answer(Found(OnlineSamples.Can));
        var cut = await Typed("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola zero");
        var cleared = cut.FindAll("[data-testid=food-group-online]").Count;
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(cleared, Is.Zero);
            Assert.That(online.Queries, Is.EqualTo(new[] { "coca-cola", "coca-cola zero" }));
        });
    }

    [Test]
    public async Task AnAnswerForTextThatChangedMeanwhile_IsDropped()
    {
        var held = online.Hold();
        var cut = await Typed("coca");
        var tap = cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await WaitForRendererAsync();

        await cut.Find("[data-testid=food-search-input]").InputAsync("cola");
        held.SetResult(Found(OnlineSamples.Can));
        await tap;

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-group-online]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task MoreThanFiveProducts_ShowFiveAndShowNMore()
    {
        online.Answer(OnlineSearchResult.Found(Enumerable.Range(0, 8).Select(i => OnlineSamples.Product($"50001126379{i:00}", $"Cola {i}", "330 ml")).ToList(), null));
        var cut = await Typed("cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        var before = cut.FindAll("[data-testid^=food-online-]").Count;
        var heading = cut.Find("[data-testid=food-group-online]").TextContent;
        var more = cut.Find("[data-testid=food-group-online-more]").TextContent.Trim();
        await cut.Find("[data-testid=food-group-online-more]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That((before, more, heading), Is.EqualTo((5, "Show 3 more", "Online · worldwide")));
            Assert.That(cut.FindAll("[data-testid^=food-online-]"), Has.Count.EqualTo(8));
            Assert.That(cut.FindAll("[data-testid=food-group-online-more]"), Is.Empty);
        });
    }

    [TestCase(OnlineSearchStatus.Limited, "The online search is busy. Try again in a minute.")]
    [TestCase(OnlineSearchStatus.Offline, "You are offline")]
    [TestCase(OnlineSearchStatus.Failed, "Couldn't reach the online search")]
    public async Task AFailedSearch_ShowsItsMessageAndTryAgain_WithTheLocalGroupsStillThere(OnlineSearchStatus status, string message)
    {
        local.Groups = [new FoodGroup(FoodGroupKind.Branded, [new FoodListing(Stored, null)])];
        online.Answer(status switch
        {
            OnlineSearchStatus.Limited => OnlineSearchResult.Limited(),
            OnlineSearchStatus.Offline => OnlineSearchResult.Offline(),
            _ => OnlineSearchResult.Failed(),
        });
        online.Answer(Found(OnlineSamples.Can));
        var cut = await Typed("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        var shown = cut.Find("[data-testid=food-search-online-error]").TextContent;
        var localRows = cut.FindAll("[data-testid=food-result-7]").Count;
        await cut.Find("[data-testid=food-search-online-retry]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo(message));
            Assert.That(localRows, Is.EqualTo(1));
            Assert.That(online.Queries, Is.EqualTo(new[] { "coca-cola", "coca-cola" }));
            Assert.That(cut.FindAll("[data-testid=food-online-5000112637922]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=food-search-online-error]"), Is.Empty);
        });
    }

    [Test]
    public async Task ASearchWithNothingUsable_SaysSo()
    {
        online.Answer(OnlineSearchResult.None());
        var cut = await Typed("zzz");

        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        Assert.That(cut.Find("[data-testid=food-search-online-none]").TextContent, Is.EqualTo("Nothing found online"));
    }

    [Test]
    public async Task InAddMode_AnOnlineFoodLoggedBefore_RaisesOnAddWithItsLastServing_LookedUpForTheMeal()
    {
        online.Answer(Found(OnlineSamples.Can));
        import.Answer(new OnlineImportResult(OnlineImportStatus.Imported, Stored));
        local.Last[7] = new CalorieTracker.Core.History.FoodHistory(7, 2, 1, new DateOnly(2031, 7, 19), 660, 2, 31);
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        await cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(requests.Select(r => (r.LastAmount, r.LastServings, r.LastServingId)), Is.EqualTo(new[] { ((double?)660, (double?)2, (int?)31) }));
            Assert.That(local.LastMeals, Is.EqualTo(new Meal?[] { Meal.Lunch }));
        });
    }

    [Test]
    public async Task InAddMode_AnOnlineFoodNeverLogged_RaisesOnAddWithNoLastServing()
    {
        online.Answer(Found(OnlineSamples.Can));
        import.Answer(new OnlineImportResult(OnlineImportStatus.Imported, Stored));
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        await cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());

        Assert.That(requests.Select(r => (r.LastAmount, r.LastServings, r.LastServingId)), Is.EqualTo(new[] { ((double?)null, (double?)null, (int?)null) }));
    }

    [Test]
    public async Task InAddMode_TappingAResult_StoresItAndRaisesOnAddWithTheFood_Once()
    {
        online.Answer(Found(OnlineSamples.Can));
        var held = import.Hold();
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        var tap = cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());
        await WaitForRendererAsync();
        var fetching = cut.Find("[data-testid=food-search-online-importing]").TextContent;
        var rowsDuringFetch = cut.FindAll("[data-testid=food-online-5000112637922]").Count;
        held.SetResult(new OnlineImportResult(OnlineImportStatus.Imported, Stored));
        await tap;
        await cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(fetching, Does.Contain("Getting the product…"));
            Assert.That(rowsDuringFetch, Is.EqualTo(1));
            Assert.That(import.Barcodes, Has.Count.EqualTo(1));
            Assert.That(requests.Select(r => (r.Food.Id, r.Query)), Is.EqualTo(new[] { (7, "coca-cola") }));
        });
    }

    [TestCase(OnlineImportStatus.Limited, "The online search is busy. Try again in a minute.")]
    [TestCase(OnlineImportStatus.Offline, "You are offline")]
    [TestCase(OnlineImportStatus.Failed, "Couldn't reach the online search")]
    public async Task AProductThatCannotBeFetched_ShowsTheMessageUnderTheResults_AndTryAgainFetchesItAgain(OnlineImportStatus status, string message)
    {
        online.Answer(Found(OnlineSamples.Can));
        import.Answer(new OnlineImportResult(status));
        import.Answer(new OnlineImportResult(OnlineImportStatus.Imported, Stored));
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());

        var shown = cut.Find("[data-testid=food-search-online-error]").TextContent;
        var kept = cut.FindAll("[data-testid=food-online-5000112637922]").Count;
        await cut.Find("[data-testid=food-search-online-retry]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo(message));
            Assert.That(kept, Is.EqualTo(1));
            Assert.That(import.Barcodes, Is.EqualTo(new[] { "5000112637922", "5000112637922" }));
            Assert.That(requests.Select(r => r.Food.Id), Is.EqualTo(new[] { 7 }));
        });
    }

    [Test]
    public async Task AProductWithNoUsableData_SaysItCannotBeAdded_WithNoTryAgain_AndTheOtherProductsStayTappable()
    {
        online.Answer(Found(OnlineSamples.Can, OnlineSamples.Bottle));
        import.Answer(new OnlineImportResult(OnlineImportStatus.NotFound));
        import.Answer(new OnlineImportResult(OnlineImportStatus.Imported, Stored));
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());

        var shown = cut.Find("[data-testid=food-search-online-unavailable]").TextContent;
        var tryAgain = cut.FindAll("[data-testid=food-search-online-retry]").Count;
        var errors = cut.FindAll("[data-testid=food-search-online-error]").Count;
        var kept = cut.FindAll("[data-testid^=food-online-]").Count;
        await cut.Find("[data-testid=food-online-5000112637939]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(shown, Is.EqualTo("That product can't be added"));
            Assert.That((tryAgain, errors, kept), Is.EqualTo((0, 0, 2)));
            Assert.That(import.Barcodes, Is.EqualTo(new[] { "5000112637922", "5000112637939" }));
            Assert.That(requests.Select(r => r.Food.Id), Is.EqualTo(new[] { 7 }));
        });
    }

    [Test]
    public async Task TryAgainAfterAFailedFetch_ThenNewText_SearchesAgainInsteadOfFetching()
    {
        online.Answer(Found(OnlineSamples.Can));
        online.Answer(OnlineSearchResult.Failed());
        import.Answer(new OnlineImportResult(OnlineImportStatus.Limited));
        var cut = await Typed("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());

        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola zero");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await cut.Find("[data-testid=food-search-online-retry]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(import.Barcodes, Has.Count.EqualTo(1));
            Assert.That(online.Queries, Is.EqualTo(new[] { "coca-cola", "coca-cola zero", "coca-cola zero" }));
        });
    }

    [Test]
    public async Task ASecondSearch_AfterExpandingTheFirst_StartsCollapsed()
    {
        online.Answer(OnlineSearchResult.Found(Enumerable.Range(0, 8).Select(i => OnlineSamples.Product($"50001126379{i:00}", $"Cola {i}", "330 ml")).ToList(), null));
        var cut = await Typed("cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await cut.Find("[data-testid=food-group-online-more]").ClickAsync(new());

        await cut.Find("[data-testid=food-search-input]").InputAsync("cola zero");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid^=food-online-5]"), Has.Count.EqualTo(5));
            Assert.That(cut.Find("[data-testid=food-group-online-more]").TextContent.Trim(), Is.EqualTo("Show 3 more"));
        });
    }

    [Test]
    public async Task TypingWhileTheNoticeIsBeingMarked_DoesNotThrow_AndShowsNoNoticeAgain()
    {
        var held = new TaskCompletionSource();
        notice.HoldMark = held;
        var cut = await Typed("coca-cola");
        var tap = cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        await WaitForRendererAsync();

        await cut.Find("[data-testid=food-search-input]").InputAsync("cola");
        held.SetResult();
        await tap;

        Assert.Multiple(() =>
        {
            Assert.That(online.Queries, Is.Empty.Or.EqualTo(new[] { "coca-cola" }));
            Assert.That(cut.FindAll("[data-testid=food-group-online]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=food-search-online-notice]"), Is.Empty);
        });
    }

    [Test]
    public async Task TypingNewText_AfterTheFirstTap_DoesNotBringTheNoticeBack()
    {
        online.Answer(Found(OnlineSamples.Can));
        var cut = await Typed("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        await cut.Find("[data-testid=food-search-input]").InputAsync("cola zero");

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-search-online]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=food-search-online-notice]"), Is.Empty);
        });
    }

    [Test]
    public async Task WithNothingTyped_NoRowShows_EvenWithSuggestedFoods_AndEnterSendsNothing()
    {
        local.Suggestions = [new FoodGroup(FoodGroupKind.Recent, [new FoodListing(Stored, null)])];
        var cut = Context.Render<FoodSearchPage>();

        await cut.Find("[data-testid=food-search-input]").KeyDownAsync(Key.Enter);
        var blank = cut.Find("[data-testid=food-search-input]").InputAsync("   ");
        await blank;
        await cut.Find("[data-testid=food-search-input]").KeyDownAsync(Key.Enter);

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=food-result-7]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=food-search-online]"), Is.Empty);
            Assert.That(online.Queries, Is.Empty);
        });
    }

    [Test]
    public void Enter_WhileFoodsAreLoading_SendsNothing()
    {
        var taps = 0;
        var cut = Context.Render<FoodSearchView>(p => p
            .Add(x => x.Query, "oat")
            .Add(x => x.Loading, true)
            .Add(x => x.OnSearchOnline, () => taps++));

        cut.Find("[data-testid=food-search-input]").KeyDown(Key.Enter);

        Assert.That(taps, Is.Zero);
    }

    [Test]
    public void TheSearchBox_OffersTheSearchKeyOnTheKeyboard()
    {
        var cut = Context.Render<FoodSearchView>();

        Assert.That(cut.Find("[data-testid=food-search-input]").GetAttribute("enterkeyhint"), Is.EqualTo("search"));
    }

    [Test]
    public async Task TheNoticeRead_DoesNotHoldBackTheFirstSearch()
    {
        var held = new TaskCompletionSource();
        notice.HoldRead = held;

        var cut = Context.Render<FoodSearchPage>();
        var suggestedWhileHeld = local.Suggested;
        held.SetResult();
        await WaitForRendererAsync();
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");

        Assert.Multiple(() =>
        {
            Assert.That(suggestedWhileHeld, Is.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=food-search-online-notice]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task TypingDuringAFetch_InAddMode_AddsNothing()
    {
        online.Answer(Found(OnlineSamples.Can));
        var held = import.Hold();
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        var tap = cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());
        await WaitForRendererAsync();

        await cut.Find("[data-testid=food-search-input]").InputAsync("cola");
        held.SetResult(new OnlineImportResult(OnlineImportStatus.Imported, Stored));
        await tap;

        Assert.That(requests, Is.Empty);
    }

    [Test]
    public async Task AStoredFoodTappedDuringAFetch_IsIgnored()
    {
        online.Answer(Found(OnlineSamples.Can));
        local.Groups = [new FoodGroup(FoodGroupKind.Branded, [new FoodListing(Stored, null)])];
        var held = import.Hold();
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());
        var tap = cut.Find("[data-testid=food-online-5000112637922]").ClickAsync(new());
        await WaitForRendererAsync();

        await cut.Find("[data-testid=food-result-7]").ClickAsync(new());
        var duringFetch = requests.Count;
        held.SetResult(new OnlineImportResult(OnlineImportStatus.Imported, Stored));
        await tap;

        Assert.Multiple(() =>
        {
            Assert.That(duringFetch, Is.Zero);
            Assert.That(requests, Has.Count.EqualTo(1));
        });
    }

    [TestCase(OnlineSearchStatus.Limited)]
    [TestCase(OnlineSearchStatus.Offline)]
    public async Task AfterABusyOrOfflineSearch_AStoredFoodCanStillBeAdded(OnlineSearchStatus status)
    {
        local.Groups = [new FoodGroup(FoodGroupKind.Branded, [new FoodListing(Stored, null)])];
        online.Answer(status == OnlineSearchStatus.Limited ? OnlineSearchResult.Limited() : OnlineSearchResult.Offline());
        var requests = new List<FoodAddRequest>();
        var cut = Context.Render<FoodSearchPage>(p => p.Add(x => x.Meal, Meal.Lunch).Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=food-search-input]").InputAsync("coca-cola");
        await cut.Find("[data-testid=food-search-online]").ClickAsync(new());

        await cut.Find("[data-testid=food-result-7]").ClickAsync(new());

        Assert.That(requests.Select(r => r.Food.Id), Is.EqualTo(new[] { 7 }));
    }

    private sealed class NeverLoader : ISeedLoader
    {
        public Task<SeedLoadResult> LoadAsync() => new TaskCompletionSource<SeedLoadResult>().Task;
    }

    /// <summary>Local search: no foods unless <see cref="Groups"/> is set.</summary>
    private sealed class FakeFoodSearch : IFoodSearch
    {
        public IReadOnlyList<FoodGroup> Groups { get; set; } = [];

        public IReadOnlyList<FoodGroup> Suggestions { get; set; } = [];

        public int Suggested { get; private set; }

        public Dictionary<int, CalorieTracker.Core.History.FoodHistory> Last { get; } = [];

        public List<Meal?> LastMeals { get; } = [];

        public Task<CalorieTracker.Core.History.FoodHistory?> LastLoggedAsync(int foodId, Meal? meal = null)
        {
            LastMeals.Add(meal);
            return Task.FromResult(Last.GetValueOrDefault(foodId));
        }

        public Task<FoodSearchResult> SearchAsync(string query, int limit = IFoodSearch.DefaultLimit, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<FoodGroupsResult> SearchGroupedAsync(string query, Meal? meal = null, CancellationToken ct = default) => Task.FromResult(FoodGroupsResult.Found(Groups));

        public Task<FoodGroupsResult> SuggestAsync(Meal? meal = null, CancellationToken ct = default)
        {
            Suggested++;
            return Task.FromResult(FoodGroupsResult.Found(Suggestions));
        }
    }
}
