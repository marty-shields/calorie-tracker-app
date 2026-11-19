using CalorieTracker.E2E.Tests.Screens;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>Checks that a food's title is the same on search, Serving and the diary row.</summary>
[Binding]
public sealed class FoodTitleSteps(FoodSearchScreen foods, ServingScreen serving, DashboardScreen dashboard, WebView web)
{
    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";

    private string noted = "";

    private static string FirstLine(string text) => text.Split('\n')[0].Trim();

    [Given(@"^I have noted the title of the first result$")]
    public void GivenIHaveNotedTheTitleOfTheFirstResult()
    {
        web.WaitUntil(() => foods.FirstResultTitle() is { Length: > 0 }, "no result to note");
        noted = foods.FirstResultTitle();
    }

    [Given(@"^I have noted the title of the " + Meals + " entry$")]
    [Then(@"^I have noted the title of the " + Meals + " entry$")]
    public void GivenIHaveNotedTheTitleOfTheEntry(string meal)
    {
        web.Eventually(() => dashboard.OpenAndReadEntries(meal).Count, Is.EqualTo(1));
        noted = FirstLine(dashboard.OpenAndReadEntries(meal).Single());
        Assert.That(noted, Is.Not.Empty, "the entry shows its food's title");
    }

    [When(@"^I search for the noted title$")]
    public void WhenISearchForTheNotedTitle() => foods.Search(noted);

    [Then(@"^the results include a food titled with the noted title$")]
    public void ThenTheResultsIncludeAFoodTitledWithTheNotedTitle() =>
        web.Eventually(() => foods.ResultRows().Select(FirstLine).ToList(), Does.Contain(noted), "a result row headed with the noted title");

    [Then(@"^the Favourites group lists a food titled with the noted title$")]
    public void ThenTheFavouritesGroupListsTheNotedTitle() =>
        web.Eventually(() => foods.RowTexts("favourites").Select(FirstLine).ToList(), Does.Contain(noted), "a Favourites row headed with the noted title");

    [Then(@"^Serving is headed with a title containing ""(.*)""$")]
    public void ThenServingIsHeadedWithATitleContaining(string word)
    {
        web.WaitUntil(() => web.Exists("serving-add"), "the Serving screen didn't show");
        web.Eventually(serving.Title, Does.Contain(word).IgnoreCase);
    }

    [Then(@"^Serving is headed with the noted title$")]
    public void ThenServingIsHeadedWithTheNotedTitle() => web.Eventually(serving.Title, Is.EqualTo(noted));

    [Then(@"^the day has an entry headed with the noted title$")]
    public void ThenTheDayHasAnEntryHeadedWithTheNotedTitle()
    {
        web.WaitUntil(() => web.Exists("dashboard-ring"), "the Dashboard didn't show the day");
        web.Eventually(
            () => new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }.SelectMany(dashboard.OpenAndReadEntries).Any(row => row.StartsWith(noted, StringComparison.Ordinal)),
            Is.True,
            "a diary row that starts with the noted title");
    }
}
