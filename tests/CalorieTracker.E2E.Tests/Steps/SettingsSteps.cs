using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class SettingsSteps(DashboardScreen dashboard, SettingsScreen settings, Journeys journeys, ScenarioState state, WebView web)
{
    [When(@"^I tap Settings on the Dashboard$")]
    public void WhenITapSettingsOnTheDashboard()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        dashboard.OpenSettings();
    }

    [Given(@"^I have opened Settings$")]
    public void GivenIHaveOpenedSettings() => journeys.OpenSettings();

    [Given(@"^the phone's dark theme is (on|off)$")]
    public void GivenThePhonesDarkThemeIs(string onOrOff)
    {
        var replaced = Native.SetDarkTheme(onOrOff == "on" ? "yes" : "no");
        state.PhoneDarkTheme ??= replaced;
    }

    /// <summary>Puts back the phone's dark theme the scenario changed.</summary>
    [AfterScenario]
    public void RestoreDarkTheme()
    {
        if (state.PhoneDarkTheme is { } night)
        {
            Native.SetDarkTheme(night);
        }
    }

    [Given(@"^I have chosen the (System|Light|Dark) theme$")]
    [When(@"^I choose the (System|Light|Dark) theme$")]
    public void WhenIChooseTheTheme(string theme) => settings.ChooseTheme(theme.ToLowerInvariant());

    [When(@"^I open Settings$")]
    public void WhenIOpenSettings() => journeys.OpenSettings();

    [Then(@"^the (System|Light|Dark) theme is chosen$")]
    public void ThenTheThemeIsChosen(string theme) =>
        web.Eventually(() => settings.IsThemeSelected(theme.ToLowerInvariant()), Is.True, $"{theme} theme chosen");

    [Then(@"^the status bar icons are light$")]
    public void ThenTheStatusBarIconsAreLight() => web.Eventually(Native.StatusBarIcons, Is.EqualTo("light"), "status bar icons");

    [Then(@"^the data credits are listed on Settings$")]
    public void ThenTheDataCreditsAreListedOnSettings()
    {
        var (cofid, openFoodFacts) = settings.Credits();
        Assert.Multiple(() =>
        {
            Assert.That(cofid, Does.Contain("CoFID").And.Contain("Open Government Licence v3.0"));
            Assert.That(openFoodFacts, Does.Contain("Open Food Facts").And.Contain("Open Database Licence"));
        });
    }

    [Then(@"^the app version is listed on Settings$")]
    public void ThenTheAppVersionIsListedOnSettings() => Assert.That(settings.Version(), Is.EqualTo($"Version {AppiumSetup.Adb.VersionName(AppiumSetup.App.Package)}"), "the version Settings shows against the installed package's versionName");
}
