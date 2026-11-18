using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

public class StoredBarcodeLookupTests
{
    private const string Barcode = "5000000000017";
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FoodStore store = null!;
    private FakeLogger<StoredBarcodeLookup> logger = null!;
    private FakeInner inner = null!;
    private FakeLogger<FoodImportService> importLogger = null!;
    private FoodImportService importer = null!;
    private StoredBarcodeLookup lookup = null!;

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
        logger = new FakeLogger<StoredBarcodeLookup>();
        inner = new FakeInner();
        importLogger = new FakeLogger<FoodImportService>();
        importer = new FoodImportService(store, TimeProvider.System, importLogger);
        lookup = new StoredBarcodeLookup(inner, store, importer, logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task Lookup_BarcodeInFoodTable_ReturnsItWithoutCallingInner()
    {
        var saved = await store.UpsertAsync(NewFood("1"), Barcode);

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(result.Food!.Id, Is.EqualTo(saved.Id));
        Assert.That(inner.Calls, Is.Zero);
    }

    [Test]
    public async Task Lookup_StoredFoodWithAFetchTimeAndNoServing_NeverAsksInner()
    {
        await store.UpsertAsync(NewFood("1"), Barcode);

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.Zero);
    }

    [Test]
    public async Task Lookup_StoredFoodWithNoFetchTime_ReturnsAtOnceWhileTheRefreshIsStillWaiting()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        var release = inner.BlockNextCall();
        inner.Enqueue(Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode).WaitAsync(Bound);
        await inner.FirstCallArrived.WaitAsync(Bound);

