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
    private BarcodeLookupResult(BarcodeLookupStatus status, Food? food, double? packAmount = null, ProductServing? serving = null)
    {
        Status = status;
        Food = food;
        PackAmount = packAmount;
        Serving = serving;
    }

    public BarcodeLookupStatus Status { get; }

    public Food? Food { get; }

    /// <summary>The pack, in g or ml, of the barcode that was looked up, which differs from the food's own pack when the food has several barcodes. Null when unknown or the lookup is not through the stored barcodes.</summary>
    public double? PackAmount { get; }

    /// <summary>The product's own serving (Open Food Facts' wording and weight), for the store to write as the food's Product row; for a stored food, that row. Null when the product has none, or the source flags it as wrong.</summary>
    public ProductServing? Serving { get; }

    public static BarcodeLookupResult Found(Food food, double? packAmount = null, ProductServing? serving = null) => new(BarcodeLookupStatus.Found, food ?? throw new ArgumentNullException(nameof(food)), packAmount, serving);

    public static BarcodeLookupResult NotFound() => new(BarcodeLookupStatus.NotFound, null);

    public static BarcodeLookupResult Offline() => new(BarcodeLookupStatus.Offline, null);

    public static BarcodeLookupResult RateLimited() => new(BarcodeLookupStatus.RateLimited, null);
}

/// <summary>A product's own serving as a lookup returns it, such as "0.5 pizza (201.8 g)" and 201.8; the weight is in g, or ml to match the food's unit. Above zero; the label is not blank. The store writes it as the food's Product serving.</summary>
public sealed record ProductServing(string Label, double Amount);
