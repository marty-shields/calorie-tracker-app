using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class FoodSearchSteps(FoodSearchScreen foods, NavigationBar navigation, Journeys journeys, WebView web)
{
    [Given(@"^I have opened Foods$")]
    public void GivenIHaveOpenedFoods() => journeys.OpenFoods();

    [When(@"^I open Foods from the navigation bar$")]
    public void WhenIOpenFoodsFromTheNavigationBar()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        navigation.Open("Foods");
    }

    [Given(@"^I have searched for ""(.*)""$")]
    [When(@"^I search for ""(.*)""$")]
    public void WhenISearchFor(string text) => foods.Search(text);

    [When(@"^I open the first result$")]
    public void WhenIOpenTheFirstResult() => foods.OpenFirstResult();

    [Given(@"^I have opened the first result$")]
    public void GivenIHaveOpenedTheFirstResult() => foods.OpenFirstResult();

    [When(@"^I tap Back to results$")]
    public void WhenITapBackToResults() => foods.BackToResults();

    [Then(@"^Foods shows ""(.*)""$")]
    public void ThenFoodsShows(string text) =>
        web.Eventually(() => text == "No foods match" ? foods.NoneText() : foods.PromptText(), Is.EqualTo(text));

    [Then(@"^every result is a food with its energy per 100 g or ml$")]
    public void ThenEveryResultShowsEnergy() =>
        web.Eventually(foods.ResultRows, Has.Count.GreaterThan(0).And.All.Match(@"\d+(\.\d)? kcal / 100 (g|ml)"));

    [Then(@"^the results include a food named like ""(.*)""$")]
    public void ThenTheResultsIncludeAFoodNamedLike(string word) =>
        web.Eventually(foods.ResultRows, Has.Some.Contains(word).IgnoreCase);

    [Then(@"^the food's name and its energy are shown$")]
    public void ThenTheFoodsValuesAreShown()
    {
        Assert.That(foods.ShowsDetail(), Is.True, "food values");
        Assert.Multiple(() =>
        {
            Assert.That(foods.DetailName(), Is.Not.Empty);
            Assert.That(foods.DetailEnergy(), Does.Match(@"^\d+(\.\d)? kcal$"));
        });
    }

    [Then(@"^the results show again with ""(.*)"" still typed$")]
    public void ThenTheResultsShowAgain(string text)
    {
        web.Eventually(foods.ShowsDetail, Is.False, "food values hidden");
        Assert.Multiple(() =>
        {
            Assert.That(foods.ResultRows(), Is.Not.Empty);
            Assert.That(foods.Typed(), Is.EqualTo(text));
        });
    }

}
