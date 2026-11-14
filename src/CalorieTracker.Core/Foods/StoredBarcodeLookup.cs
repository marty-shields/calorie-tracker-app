using System.Collections.Concurrent;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Serves barcodes from the food table, and otherwise from the inner lookup, saving what it finds.
/// Concurrent calls for one barcode share a single store read, inner call and save, which ignore the callers' cancellation.
/// A database failure propagates to the callers.
/// </summary>
internal sealed partial class StoredBarcodeLookup(IBarcodeLookup inner, FoodStore store, ILogger<StoredBarcodeLookup> logger) : IBarcodeLookup
{
    private readonly ConcurrentDictionary<string, byte> notFound = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<BarcodeLookupResult>>> inFlight = new();

    public Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
    {
        if (notFound.ContainsKey(barcode))
        {
            Log.NotFoundHit(logger);
            return Task.FromResult(BarcodeLookupResult.NotFound());
        }

        var shared = inFlight.GetOrAdd(
            barcode,
            key => new Lazy<Task<BarcodeLookupResult>>(() => Task.Run(() => FetchAsync(key)), LazyThreadSafetyMode.ExecutionAndPublication));
        return shared.Value.WaitAsync(ct);
    }

    private async Task<BarcodeLookupResult> FetchAsync(string barcode)
    {
        try
        {
            var stored = await store.FindByBarcodeAsync(barcode);
            if (stored is not null)
            {
                Log.StoreHit(logger, stored.Id);
                return BarcodeLookupResult.Found(stored);
            }

            var result = await inner.LookupAsync(barcode, CancellationToken.None);
            switch (result.Status)
            {
                case BarcodeLookupStatus.Found:
                    var food = result.Food!;
                    food.Barcode = barcode;
                    var saved = await store.UpsertAsync(food);
                    Log.Fetched(logger, result.Status, saved.Id);
                    return BarcodeLookupResult.Found(saved);
                case BarcodeLookupStatus.NotFound:
                    notFound.TryAdd(barcode, 0);
                    Log.Fetched(logger, result.Status, null);
                    Log.NotFoundRemembered(logger, notFound.Count);
                    return result;
                default:
                    Log.Fetched(logger, result.Status, null);
                    return result;
            }
        }
        finally
        {
            inFlight.TryRemove(barcode, out _);
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.StoredLookupStoreHit, LogLevel.Debug, "Barcode served from food {FoodId}")]
        public static partial void StoreHit(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.StoredLookupFetched, LogLevel.Debug, "Inner lookup returned {Status}, food {FoodId}")]
        public static partial void Fetched(ILogger logger, BarcodeLookupStatus status, int? foodId);

        [LoggerMessage((int)CoreEvent.StoredLookupNotFoundRemembered, LogLevel.Debug, "Not-found barcode remembered, {Count} remembered")]
        public static partial void NotFoundRemembered(ILogger logger, int count);

        [LoggerMessage((int)CoreEvent.StoredLookupNotFoundHit, LogLevel.Debug, "Barcode answered from remembered not-found")]
        public static partial void NotFoundHit(ILogger logger);
    }
}
