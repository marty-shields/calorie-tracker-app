using System.Globalization;
using Shields.UI.Components.Charts;

namespace CalorieTracker.UI.Screens;

/// <summary>The segments of the kit's <see cref="StackedBar"/> for the energy share of protein, carbs and fat.</summary>
internal static class MacroSegments
{
    private const double KcalPerGramProtein = 4;
    private const double KcalPerGramCarbs = 4;
    private const double KcalPerGramFat = 9;

    /// <summary>Protein, carbs and fat in the data series 1, 2 and 3, sized by their calories in whole kcal (negative grams count as zero), labelled in grams.</summary>
    public static IReadOnlyList<StackedBarSegment> Of(double proteinG, double carbsG, double fatG) =>
    [
        new("Protein", Kcal(proteinG, KcalPerGramProtein), DataSeries.One, Grams(proteinG)),
        new("Carbs", Kcal(carbsG, KcalPerGramCarbs), DataSeries.Two, Grams(carbsG)),
        new("Fat", Kcal(fatG, KcalPerGramFat), DataSeries.Three, Grams(fatG)),
    ];

    private static string Grams(double grams) => $"{Math.Max(grams, 0).ToString("0.#", CultureInfo.InvariantCulture)} g";

    private static double Kcal(double grams, double kcalPerGram) => Math.Round(Math.Max(grams, 0) * kcalPerGram, MidpointRounding.AwayFromZero);
}
