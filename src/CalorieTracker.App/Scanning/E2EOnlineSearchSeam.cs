#if DEBUG
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.App.Scanning;

/// <summary>
/// Debug builds only, for the E2E scenarios of Open Food Facts search: no scenario may reach the network, so the search call answers from here by what was typed (the whole text, any case):
/// "fizzy cola" finds three products of the United Kingdom, "oat" is offline, "milk" is busy (HTTP 429) and anything else finds nothing.
/// A tap on a product finds it again through the barcode lookup (<see cref="E2EScanSeam"/> gives <see cref="Lookup"/> its barcodes), which stores it as a scan would.
/// </summary>
internal static class E2EOnlineSearchSeam
{
    public const string Brand = "Testco";

    public const string CanBarcode = "5000000000017";

    public const string BottleBarcode = "5000000000024";

    public const string ZeroBarcode = "5000000000031";

    /// <summary>What a barcode lookup finds for the three products: the can and the 1 L bottle of one drink, and a zero-sugar variant that must stay a food of its own.</summary>
    public static BarcodeLookupResult? Lookup(string barcode) =>
        barcode switch
        {
            CanBarcode => BarcodeLookupResult.Found(Product(CanBarcode, "Fizzy Cola", "330 ml", 330, 42, 10.6), 330),
            BottleBarcode => BarcodeLookupResult.Found(Product(BottleBarcode, "Fizzy Cola", "1 L", 1000, 42, 10.6), 1000),
            ZeroBarcode => BarcodeLookupResult.Found(Product(ZeroBarcode, "Fizzy Cola Zero", "330 ml", 330, 0.3, 0), 330),
            _ => null,
        };

    private static Food Product(string barcode, string name, string pack, double packMl, double kcal, double carbs) =>
        new()
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = barcode,
            Name = name,
            Brand = Brand,
            Brands = Brand,
            PackageLabel = pack,
            PackageAmount = packMl,
            Unit = FoodUnit.Millilitre,
            Kcal = kcal,
            ProteinG = 0,
            CarbsG = carbs,
            FatG = 0,
            FetchedAt = new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero),
        };

    internal sealed class Search : IOnlineFoodSearch
    {
        public Task<OnlineSearchResult> SearchAsync(string query, CancellationToken ct = default) =>
            Task.FromResult(query.Trim().ToLowerInvariant() switch
            {
                "fizzy cola" => OnlineSearchResult.Found(
                    new[] { CanBarcode, BottleBarcode, ZeroBarcode }.Select(code => new OnlineProduct(code, Lookup(code)!.Food!, true)).ToList(),
                    "United Kingdom"),
                "oat" => OnlineSearchResult.Offline(),
                "milk" => OnlineSearchResult.Limited(),
                _ => OnlineSearchResult.None(),
            });
    }
}
#endif
