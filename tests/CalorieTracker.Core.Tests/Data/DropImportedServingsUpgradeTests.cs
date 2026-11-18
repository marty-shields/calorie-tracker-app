using CalorieTracker.Core.Data;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 18 over a database built by the version before it (schema 17).</summary>
public class DropImportedServingsUpgradeTests
{
    private const string Day = "2031-07-19";

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await BuildVersion17();
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    /// <summary>
    /// A bar with a Product row, a Pack row and an own row, and a cola with a Pack row only. Entries: one added by the bar's Product row (2 servings), one by its Pack row, one by its own row, one by weight.
    /// Both foods have the <c>ServingChecked</c> column step 8 added.
    /// </summary>
    private async Task BuildVersion17()
    {
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<Barcode>();
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingChecked integer");
        await connection.ExecuteAsync(
            "INSERT INTO Food (Id, Source, SourceId, Name, Unit, Kcal, SearchText, Type, ServingChecked) VALUES (1, 1, '5000000000017', 'Bar', 0, 430, 'bar', 1, NULL), (2, 1, '5000000000024', 'Cola', 1, 42, 'cola', 2, 1)");
        await connection.ExecuteAsync(
            "INSERT INTO Serving (Id, FoodId, Label, Amount, Source, SortOrder) VALUES (1, 1, '1 bar (40 g)', 40, 0, 0), (2, 1, 'Pack 200 g', 200, 1, 1), (3, 1, 'My bite', 10, 2, 2), (4, 2, 'Can 330 ml', 330, 1, 1)");
        await connection.ExecuteAsync(
            "INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, ServingId, IsRemoved) VALUES (?, 1, 1, 80, 2, 1, 0), (?, 1, 1, 200, 1, 2, 0), (?, 2, 1, 30, 3, 3, 0), (?, 3, 2, 100, NULL, NULL, 0)",
            Day, Day, Day, Day);
        await connection.ExecuteAsync("PRAGMA user_version = 17");
    }

    private async Task Upgrade()
    {
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        await new DatabaseInitializer(connection, SchemaSteps.All(), new FakeLogger<DatabaseInitializer>()).InitializeAsync();
    }

    private Task<List<string>> Rows(string sql) => connection.QueryScalarsAsync<string>(sql);

    [Test]
    public async Task Upgrade_RunsThroughTheInitializerFromSeventeenToEighteen()
    {
        await Upgrade();

        Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(18));
        Assert.That(DatabaseInitializer.CurrentVersion, Is.EqualTo(18));
    }

    [Test]
    public async Task Upgrade_DeletesEveryProductAndPackRow_AndLeavesTheUserRowsAsTheyWere()
    {
        var userBefore = await Rows("SELECT Id || '|' || FoodId || '|' || Label || '|' || Amount || '|' || Source || '|' || SortOrder FROM Serving WHERE Source = 2");

        await Upgrade();

        Assert.That(await Rows("SELECT COUNT(*) FROM Serving WHERE Source IN (0, 1)"), Is.EqualTo(new[] { "0" }));
        Assert.That(await Rows("SELECT Id || '|' || FoodId || '|' || Label || '|' || Amount || '|' || Source || '|' || SortOrder FROM Serving"), Is.EqualTo(userBefore));
        Assert.That(userBefore, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Upgrade_AnEntryAddedByAnImportedServingKeepsItsAmountServingsAndCalories_WithNoServingLink()
    {
        await Upgrade();

        Assert.That(
            await Rows("SELECT Meal || '|' || FoodId || '|' || Amount || '|' || IFNULL(Servings, '-') || '|' || IFNULL(ServingId, '-') || '|' || IsRemoved FROM DiaryEntry ORDER BY Id"),
            Is.EqualTo(new[] { "1|1|80.0|2.0|-|0", "1|1|200.0|1.0|-|0", "2|1|30.0|3.0|3|0", "3|2|100.0|-|-|0" }));
        Assert.That(await Rows("SELECT Kcal FROM Food ORDER BY Id"), Is.EqualTo(new[] { "430.0", "42.0" }));
    }

    [Test]
    public async Task Upgrade_DropsTheServingCheckedColumn_AndKeepsTheFoods()
    {
        await Upgrade();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'ServingChecked'"), Is.Zero);
        Assert.That(await Rows("SELECT Name || '|' || Kcal FROM Food ORDER BY Id"), Is.EqualTo(new[] { "Bar|430.0", "Cola|42.0" }));
    }

    [Test]
    public async Task Upgrade_RunTwice_ChangesNothingTheSecondTime()
    {
        await Upgrade();
        var schema = await Rows("SELECT name FROM sqlite_master ORDER BY name");
        var servings = await Rows("SELECT Id FROM Serving ORDER BY Id");

        await connection.RunInTransactionAsync(db => new DropImportedServingsUpgrade().Apply(db));

        Assert.That(await Rows("SELECT name FROM sqlite_master ORDER BY name"), Is.EqualTo(schema));
        Assert.That(await Rows("SELECT Id FROM Serving ORDER BY Id"), Is.EqualTo(servings));
    }

    [Test]
    public async Task Upgrade_DatabaseWithNoFoodServingOrDiaryEntryTable_Succeeds()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
        connection = new SQLiteAsyncConnection(databasePath);

        await connection.RunInTransactionAsync(db => new DropImportedServingsUpgrade().Apply(db));
        await connection.CreateTableAsync<Food>();
        await connection.RunInTransactionAsync(db => new DropImportedServingsUpgrade().Apply(db));
        await connection.CreateTableAsync<Serving>();
        await connection.RunInTransactionAsync(db => new DropImportedServingsUpgrade().Apply(db));

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Serving"), Is.Zero);
    }

    [Test]
    public async Task Upgrade_DatabaseWithServingsButNoDiaryTable_StillDeletesTheRows()
    {
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        await connection.RunInTransactionAsync(db => new DropImportedServingsUpgrade().Apply(db));

        Assert.That(await Rows("SELECT Label FROM Serving"), Is.EqualTo(new[] { "My bite" }));
    }
}
