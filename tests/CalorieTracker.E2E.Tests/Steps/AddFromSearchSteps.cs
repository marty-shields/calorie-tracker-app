using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>Adding a food from search: a meal's search, Add to diary, and the snackbar that follows.</summary>
[Binding]
public sealed class AddFromSearchSteps(FoodSearchScreen foods, ServingScreen serving, SnackbarScreen snackbar, Journeys journeys, WebView web)
{
    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";

    [Given(@"^I have opened search to add to " + Meals + "$")]
    public void GivenIHaveOpenedSearchToAddTo(string meal)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        foods.OpenToAdd(meal);
    }

    [When(@"^I tap Add to diary$")]
    public void WhenITapAddToDiary() => foods.AddToDiary();

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

    [Then(@"^the snackbar says it was added to the suggested meal with the action ""(.*)""$")]
    public void ThenTheSnackbarSaysAddedToTheSuggestedMeal(string action)
    {
        web.Eventually(snackbar.Message, Does.Match(@" added to (Breakfast|Lunch|Dinner|Snacks)$"));
        Assert.That(snackbar.ActionLabel(), Is.EqualTo(action));
    }
}
