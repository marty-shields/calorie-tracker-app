using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>Changing and removing entries: tap an entry, edit or trash it, and the Undo snackbar.</summary>
[Binding]
public sealed class DiarySteps(ServingScreen serving, FoodSearchScreen foods, DashboardScreen dashboard, SnackbarScreen snackbar, Journeys journeys, WebView web)
{
    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";

    private IReadOnlyList<string> entryBefore = [];
    private int eatenBefore;

    [Given(@"^I have added 100 g of the first food found by ""([^""]*)"" to " + Meals + "$")]
    public void GivenIHaveAddedTheFirstFoodFoundByTo(string text, string meal)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        dashboard.AddTo(meal);
        foods.Search(text);
        foods.OpenFirstResult();
        serving.WaitUntilOpen();
        serving.EnterGrams("100");
        serving.Add();
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(() => dashboard.OpenAndReadEntries(meal).Count, Is.EqualTo(1));
        entryBefore = dashboard.OpenAndReadEntries(meal);
        eatenBefore = dashboard.EatenKcal();
    }

    [When(@"^I tap the " + Meals + " entry$")]
    public void WhenITapTheEntry(string meal) => dashboard.OpenEntry(meal);

    [Then(@"^Serving is in edit mode with a Save button and no Add button$")]
    public void ThenServingIsInEditMode()
    {
        serving.WaitUntilEditing();
        Assert.Multiple(() =>
        {
            Assert.That(serving.SaveText(), Is.EqualTo("Save"));
            Assert.That(serving.HasAddButton(), Is.False);
            Assert.That(serving.Amount(), Is.EqualTo("100"));
        });
    }

    [Then(@"^the trash icon is named ""(.*)""$")]
    public void ThenTheTrashIconIsNamed(string name) => Assert.That(serving.RemoveLabel(), Is.EqualTo(name));

    [Then(@"^Serving shows the " + Meals + " meal and day$")]
    public void ThenServingShowsTheMealAndDay(string meal) =>
        web.Eventually(serving.When, Does.StartWith($"{meal} · "));

    [When(@"^I tap Save$")]
    public void WhenITapSave() => serving.Save();

    [When(@"^I choose " + Meals + @" on Serving$")]
    public void WhenIChooseOnServing(string meal) => serving.ChooseMeal(meal);

    [When(@"^I tap Back on Serving in edit mode$")]
    public void WhenITapBackInEditMode() => serving.Back();

    [When(@"^I tap the trash icon$")]
    public void WhenITapTheTrashIcon() => serving.Remove();

    [When(@"^I tap View day$")]
    public void WhenITapViewDay() => snackbar.TapAction();

    [When(@"^I tap Undo$")]
    public void WhenITapUndo() => snackbar.TapAction();

    [Then(@"^the " + Meals + " entry shows " + @"""(.*)""$")]
    public void ThenTheEntryShows(string meal, string text)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(() => dashboard.OpenAndReadEntries(meal).SingleOrDefault() ?? "", Does.Contain(text));
    }

    [Then(@"^the calories eaten went down$")]
    public void ThenTheCaloriesEatenWentDown() => web.Eventually(dashboard.EatenKcal, Is.LessThan(eatenBefore));

    [Then(@"^the calories eaten are as before$")]
    public void ThenTheCaloriesEatenAreAsBefore() => web.Eventually(dashboard.EatenKcal, Is.EqualTo(eatenBefore));

    [Then(@"^" + Meals + " lists no entries$")]
    public void ThenListsNoEntries(string meal)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(() => dashboard.OpenAndReadEntries(meal).Count, Is.Zero);
    }

    [Then(@"^" + Meals + " lists 1 entry$")]
    public void ThenListsOneEntry(string meal) => web.Eventually(() => dashboard.OpenAndReadEntries(meal).Count, Is.EqualTo(1));

    [Then(@"^the day lists 1 entry$")]
    public void ThenTheDayListsOneEntry()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(() => new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }.Sum(meal => dashboard.OpenAndReadEntries(meal).Count), Is.EqualTo(1));
    }

    [Then(@"^the " + Meals + " entry is the same as before$")]
    public void ThenTheEntryIsTheSameAsBefore(string meal) =>
        web.Eventually(() => dashboard.OpenAndReadEntries(meal), Is.EqualTo(entryBefore));

    [Then(@"^the snackbar says ""(.*)"" with the action ""(.*)""$")]
    public void ThenTheSnackbarSays(string text, string action)
    {
        web.Eventually(snackbar.Message, Does.EndWith(text));
        Assert.That(snackbar.ActionLabel(), Is.EqualTo(action));
    }

    [Then(@"^the snackbar goes away$")]
    public void ThenTheSnackbarGoesAway() =>
        web.WaitUntil(() => !snackbar.IsShowing(), "the snackbar stayed", TimeSpan.FromSeconds(15));
}
