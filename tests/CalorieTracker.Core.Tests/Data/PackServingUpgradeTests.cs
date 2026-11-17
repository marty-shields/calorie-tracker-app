using CalorieTracker.Core.Data;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

/// <summary>Step 15 over a database built by the version before it (schema 14).</summary>
public class PackServingUpgradeTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private int colaId;
    private int cordialId;
    private int jarId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await BuildVersion14();
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private async Task<int> InsertFood(string sourceId, string name, FoodUnit unit, double? pack, string? label)
    {
        await connection.InsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = sourceId, Name = name, Unit = unit, Kcal = 42, PackageAmount = pack, PackageLabel = label, SearchText = name.ToLowerInvariant() });
        return (await connection.Table<Food>().FirstAsync(f => f.SourceId == sourceId)).Id;
    }

    private Task AddServing(int foodId, string label, double amount, ServingSource source) =>
        connection.InsertAsync(new Serving { FoodId = foodId, Label = label, Amount = amount, Source = source, SortOrder = (int)source });

    private Task AddBarcode(int foodId, string code, double? pack, string? label) =>
        connection.InsertAsync(new Barcode { FoodId = foodId, Code = code, PackageAmount = pack, PackageLabel = label, FirstSeen = DateTimeOffset.UnixEpoch });

    /// <summary>Foods as schema 14 left them: a cola with a Product serving and three barcodes (a can, a litre bottle and a 2 L bottle), a cordial with a Product serving and a pack only on its Food row, and a jar with neither.</summary>
    private async Task BuildVersion14()
    {
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<Barcode>();
        await connection.CreateTableAsync<SeedVersion>();
        await connection.CreateTableAsync<AppSettings>();
        await connection.CreateTableAsync<DiaryEntry>();
        colaId = await InsertFood("5449000214911", "Cola", FoodUnit.Millilitre, 330, "330ml");
        await AddServing(colaId, "1 glass (200 ml)", 200, ServingSource.Product);
        await AddBarcode(colaId, "5449000214911", 330, "330ml");
        await AddBarcode(colaId, "5449000000996", 1000, "1 l");
        await AddBarcode(colaId, "5449000131805", 2000, "2 l");
        cordialId = await InsertFood("5000000000017", "Cordial", FoodUnit.Millilitre, 500, null);
        await AddServing(cordialId, "1 serving (50 ml)", 50, ServingSource.Product);
        await AddBarcode(cordialId, "5000000000017", 500, null);
        jarId = await InsertFood("5000000000024", "Jar", FoodUnit.Gram, 400, "400 g");
        await AddBarcode(jarId, "5000000000024", 400, "400 g");
        await connection.ExecuteAsync(
            "INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, ServingId, IsRemoved) VALUES (?, 1, ?, 330, NULL, NULL, 0), (?, 2, ?, 100, 2, (SELECT Id FROM Serving WHERE FoodId = ? LIMIT 1), 0)",
            "2031-07-19", colaId, "2031-07-19", colaId, colaId);
        await connection.ExecuteAsync("PRAGMA user_version = 14");
    }

    private async Task Upgrade()
    {
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        await new DatabaseInitializer(connection, SchemaSteps.Through(15), new FakeLogger<DatabaseInitializer>(), 15).InitializeAsync();
    }

    private async Task<List<(string Label, double Amount, ServingSource Source)>> ServingsOf(int foodId) =>
        [.. (await connection.QueryAsync<Serving>("SELECT * FROM Serving WHERE FoodId = ? ORDER BY SortOrder, Amount", foodId)).Select(s => (s.Label, s.Amount, s.Source))];

    [Test]
    public async Task Upgrade_AddsPackServingsNextToTheProductServing_ForEachPackUpToAThousand()
    {
        await Upgrade();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await ServingsOf(colaId), Is.EqualTo(new[]
            {
                ("1 glass (200 ml)", 200d, ServingSource.Product),
                ("Can 330 ml", 330d, ServingSource.Pack),
                ("Bottle 1 L", 1000d, ServingSource.Pack),
            }));
            Assert.That(await ServingsOf(cordialId), Is.EqualTo(new[] { ("1 serving (50 ml)", 50d, ServingSource.Product), ("Pack 500 ml", 500d, ServingSource.Pack) }));
            Assert.That(await ServingsOf(jarId), Is.EqualTo(new[] { ("Pack 400 g", 400d, ServingSource.Pack) }));
        });
    }

    [Test]
    public async Task Upgrade_APackOverAThousand_StaysOnTheBarcodeRowOnly()
    {
        await Upgrade();

        var barcode = await connection.Table<Barcode>().FirstAsync(b => b.Code == "5449000131805");
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(barcode.PackageAmount, Is.EqualTo(2000));
            Assert.That(await connection.Table<Serving>().Where(s => s.FoodId == colaId && s.Amount > 1000).CountAsync(), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Upgrade_APackThatRepeatsAnotherServing_IsNotAdded_AndAPersonsOwnServingDoesNotBlockIt()
    {
        await AddServing(jarId, "Half jar", 200, ServingSource.Product);
        await AddBarcode(jarId, "5000000000031", 200, null);
        await AddServing(cordialId, "My glass", 500, ServingSource.User);

        await Upgrade();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await ServingsOf(jarId)).Select(s => s.Amount), Is.EqualTo(new[] { 200d, 400d }), "the 200 g pack repeats the product serving");
            Assert.That((await ServingsOf(cordialId)).Select(s => (s.Amount, s.Source)), Is.EquivalentTo(new[] { (50d, ServingSource.Product), (500d, ServingSource.Pack), (500d, ServingSource.User) }));
        });
    }

    [Test]
    public async Task Upgrade_KeepsAnExistingPackRow_AndAddsNoSecondOne()
    {
        await AddServing(jarId, "Whole pack (400 g)", 400, ServingSource.Pack);

        await Upgrade();

        Assert.That(await ServingsOf(jarId), Is.EqualTo(new[] { ("Whole pack (400 g)", 400d, ServingSource.Pack) }));
    }

    [Test]
    public async Task Upgrade_WithNoBarcodeTable_UsesThePacksOnTheFoodRows()
    {
        await connection.ExecuteAsync("DROP TABLE Barcode");

        await Upgrade();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await ServingsOf(colaId)).Select(s => s.Label), Is.EqualTo(new[] { "1 glass (200 ml)", "Can 330 ml" }));
            Assert.That(await ServingsOf(jarId), Is.EqualTo(new[] { ("Pack 400 g", 400d, ServingSource.Pack) }));
        });
    }

    [Test]
    public void Apply_WithNoPackColumnsOnFood_UsesTheBarcodeRowsOnly()
    {
        var path = TempDatabase.NewPath();
        try
        {
            var db = new SQLiteConnection(path);
            db.Execute("CREATE TABLE Food (Id INTEGER PRIMARY KEY, Unit INTEGER)");
            db.Execute("CREATE TABLE Serving (Id INTEGER PRIMARY KEY, FoodId INTEGER, Label TEXT, Amount REAL, Source INTEGER, SortOrder INTEGER)");
            db.Execute("CREATE TABLE Barcode (Id INTEGER PRIMARY KEY, FoodId INTEGER, PackageAmount REAL, PackageLabel TEXT)");
            db.Execute("INSERT INTO Food (Id, Unit) VALUES (1, 1)");
            db.Execute("INSERT INTO Barcode (FoodId, PackageAmount, PackageLabel) VALUES (1, 330, NULL)");

            new PackServingUpgrade().Apply(db);

            Assert.That(db.Query<Serving>("SELECT * FROM Serving").Select(x => (x.Label, x.Amount)), Is.EqualTo(new[] { ("Can 330 ml", 330d) }));
            db.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Upgrade_APackOfZero_IsNotAdded()
    {
        var empty = await InsertFood("5000000000048", "Empty", FoodUnit.Gram, 0, null);
        await AddBarcode(empty, "5000000000048", 0, null);

        await Upgrade();

        Assert.That(await ServingsOf(empty), Is.Empty);
    }

    [Test]
    public async Task Upgrade_TheWordsOfTheLabelAndTheSizePickTheKindOfPack()
    {
        var can = await InsertFood("5000000000055", "Small can", FoodUnit.Millilitre, 250, "250 ml can");
        var bottle = await InsertFood("5000000000062", "Wine", FoodUnit.Millilitre, 750, "750 ml bottle");
        var odd = await InsertFood("5000000000079", "Odd size", FoodUnit.Millilitre, 330.5, null);
        var litre = await InsertFood("5000000000086", "Litre", FoodUnit.Millilitre, 1000, null);

        await Upgrade();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That((await ServingsOf(can)).Single().Label, Is.EqualTo("Can 250 ml"));
            Assert.That((await ServingsOf(bottle)).Single().Label, Is.EqualTo("Bottle 750 ml"));
            Assert.That((await ServingsOf(odd)).Single().Label, Is.EqualTo("Pack 330.5 ml"));
            Assert.That((await ServingsOf(litre)).Single().Label, Is.EqualTo("Bottle 1 L"));
        });
    }

    [Test]
    public async Task Upgrade_PackRowsSortAfterTheProductServings_AndAreTheFirstBarcodesLabelWhenTwoShareAnAmount()
    {
        var twin = await InsertFood("5000000000093", "Twin", FoodUnit.Millilitre, 330, "330 ml bottle");
        await AddBarcode(twin, "5000000000093", 330, "330 ml can");
        await AddBarcode(twin, "5000000000109", 330, "330 ml bottle");

        await Upgrade();

        await Assert.MultipleAsync(async () =>
        {
            var rows = await connection.QueryAsync<Serving>("SELECT * FROM Serving WHERE FoodId = ?", twin);
            Assert.That(rows.Select(r => (r.Label, r.SortOrder)), Is.EqualTo(new[] { ("Can 330 ml", 1) }));
            Assert.That(await connection.ExecuteScalarAsync<int>("SELECT SortOrder FROM Serving WHERE FoodId = ? AND Source = 1", cordialId), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Upgrade_LeavesDiaryEntriesAsTheyWere_SoTheirCaloriesDoNotChange()
    {
        var before = await connection.QueryAsync<DiaryEntry>("SELECT * FROM DiaryEntry ORDER BY Id");

        await Upgrade();

        var after = await connection.QueryAsync<DiaryEntry>("SELECT * FROM DiaryEntry ORDER BY Id");
        Assert.That(after.Select(e => (e.Id, e.FoodId, e.Amount, e.Servings, e.ServingId)), Is.EqualTo(before.Select(e => (e.Id, e.FoodId, e.Amount, e.Servings, e.ServingId))));
    }

    [Test]
    public async Task Upgrade_RunAgain_ChangesNothing()
    {
        await Upgrade();
        var first = await connection.QueryAsync<Serving>("SELECT * FROM Serving ORDER BY Id");
        await connection.CloseAsync();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.ExecuteAsync("PRAGMA user_version = 14");

        await Upgrade();

        var second = await connection.QueryAsync<Serving>("SELECT * FROM Serving ORDER BY Id");
        Assert.That(second.Select(s => (s.Id, s.FoodId, s.Label, s.Amount, s.Source)), Is.EqualTo(first.Select(s => (s.Id, s.FoodId, s.Label, s.Amount, s.Source))));
    }

    [Test]
    public void Apply_OnADatabaseWithNoTables_DoesNothing()
    {
        var path = TempDatabase.NewPath();
        try
        {
            var db = new SQLiteConnection(path);
            Assert.DoesNotThrow(() => new PackServingUpgrade().Apply(db));
            db.Close();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Upgrade_SetsTheSchemaVersionToFifteen()
    {
        await Upgrade();

        Assert.That(await connection.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(15));
    }
}
