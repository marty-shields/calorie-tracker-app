using System.Net;
using System.Text;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace CalorieTracker.Core.Tests.Foods;

public class OpenFoodFactsLookupTests
{
    private const string Barcode = "3017620422003";

    private FakeHandler handler = null!;
    private FakeLogger<OpenFoodFactsLookup> logger = null!;
    private FakeTimeProvider clock = null!;
    private List<TimeSpan> waits = null!;
    private HttpClient client = null!;
    private OpenFoodFactsLookup lookup = null!;

    [SetUp]
    public void SetUp()
    {
        handler = new FakeHandler();
        logger = new FakeLogger<OpenFoodFactsLookup>();
        clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
        waits = [];
        client = new HttpClient(handler) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };
        lookup = new OpenFoodFactsLookup(client, logger, clock, (wait, _) =>
        {
            waits.Add(wait);
            return Task.CompletedTask;
        });
    }

    [TearDown]
    public void TearDown()
    {
        client.Dispose();
        handler.Dispose();
    }

    [Test]
    public async Task Solid_IsFoundInGrams()
    {
        handler.Respond(Fixture("solid-3017620422003.json"));

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        var food = result.Food!;
        Assert.Multiple(() =>
        {
            Assert.That(food.Source, Is.EqualTo(FoodSource.OpenFoodFacts));
            Assert.That(food.SourceId, Is.EqualTo(Barcode));
            Assert.That(food.Name, Is.EqualTo("Nutella"));
            Assert.That(food.Brand, Is.EqualTo("Ferrero"));
            Assert.That(food.Unit, Is.EqualTo(FoodUnit.Gram));
            Assert.That(food.Type, Is.EqualTo(FoodType.Food));
            Assert.That(food.Kcal, Is.EqualTo(539));
            Assert.That(food.ProteinG, Is.EqualTo(6.3));
            Assert.That(food.CarbsG, Is.EqualTo(57.5));
            Assert.That(food.FatG, Is.EqualTo(30.9));
            Assert.That(food.FibreG, Is.EqualTo(0));
            Assert.That(food.SugarsG, Is.EqualTo(56.3));
            Assert.That(food.SaturatesG, Is.EqualTo(10.6));
            Assert.That(food.SaltG, Is.EqualTo(0.107));
            Assert.That(food.SearchText, Is.EqualTo("nutella ferrero"));
        });
    }

    [Test]
    public async Task Drink_IsFoundInMillilitres()
    {
        handler.Respond(Fixture("drink-5449000000996.json"));

        var result = await lookup.LookupAsync("5449000000996");

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.Multiple(() =>
        {
            Assert.That(result.Food!.Unit, Is.EqualTo(FoodUnit.Millilitre));
            Assert.That(result.Food.Type, Is.EqualTo(FoodType.Drink));
            Assert.That(result.Food.Name, Is.EqualTo("Coca-Cola Original"));
            Assert.That(result.Food.Kcal, Is.EqualTo(44));
        });
    }

    [Test]
    public async Task MissingProduct_Status0_IsNotFound()
    {
        handler.Respond(Fixture("missing-0000000000017.json"));

        Assert.That((await lookup.LookupAsync("0000000000017")).Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [Test]
    public async Task Http404_IsNotFound()
    {
        handler.Respond(Fixture("not-food-8410757001090.json"), HttpStatusCode.NotFound);

        Assert.That((await lookup.LookupAsync("8410757001090")).Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [TestCase("[\"en:beverages-and-beverages-preparations\",\"en:beverages\"]", null, FoodType.Drink)]
    [TestCase("[\"en:beverages\"]", "\"g\"", FoodType.Drink)]
    [TestCase("[\"en:Beverages\"]", null, FoodType.Drink)]
    [TestCase("[\"en:breakfasts\",\"en:spreads\"]", "\"g\"", FoodType.Food)]
    [TestCase("[\"en:breakfasts\"]", "\"ml\"", FoodType.Drink)]
    [TestCase("[]", null, FoodType.Food)]
    [TestCase(null, null, FoodType.Food)]
    [TestCase("\"en:beverages\"", null, FoodType.Food)]
    [TestCase("[1,null,{}]", null, FoodType.Food)]
    public async Task Type_IsDrinkForBeveragesOrMillilitresOtherwiseFood(string? categories, string? servingUnit, FoodType expected)
    {
        handler.Respond(Body("Thing", null, AllRequired(), servingUnit: servingUnit, categories: categories));

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Food!.Type, Is.EqualTo(expected));
    }

    [Test]
    public async Task MissingRequiredValue_IsNotFound()
    {
        handler.Respond(Fixture("missing-values-6111246651261.json"));

        Assert.That((await lookup.LookupAsync("6111246651261")).Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [TestCase("energy-kcal_100g")]
    [TestCase("proteins_100g")]
    [TestCase("carbohydrates_100g")]
    [TestCase("fat_100g")]
    public async Task EachRequiredValueMissing_IsNotFound(string omitted)
    {
        var values = new Dictionary<string, string>
        {
            ["energy-kcal_100g"] = "100",
            ["proteins_100g"] = "1",
            ["carbohydrates_100g"] = "2",
            ["fat_100g"] = "3",
        };
        values.Remove(omitted);
        handler.Respond(Body("Oats", null, values));

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [TestCase("")]
    [TestCase("  ")]
    public async Task BlankName_IsNotFound(string name)
    {
        handler.Respond(Body(name, null, AllRequired()));

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [Test]
    public async Task EnergyInKilojoulesOnly_IsConvertedToKcal()
    {
        var values = AllRequired();
        values.Remove("energy-kcal_100g");
        values["energy-kj_100g"] = "418.4";
        handler.Respond(Body("Oats", null, values));

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Food!.Kcal, Is.EqualTo(100).Within(1e-9));
    }

    [Test]
    public async Task KcalWinsOverKilojoules()
    {
        var values = AllRequired();
        values["energy-kj_100g"] = "9999";
        handler.Respond(Body("Oats", null, values));

        Assert.That((await lookup.LookupAsync(Barcode)).Food!.Kcal, Is.EqualTo(100));
    }

    [Test]
    public async Task NumbersAsStrings_AreAccepted()
    {
        var values = new Dictionary<string, string>
        {
            ["energy-kcal_100g"] = "\"250.5\"",
            ["proteins_100g"] = "\"1.5\"",
            ["carbohydrates_100g"] = "\"2\"",
            ["fat_100g"] = "\"3\"",
            ["salt_100g"] = "\"0.5\"",
        };
        handler.Respond(Body("Oats", null, values));

        var food = (await lookup.LookupAsync(Barcode)).Food!;

        Assert.Multiple(() =>
        {
            Assert.That(food.Kcal, Is.EqualTo(250.5));
            Assert.That(food.ProteinG, Is.EqualTo(1.5));
            Assert.That(food.CarbsG, Is.EqualTo(2));
            Assert.That(food.FatG, Is.EqualTo(3));
            Assert.That(food.SaltG, Is.EqualTo(0.5));
        });
    }

    [Test]
    public async Task NonNumericString_CountsAsMissing()
    {
        var values = AllRequired();
        values["proteins_100g"] = "\"lots\"";
        handler.Respond(Body("Oats", null, values));

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [Test]
    public async Task NegativeRequiredValue_IsNotFound()
    {
        var values = AllRequired();
        values["proteins_100g"] = "-1";
        handler.Respond(Body("Oats", null, values));

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [Test]
    public async Task OverflowingNumber_CountsAsMissing_AndDoesNotThrow()
    {
        var values = AllRequired();
        values["fat_100g"] = "1e400";
        handler.Respond(Body("Oats", null, values));

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
    }

    [Test]
    public async Task AccentedNameAndBrand_AreFoldedInSearchText()
    {
        handler.Respond(Body("Crème fraîche", "Président", AllRequired()));

        var food = (await lookup.LookupAsync(Barcode)).Food!;

        Assert.That((food.Name, food.SearchText), Is.EqualTo(("Crème fraîche", "creme fraiche president")));
    }

    [Test]
    public async Task OnlyFirstBrand_IsKept_AndEmptyBrandIsNull()
    {
        handler.Respond(Body("Oats", " Quaker , Other", AllRequired()));
        var first = (await lookup.LookupAsync(Barcode)).Food!;
        handler.Respond(Body("Oats", " , Other", AllRequired()));
        var empty = (await lookup.LookupAsync(Barcode)).Food!;

        Assert.Multiple(() =>
        {
            Assert.That(first.Brand, Is.EqualTo("Quaker"));
            Assert.That(first.SearchText, Is.EqualTo("oats quaker"));
            Assert.That(empty.Brand, Is.Null);
            Assert.That(empty.SearchText, Is.EqualTo("oats"));
        });
    }

    [Test]
    public async Task ResponseCodeDifferingFromTheBarcode_IsTheSourceId()
    {
        handler.Respond(Body("Oats", null, AllRequired()).Replace("\"code\":\"1\"", "\"code\":\"9780000000002\""));

        var food = (await lookup.LookupAsync(Barcode)).Food!;

        Assert.That(food.SourceId, Is.EqualTo("9780000000002"));
    }

    [TestCase("abc123")]
    [TestCase("../../evil")]
    [TestCase("97800000000٠2")]
    [TestCase("12 34")]
    public async Task ResponseCodeWithNonDigits_FallsBackToTheScannedBarcode(string code)
    {
        handler.Respond(Body("Oats", null, AllRequired()).Replace("\"code\":\"1\"", $"\"code\":\"{code}\""));

        var food = (await lookup.LookupAsync(Barcode)).Food!;

        Assert.That(food.SourceId, Is.EqualTo(Barcode));
    }

    [TestCase("1234567")]
    [TestCase("123456789012345")]
    [TestCase("abcdefgh")]
    [TestCase("1234567a90123")]
    [TestCase("")]
    [TestCase("١٢٣٤٥٦٧٨")]
    public async Task InvalidBarcode_IsNotFoundWithoutAnHttpCall(string barcode)
    {
        handler.Respond(Body("Oats", null, AllRequired()));

        var result = await lookup.LookupAsync(barcode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
            Assert.That(result.Food, Is.Null);
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [TestCase("12345678")]
    [TestCase("3017620422003")]
    [TestCase("12345678901234")]
    public async Task BarcodeOf8To14Digits_IsLookedUp(string barcode)
    {
        handler.Respond(Body("Oats", null, AllRequired()));

        var result = await lookup.LookupAsync(barcode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
        });
    }

    [TestCase("036000291452")]
    public async Task NormalisedUpcA_IsLookedUp(string upcA)
    {
        Assert.That(BarcodeNormaliser.TryNormalise(upcA, out var barcode), Is.True);
        handler.Respond(Body("Oats", null, AllRequired()));

        Assert.That((await lookup.LookupAsync(barcode)).Status, Is.EqualTo(BarcodeLookupStatus.Found));
    }

    [Test]
    public async Task ResponseWithoutCode_UsesTheScannedBarcodeAsSourceId()
    {
        handler.Respond(Body("Oats", null, AllRequired()).Replace("\"code\":\"1\",", ""));

        var food = (await lookup.LookupAsync(Barcode)).Food!;

        Assert.That(food.SourceId, Is.EqualTo(Barcode));
    }

    [Test]
    public async Task OptionalValuesAbsent_AreNull()
    {
        handler.Respond(Body("  Oats ", null, AllRequired()));

        var food = (await lookup.LookupAsync(Barcode)).Food!;

        Assert.Multiple(() =>
        {
            Assert.That(food.Name, Is.EqualTo("Oats"));
            Assert.That(food.FibreG, Is.Null);
            Assert.That(food.SugarsG, Is.Null);
            Assert.That(food.SaturatesG, Is.Null);
            Assert.That(food.SaltG, Is.Null);
        });
    }

    [TestCase("\"ML\"", null, FoodUnit.Millilitre)]
    [TestCase("\"ml\"", "\"g\"", FoodUnit.Millilitre)]
    [TestCase("\"g\"", "\"ml\"", FoodUnit.Millilitre)]
    [TestCase(null, "\"ml\"", FoodUnit.Millilitre)]
    [TestCase("\"g\"", "\"g\"", FoodUnit.Gram)]
    [TestCase(null, null, FoodUnit.Gram)]
    public async Task Unit_FollowsProductThenServingQuantityUnit(string? productUnit, string? servingUnit, FoodUnit expected)
    {
        handler.Respond(Body("Oats", null, AllRequired(), productUnit, servingUnit));

        Assert.That((await lookup.LookupAsync(Barcode)).Food!.Unit, Is.EqualTo(expected));
    }

    [Test]
    public async Task Request_IsAGetOfTheProductUrlAskingOnlyForMappedFields()
    {
        handler.Respond(Fixture("solid-3017620422003.json"));

        await lookup.LookupAsync(Barcode);

        var request = handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Get));
            Assert.That(request.Uri.GetLeftPart(UriPartial.Path), Is.EqualTo("https://world.openfoodfacts.org/api/v2/product/" + Barcode));
            Assert.That(request.Uri.Query, Is.EqualTo("?fields=product_name,brands,nutriments,product_quantity_unit,serving_quantity_unit,categories_tags"));
        });
    }

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task RateLimitedOnce_IsRetriedAndFound(HttpStatusCode limited)
    {
        handler.Respond("", limited);
        handler.Respond(Fixture("solid-3017620422003.json"));

        var result = await lookup.LookupAsync(Barcode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
            Assert.That(handler.Requests, Has.Count.EqualTo(2));
            Assert.That(waits, Is.EqualTo(new[] { OpenFoodFactsLookup.DefaultRetryWait }));
        });
    }

    [TestCase(HttpStatusCode.TooManyRequests)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    public async Task RateLimitedEveryTime_IsRateLimitedAfterThreeAttempts(HttpStatusCode limited)
    {
        handler.Respond("", limited);

        var result = await lookup.LookupAsync(Barcode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.RateLimited));
            Assert.That(handler.Requests, Has.Count.EqualTo(3));
            Assert.That(waits, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task RetryAfter_WithinTheCap_IsHonoured()
    {
        handler.Respond("", HttpStatusCode.TooManyRequests, retryAfter: "4");
        handler.Respond(Fixture("solid-3017620422003.json"));

        await lookup.LookupAsync(Barcode);

        Assert.That(waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(4) }));
    }

    [Test]
    public async Task RetryAfter_EqualToTheCap_IsHonoured()
    {
        handler.Respond("", HttpStatusCode.TooManyRequests, retryAfter: "5");
        handler.Respond(Fixture("solid-3017620422003.json"));

        await lookup.LookupAsync(Barcode);

        Assert.That(waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(5) }));
    }

    [Test]
    public async Task RetryAfter_HttpDateInThePast_FallsBackToTheDefaultWait()
    {
        handler.Respond("", HttpStatusCode.TooManyRequests, retryAfter: "Sat, 14 Nov 2026 11:59:00 GMT");
        handler.Respond(Fixture("solid-3017620422003.json"));

        await lookup.LookupAsync(Barcode);

        Assert.That(waits, Is.EqualTo(new[] { OpenFoodFactsLookup.DefaultRetryWait }));
    }

    [Test]
    public async Task RetryAfter_AsHttpDate_IsHonouredRelativeToTheClock()
    {
        handler.Respond("", HttpStatusCode.TooManyRequests, retryAfter: "Sat, 14 Nov 2026 12:00:03 GMT");
        handler.Respond(Fixture("solid-3017620422003.json"));

        await lookup.LookupAsync(Barcode);

        Assert.That(waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(3) }));
    }

    [Test]
    public async Task RetryAfter_OverTheCap_FallsBackToTheDefaultWait()
    {
        handler.Respond("", HttpStatusCode.TooManyRequests, retryAfter: "120");
        handler.Respond(Fixture("solid-3017620422003.json"));

        await lookup.LookupAsync(Barcode);

        Assert.That(waits, Is.EqualTo(new[] { OpenFoodFactsLookup.DefaultRetryWait }));
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.GatewayTimeout)]
    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.BadRequest)]
    public async Task OtherErrorStatuses_AreOfflineWithoutRetry(HttpStatusCode status)
    {
        handler.Respond(Fixture("solid-3017620422003.json"), status);

        var result = await lookup.LookupAsync(Barcode);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Offline));
            Assert.That(handler.Requests, Has.Count.EqualTo(1));
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.LookupUnexpectedStatus));
        });
    }

    [Test]
    public async Task NoNetwork_IsOffline()
    {
        handler.Throw(new HttpRequestException("no route"));

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.Offline));
    }

    [Test]
    public async Task Timeout_IsOffline()
    {
        handler.Throw(new TaskCanceledException("timed out", new TimeoutException()));

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.Offline));
    }

    [Test]
    public async Task CallerCancellation_PropagatesAndIsNotATimeout()
    {
        using var cts = new CancellationTokenSource();
        handler.Throw(new TaskCanceledException());
        cts.Cancel();

        await Assert.ThatAsync(() => lookup.LookupAsync(Barcode, cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase("not json")]
    [TestCase("")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"status\":\"1\"}")]
    [TestCase("{\"status\":2}")]
    [TestCase("{\"status\":1}")]
    [TestCase("{\"status\":1,\"product\":[]}")]
    public async Task MalformedBody_IsOffline(string body)
    {
        handler.Respond(body);

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.Offline));
    }

    [Test]
    public async Task LoneSurrogateInName_IsOfflineWithoutThrowing()
    {
        handler.Respond(Body("Bad\\ud800Name", null, AllRequired()));

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.Offline));
    }

    [Test]
    public async Task InvalidUtf8InBody_IsOfflineWithoutThrowing()
    {
        var json = Encoding.UTF8.GetBytes(Body("BadXX", null, AllRequired()));
        var index = Array.IndexOf(json, (byte)'X');
        json[index] = 0xC3;
        json[index + 1] = 0x28;
        var bytes = json;
        handler.RespondBytes(bytes);

        Assert.That((await lookup.LookupAsync(Barcode)).Status, Is.EqualTo(BarcodeLookupStatus.Offline));
    }

    [Test]
    public async Task Logging_AllPaths_LogStatusesOnlyNeverBarcodeNameBrandOrBody()
    {
        const string secretBarcode = "7351234500019";
        var values = AllRequired();
        handler.Respond("", HttpStatusCode.TooManyRequests);
        handler.Respond(Body("Distinctive Name", "Distinctive Brand", values));
        await lookup.LookupAsync(secretBarcode);

        handler.Respond("{\"status\":0,\"status_verbose\":\"Distinctive Body\"}");
        await lookup.LookupAsync(secretBarcode);
        handler.Respond("Distinctive Body", HttpStatusCode.TooManyRequests);
        await lookup.LookupAsync(secretBarcode);
        handler.Throw(new HttpRequestException("Distinctive offline " + secretBarcode));
        await lookup.LookupAsync(secretBarcode);
        handler.Respond("Distinctive Body");
        await lookup.LookupAsync(secretBarcode);
        handler.Respond("Distinctive Body", HttpStatusCode.InternalServerError);
        await lookup.LookupAsync(secretBarcode);
        handler.Respond("Distinctive Body", HttpStatusCode.NotFound);
        await lookup.LookupAsync(secretBarcode);

        var records = logger.Collector.GetSnapshot();
        Assert.That(LoggedEvents.Of(logger).Select(e => (int)e.Id).Distinct(), Is.EquivalentTo(Enumerable.Range(1201, 8)));
        foreach (var record in records)
        {
            var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}")) + record.Exception;
            Assert.That(text, Does.Not.Contain("Distinctive").And.Not.Contain(secretBarcode));
        }
    }

    private static Dictionary<string, string> AllRequired() => new()
    {
        ["energy-kcal_100g"] = "100",
        ["proteins_100g"] = "1",
        ["carbohydrates_100g"] = "2",
        ["fat_100g"] = "3",
    };

    private static string Body(string name, string? brands, Dictionary<string, string> nutriments, string? productUnit = null, string? servingUnit = null, string? categories = null)
    {
        var extra = (brands is null ? "" : $",\"brands\":\"{brands}\"")
            + (productUnit is null ? "" : $",\"product_quantity_unit\":{productUnit}")
            + (servingUnit is null ? "" : $",\"serving_quantity_unit\":{servingUnit}")
            + (categories is null ? "" : $",\"categories_tags\":{categories}");
        var nutrients = string.Join(",", nutriments.Select(n => $"\"{n.Key}\":{n.Value}"));
        return $"{{\"code\":\"1\",\"product\":{{\"product_name\":\"{name}\"{extra},\"nutriments\":{{{nutrients}}}}},\"status\":1}}";
    }

    private static string Fixture(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Foods", "Fixtures", file));

    /// <summary>Plays queued responses in order; the last one repeats.</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> queue = new();
        private Func<HttpResponseMessage>? last;

        public List<(HttpMethod Method, Uri Uri, string UserAgent)> Requests { get; } = [];

        public void Respond(string body, HttpStatusCode status = HttpStatusCode.OK, string? retryAfter = null) =>
            queue.Enqueue(() =>
            {
                var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                if (retryAfter is not null)
                {
                    response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
                }

                return response;
            });

        public void RespondBytes(byte[] body) =>
            queue.Enqueue(() =>
            {
                var content = new ByteArrayContent(body);
                content.Headers.ContentType = new("application/json");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            });

        public void Throw(Exception exception) => queue.Enqueue(() => throw exception);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!, request.Headers.UserAgent.ToString()));
            if (queue.Count > 0)
            {
                last = queue.Dequeue();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(last!());
        }
    }
}
