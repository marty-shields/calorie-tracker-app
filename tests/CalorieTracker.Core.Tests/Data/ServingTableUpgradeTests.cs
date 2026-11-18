using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Summary;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 13 over a database built by the version before it (schema 12).</summary>
public class ServingTableUpgradeTests
{
    private static readonly DateOnly Day = new(2031, 7, 19);

    // The diary table as schema 12 had it, without ServingId.
    private const string DiaryTableV12 =
        "CREATE TABLE DiaryEntry (Id integer not null primary key autoincrement, Date varchar not null, Meal integer not null, FoodId integer not null, Amount float not null, Servings float, IsRemoved integer not null)";

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private int sliceId;
    private int canId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await BuildVersion12();
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    /// <summary>Foods as schema 12 stored them (the serving on the Food row) and a diary of entries added by servings and by weight.</summary>
    private async Task BuildVersion12()
    {
        await connection.CreateTableAsync<Food>();
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingLabel varchar");
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingAmount float");
        await connection.ExecuteAsync("ALTER TABLE Food ADD COLUMN ServingChecked integer");
        await connection.ExecuteAsync(SchemaSteps.FoodBarcodeTable);
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.ExecuteAsync(DiaryTableV12);
        var legacyFoods = new List<(Food Food, string? Label, double? Amount)>
        {
            // 1: a serving and a small pack: only the serving moves, the pack was never offered next to it.
            Food("slice", "Cheese slice", FoodUnit.Gram, 300, "1 slice (20 g)", 20, pack: 200),
            // 2: no serving, a can: gets a Pack row.
            Food("can", "Cola", FoodUnit.Millilitre, 42, null, null, pack: 330),
            // 3: no serving, a pack over the limit: gets nothing.
            Food("tub", "Big tub", FoodUnit.Gram, 250, null, null, pack: 1200),
            // 4 and 5: packs at the old limit and at the new one.
            Food("bar", "Bar", FoodUnit.Gram, 450, null, null, pack: 350),
            Food("bottle", "Bottle", FoodUnit.Millilitre, 40, null, null, pack: 1000),
            // 6: a CoFID food with no serving or pack.
            Food("11-001", "Oats", FoodUnit.Gram, 375, null, null, pack: null, source: FoodSource.CoFID),
            // 7: asked for a serving and the source had none.
            Food("none", "Plain", FoodUnit.Gram, 100, null, null, pack: null),
            // 8: a serving that was out of range (the reader drops it) moves as it is.
            Food("huge", "Huge", FoodUnit.Gram, 100, "1 huge", 99_999, pack: null),
        };
        await connection.InsertAllAsync(legacyFoods.Select(f => f.Food));
        foreach (var food in legacyFoods)
        {
            // The serving columns were dropped from the model, so schema 12's values are written by hand.
            await connection.ExecuteAsync("UPDATE Food SET ServingLabel = ?, ServingAmount = ? WHERE SourceId = ?", food.Label, food.Amount, food.Food.SourceId);
        }

        await connection.ExecuteAsync("UPDATE Food SET ServingChecked = 1 WHERE SourceId = 'none'");
        sliceId = (await connection.Table<Food>().FirstAsync(f => f.SourceId == "slice")).Id;
        canId = (await connection.Table<Food>().FirstAsync(f => f.SourceId == "can")).Id;
        await connection.ExecuteAsync(
            "INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, IsRemoved) VALUES (?, 1, ?, 30, 1.5, 0), (?, 1, ?, 330, NULL, 0), (?, 2, ?, 45, 3, 1)",
            "2031-07-19", sliceId, "2031-07-19", canId, "2031-07-19", sliceId);
        await connection.ExecuteAsync("PRAGMA user_version = 12");
    }

    private static (Food Food, string? Label, double? Amount) Food(string sourceId, string name, FoodUnit unit, double kcal, string? label, double? amount, double? pack, FoodSource source = FoodSource.OpenFoodFacts) => (new()
    {
        Source = source,
        SourceId = sourceId,
        Name = name,
        Unit = unit,
        Kcal = kcal,
        PackageAmount = pack,
        SearchText = name.ToLowerInvariant(),
    }, label, amount);

    private async Task Upgrade()
    {
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        var logger = new Microsoft.Extensions.Logging.Testing.FakeLogger<DatabaseInitializer>();
        await new DatabaseInitializer(connection, [.. SchemaSteps.Through(14), new NoStep15()], logger, 15).InitializeAsync();
    }

