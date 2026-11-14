using System.Collections.Concurrent;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Serves barcodes from the food table, and otherwise from the inner lookup, saving what it finds.
/// Concurrent calls for one barcode share a single store read, inner call and save, which ignore the callers' cancellation.
/// A failing store never reaches the callers: a failed read falls through to the inner lookup, a failed save returns the unsaved food.
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

        Lazy<Task<BarcodeLookupResult>>? created = null;
        // Task.Run: the fetch's finally must not run before GetOrAdd has returned, or it could remove nothing and leave the entry behind.
        created = new Lazy<Task<BarcodeLookupResult>>(
            () => Task.Run(() => FetchAsync(barcode, created!)),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var shared = inFlight.GetOrAdd(barcode, created);
        return shared.Value.WaitAsync(ct);
    }

    private async Task<BarcodeLookupResult> FetchAsync(string barcode, Lazy<Task<BarcodeLookupResult>> self)
    {
        try
        {
            // Another fetch may have remembered the barcode between the caller's check and this entry being added.
            if (notFound.ContainsKey(barcode))
            {
                Log.NotFoundHit(logger);
                return BarcodeLookupResult.NotFound();
            }

            Food? stored = null;
            try
            {
                stored = await store.FindByBarcodeAsync(barcode);
            }
            catch (Exception)
            {
                Log.ReadFailed(logger);
            }

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
                    try
                    {
                        var saved = await store.UpsertAsync(food, barcode);
                        Log.Fetched(logger, result.Status, saved.Id);
                        return BarcodeLookupResult.Found(saved);
                    }
                    catch (Exception)
                    {
                        Log.SaveFailed(logger);
                        return BarcodeLookupResult.Found(food);
                    }
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
            inFlight.TryRemove(KeyValuePair.Create(barcode, self));
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

        [LoggerMessage((int)CoreEvent.StoredLookupReadFailed, LogLevel.Warning, "Food table read failed, falling back to the inner lookup")]
        public static partial void ReadFailed(ILogger logger);

        [LoggerMessage((int)CoreEvent.StoredLookupSaveFailed, LogLevel.Warning, "Saving the looked-up food failed, returning it unsaved")]
        public static partial void SaveFailed(ILogger logger);
    }
}
