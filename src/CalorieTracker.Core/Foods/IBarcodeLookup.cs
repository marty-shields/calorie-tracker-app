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
    private BarcodeLookupResult(BarcodeLookupStatus status, Food? food)
    {
        Status = status;
        Food = food;
    }

    public BarcodeLookupStatus Status { get; }

    public Food? Food { get; }

    public static BarcodeLookupResult Found(Food food) => new(BarcodeLookupStatus.Found, food ?? throw new ArgumentNullException(nameof(food)));

    public static BarcodeLookupResult NotFound() => new(BarcodeLookupStatus.NotFound, null);

    public static BarcodeLookupResult Offline() => new(BarcodeLookupStatus.Offline, null);

    public static BarcodeLookupResult RateLimited() => new(BarcodeLookupStatus.RateLimited, null);
}
