using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Targets;

public class TargetServiceTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeLogger<TargetService> logger = null!;
    private TargetService service = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<AppSettings>();
        gate = new DatabaseGate();
        logger = new FakeLogger<TargetService>();
        service = new TargetService(new TargetStore(connection, gate), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task GetAsync_NothingSaved_ReturnsOkWithNoTarget()
    {
        var result = await service.GetAsync();

        Assert.That((result.Status, result.Kcal), Is.EqualTo((TargetStatus.Ok, (int?)null)));
    }

    [Test]
    public async Task SaveAsync_WholeNumber_IsReadBack()
    {
        var saved = await service.SaveAsync(2000);

        Assert.That((saved.Status, saved.Kcal), Is.EqualTo((TargetStatus.Ok, (int?)2000)));
        Assert.That((await service.GetAsync()).Kcal, Is.EqualTo(2000));
    }

    [Test]
    public async Task SaveAsync_AnotherValue_ReplacesTheFirstInTheOneRow()
    {
        await service.SaveAsync(2000);
        await service.SaveAsync(1800);

        Assert.That((await service.GetAsync()).Kcal, Is.EqualTo(1800));
        Assert.That(await connection.Table<AppSettings>().CountAsync(), Is.EqualTo(1));
    }

    [TestCase(1.0, 1)]
    [TestCase(2000.0, 2000)]
    [TestCase((double)int.MaxValue, int.MaxValue)]
    public async Task SaveAsync_EdgeOfTheValidRange_Saves(double value, int expected)
    {
        Assert.That((await service.SaveAsync(value)).Kcal, Is.EqualTo(expected));
    }

    [Test]
    public async Task ClearAsync_ASavedTarget_LeavesNone()
    {
        await service.SaveAsync(2000);

        var cleared = await service.ClearAsync();

        Assert.That((cleared.Status, cleared.Kcal), Is.EqualTo((TargetStatus.Ok, (int?)null)));
        Assert.That((await service.GetAsync()).Kcal, Is.Null);
    }

    [Test]
    public async Task ClearAsync_NothingSaved_Succeeds()
    {
        Assert.That((await service.ClearAsync()).Status, Is.EqualTo(TargetStatus.Ok));
    }

    [TestCase(0.0)]
    [TestCase(-1.0)]
    [TestCase(-2000.0)]
    [TestCase(0.5)]
    [TestCase(1999.5)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    [TestCase(3000000000.0)]
    public async Task SaveAsync_NotAWholeNumberAboveZero_ReturnsInvalidAndKeepsTheTarget(double value)
    {
        await service.SaveAsync(2000);

        var result = await service.SaveAsync(value);

        Assert.That((result.Status, result.Kcal), Is.EqualTo((TargetStatus.Invalid, (int?)null)));
        Assert.That((await service.GetAsync()).Kcal, Is.EqualTo(2000));
    }

    [Test]
    public async Task EveryCall_WhenTheDatabaseFails_ReturnsFailedInsteadOfThrowing()
    {
        await connection.ExecuteAsync("DROP TABLE AppSettings");

        Assert.That((await service.GetAsync()).Status, Is.EqualTo(TargetStatus.Failed));
        Assert.That((await service.SaveAsync(2000)).Status, Is.EqualTo(TargetStatus.Failed));
        Assert.That((await service.ClearAsync()).Status, Is.EqualTo(TargetStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.TargetFailed, CoreEvent.TargetFailed, CoreEvent.TargetFailed }));
    }

    [Test]
    public async Task SaveAsyncAndClearAsync_ChangeOnlyTheTargetColumn()
    {
        await connection.ExecuteAsync("ALTER TABLE AppSettings ADD COLUMN Other integer");
        await connection.ExecuteAsync("INSERT INTO AppSettings (Id, Other) VALUES (?, 42)", AppSettings.RowId);

        await service.SaveAsync(2000);
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT Other FROM AppSettings"), Is.EqualTo(42));

        await service.ClearAsync();
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT Other FROM AppSettings"), Is.EqualTo(42));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppSettings"), Is.EqualTo(1));
    }

    [Test]
    public async Task Calls_LogOnlyEventsAndNoNumberTheUserSet()
    {
        await service.SaveAsync(2468);
        await service.GetAsync();
        await service.SaveAsync(1357.5);
        await service.SaveAsync(-9753);
        await service.ClearAsync();

        var lines = LoggedEvents.Of(logger);
        Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[] { CoreEvent.TargetSaved, CoreEvent.TargetRejected, CoreEvent.TargetRejected, CoreEvent.TargetCleared }));
        Assert.That(lines.Select(l => l.Message), Has.None.Matches(@"\d"));
        Assert.That(logger.Collector.GetSnapshot().SelectMany(r => r.StructuredState ?? []).Select(p => p.Value), Has.None.Matches(@"\d"));
    }

    [Test]
    public async Task SaveAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(new AppSettings { DailyTargetKcal = 100 });
        });

        var save = service.SaveAsync(250);
        holding.SetResult();

        await busy;
        Assert.That((await save).Status, Is.EqualTo(TargetStatus.Ok));
        Assert.That((await connection.GetAsync<AppSettings>(AppSettings.RowId)).DailyTargetKcal, Is.EqualTo(250));
    }

    [Test]
    public async Task GetAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(new AppSettings { DailyTargetKcal = 100 });
        });

        var get = service.GetAsync();
        holding.SetResult();

        await busy;
        Assert.That((await get).Kcal, Is.EqualTo(100));
    }

    [Test]
    public async Task ClearAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(new AppSettings { DailyTargetKcal = 100 });
        });

        var clear = service.ClearAsync();
        holding.SetResult();

        await busy;
        Assert.That((await clear).Status, Is.EqualTo(TargetStatus.Ok));
        Assert.That((await connection.GetAsync<AppSettings>(AppSettings.RowId)).DailyTargetKcal, Is.Null);
    }
}
