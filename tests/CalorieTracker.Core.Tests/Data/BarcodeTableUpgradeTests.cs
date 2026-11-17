using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Summary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 14 (and the steps after it) over a database built by the version before it (schema 13).</summary>
public class BarcodeTableUpgradeTests
{
    private const string ColaCode = "5449000214911";
    private const string PlainCode = "5000000000017";
    private const string LostLinkCode = "5000000000024";
    private const string ExtraCode = "5000000000031";
    private static readonly DateOnly Day = new(2031, 7, 19);
    private static readonly DateTimeOffset Fetched = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StepTime = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private int colaId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await BuildVersion13();
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    /// <summary>Foods as schema 13 stored them: barcodes in FoodBarcode, the pack on the Food row, and a diary that uses them.</summary>
    private async Task BuildVersion13()
    {
        await connection.CreateTableAsync<Food>();
        await connection.ExecuteAsync(SchemaSteps.FoodBarcodeTable);
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.InsertAllAsync(new List<Food>
        {
            // 1: an Open Food Facts food with a pack, fetched, with its link.
            Food(ColaCode, "Coca-Cola Original", FoodUnit.Millilitre, 42, pack: 330, label: "330ml", fetched: Fetched),
            // 2: stored before details were fetched: no pack, no fetch time.
            Food(PlainCode, "Plain", FoodUnit.Gram, 100, pack: null, label: null, fetched: null),
            // 3: its link row was lost, but its source id is the barcode.
            Food(LostLinkCode, "Lost link", FoodUnit.Gram, 200, pack: 250, label: "250 g", fetched: Fetched),
            // 4: CoFID, no barcode.
            Food("11-001", "Oats", FoodUnit.Gram, 375, pack: null, label: null, fetched: null, source: FoodSource.CoFID),
        });
        colaId = (await connection.Table<Food>().FirstAsync(f => f.SourceId == ColaCode)).Id;
        var plainId = (await connection.Table<Food>().FirstAsync(f => f.SourceId == PlainCode)).Id;
        await connection.ExecuteAsync(
            "INSERT INTO FoodBarcode (Barcode, FoodId) VALUES (?, ?), (?, ?), (?, ?)",
            ColaCode, colaId, PlainCode, plainId, ExtraCode, colaId);
        await connection.ExecuteAsync(
            "INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, IsRemoved) VALUES (?, 1, ?, 330, NULL, 0), (?, 2, ?, 45, 3, 1), (?, 3, ?, 100, NULL, 0)",
            "2031-07-19", colaId, "2031-07-19", plainId, "2031-07-19", plainId);
        await connection.ExecuteAsync("PRAGMA user_version = 13");
    }

    private static Food Food(string sourceId, string name, FoodUnit unit, double kcal, double? pack, string? label, DateTimeOffset? fetched, FoodSource source = FoodSource.OpenFoodFacts) => new()
    {
        Source = source,
        SourceId = sourceId,
        Name = name,
        Unit = unit,
        Kcal = kcal,
        PackageAmount = pack,
        PackageLabel = label,
        FetchedAt = fetched,
        SearchText = name.ToLowerInvariant(),
    };

    private async Task Upgrade()
    {
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        ISchemaUpgrade[] steps = [.. SchemaSteps.Through(15).Where(s => s.Version != 14), new BarcodeTableUpgrade(new FakeTimeProvider(StepTime))];
        await new DatabaseInitializer(connection, steps, new FakeLogger<DatabaseInitializer>(), 15).InitializeAsync();
    }

    private Task<List<Barcode>> Rows() => connection.QueryAsync<Barcode>("SELECT * FROM Barcode ORDER BY Id");

    [Test]
    public async Task Upgrade_GivesEveryStoredBarcodeARow_AndTheOpenFoodFactsFoodsTheirSourceIdWithTheirPack()
    {
        await Upgrade();

        var rows = (await Rows()).ToDictionary(r => r.Code);
        Assert.Multiple(() =>
        {
            Assert.That(rows.Keys, Is.EquivalentTo(new[] { ColaCode, PlainCode, LostLinkCode, ExtraCode }));
            Assert.That((rows[ColaCode].FoodId, rows[ColaCode].PackageAmount, rows[ColaCode].PackageLabel), Is.EqualTo((colaId, (double?)330, "330ml")));
            Assert.That((rows[PlainCode].PackageAmount, rows[PlainCode].PackageLabel), Is.EqualTo(((double?)null, (string?)null)));
            Assert.That((rows[LostLinkCode].PackageAmount, rows[LostLinkCode].PackageLabel), Is.EqualTo(((double?)250, "250 g")));
            Assert.That(rows[ExtraCode].FoodId, Is.EqualTo(colaId));
            Assert.That((rows[ExtraCode].PackageAmount, rows[ExtraCode].PackageLabel), Is.EqualTo(((double?)null, (string?)null)), "an extra link was never given a pack of its own");
        });
    }

