using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

public interface IBarcodeLookup
{
    /// <param name="barcode">A normalised barcode, see <see cref="BarcodeNormaliser"/>.</param>
    Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default);
}

public enum BarcodeLookupStatus
{
    Found,
    NotFound,
    Offline,
    RateLimited,
}

/// <summary>The outcome of a barcode lookup. <see cref="Food"/> is set only when <see cref="Status"/> is <see cref="BarcodeLookupStatus.Found"/>.</summary>
public sealed record BarcodeLookupResult
{
    private BarcodeLookupResult(BarcodeLookupStatus status, Food? food, double? packAmount = null)
    {
        Status = status;
        Food = food;
        PackAmount = packAmount;
    }

    public BarcodeLookupStatus Status { get; }

    public Food? Food { get; }

    /// <summary>The pack, in g or ml, of the barcode that was looked up, which differs from the food's own pack when the food has several barcodes. Null when unknown or the lookup is not through the stored barcodes.</summary>
    public double? PackAmount { get; }

    public static BarcodeLookupResult Found(Food food, double? packAmount = null) => new(BarcodeLookupStatus.Found, food ?? throw new ArgumentNullException(nameof(food)), packAmount);

    public static BarcodeLookupResult NotFound() => new(BarcodeLookupStatus.NotFound, null);

    public static BarcodeLookupResult Offline() => new(BarcodeLookupStatus.Offline, null);

    public static BarcodeLookupResult RateLimited() => new(BarcodeLookupStatus.RateLimited, null);
}