    /// <summary>Stands in for step 15, which adds packs this step's tests do not expect.</summary>
    private sealed class NoStep15 : ISchemaUpgrade
    {
        public int Version => 15;

        public void Apply(SQLiteConnection db)
        {
        }
    }

    private async Task<DaySummary> Summary()
    {
        var gate = new DatabaseGate();
        return (await new DaySummaryService(new DaySummaryStore(connection, gate), NullLogger<DaySummaryService>.Instance).GetAsync(Day)).Summary!;
    }

    private async Task<List<(string SourceId, string Label, double Amount, ServingSource Source)>> Rows() =>
        (await connection.QueryAsync<RowWithSource>("SELECT f.SourceId AS SourceId, s.Label AS Label, s.Amount AS Amount, s.Source AS Source FROM Serving s JOIN Food f ON f.Id = s.FoodId ORDER BY f.Id, s.SortOrder"))
        .Select(r => (r.SourceId, r.Label, r.Amount, (ServingSource)r.Source)).ToList();

    private sealed class SliceColumns
    {
        public string? ServingLabel { get; set; }

        public double? ServingAmount { get; set; }
    }

    private sealed class RowWithSource
    {
        public string SourceId { get; set; } = "";

        public string Label { get; set; } = "";

        public double Amount { get; set; }

        public int Source { get; set; }
    }

    [Test]
    public async Task Upgrade_AServingWithAZeroAmount_GetsNoProductRow_AndItsFoodGetsTheSmallPackRow()
    {
        await connection.InsertAsync(Food("zero", "Zero", FoodUnit.Gram, 100, null, null, pack: 200).Food);
        await connection.ExecuteAsync("UPDATE Food SET ServingLabel = '1 nothing', ServingAmount = 0 WHERE SourceId = 'zero'");

        await Upgrade();

        var rows = (await Rows()).Where(r => r.SourceId == "zero").ToList();
        Assert.That(rows.Select(r => (r.Source, r.Amount)), Is.EqualTo(new[] { (ServingSource.Pack, 200.0) }));
    }

    [Test]
    public async Task Upgrade_MovesEachFoodsServingIntoAProductRowAndAddsPackRowsWhereTheRuleOffersOne()
    {
        await Upgrade();

        Assert.That(await Rows(), Is.EqualTo(new[]
        {
            ("slice", "1 slice (20 g)", 20.0, ServingSource.Product),
            ("can", "Whole pack (330 ml)", 330.0, ServingSource.Pack),
            ("bar", "Whole pack (350 g)", 350.0, ServingSource.Pack),
            ("bottle", "Whole pack (1000 ml)", 1000.0, ServingSource.Pack),
            ("huge", "1 huge", 99_999.0, ServingSource.Product),
        }));
    }

    [Test]
    public async Task Upgrade_KeepsServingCheckedAndTheOldColumns()
    {
        await Upgrade();

        var checkedByFood = (await connection.QueryScalarsAsync<string>("SELECT SourceId || ':' || IFNULL(ServingChecked, '-') FROM Food WHERE SourceId IN ('none', 'slice') ORDER BY SourceId")).ToList();
        var sliceColumns = (await connection.QueryAsync<SliceColumns>("SELECT ServingLabel, ServingAmount FROM Food WHERE SourceId = 'slice'")).Single();
        Assert.Multiple(() =>
        {
            Assert.That(checkedByFood, Is.EqualTo(new[] { "none:1", "slice:-" }));
            Assert.That((sliceColumns.ServingLabel, sliceColumns.ServingAmount), Is.EqualTo(("1 slice (20 g)", (double?)20)));
        });
    }

