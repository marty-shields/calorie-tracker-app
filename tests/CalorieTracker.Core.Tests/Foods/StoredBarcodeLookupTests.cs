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
    private StoredBarcodeLookup lookup = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<FoodBarcode>();
        store = new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>());
        logger = new FakeLogger<StoredBarcodeLookup>();
        inner = new FakeInner();
        lookup = new StoredBarcodeLookup(inner, store, logger);
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
    public async Task Lookup_StoredFoodWithServing_NeverAsksInnerAgain()
    {
        await store.UpsertAsync(NewFood("1"), Barcode);

        await lookup.LookupAsync(Barcode);
        await lookup.LookupAsync(Barcode);

        Assert.That(inner.Calls, Is.Zero);
    }

    [Test]
    public async Task Lookup_StoredFoodWithoutServing_ReturnsAtOnceWhileTheRefreshIsStillWaiting()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        var release = inner.BlockNextCall();
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode).WaitAsync(Bound);
        await inner.FirstCallArrived.WaitAsync(Bound);

        Assert.That(first.Food!.ServingAmount, Is.Null);
        release.SetResult();
        await lookup.RefreshesIdle().WaitAsync(Bound);
        Assert.That((await lookup.LookupAsync(Barcode)).Food!.ServingAmount, Is.EqualTo(50));
    }

    [Test]
    public async Task Lookup_StoredFoodWithoutServing_AsksInnerOnceAndFillsTheServingIn()
    {
        var stored = await store.UpsertAsync(NoServing("1"), Barcode);
        var refreshed = NewFood("1");
        refreshed.Kcal = 999;
        inner.Enqueue(BarcodeLookupResult.Found(refreshed));

        var first = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var second = await lookup.LookupAsync(Barcode);
        var again = new StoredBarcodeLookup(inner, store, logger);
        var third = await again.LookupAsync(Barcode);

        Assert.That(inner.Calls, Is.EqualTo(1));
        Assert.That(first.Food!.ServingAmount, Is.Null);
        Assert.That((second.Food!.ServingLabel, second.Food.ServingAmount), Is.EqualTo(("1 portion (50 g)", 50.0)));
        Assert.That((third.Food!.ServingLabel, third.Food.ServingAmount), Is.EqualTo(("1 portion (50 g)", 50.0)));
        var row = await connection.Table<Food>().FirstAsync();
        Assert.That((row.Id, row.Kcal), Is.EqualTo((stored.Id, 999.0)));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupServingFilled));
    }

    [Test]
    public async Task Lookup_RefreshWithServing_RefreshesEveryNutrientAndChangesNothingElseOnTheRow()
    {
        var original = NoServing("1");
        original.Name = "Stored name";
        original.Brand = "Stored brand";
        original.Type = FoodType.Drink;
        original.SearchText = "stored";
        original.FibreG = 1;
        original.SaltG = 1;
        var stored = await store.UpsertAsync(original, Barcode);
        var other = await store.UpsertAsync(NoServing("2"), "5000000000024");
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
        inner.Enqueue(BarcodeLookupResult.Found(fresh));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        var rows = await connection.Table<Food>().ToListAsync();
        var row = rows.Single(r => r.Id == stored.Id);
        Assert.Multiple(() =>
        {
            Assert.That((row.Kcal, row.ProteinG, row.CarbsG, row.FatG), Is.EqualTo((201.0, 21.0, 22.0, 23.0)));
            Assert.That((row.FibreG, row.SugarsG, row.SaturatesG, row.SaltG), Is.EqualTo(((double?)null, (double?)2.5, (double?)3.5, (double?)0.25)));
            Assert.That((row.ServingLabel, row.ServingAmount), Is.EqualTo(("1 portion (50 g)", 50.0)));
            Assert.That((row.Name, row.Brand, row.Type, row.SearchText, row.Unit), Is.EqualTo(("Stored name", "Stored brand", FoodType.Drink, "stored", FoodUnit.Gram)));
            Assert.That((row.Source, row.SourceId), Is.EqualTo((FoodSource.OpenFoodFacts, "1")));
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows.Single(r => r.Id == other.Id).Kcal, Is.EqualTo(100.0));
        });
        Assert.That((await store.FindByBarcodeAsync(Barcode))!.Id, Is.EqualTo(stored.Id));
    }

    [Test]
    public async Task Lookup_ScanDuringARunningRefresh_DoesNotStartAnother()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        var release = inner.BlockNextCall();
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

        await lookup.LookupAsync(Barcode);
        await inner.FirstCallArrived.WaitAsync(Bound);
        await lookup.LookupAsync(Barcode);
        release.SetResult();
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_StoredFoodWhoseSourceHasNoServing_IsMarkedCheckedAndNotAskedAgainEvenAfterARestart()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        inner.Enqueue(BarcodeLookupResult.Found(NoServing("1")));

        var first = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var restarted = new StoredBarcodeLookup(inner, store, logger);
        await restarted.LookupAsync(Barcode);
        await restarted.RefreshesIdle().WaitAsync(Bound);

        Assert.That(first.Food!.ServingAmount, Is.Null);
        Assert.That(inner.Calls, Is.EqualTo(1));
        var row = await connection.Table<Food>().FirstAsync();
        Assert.That((row.ServingChecked, row.ServingAmount, row.Kcal), Is.EqualTo(((bool?)true, (double?)null, 100.0)));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupServingMissing));
    }

    [Test]
    public async Task Lookup_StoredFoodTheSourceNoLongerHas_IsMarkedCheckedAndNotAskedAgainEvenAfterARestart()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        inner.Enqueue(BarcodeLookupResult.NotFound());

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var restarted = new StoredBarcodeLookup(inner, store, logger);
        var second = await restarted.LookupAsync(Barcode);
        await restarted.RefreshesIdle().WaitAsync(Bound);

        Assert.That(second.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(1));
        Assert.That((await connection.Table<Food>().FirstAsync()).ServingChecked, Is.True);
    }

    [Test]
    public async Task Lookup_StoredFoodMarkedChecked_NeverAsksInner()
    {
        var food = NoServing("1");
        food.ServingChecked = true;
        await store.UpsertAsync(food, Barcode);

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.Zero);
    }

    [Test]
    public async Task Lookup_RefreshedFoodOfAnotherUnitWithNoServing_IsMarkedCheckedWithoutChangingNutrients()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        var millilitres = NoServing("1");
        millilitres.Unit = FoodUnit.Millilitre;
        millilitres.Kcal = 999;
        inner.Enqueue(BarcodeLookupResult.Found(millilitres));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        var row = await connection.Table<Food>().FirstAsync();
        Assert.That((row.ServingChecked, row.Kcal, row.Unit), Is.EqualTo(((bool?)true, 100.0, FoodUnit.Gram)));
    }

    [Test]
    public async Task Lookup_MarkingCheckedFails_StillReturnsTheFoodAndAsksAgainNextScan()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        inner.Enqueue(BarcodeLookupResult.NotFound());
        await connection.ExecuteAsync("CREATE TRIGGER block_update BEFORE UPDATE ON Food BEGIN SELECT RAISE(ABORT, 'blocked'); END");

        var first = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        await connection.ExecuteAsync("DROP TRIGGER block_update");
        inner.Enqueue(BarcodeLookupResult.NotFound());
        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That((await connection.Table<Food>().FirstAsync()).ServingChecked, Is.True);
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupServingFailed));
    }

    [TestCase(BarcodeLookupStatus.Offline)]
    [TestCase(BarcodeLookupStatus.RateLimited)]
    public async Task Lookup_StoredFoodWithoutServingWhenTheSourceIsUnavailable_ReturnsItAndAsksAgainNextScan(BarcodeLookupStatus status)
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        inner.Enqueue(status == BarcodeLookupStatus.Offline ? BarcodeLookupResult.Offline() : BarcodeLookupResult.RateLimited());
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var second = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var third = await lookup.LookupAsync(Barcode);

        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(first.Food!.ServingAmount, Is.Null);
        Assert.That(second.Food!.ServingAmount, Is.Null);
        Assert.That(third.Food!.ServingAmount, Is.EqualTo(50));
        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That((await connection.Table<Food>().FirstAsync()).ServingChecked, Is.Null);
    }

    [Test]
    public async Task Lookup_StoredFoodWithoutServingAndTheRefreshThrows_StillReturnsTheStoredFood()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        inner.EnqueueException(new InvalidOperationException("refresh failed"));

        var result = await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupServingFailed));
    }

    [Test]
    public async Task Lookup_RefreshFailsToSave_AsksAgainNextScan()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        await connection.ExecuteAsync("CREATE TRIGGER block_update BEFORE UPDATE ON Food BEGIN SELECT RAISE(ABORT, 'blocked'); END");

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        await connection.ExecuteAsync("DROP TRIGGER block_update");
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);

        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That((await lookup.LookupAsync(Barcode)).Food!.ServingAmount, Is.EqualTo(50));
    }

    [Test]
    public async Task Lookup_RefreshedServingInAnotherUnit_IsIgnored()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        var millilitres = NewFood("1");
        millilitres.Unit = FoodUnit.Millilitre;
        inner.Enqueue(BarcodeLookupResult.Found(millilitres));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Food!.ServingAmount, Is.Null);
    }

    [Test]
    public async Task Lookup_RefreshedServingOfAnotherProduct_IsIgnored()
    {
        await store.UpsertAsync(NoServing("1"), Barcode);
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("2")));

        await lookup.LookupAsync(Barcode);
        await lookup.RefreshesIdle().WaitAsync(Bound);
        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Food!.ServingAmount, Is.Null);
    }

    [Test]
    public async Task Logging_ServingRefresh_LogsIdsAndStatusOnly()
    {
        const string filledBarcode = "9876543212101";
        const string missingBarcode = "9876543212118";
        const string failedBarcode = "9876543212125";
        foreach (var (id, barcode) in new[] { ("SRC-SECRET-A", filledBarcode), ("SRC-SECRET-B", missingBarcode), ("SRC-SECRET-C", failedBarcode) })
        {
            var food = NoServing(id);
            food.Name = "Distinctive Name";
            food.Brand = "Distinctive Brand";
            await store.UpsertAsync(food, barcode);
        }

        var withServing = NewFood("SRC-SECRET-A");
        withServing.ServingLabel = "Distinctive Label (50 g)";
        inner.Enqueue(BarcodeLookupResult.Found(withServing));
        inner.Enqueue(BarcodeLookupResult.Found(NoServing("SRC-SECRET-B")));
        inner.EnqueueException(new InvalidOperationException("Distinctive failure"));

        foreach (var barcode in new[] { filledBarcode, missingBarcode, failedBarcode })
        {
            await lookup.LookupAsync(barcode);
            await lookup.RefreshesIdle().WaitAsync(Bound);
        }

        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[]
        {
            CoreEvent.StoredLookupStoreHit,
            CoreEvent.StoredLookupServingFilled,
            CoreEvent.StoredLookupStoreHit,
            CoreEvent.StoredLookupServingMissing,
            CoreEvent.StoredLookupStoreHit,
            CoreEvent.StoredLookupServingFailed,
        }));
        AssertNoContentLogged(filledBarcode, missingBarcode, failedBarcode, "Distinctive Label", "Distinctive failure");
    }

    [Test]
    public async Task Lookup_InnerFound_SavesAndReturnsStoredFoodWithId()
    {
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

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
            logger);

        var first = await real.LookupAsync("5000000000017");
        var second = await real.LookupAsync("5000000000024");

        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        Assert.That(await connection.Table<FoodBarcode>().CountAsync(), Is.EqualTo(2));
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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

        var result = await lookup.LookupAsync(Barcode);

        var link = await connection.Table<FoodBarcode>().FirstAsync();
        Assert.That((link.Barcode, link.FoodId), Is.EqualTo((Barcode, result.Food!.Id)));
    }

    [Test]
    public async Task Lookup_TwoBarcodesOneProduct_BothServedFromTheStoreAfterOneExtraInnerCall()
    {
        const string otherBarcode = "5000000000024";
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);
        var second = await lookup.LookupAsync(otherBarcode);
        Assert.That(inner.Calls, Is.EqualTo(2));

        var firstAgain = await lookup.LookupAsync(Barcode);
        var secondAgain = await lookup.LookupAsync(otherBarcode);

        Assert.That(inner.Calls, Is.EqualTo(2));
        Assert.That(new[] { second.Food!.Id, firstAgain.Food!.Id, secondAgain.Food!.Id }, Is.All.EqualTo(first.Food!.Id));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
        Assert.That(await connection.Table<FoodBarcode>().CountAsync(), Is.EqualTo(2));
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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("2")));

        await lookup.LookupAsync(Barcode);
        var other = await lookup.LookupAsync("5000000000024");

        Assert.That(other.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_Offline_ReturnsOfflineUnsavedAndNotRemembered()
    {
        inner.Enqueue(BarcodeLookupResult.Offline());
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("2")));

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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
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
        await connection.DropTableAsync<Food>();
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
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

        var first = await lookup.LookupAsync(Barcode);

        Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(first.Food!.Id, Is.Zero);
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Does.Contain(CoreEvent.StoredLookupSaveFailed));

        await connection.ExecuteAsync("DROP TRIGGER FailInserts");
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        var second = await lookup.LookupAsync(Barcode);

        Assert.That(second.Food!.Id, Is.GreaterThan(0));
        Assert.That(inner.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task Lookup_RegisteredAsDecorator_ResolvesWrappingTheInner()
    {
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(store)
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
        inner.Enqueue(BarcodeLookupResult.Found(food));
        inner.Enqueue(BarcodeLookupResult.NotFound());
        inner.Enqueue(BarcodeLookupResult.Offline());

        await lookup.LookupAsync(secretBarcode);
        var freshLookup = new StoredBarcodeLookup(inner, store, logger);
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
        inner.Enqueue(BarcodeLookupResult.Found(saveFood));
        inner.Enqueue(BarcodeLookupResult.Found(readFood));

        await FailInserts();
        await lookup.LookupAsync(saveBarcode);
        await connection.ExecuteAsync("DROP TRIGGER FailInserts");
        await connection.DropTableAsync<Food>();
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

    private static Food NewFood(string sourceId) => new()
    {
        Source = FoodSource.OpenFoodFacts,
        SourceId = sourceId,
        Name = "Oats",
        Unit = FoodUnit.Gram,
        Kcal = 100,
        ProteinG = 1,
        CarbsG = 2,
        FatG = 3,
        ServingLabel = "1 portion (50 g)",
        ServingAmount = 50,
        SearchText = "oats",
    };

    private static Food NoServing(string sourceId)
    {
        var food = NewFood(sourceId);
        food.ServingLabel = null;
        food.ServingAmount = null;
        return food;
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
