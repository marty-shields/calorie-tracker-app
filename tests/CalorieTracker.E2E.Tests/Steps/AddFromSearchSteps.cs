using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>Adding a food from search: a meal's search, a result, and the snackbar that follows.</summary>
[Binding]
public sealed class AddFromSearchSteps(FoodSearchScreen foods, ServingScreen serving, SnackbarScreen snackbar, DashboardScreen dashboard, Journeys journeys, WebView web)
{
    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";

    [Given(@"^I have opened search to add to " + Meals + "$")]
    [When(@"^I tap \+ on " + Meals + "$")]
    public void GivenIHaveOpenedSearchToAddTo(string meal)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        dashboard.AddTo(meal);
    }

    [When(@"^I tap Back on Serving$")]
    public void WhenITapBackOnServing() => serving.Back();

    [Given(@"^search is titled ""(.*)"" with no navigation bar$")]
    [Then(@"^search is titled ""(.*)"" with no navigation bar$")]
    public void ThenSearchIsTitledWithNoNavigationBar(string title)
    {
        web.Eventually(foods.ScreenTitle, Is.EqualTo(title));
        Assert.That(foods.HasNavigationBar(), Is.False);
    }

    [Then(@"^Serving has the " + Meals + " meal chosen$")]
    public void ThenServingHasTheMealChosen(string meal)
    {
        web.WaitUntil(() => web.Exists("serving-add"), "the Serving screen didn't show");
        web.Eventually(() => serving.IsMealChosen(meal), Is.True, $"{meal} chosen");
    }

    [Then(@"^the snackbar says it was added to " + Meals + @" with the action ""(.*)""$")]
    public void ThenTheSnackbarSaysAddedTo(string meal, string action)
    {
        web.Eventually(snackbar.Message, Does.EndWith($" added to {meal}"));
        Assert.That(snackbar.ActionLabel(), Is.EqualTo(action));
    }
}
