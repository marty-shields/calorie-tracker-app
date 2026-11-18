using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Navigation;

/// <summary>The Dashboard's route with the day to show and the meal to open, for screens that send a person to see what they just added.</summary>
public static class DashboardRoute
{
    /// <summary>
    /// The route that opens the Dashboard (<c>/</c>), with <c>?date=yyyy-MM-dd</c> when <paramref name="date"/> is set (null is today) and <c>open=&lt;Meal&gt;</c>
    /// when <paramref name="open"/> is set: that meal shows open on Today even after its window, such as the one a food was added to.
    /// </summary>
    public static string Url(DateOnly? date, Meal? open = null)
    {
        var query = new List<string>();
        if (date is { } day)
        {
            query.Add($"date={RouteDate.ToText(day)}");
        }

        if (open is { } meal)
        {
            query.Add($"open={meal}");
        }

        return query.Count == 0 ? "/" : "/?" + string.Join('&', query);
    }
}
