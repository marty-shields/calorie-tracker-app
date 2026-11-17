using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

public enum OnlineSearchStatus
{
    /// <summary>At least one product can be shown.</summary>
    Found,

    /// <summary>The search ran and nothing usable came back.</summary>
    None,

    /// <summary>Open Food Facts answered HTTP 429.</summary>
    Limited,

    /// <summary>No response: no network, a timeout or a dropped connection.</summary>
    Offline,

    /// <summary>Any other answer, such as a server error or a body that is not the expected JSON.</summary>
    Failed,
}

/// <summary>A product Open Food Facts offered for a search, cleaned and not stored.</summary>
/// <param name="Barcode">Normalised EAN-8 or EAN-13 digits, see <see cref="BarcodeNormaliser"/>.</param>
/// <param name="Food">What the hit says: name, brands, pack label and per-100 nutrients, with no id. Its pack amount and serving are unknown until the barcode is looked up.</param>
/// <param name="IsNew">True when no stored food has this barcode.</param>
public sealed record OnlineProduct(string Barcode, Food Food, bool IsNew);

/// <summary>The outcome of an online search. <see cref="Products"/> is empty unless <see cref="Status"/> is <see cref="OnlineSearchStatus.Found"/>.</summary>
public sealed record OnlineSearchResult
{
    private OnlineSearchResult(OnlineSearchStatus status, IReadOnlyList<OnlineProduct> products, string? country)
    {
        Status = status;
        Products = products;
        Country = country;
    }

    public OnlineSearchStatus Status { get; }

    /// <summary>The products to show, best first, at most <see cref="IOnlineFoodSearch.MaxProducts"/>.</summary>
    public IReadOnlyList<OnlineProduct> Products { get; }

    /// <summary>The country the search was limited to, such as "United Kingdom". Null when it ran worldwide.</summary>
    public string? Country { get; }

    public static OnlineSearchResult Found(IReadOnlyList<OnlineProduct> products, string? country) =>
        products.Count == 0 ? None(country) : new(OnlineSearchStatus.Found, products, country);

    public static OnlineSearchResult None(string? country = null) => new(OnlineSearchStatus.None, [], country);

    public static OnlineSearchResult Limited() => new(OnlineSearchStatus.Limited, [], null);

    public static OnlineSearchResult Offline() => new(OnlineSearchStatus.Offline, [], null);

    public static OnlineSearchResult Failed() => new(OnlineSearchStatus.Failed, [], null);
}

/// <summary>Text search on Open Food Facts. Only an explicit call sends anything; a status comes back for every failure.</summary>
public interface IOnlineFoodSearch
{
    /// <summary>Most products a result holds after cleaning.</summary>
    public const int MaxProducts = 20;

    /// <summary>
    /// Sends <paramref name="query"/> to Open Food Facts, limited to the device's country (worldwide when it has none or Open Food Facts has no match there), and cleans the answer.
    /// A blank query finds nothing and sends nothing. Only the caller's own cancellation throws.
    /// </summary>
    Task<OnlineSearchResult> SearchAsync(string query, CancellationToken ct = default);
}
