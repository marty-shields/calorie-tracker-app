namespace CalorieTracker.UI.Components.Charts;

/// <summary>One bar of a <see cref="TrendBarChart"/>.</summary>
/// <param name="Date">What <c>OnSelect</c> returns. For a month bar, any day of that month.</param>
/// <param name="Label">The short text under the bar, such as "Mon".</param>
/// <param name="Value">Calories. Null, NaN, infinity, or zero or less, means nothing was logged and leaves an empty slot.</param>
/// <param name="Target">The target this bar is judged against, when it differs from the chart's.</param>
/// <param name="Note">Marks the bar as resting on little data, such as "based on 2 days": the bar is drawn faded and the note joins its text alternative. Null for a normal bar.</param>
public sealed record TrendBar(DateOnly Date, string Label, double? Value, double? Target = null, string? Note = null);
