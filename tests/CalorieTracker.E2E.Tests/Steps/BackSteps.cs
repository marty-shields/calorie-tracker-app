using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>Back and which screen shows.</summary>
[Binding]
public sealed class BackSteps(WebView web, Native native, Journeys journeys)
{
    private const string Way = "(the back arrow|Android back)";

    [Given(@"^I have gone back with " + Way + "$")]
    [When(@"^I go back with " + Way + "$")]
    public void GoBackWith(string how) => journeys.Back(how);

    [When(@"^I press Android back$")]
    public void WhenIPressAndroidBack() => native.BackKey();

    [Then(@"^I am ((?:still )?)on (the Dashboard|Settings)$")]
    public void ThenIAmOn(string still, string screen)
    {
        if (still.Length > 0)
        {
            web.Settle();
        }

        journeys.WaitFor(screen == "Settings" ? Journeys.Screen.Settings : Journeys.Screen.Dashboard);
    }
}
