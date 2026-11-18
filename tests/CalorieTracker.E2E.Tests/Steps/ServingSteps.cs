using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>
/// The Serving screen. Without a preset meal the steps tap through Foods (open a food, Add to diary). A preset meal comes from a meal's + on the Dashboard,
/// then search and a result. Only a food that does not exist is opened through the router, since no tap reaches it.
/// </summary>
[Binding]
public sealed class ServingSteps(ServingScreen serving, FoodSearchScreen foods, DashboardScreen dashboard, Journeys journeys, WebView web)
{
    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";

    [Given(@"^I have opened Serving for the first food found by ""([^""]*)""$")]
    public void GivenIHaveOpenedServingForTheFirstFoodFoundBy(string text) => OpenFirstFound(text, null);

    [Given(@"^I have opened Serving for the first food found by ""([^""]*)"" with " + Meals + " preset$")]
    public void GivenIHaveOpenedServingForTheFirstFoodFoundByWithPreset(string text, string meal) => OpenFirstFound(text, meal);

    private void OpenFirstFound(string text, string? meal)
    {
        if (meal is null)
        {
            journeys.OpenFoods();
            foods.Search(text);
            foods.OpenFirstResult();
            foods.AddToDiary();
            serving.WaitUntilOpen();
            return;
        }

        journeys.WaitFor(Journeys.Screen.Dashboard);
        dashboard.AddTo(meal);
        foods.Search(text);
        foods.OpenFirstResult();
        serving.WaitUntilOpen();
    }

    [When(@"^I open Serving for a food that does not exist$")]
    public void WhenIOpenServingForAFoodThatDoesNotExist() => serving.OpenMissing(999_999_999);

    [Given(@"^I enter an amount of ""(.*)""$")]
    [When(@"^I enter an amount of ""(.*)""$")]
    public void WhenIEnterAnAmountOf(string text) => serving.EnterAmount(text);

    [Given(@"^I have added my own serving ""(.*)"" of ""(.*)""$")]
    [When(@"^I add my own serving ""(.*)"" of ""(.*)""$")]
    public void WhenIAddMyOwnServing(string label, string amount) => serving.AddOwnServing(label, amount);

    [When(@"^I choose the serving ""(.*)""$")]
    public void WhenIChooseTheServing(string label) => serving.ChooseServing(label);

    [Then(@"^Serving shows the serving ""(.*)""$")]
    public void ThenServingShowsTheServing(string label) => web.Eventually(serving.ServingLabel, Is.EqualTo(label));

    [Then(@"^Serving has the Grams and Servings switch$")]
    public void ThenServingHasTheSwitch() => web.Eventually(serving.HasModeSwitch, Is.True);

    [Then(@"^the servings list reads ""(.*)""$")]
    public void ThenTheServingsListReads(string labels) => web.Eventually(serving.ServingOptions, Is.EqualTo(labels.Split(", ")));

    [Then(@"^the weight reads ""(.*)""$")]
    public void ThenTheWeightReads(string text) => web.Eventually(serving.Weight, Is.EqualTo(text));

    [Then(@"^the " + Meals + @" entry reads ""(.*)""$")]
    public void ThenTheEntryReads(string meal, string text) =>
        web.Eventually(() => dashboard.OpenAndReadEntries(meal).FirstOrDefault() ?? "", Does.Contain(text));

    [When(@"^I tap Add$")]
    public void WhenITapAdd() => serving.Add();

    [Then(@"^Serving shows the food's name and an amount in grams$")]
    public void ThenServingShowsTheFoodsNameAndAnAmountInGrams()
    {
        Assert.That(serving.Title(), Is.Not.Empty);
        web.Eventually(serving.Amount, Is.EqualTo("100"));
        Assert.That(serving.Unit(), Is.EqualTo("g"));
    }

    [Then(@"^Serving has no Grams and Servings switch$")]
    public void ThenServingHasNoSwitch() => Assert.That(serving.HasModeSwitch(), Is.False);

    [Then(@"^the meal chips read Breakfast, Lunch, Dinner, Snacks in that order$")]
    public void ThenTheMealChipsReadInOrder() =>
        web.Eventually(serving.MealLabels, Is.EqualTo(new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }));

    [Then(@"^the " + Meals + " meal is chosen$")]
    public void ThenTheMealIsChosen(string meal)
    {
        web.Eventually(() => serving.IsMealChosen(meal), Is.True, $"{meal} chosen");
        Assert.That(serving.ChosenMeals(), Is.EqualTo(1));
    }

    [Then(@"^Add reads ""(.*)""$")]
    public void ThenAddReads(string text) => web.Eventually(serving.AddText, Is.EqualTo(text));

    [Then(@"^Add names the chosen meal$")]
    public void ThenAddNamesTheChosenMeal() => web.Eventually(serving.AddText, Does.Match(@"^Add to (Breakfast|Lunch|Dinner|Snacks)$"));

    [Then(@"^Serving says the meal was suggested from the time$")]
    public void ThenServingSaysTheMealWasSuggestedFromTheTime() =>
        web.Eventually(serving.Suggestion, Does.Match(@"^Suggested from the time: \d\d:\d\d$"));

    [Then(@"^Serving does not say a meal was suggested$")]
    public void ThenServingDoesNotSayAMealWasSuggested()
    {
        web.Settle();
        Assert.That(serving.Suggestion(), Is.Null);
    }

    [Then(@"^Serving says ""(.*)""$")]
    public void ThenServingSays(string text) =>
        web.Eventually(() => text == "Food not found" ? serving.NotFoundText() : serving.AmountError(), Is.EqualTo(text));

    [Then(@"^Add is (disabled|enabled)$")]
    public void ThenAddIs(string state) => web.Eventually(serving.CanAdd, Is.EqualTo(state == "enabled"));

    [Then(@"^This adds shows the energy for the amount$")]
    public void ThenThisAddsShowsTheEnergy() => web.Eventually(serving.AddsKcal, Does.Match(@"^\d+ kcal$"));

    [Then(@"^This adds is hidden$")]
    public void ThenThisAddsIsHidden() => web.Eventually(serving.ShowsAdds, Is.False);
}
