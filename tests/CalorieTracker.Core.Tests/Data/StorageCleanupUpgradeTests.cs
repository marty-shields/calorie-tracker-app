using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 16 over a database built by the version before it (schema 15).</summary>
public class StorageCleanupUpgradeTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await BuildVersion15();
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    /// <summary>
    /// Foods as schema 15 left them, still with the serving columns and the FoodBarcode table: a bar with a Product serving row and the same serving on its row, a pizza with the columns and no Product row (a serving that was removed),
    /// and two diary entries that use the bar's serving and one that has no serving.
    /// </summary>
    private async Task BuildVersion15()
    {
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<Barcode>();
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingLabel varchar");
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingAmount float");
        await connection.ExecuteAsync(SchemaSteps.FoodBarcodeTable);
        await connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS \"FoodBarcode_FoodId\" ON \"FoodBarcode\"(\"FoodId\")");
        await connection.ExecuteAsync(
            "INSERT INTO Food (Id, Source, SourceId, Name, Unit, Kcal, ServingLabel, ServingAmount, SearchText, Type) VALUES (1, 1, '5000000000017', 'Bar', 0, 430, '1 bar', 40, 'bar', 1), (2, 1, '5000000000024', 'Pizza', 0, 250, '1 slice', 100, 'pizza', 1), (3, 0, '11-002', 'Barley', 0, 360, NULL, NULL, 'barley', 1)");
        await connection.ExecuteAsync("INSERT INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (1, '1 bar', 40, 0, 0), (1, 'Pack 200 g', 200, 1, 1)");
        await connection.ExecuteAsync("INSERT INTO Barcode (FoodId, Barcode, PackageAmount, PackageLabel, FirstSeen) VALUES (1, '5000000000017', 200, '200 g', 0), (2, '5000000000024', NULL, NULL, 0)");
        await connection.ExecuteAsync("INSERT INTO FoodBarcode (Barcode, FoodId) VALUES ('5000000000017', 1), ('5000000000024', 2)");
        await connection.ExecuteAsync(
            "INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, ServingId, IsRemoved) VALUES (?, 1, 1, 80, 2, (SELECT Id FROM Serving WHERE FoodId = 1 AND Source = 0), 0), (?, 2, 1, 200, NULL, (SELECT Id FROM Serving WHERE FoodId = 1 AND Source = 1), 0), (?, 3, 2, 100, NULL, NULL, 0)",
            "2031-07-19", "2031-07-19", "2031-07-19");
        await connection.ExecuteAsync("PRAGMA user_version = 15");
    }

    private async Task Upgrade()
    {
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        await new DatabaseInitializer(connection, SchemaSteps.All(), new FakeLogger<DatabaseInitializer>()).InitializeAsync();
    }

    private Task<List<string>> Rows(string sql) => connection.QueryScalarsAsync<string>(sql);

    [Test]
    public async Task Upgrade_DropsTheFoodBarcodeTableAndTheTwoServingColumns()
    {
        await Upgrade();

        Assert.Multiple(async () =>
        {
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'FoodBarcode'"), Is.Zero);
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'FoodBarcode_FoodId'"), Is.Zero);
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name IN ('ServingLabel', 'ServingAmount')"), Is.Zero);
            Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(17));
        });
    }

    [Test]
    public async Task Upgrade_KeepsFoodsServingsBarcodesAndEveryDiaryEntryAsTheyWere()
    {
        var diaryBefore = await Rows("SELECT Date || '|' || Meal || '|' || FoodId || '|' || Amount || '|' || IFNULL(Servings, '-') || '|' || IFNULL(ServingId, '-') || '|' || IsRemoved FROM DiaryEntry ORDER BY Id");
        var servingsBefore = await Rows("SELECT Id || '|' || FoodId || '|' || Label || '|' || Amount || '|' || Source FROM Serving ORDER BY Id");
        var barcodesBefore = await Rows("SELECT Barcode || '|' || FoodId || '|' || IFNULL(PackageAmount, '-') FROM Barcode ORDER BY Id");

        await Upgrade();

        Assert.Multiple(async () =>
        {
            Assert.That(await Rows("SELECT Date || '|' || Meal || '|' || FoodId || '|' || Amount || '|' || IFNULL(Servings, '-') || '|' || IFNULL(ServingId, '-') || '|' || IsRemoved FROM DiaryEntry ORDER BY Id"), Is.EqualTo(diaryBefore));
            Assert.That(await Rows("SELECT Id || '|' || FoodId || '|' || Label || '|' || Amount || '|' || Source FROM Serving ORDER BY Id"), Is.EqualTo(servingsBefore));
            Assert.That(await Rows("SELECT Barcode || '|' || FoodId || '|' || IFNULL(PackageAmount, '-') FROM Barcode ORDER BY Id"), Is.EqualTo(barcodesBefore));
            Assert.That(await Rows("SELECT Name || '|' || Kcal FROM Food ORDER BY Id"), Is.EqualTo(new[] { "Bar|430.0", "Pizza|250.0", "Barley|360.0" }));
        });
    }

    [Test]
    public async Task Upgrade_AfterwardsTheStoreReadsTheProductServingFromTheServingRow()
    {
        await Upgrade();
        var store = new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>());

        var bar = (await store.FindWithBarcodeAsync("5000000000017")).Serving;
        var pizza = (await store.FindWithBarcodeAsync("5000000000024")).Serving;

        Assert.Multiple(() =>
        {
            Assert.That(bar, Is.EqualTo(new ProductServing("1 bar", 40)));
            Assert.That(pizza, Is.Null);
        });
    }

    [Test]
    public async Task Upgrade_RunTwice_ChangesNothingTheSecondTime()
    {
        await Upgrade();
        var schemaAfterFirst = await Rows("SELECT name FROM sqlite_master ORDER BY name");

        await connection.RunInTransactionAsync(db => new StorageCleanupUpgrade().Apply(db));

        Assert.That(await Rows("SELECT name FROM sqlite_master ORDER BY name"), Is.EqualTo(schemaAfterFirst));
    }

    [Test]
    public async Task Upgrade_DatabaseWithoutTheTableOrColumns_Succeeds()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
        connection = new SQLiteAsyncConnection(databasePath);

        await connection.RunInTransactionAsync(db => new StorageCleanupUpgrade().Apply(db));
        await connection.CreateTableAsync<Food>();
        await connection.RunInTransactionAsync(db => new StorageCleanupUpgrade().Apply(db));

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'FoodBarcode'"), Is.Zero);
    }

    [Test]
    public async Task NewDatabase_HasNoFoodBarcodeTableAndNoServingColumns()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
        connection = new SQLiteAsyncConnection(databasePath);

        await new DatabaseInitializer(connection, SchemaSteps.All(), new FakeLogger<DatabaseInitializer>()).InitializeAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'FoodBarcode'"), Is.Zero);
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name IN ('ServingLabel', 'ServingAmount')"), Is.Zero);
        });
    }
}
