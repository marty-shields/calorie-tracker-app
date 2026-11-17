using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

/// <summary>
/// What a tap on a search result does (the barcode lookup, then the import service), and AC1 and AC2 of the search: the products of a recorded search, imported one by one as taps would, through the real lookup mapping over the saved product responses.
/// </summary>
public class OnlineFoodImportTests
{
    private const string CanCode = "5000112637922";
    private const string OriginalCode = "5449000214911";
    private const string ZeroCode = "5449000131805";
    private const string LightCode = "5449000050205";
    private const string OreoCode = "7622300336738";
    private const string UkCode = "5000112604856";
    private const string LitreCode = "5000112637939";

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FoodStore store = null!;
    private FakeLogger<OnlineFoodImport> logger = null!;
    private FakeLogger<FoodImportService> importLogger = null!;
    private FoodImportService importer = null!;
    private Dictionary<string, string> responses = null!;
    private HttpClient http = null!;
    private OpenFoodFactsLookup realLookup = null!;
    private OnlineFoodImport import = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<Barcode>();
        store = new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>());
        importLogger = new FakeLogger<FoodImportService>();
        importer = new FoodImportService(store, new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero)), importLogger);
        logger = new FakeLogger<OnlineFoodImport>();
        responses = new()
        {
            [CanCode] = Fixture("details-cola-can-5000112637922.json"),
            [OriginalCode] = Fixture("details-cola-original-5449000214911.json"),
            [ZeroCode] = Fixture("details-cola-zero-5449000131805.json"),
            [LightCode] = Fixture("details-cola-light-5449000050205.json"),
            [OreoCode] = Fixture("details-oreo-7622300336738.json"),
            [UkCode] = Fixture("details-cola-uk-03kcal-5000112604856.json"),
            [LitreCode] = Resized(LitreCode, "Coca Cola 1 L", "1 L", 1000),
        };
        http = new HttpClient(new DetailsHandler(responses)) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };
        realLookup = new OpenFoodFactsLookup(http, new FakeLogger<OpenFoodFactsLookup>(), TimeProvider.System);
        import = new OnlineFoodImport(realLookup, importer, logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        http.Dispose();
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private static string Fixture(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Foods", "Fixtures", file));

    /// <summary>The can's saved response with another pack, name and barcode; the brand and nutrients are the can's, as the can's 1 L bottle has them.</summary>
    private static string Resized(string barcode, string name, string quantity, double packMl)
    {
        var root = JsonNode.Parse(Fixture("details-cola-can-5000112637922.json"))!.AsObject();
        root["code"] = barcode;
        var product = root["product"]!.AsObject();
        product["product_name"] = name;
        product["quantity"] = quantity;
        product["product_quantity"] = packMl;
        product.Remove("serving_size");
        product.Remove("serving_quantity");
        product.Remove("serving_quantity_unit");
        return root.ToJsonString();
    }

    private Task<List<Serving>> Servings(int foodId) =>
        connection.QueryAsync<Serving>("SELECT * FROM Serving WHERE FoodId = ? ORDER BY SortOrder, Amount, Id", foodId);

    /// <summary>A search over a saved response; <paramref name="without"/> drops the hits with these barcodes first.</summary>
    private OnlineFoodSearch SearchOver(string fixture, out HttpClient client, params string[] without)
    {
        var handler = new FakeSearchHandler();
        var body = JsonNode.Parse(Fixture(fixture))!.AsObject();
        var hits = body["hits"]!.AsArray();
        foreach (var hit in hits.Where(h => without.Contains((string?)h!["code"])).ToList())
        {
            hits.Remove(hit);
        }

        handler.Respond(body.ToJsonString());
        client = new HttpClient(handler) { BaseAddress = new Uri("https://search.openfoodfacts.org/") };
        return new OnlineFoodSearch(client, store, new FakeLogger<OnlineFoodSearch>(), TimeProvider.System, () => null);
    }

    [Test]
    public async Task Ac1_SearchingColaOffersTheCan_AndTheCanThenTheLitreBottleAreOneFoodWithTwoPackServings()
    {
        // The saved search lists Original 330 mL before the can, and the two have the same name, pack and values, so only Original would be offered: drop it to see the can.
        using var search = SearchOver("search-cola-variants.json", out var client, OriginalCode);
        using var _ = client;

        var found = await search.SearchAsync("coca-cola");
        var can = await import.ImportAsync(found.Products.Single(p => p.Barcode == CanCode).Barcode);
        var bottle = await import.ImportAsync(LitreCode);

        var food = (await connection.Table<Food>().ToListAsync()).Single(f => f.Id == can.Food!.Id);
        Assert.Multiple(async () =>
        {
            Assert.That(found.Products.Select(p => p.Barcode), Does.Contain(CanCode));
            Assert.That(found.Products.Single(p => p.Barcode == CanCode).Food.PackageLabel, Is.EqualTo("330 ml"));
            Assert.That((can.Status, bottle.Status), Is.EqualTo((OnlineImportStatus.Imported, OnlineImportStatus.Imported)));
            Assert.That(bottle.Food!.Id, Is.EqualTo(can.Food!.Id), "one food");
            Assert.That((can.PackAmount, bottle.PackAmount), Is.EqualTo((330.0, 1000.0)), "each tap knows its own pack");
            Assert.That((await Servings(food.Id)).Where(s => s.Source == ServingSource.Pack).Select(s => (s.Label, s.Amount)), Is.EqualTo(new[] { ("Can 330 ml", 330.0), ("Bottle 1 L", 1000.0) }));
            Assert.That(await connection.Table<Barcode>().CountAsync(), Is.EqualTo(2));
            Assert.That((await search.SearchAsync("coca-cola")).Products.Where(p => p.Barcode is CanCode).Select(p => p.IsNew), Is.EqualTo(new[] { false }), "a stored product is no longer new");
        });
    }

    [Test]
    public async Task Ac2_ZeroLightOreoAndTheLowEnergyCocaCola_NeverMergeIntoOriginalOrEachOther()
    {
        using var search = SearchOver("search-cola-variants.json", out var client);
        using var _ = client;
        var found = await search.SearchAsync("coca-cola");

        var results = new List<OnlineImportResult>();
        foreach (var product in found.Products)
        {
            results.Add(await import.ImportAsync(product.Barcode));
        }

        // The can collapses into Original in the search (same name, pack and values), and the 0.3 kcal "Coca-cola" has no pack size, so it is not offered there;
        // each is stored from its barcode all the same (a search of its own, or a scan), and none merges into Original.
        results.Add(await import.ImportAsync(CanCode));
        results.Add(await import.ImportAsync(UkCode));
        var foods = await connection.Table<Food>().ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(found.Products.Select(p => p.Barcode), Is.EquivalentTo(new[] { OriginalCode, ZeroCode, LightCode, OreoCode }));
            Assert.That(results.Select(r => r.Status), Is.All.EqualTo(OnlineImportStatus.Imported));
            Assert.That(results.Select(r => r.Food!.Id).Distinct().Count(), Is.EqualTo(6), "every product is a food of its own");
            Assert.That(foods, Has.Count.EqualTo(6));
            Assert.That(foods.Single(f => f.SourceId == OriginalCode).Kcal, Is.EqualTo(42));
            Assert.That(foods.Single(f => f.SourceId == UkCode).Kcal, Is.EqualTo(0.3));
        });
    }

    [Test]
    public async Task Import_ATappedProductTwice_IsOneFood()
    {
        var first = await import.ImportAsync(CanCode);
        var second = await import.ImportAsync(CanCode);

        Assert.Multiple(async () =>
        {
            Assert.That(second.Food!.Id, Is.EqualTo(first.Food!.Id));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_AFoodTheLookupAlreadyStored_IsReturnedAsItIs_WithoutImportingAgain()
    {
        var looked = await realLookup.LookupAsync(CanCode);
        var stored = (await importer.ImportAsync(CanCode, looked.Food!, looked.Serving)).Food!;
        var lookup = new FakeLookup(BarcodeLookupResult.Found(stored, 330));
        var tap = new OnlineFoodImport(lookup, importer, logger);
        var servings = await connection.Table<Serving>().CountAsync();
        var importerLines = importLogger.Collector.Count;

        var result = await tap.ImportAsync(CanCode);

        Assert.Multiple(async () =>
        {
            Assert.That((result.Status, result.Food!.Id, result.PackAmount), Is.EqualTo((OnlineImportStatus.Imported, stored.Id, (double?)330)));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
            Assert.That(await connection.Table<Serving>().CountAsync(), Is.EqualTo(servings), "not imported a second time");
            Assert.That(importLogger.Collector.Count, Is.EqualTo(importerLines), "the import service was not called");
        });
    }

    [Test]
    public async Task Import_A12DigitUpc_IsLookedUpAndImportedAsItsThirteenDigitCode()
    {
        var lookup = new FakeLookup(BarcodeLookupResult.Found(new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "0123456789012",
            Name = "Upc food",
            Unit = FoodUnit.Gram,
            Kcal = 100,
            ProteinG = 1,
            CarbsG = 2,
            FatG = 3,
        }));
        var tap = new OnlineFoodImport(lookup, importer, logger);

        var result = await tap.ImportAsync("123456789012");

        Assert.Multiple(async () =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineImportStatus.Imported));
            Assert.That(lookup.Asked, Is.EqualTo(new[] { "0123456789012" }));
            Assert.That((await connection.Table<Barcode>().ToListAsync()).Select(b => b.Code), Is.EqualTo(new[] { "0123456789012" }));
        });
    }

    [Test]
    public async Task Import_AnUnsavedProductWithAServing_StoresItAsTheProductRow()
    {
        var food = new Food { Source = FoodSource.OpenFoodFacts, SourceId = "0123456789012", Name = "Serving food", Unit = FoodUnit.Gram, Kcal = 100, ProteinG = 1, CarbsG = 2, FatG = 3 };
        var tap = new OnlineFoodImport(new FakeLookup(BarcodeLookupResult.Found(food, serving: new ProductServing("1 bar (40 g)", 40))), importer, logger);

        var result = await tap.ImportAsync("123456789012");

        var rows = await connection.Table<Serving>().ToListAsync();
        Assert.That(rows.Select(r => (r.FoodId, r.Label, r.Amount, r.Source)), Is.EqualTo(new[] { (result.Food!.Id, "1 bar (40 g)", 40.0, ServingSource.Product) }));
    }

    [TestCase(BarcodeLookupStatus.NotFound, OnlineImportStatus.NotFound)]
    [TestCase(BarcodeLookupStatus.Offline, OnlineImportStatus.Offline)]
    [TestCase(BarcodeLookupStatus.RateLimited, OnlineImportStatus.Limited)]
    public async Task Import_ALookupThatGivesNoProduct_IsReportedAndStoresNothing(BarcodeLookupStatus lookupStatus, OnlineImportStatus expected)
    {
        var lookup = new FakeLookup(lookupStatus switch
        {
            BarcodeLookupStatus.Offline => BarcodeLookupResult.Offline(),
            BarcodeLookupStatus.RateLimited => BarcodeLookupResult.RateLimited(),
            _ => BarcodeLookupResult.NotFound(),
        });
        var tap = new OnlineFoodImport(lookup, importer, logger);

        var result = await tap.ImportAsync(CanCode);

        Assert.Multiple(async () =>
        {
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.Food, Is.Null);
            Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
        });
    }

    [TestCase("")]
    [TestCase("123")]
    [TestCase("not a barcode")]
    public async Task Import_ABarcodeThatIsNotOne_IsNotFound_WithoutALookup(string barcode)
    {
        var lookup = new FakeLookup(BarcodeLookupResult.NotFound());
        var tap = new OnlineFoodImport(lookup, importer, logger);

        var result = await tap.ImportAsync(barcode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineImportStatus.NotFound));
            Assert.That(lookup.Asked, Is.Empty);
        });
    }

    [Test]
    public async Task Import_AnIncompleteProductFromTheLookup_IsNotFound()
    {
        var lookup = new FakeLookup(BarcodeLookupResult.Found(new Food { Source = FoodSource.OpenFoodFacts, SourceId = CanCode, Name = "", Unit = FoodUnit.Gram }));
        var tap = new OnlineFoodImport(lookup, importer, logger);

        var result = await tap.ImportAsync(CanCode);

        Assert.That(result.Status, Is.EqualTo(OnlineImportStatus.NotFound));
    }

    [Test]
    public async Task Import_WhenTheDatabaseFails_IsFailed_WithoutThrowing()
    {
        await connection.ExecuteAsync("DROP TABLE Barcode");

        var result = await import.ImportAsync(CanCode);

        Assert.That(result.Status, Is.EqualTo(OnlineImportStatus.Failed));
    }

    [Test]
    public async Task Logging_EveryPath_CarriesIdsAndStatusesOnly_NeverABarcodeOrName()
    {
        var offline = new OnlineFoodImport(new FakeLogLookup(BarcodeLookupResult.Offline()), importer, logger);
        await import.ImportAsync(CanCode);
        await import.ImportAsync(CanCode);
        await offline.ImportAsync(ZeroCode);
        await import.ImportAsync("123");
        await connection.ExecuteAsync("DROP TABLE Barcode");
        await import.ImportAsync(LightCode);

        var records = logger.Collector.GetSnapshot();
        var text = string.Join('\n', records.Select(r => r.Message + " " + string.Join(' ', r.StructuredState?.Select(p => p.Value) ?? [])));
        Assert.Multiple(() =>
        {
            Assert.That(new[] { CanCode, ZeroCode, LightCode, "123", "Coca", "Cola" }.Where(text.Contains), Is.Empty);
            Assert.That(records.Select(r => (CoreEvent)r.Id.Id).Distinct(), Is.EquivalentTo(new[] { CoreEvent.OnlineImported, CoreEvent.OnlineImportLookupFailed, CoreEvent.OnlineImportStoreFailed }));
        });
    }

    private sealed class FakeLogLookup(BarcodeLookupResult result) : IBarcodeLookup
    {
        public Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default) => Task.FromResult(result);
    }

    private sealed class FakeLookup(BarcodeLookupResult result) : IBarcodeLookup
    {
        public List<string> Asked { get; } = [];

        public Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
        {
            Asked.Add(barcode);
            return Task.FromResult(result);
        }
    }

    /// <summary>Answers <c>api/v2/product/{barcode}</c> with the saved response for the barcode.</summary>
    private sealed class DetailsHandler(Dictionary<string, string> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var barcode = request.RequestUri!.Segments.Last();
            return Task.FromResult(responses.TryGetValue(barcode, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"status\":0}", Encoding.UTF8, "application/json") });
        }
    }
}
