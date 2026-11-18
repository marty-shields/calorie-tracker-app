namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The navigation bar on the top-level screens: a destination named as in the bar (such as "Today") and the raised Add button.</summary>
public sealed class NavigationBar(WebView web)
{
    public void Open(string destination) => web.Tap(TestId(destination));

    public bool Has(string destination) => web.Exists(TestId(destination));

    public bool HasAdd() => web.Exists("add-button");

    public void TapAdd() => web.Tap("add-button");

    private static string TestId(string destination) => $"{destination.ToLowerInvariant()}-link";
}
