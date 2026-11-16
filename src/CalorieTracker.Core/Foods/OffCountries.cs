using System.Globalization;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// The Open Food Facts country tag for a device's two-letter country code, from the generated table in <c>OffCountries.Generated.cs</c>.
/// A phone says GB where Open Food Facts says UK, so GB is listed under its tag <c>en:united-kingdom</c>.
/// </summary>
internal static partial class OffCountries
{
    // Lazy: the order in which the two files' static fields are set up is not defined.
    private static readonly Lazy<Dictionary<string, (string Tag, string Name)>> ByCode =
        new(() => Generated.ToDictionary(e => e.Code, e => (e.Tag, e.Name), StringComparer.OrdinalIgnoreCase));

    /// <summary>Looks up a two-letter country code (any case). False for null, blank, not two letters, or a code Open Food Facts has no country for.</summary>
    /// <param name="tag">The tag the search filters on, such as <c>en:united-kingdom</c>.</param>
    /// <param name="name">The English name for the group heading, such as United Kingdom.</param>
    public static bool TryGet(string? code, out string tag, out string name)
    {
        if (code is { Length: 2 } && ByCode.Value.TryGetValue(code, out var found))
        {
            (tag, name) = found;
            return true;
        }

        tag = name = "";
        return false;
    }

    /// <summary>The region of the phone's current language (no permission needed), as a two-letter code. Null when the language has no region, such as plain English.</summary>
    public static string? DeviceRegionCode()
    {
        try
        {
            var region = RegionInfo.CurrentRegion;
            return region.TwoLetterISORegionName is { Length: 2 } code ? code : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
