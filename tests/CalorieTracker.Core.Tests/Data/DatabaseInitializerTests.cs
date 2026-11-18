using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Targets;
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
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

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
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        await AssertFoodTableAndIndexes();
    }

    [Test]
    public async Task InitializeAsync_ExistingDatabaseWithoutFood_CreatesTheFoodTableAndIndexes()
    {
        await SetVersion(1);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        await AssertFoodTableAndIndexes();
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_CreatesTheSeedVersionTable()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SeedVersion'"), Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_CreatesTheSettingsTableWithNoRow()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AppSettings'"), Is.EqualTo(1));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppSettings"), Is.Zero);
        var target = await new TargetService(new TargetStore(connection, new DatabaseGate()), TimeProvider.System, new FakeLogger<TargetService>()).GetAsync();
        Assert.That((target.Status, target.Kcal), Is.EqualTo((TargetStatus.Ok, (int?)null)));
    }

    [Test]
    public async Task InitializeAsync_VersionFiveDatabase_GetsTheSameSettingsSchemaAsANewDatabase()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();
        var created = await SettingsSchema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await SetVersion(5);
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await SettingsSchema(), Is.EqualTo(created));
    }

    private async Task<List<string>> SettingsSchema() =>
        await connection.QueryScalarsAsync<string>(
            "SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('AppSettings') ORDER BY cid");

    [Test]
    public async Task InitializeAsync_ExistingDatabaseWithoutSeedVersion_CreatesTheSeedVersionTable()
    {
        await SetVersion(1);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SeedVersion'"), Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_HasNoFoodBarcodeTableAndNoOldBarcodeColumn()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FoodBarcode'"), Is.Zero);
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

        await Initializer(15, SchemaSteps.Through(15)).InitializeAsync();

        var links = await connection.QueryAsync<Barcode>("SELECT Barcode, FoodId FROM FoodBarcode ORDER BY Barcode");
        Assert.That(links.Select(l => (l.Code, l.FoodId)), Is.EqualTo(new[] { ("12345670", 4), ("5000000000017", 1) }));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Barcode'"), Is.Zero);
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Food"), Is.EqualTo(4));
        Assert.That(await connection.QueryScalarsAsync<string>("SELECT name FROM pragma_index_list('FoodBarcode')"), Does.Contain("FoodBarcode_FoodId"));
        Assert.That(await Version(), Is.EqualTo(15));
    }

    [Test]
    public async Task InitializeAsync_VersionTwoDatabase_GivesEveryStoredFoodATypeAndEnforcesNotNull()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, SearchText varchar not null)");
        await connection.ExecuteAsync("CREATE UNIQUE INDEX UX_Food_Source_SourceId ON Food(Source, SourceId)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText) VALUES (1, 0, 'a', 'Cofid gram', 0, 'a'), (2, 0, 'b', 'Cofid ml', 1, 'b'), (3, 1, 'c', 'Off gram', 0, 'c'), (4, 1, 'd', 'Off ml', 1, 'd')");
        await SetVersion(2);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

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

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

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

        await Initializer(15, SchemaSteps.Through(15)).InitializeAsync();

        var foods = await connection.QueryAsync<Food>("SELECT Id, Type FROM Food ORDER BY Id");
        Assert.That(foods.Select(f => (f.Id, f.Type)), Is.EqualTo(new[] { (1, FoodType.Ingredient), (3, FoodType.Food), (4, FoodType.Drink) }));
        Assert.That((int)FoodType.Drink, Is.EqualTo(2));
        Assert.That(await connection.QueryScalarsAsync<string>("SELECT Barcode FROM FoodBarcode"), Is.EqualTo(new[] { "5000000000024" }));
        Assert.That(await Version(), Is.EqualTo(15));
    }

    [Test]
    public async Task InitializeAsync_VersionThreeDatabaseWithNoFoodTable_RunsTheDishStepWithoutError()
    {
        await SetVersion(3);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

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

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        var foods = await connection.QueryAsync<Food>("SELECT * FROM Food ORDER BY Id");
        Assert.That(foods.Select(f => f.Name), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name IN ('ServingLabel', 'ServingAmount')"), Is.EqualTo(0), "step 16 drops the columns");
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Serving"), Is.EqualTo(0));
        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionFourDatabaseWithNoFoodTable_RunsTheServingStepWithoutError()
    {
        await SetVersion(4);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionFourDatabaseWithServingColumnsAlready_KeepsThemAndTheirValues()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, ServingLabel varchar, ServingAmount float, SearchText varchar not null, Type integer not null)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, ServingLabel, ServingAmount, SearchText, Type) VALUES (1, 1, 'a', 'A', 0, '1 slice (30 g)', 30, 'a', 1)");
        await SetVersion(4);

        await Initializer(15, SchemaSteps.Through(15)).InitializeAsync();

        var food = (await connection.QueryAsync<LegacyFoodRow>("SELECT * FROM Food")).Single();
        Assert.That((food.ServingLabel, food.ServingAmount), Is.EqualTo(("1 slice (30 g)", 30.0)));
    }

    [Test]
    public void SettingsTableUpgrade_Apply_CreatesTheTableWithNoRow()
    {
        // Runs the step alone: the initializer's later CreateTableAsync would hide a broken step.
        var db = new SQLiteConnection(databasePath);
        try
        {
            new SettingsTableUpgrade().Apply(db);
            new SettingsTableUpgrade().Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AppSettings'"), Is.EqualTo(1));
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM AppSettings"), Is.Zero);
            Assert.That(
                db.QueryScalars<string>("SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('AppSettings') ORDER BY cid"),
                Is.EqualTo(new[] { "Id:INTEGER:1:1", "DailyTargetKcal:INTEGER:0:0" }));
        }
        finally
        {
            db.Close();
        }
    }

    [Test]
    public async Task InitializeAsync_VersionFiveDatabaseWithFoods_GetsTheSettingsTableWithNoTargetAndKeepsItsFoods()
    {
        await connection.ExecuteAsync(
            "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, ServingLabel varchar, ServingAmount float, SearchText varchar not null, Type integer not null)");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText, Type) VALUES (1, 1, 'a', 'A', 0, 'a', 1)");
        await SetVersion(5);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Message), Does.Contain("Schema upgrade step 6 applied"));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppSettings"), Is.Zero);
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
    }

    private const string DiaryColumns = "SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('DiaryEntry') ORDER BY cid";

    private static readonly string[] ExpectedDiaryColumns =
        ["Id:INTEGER:1:1", "Date:varchar:1:0", "Meal:INTEGER:1:0", "FoodId:INTEGER:1:0", "Amount:float:1:0", "Servings:float:0:0", "IsRemoved:INTEGER:1:0", "ServingId:INTEGER:0:0"];

    private const string FoodTableSql =
        "CREATE TABLE Food (Id integer primary key autoincrement not null, Source integer not null, SourceId varchar not null, Name varchar not null, Brand varchar, Unit integer not null, Kcal float, ProteinG float, CarbsG float, FatG float, FibreG float, SugarsG float, SaturatesG float, SaltG float, ServingLabel varchar, ServingAmount float, SearchText varchar not null, Type integer not null)";

    private const string DiaryTableSql =
        "CREATE TABLE DiaryEntry (Id integer not null primary key autoincrement, Date varchar not null, Meal integer not null, FoodId integer not null, Amount float not null, Servings float, IsRemoved integer not null)";

    [Test]
    public async Task InitializeAsync_NewDatabase_CreatesTheDiaryTable()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.QueryScalarsAsync<string>(DiaryColumns), Is.EqualTo(ExpectedDiaryColumns));
        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
    }

    [Test]
    public async Task InitializeAsync_VersionSixDatabaseWithData_GetsTheDiaryTableAndKeepsItsData()
    {
        await connection.ExecuteAsync(FoodTableSql);
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText, Type) VALUES (1, 1, 'a', 'A', 0, 'a', 1)");
        await connection.ExecuteAsync("CREATE TABLE FoodBarcode (Barcode varchar primary key not null, FoodId integer not null)");
        await connection.ExecuteAsync("INSERT INTO FoodBarcode (Barcode, FoodId) VALUES ('5000000000017', 1)");
        await connection.ExecuteAsync("CREATE TABLE AppSettings (Id integer not null primary key, DailyTargetKcal integer)");
        await connection.ExecuteAsync("INSERT INTO AppSettings (Id, DailyTargetKcal) VALUES (1, 2100)");
        await SetVersion(6);

        await Initializer(15, SchemaSteps.Through(15)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(15));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Message), Does.Contain("Schema upgrade step 7 applied"));
        Assert.That(await connection.QueryScalarsAsync<string>(DiaryColumns), Is.EqualTo(ExpectedDiaryColumns));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry"), Is.Zero);
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        Assert.That(await connection.ExecuteScalarAsync<string>("SELECT Barcode FROM FoodBarcode"), Is.EqualTo("5000000000017"));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT DailyTargetKcal FROM AppSettings"), Is.EqualTo(2100));
    }

    [Test]
    public async Task InitializeAsync_VersionSixDatabase_GetsTheSameDiarySchemaAsANewDatabase()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();
        var created = await DiarySchema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.ExecuteAsync(FoodTableSql);
        await SetVersion(6);
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await DiarySchema(), Is.EqualTo(created));
        Assert.That(created, Has.Count.EqualTo(ExpectedDiaryColumns.Length + 2));
    }

    private async Task<List<string>> DiarySchema()
    {
        var columns = await connection.QueryScalarsAsync<string>(DiaryColumns);
        var indexes = await connection.QueryScalarsAsync<string>(
            "SELECT i.name || ':' || i.\"unique\" || ':' || (SELECT group_concat(c.name) FROM pragma_index_info(i.name) c) FROM pragma_index_list('DiaryEntry') i WHERE i.origin = 'c' ORDER BY i.name");
        return [.. columns, .. indexes];
    }

    [Test]
    public void DiaryTableUpgrade_Apply_CreatesTheTableWithNoRowAndItsIndexes()
    {
        // Runs the step alone: the initializer's later CreateTableAsync would hide a broken step.
        var db = new SQLiteConnection(databasePath);
        try
        {
            new DiaryTableUpgrade().Apply(db);
            new DiaryTableUpgrade().Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM DiaryEntry"), Is.Zero);
            Assert.That(db.QueryScalars<string>(DiaryColumns), Is.EqualTo(ExpectedDiaryColumns[..^1]));
            Assert.That(
                db.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = 'DiaryEntry' AND sql IS NOT NULL ORDER BY name"),
                Is.EqualTo(new[] { "DiaryEntry_Date", "DiaryEntry_FoodId" }));
        }
        finally
        {
            db.Close();
        }
    }

    [Test]
    public void CurrentVersion_IsTwenty()
    {
        Assert.That(DatabaseInitializer.CurrentVersion, Is.EqualTo(20));
    }

    private const string ServingCheckedColumn = "SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('Food') WHERE name = 'ServingChecked'";

    [Test]
    public async Task InitializeAsync_NewDatabase_HasNoServingCheckedColumn()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.QueryScalarsAsync<string>(ServingCheckedColumn), Is.Empty);
    }

    [Test]
    public async Task InitializeAsync_VersionSevenDatabaseWithData_GetsTheServingCheckedColumnEmptyAndKeepsItsData()
    {
        await connection.ExecuteAsync(FoodTableSql);
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, Kcal, ServingLabel, ServingAmount, SearchText, Type) VALUES (1, 1, 'a', 'A', 0, 120, '1 slice (30 g)', 30, 'a', 1), (2, 1, 'b', 'B', 1, 40, NULL, NULL, 'b', 2)");
        await connection.ExecuteAsync("CREATE TABLE FoodBarcode (Barcode varchar primary key not null, FoodId integer not null)");
        await connection.ExecuteAsync("INSERT INTO FoodBarcode (Barcode, FoodId) VALUES ('5000000000017', 2)");
        await connection.ExecuteAsync("CREATE TABLE AppSettings (Id integer not null primary key, DailyTargetKcal integer)");
        await connection.ExecuteAsync("INSERT INTO AppSettings (Id, DailyTargetKcal) VALUES (1, 2100)");
        await connection.ExecuteAsync(DiaryTableSql);
        await connection.ExecuteAsync("INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, IsRemoved) VALUES ('2031-07-19', 1, 1, 30, 1, 0)");
        await SetVersion(7);

        await Initializer(15, SchemaSteps.Through(15)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(15));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Message), Does.Contain("Schema upgrade step 8 applied"));
        var foods = await connection.QueryAsync<LegacyFoodRow>("SELECT * FROM Food ORDER BY Id");
        Assert.That(foods.Select(f => (f.Name, f.Kcal, f.ServingLabel, f.ServingAmount, f.ServingChecked)),
            Is.EqualTo(new[] { ("A", 120.0, (string?)"1 slice (30 g)", (double?)30, (bool?)null), ("B", 40.0, null, null, null) }));
        Assert.That(await connection.ExecuteScalarAsync<string>("SELECT Barcode FROM FoodBarcode"), Is.EqualTo("5000000000017"));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT DailyTargetKcal FROM AppSettings"), Is.EqualTo(2100));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry"), Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_VersionSevenDatabase_GetsTheSameFoodSchemaAsANewDatabase()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();
        var created = await FoodSchema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.ExecuteAsync(FoodTableSql);
        await SetVersion(7);
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await FoodSchema(), Is.EqualTo(created));
        Assert.That(created, Has.None.Contain("ServingChecked"), "step 18 dropped the column step 8 added");
    }

    [Test]
    public async Task InitializeAsync_VersionEightDatabaseWithSeedLoaded_GetsEmptyColumnsAndForgetsOnlyTheCoFidSeedVersion()
    {
        await connection.ExecuteAsync(FoodTableSql);
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingChecked INTEGER");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, Kcal, SearchText, Type) VALUES (1, 0, '11-002', 'Pearl barley', 0, 360, 'pearl barley', 0), (2, 1, '5000000000017', 'B', 0, 40, 'b', 1)");
        await connection.ExecuteAsync("CREATE TABLE SeedVersion (Source integer not null primary key, Version integer not null)");
        await connection.ExecuteAsync("INSERT INTO SeedVersion (Source, Version) VALUES (0, 6), (1, 3)");
        await connection.ExecuteAsync("CREATE TABLE FoodBarcode (Barcode varchar primary key not null, FoodId integer not null)");
        await connection.ExecuteAsync("CREATE TABLE AppSettings (Id integer not null primary key, DailyTargetKcal integer)");
        await connection.ExecuteAsync(DiaryTableSql);
        await connection.ExecuteAsync("INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, IsRemoved) VALUES ('2031-07-19', 1, 1, 30, 1, 0)");
        await SetVersion(8);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(DatabaseInitializer.CurrentVersion));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Message), Does.Contain("Schema upgrade step 9 applied"));
        var foods = await connection.Table<Food>().OrderBy(f => f.Id).ToListAsync();
        Assert.That(foods.Select(f => (f.Id, f.Name, f.OriginalName, f.FoodGroup)),
            Is.EqualTo(new[] { (1, "Pearl barley", (string?)null, (string?)null), (2, "B", null, null) }));
        Assert.That(await connection.QueryScalarsAsync<int>("SELECT Source FROM SeedVersion"), Is.EqualTo(new[] { 1 }));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry WHERE FoodId = 1"), Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_VersionEightDatabase_GetsTheSameFoodSchemaAsANewDatabase()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();
        var created = await FoodSchema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.ExecuteAsync(FoodTableSql);
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingChecked INTEGER");
        await SetVersion(8);
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await FoodSchema(), Is.EqualTo(created));
        Assert.That(created, Does.Contain("OriginalName:varchar:0:0").And.Contain("FoodGroup:varchar:0:0"));
    }

    [Test]
    public void FoodOriginalNameUpgrade_Apply_RunTwiceOrWithNoTables_DoesNotFail()
    {
        var db = new SQLiteConnection(databasePath);
        try
        {
            new FoodOriginalNameUpgrade().Apply(db);
            db.Execute(FoodTableSql);
            new FoodOriginalNameUpgrade().Apply(db);
            new FoodOriginalNameUpgrade().Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name IN ('OriginalName', 'FoodGroup')"), Is.EqualTo(2));
        }
        finally
        {
            db.Close();
        }
    }

    [Test]
    public async Task InitializeAsync_VersionNineDatabaseWithFoods_GetsEmptyDetailColumnsAndKeepsItsFoods()
    {
        await connection.ExecuteAsync(FoodTableSql);
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingChecked INTEGER");
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN OriginalName varchar");
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN FoodGroup varchar");
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Brand, Unit, Kcal, ServingLabel, ServingAmount, SearchText, Type) VALUES (1, 1, '3017620422003', 'Nutella', 'Ferrero', 0, 539, '15 g', 15, 'nutella ferrero', 1), (2, 0, '11-002', 'Pearl barley', NULL, 0, 360, NULL, NULL, 'pearl barley', 0)");
        await connection.ExecuteAsync("CREATE TABLE FoodBarcode (Barcode varchar primary key not null, FoodId integer not null)");
        await connection.ExecuteAsync("INSERT INTO FoodBarcode (Barcode, FoodId) VALUES ('3017620422003', 1)");
        await connection.ExecuteAsync("CREATE TABLE AppSettings (Id integer not null primary key, DailyTargetKcal integer)");
        await connection.ExecuteAsync(DiaryTableSql);
        await connection.ExecuteAsync("INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, IsRemoved) VALUES ('2031-07-19', 1, 1, 30, 1, 0)");
        await SetVersion(9);

        await Initializer(15, SchemaSteps.Through(15)).InitializeAsync();

        Assert.That(await Version(), Is.EqualTo(15));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Message), Does.Contain("Schema upgrade step 10 applied"));
        var foods = await connection.QueryAsync<LegacyFoodRow>("SELECT * FROM Food ORDER BY Id");
        Assert.That(foods.Select(f => (f.Id, f.Name, f.Brand, f.Kcal, f.ServingLabel, f.ServingAmount)),
            Is.EqualTo(new[] { (1, "Nutella", (string?)"Ferrero", 539.0, (string?)"15 g", (double?)15), (2, "Pearl barley", null, 360.0, null, null) }));
        Assert.That(foods.Select(f => (f.Brands, f.GenericName, f.PackageLabel, f.PackageAmount, f.Category, f.Popularity, f.Completeness, f.SourceUpdated, f.FetchedAt)),
            Is.All.EqualTo(((string?)null, (string?)null, (string?)null, (double?)null, (string?)null, (int?)null, (double?)null, (DateTimeOffset?)null, (DateTimeOffset?)null)));
        Assert.That(await connection.ExecuteScalarAsync<string>("SELECT Barcode FROM FoodBarcode WHERE FoodId = 1"), Is.EqualTo("3017620422003"));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry WHERE FoodId = 1"), Is.EqualTo(1));
    }

    [Test]
    public async Task InitializeAsync_VersionNineDatabase_GetsTheSameFoodSchemaAsANewDatabase()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();
        var created = await FoodSchema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.ExecuteAsync(FoodTableSql);
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingChecked INTEGER");
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN OriginalName varchar");
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN FoodGroup varchar");
        await SetVersion(9);
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await FoodSchema(), Is.EqualTo(created));
        Assert.That(created, Does.Contain("FetchedAt:bigint:0:0").And.Contain("PackageAmount:float:0:0").And.Contain("Popularity:INTEGER:0:0"));
    }

    [Test]
    public void FoodSourceDetailsUpgrade_Apply_RunTwiceOrWithNoTable_DoesNotFail()
    {
        var db = new SQLiteConnection(databasePath);
        try
        {
            new FoodSourceDetailsUpgrade().Apply(db);
            db.Execute(FoodTableSql);
            new FoodSourceDetailsUpgrade().Apply(db);
            new FoodSourceDetailsUpgrade().Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name IN ('Brands', 'GenericName', 'PackageLabel', 'PackageAmount', 'Category', 'Popularity', 'Completeness', 'SourceUpdated', 'FetchedAt')"), Is.EqualTo(9));
        }
        finally
        {
            db.Close();
        }
    }

    private async Task<List<string>> FoodSchema() =>
        await connection.QueryScalarsAsync<string>(
            "SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('Food') ORDER BY name");

    [Test]
    public void FoodServingCheckedUpgrade_Apply_AddsTheColumnOnceAndKeepsItsValues()
    {
        // Runs the step alone: the initializer's later CreateTableAsync would hide a broken step.
        var db = new SQLiteConnection(databasePath);
        try
        {
            db.Execute(FoodTableSql);
            db.Execute("INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText, Type) VALUES (1, 1, 'a', 'A', 0, 'a', 1)");

            new FoodServingCheckedUpgrade().Apply(db);
            db.Execute("UPDATE Food SET ServingChecked = 1");
            new FoodServingCheckedUpgrade().Apply(db);

            Assert.That(db.QueryScalars<string>(ServingCheckedColumn), Is.EqualTo(new[] { "ServingChecked:INTEGER:0:0" }));
            Assert.That(db.ExecuteScalar<int>("SELECT ServingChecked FROM Food"), Is.EqualTo(1));
        }
        finally
        {
            db.Close();
        }
    }

    [Test]
    public void FoodServingCheckedUpgrade_Apply_WithNoFoodTable_DoesNothing()
    {
        var db = new SQLiteConnection(databasePath);
        try
        {
            new FoodServingCheckedUpgrade().Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'Food'"), Is.Zero);
        }
        finally
        {
            db.Close();
        }
    }

    [Test]
    public async Task EveryStepFromTheDiaryTableOn_AppliedToADatabaseWithDiaryEntries_KeepsEveryFoodItsBarcodeAndTheEntries()
    {
        // Only steps from the diary table's version on can meet a diary entry in a real upgrade, so only those run, on the current schema.
        // A later step that deletes foods fails this test.
        await Initializer(15, SchemaSteps.Through(15)).InitializeAsync();
        await connection.ExecuteAsync(SchemaSteps.FoodBarcodeTable);
        var foodIds = new List<int>();
        var id = 0;
        foreach (var type in Enum.GetValues<FoodType>())
        {
            foreach (var source in Enum.GetValues<FoodSource>())
            {
                foreach (var unit in Enum.GetValues<FoodUnit>())
                {
                    id++;
                    foodIds.Add(id);
                    await connection.ExecuteAsync($"INSERT INTO Food (Id, Source, SourceId, Name, Unit, SearchText, Type) VALUES ({id}, {(int)source}, 'f{id}', 'F{id}', {(int)unit}, 'f{id}', {(int)type})");
                    await connection.ExecuteAsync($"INSERT INTO FoodBarcode (Barcode, FoodId) VALUES ('50000000{id:D5}', {id})");
                    await connection.ExecuteAsync($"INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, IsRemoved) VALUES ('2031-07-19', 0, {id}, 10, 0)");
                }
            }
        }

        await connection.CloseAsync();

        var db = new SQLiteConnection(databasePath);
        try
        {
            var diaryVersion = new DiaryTableUpgrade().Version;
            foreach (var step in SchemaSteps.All().Where(s => s.Version >= diaryVersion && s.Version <= 15).OrderBy(s => s.Version))
            {
                step.Apply(db);
            }

            Assert.That(db.QueryScalars<int>("SELECT Id FROM Food ORDER BY Id"), Is.EqualTo(foodIds));
            Assert.That(db.QueryScalars<int>("SELECT FoodId FROM FoodBarcode ORDER BY FoodId"), Is.EqualTo(foodIds));
            Assert.That(db.QueryScalars<int>("SELECT FoodId FROM DiaryEntry ORDER BY FoodId"), Is.EqualTo(foodIds));

            // Step 16 drops FoodBarcode, so every barcode must already be in Barcode.
            new StorageCleanupUpgrade().Apply(db);
            Assert.That(db.QueryScalars<int>("SELECT Id FROM Food ORDER BY Id"), Is.EqualTo(foodIds));
            Assert.That(db.QueryScalars<int>("SELECT DISTINCT FoodId FROM Barcode ORDER BY FoodId"), Is.EqualTo(foodIds));
            Assert.That(db.QueryScalars<int>("SELECT FoodId FROM DiaryEntry ORDER BY FoodId"), Is.EqualTo(foodIds));
        }
        finally
        {
            db.Close();
            connection = new SQLiteAsyncConnection(databasePath);
        }
    }

    [Test]
    public void EveryStepThatDeletesFoods_IsOlderThanTheDiaryTable()
    {
        // Add any new step that deletes foods to this list.
        var foodDeletingSteps = new ISchemaUpgrade[] { new DropDishesUpgrade() };
        var diaryVersion = new DiaryTableUpgrade().Version;

        foreach (var step in foodDeletingSteps)
        {
            Assert.That(
                step.Version,
                Is.LessThan(diaryVersion),
                $"{step.GetType().Name} deletes foods, so it must be older than the diary table (step {diaryVersion}): no step older than the diary table may meet a diary entry, and a diary entry must never lose its food.");
        }
    }

    [Test]
    public async Task InitializeAsync_ExistingCurrentVersionDatabaseWithoutDiaryEntry_CreatesTheDiaryTable()
    {
        await connection.ExecuteAsync(FoodTableSql);
        await SetVersion(DatabaseInitializer.CurrentVersion);

        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.QueryScalarsAsync<string>(DiaryColumns), Is.EqualTo(ExpectedDiaryColumns));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry"), Is.Zero);
    }

    [Test]
    public async Task InitializeAsync_NewDatabase_HasANotNullTypeColumnWithNoDefault()
    {
        await Initializer(DatabaseInitializer.CurrentVersion, SchemaSteps.All()).InitializeAsync();

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT \"notnull\" FROM pragma_table_info('Food') WHERE name = 'Type'"), Is.EqualTo(1));
        Assert.That(await connection.ExecuteScalarAsync<string?>("SELECT dflt_value FROM pragma_table_info('Food') WHERE name = 'Type'"), Is.Null);
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

    /// <summary>A Food row as an older schema stored it, with the serving columns step 16 later dropped.</summary>
    private sealed class LegacyFoodRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public string? Brand { get; set; }

        public double Kcal { get; set; }

        public string? ServingLabel { get; set; }

        public double? ServingAmount { get; set; }

        public bool? ServingChecked { get; set; }

        public string? Brands { get; set; }

        public string? GenericName { get; set; }

        public string? PackageLabel { get; set; }

        public double? PackageAmount { get; set; }

        public string? Category { get; set; }

        public int? Popularity { get; set; }

        public double? Completeness { get; set; }

        public DateTimeOffset? SourceUpdated { get; set; }

        public DateTimeOffset? FetchedAt { get; set; }
    }

    private sealed class FakeStep(int version, Action<SQLiteConnection> apply) : ISchemaUpgrade
    {
        public int Version => version;

        public void Apply(SQLiteConnection db) => apply(db);
    }
}