        Assert.That(first.Food!.FetchedAt, Is.Null);
        release.SetResult();
        await lookup.RefreshesIdle().WaitAsync(Bound);
        Assert.That((await lookup.LookupAsync(Barcode)).Food!.FetchedAt, Is.Not.Null);
    }

    [Test]
    public async Task Lookup_FoodWithNoFetchTimeScannedByAMergedBarcode_KeepsItsOwnValuesAndAsksAgainNextScan()
    {
        const string attached = "5000000000024";
        var stored = await store.UpsertAsync(NoDetails("1"), Barcode);
        await connection.InsertAsync(new Barcode { FoodId = stored.Id, Code = attached, FirstSeen = DateTimeOffset.UnixEpoch });
        var merged = NewFood(attached);
        merged.Kcal = 999;
        inner.Enqueue(Found(merged));
        inner.Enqueue(Found(merged));

        await lookup.LookupAsync(attached);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        await lookup.LookupAsync(attached);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        var row = await connection.Table<Food>().FirstAsync();
        Assert.That((inner.Calls, row.FetchedAt, row.Kcal), Is.EqualTo((2, (DateTimeOffset?)null, 100.0)));
    }

    [Test]
    public async Task Lookup_Refresh_RefreshesEveryNutrientAndChangesNothingElseOnTheRow()
    {
        var original = NoDetails("1");
        original.Name = "Stored name";
        original.Brand = "Stored brand";
        original.Type = FoodType.Drink;
        original.SearchText = "stored";
        original.FibreG = 1;
        original.SaltG = 1;
        var stored = await store.UpsertAsync(original, Barcode);
        var other = await store.UpsertAsync(NewFood("2"), "5000000000024");
        var fresh = NewFood("1");
        fresh.Name = "New name";
        fresh.Brand = "New brand";
        fresh.Type = FoodType.Food;
        fresh.Kcal = 201;
        fresh.ProteinG = 21;
        fresh.CarbsG = 22;
        fresh.FatG = 23;
        fresh.FibreG = null;
        fresh.SugarsG = 2.5;
        fresh.SaturatesG = 3.5;
        fresh.SaltG = 0.25;
        inner.Enqueue(Found(fresh));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        var rows = await connection.Table<Food>().ToListAsync();
        var row = rows.Single(r => r.Id == stored.Id);
        Assert.Multiple(async () =>
        {
            Assert.That((row.Kcal, row.ProteinG, row.CarbsG, row.FatG), Is.EqualTo((201.0, 21.0, 22.0, 23.0)));
            Assert.That((row.FibreG, row.SugarsG, row.SaturatesG, row.SaltG), Is.EqualTo(((double?)null, (double?)2.5, (double?)3.5, (double?)0.25)));
            Assert.That(await connection.Table<Serving>().CountAsync(), Is.Zero);
            Assert.That((row.Name, row.Brand, row.Type, row.SearchText, row.Unit), Is.EqualTo(("Stored name", "Stored brand", FoodType.Drink, "oats", FoodUnit.Gram)));
            Assert.That((row.Source, row.SourceId), Is.EqualTo((FoodSource.OpenFoodFacts, "1")));
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows.Single(r => r.Id == other.Id).Kcal, Is.EqualTo(100.0));
        });
        Assert.That((await store.FindByBarcodeAsync(Barcode))!.Id, Is.EqualTo(stored.Id));
    }

    [Test]
    public async Task Lookup_ScanDuringARunningRefresh_DoesNotStartAnother()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        var release = inner.BlockNextCall();
        inner.Enqueue(Found(NewFood("1")));

        await lookup.LookupAsync(Barcode);
        await inner.FirstCallArrived.WaitAsync(Bound);
        await lookup.LookupAsync(Barcode);
        release.SetResult();
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_RefreshWithTheRealLookup_StoresNoServingWhateverTheRecordHolds()
    {
        const string weetabix = "5010029000023";
        var body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Foods", "Fixtures", "details-weetabix-5010029000023.json"));
        using var http = new HttpClient(new JsonHandler(body)) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };
        var real = new StoredBarcodeLookup(new OpenFoodFactsLookup(http, new FakeLogger<OpenFoodFactsLookup>(), TimeProvider.System), store, importer, logger);
        await store.UpsertAsync(NoDetails(weetabix), weetabix);

        await real.LookupAsync(weetabix);
        await real.RefreshesIdle().WaitAsync(Bound);
        await real.LookupAsync(weetabix);
        await real.RefreshesIdle().WaitAsync(Bound);

        var row = await connection.Table<Food>().FirstAsync();
        Assert.That(row.FetchedAt, Is.Not.Null);
        Assert.That(await connection.Table<Serving>().CountAsync(), Is.Zero);
        Assert.That(LoggedEvents.Of(logger).Count(e => e.Id == CoreEvent.StoredLookupRefreshed), Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_StoredFoodTheSourceNoLongerHas_IsLeftAloneAndAskedAgainOnALaterScan()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        inner.Enqueue(BarcodeLookupResult.NotFound());
        inner.Enqueue(BarcodeLookupResult.NotFound());

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var restarted = new StoredBarcodeLookup(inner, store, importer, logger);
        var second = await restarted.LookupAsync(Barcode);
        await restarted.RefreshesIdle().WaitAsync(Bound);

        Assert.That(second.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That((await connection.Table<Food>().FirstAsync()).FetchedAt, Is.Null);
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Not.Contain(CoreEvent.StoredLookupRefreshed));
    }

    [Test]
    public async Task Lookup_RefreshedFoodOfAnotherUnit_IsLeftUntouchedAndAskedAgain()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        var millilitres = NewFood("1");
        millilitres.Unit = FoodUnit.Millilitre;
        millilitres.Kcal = 999;
        inner.Enqueue(Found(millilitres));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var result = await lookup.LookupAsync(Barcode);

        await AssertUntouchedAndAskedAgainAsync(result);
    }

    [Test]
    public async Task Lookup_RefreshedFoodOfAnotherProduct_IsLeftUntouchedAndAskedAgain()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        inner.Enqueue(Found(NewFood("2")));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var result = await lookup.LookupAsync(Barcode);

        await AssertUntouchedAndAskedAgainAsync(result);
    }

    [TestCase(BarcodeLookupStatus.Offline)]
    [TestCase(BarcodeLookupStatus.RateLimited)]
    public async Task Lookup_StoredFoodWhenTheSourceIsUnavailable_ReturnsItAndAsksAgainNextScan(BarcodeLookupStatus status)
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        inner.Enqueue(status == BarcodeLookupStatus.Offline ? BarcodeLookupResult.Offline() : BarcodeLookupResult.RateLimited());
        inner.Enqueue(Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var second = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var third = await lookup.LookupAsync(Barcode);

        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(second.Food!.FetchedAt, Is.Null);
        Assert.That(third.Food!.FetchedAt, Is.Not.Null);
        Assert.That(inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_StoredFoodAndTheRefreshThrows_StillReturnsTheStoredFood()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        inner.EnqueueException(new InvalidOperationException("refresh failed"));

        var result = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupRefreshFailed));
    }

    [Test]
    public async Task Lookup_RefreshFailsToSave_LogsItAndAsksAgainNextScan()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        inner.Enqueue(Found(NewFood("1")));
        await connection.ExecuteAsync("CREATE TRIGGER block_update BEFORE UPDATE ON Food BEGIN SELECT RAISE(ABORT, 'blocked'); END");

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        await connection.ExecuteAsync("DROP TRIGGER block_update");
        inner.Enqueue(Found(NewFood("1")));
        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupRefreshFailed));
        Assert.That((await lookup.LookupAsync(Barcode)).Food!.FetchedAt, Is.Not.Null);
    }

    private async Task AssertUntouchedAndAskedAgainAsync(BarcodeLookupResult result)
    {
        var row = await connection.Table<Food>().FirstAsync();
        Assert.That(result.Food!.FetchedAt, Is.Null);
        Assert.That(await connection.Table<Serving>().CountAsync(), Is.Zero);
        Assert.That((row.Kcal, row.ProteinG, row.CarbsG, row.FatG, row.Unit), Is.EqualTo((100.0, 1.0, 2.0, 3.0, FoodUnit.Gram)));
        Assert.That(inner.Calls, Is.EqualTo(2), "the second scan asked the source again");

        var later = new StoredBarcodeLookup(inner, store, importer, logger);
        inner.Enqueue(Found(NewFood("1")));
        await later.LookupAsync(Barcode);
        await later.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.EqualTo(3), "a fresh instance asked again");
    }

    [Test]
    public async Task Logging_Refresh_LogsIdsOnly()
    {
        const string filledBarcode = "9876543212101";
        const string otherBarcode = "9876543212118";
        const string failedBarcode = "9876543212125";
        foreach (var (id, barcode) in new[] { ("SRC-SECRET-A", filledBarcode), ("SRC-SECRET-B", otherBarcode), ("SRC-SECRET-C", failedBarcode) })
        {
            var food = NoDetails(id);
            food.Name = $"Distinctive Name {id}";
            food.Brand = "Distinctive Brand";
            await store.UpsertAsync(food, barcode);
        }

        inner.Enqueue(Found(NewFood("SRC-SECRET-A")));
        inner.Enqueue(Found(NewFood("SRC-SECRET-OTHER")));
        inner.EnqueueException(new InvalidOperationException("Distinctive failure"));

        foreach (var barcode in new[] { filledBarcode, otherBarcode, failedBarcode })
        {
            await lookup.LookupAsync(barcode);
            await lookup.RefreshesIdle().WaitAsync(Bound);
        }

        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[]
        {
            CoreEvent.StoredLookupStoreHit,
            CoreEvent.StoredLookupRefreshed,
            CoreEvent.StoredLookupStoreHit,
            CoreEvent.StoredLookupStoreHit,
            CoreEvent.StoredLookupRefreshFailed,
        }));
        AssertNoContentLogged(filledBarcode, otherBarcode, failedBarcode, "Distinctive failure");
    }

    [Test]
    public async Task Lookup_InnerFound_SavesAndReturnsStoredFoodWithId()
    {
        inner.Enqueue(Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);
        var second = await lookup.LookupAsync(Barcode);

        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(first.Food!.Id, Is.GreaterThan(0));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        Assert.That(second.Food!.Id, Is.EqualTo(first.Food.Id));
        Assert.That(inner.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_TwoBarcodesOfOneOpenFoodFactsProduct_StoreOneFoodWithTwoLinks()
    {
        const string product = "{\"code\":\"5000000000017\",\"status\":1,\"product\":{\"product_name\":\"Oats\",\"nutriments\":{\"energy-kcal_100g\":370,\"proteins_100g\":11,\"carbohydrates_100g\":60,\"fat_100g\":7}}}";
        using var http = new HttpClient(new JsonHandler(product)) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };
        var real = new StoredBarcodeLookup(
            new OpenFoodFactsLookup(http, new FakeLogger<OpenFoodFactsLookup>(), TimeProvider.System),
            store,
            importer,
            logger);

        var first = await real.LookupAsync("5000000000017");
        var second = await real.LookupAsync("5000000000024");

        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        Assert.That(await connection.Table<Barcode>().CountAsync(), Is.EqualTo(2));
        Assert.That(second.Food!.Id, Is.EqualTo(first.Food!.Id));
    }

    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    [Test]
    public async Task Lookup_InnerFound_LinksTheBarcodeToTheStoredFood()
    {
        inner.Enqueue(Found(NewFood("1")));

        var result = await lookup.LookupAsync(Barcode);

        var link = await connection.Table<Barcode>().FirstAsync();
        Assert.That((link.Code, link.FoodId), Is.EqualTo((Barcode, result.Food!.Id)));
    }

    [Test]
    public async Task Lookup_TwoBarcodesOneProduct_BothServedFromTheStoreAfterOneExtraInnerCall()
    {
        const string otherBarcode = "5000000000024";
        inner.Enqueue(Found(NewFood("1")));
        inner.Enqueue(Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);
        var second = await lookup.LookupAsync(otherBarcode);
        Assert.That(inner.Calls, Is.EqualTo(2));

        var firstAgain = await lookup.LookupAsync(Barcode);
        var secondAgain = await lookup.LookupAsync(otherBarcode);

        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That(new[] { second.Food!.Id, firstAgain.Food!.Id, secondAgain.Food!.Id }, Is.All.EqualTo(first.Food!.Id));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        Assert.That(await connection.Table<Barcode>().CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_InnerNotFound_ReturnsNotFoundAndSavesNothing()
    {
        inner.Enqueue(BarcodeLookupResult.NotFound());

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Lookup_NotFoundTwice_CallsInnerOnce()
    {
        inner.Enqueue(BarcodeLookupResult.NotFound());

        await lookup.LookupAsync(Barcode);
        var second = await lookup.LookupAsync(Barcode);

        Assert.That(second.Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
        Assert.That(inner.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_NotFoundForOneBarcode_DoesNotAffectAnother()
    {
        inner.Enqueue(BarcodeLookupResult.NotFound());
        inner.Enqueue(Found(NewFood("2")));

        await lookup.LookupAsync(Barcode);
        var other = await lookup.LookupAsync("5000000000024");

        Assert.That(other.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_Offline_ReturnsOfflineUnsavedAndNotRemembered()
    {
        inner.Enqueue(BarcodeLookupResult.Offline());
        inner.Enqueue(Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);
        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Offline));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);

        var second = await lookup.LookupAsync(Barcode);

        Assert.That(second.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_RateLimited_ReturnsRateLimitedUnsavedAndNotRemembered()
    {
        inner.Enqueue(BarcodeLookupResult.RateLimited());
        inner.Enqueue(Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);
        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.RateLimited));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);

        var second = await lookup.LookupAsync(Barcode);

        Assert.That(second.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_ConcurrentCallsSameBarcode_ShareOneInnerCall()
    {
        var release = inner.BlockNextCall();
        inner.Enqueue(Found(NewFood("1")));

        var calls = Enumerable.Range(0, 5).Select(_ => lookup.LookupAsync(Barcode)).ToArray();
        await inner.FirstCallArrived.WaitAsync(Bound);
        release.SetResult();
        var results = await Task.WhenAll(calls).WaitAsync(Bound);

        Assert.That(results.Select(r => r.Food!.Id).Distinct().Count(), Is.EqualTo(1));
        Assert.That(inner.Calls, Is.EqualTo(1));
        Assert.That(inner.Tokens.Select(t => t.CanBeCanceled), Is.All.False);
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_ConcurrentCallsDifferentBarcodes_EachCallInner()
    {
        var release = inner.BlockNextCall();
        inner.Enqueue(Found(NewFood("1")));
        inner.Enqueue(Found(NewFood("2")));

        var first = lookup.LookupAsync(Barcode);
        await inner.FirstCallArrived.WaitAsync(Bound);
        var second = await lookup.LookupAsync("5000000000024").WaitAsync(Bound);

        Assert.That(first.IsCompleted, Is.False);
        release.SetResult();
        var results = new[] { await first.WaitAsync(Bound), second };

        Assert.That(results.Select(r => r.Status), Is.All.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That(inner.Barcodes, Is.EquivalentTo(new[] { Barcode, "5000000000024" }));
    }

    [Test]
    public async Task Lookup_AfterSharedCallFinishes_NewCallIsNotStuckOnOldTask()
    {
        var release = inner.BlockNextCall();
        inner.Enqueue(BarcodeLookupResult.Offline());
        inner.Enqueue(Found(NewFood("1")));

        var pending = lookup.LookupAsync(Barcode);
        await inner.FirstCallArrived.WaitAsync(Bound);
        release.SetResult();
        Assert.That((await pending.WaitAsync(Bound)).Status, Is.EqualTo(BarcodeLookupStatus.Offline));

        var next = await lookup.LookupAsync(Barcode);

        Assert.That(next.Status, Is.EqualTo(BarcodeLookupStatus.Found));
    }

    [Test]
    public async Task Lookup_CallerCancelled_OtherCallersStillGetTheResult()
    {
        var release = inner.BlockNextCall();
        inner.Enqueue(Found(NewFood("1")));
        using var cts = new CancellationTokenSource();

        var cancelled = lookup.LookupAsync(Barcode, cts.Token);
        var other = lookup.LookupAsync(Barcode);
        await inner.FirstCallArrived.WaitAsync(Bound);
        await cts.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await cancelled.WaitAsync(Bound));
        release.SetResult();

        Assert.That((await other.WaitAsync(Bound)).Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(1));
        Assert.That(inner.Tokens.Select(t => t.CanBeCanceled), Is.All.False);
    }

    [Test]
    public async Task Lookup_StoreReadFails_FallsThroughToInnerAndReturnsItsResult()
    {
        await connection.DropTableAsync<Barcode>();
        inner.Enqueue(BarcodeLookupResult.Offline());

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Offline));
        Assert.That(inner.Calls, Is.EqualTo(1));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupReadFailed));
    }

    [Test]
    public async Task Lookup_StoreSaveFails_ReturnsFoundUnsavedAndNextCallRetries()
    {
        await FailInserts();
        inner.Enqueue(Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);

        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(first.Food!.Id, Is.Zero);
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupSaveFailed));

        await connection.ExecuteAsync("DROP TRIGGER FailInserts");
        inner.Enqueue(Found(NewFood("1")));
        var second = await lookup.LookupAsync(Barcode);

        Assert.That(second.Food!.Id, Is.GreaterThan(0));
        Assert.That(inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_RegisteredAsDecorator_ResolvesWrappingTheInner()
    {
        inner.Enqueue(Found(NewFood("1")));
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(store)
            .AddSingleton(importer)
            .AddDecoratedSingleton<IBarcodeLookup, StoredBarcodeLookup>(_ => inner)
            .BuildServiceProvider();

        var resolved = provider.GetRequiredService<IBarcodeLookup>();
        await resolved.LookupAsync(Barcode);
        await resolved.LookupAsync(Barcode);

        Assert.That(resolved, Is.InstanceOf<StoredBarcodeLookup>());
        Assert.That(inner.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Logging_AllPaths_LogsIdsOnlyNeverBarcodeOrName()
    {
        const string secretBarcode = "9876543210987";
        const string missBarcode = "9876543210994";
        const string offlineBarcode = "9876543211007";
        var food = NewFood("SRC-SECRET");
        food.Name = "Distinctive Name";
        food.Brand = "Distinctive Brand";
        inner.Enqueue(Found(food));
        inner.Enqueue(BarcodeLookupResult.NotFound());
        inner.Enqueue(BarcodeLookupResult.Offline());

        await lookup.LookupAsync(secretBarcode);
        var freshLookup = new StoredBarcodeLookup(inner, store, importer, logger);
        await freshLookup.LookupAsync(secretBarcode);
        await lookup.LookupAsync(missBarcode);
        await lookup.LookupAsync(missBarcode);
        await lookup.LookupAsync(offlineBarcode);

        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[]
        {
            CoreEvent.StoredLookupFetched,
            CoreEvent.StoredLookupStoreHit,
            CoreEvent.StoredLookupFetched,
            CoreEvent.StoredLookupNotFoundRemembered,
            CoreEvent.StoredLookupNotFoundHit,
            CoreEvent.StoredLookupFetched,
        }));
        AssertNoContentLogged(secretBarcode, missBarcode, offlineBarcode);
    }

    [Test]
    public async Task Logging_StoreFailures_LogEventsOnlyNeverBarcodeNameOrExceptionText()
    {
        const string readBarcode = "9876543212004";
        const string saveBarcode = "9876543212011";
        var readFood = NewFood("SRC-SECRET");
        readFood.Name = "Distinctive Name";
        var saveFood = NewFood("SRC-SECRET-2");
        saveFood.Name = "Distinctive Name";
        inner.Enqueue(Found(saveFood));
        inner.Enqueue(Found(readFood));

        await FailInserts();
        await lookup.LookupAsync(saveBarcode);
        await connection.ExecuteAsync("DROP TRIGGER FailInserts");
        await connection.DropTableAsync<Barcode>();
        await lookup.LookupAsync(readBarcode);

        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[]
        {
            CoreEvent.StoredLookupSaveFailed,
            CoreEvent.StoredLookupReadFailed,
            CoreEvent.StoredLookupSaveFailed,
        }));
        AssertNoContentLogged(readBarcode, saveBarcode, "SQLite", "FailInserts", "no such table");
    }

    private Task<int> FailInserts() => connection.ExecuteAsync(
        "CREATE TRIGGER FailInserts BEFORE INSERT ON Food BEGIN SELECT RAISE(ABORT, 'FailInserts'); END");

    private void AssertNoContentLogged(params string[] forbidden)
    {
        foreach (var record in logger.Collector.GetSnapshot())
        {
            var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
            Assert.That(record.Exception, Is.Null);
            Assert.That(text, Does.Not.Contain("Distinctive").And.Not.Contain("SRC-SECRET"));
            foreach (var value in forbidden)
            {
                Assert.That(text, Does.Not.Contain(value));
            }
        }
    }

    private static Food Detailed(string sourceId)
    {
        var food = NewFood(sourceId);
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
    public async Task Lookup_FirstScanOfAProductWithAPack_SavesTheBarcodeRowWithThePackAndNoServing()
    {
        inner.Enqueue(Found(Detailed("1")));

        var result = await lookup.LookupAsync(Barcode);

        var barcode = await connection.Table<Barcode>().FirstAsync();
        Assert.That((barcode.FoodId, barcode.PackageAmount, barcode.PackageLabel), Is.EqualTo((result.Food!.Id, (double?)400, "400 g e")));
        Assert.That(await connection.Table<Serving>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Lookup_RefreshOfAFoodWithAPersonsOwnServing_KeepsTheOwnRowAndTheFoodId()
    {
        var stored = await store.UpsertAsync(NoDetails("1"), Barcode);
        await connection.InsertAsync(new Serving { FoodId = stored.Id, Label = "My bowl", Amount = 75, Source = ServingSource.User });
        inner.Enqueue(Found(Detailed("1")));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        var rows = await connection.QueryAsync<Serving>("SELECT * FROM Serving WHERE FoodId = ?", stored.Id);
        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(r => (r.Label, r.Source)), Is.EqualTo(new[] { ("My bowl", ServingSource.User) }));
            Assert.That(connection.Table<Food>().CountAsync().Result, Is.EqualTo(1));
        });
        Assert.That((await store.FindByBarcodeAsync(Barcode))!.Id, Is.EqualTo(stored.Id));
    }

    [Test]
    public async Task Lookup_StoredFoodWithNoFetchTime_ReturnsAtOnceThenRefreshesItWithTheNewFieldsAndKeepsItsIdAndDiary()
    {
        var stored = await store.UpsertAsync(NoDetails("1"), Barcode);
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.ExecuteAsync("INSERT INTO DiaryEntry (Date, Meal, FoodId, Amount, Servings, IsRemoved) VALUES ('2031-07-19', 1, ?, 30, 1, 0)", stored.Id);
        var release = inner.BlockNextCall();
        inner.Enqueue(Found(Detailed("1")));

        var first = await lookup.LookupAsync(Barcode).WaitAsync(Bound);
        await inner.FirstCallArrived.WaitAsync(Bound);
        Assert.That(first.Food!.FetchedAt, Is.Null);
        release.SetResult();
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var second = await lookup.LookupAsync(Barcode);
        var third = await new StoredBarcodeLookup(inner, store, importer, logger).LookupAsync(Barcode);

        Assert.That(inner.Calls, Is.EqualTo(1), "a refreshed food is not asked again");
        foreach (var food in new[] { second.Food!, third.Food! })
        {
            Assert.That((food.Id, food.Brands, food.GenericName, food.PackageAmount, food.Category, food.Popularity), Is.EqualTo((stored.Id, "Ferrero, Nutella", "Hazelnut And Cocoa Spread", (double?)400, "Sweets", (int?)804)));
            Assert.That(food.FetchedAt, Is.EqualTo(new DateTimeOffset(2026, 11, 16, 9, 30, 0, TimeSpan.Zero)));
        }

        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM DiaryEntry WHERE FoodId = ?", stored.Id), Is.EqualTo(1));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
    }

    [TestCase(BarcodeLookupStatus.Offline)]
    [TestCase(BarcodeLookupStatus.RateLimited)]
    public async Task Lookup_StoredFoodWithNoFetchTimeWhenTheSourceIsUnavailable_StaysUnrefreshedAndAsksAgainNextScan(BarcodeLookupStatus status)
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        inner.Enqueue(status == BarcodeLookupStatus.Offline ? BarcodeLookupResult.Offline() : BarcodeLookupResult.RateLimited());
        inner.Enqueue(Found(Detailed("1")));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        Assert.That((await connection.Table<Food>().FirstAsync()).FetchedAt, Is.Null);
        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That((await connection.Table<Food>().FirstAsync()).Brands, Is.EqualTo("Ferrero, Nutella"));
    }

    [Test]
    public async Task Lookup_CoFidFoodWithNoFetchTime_IsNeverLookedUp()
    {
        var food = NoDetails("1");
        food.Source = FoodSource.CoFID;
        await store.UpsertAsync(food, Barcode);

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.Zero);
    }

    [Test]
    public async Task Lookup_RefreshOfAnotherProductForTheBarcode_LeavesTheFoodForALaterScan()
    {
        await store.UpsertAsync(NoDetails("1"), Barcode);
        inner.Enqueue(Found(Detailed("other")));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That((await connection.Table<Food>().FirstAsync()).FetchedAt, Is.Null);
    }

    [Test]
    public async Task Logging_DetailsRefresh_LogsIdsAndFlagsOnly()
    {
        var food = NoDetails("SRC-SECRET");
        food.Name = "Distinctive Name";
        await store.UpsertAsync(food, Barcode);
        var fresh = Detailed("SRC-SECRET");
        fresh.Brands = "Distinctive Brands";
        fresh.GenericName = "Distinctive Generic";
        fresh.Category = "Distinctive Category";
        inner.Enqueue(Found(fresh));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.StoredLookupStoreHit, CoreEvent.StoredLookupRefreshed }));
        AssertNoContentLogged(Barcode, "SRC-SECRET", "Distinctive");
    }

    private static BarcodeLookupResult Found(Food food) => BarcodeLookupResult.Found(food);

    private static Food NewFood(string sourceId)
    {
        var food = new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = sourceId,
            Name = "Oats",
            Unit = FoodUnit.Gram,
            Kcal = 100,
            ProteinG = 1,
            CarbsG = 2,
            FatG = 3,
            SearchText = "oats",
            FetchedAt = new DateTimeOffset(2026, 11, 15, 8, 0, 0, TimeSpan.Zero),
        };
        return food;
    }

    private static Food NoDetails(string sourceId)
    {
        var food = NewFood(sourceId);
        food.FetchedAt = null;
        return food;
    }

    private static Food Cola(string sourceId, double kcal = 42, double? pack = 330, string name = "Coca-Cola Original")
    {
        var food = NewFood(sourceId);
        food.Name = name;
        food.Brand = "Coca-Cola";
        food.Unit = FoodUnit.Millilitre;
        food.Type = FoodType.Drink;
        food.Kcal = kcal;
        food.ProteinG = 0;
        food.CarbsG = 10.6;
        food.FatG = 0;
        food.PackageAmount = pack;
        food.PackageLabel = pack is null ? null : $"{pack} ml";
        return food;
    }

    [Test]
    public async Task Lookup_ANewBarcodeOfTheSameProductInAnotherSize_ComesBackOnTheStoredFoodWithItsOwnPack()
    {
        const string litre = "5000000000024";
        inner.Enqueue(Found(Cola("5000000000017")));
        inner.Enqueue(Found(Cola(litre, pack: 1000, name: "Coca-Cola Original 1L")));

        var can = await lookup.LookupAsync(Barcode);
        var bottle = await lookup.LookupAsync(litre);

        Assert.Multiple(async () =>
        {
            Assert.That(bottle.Food!.Id, Is.EqualTo(can.Food!.Id));
            Assert.That(bottle.Food.Name, Is.EqualTo("Coca-Cola Original"));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
            Assert.That((await connection.Table<Barcode>().ToListAsync()).Select(b => b.Code), Is.EquivalentTo(new[] { Barcode, litre }));
        });
    }

    [Test]
    public async Task Lookup_BothBarcodesOfAMergedFood_AreServedFromTheStoreWithTheirOwnPack()
    {
        const string litre = "5000000000024";
        inner.Enqueue(Found(Cola("5000000000017")));
        inner.Enqueue(Found(Cola(litre, pack: 1000, name: "Coca-Cola Original 1L")));
        await lookup.LookupAsync(Barcode);
        await lookup.LookupAsync(litre);
        await lookup.RefreshesIdle();
        var calls = inner.Calls;
        var restarted = new StoredBarcodeLookup(inner, store, importer, logger);

        var can = await restarted.LookupAsync(Barcode);
        var bottle = await restarted.LookupAsync(litre);

        Assert.That(inner.Calls, Is.EqualTo(calls));
        Assert.That(bottle.Food!.Id, Is.EqualTo(can.Food!.Id));
    }

    [Test]
    public async Task Lookup_RefreshOfAMergedBarcode_NeverOverwritesTheFoodsNutrientsWithItsOwnValues()
    {
        const string litre = "5000000000024";
        var can = Cola("5000000000017");
        can.FetchedAt = null;
        inner.Enqueue(Found(can));
        inner.Enqueue(Found(Cola(litre, kcal: 42.5, pack: 1000, name: "Coca-Cola Original 1L")));
        await lookup.LookupAsync(Barcode);
        var attached = await lookup.LookupAsync(litre);
        var other = Cola(litre, kcal: 42.5, pack: 1000, name: "Coca-Cola Original 1L");
        inner.Enqueue(Found(other));

        // The food has no fetch time, so scanning either barcode asks the source again in the background.
        await lookup.LookupAsync(litre);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        var food = await connection.Table<Food>().FirstAsync();
        Assert.That((food.Id, food.Name, food.Kcal, food.FetchedAt), Is.EqualTo((attached.Food!.Id, "Coca-Cola Original", 42.0, (DateTimeOffset?)null)));
        Assert.That(inner.Calls, Is.EqualTo(3));
    }

    [Test]
    public async Task Lookup_RefreshOfTheOriginalBarcode_KeepsTheFoodIdDiaryEntriesAndOwnServings_AndUpdatesOnlyItsOwnBarcodePack()
    {
        const string litre = "5000000000024";
        var can = Cola("5000000000017");
        can.FetchedAt = null;
        inner.Enqueue(Found(can));
        inner.Enqueue(Found(Cola(litre, pack: 1000, name: "Coca-Cola Original 1L")));
        var first = await lookup.LookupAsync(Barcode);
        await lookup.LookupAsync(litre);
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = first.Food!.Id, Amount = 330 });
        await connection.InsertAsync(new Serving { FoodId = first.Food.Id, Label = "My glass", Amount = 200, Source = ServingSource.User });
        var refreshed = Cola("5000000000017", kcal: 43, pack: 350);
        inner.Enqueue(Found(refreshed));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        var food = await connection.Table<Food>().FirstAsync();
        var packs = (await connection.Table<Barcode>().ToListAsync()).ToDictionary(b => b.Code, b => b.PackageAmount);
        Assert.Multiple(async () =>
        {
            Assert.That((food.Id, food.Kcal), Is.EqualTo((first.Food.Id, 43.0)));
            Assert.That((packs[Barcode], packs[litre]), Is.EqualTo(((double?)350, (double?)1000)));
            Assert.That((await connection.Table<DiaryEntry>().ToListAsync()).Select(e => (e.FoodId, e.Amount)), Is.EqualTo(new[] { (first.Food.Id, 330.0) }));
            Assert.That((await connection.Table<Serving>().ToListAsync()).Select(s => s.Label), Does.Contain("My glass"));
        });
    }

    [Test]
    public async Task Lookup_AProductTheImportCannotUse_IsNotFoundAndNothingIsStored()
    {
        var broken = Cola("5000000000017");
        broken.Name = " ";
        inner.Enqueue(Found(broken));

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.NotFound));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
        Assert.That(await connection.Table<Barcode>().CountAsync(), Is.Zero);
    }

    [Test]
    public async Task Logging_AMergeAndItsRefresh_NeverCarriesABarcodeOrAName_InEitherLog()
    {
        const string litre = "9876543210994";
        var can = Cola("9876543210987");
        can.FetchedAt = null;
        can.Name = "Distinctive Name";
        can.Brand = "Distinctive Brand";
        var bottle = Cola(litre, pack: 1000);
        bottle.Name = "Distinctive Name 1L";
        bottle.Brand = "Distinctive Brand";
        inner.Enqueue(Found(can));
        inner.Enqueue(Found(bottle));
        inner.Enqueue(Found(can));

        await lookup.LookupAsync("9876543210987");
        await lookup.LookupAsync(litre);
        await lookup.LookupAsync("9876543210987");
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(LoggedEvents.Of(importLogger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.FoodImportCreated, CoreEvent.FoodImportAttached }));
        foreach (var log in new[] { (FakeLogger)logger, importLogger })
        {
            foreach (var record in log.Collector.GetSnapshot())
            {
                var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
                Assert.That(text, Does.Not.Contain("9876543210").And.Not.Contain("Distinctive"));
            }
        }
    }

    private sealed class FakeInner : IBarcodeLookup
    {
        private readonly Queue<Func<BarcodeLookupResult>> results = new();
        private readonly TaskCompletionSource firstCallArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? block;
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public List<string> Barcodes { get; } = [];

        public List<CancellationToken> Tokens { get; } = [];

        public Task FirstCallArrived => firstCallArrived.Task;

        public void Enqueue(BarcodeLookupResult result)
        {
            lock (results)
            {
                results.Enqueue(() => result);
            }
        }

        public void EnqueueException(Exception exception)
        {
            lock (results)
            {
                results.Enqueue(() => throw exception);
            }
        }

        public TaskCompletionSource BlockNextCall() => block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
        {
            Interlocked.Increment(ref calls);
            lock (Barcodes)
            {
                Barcodes.Add(barcode);
                Tokens.Add(ct);
            }

            // Take the block before signalling: a call started right after the signal must not see it too.
            var pending = Interlocked.Exchange(ref block, null);
            firstCallArrived.TrySetResult();
            if (pending is not null)
            {
                await pending.Task;
            }

            Func<BarcodeLookupResult> next;
            lock (results)
            {
                next = results.Dequeue();
            }

            return next();
        }
    }
}
