namespace CalorieTracker.UI.Navigation;

/// <summary>The Dashboard's route with the day to show, for screens that send a person to see what they just added.</summary>
public static class DashboardRoute
{
    /// <summary>The route that opens the Dashboard on <paramref name="date"/> (<c>/?date=yyyy-MM-dd</c>).</summary>
    public static string Url(DateOnly date) => $"/?date={RouteDate.ToText(date)}";
}
