using Bunit.TestDoubles;
using CalorieTracker.Core;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Trends;
using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.UI.Tests.Screens;

/// <summary>The page over Core's real services and a temporary database. Today is Saturday 19 Jul 2031, so the current week is 14 to 20 Jul.</summary>
public class TrendsPageTests : ComponentTestBase
{
    private static readonly DateOnly Today = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private ServiceProvider core = null!;
    private FakeLogger<TrendsPage> logger = null!;
    private Food rice = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"calorietracker-ui-{Guid.NewGuid():N}.db3");
        core = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(databasePath)
            .BuildServiceProvider();
        await core.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.InsertOrReplaceAsync(new TargetHistory { EffectiveFrom = "0001-01-01", Kcal = 2000 });
        rice = new Food { Source = FoodSource.OpenFoodFacts, SourceId = "rice", Name = "Secret rice", Type = FoodType.Ingredient, Unit = FoodUnit.Gram, Kcal = 100, ProteinG = 2, CarbsG = 20, FatG = 1, SearchText = "secret rice" };
        await connection.InsertAsync(rice);
        Context.JSInterop.SetupModule("./_content/CalorieTracker.UI/Components/Layout/AddSheet.razor.js").Mode = JSRuntimeMode.Loose;
        logger = new FakeLogger<TrendsPage>();
        Context.Services.AddSingleton<ILogger<TrendsPage>>(logger);
        foreach (var service in new object[] { core.GetRequiredService<TrendsService>(), core.GetRequiredService<DiaryService>(), core.GetRequiredService<FoodHistoryService>(), core.GetRequiredService<FoodService>() })
        {
            Context.Services.AddSingleton(service.GetType(), service);
        }

        ((FakeTimeProvider)Context.Services.GetRequiredService<TimeProvider>()).SetUtcNow(new DateTimeOffset(2031, 7, 19, 18, 30, 0, TimeSpan.Zero));
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        await core.DisposeAsync();
        File.Delete(databasePath);
    }

    private Task<DiaryResult> Log(DateOnly day, double grams, Meal meal = Meal.Dinner) =>
        core.GetRequiredService<DiaryService>().AddAsync(day, meal, rice.Id, grams, null, null);

    private async Task LogThreeDaysOfLastWeek()
    {
        await Log(new DateOnly(2031, 7, 14), 2000);
        await Log(new DateOnly(2031, 7, 15), 1000);
        await Log(new DateOnly(2031, 7, 17), 2400);
    }

    private IRenderedComponent<TrendsPage> Open(Action<ComponentParameterCollectionBuilder<TrendsPage>>? more = null)
    {
        var cut = Context.Render<TrendsPage>(p => more?.Invoke(p));
        cut.WaitForState(() => !Exists(cut, "trends-loading"), TimeSpan.FromSeconds(5));
        return cut;
    }

    private static bool Exists(IRenderedComponent<TrendsPage> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private static string Text(IRenderedComponent<TrendsPage> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static async Task Tap(IRenderedComponent<TrendsPage> cut, string testId)
    {
        await cut.InvokeAsync(() => cut.Find($"[data-testid={testId}]").ClickAsync(new()));
        cut.WaitForState(() => !Exists(cut, "trends-loading"), TimeSpan.FromSeconds(5));
    }

    [Test]
    public void WithNoQuery_OpensOnTheCurrentWeek_MondayToSunday()
    {
        var cut = Open();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("14 – 20 Jul"));
            Assert.That(cut.Find("[data-testid=trends-period-week]").HasAttribute("checked"), Is.True);
            Assert.That(Text(cut, "trends-empty-message"), Is.EqualTo("Nothing logged yet"));
            Assert.That(cut.Find("[aria-current=page]").GetAttribute("data-testid"), Is.EqualTo("trends-link"));
        });
    }

    [Test]
    public async Task Previous_MovesBackAWeek_AndShowsThatWeeksFigures_AndNextReturns()
    {
        await LogThreeDaysOfLastWeek();
        var cut = Open();

        await Tap(cut, "trends-previous");

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("7 – 13 Jul"));
            Assert.That(Exists(cut, "trends-empty"), Is.False);
            Assert.That(Text(cut, "trends-average-note"), Is.EqualTo("Not enough days yet"));
        });

        await Tap(cut, "trends-next");

        Assert.That(Text(cut, "trends-label"), Is.EqualTo("14 – 20 Jul"));
    }

    [Test]
    public async Task AWeekWithThreeLoggedDays_ShowsItsAverageAndHowManyAreOnTarget()
    {
        await LogThreeDaysOfLastWeek();

        var cut = Open(p => p.Add(x => x.From, new DateOnly(2031, 7, 16)));

        // 2,000 on target, 1,000 under, 2,400 over: 1,800 a day on average against 2,000.
        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("14 – 20 Jul"));
            Assert.That(Text(cut, "trends-average-figure"), Is.EqualTo("1,800"));
            Assert.That(Text(cut, "trends-average-caption"), Is.EqualTo("kcal a day on average, 200 under target"));
            Assert.That(Text(cut, "trends-on-target-figure"), Is.EqualTo("1 of 7"));
            Assert.That(cut.Find("[data-testid=trends-chart-bar-2031-07-17]").ClassList, Does.Contain("over"));
        });
    }

    [Test]
    public void AFarFutureFrom_OpensTheCurrentWeek()
    {
        var cut = Open(p => p.Add(x => x.From, DateOnly.MaxValue));

        Assert.That(Text(cut, "trends-label"), Is.EqualTo("14 – 20 Jul"));
    }

    [Test]
    public void AFarPastFrom_OpensTheWeekOfTheEarliestDay()
    {
        var cut = Open(p => p.Add(x => x.From, DateOnly.MinValue));

        Assert.That(Text(cut, "trends-label"), Is.EqualTo("27 Dec 1999 – 2 Jan 2000"));
    }

    [Test]
    public void Next_StopsAtTheCurrentWeek()
    {
        var cut = Open();

        cut.Find("[data-testid=trends-next]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("14 – 20 Jul"));
            Assert.That(cut.Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public async Task TheMonthAndYearSegments_OpenTheSpanHoldingToday()
    {
        await LogThreeDaysOfLastWeek();
        var cut = Open();

        await cut.InvokeAsync(() => cut.Find("[data-testid=trends-period-month]").Change(true));
        cut.WaitForState(() => !Exists(cut, "trends-loading"));
        var month = Text(cut, "trends-label");
        await cut.InvokeAsync(() => cut.Find("[data-testid=trends-period-year]").Change(true));
        cut.WaitForState(() => !Exists(cut, "trends-loading"));

        Assert.Multiple(() =>
        {
            Assert.That(month, Is.EqualTo("July 2031"));
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("2031"));
            Assert.That(Text(cut, "trends-on-target-caption"), Is.EqualTo("months averaging within 10% of target"));
        });
    }

    [Test]
    public async Task TheRoutesPeriodAndDay_OpenThatSpan_AndAChangeReopensIt()
    {
        await LogThreeDaysOfLastWeek();
        var cut = Open(p => p.Add(x => x.Period, TrendsPeriod.Month).Add(x => x.From, new DateOnly(2031, 3, 17)));
        Assert.That(Text(cut, "trends-label"), Is.EqualTo("March 2031"));

        cut.Render(p => p.Add(x => x.Period, TrendsPeriod.Year).Add(x => x.From, new DateOnly(2030, 5, 1)));
        cut.WaitForState(() => Text(cut, "trends-label") == "2030");

        Assert.That(Exists(cut, "trends-period"), Is.True);
    }

    [Test]
    public async Task TappingADaysBar_RaisesThatDay_AndLogsOnlyTheKind()
    {
        await LogThreeDaysOfLastWeek();
        var opened = new List<DateOnly>();
        var cut = Open(p => p.Add(x => x.From, new DateOnly(2031, 7, 16)).Add(x => x.OnOpenDay, opened.Add));

        await Tap(cut, "trends-chart-bar-2031-07-15");

        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.EqualTo(new[] { new DateOnly(2031, 7, 15) }));
            Assert.That(LoggedEvents.Of(logger).Where(r => r.Id == UiEvent.TrendsOpened).Select(r => r.Message), Is.EqualTo(new[] { "Trends opened a day from Week" }));
        });
    }

    [Test]
    public async Task TappingAMonthsBar_InYear_OpensThatMonth_AndAWeeksBar_InMonth_OpensThatWeek()
    {
        await LogThreeDaysOfLastWeek();
        var cut = Open(p => p.Add(x => x.Period, TrendsPeriod.Year));

        await Tap(cut, "trends-months-chart-bar-2031-07-01");

        Assert.That(Text(cut, "trends-label"), Is.EqualTo("July 2031"));

        await Tap(cut, "trends-weeks-chart-bar-2031-07-14");

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("14 – 20 Jul"));
            Assert.That(cut.Find("[data-testid=trends-period-week]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public async Task TappingTheFirstWeekBarOfAMonthThatStartsMidWeek_OpensThatWeekFromItsMonday()
    {
        await Log(new DateOnly(2031, 7, 2), 2000);
        await Log(new DateOnly(2031, 7, 3), 2000);
        var cut = Open(p => p.Add(x => x.Period, TrendsPeriod.Month));

        // 1 Jul 2031 is a Tuesday, so the first bucket is clipped to Tuesday to Sunday.
        await Tap(cut, "trends-weeks-chart-bar-2031-06-30");

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("30 Jun – 6 Jul"));
            Assert.That(cut.Find("[data-testid=trends-period-week]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public async Task TheArrows_StepAMonthAndAYear_AndNextStopsAtTheCurrentOne()
    {
        await LogThreeDaysOfLastWeek();
        var month = Open(p => p.Add(x => x.Period, TrendsPeriod.Month));
        await Tap(month, "trends-previous");
        var june = Text(month, "trends-label");
        await Tap(month, "trends-next");
        var july = Text(month, "trends-label");
        var year = Open(p => p.Add(x => x.Period, TrendsPeriod.Year));
        await Tap(year, "trends-previous");
        var last = Text(year, "trends-label");
        await Tap(year, "trends-next");

        Assert.Multiple(() =>
        {
            Assert.That(june, Is.EqualTo("June 2031"));
            Assert.That(july, Is.EqualTo("July 2031"));
            Assert.That(month.Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.True);
            Assert.That(last, Is.EqualTo("2030"));
            Assert.That(Text(year, "trends-label"), Is.EqualTo("2031"));
        });
    }

    [Test]
    public async Task SwitchingFromAPastSpan_OpensTheSpanHoldingItsFirstDay()
    {
        var cut = Open(p => p.Add(x => x.Period, TrendsPeriod.Year));
        await Tap(cut, "trends-previous");

        await cut.InvokeAsync(() => cut.Find("[data-testid=trends-period-month]").Change(true));
        cut.WaitForState(() => !Exists(cut, "trends-loading"));

        Assert.That(Text(cut, "trends-label"), Is.EqualTo("January 2030"));
    }

    [Test]
    public async Task SwitchingFromTheCurrentWeekThatSpansTwoMonths_OpensTheMonthHoldingToday()
    {
        // Friday 1 Aug 2031: the week is 28 Jul to 3 Aug.
        ((FakeTimeProvider)Context.Services.GetRequiredService<TimeProvider>()).SetUtcNow(new DateTimeOffset(2031, 8, 1, 12, 0, 0, TimeSpan.Zero));
        var cut = Open();
        var week = Text(cut, "trends-label");

        await cut.InvokeAsync(() => cut.Find("[data-testid=trends-period-month]").Change(true));
        cut.WaitForState(() => !Exists(cut, "trends-loading"));

        Assert.Multiple(() =>
        {
            Assert.That(week, Is.EqualTo("28 Jul – 3 Aug"));
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("August 2031"));
        });
    }

    [Test]
    public async Task EachChangeOfSpan_PutsItInTheAddressWithoutAddingAHistoryEntry_SoReturningShowsIt()
    {
        var navigation = Context.Services.GetRequiredService<BunitNavigationManager>();
        var cut = Open();

        await Tap(cut, "trends-previous");
        var previous = navigation.Uri;
        await cut.InvokeAsync(() => cut.Find("[data-testid=trends-period-month]").Change(true));
        cut.WaitForState(() => !Exists(cut, "trends-loading"));

        Assert.Multiple(() =>
        {
            Assert.That(previous, Does.EndWith("/trends?period=week&from=2031-07-07"));
            Assert.That(navigation.Uri, Does.EndWith("/trends?period=month&from=2031-07-01"));
            Assert.That(navigation.History.All(h => h.Options.ReplaceHistoryEntry), Is.True);
        });
    }

    [Test]
    public async Task TappingMarchInYear_OpensMonthForMarch()
    {
        await Log(new DateOnly(2031, 3, 10), 1800);
        var cut = Open(p => p.Add(x => x.Period, TrendsPeriod.Year));

        await Tap(cut, "trends-months-chart-bar-2031-03-01");

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("March 2031"));
            Assert.That(cut.Find("[data-testid=trends-period-month]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public async Task AFailedRead_ShowsTryAgain_AndTryAgainReadsAgain()
    {
        await connection.ExecuteAsync("ALTER TABLE DiaryEntry RENAME TO DiaryEntryAway");
        var cut = Open();
        Assert.That(Text(cut, "trends-load-failed-error"), Is.EqualTo("Couldn't load your trends"));

        await connection.ExecuteAsync("ALTER TABLE DiaryEntryAway RENAME TO DiaryEntry");
        await Tap(cut, "trends-load-failed-retry");

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "trends-load-failed"), Is.False);
            Assert.That(Text(cut, "trends-empty-message"), Is.EqualTo("Nothing logged yet"));
        });
    }

    [Test]
    public async Task AnEntryAddedElsewhere_ReadsTheSpanAgain()
    {
        var cut = Open();
        Assert.That(Exists(cut, "trends-empty"), Is.True);
        await Log(new DateOnly(2031, 7, 14), 2000);
        await Log(new DateOnly(2031, 7, 15), 2000);
        await Log(new DateOnly(2031, 7, 16), 2000);

        cut.WaitForState(() => Exists(cut, "trends-average-figure") && Text(cut, "trends-average-figure") == "2,000", TimeSpan.FromSeconds(5));

        Assert.That(Text(cut, "trends-average-figure"), Is.EqualTo("2,000"));
    }

    [Test]
    public async Task AndroidBack_GoesToTheStartDestination()
    {
        var navigated = new List<NavDestination>();
        Open(p => p.Add(x => x.OnNavigate, navigated.Add));

        await BackButton.HandleAsync();

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Today }));
    }

    [Test]
    public async Task TheBarsAddSheet_OpensOnTrends_AndSearchAndScanCarryTheMealAndToday()
    {
        var searches = new List<MealAddRequest>();
        var scans = new List<MealAddRequest>();
        var cut = Open(p => p.Add(x => x.OnAdd, searches.Add).Add(x => x.OnScan, scans.Add));

        await cut.InvokeAsync(() => cut.Find("[data-testid=add-button]").ClickAsync(new()));
        cut.WaitForState(() => Exists(cut, "add-sheet-search"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-sheet-search]").ClickAsync(new()));
        cut.WaitForState(() => !Exists(cut, "add-sheet"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-button]").ClickAsync(new()));
        cut.WaitForState(() => Exists(cut, "add-sheet-scan"), TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-sheet-scan]").ClickAsync(new()));

        Assert.Multiple(() =>
        {
            Assert.That(searches, Is.EqualTo(new[] { new MealAddRequest(Meal.Dinner, Today) }));
            Assert.That(scans, Is.EqualTo(new[] { new MealAddRequest(Meal.Dinner, Today) }));
            Assert.That(Exists(cut, "add-sheet"), Is.False);
        });
    }

    [Test]
    public async Task AUsualFoodAddedFromTheSheetOnTrends_GoesToTodayAndPostsTheSnackbar()
    {
        await Log(Today.AddDays(-1), 150);
        var cut = Open();
        await cut.InvokeAsync(() => cut.Find("[data-testid=add-button]").ClickAsync(new()));
        cut.WaitForState(() => Exists(cut, $"add-sheet-usual-{rice.Id}-add"), TimeSpan.FromSeconds(5));

        await cut.InvokeAsync(() => cut.Find($"[data-testid=add-sheet-usual-{rice.Id}-add]").ClickAsync(new()));

        var entries = (await core.GetRequiredService<DiaryService>().ListAsync(Today)).Entries;
        Assert.Multiple(() =>
        {
            Assert.That(entries.Single().Meal, Is.EqualTo(Meal.Dinner));
            Assert.That(Context.Services.GetRequiredService<SnackbarService>().Current!.Text, Is.EqualTo("Secret rice added to Dinner"));
        });
    }

    [Test]
    public async Task TheLogLines_CarryThePeriodAndCountsOnly_NeverAFoodACalorieOrADate()
    {
        await LogThreeDaysOfLastWeek();
        var cut = Open(p => p.Add(x => x.From, new DateOnly(2031, 7, 16)));
        await Tap(cut, "trends-chart-bar-2031-07-17");
        await cut.InvokeAsync(() => cut.Find("[data-testid=trends-period-year]").Change(true));
        cut.WaitForState(() => !Exists(cut, "trends-loading"));
        await Tap(cut, "trends-months-chart-bar-2031-07-01");

        var records = LoggedEvents.Of(logger);
        var messages = records.Select(r => r.Message).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(records.Select(r => r.Id), Does.Contain(UiEvent.TrendsLoaded).And.Contain(UiEvent.TrendsOpened));
            Assert.That(messages, Does.Contain("Trends read for Week, days 7"));
            Assert.That(messages, Does.Contain("Trends opened a day from Week"));
            Assert.That(messages, Does.Contain("Trends opened a month from Year"));
            Assert.That(messages, Has.None.Contain("Secret").And.None.Contain("rice").And.None.Contain("2031").And.None.Contain("2400").And.None.Contain("1,800").And.None.Contain("kcal"));
        });
    }

    [Test]
    public async Task AFailedRead_LogsThePeriodOnly()
    {
        await connection.ExecuteAsync("ALTER TABLE DiaryEntry RENAME TO DiaryEntryAway");

        Open();

        var failed = LoggedEvents.Of(logger).Where(r => r.Id == UiEvent.TrendsLoadFailed).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(failed.Select(r => (r.Level, r.Message)), Is.EqualTo(new[] { (LogLevel.Warning, "Trends could not be read for Week") }));
        });
    }
}