    [Test]
    public async Task Upgrade_AddsTheEmptyServingIdColumn_AndDiaryEntriesKeepTheirAmountsCountsAndCalories()
    {
        var before = await Summary();

        await Upgrade();
        var after = await Summary();

        var entries = await connection.Table<DiaryEntry>().OrderBy(e => e.Id).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(e => e.ServingId), Is.All.Null);
            Assert.That(entries.Select(e => (e.Amount, e.Servings, e.IsRemoved)), Is.EqualTo(new[] { (30.0, (double?)1.5, false), (330.0, (double?)null, false), (45.0, (double?)3, true) }));
            Assert.That(after.Totals, Is.EqualTo(before.Totals));
            Assert.That(after.Meals.SelectMany(m => m.Entries).Select(e => (e.Amount, e.Servings, e.Nutrition)), Is.EqualTo(before.Meals.SelectMany(m => m.Entries).Select(e => (e.Amount, e.Servings, e.Nutrition))));
            Assert.That(after.Totals.Kcal, Is.EqualTo(before.Totals.Kcal).And.GreaterThan(0));
        });
    }

    [Test]
    public async Task Upgrade_ThenAddingByTheNewServingWorks_AndTheFoodsCaloriesAreStillItsOwn()
    {
        await Upgrade();
        var slice = (await connection.Table<Serving>().ToListAsync()).Single(s => s.FoodId == sliceId);
        var gate = new DatabaseGate();
        var diary = new DiaryService(new DiaryStore(connection, gate), NullLogger<DiaryService>.Instance);

        var added = await diary.AddAsync(Day, Meal.Dinner, sliceId, 40, 2, slice.Id);

        Assert.That((added.Status, added.Entry!.ServingId), Is.EqualTo((DiaryStatus.Ok, (int?)slice.Id)));
    }

    [Test]
    public async Task Upgrade_SetsTheVersionAndLogsTheStep()
    {
        await Upgrade();

        Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(15));
    }

    [Test]
    public async Task Apply_RunTwice_AddsNoRowsAndKeepsTheIds()
    {
        await connection.CloseAsync();
        var db = new SQLiteConnection(databasePath);
        try
        {
            new ServingTableUpgrade().Apply(db);
            var first = db.Query<Serving>("SELECT * FROM Serving ORDER BY Id");
            new ServingTableUpgrade().Apply(db);
            new ServingTableUpgrade().Apply(db);
            var again = db.Query<Serving>("SELECT * FROM Serving ORDER BY Id");

            Assert.That(again.Select(s => (s.Id, s.FoodId, s.Label, s.Amount, s.Source)), Is.EqualTo(first.Select(s => (s.Id, s.FoodId, s.Label, s.Amount, s.Source))));
            Assert.That(first, Has.Count.EqualTo(5));
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('DiaryEntry') WHERE name = 'ServingId'"), Is.EqualTo(1));
        }
        finally
        {
            db.Close();
        }

        connection = new SQLiteAsyncConnection(databasePath);
    }

    [Test]
    public async Task Apply_AfterAServingWasMovedAndThePersonAddedOne_KeepsTheirRow()
    {
        await connection.CloseAsync();
        var db = new SQLiteConnection(databasePath);
        try
        {
            new ServingTableUpgrade().Apply(db);
            db.Execute("INSERT INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (?, 'My slice', 25, 2, 2)", sliceId);
            new ServingTableUpgrade().Apply(db);

            Assert.That(db.Query<Serving>("SELECT * FROM Serving WHERE FoodId = ? ORDER BY SortOrder", sliceId).Select(s => s.Label), Is.EqualTo(new[] { "1 slice (20 g)", "My slice" }));
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
            new ServingTableUpgrade().Apply(db);
            new ServingTableUpgrade().Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'Serving'"), Is.EqualTo(1));
        }
        finally
        {
            db.Close();
            File.Delete(path);
        }
    }

    [Test]
    public async Task Upgrade_GivesTheSameServingSchemaAsANewDatabase()
    {
        await Upgrade();
        var upgraded = await Schema();
        await connection.CloseAsync();
        File.Delete(databasePath);

        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await new DatabaseInitializer(connection, SchemaSteps.All(), new Microsoft.Extensions.Logging.Testing.FakeLogger<DatabaseInitializer>()).InitializeAsync();

        Assert.That(await Schema(), Is.EqualTo(upgraded));
        Assert.That(upgraded, Does.Contain("Source:INTEGER:1:0").And.Contain("Amount:float:1:0"));
    }

    private async Task<List<string>> Schema()
    {
        var columns = await connection.QueryScalarsAsync<string>("SELECT name || ':' || type || ':' || \"notnull\" || ':' || pk FROM pragma_table_info('Serving') ORDER BY cid");
        var indexes = await connection.QueryScalarsAsync<string>(
            "SELECT i.name || ':' || i.\"unique\" || ':' || (SELECT group_concat(c.name) FROM pragma_index_info(i.name) c) FROM pragma_index_list('Serving') i WHERE i.origin = 'c' ORDER BY i.name");
        var diary = await connection.QueryScalarsAsync<string>("SELECT name || ':' || type || ':' || \"notnull\" FROM pragma_table_info('DiaryEntry') ORDER BY cid");
        return [.. columns, .. indexes, .. diary];
    }
}
