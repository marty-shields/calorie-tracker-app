using System.Globalization;
using System.Text;

namespace CalorieTracker.Core.Foods;

/// <summary>Folds text the same way when <see cref="Data.Food.SearchText"/> is written and when a query is typed, so accents and case never decide a match.</summary>
internal static class SearchFolding
{
    /// <summary>Lowercases (invariant) and drops combining marks after decomposing, so "Crème" becomes "creme". Letters with no decomposition, such as "ł", are kept.</summary>
    public static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
