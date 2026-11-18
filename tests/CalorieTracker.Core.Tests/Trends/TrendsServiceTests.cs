using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Summary;
using CalorieTracker.Core.Targets;
using CalorieTracker.Core.Trends;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Trends;

public class TrendsServiceTests
{
    private static readonly int[] WeekKcal = [1980, 2240, 1875, 2050, 2390, 2610, 1720];

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeTimeProvider time = null!;
    private FakeLogger<TrendsService> logger = null!;
    private DiaryService diary = null!;
    private TargetService targets = null!;
    private TrendsService service = null!;
    private int rice;
    private int cheddar;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<TargetHistory>();
        rice = await AddFood("Secret rice", 100);
        cheddar = await AddFood("Secret cheddar", 400);
        gate = new DatabaseGate();
        time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        logger = new FakeLogger<TrendsService>();
        diary = new DiaryService(new DiaryStore(connection, gate), NullLogger<DiaryService>.Instance);
        targets = new TargetService(new TargetStore(connection, gate), time, NullLogger<TargetService>.Instance);
        service = new TrendsService(new TrendsStore(connection, gate), targets, logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private static DateOnly Nov(int day) => new(2026, 11, day);

    private async Task<int> AddFood(string name, double kcal)
    {
        var food = new Food { Source = FoodSource.CoFID, SourceId = name, Name = name, Unit = FoodUnit.Gram, Kcal = kcal, ProteinG = 10, CarbsG = 20, FatG = 5, SearchText = name.ToLowerInvariant() };
        await connection.InsertAsync(food);
        return food.Id;
    }

    /// <summary>Logs a day of exactly <paramref name="kcal"/>: 400 of cheddar at lunch and the rest as rice at dinner.</summary>
    private async Task<int> Log(DateOnly date, int kcal)
    {
        await diary.AddAsync(date, Meal.Lunch, cheddar, 100);
        return (await diary.AddAsync(date, Meal.Dinner, rice, kcal - 400)).Entry!.Id;
    }

    private async Task LogWeek()
    {
        for (var i = 0; i < 7; i++)
        {
            await Log(Nov(9 + i), WeekKcal[i]);
        }
    }

    private async Task SaveTargetOn(DateOnly date, double kcal)
    {
        time.SetUtcNow(new DateTimeOffset(date.Year, date.Month, date.Day, 12, 0, 0, TimeSpan.Zero));
        Assert.That((await targets.SaveAsync(kcal)).Status, Is.EqualTo(TargetStatus.Ok));
    }

    private async Task<TrendsReport> Get(DateOnly from, DateOnly to, DateOnly today)
    {
        var result = await service.GetAsync(from, to, today);
        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Ok));
        return result.Report!;
    }

    [Test]
    public async Task Ac1_FixtureWeek_GivesTheAverageTheBandCountsAndTheTopFoodsInKcalOrder()
    {
        await LogWeek();
        await SaveTargetOn(Nov(1), 2100);

        var report = await Get(Nov(9), Nov(15), Nov(16));

        var range = report.Range;
        Assert.Multiple(() =>
        {
            Assert.That(report.Days.Select(d => d.Totals.Kcal), Is.EqualTo(WeekKcal));
            Assert.That((range.PastDays, range.DaysLogged, range.AverageKcal), Is.EqualTo((7, 7, (int?)2124)));
            Assert.That(report.Days.Where(d => d.Status == DayStatus.OnTarget).Select(d => d.Date), Is.EqualTo(new[] { Nov(9), Nov(10), Nov(12) }));
            Assert.That((range.Under, range.OnTarget, range.Over), Is.EqualTo((2, 3, 2)));
            Assert.That(range.TopFoods.Select(f => (f.FoodId, f.Title, f.TimesEaten, f.Kcal)), Is.EqualTo(new[] { (rice, "Secret rice", 7, 12065), (cheddar, "Secret cheddar", 7, 2800) }));
            Assert.That(range.Enough, Is.EqualTo(new TrendsEnoughData(true, true, true, true, true)));
            Assert.That(range.AverageKcalByMeal, Is.EqualTo(new MealKcal(0, 400, 1724, 0)));
            Assert.That(range.AverageMacros, Is.EqualTo(new Nutrition(2124, 182.4, 364.7, 91.2)));
            Assert.That(report.BucketKind, Is.EqualTo(TrendBucketKind.None));
        });
    }

    [Test]
    public async Task Ac1_TheDayCarriesItsTargetMacrosAndKcalByMeal()
    {
        await Log(Nov(9), 1980);
        await SaveTargetOn(Nov(1), 2100);

        var day = (await Get(Nov(9), Nov(9), Nov(20))).Days.Single();

        Assert.That(day, Is.EqualTo(new TrendDay(Nov(9), 2100, new Nutrition(1980, 168, 336, 84), new MealKcal(0, 400, 1580, 0), true, DayStatus.OnTarget)));
    }

    [Test]
    public async Task TopFoods_AreCappedAtFive()
    {
        for (var i = 0; i < 7; i++)
        {
            await diary.AddAsync(Nov(9), Meal.Snacks, await AddFood($"Food {i}", 100 + i * 10), 100);
        }

        var top = (await Get(Nov(9), Nov(9), Nov(20))).Range.TopFoods;

        Assert.That(top.Select(f => f.Kcal), Is.EqualTo(new[] { 160, 150, 140, 130, 120 }));
    }

    [TestCase(1890, DayStatus.OnTarget)]
    [TestCase(2310, DayStatus.OnTarget)]
    [TestCase(1889, DayStatus.Under)]
    [TestCase(2311, DayStatus.Over)]
    [TestCase(2100, DayStatus.OnTarget)]
    public async Task Ac2_TheBandEdgesAreInclusive(int kcal, DayStatus expected)
    {
        await SaveTargetOn(Nov(1), 2100);
        await Log(Nov(9), kcal);

        var day = (await Get(Nov(9), Nov(9), Nov(20))).Days.Single();

        Assert.That(day.Status, Is.EqualTo(expected));
    }

    [Test]
    public async Task Ac3_ATargetChangeInTheMiddleJudgesEachDayAgainstItsOwnTarget()
    {
        await SaveTargetOn(Nov(1), 2000);
        await SaveTargetOn(Nov(12), 3000);
        await Log(Nov(10), 2000);
        await Log(Nov(11), 2000);
        await Log(Nov(12), 2000);
        await Log(Nov(13), 2000);

        var days = (await Get(Nov(10), Nov(13), Nov(20))).Days;

        Assert.That(days.Select(d => (d.TargetKcal, d.Status)), Is.EqualTo(new[]
        {
            ((int?)2000, DayStatus.OnTarget),
            ((int?)2000, DayStatus.OnTarget),
            ((int?)3000, DayStatus.Under),
            ((int?)3000, DayStatus.Under),
        }));
    }

    [Test]
    public async Task DayStatuses_CoverNoTargetNotLoggedTodayAndFuture()
    {
        await Log(Nov(9), 2000);
        await Log(Nov(12), 2000);
        await Log(Nov(13), 2000);
        await SaveTargetOn(Nov(11), 2000);

        var days = (await Get(Nov(9), Nov(14), Nov(12))).Days;

        Assert.That(days.Select(d => d.Status), Is.EqualTo(new[]
        {
            DayStatus.NoTarget, DayStatus.NotLogged, DayStatus.NotLogged, DayStatus.Today, DayStatus.Future, DayStatus.Future,
        }));
        Assert.That(days[0].TargetKcal, Is.Null);
    }

    [Test]
    public async Task Ac4_RemovedEntriesDoNotCountAndTodayIsLeftOutOfTheAverages()
    {
        await Log(Nov(9), 2000);
        await Log(Nov(10), 2400);
        var removedId = await Log(Nov(10), 3000);
        await diary.RemoveAsync(removedId);
        await Log(Nov(11), 1000);
        await Log(Nov(12), 5000);
        await SaveTargetOn(Nov(1), 2000);

        var report = await Get(Nov(9), Nov(12), Nov(12));

        Assert.Multiple(() =>
        {
            // Day 10 keeps its cheddar of the removed entry's twin: 2400 plus the second log's 400 lunch (not removed) and 2600 dinner removed.
            Assert.That(report.Days[1].Totals.Kcal, Is.EqualTo(2800));
            Assert.That(report.Days[3].Status, Is.EqualTo(DayStatus.Today));
            Assert.That(report.Days[3].Totals.Kcal, Is.EqualTo(5000));
            Assert.That((report.Range.PastDays, report.Range.DaysLogged, report.Range.AverageKcal), Is.EqualTo((3, 3, (int?)1933)));
            Assert.That((report.Range.Under, report.Range.OnTarget, report.Range.Over), Is.EqualTo((1, 1, 1)));
            Assert.That(report.Range.TopFoods.Sum(f => f.TimesEaten), Is.EqualTo(7));
        });
    }

    [Test]
    public async Task Ac5_TheStreakCountsBackFromTodayAndStopsAtTheFirstEmptyDay()
    {
        await Log(Nov(10), 2000);
        await Log(Nov(12), 2000);
        await Log(Nov(13), 2000);
        await Log(Nov(14), 2000);

        Assert.That((await Get(Nov(9), Nov(14), Nov(14))).Range.Streak, Is.EqualTo(3));
        Assert.That((await Get(Nov(9), Nov(14), Nov(15))).Range.Streak, Is.EqualTo(3));
        Assert.That((await Get(Nov(9), Nov(14), Nov(16))).Range.Streak, Is.EqualTo(0));
        Assert.That((await Get(Nov(9), Nov(11), Nov(14))).Range.Streak, Is.EqualTo(3));
    }

    [Test]
    public async Task Ac5_TheStreakNeverCountsAFutureDay()
    {
        await Log(Nov(13), 2000);
        await Log(Nov(14), 2000);

        Assert.That((await Get(Nov(9), Nov(15), Nov(13))).Range.Streak, Is.EqualTo(1));
    }

    [Test]
    public async Task Ac6_AMonthRangeHasWeekBucketsWithAverageDaysLoggedAndDaysOver()
    {
        await SaveTargetOn(Nov(1), 2100);
        await LogWeek();
        await Log(Nov(2), 2000);
        await Log(Nov(3), 2400);
        await Log(Nov(30), 1000);

        var report = await Get(Nov(1), Nov(30), Nov(16));

        Assert.That(report.BucketKind, Is.EqualTo(TrendBucketKind.Week));
        Assert.That(
            report.Buckets.Select(b => (b.Start, b.End, b.AverageKcal, b.DaysLogged, b.DaysOver)),
            Is.EqualTo(new[]
            {
                (Nov(1), Nov(1), (int?)null, 0, 0),
                (Nov(2), Nov(8), (int?)2200, 2, 1),
                (Nov(9), Nov(15), (int?)2124, 7, 2),
                (Nov(16), Nov(22), (int?)null, 0, 0),
                (Nov(23), Nov(29), (int?)null, 0, 0),
                (Nov(30), Nov(30), (int?)null, 0, 0),
            }));
    }

    [Test]
    public async Task Ac6_ALoggedFutureDayIsLeftOutOfTheRangeFigures()
    {
        await SaveTargetOn(Nov(1), 2100);
        await LogWeek();
        await Log(Nov(2), 2000);
        await Log(Nov(3), 2400);
        await Log(Nov(30), 1000);

        var range = (await Get(Nov(1), Nov(30), Nov(16))).Range;

        // Past days Nov 1 to 15; 9 logged: week 14865 + 2000 + 2400 = 19265, so 2140.6. With Nov 30 it would be 10 days and 2027.
        // Bands against 1890 to 2310: under 1875 and 1720; on target 1980, 2240, 2050 and 2000; over 2390, 2610 and 2400 (Nov 30's 1000 would be a third under).
        // Rice: 12065 + 1600 + 2000 = 15665 over 9 days; cheddar 9 x 400 (Nov 30 would make 10 and 11 each).
        Assert.Multiple(() =>
        {
            Assert.That((range.PastDays, range.DaysLogged, range.AverageKcal), Is.EqualTo((15, 9, (int?)2141)));
            Assert.That((range.Under, range.OnTarget, range.Over), Is.EqualTo((2, 4, 3)));
            Assert.That(range.TopFoods.Select(f => (f.FoodId, f.TimesEaten, f.Kcal)), Is.EqualTo(new[] { (rice, 9, 15665), (cheddar, 9, 3600) }));
        });
    }

    [Test]
    public async Task Ac6_AYearRangeHasMonthBuckets()
    {
        var january = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await new TargetService(new TargetStore(connection, gate), january, NullLogger<TargetService>.Instance).SaveAsync(2000);
        await Log(new DateOnly(2026, 1, 31), 2500);
        await Log(new DateOnly(2026, 2, 1), 2000);
        await Log(new DateOnly(2026, 2, 28), 1500);
        await Log(new DateOnly(2026, 2, 27), 2600);
        await Log(new DateOnly(2026, 12, 31), 9000);

        var report = await Get(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Nov(18));

        Assert.That(report.BucketKind, Is.EqualTo(TrendBucketKind.Month));
        Assert.That(report.Buckets, Has.Count.EqualTo(12));
        Assert.That(
            report.Buckets.Take(3).Select(b => (b.Start, b.End, b.AverageKcal, b.DaysLogged, b.DaysOver)),
            Is.EqualTo(new[]
            {
                (new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), (int?)2500, 1, 1),
                (new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), (int?)2033, 3, 1),
                (new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), (int?)null, 0, 0),
            }));
        Assert.That(report.Buckets[^1].AverageKcal, Is.Null);
    }

    [Test]
    public async Task Ac6_TodayIsLeftOutOfItsBucketAverage()
    {
        await Log(Nov(16), 2000);
        await Log(Nov(17), 4000);

        var report = await Get(Nov(9), Nov(22), Nov(17));

        Assert.That(report.Buckets.Single(b => b.Start == Nov(16)).AverageKcal, Is.EqualTo(2000));
    }

    [Test]
    public async Task Ac7_AnEmptyRangeGivesNoFiguresAndNotEnoughDataFlags()
    {
        var result = await service.GetAsync(Nov(9), Nov(15), Nov(16));

        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Ok));
        var range = result.Report!.Range;
        Assert.Multiple(() =>
        {
            Assert.That(result.Report.Days, Has.Count.EqualTo(7));
            Assert.That((range.PastDays, range.DaysLogged, range.AverageKcal, range.AverageMacros, range.AverageKcalByMeal, range.Streak), Is.EqualTo((7, 0, (int?)null, (Nutrition?)null, (MealKcal?)null, 0)));
            Assert.That(range.TopFoods, Is.Empty);
            Assert.That(range.Enough, Is.EqualTo(new TrendsEnoughData(false, false, false, false, false)));
        });
    }

    [Test]
    public async Task EnoughData_NeedsThreeLoggedDaysAndCountsNeedThreeJudgedDays()
    {
        await SaveTargetOn(Nov(1), 2000);
        await Log(Nov(9), 2000);
        await Log(Nov(10), 2000);

        Assert.That((await Get(Nov(9), Nov(15), Nov(16))).Range.Enough.Average, Is.False);

        await Log(Nov(11), 2000);
        Assert.That((await Get(Nov(9), Nov(15), Nov(16))).Range.Enough, Is.EqualTo(new TrendsEnoughData(true, true, true, true, true)));

        await connection.ExecuteAsync("DELETE FROM TargetHistory");
        Assert.That((await Get(Nov(9), Nov(15), Nov(16))).Range.Enough, Is.EqualTo(new TrendsEnoughData(true, true, true, false, true)));
    }

    [Test]
    public async Task Ac7_ABackwardsOrTooLongRangeIsInvalid()
    {
        Assert.That((await service.GetAsync(Nov(15), Nov(9), Nov(16))).Status, Is.EqualTo(TrendsStatus.Invalid));
        Assert.That((await service.GetAsync(Nov(1), Nov(1).AddDays(TargetService.MaxRangeDays), Nov(16))).Status, Is.EqualTo(TrendsStatus.Invalid));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.TrendsRejected, CoreEvent.TrendsRejected }));
    }

    [TestCase("DiaryEntry")]
    [TestCase("TargetHistory")]
    [TestCase("Food")]
    public async Task Ac7_AFailingDatabaseReturnsFailedInsteadOfThrowing(string table)
    {
        await Log(Nov(9), 2000);
        await connection.ExecuteAsync($"DROP TABLE {table}");

        var result = await service.GetAsync(Nov(9), Nov(15), Nov(16));

        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Failed));
        Assert.That(result.Report, Is.Null);
    }

    [Test]
    public async Task AnEntryWhoseFoodIsGone_ReturnsFailedAndLogsTheIds()
    {
        await Log(Nov(9), 2000);
        await connection.ExecuteAsync("DELETE FROM Food WHERE Id = ?", rice);

        var result = await service.GetAsync(Nov(9), Nov(15), Nov(16));

        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.TrendsFoodMissing }));
    }

    [Test]
    public async Task AFoodWithAnInfiniteNutrient_ReturnsFailedAndLogsOverflow()
    {
        await connection.ExecuteAsync("UPDATE Food SET ProteinG = 1e999 WHERE Id = ?", rice);
        await Log(Nov(9), 2000);

        var result = await service.GetAsync(Nov(9), Nov(15), Nov(16));

        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.TrendsOverflow }));
    }

    [Test]
    public async Task Ac8_ACallReadsTheRangeEntriesOnceTheStreakDatesOnceAndTargetsOnce()
    {
        await LogWeek();
        await SaveTargetOn(Nov(1), 2100);
        var rangeReads = 0;
        var streakReads = 0;
        var targetReads = 0;
        connection.Trace = true;
        connection.Tracer = sql =>
        {
            var diaryRead = sql.Contains("FROM DiaryEntry", StringComparison.Ordinal);
            var streak = sql.Contains("SELECT DISTINCT Date FROM DiaryEntry", StringComparison.Ordinal);
            rangeReads += diaryRead && !streak ? 1 : 0;
            streakReads += streak ? 1 : 0;
            targetReads += sql.Contains("FROM TargetHistory", StringComparison.Ordinal) ? 1 : 0;
        };

        var (_, result) = await GateHolder.CallWhileHeld(gate, () => service.GetAsync(Nov(1), Nov(30), Nov(16)), () => Task.FromResult(0));

        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Ok));
        Assert.That((rangeReads, streakReads, targetReads), Is.EqualTo((1, 1, 1)));
    }

    [Test]
    public async Task Ac8_TheStoreReadWaitsForTheGate()
    {
        await LogWeek();

        var (_, read) = await GateHolder.CallWhileHeld(gate, () => new TrendsStore(connection, gate).ReadAsync("2026-11-09", "2026-11-15", Nov(16)), () => Task.FromResult(0));

        Assert.That((read.Entries.Count, read.Streak), Is.EqualTo((14, 7)));
    }

    [Test]
    public async Task AStreakOfTwentyDaysIsReportedOnASevenDayRange()
    {
        for (var i = 0; i < 20; i++)
        {
            await Log(Nov(1).AddDays(i), 2000);
        }

        Assert.That((await Get(Nov(14), Nov(20), Nov(21))).Range.Streak, Is.EqualTo(20));
    }

    [Test]
    public async Task APastRangeStillReportsTheStreakThroughToday()
    {
        await Log(Nov(14), 2000);
        await Log(Nov(15), 2000);
        await Log(Nov(16), 2000);

        Assert.That((await Get(Nov(1), Nov(5), Nov(16))).Range.Streak, Is.EqualTo(3));
    }

    [Test]
    public async Task AnEntryAfterTheRangeWithADeletedFood_DoesNotFailAnOlderRange()
    {
        await Log(Nov(9), 2000);
        await diary.AddAsync(Nov(20), Meal.Lunch, await AddFood("Gone", 100), 100);
        await connection.ExecuteAsync("DELETE FROM Food WHERE Name = 'Gone'");

        var result = await service.GetAsync(Nov(9), Nov(15), Nov(21));

        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Ok));
        Assert.That(LoggedEvents.Of(logger).Single().Message, Is.EqualTo("Trends built for 7 days from 2 entries"));
    }

    [Test]
    public async Task EachMealHasItsOwnKcalInTheDayAndTheAverage()
    {
        await diary.AddAsync(Nov(9), Meal.Breakfast, rice, 150);
        await diary.AddAsync(Nov(9), Meal.Lunch, rice, 250);
        await diary.AddAsync(Nov(9), Meal.Dinner, rice, 350);
        await diary.AddAsync(Nov(9), Meal.Snacks, rice, 450);

        var report = await Get(Nov(9), Nov(9), Nov(20));

        Assert.That(report.Days.Single().KcalByMeal, Is.EqualTo(new MealKcal(150, 250, 350, 450)));
        Assert.That(report.Range.AverageKcalByMeal, Is.EqualTo(new MealKcal(150, 250, 350, 450)));
    }

    [Test]
    public async Task OneLoggedDayGivesItsOwnMacrosAsTheAverage()
    {
        await Log(Nov(9), 1980);

        Assert.That((await Get(Nov(9), Nov(15), Nov(16))).Range.AverageMacros, Is.EqualTo(new Nutrition(1980, 168, 336, 84)));
    }

    [Test]
    public async Task AnAverageOnExactlyHalfRoundsAwayFromZero()
    {
        await Log(Nov(9), 2000);
        await Log(Nov(10), 2001);

        var range = (await Get(Nov(9), Nov(15), Nov(16))).Range;

        Assert.That((range.AverageKcal, range.AverageMacros!.Kcal), Is.EqualTo(((int?)2001, 2001)));
    }

    [Test]
    public async Task AFractionalAmountGivesTheSameDayTotalsAsTheDaySummary()
    {
        var snack = await AddFood("Snack", 150);
        await diary.AddAsync(Nov(9), Meal.Snacks, snack, 33.3);
        await diary.AddAsync(Nov(9), Meal.Snacks, rice, 33.3);
        var summary = new DaySummaryService(new DaySummaryStore(connection, gate), NullLogger<DaySummaryService>.Instance);

        var expected = (await summary.GetAsync(Nov(9))).Summary!.Totals;
        var day = (await Get(Nov(9), Nov(9), Nov(20))).Days.Single();

        Assert.That(day.Totals, Is.EqualTo(expected));
        Assert.That(day.Totals.Kcal, Is.EqualTo(83));
    }

    [Test]
    public async Task TopFoodsWithEqualKcalAreOrderedByTimesEatenThenFoodId()
    {
        var a = await AddFood("A", 100);
        var b = await AddFood("B", 100);
        var c = await AddFood("C", 100);
        await diary.AddAsync(Nov(9), Meal.Snacks, c, 100);
        await diary.AddAsync(Nov(9), Meal.Snacks, b, 50);
        await diary.AddAsync(Nov(9), Meal.Snacks, b, 50);
        await diary.AddAsync(Nov(9), Meal.Snacks, a, 100);

        var top = (await Get(Nov(9), Nov(9), Nov(20))).Range.TopFoods;

        Assert.That(top.Select(f => (f.FoodId, f.TimesEaten)), Is.EqualTo(new[] { (b, 2), (a, 1), (c, 1) }));
    }

    [TestCase(7, TrendBucketKind.None)]
    [TestCase(8, TrendBucketKind.Week)]
    [TestCase(62, TrendBucketKind.Week)]
    [TestCase(63, TrendBucketKind.Month)]
    public async Task TheBucketKindChangesAtSevenToEightAndSixtyTwoToSixtyThreeDays(int length, TrendBucketKind expected)
    {
        var report = await Get(Nov(1), Nov(1).AddDays(length - 1), Nov(20));

        Assert.That(report.BucketKind, Is.EqualTo(expected));
    }

    [Test]
    public async Task AnEntryWithAnUnknownMeal_ReturnsFailedAndLogsTheEntry()
    {
        await Log(Nov(9), 2000);
        await connection.ExecuteAsync("UPDATE DiaryEntry SET Meal = 9");

        var result = await service.GetAsync(Nov(9), Nov(15), Nov(16));

        Assert.That(result.Status, Is.EqualTo(TrendsStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.TrendsMealUnknown }));
    }

    [Test]
    public async Task Ac8_TheLogLinesCarryCountsOnlyAndNoFoodNameOrKcal()
    {
        await LogWeek();
        await SaveTargetOn(Nov(1), 2100);
        await service.GetAsync(Nov(9), Nov(15), Nov(16));
        await service.GetAsync(Nov(15), Nov(9), Nov(16));
        await connection.ExecuteAsync("DELETE FROM Food WHERE Id = ?", rice);
        await service.GetAsync(Nov(9), Nov(15), Nov(16));
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");
        await service.GetAsync(Nov(9), Nov(15), Nov(16));

        var lines = LoggedEvents.Of(logger);

        Assert.That(lines.Select(l => (int)l.Id), Is.All.InRange(1080, 1089));
        Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[] { CoreEvent.TrendsBuilt, CoreEvent.TrendsRejected, CoreEvent.TrendsFoodMissing, CoreEvent.TrendsFailed }));
        Assert.That(lines[0].Message, Is.EqualTo("Trends built for 7 days from 14 entries"));
        foreach (var (_, _, message) in lines)
        {
            Assert.That(message, Does.Not.Contain("Secret").And.Not.Contain("2026").And.Not.Contain("2124").And.Not.Contain("1980"));
        }
    }
}
