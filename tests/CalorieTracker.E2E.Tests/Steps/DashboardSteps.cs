using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class DashboardSteps(WebView web, DashboardScreen dashboard, NavigationBar navigation, Journeys journeys)
{
    [Then(@"^I am on the Dashboard titled ""(.*)""$")]
    public void ThenOnTheDashboardTitled(string title) => web.Eventually(() => web.Text("screen-title"), Is.EqualTo(title));

    [Then(@"^the Dashboard shows ""(.*)""$")]
    public void ThenTheDashboardShows(string text)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(dashboard.EmptyText, Is.EqualTo(text));
    }

    [Then(@"^I can open the Dashboard from the navigation bar$")]
    public void ThenTheNavigationBarShows() => Assert.That(navigation.Has("Dashboard"), Is.True, "Dashboard");

    [Then(@"^no startup error is shown$")]
    public void ThenNoStartupError() => Assert.That(dashboard.HasStartupError(), Is.False);

    [When(@"^I go back to the Dashboard$")]
    public void WhenIGoBackToTheDashboard() => journeys.GoToDashboard();
}
