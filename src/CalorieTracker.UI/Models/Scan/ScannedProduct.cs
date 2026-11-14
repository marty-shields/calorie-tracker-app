namespace CalorieTracker.UI.Models.Scan;

/// <summary>A looked-up product as the scan screen shows it. Values are per 100 g, or per 100 ml when <paramref name="IsLiquid"/>.</summary>
/// <param name="Brand">Null when the source has no brand.</param>
/// <param name="Kcal">Energy in kcal.</param>
/// <param name="Protein">Protein in grams.</param>
/// <param name="Carbs">Carbohydrate in grams.</param>
/// <param name="Fat">Fat in grams.</param>
/// <param name="IsLiquid">True when the values are per 100 ml.</param>
/// <param name="Fibre">Fibre in grams. Null when the source has none, and the screen shows no row.</param>
/// <param name="Sugars">Sugars in grams. Null when the source has none.</param>
/// <param name="Saturates">Saturated fat in grams. Null when the source has none.</param>
/// <param name="Salt">Salt in grams. Null when the source has none.</param>
public sealed record ScannedProduct(string Name, string? Brand, decimal Kcal, decimal Protein, decimal Carbs, decimal Fat, bool IsLiquid = false,
    decimal? Fibre = null, decimal? Sugars = null, decimal? Saturates = null, decimal? Salt = null)
{
    /// <summary>Largest allowed gap between <see cref="Kcal"/> and the energy the macros add up to, as a fraction of that energy. A placeholder to confirm.</summary>
    public const decimal KcalTolerance = 0.20m;

    /// <summary>True when kcal differs from 4 × protein + 4 × carbs + 9 × fat by more than <see cref="KcalTolerance"/>. A product with no macros never warns.</summary>
    public bool KcalMismatch
    {
        get
        {
            var expected = (4 * Protein) + (4 * Carbs) + (9 * Fat);
            return expected > 0 && Math.Abs(Kcal - expected) > KcalTolerance * expected;
        }
    }
}
