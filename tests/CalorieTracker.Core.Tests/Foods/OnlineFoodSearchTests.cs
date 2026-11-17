using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

/// <summary>The Open Food Facts text search against saved Search-a-licious responses (see the README in <c>Fixtures</c>); no test calls the live service.</summary>
public class OnlineFoodSearchTests
{
    private const string Typed = "zq-secret-cola";

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeSearchHandler handler = null!;
    private FakeLogger<OnlineFoodSearch> logger = null!;
    private FakeTimeProvider clock = null!;
    private List<TimeSpan> waits = null!;
    private HttpClient client = null!;
    private string? region;
    private OnlineFoodSearch search = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Barcode>();
        handler = new FakeSearchHandler();
        logger = new FakeLogger<OnlineFoodSearch>();
        clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
        waits = [];
        client = new HttpClient(handler) { BaseAddress = new Uri("https://search.openfoodfacts.org/") };
        region = "GB";
        search = new OnlineFoodSearch(
            client,
            new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>()),
            logger,
            clock,
            () => region,
            (wait, _) =>
            {
                waits.Add(wait);
                clock.Advance(wait);
                return Task.CompletedTask;
            });
    }

    [TearDown]
    public async Task TearDown()
    {
        search.Dispose();
        client.Dispose();
        handler.Dispose();
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private static string Fixture(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Foods", "Fixtures", file));

    /// <summary>A response with these hits; each is the JSON of one hit.</summary>
    private static string Hits(params string[] hits) => $"{{\"hits\":[{string.Join(',', hits)}],\"page\":1,\"page_size\":50}}";

    private static string Hit(string code, string? name, string? quantity, double? kcal = 42, double protein = 0, double carbs = 10.6, double fat = 0, string brands = "[\"Coca-Cola\"]", string? categories = null)
    {
        var hit = new JsonObject { ["code"] = code, ["brands"] = JsonNode.Parse(brands) };
        if (name is not null)
        {
            hit["product_name"] = name;
        }

        if (quantity is not null)
        {
            hit["quantity"] = quantity;
        }

        if (categories is not null)
        {
            hit["categories_tags"] = JsonNode.Parse(categories);
        }

        if (kcal is not null)
        {
            hit["nutriments"] = new JsonObject { ["energy-kcal_100g"] = kcal, ["proteins_100g"] = protein, ["carbohydrates_100g"] = carbs, ["fat_100g"] = fat };
        }

        return hit.ToJsonString();
    }

    [Test]
    public async Task Search_InTheUk_SendsOneRequestWithTheCountryFilterInsideTheQuery_AndReturnsTheCleanedHits()
    {
        handler.Respond(Fixture("search-coca-cola-uk.json"));

        var result = await search.SearchAsync("coca-cola");

        var request = handler.Requests.Single();
        var barcodes = result.Products.Select(p => p.Barcode).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Found));
            Assert.That(result.Country, Is.EqualTo("United Kingdom"));
            Assert.That(request.Uri.Host, Is.EqualTo("search.openfoodfacts.org"));
            Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/search"));
            Assert.That(handler.Queries.Single(), Is.EqualTo("coca-cola countries_tags:\"en:united-kingdom\""));
            Assert.That(request.Uri.Query, Does.Contain("page_size=50").And.Contain("fields="));
            Assert.That(request.Uri.Query, Does.Not.Contain("filters"));
            Assert.That(OnlineFoodSearch.UserAgent, Does.StartWith("CalorieTracker.Core/").And.Not.Contain("@").And.Not.Contain("http").And.Not.Contain(" "), "a generic name and version, no contact address");
            Assert.That(barcodes, Has.Count.InRange(1, 20));
            Assert.That(barcodes, Does.Contain("5000112630633").And.Contain("5000112641981"), "a 330 ml and a 1.5 L with nutrition");
            Assert.That(barcodes, Does.Not.Contain("5000112663914"), "no nutrition");
            Assert.That(barcodes, Does.Not.Contain("5000112604856"), "no pack size and no category, so no unit");
            Assert.That(barcodes, Does.Not.Contain("5449000052971"), "no product name");
            Assert.That(barcodes, Is.Unique);
        });
    }

    [Test]
    public async Task Search_AHitIsMappedToAFoodWithTheNamePackBrandAndPer100Values()
    {
        handler.Respond(Hits(Hit("5000112630633", "Coca cola", "330 ml", brands: "[\"Coca-Cola\",\" Coca-Cola Company\",\"coca-cola\"]", categories: "[\"en:beverages\"]")));

        var product = (await search.SearchAsync("cola")).Products.Single();

        var food = product.Food;
        Assert.Multiple(() =>
        {
            Assert.That(product.Barcode, Is.EqualTo("5000112630633"));
            Assert.That(product.IsNew);
            Assert.That(food.Id, Is.Zero);
            Assert.That((food.Source, food.SourceId, food.Name, food.Brand, food.Brands), Is.EqualTo((FoodSource.OpenFoodFacts, "5000112630633", "Coca cola", "Coca-Cola", "Coca-Cola, Coca-Cola Company")));
            Assert.That((food.Unit, food.PackageLabel), Is.EqualTo((FoodUnit.Millilitre, "330 ml")));
            Assert.That((food.Kcal, food.ProteinG, food.CarbsG, food.FatG), Is.EqualTo((42.0, 0.0, 10.6, 0.0)));
            Assert.That(food.PackageAmount, Is.Null, "the pack in ml comes from the barcode lookup");
            Assert.That(food.SearchText, Is.EqualTo("coca cola coca-cola coca-cola company"));
        });
    }

    [TestCase("330 ml", FoodUnit.Millilitre)]
    [TestCase("1.5L", FoodUnit.Millilitre)]
    [TestCase("1,25 l", FoodUnit.Millilitre)]
    [TestCase("8 x 330 ml", FoodUnit.Millilitre)]
    [TestCase("250 ml x 4", FoodUnit.Millilitre)]
    [TestCase("8x330ml", FoodUnit.Millilitre)]
    [TestCase("33 cl", FoodUnit.Millilitre)]
    [TestCase("154g", FoodUnit.Gram)]
    [TestCase("1 kg", FoodUnit.Gram)]
    [TestCase("400 g e", FoodUnit.Gram)]
    public async Task Search_TheUnitComesFromTheQuantityText(string quantity, FoodUnit unit)
    {
        handler.Respond(Hits(Hit("5000112630633", "Thing", quantity)));

        var result = await search.SearchAsync("thing");

        Assert.That(result.Products.Single().Food.Unit, Is.EqualTo(unit));
    }

    [Test]
    public async Task Search_WithNoQuantity_TheBeverageCategoryGivesMillilitresAndAnyOtherCategoryGrams()
    {
        handler.Respond(Hits(
            Hit("5000112630633", "Drink", null, categories: "[\"en:beverages\",\"en:sodas\"]"),
            Hit("5000112630640", "Food", null, categories: "[\"en:snacks\"]", kcal: 300)));

        var products = (await search.SearchAsync("thing")).Products;

        Assert.That(products.Select(p => (p.Barcode, p.Food.Unit)), Is.EqualTo(new[] { ("5000112630633", FoodUnit.Millilitre), ("5000112630640", FoodUnit.Gram) }));
    }

    [Test]
    public async Task Search_AnIncompleteHit_IsNotOffered_AndDoesNotBreakTheList()
    {
        var withoutKcal = JsonNode.Parse(Hit("5000112630701", "No kcal", "330 ml"))!.AsObject();
        withoutKcal["nutriments"]!.AsObject().Remove("energy-kcal_100g");
        var kilojoulesOnly = JsonNode.Parse(Hit("5000112630718", "Kilojoules", "330 ml"))!.AsObject();
        kilojoulesOnly["nutriments"]!.AsObject().Remove("energy-kcal_100g");
        kilojoulesOnly["nutriments"]!["energy-kj_100g"] = 418.4;
        var withoutFat = JsonNode.Parse(Hit("5000112630725", "No fat", "330 ml"))!.AsObject();
        withoutFat["nutriments"]!.AsObject().Remove("fat_100g");
        var withoutProtein = JsonNode.Parse(Hit("5000112630756", "No protein", "330 ml"))!.AsObject();
        withoutProtein["nutriments"]!.AsObject().Remove("proteins_100g");
        var withoutCarbs = JsonNode.Parse(Hit("5000112630763", "No carbs", "330 ml"))!.AsObject();
        withoutCarbs["nutriments"]!.AsObject().Remove("carbohydrates_100g");
        var notANumber = JsonNode.Parse(Hit("5000112630770", "Not a number", "330 ml"))!.AsObject();
        notANumber["nutriments"]!["energy-kcal_100g"] = "NaN";
        handler.Respond(Hits(
            Hit("5000112630633", null, "330 ml"),
            Hit("5000112630640", "   ", "330 ml"),
            Hit("5000112630787", "Negative kcal", "330 ml", kcal: -5),
            Hit("5000112630794", "Negative fat", "330 ml", fat: -1),
            withoutProtein.ToJsonString(),
            withoutCarbs.ToJsonString(),
            notANumber.ToJsonString(),
            Hit("5000112630657", "No unit", null),
            Hit("5000112630664", "No nutrition", "330 ml", kcal: null),
            Hit("123", "Short barcode", "330 ml"),
            Hit("", "No barcode", "330 ml"),
            withoutKcal.ToJsonString(),
            withoutFat.ToJsonString(),
            "null",
            "42",
            "\"text\"",
            "[]",
            "{}",
            "{\"code\":\"5000112630732\",\"product_name\":\"Nutriments are text\",\"quantity\":\"1 l\",\"nutriments\":\"none\"}",
            kilojoulesOnly.ToJsonString(),
            Hit("5000112630749", "Fine", "330 ml")));

        var result = await search.SearchAsync("thing");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Found));
            Assert.That(result.Products.Select(p => p.Barcode), Is.EqualTo(new[] { "5000112630718", "5000112630749" }));
            Assert.That(result.Products[0].Food.Kcal, Is.EqualTo(100).Within(0.001), "kilojoules become kcal");
        });
    }

    [Test]
    public async Task Search_ALongNameBrandAndPack_AreCutAtTheirLimits_NeverInsideASurrogatePair()
    {
        var manyBrands = string.Join(", ", Enumerable.Range(0, 25).Select(i => $"brand{i:000}"));
        handler.Respond(Hits(
            Hit("5000112630633", new string('n', 150), new string('1', 150) + " ml", brands: $"[\"{new string('b', 150)}\"]"),
            Hit("5000112630640", "Many brands", "330 ml", brands: JsonNode.Parse(JsonSerializer.Serialize(manyBrands.Split(", ")))!.ToJsonString()),
            Hit("5000112630657", new string('a', 99) + "\U0001F600z", "330 ml")));

        var foods = (await search.SearchAsync("thing")).Products.Select(p => p.Food).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(foods[0].Name, Is.EqualTo(new string('n', 100)));
            Assert.That(foods[0].Brand, Is.EqualTo(new string('b', 100)));
            Assert.That(foods[0].Brands, Is.EqualTo(new string('b', 150)));
            Assert.That(foods[0].PackageLabel, Is.EqualTo(new string('1', 100)));
            Assert.That(foods[1].Brands, Has.Length.EqualTo(199).And.StartWith("brand000, brand001").And.EndsWith("brand019,"));
            Assert.That(foods[2].Name, Is.EqualTo(new string('a', 99)));
        });
    }

    [Test]
    public async Task Search_ASingleBrand_IsBothTheBrandAndTheBrands()
    {
        handler.Respond(Hits(Hit("5000112630633", "Cola", "330 ml", brands: "[\"Coca-Cola\"]")));

        var food = (await search.SearchAsync("cola")).Products.Single().Food;

        Assert.That((food.Brand, food.Brands), Is.EqualTo(("Coca-Cola", "Coca-Cola")));
    }

    [Test]
    public async Task Search_BrandsAsOneCommaSeparatedString_AreSplit()
    {
        handler.Respond(Hits(Hit("5000112630633", "Cola", "330 ml", brands: "\"Coca-Cola, Pepsi ,coca-cola\"")));

        var food = (await search.SearchAsync("cola")).Products.Single().Food;

        Assert.That((food.Brand, food.Brands), Is.EqualTo(("Coca-Cola", "Coca-Cola, Pepsi")));
    }

    [Test]
    public async Task Search_AHitWithNoBrands_HasNoBrand()
    {
        handler.Respond(Hits(Hit("5000112630633", "Cola", "330 ml", brands: "[]")));

        var food = (await search.SearchAsync("cola")).Products.Single().Food;

        Assert.That((food.Brand, food.Brands), Is.EqualTo(((string?)null, (string?)null)));
    }

    [Test]
    public async Task Search_ACommaOrADotInThePackSize_CollapsesTheSame()
    {
        handler.Respond(Hits(Hit("5000112630633", "Cola", "1,5 l"), Hit("5000112630640", "Cola", "1.5 l")));

        var result = await search.SearchAsync("cola");

        Assert.That(result.Products.Select(p => p.Barcode), Is.EqualTo(new[] { "5000112630633" }));
    }

    [Test]
    public async Task Search_WhenEveryHitIsIncomplete_IsNone()
    {
        handler.Respond(Hits(Hit("5000112630633", null, "330 ml"), Hit("5000112630640", "No unit", null)));
        region = null;

        var result = await search.SearchAsync("thing");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.None));
            Assert.That(result.Products, Is.Empty);
        });
    }

    [Test]
    public async Task Search_HitsThatMatchOnNamePackAndPer100Values_CollapseIntoTheFirst()
    {
        handler.Respond(Hits(
            Hit("5000112630633", "Coca cola", "330 ml"),
            Hit("5449000000996", "Coca-Cola", "330ml"),
            Hit("5000112545326", "Coca-Cola Original 330 ml", "330 ml"),
            Hit("5000112641981", "Coca cola", "1.5L"),
            Hit("5000112604856", "Coca-cola", "330 ml", kcal: 0.3),
            Hit("5449000043757", "Coca-cola zero", "330 ml"),
            Hit("5000112552508", "Coca-cola", "330 ml", kcal: 42.4)));

        var result = await search.SearchAsync("cola");

        Assert.That(result.Products.Select(p => p.Barcode), Is.EqualTo(new[] { "5000112630633", "5000112545326", "5000112641981", "5000112604856", "5449000043757", "5000112552508" }),
            "the 330 ml of the same name and values collapse; another size, another name and other values stay");
    }

    [Test]
    public async Task Search_KeepsAtMostTwentyProducts()
    {
        handler.Respond(Hits(Enumerable.Range(0, 30).Select(i => Hit($"50001126{i:00000}", $"Cola {(char)('a' + i)}", "330 ml")).ToArray()));

        var result = await search.SearchAsync("cola");

        Assert.That(result.Products.Select(p => p.Food.Name), Is.EqualTo(Enumerable.Range(0, 20).Select(i => $"Cola {(char)('a' + i)}")));
    }

    [Test]
    public async Task Search_ProductsAlreadyStored_AreNotNew()
    {
        await connection.InsertAsync(new Barcode { FoodId = 1, Code = "5000112630633", FirstSeen = clock.GetUtcNow() });
        handler.Respond(Hits(Hit("5000112630633", "Coca cola", "330 ml"), Hit("5000112641981", "Coca cola", "1.5L")));

        var result = await search.SearchAsync("cola");

        Assert.That(result.Products.Select(p => (p.Barcode, p.IsNew)), Is.EqualTo(new[] { ("5000112630633", false), ("5000112641981", true) }));
    }

    [Test]
    public async Task Search_ACachedResult_ReadsWhatIsStoredNow()
    {
        handler.Respond(Hits(Hit("5000112630633", "Coca cola", "330 ml")));
        var before = await search.SearchAsync("cola");
        await connection.InsertAsync(new Barcode { FoodId = 1, Code = "5000112630633", FirstSeen = clock.GetUtcNow() });

        var after = await search.SearchAsync("cola");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(before.Products.Single().IsNew);
            Assert.That(after.Products.Single().IsNew, Is.False);
        });
    }

    [Test]
    public async Task Search_WhenTheStoredBarcodesCannotBeRead_StillReturnsTheProducts_NoneMarkedNew()
    {
        await connection.ExecuteAsync("DROP TABLE Barcode");
        handler.Respond(Hits(Hit("5000112630633", "Coca cola", "330 ml")));

        var result = await search.SearchAsync("cola");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Found));
            Assert.That(result.Products.Single().IsNew, Is.False);
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.OnlineSearchStoredCheckFailed));
        });
    }

    [Test]
    public async Task Search_WithNothingFound_IsNone()
    {
        handler.Respond(Fixture("search-none.json"));
        region = null;

        var result = await search.SearchAsync("zxqvbnmkjh");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.None));
            Assert.That(result.Products, Is.Empty);
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Search_Http429_IsLimited_AndIsNotCached()
    {
        handler.Respond("{}", HttpStatusCode.TooManyRequests);
        handler.Respond(Fixture("search-cola-variants.json"));

        var limited = await search.SearchAsync("cola");
        var again = await search.SearchAsync("cola");

        Assert.Multiple(() =>
        {
            Assert.That(limited.Status, Is.EqualTo(OnlineSearchStatus.Limited));
            Assert.That(limited.Products, Is.Empty);
            Assert.That(again.Status, Is.EqualTo(OnlineSearchStatus.Found));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Search_NoResponse_IsOffline_AndIsNotCached()
    {
        handler.Throw(new HttpRequestException("no network"));
        handler.Throw(new IOException("connection reset"));
        handler.Throw(new TaskCanceledException("timeout"));
        handler.Respond(Fixture("search-cola-variants.json"));

        Assert.Multiple(async () =>
        {
            Assert.That((await search.SearchAsync("cola")).Status, Is.EqualTo(OnlineSearchStatus.Offline));
            Assert.That((await search.SearchAsync("cola")).Status, Is.EqualTo(OnlineSearchStatus.Offline));
            Assert.That((await search.SearchAsync("cola")).Status, Is.EqualTo(OnlineSearchStatus.Offline), "a timeout is offline too");
            Assert.That((await search.SearchAsync("cola")).Status, Is.EqualTo(OnlineSearchStatus.Found));
            Assert.That(handler.Requests, Has.Count.EqualTo(4));
        });
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.UnprocessableEntity)]
    public async Task Search_AnyOtherHttpError_IsFailed(HttpStatusCode status)
    {
        handler.Respond("{\"detail\":\"oops\"}", status);

        var result = await search.SearchAsync("cola");

        Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Failed));
    }

    [TestCase("not json")]
    [TestCase("")]
    [TestCase("[]")]
    [TestCase("{\"page\":1}")]
    [TestCase("{\"hits\":{}}")]
    [TestCase("{\"hits\":[{\"code\":\"5000112630633\",\"product_name\":\"\\ud800\",\"quantity\":\"1 l\"}]}")]
    public async Task Search_ABodyThatIsNotTheExpectedJson_IsFailed(string body)
    {
        handler.Respond(body);

        var result = await search.SearchAsync("cola");

        Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Failed));
    }

    [Test]
    public async Task Search_TheCallersCancellation_Throws()
    {
        handler.Respond(Fixture("search-cola-variants.json"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(async () => await search.SearchAsync("cola", cancelled.Token));
    }

    [Test]
    public async Task Search_TheSameTextAgain_ReusesTheCachedResult_WithNoRequest()
    {
        handler.Respond(Fixture("search-cola-variants.json"));

        var first = await search.SearchAsync("coca-cola");
        var again = await search.SearchAsync("coca-cola");
        var spaced = await search.SearchAsync("  Coca-Cola  ");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(waits, Is.Empty);
            Assert.That(again.Products.Select(p => p.Barcode), Is.EqualTo(first.Products.Select(p => p.Barcode)));
            Assert.That(spaced.Products, Has.Count.EqualTo(first.Products.Count));
            Assert.That(again.Country, Is.EqualTo(first.Country));
        });
    }

    [Test]
    public async Task Search_The21stDistinctQuery_EvictsTheOldest()
    {
        handler.Respond(Fixture("search-cola-variants.json"));
        for (var i = 0; i < 21; i++)
        {
            await search.SearchAsync($"cola {i}");
        }

        await search.SearchAsync("cola 1");
        var afterSecond = handler.Requests.Count;
        await search.SearchAsync("cola 0");

        Assert.Multiple(() =>
        {
            Assert.That(afterSecond, Is.EqualTo(21), "the second is still cached");
            Assert.That(handler.Requests, Has.Count.EqualTo(22), "the first was dropped and is sent again");
        });
    }

    [Test]
    public async Task Search_The20thDistinctQuery_KeepsTheFirstCached()
    {
        handler.Respond(Fixture("search-cola-variants.json"));
        for (var i = 0; i < 20; i++)
        {
            await search.SearchAsync($"cola {i}");
        }

        await search.SearchAsync("cola 0");

        Assert.That(handler.Requests, Has.Count.EqualTo(20));
    }

    [Test]
    public async Task Search_AnotherCountry_IsAnotherCacheEntry()
    {
        handler.Respond(Fixture("search-cola-variants.json"));
        await search.SearchAsync("cola");
        region = "FR";

        await search.SearchAsync("cola");

        Assert.That(handler.Queries, Is.EqualTo(new[] { "cola countries_tags:\"en:united-kingdom\"", "cola countries_tags:\"en:france\"" }));
    }

    [Test]
    public async Task Search_RequestsAreAtLeastSixSecondsApart_ASoonerOneWaitsTheRest()
    {
        handler.Respond(Fixture("search-cola-variants.json"));
        await search.SearchAsync("cola");
        clock.Advance(TimeSpan.FromSeconds(2.5));

        await search.SearchAsync("oreo");
        await search.SearchAsync("zero");

        Assert.Multiple(() =>
        {
            Assert.That(waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(6) }));
            Assert.That(handler.Requests, Has.Count.EqualTo(3));
        });
    }

    [Test]
    public async Task Search_ARequestAfterTheGap_DoesNotWait()
    {
        handler.Respond(Fixture("search-cola-variants.json"));
        await search.SearchAsync("cola");
        clock.Advance(TimeSpan.FromSeconds(6));

        await search.SearchAsync("oreo");

        Assert.That(waits, Is.Empty);
    }

    [Test]
    public async Task Search_ACountryWithNothing_RepeatsOnceWorldwide_AndTheHeadingSaysSo()
    {
        handler.Respond(Fixture("search-none.json"));
        handler.Respond(Fixture("search-cola-variants.json"));

        var result = await search.SearchAsync("cola");
        var cached = await search.SearchAsync("cola");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Queries, Is.EqualTo(new[] { "cola countries_tags:\"en:united-kingdom\"", "cola" }));
            Assert.That(waits, Is.EqualTo(new[] { OnlineFoodSearch.MinGap }), "the repeat is a request too");
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Found));
            Assert.That(result.Country, Is.Null);
            Assert.That(cached.Country, Is.Null);
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Search_ACountryWithNothingAndNothingWorldwide_IsNone_AfterOneRepeat()
    {
        handler.Respond(Fixture("search-none.json"));

        var result = await search.SearchAsync("zxqvbnmkjh");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.None));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Search_ACountryRequestThatFails_IsNotRepeatedWorldwide()
    {
        handler.Respond("{}", HttpStatusCode.TooManyRequests);

        var result = await search.SearchAsync("cola");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Limited));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        });
    }

    [TestCase(null)]
    [TestCase("ZZ")]
    [TestCase("UK")]
    public async Task Search_WithNoRegionOrAnUnmappedCode_RunsWorldwide(string? code)
    {
        region = code;
        handler.Respond(Fixture("search-cola-variants.json"));

        var result = await search.SearchAsync("cola");

        Assert.Multiple(() =>
        {
            Assert.That(handler.Queries.Single(), Is.EqualTo("cola"));
            Assert.That(result.Country, Is.Null);
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.Found));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("\"\\ \"")]
    public async Task Search_ABlankQuery_FindsNothing_AndSendsNothing(string? query)
    {
        var result = await search.SearchAsync(query!);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(OnlineSearchStatus.None));
            Assert.That(handler.Requests, Is.Empty);
            Assert.That(logger.Collector.Count, Is.Zero);
        });
    }

    [Test]
    public async Task Search_QuotesAndBackslashesAreDropped_SoTheyCannotBreakTheCountryFilter_AndALongQueryIsCut()
    {
        handler.Respond(Fixture("search-cola-variants.json"));
        await search.SearchAsync("coca\"-cola\\ \"zero\" " + new string('x', 200));

        var sent = handler.Queries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(sent, Does.StartWith("coca cola zero x"));
            Assert.That(sent, Does.EndWith(" countries_tags:\"en:united-kingdom\""));
            Assert.That(sent.Count(c => c == '"'), Is.EqualTo(2), "only the filter's quotes");
            Assert.That(sent.Length, Is.EqualTo(100 + " countries_tags:\"en:united-kingdom\"".Length));
        });
    }

    [TestCase("salt (and) vinegar", "salt and vinegar")]
    [TestCase("a [b] {c} d:e f/g", "a b c d e f g")]
    [TestCase("ab^2 ~x *y z? w!", "ab 2 x y z w")]
    [TestCase("-cola +zero co-la", "cola zero co-la")]
    [TestCase("cola AND zero OR lime NOT diet", "cola zero lime diet")]
    [TestCase("cola && zero || lime", "cola zero lime")]
    [TestCase("salt and vinegar", "salt and vinegar")]
    [TestCase("- + AND", "")]
    public async Task Search_TheQuerySyntaxOfTheSearchIsDropped_SoTheTextIsOnlyWords(string typed, string sent)
    {
        region = null;
        handler.Respond(Fixture("search-cola-variants.json"));

        var result = await search.SearchAsync(typed);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Queries, sent.Length == 0 ? Is.Empty : Is.EqualTo(new[] { sent }));
            Assert.That(result.Status, Is.EqualTo(sent.Length == 0 ? OnlineSearchStatus.None : OnlineSearchStatus.Found));
        });
    }

    [Test]
    public async Task Logging_EveryPath_CarriesCountsAndStatusesOnly_NeverTheQueryOrABarcode()
    {
        // Found, cached, waiting, answered, worldwide repeat, limited, no response, malformed, unexpected status, stored check failed.
        handler.Respond(Fixture("search-none.json"));
        handler.Respond(Fixture("search-cola-variants.json"));
        handler.Respond("{}", HttpStatusCode.TooManyRequests);
        handler.Throw(new HttpRequestException(Typed));
        handler.Respond("not json " + Typed);
        handler.Respond(Typed, HttpStatusCode.InternalServerError);
        handler.Respond(Hits(Hit("5000112630633", Typed, "330 ml")));
        await search.SearchAsync(Typed);
        await search.SearchAsync(Typed);
        await search.SearchAsync(Typed + " 2");
        await search.SearchAsync(Typed + " 3");
        await search.SearchAsync(Typed + " 4");
        await search.SearchAsync(Typed + " 5");
        await connection.ExecuteAsync("DROP TABLE Barcode");
        region = null;
        await search.SearchAsync(Typed + " 6");

        var records = logger.Collector.GetSnapshot();
        var text = string.Join('\n', records.Select(r => r.Message + " " + string.Join(' ', r.StructuredState?.Select(p => p.Value) ?? [])));
        var barcodes = new[] { "5000112630633", "5449000214911", "5000112637922", "5449000131805", "5449000050205", "7622300336738", "5000112604856" };
        var ids = records.Select(r => (CoreEvent)r.Id.Id).Distinct().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Not.Contain("secret"));
            Assert.That(text.Contains("cola", StringComparison.OrdinalIgnoreCase), Is.False);
            Assert.That(barcodes.Where(text.Contains), Is.Empty);
            Assert.That(text, Does.Not.Contain("en:").And.Not.Contain("http"));
            Assert.That(records.Select(r => r.Exception), Is.All.Null);
            Assert.That(ids, Is.SupersetOf(new[]
            {
                CoreEvent.OnlineSearchSent, CoreEvent.OnlineSearchCached, CoreEvent.OnlineSearchAnswered, CoreEvent.OnlineSearchWaiting, CoreEvent.OnlineSearchWorldwideRepeat,
                CoreEvent.OnlineSearchLimited, CoreEvent.OnlineSearchNoResponse, CoreEvent.OnlineSearchMalformed, CoreEvent.OnlineSearchUnexpectedStatus, CoreEvent.OnlineSearchStoredCheckFailed,
            }));
        });
    }

    [Test]
    public void Logging_TheLinesTakeCountsFlagsAndStatusesOnly()
    {
        var lines = typeof(OnlineFoodSearch).GetNestedType("Log", System.Reflection.BindingFlags.NonPublic)!
            .GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
            .Where(m => m.GetCustomAttributes(typeof(LoggerMessageAttribute), false).Length > 0)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Count.EqualTo(10));
            foreach (var line in lines)
            {
                Assert.That(line.GetParameters().Skip(1).Select(p => p.ParameterType), Is.All.Matches<Type>(t => t == typeof(int) || t == typeof(bool)), line.Name);
            }
        });
    }
}
