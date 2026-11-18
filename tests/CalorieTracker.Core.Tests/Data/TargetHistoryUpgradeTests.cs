using CalorieTracker.Core.Data;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 20 over a database built by the version before it (schema 19).</summary>
public class TargetHistoryUpgradeTests
{
    private const string HistoryColumns = "SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('TargetHistory') ORDER BY cid";

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;

    [SetUp]
    public void SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private async Task BuildVersion19(int? target)
    {
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<Barcode>();
        await connection.CreateTableAsync<Favourite>();
        await connection.InsertAsync(new Food { Source = FoodSource.CoFID, SourceId = "x", Name = "Zebra pie", SearchText = "zebra pie" });
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = 1, Amount = 10 });
        await connection.InsertAsync(new Favourite { FoodId = 1, CreatedAt = DateTimeOffset.UnixEpoch });
        if (target is not null)
        {
            await connection.InsertAsync(new AppSettings { DailyTargetKcal = target });
        }

        await connection.ExecuteAsync("PRAGMA user_version = 19");
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
    }

    private Task Initialize() =>
        new DatabaseInitializer(connection, SchemaSteps.All(), new FakeLogger<DatabaseInitializer>()).InitializeAsync();

    private TargetService Service() =>
        new(new TargetStore(connection, new DatabaseGate()), TimeProvider.System, new FakeLogger<TargetService>());

    [Test]
    public async Task Upgrade_WithATarget_GivesEveryPastDateThatTarget_AndKeepsFoodsDiaryAndFavourites()
    {
        await BuildVersion19(2100);

        await Initialize();

        var service = Service();
        Assert.Multiple(async () =>
        {
            Assert.That((await service.ForDateAsync(new DateOnly(1, 1, 1))).Kcal, Is.EqualTo(2100));
            Assert.That((await service.ForDateAsync(new DateOnly(2020, 3, 4))).Kcal, Is.EqualTo(2100));
            Assert.That((await service.ForDateAsync(new DateOnly(2999, 1, 1))).Kcal, Is.EqualTo(2100));
            Assert.That(await connection.QueryScalarsAsync<string>("SELECT EffectiveFrom FROM TargetHistory"), Is.EqualTo(new[] { "0001-01-01" }));
            Assert.That((await service.GetAsync()).Kcal, Is.EqualTo(2100));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Food"), Is.EqualTo(1));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry"), Is.EqualTo(1));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Favourite"), Is.EqualTo(1));
            Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(20));
        });
    }

    [Test]
    public async Task Upgrade_WithoutATarget_GivesNoDateOne_AndAClearedSettingWritesNoRow()
    {
        await BuildVersion19(null);

        await Initialize();

        var service = Service();
        Assert.Multiple(async () =>
        {
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TargetHistory"), Is.Zero);
            Assert.That((await service.ForDateAsync(new DateOnly(2020, 3, 4))).Kcal, Is.Null);
            Assert.That((await service.ForDateAsync(new DateOnly(2020, 3, 4))).Status, Is.EqualTo(TargetStatus.Ok));
        });
    }

    [Test]
    public async Task Upgrade_AndANewInstall_GiveTheSameTable_TheNewOneEmpty()
    {
        await BuildVersion19(null);
        await Initialize();
        var upgraded = await connection.QueryScalarsAsync<string>(HistoryColumns);
        await connection.CloseAsync();
        File.Delete(databasePath);
        connection = new SQLiteAsyncConnection(databasePath);

        await Initialize();

        Assert.Multiple(async () =>
        {
            Assert.That(upgraded, Is.EqualTo(new[] { "EffectiveFrom:varchar:1:1", "Kcal:float:0:0" }));
            Assert.That(await connection.QueryScalarsAsync<string>(HistoryColumns), Is.EqualTo(upgraded));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TargetHistory"), Is.Zero);
        });
    }

    [Test]
    public async Task Apply_Twice_KeepsOneRowAndExistingRows()
    {
        await BuildVersion19(2100);
        await connection.CloseAsync();
        var db = new SQLiteConnection(databasePath);
        try
        {
            new TargetHistoryUpgrade().Apply(db);
            db.Execute("INSERT INTO TargetHistory (EffectiveFrom, Kcal) VALUES ('2031-01-01', 1500)");

            Assert.DoesNotThrow(() => new TargetHistoryUpgrade().Apply(db));
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM TargetHistory"), Is.EqualTo(2));
        }
        finally
        {
            db.Close();
        }
    }
}
