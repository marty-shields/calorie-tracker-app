namespace CalorieTracker.UI.Navigation;

/// <summary>Settings' route and its <c>back</c> query: the in-app route that opened it, so Back returns there.</summary>
public static class SettingsRoute
{
    public const string Template = "/settings";

    /// <summary>The route that opens Settings and returns to <paramref name="openedFrom"/> (an in-app route such as <c>/trends?period=month&amp;from=2026-11-01</c>).</summary>
    public static string Url(string openedFrom) => $"{Template}?back={Uri.EscapeDataString(openedFrom)}";

    /// <summary>Where Back goes: the <c>back</c> route if it is in-app, else Today.</summary>
    public static string BackTarget(string? backTo) => ServingRoute.ReturnOr(backTo, "/");
}
