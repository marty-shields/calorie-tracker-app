namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The snackbar above the navigation bar, on whichever screen shows it.</summary>
public sealed class SnackbarScreen(WebView web)
{
    public string Message() => web.Exists("snackbar-message") ? web.Text("snackbar-message") : "";

    public string ActionLabel() => web.Exists("snackbar-action") ? web.Text("snackbar-action") : "";

    public bool IsShowing() => web.Exists("snackbar");

    public void TapAction() => web.Tap("snackbar-action");
}
