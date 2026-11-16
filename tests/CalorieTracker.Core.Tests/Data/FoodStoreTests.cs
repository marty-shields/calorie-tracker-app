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
        await connection.CreateTableAsync<FoodBarcode>();
        await connection.CreateTableAsync<DiaryEntry>();
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
        var saved = await store.UpsertAsync(NewFood("1"), "5000000000017");

        var found = await store.FindByBarcodeAsync("5000000000017");

        Assert.That(found?.Id, Is.EqualTo(saved.Id));
    }

    [Test]
    public async Task FindByBarcodeAsync_UnknownBarcode_ReturnsNull()
    {
        await store.UpsertAsync(NewFood("1"), "5000000000017");

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
    public async Task UpsertAsync_FreshLookupWithNoServing_KeepsAStoredServingChecked()
    {
        var first = NewFood("1");
        first.ServingChecked = true;
        await store.UpsertAsync(first);

        await store.UpsertAsync(NewFood("1", kcal: 250));

        var row = await connection.Table<Food>().FirstAsync();
        Assert.That((row.ServingChecked, row.Kcal), Is.EqualTo(((bool?)true, 250.0)));
    }

    [Test]
    public async Task UpsertAsync_FreshLookupWithAServing_ReplacesServingChecked()
    {
        var first = NewFood("1");
        first.ServingChecked = true;
        await store.UpsertAsync(first);
        var fresh = NewFood("1");
        fresh.ServingLabel = "1 slice";
        fresh.ServingAmount = 30;

        await store.UpsertAsync(fresh);

        Assert.That((await connection.Table<Food>().FirstAsync()).ServingChecked, Is.Null);
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
        var food = NewFood("1");
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
    public async Task UpsertAsync_BarcodeLinkedToAnotherFood_KeepsTheFirstOwner()
    {
        var first = await store.UpsertAsync(NewFood("1"), "12345670");
        var second = await store.UpsertAsync(NewFood("2", FoodSource.OpenFoodFacts), "12345670");

        var found = await store.FindByBarcodeAsync("12345670");

        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        Assert.That(found?.Id, Is.EqualTo(first.Id));
        Assert.That(await connection.Table<FoodBarcode>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task UpsertAsync_SameFoodWithSeveralBarcodes_FindsItByEach()
    {
        var first = await store.UpsertAsync(NewFood("1"), "12345670");
        var again = await store.UpsertAsync(NewFood("1", kcal: 250), "5000000000017");

        Assert.That(again.Id, Is.EqualTo(first.Id));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        Assert.That((await store.FindByBarcodeAsync("12345670"))?.Id, Is.EqualTo(first.Id));
        var byNew = await store.FindByBarcodeAsync("5000000000017");
        Assert.That((byNew?.Id, byNew?.Kcal), Is.EqualTo((first.Id, (double?)250)));
    }

    [Test]
    public async Task UpsertAsync_LinkFails_SavesNeitherTheFoodNorTheLink()
    {
        await connection.ExecuteAsync("DROP TABLE FoodBarcode");

        Assert.That(() => store.UpsertAsync(NewFood("1"), "12345670"), Throws.Exception);

        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task UpsertAsync_WithoutABarcode_LinksNothing()
    {
        await store.UpsertAsync(NewFood("1"));

        Assert.That(await connection.Table<FoodBarcode>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task FindByBarcodeAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            var inserted = NewFood("1");
            await connection.InsertAsync(inserted);
            await connection.InsertAsync(new FoodBarcode { Barcode = "12345670", FoodId = inserted.Id });
        });

        var find = store.FindByBarcodeAsync("12345670");
        holding.SetResult();

        await busy;
        Assert.That((await find)?.SourceId, Is.EqualTo("1"));
    }

    private Task<List<Food>> FindIn(IReadOnlyCollection<int> ids) =>
        gate.RunAsync(async () =>
        {
            var found = new List<Food>();
            await connection.RunInTransactionAsync(db => found = FoodStore.FindIn(db, ids));
            return found;
        });

    [Test]
    public async Task FindIn_SeveralIds_ReturnsEachFoodOnce()
    {
        var a = NewFood("1");
        var b = NewFood("2");
        var other = NewFood("3");
        await connection.InsertAllAsync(new List<Food> { a, b, other });

        var found = await FindIn([a.Id, b.Id, a.Id]);

        Assert.That(found.Select(f => f.Id).Order(), Is.EqualTo(new[] { a.Id, b.Id }.Order()));
    }

    [Test]
    public async Task FindIn_UnknownId_IsLeftOut()
    {
        var a = NewFood("1");
        await connection.InsertAsync(a);

        var found = await FindIn([a.Id, a.Id + 1000]);

        Assert.That(found.Select(f => f.Id), Is.EqualTo(new[] { a.Id }));
    }

    [Test]
    public async Task FindIn_MoreIdsThanOneChunk_ReturnsEveryFood()
    {
        var foods = Enumerable.Range(1, 1500).Select(i => NewFood(i.ToString())).ToList();
        await connection.InsertAllAsync(foods);

        var found = await FindIn(foods.Select(f => f.Id).ToList());

        Assert.That(found.Select(f => f.Id).Order(), Is.EqualTo(foods.Select(f => f.Id).Order()));
    }

    [Test]
    public async Task FindIn_NoIds_ReturnsNothing()
    {
        Assert.That(await FindIn([]), Is.Empty);
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

    private static Food WithDetails(Food food)
    {
        food.Brands = "Ferrero, Nutella";
        food.GenericName = "Hazelnut And Cocoa Spread";
        food.PackageLabel = "400 g e";
        food.PackageAmount = 400;
        food.Category = "Sweets";
        food.Popularity = 804;
        food.Completeness = 0.9;
        food.SourceUpdated = DateTimeOffset.FromUnixTimeSeconds(1794233882);
        food.FetchedAt = new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.Zero);
        return food;
    }

    [Test]
    public async Task UpsertAsync_SourceDetails_RoundTrip_AndNullsStayNull()
    {
        var withDetails = await store.UpsertAsync(WithDetails(NewFood("1", FoodSource.OpenFoodFacts)));
        var without = await store.UpsertAsync(NewFood("2", FoodSource.OpenFoodFacts));

        var rows = await connection.Table<Food>().ToListAsync();
        var row = rows.Single(r => r.Id == withDetails.Id);
        var empty = rows.Single(r => r.Id == without.Id);
        Assert.Multiple(() =>
        {
            Assert.That((row.Brands, row.GenericName, row.PackageLabel, row.PackageAmount), Is.EqualTo(("Ferrero, Nutella", "Hazelnut And Cocoa Spread", "400 g e", (double?)400)));
            Assert.That((row.Category, row.Popularity, row.Completeness), Is.EqualTo(("Sweets", (int?)804, (double?)0.9)));
            Assert.That(row.SourceUpdated, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1794233882)));
            Assert.That(row.FetchedAt, Is.EqualTo(new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.Zero)));
            Assert.That((empty.Brands, empty.GenericName, empty.PackageLabel, empty.PackageAmount, empty.Category, empty.Popularity, empty.Completeness, empty.SourceUpdated, empty.FetchedAt),
                Is.EqualTo(((string?)null, (string?)null, (string?)null, (double?)null, (string?)null, (int?)null, (double?)null, (DateTimeOffset?)null, (DateTimeOffset?)null)));
        });
    }

    [Test]
    public async Task RefreshAsync_SetsTheSourceDetailsAndLeavesIdentityAlone()
    {
        var target = await store.UpsertAsync(NewFood("1", FoodSource.OpenFoodFacts), "5000000000017");
        var other = await store.UpsertAsync(NewFood("2", FoodSource.OpenFoodFacts));
        var fresh = WithDetails(Fresh());

        await store.RefreshAsync(target.Id, fresh);

        var rows = await connection.Table<Food>().ToListAsync();
        var row = rows.Single(r => r.Id == target.Id);
        Assert.Multiple(() =>
        {
            Assert.That((row.Brands, row.GenericName, row.PackageLabel, row.PackageAmount, row.Category), Is.EqualTo(("Ferrero, Nutella", "Hazelnut And Cocoa Spread", "400 g e", (double?)400, "Sweets")));
            Assert.That((row.Popularity, row.Completeness, row.SourceUpdated, row.FetchedAt), Is.EqualTo(((int?)804, (double?)0.9, (DateTimeOffset?)DateTimeOffset.FromUnixTimeSeconds(1794233882), (DateTimeOffset?)new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.Zero))));
            Assert.That((row.Name, row.Brand, row.SourceId), Is.EqualTo(("Oats", (string?)null, "1")));
            Assert.That(rows.Single(r => r.Id == other.Id).FetchedAt, Is.Null);
        });
        Assert.That((await store.FindByBarcodeAsync("5000000000017"))!.Id, Is.EqualTo(target.Id));
    }

    [Test]
    public async Task RefreshAsync_FreshWithNoServing_ClearsTheStoredServing_AndHalfAServingCountsAsNone()
    {
        var original = NewFood("1");
        original.ServingLabel = "100 g";
        original.ServingAmount = 100;
        var target = await store.UpsertAsync(original);
        var fresh = Fresh();
        fresh.ServingLabel = null;
        await store.RefreshAsync(target.Id, fresh);

        var row = await connection.Table<Food>().FirstAsync();

        Assert.That((row.ServingLabel, row.ServingAmount), Is.EqualTo(((string?)null, (double?)null)));
    }

    private static Food Fresh() => new()
    {
        ServingLabel = "1 slice (30 g)",
        ServingAmount = 30,
        Kcal = 111,
        ProteinG = 11,
        CarbsG = 12,
        FatG = 13,
        FibreG = 1.5,
        SugarsG = 2.5,
        SaturatesG = 3.5,
        SaltG = 0.5,
        // Everything below must be left alone by a refresh.
        Name = "Renamed",
        Brand = "Other brand",
        Type = FoodType.Drink,
        Unit = FoodUnit.Millilitre,
        SourceId = "changed",
        SearchText = "renamed",
    };

    [Test]
    public async Task RefreshAsync_SetsTheServingAndNutrientsOfOnlyTheRowWithThatId()
    {
        var original = NewFood("1", kcal: 100);
        original.Brand = "Brand";
        original.Type = FoodType.Food;
        original.FibreG = 9;
        var target = await store.UpsertAsync(original, "5000000000017");
        var other = await store.UpsertAsync(NewFood("2", kcal: 200));

        var changed = await store.RefreshAsync(target.Id, Fresh());

        Assert.That(changed, Is.True);
        var rows = await connection.Table<Food>().ToListAsync();
        var row = rows.Single(r => r.Id == target.Id);
        Assert.Multiple(() =>
        {
            Assert.That((row.ServingLabel, row.ServingAmount), Is.EqualTo(("1 slice (30 g)", 30.0)));
            Assert.That((row.Kcal, row.ProteinG, row.CarbsG, row.FatG), Is.EqualTo((111.0, 11.0, 12.0, 13.0)));
            Assert.That((row.FibreG, row.SugarsG, row.SaturatesG, row.SaltG), Is.EqualTo(((double?)1.5, (double?)2.5, (double?)3.5, (double?)0.5)));
            Assert.That((row.Name, row.Brand, row.Type, row.Unit, row.Source, row.SourceId, row.SearchText), Is.EqualTo(("Oats", "Brand", FoodType.Food, FoodUnit.Gram, FoodSource.CoFID, "1", "oats")));
            Assert.That(row.ServingChecked, Is.Null);
        });
        var otherRow = rows.Single(r => r.Id == other.Id);
        Assert.That((otherRow.ServingLabel, otherRow.ServingAmount, otherRow.Kcal), Is.EqualTo(((string?)null, (double?)null, 200.0)));
        Assert.That(await store.FindByBarcodeAsync("5000000000017"), Has.Property("Id").EqualTo(target.Id));
    }

    [Test]
    public async Task RefreshAsync_AnOptionalNutrientTheSourceLacks_ClearsIt()
    {
        var original = NewFood("1");
        original.FibreG = 9;
        var target = await store.UpsertAsync(original);
        var fresh = Fresh();
        fresh.FibreG = null;

        await store.RefreshAsync(target.Id, fresh);

        Assert.That((await connection.Table<Food>().FirstAsync()).FibreG, Is.Null);
    }

    [Test]
    public async Task RefreshAsync_UnknownId_ReturnsFalseAndLogsNothing()
    {
        var changed = await store.RefreshAsync(999, Fresh());

        Assert.That(changed, Is.False);
        Assert.That(LoggedEvents.Of(logger), Is.Empty);
    }

    [Test]
    public async Task RefreshAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var inserted = NewFood("1");
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(inserted);
        });

        var set = store.RefreshAsync(1, Fresh());
        holding.SetResult();

        await busy;
        Assert.That(await set, Is.True);
        Assert.That((await connection.Table<Food>().FirstAsync()).ServingAmount, Is.EqualTo(30));
    }

    [Test]
    public async Task MarkServingCheckedAsync_MarksOnlyTheRowWithThatIdAndChangesNothingElse()
    {
        var target = await store.UpsertAsync(NewFood("1", kcal: 100));
        var other = await store.UpsertAsync(NewFood("2", kcal: 200));

        var changed = await store.MarkServingCheckedAsync(target.Id);

        Assert.That(changed, Is.True);
        var rows = await connection.Table<Food>().ToListAsync();
        var row = rows.Single(r => r.Id == target.Id);
        Assert.That((row.ServingChecked, row.ServingAmount, row.Kcal, row.Name), Is.EqualTo(((bool?)true, (double?)null, 100.0, "Oats")));
        Assert.That(rows.Single(r => r.Id == other.Id).ServingChecked, Is.Null);
    }

    [Test]
    public async Task MarkServingCheckedAsync_LogsTheIdOnly_AndNothingForAnUnknownId()
    {
        var target = await store.UpsertAsync(NewFood("SRC-SECRET"));
        var afterInsert = LoggedEvents.Of(logger).Count;

        await store.MarkServingCheckedAsync(999);
        Assert.That(LoggedEvents.Of(logger).Count, Is.EqualTo(afterInsert));
        await store.MarkServingCheckedAsync(target.Id);

        var last = logger.Collector.GetSnapshot()[^1];
        Assert.That(LoggedEvents.Of(logger).Last().Id, Is.EqualTo(CoreEvent.FoodUpdated));
        Assert.That(last.Message, Does.Not.Contain("SRC-SECRET"));
    }

    [Test]
    public async Task MarkServingCheckedAsync_UnknownId_ReturnsFalse()
    {
        Assert.That(await store.MarkServingCheckedAsync(999), Is.False);
    }

    [Test]
    public async Task MarkServingCheckedAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(NewFood("1"));
        });

        var mark = store.MarkServingCheckedAsync(1);
        holding.SetResult();

        await busy;
        Assert.That(await mark, Is.True);
        Assert.That((await connection.Table<Food>().FirstAsync()).ServingChecked, Is.True);
    }

    [Test]
    public async Task Logging_UpsertAndFind_LogsIdsOnlyNeverNameOrBarcode()
    {
        var food = NewFood("SRC-SECRET");
        food.Name = "Distinctive Name";
        food.Brand = "Distinctive Brand";

        await store.UpsertAsync(food, "9876543210987");
        await store.UpsertAsync(food, "9876543210987");
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
    public async Task UpsertManyAsync_MixedSources_ThrowsAndWritesNothing()
    {
        Assert.That(() => store.UpsertManyAsync([NewFood("1"), NewFood("2", FoodSource.OpenFoodFacts)]), Throws.ArgumentException);

        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
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

    [Test]
    public async Task DeleteMissingAsync_SkipsFoodsADiaryEntryPointsAtIncludingARemovedOne_AndDeletesTheRest()
    {
        var kept = await store.UpsertAsync(NewFood("kept"), "5000000000017");
        var removedOnly = await store.UpsertAsync(NewFood("removed"), "5000000000024");
        var stale = await store.UpsertAsync(NewFood("stale"), "5000000000031");
        var listed = await store.UpsertAsync(NewFood("listed"));
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = kept.Id, Amount = 10 });
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = removedOnly.Id, Amount = 10, IsRemoved = true });

        var deleted = await store.DeleteMissingAsync(FoodSource.CoFID, new HashSet<string> { "listed" });

        Assert.That(deleted, Is.EqualTo(1));
        Assert.That((await connection.Table<Food>().ToListAsync()).Select(f => f.Id), Is.EquivalentTo(new[] { kept.Id, removedOnly.Id, listed.Id }));
        Assert.That(await connection.QueryScalarsAsync<string>("SELECT Barcode FROM FoodBarcode ORDER BY Barcode"), Is.EqualTo(new[] { "5000000000017", "5000000000024" }));
        Assert.That(stale.Id, Is.Not.Zero);
    }

    private static Food NewFood(string sourceId, FoodSource source = FoodSource.CoFID, double kcal = 100) => new()
    {
        Source = source,
        SourceId = sourceId,
        Name = "Oats",
        Unit = FoodUnit.Gram,
        Kcal = kcal,
        ProteinG = 1,
        CarbsG = 2,
        FatG = 3,
        SearchText = "oats",
    };
}
