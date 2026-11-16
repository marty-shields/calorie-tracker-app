using CalorieTracker.Core;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Summary;
using CalorieTracker.Core.Targets;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.UI.Tests.Screens;

/// <summary>The page over Core's real services and a temporary database, since they have no interfaces to fake.</summary>
public class DashboardPageTests : ComponentTestBase
{
    private static readonly DateOnly Today = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private ServiceProvider core = null!;
    private int cheddarId;
    private int sliceId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"calorietracker-ui-{Guid.NewGuid():N}.db3");
        core = new ServiceCollection()
            .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>))
            .AddCore(databasePath)
            .BuildServiceProvider();
        await core.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        var cheddar = new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "cheddar",
            Name = "Cheddar cheese",
            Type = FoodType.Ingredient,
            Unit = FoodUnit.Gram,
            Kcal = 416,
            ProteinG = 25.4,
            CarbsG = 0.1,
            FatG = 34.9,
            ServingLabel = "1 slice (20 g)",
            ServingAmount = 20,
            SearchText = "cheddar cheese",
        };
        await connection.InsertAsync(cheddar);
        cheddarId = cheddar.Id;
        var slice = new Serving { FoodId = cheddarId, Label = "1 slice (20 g)", Amount = 20, Source = ServingSource.Product, SortOrder = 0 };
        await connection.InsertAsync(slice);
        sliceId = slice.Id;
        Context.Services.AddSingleton(core.GetRequiredService<DaySummaryService>());
        Context.Services.AddSingleton(core.GetRequiredService<DiaryService>());
        Context.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2031, 7, 19, 12, 40, 0, TimeSpan.Zero)));
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        await core.DisposeAsync();
        File.Delete(databasePath);
    }

    private Task Log(DateOnly day, Meal meal, double grams, double? servings = null, int? servingId = null) =>
        core.GetRequiredService<DiaryService>().AddAsync(day, meal, cheddarId, grams, servings, servingId);

    private IRenderedComponent<DashboardPage> Render(DateOnly? date = null, Action<ComponentParameterCollectionBuilder<DashboardPage>>? more = null)
    {
        var cut = Context.Render<DashboardPage>(p =>
        {
            p.Add(x => x.Date, date);
            more?.Invoke(p);
        });
        cut.WaitForState(() => cut.FindAll("[data-testid=dashboard-loading]").Count == 0);
        return cut;
    }

    private static string Text(IRenderedComponent<DashboardPage> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static bool Exists(IRenderedComponent<DashboardPage> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    [Test]
    public void FirstRun_ShowsTheEmptyDayAndNoTarget()
    {
        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Today, 19 Jul"));
            Assert.That(Text(cut, "dashboard-empty-message"), Is.EqualTo("Nothing logged today"));
            Assert.That(Text(cut, "dashboard-ring-label"), Is.EqualTo("kcal eaten"));
            Assert.That(Text(cut, "dashboard-no-target"), Is.EqualTo("No target set"));
        });
    }

    [Test]
    public async Task ALoggedEntry_ShowsUnderItsMealAndInEveryTotal()
    {
        await Log(Today, Meal.Lunch, 30);
        await core.GetRequiredService<TargetService>().SaveAsync(2100);

        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "meal-lunch-calories"), Is.EqualTo("125 kcal"));
            Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("125"));
            Assert.That(Text(cut, "dashboard-ring-value"), Is.EqualTo("1,975"));
            Assert.That(Text(cut, "dashboard-ring-label"), Is.EqualTo("kcal left"));
            Assert.That(cut.FindAll("[data-testid=meal-lunch] ~ ul .row"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=meal-breakfast] ~ ul .row"), Is.Empty);
            Assert.That(Exists(cut, "dashboard-empty"), Is.False);
            Assert.That(Text(cut, "dashboard-share-protein"), Does.Contain("7.6 g"));
        });
    }

    [Test]
    public async Task AnEntryAddedByServings_NamesTheServingItWasAddedBy()
    {
        await Log(Today, Meal.Snacks, 40, 2, sliceId);

        var cut = Render();

        Assert.That(cut.Find("[data-testid^=entry-]").TextContent, Does.Contain("2 servings · 1 slice (20 g)"));
    }

    [Test]
    public async Task AnEntryAddedByServingsBeforeServingsHadATable_ShowsTheAmountInstead()
    {
        await Log(Today, Meal.Snacks, 40, 2);

        var cut = Render();

        Assert.That(cut.Find("[data-testid^=entry-]").TextContent, Does.Contain("2 servings · 40 g").And.Not.Contain("slice"));
    }

    [Test]
    public async Task AnEntryRemovedElsewhere_LeavesTheDayAndEveryTotal_AsUndoDoes()
    {
        await Log(Today, Meal.Lunch, 30);
        var added = await core.GetRequiredService<DiaryService>().AddAsync(Today, Meal.Lunch, cheddarId, 30);
        var cut = Render();
        Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("250"));

        await core.GetRequiredService<DiaryService>().RemoveAsync(added.Entry!.Id);

        await cut.WaitForStateAsync(() => Text(cut, "dashboard-eaten") == "125", TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("125"));
            Assert.That(Text(cut, "meal-lunch-calories"), Is.EqualTo("125 kcal"));
            Assert.That(cut.FindAll("[data-testid=meal-lunch] ~ ul .row"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task OverTheTarget_ShowsHowManyOver()
    {
        await Log(Today, Meal.Dinner, 100);
        await core.GetRequiredService<TargetService>().SaveAsync(400);

        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "dashboard-ring-value"), Is.EqualTo("16"));
            Assert.That(Text(cut, "dashboard-ring-label"), Is.EqualTo("kcal over"));
        });
    }

    [Test]
    public async Task Next_ShowsTomorrowAndPreviousComesBack_TodayReturnsFromAnyDay()
    {
        await Log(Today.AddDays(1), Meal.Breakfast, 50);
        var cut = Render();

        await cut.Find("[data-testid=date-strip-next]").ClickAsync();
        cut.WaitForAssertion(() => Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("208")));
        Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Tomorrow, 20 Jul"));

        await cut.Find("[data-testid=date-strip-previous]").ClickAsync();
        cut.WaitForAssertion(() => Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("0")));
        await cut.Find("[data-testid=date-strip-previous]").ClickAsync();
        cut.WaitForAssertion(() => Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Yesterday")));

        await cut.Find("[data-testid=date-strip-label]").ClickAsync();
        cut.WaitForAssertion(() => Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Today, 19 Jul")));
    }

    [Test]
    public async Task ADateParameter_OpensThatDay_AndANewValueOpensAnother()
    {
        await Log(new DateOnly(2031, 7, 1), Meal.Lunch, 100);
        var cut = Render(new DateOnly(2031, 7, 1));

        Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Tue, 1 Jul"));
        Assert.That(Text(cut, "dashboard-eaten"), Is.EqualTo("416"));

        cut.Render(p => p.Add(x => x.Date, Today));
        cut.WaitForAssertion(() => Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Today")));
    }

    [Test]
    public async Task ReRenderingWithTheSameDate_KeepsTheDayThePersonMovedTo()
    {
        var cut = Render();

        await cut.Find("[data-testid=date-strip-next]").ClickAsync();
        cut.WaitForAssertion(() => Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Tomorrow")));
        cut.Render(p => p.Add(x => x.OnSettings, () => { }));

        Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Tomorrow"));
    }

    [Test]
    public async Task MealPlus_RaisesTheMealAndTheDayOnTheStrip()
    {
        var requests = new List<MealAddRequest>();
        var cut = Render(more: p => p.Add(x => x.OnAdd, requests.Add));
        await cut.Find("[data-testid=date-strip-next]").ClickAsync();
        cut.WaitForAssertion(() => Assert.That(Text(cut, "date-strip-label"), Does.StartWith("Tomorrow")));

        await cut.Find("[data-testid=meal-lunch-add]").ClickAsync();

        Assert.That(requests, Is.EqualTo(new[] { new MealAddRequest(Meal.Lunch, Today.AddDays(1)) }));
        Assert.That(FoodSearchRoute.AddUrl(requests[0].Meal, requests[0].Date), Is.EqualTo("/foods?meal=Lunch&date=2031-07-20"));
    }

    [Test]
    public async Task ADayThatCannotBeRead_ShowsLoadFailed_AndTryAgainReadsItAgain()
    {
        await Log(Today, Meal.Lunch, 30);
        await connection.ExecuteAsync("ALTER TABLE Food RENAME TO FoodAway");

        var cut = Render();

        Assert.That(Text(cut, "dashboard-load-failed-error"), Is.EqualTo("Couldn't load this day"));

        await connection.ExecuteAsync("ALTER TABLE FoodAway RENAME TO Food");
        await cut.Find("[data-testid=dashboard-load-failed-retry]").ClickAsync();
        cut.WaitForAssertion(() => Assert.That(Text(cut, "meal-lunch-calories"), Is.EqualTo("125 kcal")));
    }

    [Test]
    public async Task AnEntryWhoseFoodIsGone_ShowsLoadFailed()
    {
        await Log(Today, Meal.Lunch, 30);
        await connection.ExecuteAsync("DELETE FROM Food WHERE Id = ?", cheddarId);

        var cut = Render();

        Assert.That(Exists(cut, "dashboard-load-failed-error"), Is.True);
    }

    [Test]
    public async Task Buttons_RaiseTheirCallbacks()
    {
        var navigated = new List<NavDestination>();
        var settings = 0;
        var cut = Render(more: p => p
            .Add(x => x.OnNavigate, navigated.Add)
            .Add(x => x.OnSettings, () => settings++));

        await cut.Find("[data-testid=dashboard-link]").ClickAsync();
        await cut.Find("[data-testid=settings]").ClickAsync();
        await cut.Find("[data-testid=scan-link]").ClickAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Scan }));
            Assert.That(settings, Is.EqualTo(1));
        });
    }

    [Test]
    public void AndroidBack_HasNoHandler()
    {
        Render();

        Assert.That(BackButton.HasHandler, Is.False);
    }
}
