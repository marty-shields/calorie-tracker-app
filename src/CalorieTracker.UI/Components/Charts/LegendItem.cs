namespace CalorieTracker.UI.Components.Charts;

/// <summary>One entry of a <see cref="ChartLegend"/>.</summary>
/// <param name="Kind">Picks the swatch.</param>
/// <param name="Label">The words next to the swatch.</param>
public sealed record LegendItem(LegendKind Kind, string Label);
