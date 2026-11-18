using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;

namespace CalorieTracker.E2E.Tests.Support;

/// <summary>What a user does across screens: open Settings, Foods or Scan (the last two through the Add sheet), go back to the Dashboard.</summary>
public sealed class Journeys(WebView web, Native native, DashboardScreen dashboard, SettingsScreen settings, AddSheetScreen addSheet)
{
    public enum Screen
    {
        Dashboard,
        Settings,
        Foods,
        Scan,
        AddFood,
        Other,
    }

    /// <summary>The screen shown, by its title.</summary>
    public Screen Current() => web.Text("screen-title") switch
    {
        DashboardScreen.Title => Screen.Dashboard,
        SettingsScreen.Title => Screen.Settings,
        FoodSearchScreen.Title => Screen.Foods,
        ScanScreen.Title => Screen.Scan,
        var title when FoodSearchScreen.IsAddTitle(title) => Screen.AddFood,
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

    /// <summary>
    /// Opens food search with no meal, by the old <c>/foods</c> link, and waits for it. Nothing in the app leads there now:
    /// the Add sheet always opens search for a meal (<see cref="OpenFoodsFromTheAddSheet"/>). Scenarios that need the meal-less values step use this until it is removed.
    /// </summary>
    public void OpenFoods()
    {
        WaitFor(Screen.Dashboard);
        web.NavigateTo("/foods");
        WaitFor(Screen.Foods);
    }

    /// <summary>Opens search from the Dashboard through the Add sheet's Search foods, which adds to the meal the sheet guessed, and waits for it.</summary>
    public void OpenFoodsFromTheAddSheet()
    {
        WaitFor(Screen.Dashboard);
        addSheet.Open();
        addSheet.Search();
        WaitFor(Screen.AddFood);
    }

    /// <summary>Opens Scan from the Dashboard through the Add sheet's Scan barcode and waits for it.</summary>
    public void OpenScan()
    {
        WaitFor(Screen.Dashboard);
        addSheet.Open();
        addSheet.Scan();
        WaitFor(Screen.Scan);
    }

    /// <summary>Goes to the Dashboard from Settings, Foods, Scan or search for a meal (Android back, which the bar no longer replaces), or stays on it.</summary>
    public void GoToDashboard()
    {
        switch (Current())
        {
            case Screen.Settings:
                settings.Back();
                break;
            case Screen.Foods:
            case Screen.Scan:
            case Screen.AddFood:
                native.BackKey();
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
