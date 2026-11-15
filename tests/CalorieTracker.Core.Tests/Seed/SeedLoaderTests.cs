using System.Text;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Seed;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Seed;

public class SeedLoaderTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FoodStore store = null!;
    private FakeLogger<SeedLoader> logger = null!;
    private string seedJson = "";

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<FoodBarcode>();
        await connection.CreateTableAsync<SeedVersion>();
        store = new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>());
        logger = new FakeLogger<SeedLoader>();
        seedJson = Sample(1);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task LoadAsync_EmptyDatabase_StoresEveryFoodMapped()
    {
        var result = await Loader().LoadAsync();

        var foods = (await connection.Table<Food>().ToListAsync()).ToDictionary(f => f.SourceId);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 3, 0)));
            Assert.That(foods.Keys, Is.EquivalentTo(new[] { "11-001", "11-002", "11-003" }));
            Assert.That(foods.Values.Select(f => f.Source), Is.All.EqualTo(FoodSource.CoFID));
            Assert.That(foods["11-001"].Kcal, Is.EqualTo(355));
            Assert.That((foods["11-001"].ProteinG, foods["11-001"].CarbsG, foods["11-001"].FatG), Is.EqualTo((0.4, 94d, 0.1)));
            Assert.That((foods["11-001"].FibreG, foods["11-001"].SugarsG, foods["11-001"].SaturatesG, foods["11-001"].SaltG), Is.EqualTo(((double?)null, (double?)0, (double?)null, (double?)0.01)));
            Assert.That(foods["11-002"].Unit, Is.EqualTo(FoodUnit.Millilitre));
            Assert.That(foods["11-001"].Unit, Is.EqualTo(FoodUnit.Gram));
            Assert.That(foods["11-001"].Brand, Is.Null);
        });
        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.EqualTo(1));
    }

    [Test]
    public async Task LoadAsync_SetsSearchTextToTheLowercasedName()
    {
        await Loader().LoadAsync();

        var food = await connection.Table<Food>().FirstAsync(f => f.SourceId == "11-003");
        Assert.That((food.Name, food.SearchText), Is.EqualTo(("Barley, Pearl, Raw", "barley, pearl, raw")));
    }

    [Test]
    public async Task LoadAsync_AccentedName_StoresFoldedSearchText()
    {
        seedJson = Wrap(1, [Row("11-001", "Crème fraîche", 100)]);

        await Loader().LoadAsync();

        var food = await connection.Table<Food>().FirstAsync();
        Assert.That((food.Name, food.SearchText), Is.EqualTo(("Crème fraîche", "creme fraiche")));
    }

    [Test]
    public async Task LoadAsync_SameVersionAgain_IsUpToDateAndWritesNothing()
    {
        await Loader().LoadAsync();
        await connection.ExecuteAsync("UPDATE Food SET Name = 'Edited' WHERE SourceId = '11-001'");

        var result = await Loader().LoadAsync();

        Assert.That(result.Status, Is.EqualTo(SeedLoadStatus.UpToDate));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(3));
        Assert.That((await connection.Table<Food>().FirstAsync(f => f.SourceId == "11-001")).Name, Is.EqualTo("Edited"));
    }

    [Test]
    public async Task LoadAsync_NewerVersion_UpdatesKeepingIdsAndInsertsNewFoods()
    {
        await Loader().LoadAsync();
        var before = (await connection.Table<Food>().ToListAsync()).ToDictionary(f => f.SourceId, f => f.Id);
        seedJson = Sample(2, changedKcal: 999, extra: true);

        var result = await Loader().LoadAsync();

        var after = (await connection.Table<Food>().ToListAsync()).ToDictionary(f => f.SourceId);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new SeedLoadResult(SeedLoadStatus.Loaded, 2, 1, 3)));
            Assert.That(after["11-001"].Kcal, Is.EqualTo(999));
            Assert.That(after["11-001"].Name, Is.EqualTo("Arrowroot v2"));
            Assert.That(after["11-001"].Id, Is.EqualTo(before["11-001"]));
            Assert.That(after["11-002"].Id, Is.EqualTo(before["11-002"]));
            Assert.That(after["11-004"].Id, Is.Not.AnyOf(before.Values));
            Assert.That(after, Has.Count.EqualTo(4));
        });
        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.EqualTo(2));
    }

    [Test]
    public async Task LoadAsync_EveryType_IsStored()
    {
        seedJson = Wrap(1, [Row("1", "A", 1, type: "Ingredient"), Row("2", "B", 1, type: "Food"), Row("3", "C", 1, type: "Dish"), Row("4", "D", 1, type: "Drink")]);

        await Loader().LoadAsync();

        var types = (await connection.Table<Food>().ToListAsync()).OrderBy(f => f.SourceId).Select(f => f.Type);
        Assert.That(types, Is.EqualTo(new FoodType?[] { FoodType.Ingredient, FoodType.Food, FoodType.Dish, FoodType.Drink }));
    }

    [Test]
    public async Task LoadAsync_RowStoredWithoutAType_GetsItsTypeAndKeepsIdAndBarcodeLinks()
    {
        await Loader().LoadAsync();
        await connection.ExecuteAsync("UPDATE Food SET Type = NULL");
        await store.UpsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = "5000000000017", Name = "Off", Unit = FoodUnit.Gram, SearchText = "off", Type = FoodType.Food }, "5000000000017");
        var arrowroot = await connection.Table<Food>().FirstAsync(f => f.SourceId == "11-001");
        await connection.ExecuteAsync("INSERT INTO FoodBarcode (Barcode, FoodId) VALUES (?, ?)", "5000000000024", arrowroot.Id);
        await store.SetSeedVersionAsync(FoodSource.CoFID, 3);
        seedJson = Wrap(4, [Row("11-001", "Arrowroot", 355, type: "Ingredient"), Row("11-002", "Gin", 250, unit: "Millilitre", type: "Drink"), Row("11-003", "Barley", 360, type: "Dish")]);

        await Loader().LoadAsync();

        var after = (await connection.Table<Food>().Where(f => f.Source == FoodSource.CoFID).ToListAsync()).ToDictionary(f => f.SourceId);
        var linked = await connection.QueryScalarsAsync<int>("SELECT FoodId FROM FoodBarcode WHERE Barcode = ?", "5000000000024");
        Assert.Multiple(() =>
        {
            Assert.That(after["11-001"].Id, Is.EqualTo(arrowroot.Id));
            Assert.That(after.Values.Select(f => f.Type), Is.EqualTo(new FoodType?[] { FoodType.Ingredient, FoodType.Drink, FoodType.Dish }));
            Assert.That(linked, Is.EqualTo(new[] { arrowroot.Id }));
        });
    }

    [Test]
    public async Task LoadAsync_OpenFoodFactsRows_AreUntouched()
    {
        await Loader().LoadAsync();
        await store.UpsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = "11-001", Name = "Off", Unit = FoodUnit.Gram, Kcal = 1, ProteinG = 2, CarbsG = 3, FatG = 4, SearchText = "off" }, "5000000000017");
        await store.UpsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = "999", Name = "Other", Unit = FoodUnit.Gram, Kcal = 1, ProteinG = 2, CarbsG = 3, FatG = 4, SearchText = "other" });
        var before = await Snapshot(FoodSource.OpenFoodFacts);
        seedJson = Sample(2, changedKcal: 999, extra: true);

        await Loader().LoadAsync();

        Assert.That(await Snapshot(FoodSource.OpenFoodFacts), Is.EqualTo(before));
        Assert.That(before, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task LoadAsync_StoredVersionHigher_IsUpToDateAndChangesNothing()
    {
        await store.SetSeedVersionAsync(FoodSource.CoFID, 5);

        var result = await Loader().LoadAsync();

        Assert.That(result.Status, Is.EqualTo(SeedLoadStatus.UpToDate));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.EqualTo(5));
    }

    [TestCase("not json")]
    [TestCase("")]
    [TestCase("""{"seedVersion":1,"source":"CoFID 2021","foods":[{"source":"CoFID","sourceId":"1","name":"A","unit":"Gram","energyKcal":1,"proteinG":1,"carbsG":1}]}""")]
    [TestCase("""{"seedVersion":1,"source":"CoFID 2021","foods":[{"source":"CoFID","sourceId":"1","name":"A","unit":"Gram","energyKcal":1,"proteinG":1,"carbsG":1,"fatG":1},{"source":"CoFID","sourceId":"1","name":"B","unit":"Gram","energyKcal":1,"proteinG":1,"carbsG":1,"fatG":1}]}""")]
    [TestCase("""{"seedVersion":1,"source":"CoFID 2021","foods":[{"source":"OpenFoodFacts","sourceId":"1","name":"A","unit":"Gram","energyKcal":1,"proteinG":1,"carbsG":1,"fatG":1}]}""")]
    public async Task LoadAsync_BadSeed_FailsWritesNothingAndTheNextGoodRunLoads(string bad)
    {
        seedJson = bad;

        var failed = await Loader().LoadAsync();

        Assert.That(failed.Status, Is.EqualTo(SeedLoadStatus.Failed));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.Null);

        seedJson = Sample(1);
        Assert.That((await Loader().LoadAsync()).Status, Is.EqualTo(SeedLoadStatus.Loaded));
    }

    [Test]
    public async Task LoadAsync_MissingSeed_Fails()
    {
        var result = await new SeedLoader(store, logger, () => throw new FileNotFoundException()).LoadAsync();

        Assert.That(result.Status, Is.EqualTo(SeedLoadStatus.Failed));
    }

    [Test]
    public async Task LoadAsync_MoreFoodsThanOneChunk_LoadsAllAndWritesTheVersionOnce()
    {
        var foods = Enumerable.Range(0, (SeedLoader.ChunkSize * 2) + 200).Select(i => Row($"9-{i:D4}", $"Food {i}", 100));
        seedJson = Wrap(1, foods);

        var result = await Loader().LoadAsync();

        Assert.That(result.Inserted, Is.EqualTo(1200));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1200));
        Assert.That(await connection.Table<SeedVersion>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task LoadAsync_LaterChunkFails_StoresNoVersionAndTheNextGoodRunLoads()
    {
        seedJson = Wrap(1, Enumerable.Range(0, (SeedLoader.ChunkSize * 2) + 200).Select(i => Row($"9-{i:D4}", $"Food {i}", 100)));
        await connection.ExecuteAsync("CREATE TRIGGER FailChunkTwo BEFORE INSERT ON Food WHEN NEW.SourceId = '9-0700' BEGIN SELECT RAISE(ABORT, 'boom'); END");

        var failed = await Loader().LoadAsync();

        Assert.That(failed.Status, Is.EqualTo(SeedLoadStatus.Failed));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(SeedLoader.ChunkSize));
        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.Null);

        await connection.ExecuteAsync("DROP TRIGGER FailChunkTwo");
        var result = await Loader().LoadAsync();

        Assert.That(result, Is.EqualTo(new SeedLoadResult(SeedLoadStatus.Loaded, 1, 700, SeedLoader.ChunkSize)));
        Assert.That(await store.GetSeedVersionAsync(FoodSource.CoFID), Is.EqualTo(1));
    }

    [Test]
    public async Task LoadAsync_LogsCountsAndNoFoodNames()
    {
        await Loader().LoadAsync();
        await Loader().LoadAsync();
        seedJson = "not json";
        await Loader().LoadAsync();

        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.SeedLoaded, CoreEvent.SeedUpToDate, CoreEvent.SeedLoadFailed }));
        var loaded = logger.Collector.GetSnapshot()[0];
        Assert.That(loaded.StructuredState!.Select(p => p.Value), Does.Contain("3"));
        foreach (var record in logger.Collector.GetSnapshot())
        {
            var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}")) + record.Exception;
            Assert.That(text, Does.Not.Contain("Arrowroot").And.Not.Contain("Barley").And.Not.Contain("11-00").IgnoreCase);
        }
    }

    private static IEnumerable<TestCaseData> BadSeeds()
    {
        yield return new TestCaseData(Wrap(1, [Row("1", "A", 1, unit: "Litre")])).SetName("unit Litre");
        yield return new TestCaseData(Wrap(1, [Row("1", "A", 1, unit: "1")])).SetName("numeric unit");
        yield return new TestCaseData(Wrap(1, [Row("1", "A", 1, unit: "99")])).SetName("undefined numeric unit");
        yield return new TestCaseData(Wrap(1, ["""{"source":"CoFID","sourceId":"1","name":"A","energyKcal":1,"proteinG":1,"carbsG":1,"fatG":1}"""])).SetName("missing unit");
        yield return new TestCaseData(Wrap(1, [Row("1", "A", 1, type: "Snack")])).SetName("unknown type");
        yield return new TestCaseData(Wrap(1, [Row("1", "A", 1, type: "1")])).SetName("numeric type");
        yield return new TestCaseData(Wrap(1, ["""{"source":"CoFID","sourceId":"1","name":"A","unit":"Gram","energyKcal":1,"proteinG":1,"carbsG":1,"fatG":1}"""])).SetName("missing type");
        yield return new TestCaseData(Wrap(1, [Row("1", "   ", 1)])).SetName("whitespace name");
        yield return new TestCaseData(Wrap(0, [Row("1", "A", 1)])).SetName("seedVersion 0");
        yield return new TestCaseData("""{"foods":[]}""").SetName("missing seedVersion");
        yield return new TestCaseData(Wrap(1, [Row("1", "A", 1), Row("1", "B", 1)])).SetName("duplicate source id");
    }

    [TestCaseSource(nameof(BadSeeds))]
    public void SeedFile_BadSeed_ThrowsFormatException(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<FormatException>(() => SeedFile.Parse(stream));
    }

    [Test]
    public void SeedFile_BundledSeed_IsComplete()
    {
        using var stream = SeedLoader.OpenBundledSeed();

        var seed = SeedFile.Parse(stream);

        Assert.Multiple(() =>
        {
            // 4 since the food types; 3 was the everyday names and 2 food search
            Assert.That(seed.Version, Is.GreaterThanOrEqualTo(4));
            Assert.That(seed.Foods, Has.Count.EqualTo(2853));
            Assert.That(seed.Foods.Select(f => f.Source), Is.All.EqualTo(FoodSource.CoFID));
            Assert.That(seed.Foods.Select(f => f.SourceId), Is.Unique);
            Assert.That(seed.Foods.Select(f => f.Name), Is.All.Not.Empty);
            Assert.That(seed.Foods.Select(f => f.Type), Is.All.Not.Null);
        });
    }

    [Test]
    public void SeedFile_BundledSeed_HasTheApprovedTypeCounts()
    {
        using var stream = SeedLoader.OpenBundledSeed();

        var seed = SeedFile.Parse(stream);

        var counts = seed.Foods.GroupBy(f => f.Type!.Value).ToDictionary(g => g.Key, g => g.Count());
        Assert.That(counts, Is.EqualTo(new Dictionary<FoodType, int>
        {
            [FoodType.Ingredient] = 1585,
            [FoodType.Food] = 476,
            [FoodType.Dish] = 637,
            [FoodType.Drink] = 155,
        }));
    }

    [Test]
    public async Task LoadAsync_StoredOpenFoodFactsFoodWithNoType_BecomesFoodAndNothingElseChanges()
    {
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Brand, Unit, Kcal, ProteinG, CarbsG, FatG, SearchText, Type) VALUES (77, ?, 'b1', 'Mine', 'Br', ?, 10, 1, 2, 3, 'mine br', NULL)", (int)FoodSource.OpenFoodFacts, (int)FoodUnit.Gram);
        await connection.ExecuteAsync("INSERT INTO Food (Id, Source, SourceId, Name, Unit, Kcal, ProteinG, CarbsG, FatG, SearchText, Type) VALUES (78, ?, 'b2', 'Typed', ?, 10, 1, 2, 3, 'typed', ?)", (int)FoodSource.OpenFoodFacts, (int)FoodUnit.Gram, (int)FoodType.Drink);
        var before = await Snapshot(FoodSource.OpenFoodFacts);

        await Loader().LoadAsync();

        var after = await Snapshot(FoodSource.OpenFoodFacts);
        var foods = (await connection.Table<Food>().Where(f => f.Source == FoodSource.OpenFoodFacts).ToListAsync()).ToDictionary(f => f.Id);
        Assert.Multiple(() =>
        {
            Assert.That(foods[77].Type, Is.EqualTo(FoodType.Food));
            Assert.That(foods[78].Type, Is.EqualTo(FoodType.Drink));
            Assert.That(after, Is.EqualTo(before));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.SeedTypesBackfilled));
            Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Has.None.Contain("Mine"));
        });
    }

    private SeedLoader Loader() => new(store, logger, () => new MemoryStream(Encoding.UTF8.GetBytes(seedJson)));

    private async Task<List<string>> Snapshot(FoodSource source) =>
        (await connection.Table<Food>().Where(f => f.Source == source).OrderBy(f => f.Id).ToListAsync())
            .Select(f => $"{f.Id}|{f.Source}|{f.SourceId}|{f.Name}|{f.Brand}|{f.Unit}|{f.Kcal}|{f.ProteinG}|{f.CarbsG}|{f.FatG}|{f.FibreG}|{f.SugarsG}|{f.SaturatesG}|{f.SaltG}|{f.SearchText}").ToList();

    private static string Sample(int version, double changedKcal = 355, bool extra = false)
    {
        var rows = new List<string>
        {
            Row("11-001", version == 1 ? "Arrowroot" : "Arrowroot v2", changedKcal, fibre: "null", sugars: "0", saturates: "null", salt: "0.01", protein: "0.4", carbs: "94", fat: "0.1"),
            Row("11-002", "Gin", 250, unit: "Millilitre"),
            Row("11-003", "  Barley, Pearl, Raw ", 360),
        };
        if (extra)
        {
            rows.Add(Row("11-004", "Extra", 10));
        }

        return Wrap(version, rows);
    }

    private static string Wrap(int version, IEnumerable<string> rows) =>
        $$"""{"seedVersion":{{version}},"source":"CoFID 2021","foods":[{{string.Join(",\n", rows)}}]}""";

    private static string Row(string id, string name, double kcal, string unit = "Gram", string fibre = "1.5", string sugars = "1", string saturates = "0.5", string salt = "0.1", string protein = "1", string carbs = "2", string fat = "3", string type = "Ingredient") =>
        $$"""{"source":"CoFID","sourceId":"{{id}}","name":"{{name}}","originalName":"{{name}}","foodGroup":"AA","unit":"{{unit}}","type":"{{type}}","energyKcal":{{kcal}},"proteinG":{{protein}},"carbsG":{{carbs}},"fatG":{{fat}},"fibreG":{{fibre}},"sugarsG":{{sugars}},"saturatesG":{{saturates}},"saltG":{{salt}}}""";
}
