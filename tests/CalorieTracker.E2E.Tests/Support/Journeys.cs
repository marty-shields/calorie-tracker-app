using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;

namespace CalorieTracker.E2E.Tests.Support;

/// <summary>What a user does across screens: open Settings, Foods or Scan, go back to the Dashboard.</summary>
public sealed class Journeys(WebView web, Native native, DashboardScreen dashboard, SettingsScreen settings, NavigationBar navigation)
{
    public enum Screen
    {
        Dashboard,
        Settings,
        Foods,
        Scan,
        Other,
    }

    /// <summary>The screen shown, by its title.</summary>
    public Screen Current() => web.Text("screen-title") switch
    {
        DashboardScreen.Title => Screen.Dashboard,
        SettingsScreen.Title => Screen.Settings,
        FoodSearchScreen.Title => Screen.Foods,
        ScanScreen.Title => Screen.Scan,
        _ => Screen.Other,
    };

    /// <summary>Waits until <paramref name="screen"/> shows.</summary>
    public void WaitFor(Screen screen) => web.WaitUntil(() => Current() == screen, $"{screen} didn't show");

    /// <summary>Opens Settings from the Dashboard and waits for it.</summary>
    public void OpenSettings()
    {
        WaitFor(Screen.Dashboard);
        dashboard.OpenSettings();
        WaitFor(Screen.Settings);
    }

    /// <summary>Opens Foods from the Dashboard's navigation bar and waits for it.</summary>
    public void OpenFoods()
    {
        WaitFor(Screen.Dashboard);
        navigation.Open("Foods");
        WaitFor(Screen.Foods);
    }

    /// <summary>Opens Scan from the navigation bar and waits for it.</summary>
    public void OpenScan()
    {
        WaitFor(Screen.Dashboard);
        navigation.Open("Scan");
        WaitFor(Screen.Scan);
    }

    /// <summary>Goes to the Dashboard from Settings, Foods or Scan, or stays on it.</summary>
    public void GoToDashboard()
    {
        switch (Current())
        {
            case Screen.Settings:
                settings.Back();
                break;
            case Screen.Foods:
            case Screen.Scan:
                navigation.Open("Dashboard");
                break;
        }

        WaitFor(Screen.Dashboard);
    }

    /// <summary>Goes back: "the back arrow" or "Android back" (the back key).</summary>
    public void Back(string how)
    {
        switch (how)
        {
            case "the back arrow":
                web.Tap("back");
                break;
            case "Android back":
                native.BackKey();
                break;
            default:
                throw new ArgumentException($"Unknown way back: {how}.", nameof(how));
        }
    }
}
