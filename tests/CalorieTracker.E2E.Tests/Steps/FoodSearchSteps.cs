using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class FoodSearchSteps(FoodSearchScreen foods, Journeys journeys, WebView web)
{
    [Given(@"^I have opened Foods$")]
    public void GivenIHaveOpenedFoods() => journeys.OpenFoods();

    [When(@"^I open the old link ""(.*)""$")]
    public void WhenIOpenTheOldLink(string route)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.NavigateTo(route);
        web.WaitUntil(() => journeys.Current() != Journeys.Screen.Dashboard, "the old link didn't open a screen");
        if (route.StartsWith("/foods", StringComparison.Ordinal))
        {
            foods.WaitUntilLoaded();
        }
    }

    [Given(@"^I have searched for ""(.*)""$")]
    [When(@"^I search for ""(.*)""$")]
    public void WhenISearchFor(string text) => foods.Search(text);

    [When(@"^I open the first result$")]
    public void WhenIOpenTheFirstResult() => foods.OpenFirstResult();

    [Given(@"^I have opened the first result$")]
    public void GivenIHaveOpenedTheFirstResult() => foods.OpenFirstResult();

    [When(@"^I tap the barcode button in search$")]
    public void WhenITapTheBarcodeButtonInSearch()
    {
        foods.WaitUntilLoaded();
        Assert.That(foods.HasScanButton(), Is.True, "the barcode button");
        foods.TapScan();
    }

    [Then(@"^search is titled for a meal of the time of day$")]
    public void ThenSearchIsTitledForAMealOfTheTimeOfDay() =>
        web.Eventually(foods.ScreenTitle, Does.Match(@"^Add to (Breakfast|Lunch|Dinner|Snacks)$"));

    [Then(@"^no food values screen is shown$")]
    public void ThenNoFoodValuesScreenIsShown() => Assert.That(foods.ShowsDetail(), Is.False);

    [Then(@"^no type chips show$")]
    public void ThenNoTypeChipsShow() =>
        web.Eventually(foods.HasTypeChips, Is.False, "no type chips");

    [Then(@"^""(.*)"" is still typed$")]
    public void ThenIsStillTyped(string text) => web.Eventually(foods.Typed, Is.EqualTo(text));

    [Then(@"^Foods shows ""(.*)""$")]
    public void ThenFoodsShows(string text) =>
        web.Eventually(() => text == "No foods match" ? foods.NoneText() : foods.PromptText(), Is.EqualTo(text));

    [Then(@"^every result is a food with its energy per 100 g or ml$")]
    public void ThenEveryResultShowsEnergy() =>
        web.Eventually(foods.ResultRows, Has.Count.GreaterThan(0).And.All.Match(@"\d+(\.\d)? kcal / 100 (g|ml)"));

    [Then(@"^the results include a food named like ""(.*)""$")]
    public void ThenTheResultsIncludeAFoodNamedLike(string word) =>
        web.Eventually(foods.ResultRows, Has.Some.Contains(word).IgnoreCase);
}
