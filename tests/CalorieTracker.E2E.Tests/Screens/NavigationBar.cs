namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The navigation bar on the top-level screens. A destination is named as in the bar, e.g. "Dashboard".</summary>
public sealed class NavigationBar(WebView web)
{
    public void Open(string destination) => web.Tap(TestId(destination));

    public bool Has(string destination) => web.Exists(TestId(destination));

    private static string TestId(string destination) => $"{destination.ToLowerInvariant()}-link";
}
