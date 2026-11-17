using CalorieTracker.Core.Data;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 17 over a database built by the version before it (schema 16).</summary>
public class OnlineSearchNoticeUpgradeTests
{
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

    private async Task BuildVersion16(bool withColumn)
    {
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<Barcode>();
        await connection.ExecuteAsync("CREATE TABLE \"AppSettings\" (\"Id\" integer not null primary key, \"DailyTargetKcal\" integer)");
        if (withColumn)
        {
            await connection.ExecuteAsync("ALTER TABLE AppSettings ADD COLUMN OnlineSearchNoticeSeen INTEGER");
        }

        await connection.ExecuteAsync("INSERT INTO AppSettings (Id, DailyTargetKcal) VALUES (1, 2100)");
        await connection.ExecuteAsync("PRAGMA user_version = 16");
    }

    private async Task Upgrade()
    {
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        await new DatabaseInitializer(connection, SchemaSteps.All(), new FakeLogger<DatabaseInitializer>()).InitializeAsync();
    }

    [Test]
    public async Task Upgrade_AddsTheEmptyColumn_KeepsTheTarget_AndSetsVersion17()
    {
        await BuildVersion16(withColumn: false);

        await Upgrade();

        Assert.Multiple(async () =>
        {
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('AppSettings') WHERE name = 'OnlineSearchNoticeSeen'"), Is.EqualTo(1));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT DailyTargetKcal FROM AppSettings WHERE Id = 1"), Is.EqualTo(2100));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppSettings WHERE OnlineSearchNoticeSeen IS NOT NULL"), Is.Zero);
            Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(17));
        });
    }

    [Test]
    public async Task Upgrade_OnADatabaseThatAlreadyHasTheColumn_KeepsItsValue()
    {
        await BuildVersion16(withColumn: true);
        await connection.ExecuteAsync("UPDATE AppSettings SET OnlineSearchNoticeSeen = 1");

        await Upgrade();

        Assert.Multiple(async () =>
        {
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT OnlineSearchNoticeSeen FROM AppSettings WHERE Id = 1"), Is.EqualTo(1));
            Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(17));
        });
    }

    [Test]
    public void Apply_WithNoAppSettingsTable_ChangesNothing()
    {
        var db = new SQLiteConnection(databasePath);
        try
        {
            Assert.DoesNotThrow(() => new OnlineSearchNoticeUpgrade().Apply(db));
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'AppSettings'"), Is.Zero);
        }
        finally
        {
            db.Close();
        }
    }
}
