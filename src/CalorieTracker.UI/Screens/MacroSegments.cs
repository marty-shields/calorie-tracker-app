using Shields.UI.Components.Charts;

namespace CalorieTracker.UI.Screens;

/// <summary>The segments of the kit's <see cref="StackedBar"/> for the energy share of protein, carbs and fat.</summary>
internal static class MacroSegments
{
    private const double KcalPerGramProtein = 4;
    private const double KcalPerGramCarbs = 4;
    private const double KcalPerGramFat = 9;

    /// <summary>Protein, carbs and fat in the data series 1, 2 and 3, sized by their calories (negative grams count as zero). The bar is labelled in kcal, since its sizes are.</summary>
    public static IReadOnlyList<StackedBarSegment> Of(double proteinG, double carbsG, double fatG) =>
    [
        new("Protein", Math.Max(proteinG, 0) * KcalPerGramProtein, DataSeries.One),
        new("Carbs", Math.Max(carbsG, 0) * KcalPerGramCarbs, DataSeries.Two),
        new("Fat", Math.Max(fatG, 0) * KcalPerGramFat, DataSeries.Three),
    ];
}
