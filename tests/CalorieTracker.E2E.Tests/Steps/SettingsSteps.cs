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

    [Given(@"^I have set the daily target to (\d+)$")]
    [When(@"^I set the daily target to (\d+)$")]
    public void WhenISetTheDailyTargetTo(string kcal)
    {
        settings.TypeTarget(kcal);
        web.Eventually(settings.TargetError, Is.Null, "no message under the daily target");
        ReopenSettingsUntil(kcal);
    }

    [When(@"^I type (\d+) as the daily target$")]
    public void WhenITypeAsTheDailyTarget(string kcal) => settings.TypeTarget(kcal);

    [When(@"^I clear the daily target$")]
    public void WhenIClearTheDailyTarget()
    {
        settings.ClearTarget();
        ReopenSettingsUntil("");
    }

    /// <summary>
    /// Saves run as the user types and nothing on screen says when the last one ended. Settings reads the target after them (the database runs calls in order),
    /// so leaving and reopening it until it shows the expected value means the save is done before the app restarts.
    /// </summary>
    private void ReopenSettingsUntil(string expected)
    {
        settings.Back();
        journeys.OpenSettings();
        web.Eventually(settings.TargetValue, Is.EqualTo(expected), "daily target saved");
    }

    [Then(@"^the daily target is (\d+)$")]
    public void ThenTheDailyTargetIs(string kcal) =>
        web.Eventually(settings.TargetValue, Is.EqualTo(kcal), "daily target shown");

    [Then(@"^the daily target is empty$")]
    public void ThenTheDailyTargetIsEmpty() =>
        web.Eventually(settings.TargetValue, Is.EqualTo(""), "daily target shown");

    [Then(@"^Settings asks for ""(.*)""$")]
    public void ThenSettingsAsksFor(string message) =>
        web.Eventually(settings.TargetError, Is.EqualTo(message), "message under the daily target");

    [Then(@"^the app version is listed on Settings$")]
    public void ThenTheAppVersionIsListedOnSettings() => Assert.That(settings.Version(), Is.EqualTo($"Version {AppiumSetup.Adb.VersionName(AppiumSetup.App.Package)}"), "the version Settings shows against the installed package's versionName");
}
