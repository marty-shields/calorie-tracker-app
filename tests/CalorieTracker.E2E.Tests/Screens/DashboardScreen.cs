namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The Dashboard: its empty state, the navigation bar and the Settings gear.</summary>
public sealed class DashboardScreen(WebView web)
{
    public const string Title = "Calorie Tracker";

    /// <summary>The empty state's message, once the Dashboard has loaded.</summary>
    public string EmptyText()
    {
        web.WaitUntil(() => web.Exists("dashboard-empty-message"), "the Dashboard didn't load");
        return web.Text("dashboard-empty-message");
    }

    public bool HasStartupError() => web.Exists("startup-error");

    public void OpenSettings() => web.Tap("settings");
}
