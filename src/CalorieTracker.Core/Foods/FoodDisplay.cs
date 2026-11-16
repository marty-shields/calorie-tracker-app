using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// How a food is shown on every screen. Brands are compared with the title ignoring case, accents, spaces and hyphens, so "Coca Cola" already contains "Coca-Cola".
/// </summary>
/// <param name="Title">The name when it already contains one of the brands, otherwise the first brand, a space and the name. Just the name when the food has no brand.</param>
/// <param name="BrandLine">The first brand the title does not contain. Null when there is none.</param>
/// <param name="Pack">The pack size as the source words it. Null when there is none.</param>
/// <param name="GenericName">The source's generic name. Null when blank or when it is the title or the original name (compared the same way as brands).</param>
public sealed record FoodDisplay(string Title, string? BrandLine, string? Pack, string? GenericName)
{
    public static FoodDisplay Of(Food food) =>
        Of(food.Name, food.Brand, food.Brands, food.PackageLabel, food.GenericName);

    /// <param name="brand">Used as the only brand when <paramref name="brands"/> lists none.</param>
    /// <param name="brands">The brands joined with ", ", first first.</param>
    public static FoodDisplay Of(string name, string? brand, string? brands, string? packageLabel, string? genericName = null)
    {
        var list = (brands ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(b => b.Any(char.IsLetterOrDigit))
            .ToList();
        if (list.Count == 0 && brand is not null && brand.Any(char.IsLetterOrDigit))
        {
            list.Add(brand.Trim());
        }

        var trimmedName = (name ?? "").Trim();
        var folded = Compare(trimmedName);
        var title = list.Count == 0 || list.Any(b => folded.Contains(Compare(b), StringComparison.Ordinal))
            ? trimmedName
            : trimmedName.Length == 0 ? list[0] : $"{list[0]} {trimmedName}";
        var titleKey = Compare(title);
        var line = list.FirstOrDefault(b => !titleKey.Contains(Compare(b), StringComparison.Ordinal));
        var pack = string.IsNullOrWhiteSpace(packageLabel) ? null : packageLabel.Trim();
        var generic = string.IsNullOrWhiteSpace(genericName) || Compare(genericName) == titleKey || Compare(genericName) == Compare(trimmedName) ? null : genericName.Trim();
        return new FoodDisplay(title, line, pack, generic);
    }

    /// <summary>The brand line and the pack joined with " · ", skipping what is empty. Null when both are.</summary>
    public string? Detail => BrandLine is null ? Pack : Pack is null ? BrandLine : $"{BrandLine} · {Pack}";

    private static string Compare(string text) =>
        new(SearchFolding.Fold(text).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
}
