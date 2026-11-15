using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

public class DatabaseInitializerTests
{
    private readonly List<string> calls = [];
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<DatabaseInitializer> logger = null!;

    [SetUp]
    public void SetUp()
    {
        calls.Clear();
        logger = new FakeLogger<DatabaseInitializer>();
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_SetsTheCurrentVersionWithoutRunningSteps()
    {
        await Initializer(2, Step(2)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(2));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_NewDatabaseAtTheAppsVersion_NeedsNoSteps()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_OlderDatabase_RunsTheMissingStepsInOrder()
    {
        await SetVersion(1);

        await Initializer(3, Step(3), Step(2)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(3));
        Assert.That(calls, Is.EqualTo(new[] { "step 2", "step 3" }));
    }

    [Test]
    public async Task InitializeAsync_OlderDatabase_RunsOnlyTheStepsAfterTheStoredVersion()
    {
        await SetVersion(2);

        await Initializer(3, Step(2), Step(3)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(3));
        Assert.That(calls, Is.EqualTo(new[] { "step 3" }));
    }

    [Test]
    public async Task InitializeAsync_CurrentDatabase_RunsNoSteps()
    {
        await SetVersion(2);

        await Initializer(2, Step(2)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(2));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_LogsTheVersionsAndTheNewDatabase()
    {
        await Initializer(2, Step(2)).InitializeAsync();

        Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
        {
            (CoreEvent.SchemaVersion, LogLevel.Information, "Database schema version 0, app schema version 2"),
            (CoreEvent.DatabaseCreated, LogLevel.Information, "New database created at schema version 2"),
        }));
    }

    [Test]
    public async Task InitializeAsync_OlderDatabase_LogsTheVersionsAndEachStep()
    {
        await SetVersion(1);

        await Initializer(3, Step(2), Step(3)).InitializeAsync();

        Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
        {
            (CoreEvent.SchemaVersion, LogLevel.Information, "Database schema version 1, app schema version 3"),
            (CoreEvent.SchemaStepApplied, LogLevel.Information, "Schema upgrade step 2 applied"),
            (CoreEvent.SchemaStepApplied, LogLevel.Information, "Schema upgrade step 3 applied"),
        }));
    }

    [Test]
    public async Task InitializeAsync_NewerDatabase_ThrowsAndChangesNothing()
    {
        await SetVersion(3);

        Assert.That(() => Initializer(2, Step(2)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(3));
        Assert.That(calls, Is.Empty);
        Assert.That(LoggedEvents.Of(logger), Is.EqualTo(new[]
        {
            (CoreEvent.SchemaVersion, LogLevel.Information, "Database schema version 3, app schema version 2"),
        }));
    }

    [Test]
    public async Task InitializeAsync_MissingStep_ThrowsAndChangesNothing()
    {
        await SetVersion(1);

        Assert.That(() => Initializer(3, Step(3)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(1));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_DuplicateStep_ThrowsAndChangesNothing()
    {
        await SetVersion(1);

        Assert.That(() => Initializer(2, Step(2), Step(2)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(1));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_StepAboveTheCurrentVersion_ThrowsAndChangesNothing()
    {
        await SetVersion(1);

        Assert.That(() => Initializer(2, Step(2), Step(3)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(1));
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_NewDatabaseWithAMissingStep_ThrowsAndChangesNothing()
    {
        Assert.That(() => Initializer(3, Step(3)).InitializeAsync(), Throws.InvalidOperationException);

        Assert.That(await Version(), Is.Zero);
        Assert.That(calls, Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_FailingStep_RollsBackThatStepOnly()
    {
        await SetVersion(1);
        var failing = Step(3, db =>
        {
            db.Execute("INSERT INTO Marker (Id) VALUES (1)");
            throw new InvalidOperationException("Step failed");
        });

        Assert.That(() => Initializer(3, Step(2, db => db.Execute("CREATE TABLE Marker (Id INTEGER)")), failing).InitializeAsync(),
            Throws.InvalidOperationException);

        Assert.That(await Version(), Is.EqualTo(2));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Marker"), Is.Zero);
        Assert.That(calls, Is.EqualTo(new[] { "step 2", "step 3" }));
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_CreatesTheFoodTableAndIndexes()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        await AssertFoodTableAndIndexes();
    }

    [Test]
    public async Task InitializeAsync_ExistingDatabaseWithoutFood_CreatesTheFoodTableAndIndexes()
    {
        await SetVersion(1);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        await AssertFoodTableAndIndexes();
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_CreatesTheSeedVersionTable()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SeedVersion'"), Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_ExistingDatabaseWithoutSeedVersion_CreatesTheSeedVersionTable()
    {
        await SetVersion(1);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SeedVersion'"), Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_CreatesTheFoodBarcodeTableWithoutTheOldColumn()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FoodBarcode'"), Is.EqualTo(1));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Barcode'"), Is.Zero);
    }

    [Test]
    public async Task InitializeAsync_VersionOneDatabase_MovesBarcodesToTheFoodBarcodeTable()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Barcode varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null)");
        await connection.ExecuteAsync("CREATE INDEX Food_Barcode ON Food(Barcode)");
        await connection.ExecuteAsync("CREATE UNIQUE INDEX UX_Food_Source_SourceId ON Food(Source, SourceId)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Barcode, Unit, SearchText) VALUES (1, 1, 'a', 'A', '5000000000017', 0, 'a'), (2, 1, 'b', 'B', NULL, 0, 'b'), (3, 1, 'c', 'C', '5000000000017', 0, 'c'), (4, 1, 'd', 'D', '12345670', 0, 'd')");
        await SetVersion(1);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        var links = await connection.QueryAsync<FoodBarcode>("SELECT Barcode, FoodId FROM FoodBarcode ORDER BY Barcode");
        Assert.That(links.Select(l => (l.Barcode, l.FoodId)), Is.EqualTo(new[] { ("12345670", 4), ("5000000000017", 1) }));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Barcode'"), Is.Zero);
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Food"), Is.EqualTo(4));
        Assert.That(await connection.QueryScalarsAsync<string>("SELECT name FROM pragma_index_list('FoodBarcode')"), Does.Contain("FoodBarcode_FoodId"));
        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionTwoDatabase_GivesEveryStoredFoodATypeAndEnforcesNotNull()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null)");
        await connection.ExecuteAsync("CREATE UNIQUE INDEX UX_Food_Source_SourceId ON Food(Source, SourceId)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText) VALUES (1, 0, 'a', 'Cofid gram', 0, 'a'), (2, 0, 'b', 'Cofid ml', 1, 'b'), (3, 1, 'c', 'Off gram', 0, 'c'), (4, 1, 'd', 'Off ml', 1, 'd')");
        await SetVersion(2);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        var types = await connection.QueryScalarsAsync<int>("SELECT Type FROM Food ORDER BY Id");
        Assert.That(types, Is.EqualTo(new[] { (int)FoodType.Food, (int)FoodType.Food, (int)FoodType.Food, (int)FoodType.Drink }));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT \"notnull\" FROM pragma_table_info('Food') WHERE name = 'Type'"), Is.EqualTo(1));
        Assert.That(() => connection.ExecuteAsync("INSERT INTO Food (Source, SourceId, Name, Unit, SearchText, Type) VALUES (1, 'e', 'E', 0, 'e', NULL)"), Throws.Exception);
        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionTwoDatabaseWithNullTypes_FillsThemByTheSameRule()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null, Type integer)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText, Type) VALUES (1, 0, 'a', 'A', 0, 'a', NULL), (2, 1, 'b', 'B', 1, 'b', NULL), (3, 1, 'c', 'C', 0, 'c', NULL), (4, 1, 'd', 'D', 1, 'd', 2)");
        await SetVersion(2);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        var types = await connection.QueryScalarsAsync<int>("SELECT Type FROM Food ORDER BY Id");
        Assert.That(types, Is.EqualTo(new[] { (int)FoodType.Food, (int)FoodType.Drink, (int)FoodType.Food }));
    }

    [Test]
    public async Task InitializeAsync_VersionThreeDatabaseWithDishes_DeletesThemAndRenumbersDrink()
    {
        // Version 3 numbering: Ingredient 0, Food 1, Dish 2, Drink 3.
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null, Type integer not null DEFAULT 1)");
        await connection.ExecuteAsync("CREATE UNIQUE INDEX UX_Food_Source_SourceId ON Food(Source, SourceId)");
        await connection.ExecuteAsync("CREATE TABLE FoodBarcode (Barcode varchar primary key not null, FoodId integer not null)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText, Type) VALUES (1, 0, 'a', 'Ingredient', 0, 'a', 0), (2, 0, 'b', 'Dish', 0, 'b', 2), (3, 0, 'c', 'Food', 0, 'c', 1), (4, 0, 'd', 'Drink', 1, 'd', 3), (5, 0, 'e', 'Other dish', 0, 'e', 2)");
        await connection.ExecuteAsync("INSERT INTO FoodBarcode (Barcode, FoodId) VALUES ('5000000000017', 2), ('5000000000024', 4)");
        await SetVersion(3);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        var foods = await connection.QueryAsync<Food>("SELECT Id, Type FROM Food ORDER BY Id");
        Assert.That(foods.Select(f => (f.Id, f.Type)), Is.EqualTo(new[] { (1, FoodType.Ingredient), (3, FoodType.Food), (4, FoodType.Drink) }));
        Assert.That((int)FoodType.Drink, Is.EqualTo(2));
        Assert.That(await connection.QueryScalarsAsync<string>("SELECT Barcode FROM FoodBarcode"), Is.EqualTo(new[] { "5000000000024" }));
        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionThreeDatabaseWithNoFoodTable_RunsTheDishStepWithoutError()
    {
        await SetVersion(3);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionFourDatabase_GetsEmptyServingColumnsAndKeepsItsFoods()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null, Type integer not null)");
        await connection.ExecuteAsync("CREATE UNIQUE INDEX UX_Food_Source_SourceId ON Food(Source, SourceId)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText, Type) VALUES (1, 1, 'a', 'A', 0, 'a', 1), (2, 0, 'b', 'B', 1, 'b', 2)");
        await SetVersion(4);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        var foods = await connection.QueryAsync<Food>("SELECT * FROM Food ORDER BY Id");
        Assert.That(foods.Select(f => f.Name), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(foods.Select(f => (f.ServingLabel, f.ServingAmount)), Is.All.EqualTo(((string?)null, (double?)null)));
        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionFourDatabaseWithNoFoodTable_RunsTheServingStepWithoutError()
    {
        await SetVersion(4);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionFourDatabaseWithServingColumnsAlready_KeepsThemAndTheirValues()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, ServingLabel varchar, ServingAmount float, SearchText varchar not null, Type integer not null)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, ServingLabel, ServingAmount, SearchText, Type) VALUES (1, 1, 'a', 'A', 0, '1 slice (30 g)', 30, 'a', 1)");
        await SetVersion(4);

        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        var food = (await connection.QueryAsync<Food>("SELECT * FROM Food")).Single();
        Assert.That((food.ServingLabel, food.ServingAmount), Is.EqualTo(("1 slice (30 g)", 30.0)));
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_HasNullableServingColumns()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await connection.QueryScalarsAsync<int>("SELECT \"notnull\" FROM pragma_table_info('Food') WHERE name IN ('ServingLabel', 'ServingAmount')"), Is.EqualTo(new[] { 0, 0 }));
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_HasANotNullTypeColumnWithNoDefault()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT \"notnull\" FROM pragma_table_info('Food') WHERE name = 'Type'"), Is.EqualTo(1));
        Assert.That(await connection.ExecuteScalarAsync<string?>("SELECT dflt_value FROM pragma_table_info('Food') WHERE name = 'Type'"), Is.Null);
    }

    [Test]
    public async Task InitializeAsync_VersionOneDatabase_GetsTheSameFoodBarcodeSchemaAsANewDatabase()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();
        var created = await FoodBarcodeSchema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Barcode varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null)");
        await connection.ExecuteAsync("CREATE UNIQUE INDEX UX_Food_Source_SourceId ON Food(Source, SourceId)");
        await SetVersion(1);
        await Initializer(DatabaseInitializer.CurrentVersion, new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade()).InitializeAsync();

        Assert.That(await FoodBarcodeSchema(), Is.EqualTo(created));
    }

    private async Task<List<string>> FoodBarcodeSchema()
    {
        var columns = await connection.QueryScalarsAsync<string>(
            "SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('FoodBarcode') ORDER BY cid");
        var indexes = await connection.QueryScalarsAsync<string>(
            "SELECT i.name || ':' || i.\"unique\" || ':' || (SELECT group_concat(c.name) FROM pragma_index_info(i.name) c) FROM pragma_index_list('FoodBarcode') i WHERE i.origin = 'c' ORDER BY i.name");
        return [.. columns, .. indexes];
    }

    [Test]
    public async Task Sqlite_BundledVersion_SupportsDropColumn()
    {
        var version = await connection.ExecuteScalarAsync<string>("SELECT sqlite_version()");

        Assert.That(System.Version.Parse(version), Is.GreaterThanOrEqualTo(new System.Version(3, 35)));
        TestContext.Out.WriteLine($"sqlite_version() = {version}");
    }

    private async Task AssertFoodTableAndIndexes()
    {
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'"), Is.EqualTo(1));
        var unique = await connection.QueryScalarsAsync<string>("SELECT name FROM pragma_index_list('Food') WHERE \"unique\" = 1");
        var all = await connection.QueryScalarsAsync<string>("SELECT name FROM pragma_index_list('Food')");
        Assert.That(unique, Does.Contain("UX_Food_Source_SourceId"));
        Assert.That(all, Does.Not.Contain("Food_Barcode"));
    }

    [Test]
    public async Task FoodServingUpgrade_VersionFourFoodTable_GainsBothNullableServingColumns()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Type integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null)");
        await connection.ExecuteAsync("INSERT INTO Food (Source, SourceId, Name, Unit, Type, SearchText) VALUES (1, 'a', 'A', 0, 0, 'a')");

        using (var db = new SQLiteConnection(databasePath))
        {
            new FoodServingUpgrade().Apply(db);
        }

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name IN ('ServingLabel', 'ServingAmount') AND \"notnull\" = 0"), Is.EqualTo(2));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Food WHERE ServingLabel IS NULL AND ServingAmount IS NULL"), Is.EqualTo(1));
    }

    private DatabaseInitializer Initializer(int currentVersion, params ISchemaUpgrade[] steps) =>
        new(connection, steps, logger, currentVersion);

    private FakeStep Step(int version, Action<SQLiteConnection>? apply = null) => new(version, db =>
    {
        calls.Add($"step {version}");
        apply?.Invoke(db);
    });

    private Task SetVersion(int version) => connection.ExecuteAsync($"PRAGMA user_version = {version}");

    private Task<int> Version() => connection.ExecuteScalarAsync<int>("PRAGMA user_version");

    private sealed class FakeStep(int version, Action<SQLiteConnection> apply) : ISchemaUpgrade
    {
        public int Version => version;

        public void Apply(SQLiteConnection db) => apply(db);
    }
}
