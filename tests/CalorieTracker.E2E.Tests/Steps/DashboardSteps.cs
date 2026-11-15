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

    private static readonly string[] Meals = ["Breakfast", "Lunch", "Dinner", "Snacks"];

    [Then(@"^every meal has a \+ button$")]
    public void ThenEveryMealHasAPlus()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(() => Meals.Where(dashboard.HasAddButton).ToList(), Is.EqualTo(Meals));
    }

    [Then(@"^Lunch lists 1 entry with a kcal total$")]
    public void ThenLunchListsOneEntry()
    {
        web.Eventually(() => dashboard.EntryCount("Lunch"), Is.EqualTo(1));
        Assert.That(dashboard.MealTotal("Lunch"), Does.EndWith(" kcal"));
    }

    [Then(@"^the other meals list no entries$")]
    public void ThenTheOtherMealsListNoEntries() =>
        Assert.That(new[] { "Breakfast", "Dinner", "Snacks" }.Sum(dashboard.EntryCount), Is.Zero);

    [Then(@"^the calories eaten are above 0$")]
    public void ThenTheCaloriesEatenAreAbove0() =>
        web.Eventually(() => int.Parse(dashboard.Eaten().Replace(",", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThan(0));

    [Then(@"^the Dashboard no longer says nothing is logged$")]
    public void ThenTheDashboardNoLongerSaysNothingIsLogged() => Assert.That(web.Exists("dashboard-empty"), Is.False);

    [Then(@"^the ring is captioned ""(.*)""$")]
    public void ThenTheRingIsCaptioned(string caption)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(dashboard.RingLabel, Is.EqualTo(caption));
    }

    [Then(@"^the Dashboard says ""(.*)""$")]
    public void ThenTheDashboardSays(string text) => web.Eventually(() => dashboard.NoTargetText(), Is.EqualTo(text));

    [When(@"^I tap Set one in Settings$")]
    public void WhenITapSetOneInSettings() => dashboard.SetTarget();

    [When(@"^I tap the next day$")]
    public void WhenITapTheNextDay() => dashboard.NextDay();

    [When(@"^I tap the date strip label$")]
    public void WhenITapTheDateStripLabel() => dashboard.ToToday();

    [Then(@"^the date strip says ""(.*)""$")]
    public void ThenTheDateStripSays(string text) => web.Eventually(dashboard.DateLabel, Does.StartWith(text));

    [When(@"^I go back to the Dashboard$")]
    public void WhenIGoBackToTheDashboard() => journeys.GoToDashboard();
}
