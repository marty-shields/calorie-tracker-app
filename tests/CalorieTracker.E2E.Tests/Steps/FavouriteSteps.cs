using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>The star that makes a food a favourite, on Serving (add and edit mode).</summary>
[Binding]
public sealed class FavouriteSteps(ServingScreen serving, FoodSearchScreen foods, SnackbarScreen snackbar, Journeys journeys, WebView web)
{
    [When(@"^I open Serving again for the first food found by ""([^""]*)""$")]
    public void WhenIOpenServingAgainForTheFirstFoodFoundBy(string text)
    {
        journeys.WaitFor(Journeys.Screen.AddFood);
        foods.Search(text);
        foods.OpenFirstResult();
        serving.WaitUntilOpen();
    }

    [Given(@"^I have starred the food on Serving$")]
    public void GivenIHaveStarredTheFoodOnServing()
    {
        serving.TapStar();
        web.Eventually(snackbar.Message, Does.EndWith("Added to favourites"));
    }

    [When(@"^I tap the star on Serving$")]
    public void WhenITapTheStarOnServing() => serving.TapStar();

    [Then(@"^the star on Serving is outlined and named ""(.*)""$")]
    public void ThenTheStarOnServingIsOutlined(string name) => ServingStarIs("false", name);

    [Then(@"^the star on Serving is filled and named ""(.*)""$")]
    public void ThenTheStarOnServingIsFilled(string name) => ServingStarIs("true", name);

    [Then(@"^the star comes before the trash icon on Serving$")]
    public void ThenTheStarComesBeforeTheTrashIcon()
    {
        var order = serving.TopBarOrder();
        Assert.That(order.IndexOf("serving-favourite"), Is.GreaterThanOrEqualTo(0).And.LessThan(order.IndexOf("serving-remove")));
    }

    [Then(@"^the snackbar says ""([^""]*)""$")]
    public void ThenTheSnackbarSays(string text)
    {
        web.Eventually(snackbar.Message, Does.EndWith(text));
        Assert.That(snackbar.ActionLabel(), Is.Empty);
    }

    private void ServingStarIs(string pressed, string name) => web.Eventually(() => (serving.StarPressed(), serving.StarLabel()), Is.EqualTo((pressed, name)));
}
