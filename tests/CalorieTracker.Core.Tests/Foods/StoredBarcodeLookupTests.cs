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
        var saved = await store.UpsertAsync(NewFood("1", Barcode));

        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(result.Food!.Id, Is.EqualTo(saved.Id));
        Assert.That(inner.Calls, Is.Zero);
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
    public async Task Lookup_InnerFound_SetsTheBarcodeOnTheStoredFood()
    {
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));

        await lookup.LookupAsync(Barcode);

        Assert.That((await connection.Table<Food>().FirstAsync()).Barcode, Is.EqualTo(Barcode));
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
        await inner.FirstCallArrived;
        release.SetResult();
        var results = await Task.WhenAll(calls);

        Assert.That(results.Select(r => r.Food!.Id).Distinct().Count(), Is.EqualTo(1));
        Assert.That(inner.Calls, Is.EqualTo(1));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_ConcurrentCallsDifferentBarcodes_EachCallInner()
    {
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("2")));

        var results = await Task.WhenAll(lookup.LookupAsync(Barcode), lookup.LookupAsync("5000000000024"));

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
        await inner.FirstCallArrived;
        release.SetResult();
        Assert.That((await pending).Status, Is.EqualTo(BarcodeLookupStatus.Offline));

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
        await inner.FirstCallArrived;
        await cts.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await cancelled);
        release.SetResult();

        Assert.That((await other).Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(inner.Calls, Is.EqualTo(1));
    }

    [Test]
    public async Task Lookup_StoreThrows_InFlightEntryIsCleared()
    {
        await connection.DropTableAsync<Food>();
        await Assert.ThrowsAsync<SQLiteException>(async () => await lookup.LookupAsync(Barcode));

        await connection.CreateTableAsync<Food>();
        inner.Enqueue(BarcodeLookupResult.Found(NewFood("1")));
        var result = await lookup.LookupAsync(Barcode);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
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
        var food = NewFood("SRC-SECRET", null);
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
        foreach (var record in logger.Collector.GetSnapshot())
        {
            var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
            Assert.That(text, Does.Not.Contain("Distinctive").And.Not.Contain("SRC-SECRET")
                .And.Not.Contain(secretBarcode).And.Not.Contain(missBarcode).And.Not.Contain(offlineBarcode));
        }
    }

    private static Food NewFood(string sourceId, string? barcode = null) => new()
    {
        Source = FoodSource.OpenFoodFacts,
        SourceId = sourceId,
        Name = "Oats",
        Barcode = barcode,
        Unit = FoodUnit.Gram,
        Kcal = 100,
        ProteinG = 1,
        CarbsG = 2,
        FatG = 3,
        SearchText = "oats",
    };

    private sealed class FakeInner : IBarcodeLookup
    {
        private readonly Queue<BarcodeLookupResult> results = new();
        private readonly TaskCompletionSource firstCallArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? block;
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public List<string> Barcodes { get; } = [];

        public Task FirstCallArrived => firstCallArrived.Task;

        public void Enqueue(BarcodeLookupResult result)
        {
            lock (results)
            {
                results.Enqueue(result);
            }
        }

        public TaskCompletionSource BlockNextCall() => block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
        {
            Interlocked.Increment(ref calls);
            lock (Barcodes)
            {
                Barcodes.Add(barcode);
            }

            firstCallArrived.TrySetResult();
            if (block is { } pending)
            {
                block = null;
                await pending.Task;
            }

            lock (results)
            {
                return results.Dequeue();
            }
        }
    }
}
