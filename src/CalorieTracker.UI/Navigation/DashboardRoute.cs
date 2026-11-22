namespace CalorieTracker.UI.Navigation;

/// <summary>The Dashboard's route with the day to show, for screens that send a person back to Today.</summary>
public static class DashboardRoute
{
    /// <summary>The route that opens the Dashboard (<c>/</c>), with <c>?date=yyyy-MM-dd</c> when <paramref name="date"/> is set (null is today).</summary>
    public static string Url(DateOnly? date) =>
        date is { } day ? $"/?date={RouteDate.ToText(day)}" : "/";
}
