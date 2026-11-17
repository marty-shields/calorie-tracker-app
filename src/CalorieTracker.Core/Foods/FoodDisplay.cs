using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// How a food is shown on every screen. Brands are compared with the title as whole words ignoring case, accents, spaces and hyphens, so "Coca Cola" already contains "Coca-Cola", but "Goat Milk" does not contain "Oat".
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
        var title = list.Count == 0 || list.Any(b => ContainsBrand(trimmedName, b))
            ? trimmedName
            : trimmedName.Length == 0 ? list[0] : $"{list[0]} {trimmedName}";
        var titleKey = Compare(title);
        var line = list.FirstOrDefault(b => !ContainsBrand(title, b));
        var pack = string.IsNullOrWhiteSpace(packageLabel) ? null : packageLabel.Trim();
        var generic = string.IsNullOrWhiteSpace(genericName) || Compare(genericName) == titleKey || Compare(genericName) == Compare(trimmedName) ? null : genericName.Trim();
        return new FoodDisplay(title, line, pack, generic);
    }

    /// <summary>The brand line and the pack joined with " · ", skipping what is empty. Null when both are.</summary>
    public string? Detail => BrandLine is null ? Pack : Pack is null ? BrandLine : $"{BrandLine} · {Pack}";

    /// <summary>True when the brand's words run through whole words of the text, the same letters in the same order. "Cocacola" meets "Coca Cola", "Oat" does not meet "Goat".</summary>
    private static bool ContainsBrand(string text, string brand)
    {
        var target = Compare(brand);
        var words = Words(text);
        for (var start = 0; start < words.Count; start++)
        {
            var joined = "";
            for (var end = start; end < words.Count && joined.Length < target.Length; end++)
            {
                joined += words[end];
            }

            if (joined == target)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        foreach (var c in SearchFolding.Fold(text))
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    private static string Compare(string text) =>
        new(SearchFolding.Fold(text).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
}
