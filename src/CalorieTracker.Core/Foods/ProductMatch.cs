using System.Text;
using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// The strict rule for deciding that two Open Food Facts products are one food sold in different sizes (a 330 ml can and a 1 L bottle of the same drink).
/// Every part must hold, so anything doubtful stays two foods: a missed merge costs a second food, a wrong one gives a person the wrong nutrients.
/// </summary>
internal static class ProductMatch
{
    /// <summary>A nutrient per 100 matches when it differs by at most this fraction of the larger value, or by <see cref="AbsoluteTolerance"/>, whichever is more.</summary>
    public const double RelativeTolerance = 0.02;

    /// <summary>The smallest difference, in g per 100 g or 100 ml (kcal for energy), that is always within tolerance, so values near zero are not compared by their ratio.</summary>
    public const double AbsoluteTolerance = 0.1;

    /// <summary>
    /// True when <paramref name="incoming"/> and <paramref name="existing"/> have the same unit, a brand (not blank) that is the same after folding, a name that is the same after <see cref="NormaliseName"/> (not blank),
    /// and the same kcal, protein, carbs and fat within tolerance, and each of fibre, sugars, saturates and salt either unknown in both or within tolerance in both.
    /// The brand is the first brand only: "COCA-COLA SERVICES SA/NV" and "Coca-Cola" are different brands.
    /// </summary>
    public static bool IsSameProduct(Food existing, Food incoming)
    {
        if (existing.Unit != incoming.Unit)
        {
            return false;
        }

        var brand = NormaliseWords(existing.Brand);
        if (brand.Length == 0 || brand != NormaliseWords(incoming.Brand))
        {
            return false;
        }

        var name = NormaliseName(existing.Name);
        if (name.Length == 0 || name != NormaliseName(incoming.Name))
        {
            return false;
        }

        return Within(existing.Kcal, incoming.Kcal)
            && Within(existing.ProteinG, incoming.ProteinG)
            && Within(existing.CarbsG, incoming.CarbsG)
            && Within(existing.FatG, incoming.FatG)
            && Within(existing.FibreG, incoming.FibreG)
            && Within(existing.SugarsG, incoming.SugarsG)
            && Within(existing.SaturatesG, incoming.SaturatesG)
            && Within(existing.SaltG, incoming.SaltG);
    }

    /// <summary>
    /// The folded name with size words removed and every other character that is not a letter or digit treated as a space, so "Coca-Cola Original 1.5L" and "Coca Cola original 330 ml" read the same.
    /// A size word is a number with a unit: ml, cl, dl, l, litre(s), liter(s), g, kg, gram(s), oz. Words such as "zero", "light" or "sugar" are kept, which is what keeps variants apart.
    /// </summary>
    public static string NormaliseName(string? name) =>
        name is null ? "" : NormaliseWords(RemoveSizes(SearchFolding.Fold(name)));

    private static string NormaliseWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var folded = SearchFolding.Fold(text);
        var letters = string.Create(folded.Length, folded, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = IsWordChar(source[i]) ? source[i] : ' ';
            }
        });
        return string.Join(' ', letters.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Within(double a, double b) =>
        Math.Abs(a - b) <= Math.Max(AbsoluteTolerance, RelativeTolerance * Math.Max(Math.Abs(a), Math.Abs(b)));

    private static bool Within(double? a, double? b) =>
        a is null || b is null ? a is null && b is null : Within(a.Value, b.Value);

    private static readonly HashSet<string> SizeUnits = new(StringComparer.Ordinal)
    {
        "ml", "cl", "dl", "ltr", "litres", "litre", "liters", "liter", "l", "kg", "gram", "grams", "gr", "g", "oz",
    };

    private static bool IsWordChar(char c) => char.IsLetter(c) || char.IsNumber(c);

    /// <summary>Replaces each size (a number, an optional space, then a whole-word unit) with one space. A number glued to a letter or digit before it, or a unit that continues into more letters or digits, is not a size.</summary>
    private static string RemoveSizes(string text)
    {
        var result = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var end = SizeEnd(text, i);
            if (end > i)
            {
                result.Append(' ');
                i = end;
            }
            else
            {
                result.Append(text[i]);
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>The index after the size that starts at <paramref name="start"/>, or <paramref name="start"/> when none does.</summary>
    private static int SizeEnd(string text, int start)
    {
        if (!char.IsDigit(text[start]) || (start > 0 && IsWordChar(text[start - 1])))
        {
            return start;
        }

        var i = start;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i + 1 < text.Length && text[i] is '.' or ',' && char.IsDigit(text[i + 1]))
        {
            i++;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                i++;
            }
        }

        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        var unitEnd = i;
        while (unitEnd < text.Length && IsWordChar(text[unitEnd]))
        {
            unitEnd++;
        }

        return SizeUnits.Contains(text[i..unitEnd]) ? unitEnd : start;
    }
}
