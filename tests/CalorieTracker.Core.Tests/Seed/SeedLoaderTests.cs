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
            Assert.That((foods["11-001"].Brand, foods["11-001"].Barcode), Is.EqualTo(((string?)null, (string?)null)));
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
    public async Task LoadAsync_OpenFoodFactsRows_AreUntouched()
    {
        await Loader().LoadAsync();
        await store.UpsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = "11-001", Name = "Off", Barcode = "5000000000017", Unit = FoodUnit.Gram, Kcal = 1, ProteinG = 2, CarbsG = 3, FatG = 4, SearchText = "off" });
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

    [Test]
    public void SeedFile_BundledSeed_IsComplete()
    {
        using var stream = SeedLoader.OpenBundledSeed();

        var seed = SeedFile.Parse(stream);

        Assert.Multiple(() =>
        {
            Assert.That(seed.Version, Is.GreaterThanOrEqualTo(1));
            Assert.That(seed.Foods, Has.Count.EqualTo(2853));
            Assert.That(seed.Foods.Select(f => f.Source), Is.All.EqualTo(FoodSource.CoFID));
            Assert.That(seed.Foods.Select(f => f.SourceId), Is.Unique);
            Assert.That(seed.Foods.Select(f => f.Name), Is.All.Not.Empty);
        });
    }

    private SeedLoader Loader() => new(store, logger, () => new MemoryStream(Encoding.UTF8.GetBytes(seedJson)));

    private async Task<List<string>> Snapshot(FoodSource source) =>
        (await connection.Table<Food>().Where(f => f.Source == source).OrderBy(f => f.Id).ToListAsync())
            .Select(f => $"{f.Id}|{f.SourceId}|{f.Name}|{f.Barcode}|{f.Kcal}|{f.SearchText}").ToList();

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

    private static string Row(string id, string name, double kcal, string unit = "Gram", string fibre = "1.5", string sugars = "1", string saturates = "0.5", string salt = "0.1", string protein = "1", string carbs = "2", string fat = "3") =>
        $$"""{"source":"CoFID","sourceId":"{{id}}","name":"{{name}}","originalName":"{{name}}","foodGroup":"AA","unit":"{{unit}}","energyKcal":{{kcal}},"proteinG":{{protein}},"carbsG":{{carbs}},"fatG":{{fat}},"fibreG":{{fibre}},"sugarsG":{{sugars}},"saturatesG":{{saturates}},"saltG":{{salt}}}""";
}
