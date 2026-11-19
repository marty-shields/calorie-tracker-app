using System.Globalization;
using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Scanning;

/// <summary>The "Adding to Dinner · 18 Nov" text of the scan chip, shared by the web view and the native camera screen.</summary>
public static class ScanCaption
{
    public static string For(Meal meal, DateOnly date) => $"Adding to {meal} · {date.ToString("d MMM", CultureInfo.InvariantCulture)}";
}
