using CalorieTracker.UI.Components.Actions;

namespace CalorieTracker.UI.Navigation;

/// <summary>The bottom navigation bar's items, in bar order. Add is not here: it is an action, not a screen.</summary>
public static class NavDestinations
{
    public static IReadOnlyList<NavItem> All { get; } =
    [
        new(NavDestination.Today, "Today", IconName.Today, "today-link", ""),
    ];

    public static NavItem Of(NavDestination destination) => All.Single(item => item.Destination == destination);
}

/// <summary>One item of the bottom navigation bar.</summary>
/// <param name="Route">The App route it opens; empty for Today, whose route is <c>/</c>.</param>
public sealed record NavItem(NavDestination Destination, string Label, IconName Icon, string TestId, string Route);
