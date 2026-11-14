using CalorieTracker.UI.Components.Actions;

namespace CalorieTracker.UI.Navigation;

/// <summary>The bottom navigation bar's items, in bar order.</summary>
public static class NavDestinations
{
    /// <summary>Back from another top-level screen goes here; back from here leaves the app.</summary>
    public const NavDestination Start = NavDestination.Dashboard;

    public static IReadOnlyList<NavItem> All { get; } =
    [
        new(NavDestination.Dashboard, "Dashboard", IconName.Home, "dashboard-link", ""),
        new(NavDestination.Foods, "Foods", IconName.Search, "foods-link", "foods"),
        new(NavDestination.Scan, "Scan", IconName.Scan, "scan-link", "scan"),
    ];

    public static NavItem Of(NavDestination destination) => All.Single(item => item.Destination == destination);
}

/// <summary>One item of the bottom navigation bar.</summary>
/// <param name="Route">The App route it opens; empty for the Dashboard.</param>
public sealed record NavItem(NavDestination Destination, string Label, IconName Icon, string TestId, string Route);
