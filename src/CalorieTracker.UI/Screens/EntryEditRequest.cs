using CalorieTracker.Core.Data;
using CalorieTracker.Core.Summary;

namespace CalorieTracker.UI.Screens;

/// <summary>An entry tapped on the Dashboard: the entry, the meal it is under and the day on the date strip.</summary>
public sealed record EntryEditRequest(SummaryEntry Entry, Meal Meal, DateOnly Date);
