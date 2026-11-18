using CalorieTracker.Core.Data;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 19 over a database built by the version before it (schema 18).</summary>
public class FavouriteTableUpgradeTests
{
    private const string FavouriteColumns = "SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('Favourite') ORDER BY cid";

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

    private async Task BuildVersion18()
    {
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<Barcode>();
        await connection.InsertAsync(new Food { Source = FoodSource.CoFID, SourceId = "x", Name = "Zebra pie", SearchText = "zebra pie" });
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = 1, Amount = 10 });
        await connection.ExecuteAsync("PRAGMA user_version = 18");
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
    }

    private Task Initialize() =>
        new DatabaseInitializer(connection, SchemaSteps.All(), new FakeLogger<DatabaseInitializer>()).InitializeAsync();

    [Test]
    public async Task Upgrade_CreatesTheEmptyTable_KeepsFoodsAndDiary_AndSetsVersion19()
    {
        await BuildVersion18();

        await Initialize();

        Assert.Multiple(async () =>
        {
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Favourite"), Is.Zero);
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Food"), Is.EqualTo(1));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry"), Is.EqualTo(1));
            Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(19));
        });
    }

    [Test]
    public async Task Upgrade_AndANewInstall_GiveTheSameTable()
    {
        await BuildVersion18();
        await Initialize();
        var upgraded = await connection.QueryScalarsAsync<string>(FavouriteColumns);
        await connection.CloseAsync();
        File.Delete(databasePath);
        connection = new SQLiteAsyncConnection(databasePath);

        await Initialize();

        Assert.Multiple(async () =>
        {
            Assert.That(upgraded, Is.EqualTo(new[] { "FoodId:INTEGER:1:1", "CreatedAt:bigint:1:0" }));
            Assert.That(await connection.QueryScalarsAsync<string>(FavouriteColumns), Is.EqualTo(upgraded));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Favourite"), Is.Zero);
        });
    }

    [Test]
    public async Task Apply_TwiceOrOnATableThatExists_KeepsItsRows()
    {
        await BuildVersion18();
        await connection.CloseAsync();
        var db = new SQLiteConnection(databasePath);
        try
        {
            new FavouriteTableUpgrade().Apply(db);
            db.Execute("INSERT INTO Favourite (FoodId, CreatedAt) VALUES (1, 5)");

            Assert.DoesNotThrow(() => new FavouriteTableUpgrade().Apply(db));
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM Favourite"), Is.EqualTo(1));
        }
        finally
        {
            db.Close();
        }
    }
}