    [Test]
    public async Task Upgrade_ABarcodeTwoFoodsShare_PointsToTheLowerFoodId()
    {
        var plainId = (await connection.Table<Food>().FirstAsync(f => f.SourceId == PlainCode)).Id;
        // The table has the barcode as its key, so the step's defence for a database that lacks it needs a table without one. Inserted with the higher food id first, so row order alone would pick the wrong food.
        await connection.ExecuteAsync("DROP TABLE FoodBarcode");
        await connection.ExecuteAsync("CREATE TABLE FoodBarcode (Barcode varchar not null, FoodId integer not null)");
        await connection.ExecuteAsync("INSERT INTO FoodBarcode (Barcode, FoodId) VALUES (?, ?), (?, ?)", "5000000000055", plainId, "5000000000055", colaId);

        await Upgrade();

        var row = (await Rows()).Single(r => r.Code == "5000000000055");
        Assert.That(row.FoodId, Is.EqualTo(Math.Min(colaId, plainId)));
    }

    [Test]
    public async Task Upgrade_FirstSeenIsTheFetchTimeOrTheTimeOfTheStep()
    {
        await Upgrade();

        var rows = (await Rows()).ToDictionary(r => r.Code);
        Assert.Multiple(() =>
        {
            Assert.That(rows[ColaCode].FirstSeen, Is.EqualTo(Fetched));
            Assert.That(rows[PlainCode].FirstSeen, Is.EqualTo(StepTime));
            Assert.That(rows[ExtraCode].FirstSeen, Is.EqualTo(Fetched));
        });
    }

