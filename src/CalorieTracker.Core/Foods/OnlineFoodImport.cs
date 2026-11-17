using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

public enum OnlineImportStatus
{
    /// <summary>The product is stored; <see cref="OnlineImportResult.Food"/> is its food.</summary>
    Imported,

    /// <summary>Open Food Facts has no usable product for the barcode, or it lacks a name, a unit or nutrition.</summary>
    NotFound,

    /// <summary>Open Food Facts is busy (HTTP 429 after the lookup's retries).</summary>
    Limited,

    /// <summary>No response: no network, a timeout or a dropped connection.</summary>
    Offline,

    /// <summary>The database failed, so nothing is known to be stored.</summary>
    Failed,
}

/// <summary>The outcome of an import. <see cref="Food"/> is set only when <see cref="Status"/> is <see cref="OnlineImportStatus.Imported"/>.</summary>
public sealed record OnlineImportResult(OnlineImportStatus Status, Food? Food = null);

/// <summary>Stores a product the person tapped in the search results.</summary>
public interface IOnlineFoodImport
{
    /// <param name="barcode">The barcode of an <see cref="OnlineProduct"/>.</param>
    Task<OnlineImportResult> ImportAsync(string barcode, CancellationToken ct = default);
}

/// <summary>
/// Stores a product the person tapped in the search results, the way a scan does: the barcode lookup (which stores what it finds through <see cref="FoodImportService"/>), then the import service itself for a product the lookup returned unsaved,
/// so the food has the same full record as a scanned one and a product that is the same food in another size lands on the stored food. Nothing else writes foods from a search. Logs carry food ids and statuses only, never a barcode.
/// </summary>
public sealed partial class OnlineFoodImport : IOnlineFoodImport
{
    private readonly IBarcodeLookup lookup;
    private readonly FoodImportService importer;
    private readonly ILogger<OnlineFoodImport> logger;

    internal OnlineFoodImport(IBarcodeLookup lookup, FoodImportService importer, ILogger<OnlineFoodImport> logger)
    {
        this.lookup = lookup;
        this.importer = importer;
        this.logger = logger;
    }

    public async Task<OnlineImportResult> ImportAsync(string barcode, CancellationToken ct = default)
    {
        if (!BarcodeNormaliser.TryNormalise(barcode, out var normalised))
        {
            Log.LookupFailed(logger, BarcodeLookupStatus.NotFound);
            return new OnlineImportResult(OnlineImportStatus.NotFound);
        }

        var result = await lookup.LookupAsync(normalised, ct);
        if (result is not { Status: BarcodeLookupStatus.Found, Food: { } food })
        {
            Log.LookupFailed(logger, result.Status);
            return new OnlineImportResult(result.Status switch
            {
                BarcodeLookupStatus.Offline => OnlineImportStatus.Offline,
                BarcodeLookupStatus.RateLimited => OnlineImportStatus.Limited,
                _ => OnlineImportStatus.NotFound,
            });
        }

        // The stored lookup has imported it already and returns a food with an id.
        if (food.Id > 0)
        {
            Log.Imported(logger, food.Id, FoodImportStatus.Known);
            return new OnlineImportResult(OnlineImportStatus.Imported, food);
        }

        var imported = await importer.ImportAsync(normalised, food, result.Serving);
        if (imported is { Status: FoodImportStatus.Created or FoodImportStatus.Attached or FoodImportStatus.Known, Food: { } stored })
        {
            Log.Imported(logger, stored.Id, imported.Status);
            return new OnlineImportResult(OnlineImportStatus.Imported, stored);
        }

        Log.StoreFailed(logger, imported.Status);
        return new OnlineImportResult(imported.Status == FoodImportStatus.NotImportable ? OnlineImportStatus.NotFound : OnlineImportStatus.Failed);
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.OnlineImported, LogLevel.Information, "Search result stored as food {FoodId}: {Status}")]
        public static partial void Imported(ILogger logger, int foodId, FoodImportStatus status);

        [LoggerMessage((int)CoreEvent.OnlineImportLookupFailed, LogLevel.Information, "Search result lookup gave no product: {Status}")]
        public static partial void LookupFailed(ILogger logger, BarcodeLookupStatus status);

        [LoggerMessage((int)CoreEvent.OnlineImportStoreFailed, LogLevel.Warning, "Search result could not be stored: {Status}")]
        public static partial void StoreFailed(ILogger logger, FoodImportStatus status);
    }
}
