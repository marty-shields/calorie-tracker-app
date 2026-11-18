using CalorieTracker.Core.Data;
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

    private Task<Food> Stored(string sourceId, Action<Food>? change = null)
    {
        var food = new Food { Source = FoodSource.OpenFoodFacts, SourceId = sourceId, Name = "Cola", Unit = FoodUnit.Millilitre, Kcal = 42, SearchText = "cola" };
        change?.Invoke(food);
        return foods.UpsertAsync(food);
    }

    private async Task<List<Serving>> Rows(int foodId)
    {
        List<Serving> servings = [];
        await connection.RunInTransactionAsync(db => servings = FoodStore.ServingsOf(db, foodId));
        return servings;
    }

    [Test]
    public async Task UpsertAsync_FoodWithAPack_WritesNoServingRow()
    {
        var food = await Stored("1", f => f.PackageAmount = 330);

        Assert.That(await Rows(food.Id), Is.Empty);
    }

    [Test]
    public async Task UpsertAsync_CoFidFood_WritesNoRows()
    {
        var food = await Stored("11-001", f => f.Source = FoodSource.CoFID);

        Assert.That(await Rows(food.Id), Is.Empty);
    }

    [Test]
    public async Task UpsertAsync_FoodStoredAgain_LeavesTheOwnRowsAlone()
    {
        var first = await Stored("1");
        await servings.AddUserAsync(first.Id, "My bite", 10);

        await Stored("1");

        Assert.That((await Rows(first.Id)).Select(r => r.Label), Is.EqualTo(new[] { "My bite" }));
    }

    [Test]
    public async Task RefreshAsync_KeepsTheFoodIdAndTheOwnRows()
    {
        var food = await Stored("1");
        await servings.AddUserAsync(food.Id, "My mug", 200);
        var fresh = new Food { PackageAmount = 330, Unit = FoodUnit.Millilitre, Kcal = 40, SearchText = "cola" };

        Assert.That(await foods.RefreshAsync(food.Id, fresh), Is.True);

        Assert.That((await Rows(food.Id)).Select(r => (r.Label, r.Source)), Is.EqualTo(new[] { ("My mug", ServingSource.User) }));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task FindWithBarcodeAsync_And_ImportAsync_GiveTheFoodAndTheBarcodesRow()
    {
        var imported = await foods.ImportAsync("5000000000017", new Food { Source = FoodSource.OpenFoodFacts, SourceId = "1", Name = "A", PackageAmount = 40, SearchText = "a" }, DateTimeOffset.UnixEpoch);

        var byBarcode = await foods.FindWithBarcodeAsync("5000000000017");
        var none = await foods.FindWithBarcodeAsync("5000000000024");

        Assert.Multiple(() =>
        {
            Assert.That(byBarcode.Food?.Id, Is.EqualTo(imported.Food.Id));
            Assert.That(byBarcode.Barcode?.PackageAmount, Is.EqualTo(40));
            Assert.That((none.Food, none.Barcode), Is.EqualTo(((Food?)null, (Barcode?)null)));
        });
    }

    [Test]
    public async Task AddUserAsync_AddsAUserRow()
    {
        var food = await Stored("1", f => f.PackageAmount = 330);

        var (outcome, serving) = await servings.AddUserAsync(food.Id, "My mug", 200);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(AddUserServingOutcome.Added));
            Assert.That((serving!.FoodId, serving.Label, serving.Amount, serving.Source), Is.EqualTo((food.Id, "My mug", 200.0, ServingSource.User)));
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
        var food = await Stored("1");
        var mug = (await servings.AddUserAsync(food.Id, "My mug", 200)).Serving!;
        var bar = (await servings.AddUserAsync(food.Id, "My bar", 40)).Serving!;
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
            Assert.That(left.Select(r => r.Label), Is.EqualTo(new[] { "My bar" }));
            Assert.That(entries.Select(e => (e.Amount, e.Servings, e.ServingId)), Is.EqualTo(new[] { (400.0, (double?)2, (int?)null), (40.0, (double?)1, (int?)bar.Id) }));
        });
    }

    [Test]
    public async Task RemoveUserAsync_ARowThatIsNotTheUsersOrAnUnknownId_DeletesNothing()
    {
        var food = await Stored("1");
        await connection.InsertAsync(new Serving { FoodId = food.Id, Label = "Left from an old version", Amount = 330, Source = ServingSource.Pack });
        var row = (await Rows(food.Id)).Single();
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = food.Id, Amount = 330, Servings = 1, ServingId = row.Id });

        var results = new[] { await servings.RemoveUserAsync(row.Id), await servings.RemoveUserAsync(999) };

        var rowCount = (await Rows(food.Id)).Count;
        var linked = (await connection.Table<DiaryEntry>().FirstAsync()).ServingId;
        Assert.Multiple(() =>
        {
            Assert.That(results, Is.All.False);
            Assert.That(rowCount, Is.EqualTo(1));
            Assert.That(linked, Is.EqualTo(row.Id));
        });
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
        await connection.InsertAsync(new Serving { FoodId = food.Id, Label = "A", Amount = 100, Source = ServingSource.User });

        Assert.That(async () => await connection.InsertAsync(new Serving { FoodId = food.Id, Label = "B", Amount = 100, Source = ServingSource.User }), Throws.InstanceOf<SQLiteException>());
        Assert.That(async () => await connection.InsertAsync(new Serving { FoodId = food.Id, Label = "B", Amount = 200, Source = ServingSource.User }), Throws.Nothing);
    }
}
