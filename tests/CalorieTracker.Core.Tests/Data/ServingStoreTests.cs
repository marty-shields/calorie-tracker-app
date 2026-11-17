using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Data;

public class ServingStoreTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FoodStore foods = null!;
    private ServingStore servings = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Barcode>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<Favourite>();
        var gate = new DatabaseGate();
        foods = new FoodStore(connection, gate, new FakeLogger<FoodStore>());
        servings = new ServingStore(connection, gate);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private Task<Food> Stored(string sourceId, Action<Food>? change = null, ProductServing? serving = null)
    {
        var food = new Food { Source = FoodSource.OpenFoodFacts, SourceId = sourceId, Name = "Cola", Unit = FoodUnit.Millilitre, Kcal = 42, SearchText = "cola" };
        change?.Invoke(food);
        return foods.UpsertAsync(food, serving: serving);
    }

    private async Task<List<Serving>> Rows(int foodId)
    {
        List<Serving> servings = [];
        await connection.RunInTransactionAsync(db => servings = FoodStore.ServingsOf(db, foodId));
        return servings;
    }

    [Test]
    public async Task UpsertAsync_ServingAndPack_WritesAProductRowAndAPackRow()
    {
        var food = await Stored("1", f => f.PackageAmount = 330, new ProductServing("1 glass (250 ml)", 250));

        var rows = await Rows(food.Id);

        Assert.That(rows.Select(r => (r.Label, r.Amount, r.Source)), Is.EqualTo(new[]
        {
            ("1 glass (250 ml)", 250.0, ServingSource.Product),
            ("Can 330 ml", 330.0, ServingSource.Pack),
        }));
    }

    [Test]
    public async Task UpsertAsync_CoFidFoodWithNoServingOrPack_WritesNoRows()
    {
        var food = await Stored("11-001", f => f.Source = FoodSource.CoFID);

        Assert.That(await Rows(food.Id), Is.Empty);
    }

    [Test]
    public async Task UpsertAsync_SameFoodTwice_KeepsOneRowOfEachAndTheProductRowsId()
    {
        var first = await Stored("1", f => f.PackageAmount = 330, new ProductServing("1 glass (250 ml)", 250));
        var productId = (await Rows(first.Id)).Single(r => r.Source == ServingSource.Product).Id;

        await Stored("1", f => f.PackageAmount = 330, new ProductServing("1 tall glass (300 ml)", 300));

        var rows = await Rows(first.Id);
        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(r => (r.Label, r.Amount, r.Source)), Is.EqualTo(new[]
            {
                ("1 tall glass (300 ml)", 300.0, ServingSource.Product),
                ("Can 330 ml", 330.0, ServingSource.Pack),
            }));
            Assert.That(rows.Single(r => r.Source == ServingSource.Product).Id, Is.EqualTo(productId));
        });
    }

    [Test]
    public async Task UpsertAsync_LookupWithNoServing_LeavesTheStoredRowsAlone()
    {
        var first = await Stored("1", serving: new ProductServing("1 bar", 40));
        await servings.AddUserAsync(first.Id, "My bite", 10);

        await Stored("1");

        Assert.That((await Rows(first.Id)).Select(r => r.Label), Is.EqualTo(new[] { "My bite", "1 bar" }));
    }

    [Test]
    public async Task RefreshAsync_WritesProductAndPack_KeepsTheFoodIdAndTheOwnRows()
    {
        var food = await Stored("1");
        await servings.AddUserAsync(food.Id, "My mug", 200);
        var fresh = new Food { PackageAmount = 330, Unit = FoodUnit.Millilitre, Kcal = 40, SearchText = "cola" };

        Assert.That(await foods.RefreshAsync(food.Id, fresh, serving: new ProductServing("1 glass (250 ml)", 250)), Is.True);

        Assert.That((await Rows(food.Id)).Select(r => (r.Label, r.Source)), Is.EqualTo(new[]
        {
            ("My mug", ServingSource.User),
            ("1 glass (250 ml)", ServingSource.Product),
            ("Can 330 ml", ServingSource.Pack),
        }));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task RefreshAsync_AChangedProductServing_UpdatesTheRowInPlaceSoEntriesKeepTheirLink()
    {
        var food = await Stored("1", serving: new ProductServing("1 glass (250 ml)", 250));
        var product = (await Rows(food.Id)).Single();
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = food.Id, Amount = 250, Servings = 1, ServingId = product.Id });

        await foods.RefreshAsync(food.Id, new Food { Unit = FoodUnit.Millilitre, SearchText = "cola" }, serving: new ProductServing("1 tall glass (300 ml)", 300));

        var rows = await Rows(food.Id);
        var entry = await connection.Table<DiaryEntry>().FirstAsync();
        Assert.That((rows.Single().Id, rows.Single().Label, rows.Single().Amount, entry.ServingId), Is.EqualTo((product.Id, "1 tall glass (300 ml)", 300.0, (int?)product.Id)));
    }

    [Test]
    public async Task FindWithBarcodeAsync_And_ImportAsync_GiveTheFoodsProductServingFromItsRow()
    {
        var imported = await foods.ImportAsync("5000000000017", new Food { Source = FoodSource.OpenFoodFacts, SourceId = "1", Name = "A", SearchText = "a" }, DateTimeOffset.UnixEpoch, new ProductServing("1 bar", 40));
        await foods.UpsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = "2", Name = "B", SearchText = "b" }, "5000000000024");

        var byBarcode = await foods.FindWithBarcodeAsync("5000000000017");
        var none = await foods.FindWithBarcodeAsync("5000000000024");
        var again = await foods.ImportAsync("5000000000017", new Food { Source = FoodSource.OpenFoodFacts, SourceId = "1", Name = "A", SearchText = "a" }, DateTimeOffset.UnixEpoch);

        Assert.Multiple(() =>
        {
            Assert.That(imported.Serving, Is.EqualTo(new ProductServing("1 bar", 40)));
            Assert.That(byBarcode.Serving, Is.EqualTo(new ProductServing("1 bar", 40)));
            Assert.That(none.Serving, Is.Null);
            Assert.That(again.Serving, Is.EqualTo(new ProductServing("1 bar", 40)), "a known barcode gives the stored serving, not the one passed");
        });
    }

    [Test]
    public async Task AddUserAsync_AddsAUserRowSortedAfterTheOthers()
    {
        var food = await Stored("1", f => f.PackageAmount = 330);

        var (outcome, serving) = await servings.AddUserAsync(food.Id, "My mug", 200);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(AddUserServingOutcome.Added));
            Assert.That((serving!.FoodId, serving.Label, serving.Amount, serving.Source, serving.SortOrder), Is.EqualTo((food.Id, "My mug", 200.0, ServingSource.User, 2)));
        });
    }

    [Test]
    public async Task AddUserAsync_ASecondOneOfTheSameAmount_IsADuplicateAndAddsNothing()
    {
        var food = await Stored("1");
        await servings.AddUserAsync(food.Id, "My mug", 200);

        var (outcome, serving) = await servings.AddUserAsync(food.Id, "Other mug", 200);

        Assert.That((outcome, serving, (await Rows(food.Id)).Count), Is.EqualTo((AddUserServingOutcome.Duplicate, (Serving?)null, 1)));
    }

    [Test]
    public async Task AddUserAsync_TheAmountOfAProductOrPackRow_IsAllowed()
    {
        var food = await Stored("1", serving: new ProductServing("1 bar", 40));

        var (outcome, _) = await servings.AddUserAsync(food.Id, "My bar", 40);

        Assert.That(outcome, Is.EqualTo(AddUserServingOutcome.Added));
    }

    [Test]
    public async Task AddUserAsync_UnknownFood_AddsNothing()
    {
        var (outcome, serving) = await servings.AddUserAsync(99, "My mug", 200);

        Assert.That((outcome, serving, await connection.Table<Serving>().CountAsync()), Is.EqualTo((AddUserServingOutcome.FoodNotFound, (Serving?)null, 0)));
    }

    [Test]
    public async Task AddUserAsync_OnACoFidFood_Works()
    {
        var food = await Stored("11-001", f => f.Source = FoodSource.CoFID);

        var (outcome, _) = await servings.AddUserAsync(food.Id, "1 tbsp", 15);

        Assert.That(outcome, Is.EqualTo(AddUserServingOutcome.Added));
    }

    [Test]
    public async Task RemoveUserAsync_DeletesTheRowAndClearsOnlyTheLinksToIt()
    {
        var food = await Stored("1", serving: new ProductServing("1 bar", 40));
        var mug = (await servings.AddUserAsync(food.Id, "My mug", 200)).Serving!;
        var bar = (await Rows(food.Id)).Single(r => r.Source == ServingSource.Product);
        await connection.InsertAllAsync(new List<DiaryEntry>
        {
            new() { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = food.Id, Amount = 400, Servings = 2, ServingId = mug.Id },
            new() { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = food.Id, Amount = 40, Servings = 1, ServingId = bar.Id },
        });

        var removed = await servings.RemoveUserAsync(mug.Id);

        var entries = await connection.Table<DiaryEntry>().OrderBy(e => e.Id).ToListAsync();
        var left = await Rows(food.Id);
        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(left.Select(r => r.Label), Is.EqualTo(new[] { "1 bar" }));
            Assert.That(entries.Select(e => (e.Amount, e.Servings, e.ServingId)), Is.EqualTo(new[] { (400.0, (double?)2, (int?)null), (40.0, (double?)1, (int?)bar.Id) }));
        });
    }

    [Test]
    public async Task RemoveUserAsync_AProductOrPackRowOrAnUnknownId_DeletesNothing()
    {
        var food = await Stored("1", f => f.PackageAmount = 330, new ProductServing("1 glass (250 ml)", 250));
        var rows = await Rows(food.Id);

        var results = new[] { await servings.RemoveUserAsync(rows[0].Id), await servings.RemoveUserAsync(rows[1].Id), await servings.RemoveUserAsync(999) };

        Assert.That(results, Is.All.False);
        Assert.That((await Rows(food.Id)).Count, Is.EqualTo(2));
    }

    [Test]
    public async Task DeleteMissingAsync_DeletesTheServingsOfTheFoodsItDeletes_AndOfNoOthers()
    {
        var gone = await Stored("11-001", f => f.Source = FoodSource.CoFID);
        var kept = await Stored("11-002", f => f.Source = FoodSource.CoFID);
        await servings.AddUserAsync(gone.Id, "1 tbsp", 15);
        await servings.AddUserAsync(kept.Id, "1 tsp", 5);

        await foods.DeleteMissingAsync(FoodSource.CoFID, new HashSet<string> { "11-002" });

        Assert.That((await connection.Table<Serving>().ToListAsync()).Select(r => r.FoodId), Is.EqualTo(new[] { kept.Id }));
    }

    [Test]
    public async Task UniqueIndex_RejectsASecondRowOfOneFoodSourceAndAmount()
    {
        var food = await Stored("1");
        await connection.InsertAsync(new Serving { FoodId = food.Id, Label = "A", Amount = 100, Source = ServingSource.Pack, SortOrder = 1 });

        Assert.That(async () => await connection.InsertAsync(new Serving { FoodId = food.Id, Label = "B", Amount = 100, Source = ServingSource.Pack, SortOrder = 1 }), Throws.InstanceOf<SQLiteException>());
        Assert.That(async () => await connection.InsertAsync(new Serving { FoodId = food.Id, Label = "B", Amount = 100, Source = ServingSource.User, SortOrder = 2 }), Throws.Nothing);
    }
}