    [Test]
    public async Task Upgrade_EveryBarcodeStillResolvesToItsFood_AndTheOldTableIsLeftAlone()
    {
        await Upgrade();
        var store = new FoodStore(connection, new DatabaseGate(), NullLogger<FoodStore>.Instance);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await store.FindByBarcodeAsync(ColaCode))?.SourceId, Is.EqualTo(ColaCode));
            Assert.That((await store.FindByBarcodeAsync(ExtraCode))?.Id, Is.EqualTo(colaId));
            Assert.That((await store.FindByBarcodeAsync(PlainCode))?.SourceId, Is.EqualTo(PlainCode));
            Assert.That((await store.FindByBarcodeAsync(LostLinkCode))?.SourceId, Is.EqualTo(LostLinkCode));
            Assert.That(await store.FindByBarcodeAsync("5000000000048"), Is.Null);
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM FoodBarcode"), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Upgrade_DiaryEntriesAndTheirCaloriesAreUnchanged()
    {
        var before = await Summary();

        await Upgrade();
        var after = await Summary();

        Assert.Multiple(() =>
        {
            Assert.That(after.Totals, Is.EqualTo(before.Totals));
            Assert.That(after.Totals.Kcal, Is.GreaterThan(0));
            Assert.That(after.Meals.SelectMany(m => m.Entries).Select(e => (e.Amount, e.Servings, e.Nutrition)), Is.EqualTo(before.Meals.SelectMany(m => m.Entries).Select(e => (e.Amount, e.Servings, e.Nutrition))));
        });
    }

    private async Task<DaySummary> Summary() =>
        (await new DaySummaryService(new DaySummaryStore(connection, new DatabaseGate()), NullLogger<DaySummaryService>.Instance).GetAsync(Day)).Summary!;

    [Test]
    public async Task Upgrade_SetsTheVersion_AndAScanOfAnExistingBarcodeStillFindsItsFoodWithoutAnImport()
    {
        await Upgrade();
        var store = new FoodStore(connection, new DatabaseGate(), NullLogger<FoodStore>.Instance);
        var importer = new FoodImportService(store, TimeProvider.System, NullLogger<FoodImportService>.Instance);
        var again = new Food { Source = FoodSource.OpenFoodFacts, SourceId = ColaCode, Name = "Coca-Cola Original", Unit = FoodUnit.Millilitre, Kcal = 42 };

        var result = await importer.ImportAsync(ColaCode, again);

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(15));
            Assert.That((result.Status, result.Food!.Id, result.PackAmount), Is.EqualTo((FoodImportStatus.Known, colaId, (double?)330)));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Apply_RunTwice_AddsNoRowsAndKeepsTheIds()
    {
        await connection.CloseAsync();
        var db = new SQLiteConnection(databasePath);
        try
        {
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);
            var first = db.Query<Barcode>("SELECT * FROM Barcode ORDER BY Id");
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);
            var again = db.Query<Barcode>("SELECT * FROM Barcode ORDER BY Id");

            Assert.That(again.Select(b => (b.Id, b.FoodId, b.Code, b.PackageAmount, b.PackageLabel, b.FirstSeen)), Is.EqualTo(first.Select(b => (b.Id, b.FoodId, b.Code, b.PackageAmount, b.PackageLabel, b.FirstSeen))));
            Assert.That(first, Has.Count.EqualTo(4));
        }
        finally
        {
            db.Close();
        }

        connection = new SQLiteAsyncConnection(databasePath);
    }

    [Test]
    public async Task Apply_AfterABarcodeWasAttachedToAnotherFood_KeepsItThere()
    {
        await connection.CloseAsync();
        var db = new SQLiteConnection(databasePath);
        try
        {
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);
            db.Execute("UPDATE Barcode SET FoodId = ? WHERE Barcode = ?", colaId, PlainCode);
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT FoodId FROM Barcode WHERE Barcode = ?", PlainCode), Is.EqualTo(colaId));
        }
        finally
        {
            db.Close();
        }

        connection = new SQLiteAsyncConnection(databasePath);
    }

    [Test]
    public void Apply_OnAnEmptyDatabase_CreatesTheTableAndDoesNotFail()
    {
        var path = TempDatabase.NewPath();
        var db = new SQLiteConnection(path);
        try
        {
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'Barcode'"), Is.EqualTo(1));
        }
        finally
        {
            db.Close();
            File.Delete(path);
        }
    }

    [Test]
    public async Task Apply_OnADatabaseWithoutTheOldTableOrColumns_FillsFromTheSourceIdsAlone()
    {
        await connection.ExecuteAsync("DROP TABLE FoodBarcode");
        await connection.CloseAsync();
        var db = new SQLiteConnection(databasePath);
        try
        {
            new BarcodeTableUpgrade(TimeProvider.System).Apply(db);

            Assert.That(db.QueryScalars<string>("SELECT Barcode FROM Barcode ORDER BY Barcode"), Is.EqualTo(new[] { PlainCode, LostLinkCode, ColaCode }.Order()));
        }
        finally
        {
            db.Close();
        }

        connection = new SQLiteAsyncConnection(databasePath);
    }

    [Test]
    public async Task Upgrade_GivesTheSameBarcodeSchemaAsANewDatabase()
    {
        await Upgrade();
        var upgraded = await Schema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await new DatabaseInitializer(connection, SchemaSteps.Through(15), new FakeLogger<DatabaseInitializer>(), 15).InitializeAsync();

        Assert.That(await Schema(), Is.EqualTo(upgraded));
        Assert.That(upgraded, Does.Contain("UX_Barcode_Barcode:1:Barcode").And.Contain("Barcode_FoodId:0:FoodId"));
    }

    [Test]
    public async Task TheBarcodeColumnIsUnique()
    {
        await Upgrade();

        Assert.That(
            async () => await connection.ExecuteAsync("INSERT INTO Barcode (FoodId, Barcode, FirstSeen) VALUES (?, ?, 0)", colaId, ColaCode),
            Throws.InstanceOf<SQLiteException>());
    }

    private async Task<List<string>> Schema()
    {
        var columns = await connection.QueryScalarsAsync<string>("SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('Barcode') ORDER BY cid");
        var indexes = await connection.QueryScalarsAsync<string>(
            "SELECT i.name || ':' || i.\"unique\" || ':' || (SELECT group_concat(c.name) FROM pragma_index_info(i.name) c) FROM pragma_index_list('Barcode') i WHERE i.origin = 'c' ORDER BY i.name");
        return [.. columns, .. indexes];
    }
}
