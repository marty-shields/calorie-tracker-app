using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>The star that makes a food a favourite, on Serving (add and edit mode) and on a food's values on the Foods tab.</summary>
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

    [When(@"^I tap the star on the food's values$")]
    public void WhenITapTheStarOnTheFoodsValues() => foods.TapDetailStar();

    [Then(@"^the star on the food's values is outlined and named ""(.*)""$")]
    public void ThenTheStarOnTheValuesIsOutlined(string name) => ValuesStarIs("false", name);

    [Then(@"^the star on the food's values is filled and named ""(.*)""$")]
    public void ThenTheStarOnTheValuesIsFilled(string name) => ValuesStarIs("true", name);

    [Then(@"^no result row shows a star$")]
    public void ThenNoResultRowShowsAStar() => Assert.That(foods.RowsShowAStar(), Is.False);

    private void ServingStarIs(string pressed, string name) => web.Eventually(() => (serving.StarPressed(), serving.StarLabel()), Is.EqualTo((pressed, name)));

    private void ValuesStarIs(string pressed, string name) => web.Eventually(() => (foods.DetailStarPressed(), foods.DetailStarLabel()), Is.EqualTo((pressed, name)));
}
