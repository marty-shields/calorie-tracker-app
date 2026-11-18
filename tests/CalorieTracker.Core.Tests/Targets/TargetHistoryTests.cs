using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Targets;

public class TargetHistoryTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeLogger<TargetService> logger = null!;
    private FakeTimeProvider time = null!;
    private TargetService service = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<TargetHistory>();
        gate = new DatabaseGate();
        logger = new FakeLogger<TargetService>();
        time = new FakeTimeProvider(Noon(1));
        service = new TargetService(new TargetStore(connection, gate), time, logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private static DateTimeOffset Noon(int nov) => new(2031, 11, nov, 12, 0, 0, TimeSpan.Zero);

    private static DateOnly Nov(int day) => new(2031, 11, day);

    private async Task SaveOn(int day, double kcal)
    {
        time.SetUtcNow(Noon(day));
        Assert.That((await service.SaveAsync(kcal)).Status, Is.EqualTo(TargetStatus.Ok));
    }

    [Test]
    public async Task ForRangeAsync_TwoTargets_GivesEachDayTheOneThatAppliedAndNoneBeforeTheFirst()
    {
        await SaveOn(1, 2000);
        await SaveOn(10, 2200);

        var range = await service.ForRangeAsync(Nov(1), Nov(15));
        var before = await service.ForRangeAsync(new DateOnly(2031, 10, 30), Nov(1));

        Assert.That(range.Status, Is.EqualTo(TargetStatus.Ok));
        Assert.That(range.Days.Select(d => d.Date), Is.EqualTo(Enumerable.Range(1, 15).Select(Nov)));
        Assert.That(range.Days.Select(d => d.Kcal), Is.EqualTo(Enumerable.Repeat((int?)2000, 9).Concat(Enumerable.Repeat((int?)2200, 6))));
        Assert.That(before.Days.Select(d => d.Kcal), Is.EqualTo(new int?[] { null, null, 2000 }));
    }

    [Test]
    public async Task ForDateAsync_GivesTheTargetOfThatDay()
    {
        await SaveOn(1, 2000);
        await SaveOn(10, 2200);

        Assert.That((await service.ForDateAsync(new DateOnly(2031, 10, 31))).Kcal, Is.Null);
        Assert.That((await service.ForDateAsync(Nov(9))).Kcal, Is.EqualTo(2000));
        Assert.That((await service.ForDateAsync(Nov(10))).Kcal, Is.EqualTo(2200));
        Assert.That((await service.ForDateAsync(Nov(28))).Kcal, Is.EqualTo(2200));
        Assert.That((await service.ForDateAsync(Nov(28))).Status, Is.EqualTo(TargetStatus.Ok));
    }

    [Test]
    public async Task SaveAsync_TwiceOnTheSameDay_LeavesOneRowHoldingTheLastValue()
    {
        await SaveOn(5, 2000);
        await SaveOn(5, 1800);

        var rows = await connection.QueryAsync<TargetHistory>("SELECT * FROM TargetHistory");
        Assert.That(rows.Select(r => (r.EffectiveFrom, r.Kcal)), Is.EqualTo(new[] { ("2031-11-05", (double?)1800) }));
        Assert.That((await service.ForDateAsync(Nov(5))).Kcal, Is.EqualTo(1800));
    }

    [Test]
    public async Task ClearAsync_OnADay_MeansNoTargetFromThenOnAndEarlierDaysKeepTheirs()
    {
        await SaveOn(1, 2000);
        time.SetUtcNow(Noon(12));
        await service.ClearAsync();

        var range = await service.ForRangeAsync(Nov(10), Nov(14));

        Assert.That(range.Days.Select(d => d.Kcal), Is.EqualTo(new int?[] { 2000, 2000, null, null, null }));
        Assert.That((await service.GetAsync()).Kcal, Is.Null);
    }

    [Test]
    public async Task SaveAsync_AfterAClearOnTheSameDay_ReplacesTheClearedRow()
    {
        await SaveOn(12, 2000);
        await service.ClearAsync();
        await SaveOn(12, 1700);

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TargetHistory"), Is.EqualTo(1));
        Assert.That((await service.ForDateAsync(Nov(12))).Kcal, Is.EqualTo(1700));
    }

    [Test]
    public async Task SaveAsync_Rejected_WritesNoHistoryRow()
    {
        await service.SaveAsync(-5);

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TargetHistory"), Is.Zero);
    }

    [Test]
    public async Task SaveAsync_UsesTheLocalDateOfTheClock()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus13", TimeSpan.FromHours(13), "plus13", "plus13");
        time = new FakeTimeProvider(new DateTimeOffset(2031, 11, 4, 23, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(zone);
        service = new TargetService(new TargetStore(connection, gate), time, logger);

        await service.SaveAsync(2000);

        Assert.That(await connection.ExecuteScalarAsync<string>("SELECT EffectiveFrom FROM TargetHistory"), Is.EqualTo("2031-11-05"));
    }

    [Test]
    public async Task ForRangeAsync_SingleDayAndEndBeforeStart()
    {
        await SaveOn(1, 2000);

        var one = await service.ForRangeAsync(Nov(3), Nov(3));
        var backwards = await service.ForRangeAsync(Nov(4), Nov(3));

        Assert.That(one.Days, Is.EqualTo(new[] { new DayTarget(Nov(3), 2000) }));
        Assert.That(backwards.Status, Is.EqualTo(TargetStatus.Invalid));
        Assert.That(backwards.Days, Is.Empty);
    }

    [Test]
    public async Task ForRangeAsync_WaitsForTheGateAndReadsHistoryOnce()
    {
        await SaveOn(1, 2000);
        var reads = new List<string>();
        connection.Trace = true;
        connection.Tracer = sql =>
        {
            if (sql.Contains("FROM TargetHistory", StringComparison.Ordinal))
            {
                reads.Add(sql);
            }
        };

        var (_, range) = await GateHolder.CallWhileHeld(gate, () => service.ForRangeAsync(Nov(1), Nov(30)), () => Task.FromResult(0));

        Assert.That(range.Days, Has.Count.EqualTo(30));
        Assert.That(reads, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ForDateAsync_WaitsForTheGate()
    {
        await SaveOn(1, 2000);

        var (_, date) = await GateHolder.CallWhileHeld(gate, () => service.ForDateAsync(Nov(2)), () => Task.FromResult(0));

        Assert.That(date.Kcal, Is.EqualTo(2000));
    }

    private void MoveClockBackTo(int day)
    {
        time = new FakeTimeProvider(Noon(day));
        service = new TargetService(new TargetStore(connection, gate), time, logger);
    }

    [Test]
    public async Task SaveAsync_OnAnEarlierDateAfterTheClockMovedBack_DropsLaterRows()
    {
        await SaveOn(18, 2000);
        MoveClockBackTo(17);
        await service.SaveAsync(1800);

        Assert.That((await service.ForDateAsync(Nov(18))).Kcal, Is.EqualTo(1800));
        Assert.That((await service.ForDateAsync(Nov(25))).Kcal, Is.EqualTo(1800));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TargetHistory"), Is.EqualTo(1));
    }

    [Test]
    public async Task ClearAsync_OnAnEarlierDateAfterTheClockMovedBack_DropsLaterRows()
    {
        await SaveOn(18, 2000);
        MoveClockBackTo(17);
        await service.ClearAsync();

        Assert.That((await service.ForDateAsync(Nov(18))).Kcal, Is.Null);
        Assert.That((await service.ForDateAsync(Nov(25))).Kcal, Is.Null);
    }

    [Test]
    public async Task ForRangeAsync_AtTheLimitWorks_AboveItIsInvalid_AndMaxValueDoesNotThrow()
    {
        await SaveOn(1, 2000);
        var start = Nov(1);

        var atLimit = await service.ForRangeAsync(start, start.AddDays(TargetService.MaxRangeDays - 1));
        var over = await service.ForRangeAsync(start, start.AddDays(TargetService.MaxRangeDays));
        var huge = await service.ForRangeAsync(DateOnly.MinValue, DateOnly.MaxValue);
        var atMax = await service.ForRangeAsync(DateOnly.MaxValue, DateOnly.MaxValue);

        Assert.That(atLimit.Status, Is.EqualTo(TargetStatus.Ok));
        Assert.That(atLimit.Days, Has.Count.EqualTo(TargetService.MaxRangeDays));
        Assert.That((over.Status, over.Days), Is.EqualTo((TargetStatus.Invalid, Enumerable.Empty<DayTarget>())));
        Assert.That(huge.Status, Is.EqualTo(TargetStatus.Invalid));
        Assert.That(atMax.Status, Is.EqualTo(TargetStatus.Ok));
        Assert.That(atMax.Days.Single().Kcal, Is.EqualTo(2000));
        Assert.That(LoggedEvents.Of(logger).Count(l => l.Id == CoreEvent.TargetRangeRejected), Is.EqualTo(2));
    }

    [Test]
    public async Task Calls_LogCountsAndNoTargetValueOrDate()
    {
        await service.SaveAsync(2468);
        await service.ForDateAsync(Nov(3));
        await service.ForRangeAsync(Nov(1), Nov(15));
        await service.ForRangeAsync(Nov(15), Nov(1));
        await service.ClearAsync();

        var lines = LoggedEvents.Of(logger);
        Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[] { CoreEvent.TargetSaved, CoreEvent.TargetDateRead, CoreEvent.TargetRangeRead, CoreEvent.TargetRangeRejected, CoreEvent.TargetCleared }));
        Assert.That(lines[2].Message, Is.EqualTo("Daily targets read for 15 days"));
        var everything = string.Join(' ', lines.Select(l => l.Message))
            + string.Join(' ', logger.Collector.GetSnapshot().SelectMany(r => r.StructuredState ?? []).Select(p => p.Value));
        Assert.That(everything, Does.Not.Contain("2468").And.Not.Contain("2031").And.Not.Contain("-11-"));
    }
}
