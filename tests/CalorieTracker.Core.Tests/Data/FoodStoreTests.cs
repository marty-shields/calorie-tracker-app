using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

public class FoodStoreTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeLogger<FoodStore> logger = null!;
    private FoodStore store = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<SeedVersion>();
        gate = new DatabaseGate();
        logger = new FakeLogger<FoodStore>();
        store = new FoodStore(connection, gate, logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task FindByBarcodeAsync_StoredBarcode_ReturnsTheFood()
    {
        var saved = await store.UpsertAsync(NewFood("1", barcode: "5000000000017"));

        var found = await store.FindByBarcodeAsync("5000000000017");

        Assert.That(found?.Id, Is.EqualTo(saved.Id));
    }

    [Test]
    public async Task FindByBarcodeAsync_UnknownBarcode_ReturnsNull()
    {
        await store.UpsertAsync(NewFood("1", barcode: "5000000000017"));

        Assert.That(await store.FindByBarcodeAsync("0000000000000"), Is.Null);
    }

    [Test]
    public async Task UpsertAsync_NewFood_InsertsAndReturnsItsId()
    {
        var saved = await store.UpsertAsync(NewFood("1"));

        Assert.That(saved.Id, Is.GreaterThan(0));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task UpsertAsync_SameSourceAndSourceId_UpdatesAndKeepsTheId()
    {
        var first = await store.UpsertAsync(NewFood("1", kcal: 100));

        var second = await store.UpsertAsync(NewFood("1", kcal: 250));

        var rows = await connection.Table<Food>().ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(second.Id, Is.EqualTo(first.Id));
        Assert.That(rows[0].Kcal, Is.EqualTo(250));
    }

    [Test]
    public async Task UpsertAsync_SameSourceIdUnderAnotherSource_IsASeparateRow()
    {
        var cofid = await store.UpsertAsync(NewFood("1", FoodSource.CoFID));
        var off = await store.UpsertAsync(NewFood("1", FoodSource.OpenFoodFacts));

        Assert.That(off.Id, Is.Not.EqualTo(cofid.Id));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task UpsertAsync_OptionalFieldsNull_RoundTripAsNull()
    {
        var saved = await store.UpsertAsync(NewFood("1"));

        var row = await connection.Table<Food>().FirstAsync(f => f.Id == saved.Id);

        Assert.Multiple(() =>
        {
            Assert.That(row.Barcode, Is.Null);
            Assert.That(row.Brand, Is.Null);
            Assert.That(row.FibreG, Is.Null);
            Assert.That(row.SugarsG, Is.Null);
            Assert.That(row.SaturatesG, Is.Null);
            Assert.That(row.SaltG, Is.Null);
        });
    }

    [Test]
    public async Task UpsertAsync_OptionalFieldsSet_RoundTrip()
    {
        var food = NewFood("1", barcode: "12345670");
        food.Brand = "Acme";
        food.FibreG = 1.5;
        food.SaltG = 0.25;
        food.Unit = FoodUnit.Millilitre;

        var saved = await store.UpsertAsync(food);

        var row = await connection.Table<Food>().FirstAsync(f => f.Id == saved.Id);
        Assert.Multiple(() =>
        {
            Assert.That(row.Brand, Is.EqualTo("Acme"));
            Assert.That(row.FibreG, Is.EqualTo(1.5));
            Assert.That(row.SaltG, Is.EqualTo(0.25));
            Assert.That(row.Unit, Is.EqualTo(FoodUnit.Millilitre));
        });
    }

    [Test]
    public async Task FindByBarcodeAsync_TwoRowsShareABarcode_ReturnsTheLowestId()
    {
        var first = await store.UpsertAsync(NewFood("1", barcode: "12345670"));
        await store.UpsertAsync(NewFood("2", FoodSource.OpenFoodFacts, barcode: "12345670"));

        var found = await store.FindByBarcodeAsync("12345670");

        Assert.That(found?.Id, Is.EqualTo(first.Id));
    }

    [Test]
    public async Task FindByBarcodeAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(NewFood("1", barcode: "12345670"));
        });

        var find = store.FindByBarcodeAsync("12345670");
        holding.SetResult();

        await busy;
        Assert.That((await find)?.SourceId, Is.EqualTo("1"));
    }

    [Test]
    public async Task UpsertAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(NewFood("1", kcal: 100));
        });

        var upsert = store.UpsertAsync(NewFood("1", kcal: 250));
        holding.SetResult();

        await busy;
        var saved = await upsert;
        var rows = await connection.Table<Food>().ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(saved.Id, Is.EqualTo(rows[0].Id));
        Assert.That(rows[0].Kcal, Is.EqualTo(250));
    }

    [Test]
    public async Task Logging_UpsertAndFind_LogsIdsOnlyNeverNameOrBarcode()
    {
        var food = NewFood("SRC-SECRET", barcode: "9876543210987");
        food.Name = "Distinctive Name";
        food.Brand = "Distinctive Brand";

        await store.UpsertAsync(food);
        await store.UpsertAsync(food);
        await store.FindByBarcodeAsync("9876543210987");

        var records = logger.Collector.GetSnapshot();
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.FoodInserted, CoreEvent.FoodUpdated }));
        foreach (var record in records)
        {
            var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
            Assert.That(text, Does.Not.Contain("Distinctive").And.Not.Contain("9876543210987").And.Not.Contain("SRC-SECRET"));
        }
    }

    [Test]
    public async Task UpsertManyAsync_NewFoods_InsertsThem()
    {
        var result = await store.UpsertManyAsync([NewFood("1"), NewFood("2")]);

        Assert.That(result, Is.EqualTo(new UpsertManyResult(2, 0)));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task UpsertManyAsync_ExistingFoods_UpdateKeepingTheId()
    {
        var saved = await store.UpsertAsync(NewFood("1", kcal: 100));

        var result = await store.UpsertManyAsync([NewFood("1", kcal: 250)]);

        var row = await connection.Table<Food>().FirstAsync();
        Assert.That(result, Is.EqualTo(new UpsertManyResult(0, 1)));
        Assert.That((row.Id, row.Kcal), Is.EqualTo((saved.Id, 250d)));
    }

    [Test]
    public async Task UpsertManyAsync_MixedFoods_InsertsAndUpdates()
    {
        await store.UpsertAsync(NewFood("1"));

        var result = await store.UpsertManyAsync([NewFood("1"), NewFood("2")]);

        Assert.That(result, Is.EqualTo(new UpsertManyResult(1, 1)));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task UpsertManyAsync_OtherSourceWithTheSameSourceId_IsNotTouched()
    {
        var other = await store.UpsertAsync(NewFood("1", FoodSource.OpenFoodFacts, kcal: 5));

        var result = await store.UpsertManyAsync([NewFood("1", kcal: 250)]);

        var rows = await connection.Table<Food>().ToListAsync();
        Assert.That(result, Is.EqualTo(new UpsertManyResult(1, 0)));
        Assert.That(rows.Single(f => f.Id == other.Id).Kcal, Is.EqualTo(5));
        Assert.That(rows, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task UpsertManyAsync_NoFoods_ChangesNothing()
    {
        Assert.That(await store.UpsertManyAsync([]), Is.EqualTo(new UpsertManyResult(0, 0)));
    }

    [Test]
    public async Task SeedVersion_IsNullUntilSet_ThenReplaced()
    {
        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.Null);

        await store.SetSeedVersionAsync(FoodSource.CoFID, 1);
        await store.SetSeedVersionAsync(FoodSource.CoFID, 2);

        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.EqualTo(2));
        Assert.That(await store.GetSeedVersionAsync(FoodSource.OpenFoodFacts), Is.Null);
    }

    private static Food NewFood(string sourceId, FoodSource source = FoodSource.CoFID, string? barcode = null, double kcal = 100) => new()
    {
        Source = source,
        SourceId = sourceId,
        Name = "Oats",
        Barcode = barcode,
        Unit = FoodUnit.Gram,
        Kcal = kcal,
        ProteinG = 1,
        CarbsG = 2,
        FatG = 3,
        SearchText = "oats",
    };
}
