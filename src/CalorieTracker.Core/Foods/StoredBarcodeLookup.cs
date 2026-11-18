using System.Collections.Concurrent;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Serves barcodes from the barcode table (a food can have several barcodes), and otherwise from the inner lookup, importing what it finds with <see cref="FoodImportService"/>: a product that is the same food in another size gets the barcode on its stored food. The result carries the pack of the barcode asked for.
/// Concurrent calls for one barcode share a single store read, inner call and save, which ignore the callers' cancellation.
/// An Open Food Facts food with no fetch time (stored before its details were) is returned at once and looked up again in the background, one at a time for each barcode (a lookup that was offline or rate limited is tried again on a later scan); the nutrients and the source details are stored when the lookup is for the same product and unit, and show on the next scan. The food's id, servings and diary entries are untouched. A food with a fetch time is never looked up again.
/// A failing store never reaches the callers: a failed read falls through to the inner lookup, a failed save returns the unsaved food.
/// </summary>
internal sealed partial class StoredBarcodeLookup(IBarcodeLookup inner, FoodStore store, FoodImportService importer, ILogger<StoredBarcodeLookup> logger) : IBarcodeLookup
{
    private readonly ConcurrentDictionary<string, byte> notFound = new();
    private readonly ConcurrentDictionary<string, Task> refreshing = new();
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
                (stored, _) = await store.FindWithBarcodeAsync(barcode);
            }
            catch (Exception)
            {
                Log.ReadFailed(logger);
            }

            if (stored is not null)
            {
                Log.StoreHit(logger, stored.Id);
                if (WantsDetails(stored))
                {
                    StartRefresh(barcode, stored);
                }

                return BarcodeLookupResult.Found(stored);
            }

            var result = await inner.LookupAsync(barcode, CancellationToken.None);
            switch (result.Status)
            {
                case BarcodeLookupStatus.Found:
                    var food = result.Food!;
                    var imported = await importer.ImportAsync(barcode, food);
                    switch (imported.Status)
                    {
                        case FoodImportStatus.Created or FoodImportStatus.Attached or FoodImportStatus.Known:
                            Log.Fetched(logger, result.Status, imported.Food!.Id);
                            return BarcodeLookupResult.Found(imported.Food);
                        case FoodImportStatus.NotImportable:
                            // Same as a product the lookup could not use: not found, and not asked for again.
                            notFound.TryAdd(barcode, 0);
                            Log.Fetched(logger, BarcodeLookupStatus.NotFound, null);
                            return BarcodeLookupResult.NotFound();
                        default:
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

    /// <summary>Completes when the refreshes running now have finished. Never faults.</summary>
    internal Task RefreshesIdle() => Task.WhenAll(refreshing.Values);

    private void StartRefresh(string barcode, Food stored)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!refreshing.TryAdd(barcode, started.Task))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshStoredAsync(barcode, stored);
            }
            finally
            {
                refreshing.TryRemove(barcode, out _);
                started.SetResult();
            }
        });
    }

    private static bool WantsDetails(Food stored) => stored.Source == FoodSource.OpenFoodFacts && stored.FetchedAt is null;

    /// <summary>
    /// Asks the inner lookup again for <paramref name="stored"/>. A Found answer with the same source id and unit refreshes the row's nutrients and source details and logs it; any other answer (not found, offline, rate limited, another product or unit) leaves the food alone for a later scan. Never throws.
    /// </summary>
    private async Task RefreshStoredAsync(string barcode, Food stored)
    {
        try
        {
            var result = await inner.LookupAsync(barcode, CancellationToken.None);
            var fresh = result.Food;
            if (result.Status == BarcodeLookupStatus.Found && fresh!.Unit == stored.Unit && fresh.SourceId == stored.SourceId
                && await store.RefreshAsync(stored.Id, fresh, barcode))
            {
                Log.Refreshed(logger, stored.Id);
            }
        }
        catch (Exception)
        {
            Log.RefreshFailed(logger, stored.Id);
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.StoredLookupRefreshed, LogLevel.Debug, "Food {FoodId} refreshed")]
        public static partial void Refreshed(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.StoredLookupRefreshFailed, LogLevel.Warning, "Refresh of food {FoodId} failed")]
        public static partial void RefreshFailed(ILogger logger, int foodId);

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
