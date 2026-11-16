using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>The lists search shows: foods from the diary before and while typing, their last amounts, and "Show N more".</summary>
[Binding]
public sealed class FoodSearchGroupsSteps(FoodSearchScreen foods, ServingScreen serving, DashboardScreen dashboard, Journeys journeys, WebView web)
{
    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";
    private const string Groups = "(Usual|Recent|From your diary|Common|Branded)";

    private int rowsBefore;
    private int more;

    private static string Key(string heading) => heading switch
    {
        "From your diary" => "diary",
        var other => other.ToLowerInvariant(),
    };

    [Given(@"^I have logged (\d+) g of the first food found by ""([^""]*)"" in " + Meals + "$")]
    public void GivenIHaveLogged(string grams, string text, string meal)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        dashboard.AddTo(meal);
        foods.Search(text);
        foods.OpenFirstResult();
        serving.WaitUntilOpen();
        serving.EnterGrams(grams);
        serving.Add();
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(() => dashboard.Entries(meal).Count, Is.EqualTo(1));
    }

    [Then(@"^search lists only the " + Groups + " group$")]
    public void ThenSearchListsOnlyTheGroup(string heading) => foods.WaitForGroups(Key(heading));

    [Then(@"^search lists the groups From your diary and Common, in that order$")]
    public void ThenSearchListsTheDiaryThenCommon() => web.Eventually(foods.Groups, Is.EqualTo(new[] { "diary", "common" }));

    [When(@"^I open the first food in the " + Groups + " group$")]
    public void WhenIOpenTheFirstFoodIn(string heading) => foods.OpenFirstIn(Key(heading));

    [Then(@"^the first food in the " + Groups + @" group shows ""(\d+)"" g and its calories$")]
    public void ThenTheFirstFoodShows(string heading, string grams) =>
        web.Eventually(() => foods.FirstRowText(Key(heading)), Does.Match($@"{grams} g · \d+ kcal"));

    [Then(@"^no food shows twice$")]
    public void ThenNoFoodShowsTwice() => web.Eventually(foods.AllRowIds, Is.Unique);

    [Then(@"^Serving shows an amount of ""(.*)"" g$")]
    public void ThenServingShowsAnAmountOf(string grams)
    {
        serving.WaitUntilOpen();
        web.Eventually(serving.Amount, Is.EqualTo(grams));
        Assert.That(serving.Unit(), Is.EqualTo("g"));
    }

    [Then(@"^the Common group shows 5 foods and a Show more row$")]
    public void ThenTheCommonGroupShowsFiveAndAShowMoreRow()
    {
        more = foods.MoreCount("common");
        Assert.That(foods.RowIds("common"), Has.Count.EqualTo(5));
        rowsBefore = 5;
    }

    [When(@"^I tap Show more on the Common group$")]
    public void WhenITapShowMore() => foods.ShowMore("common");

    [Then(@"^the Common group shows the rest of its foods$")]
    public void ThenTheCommonGroupShowsTheRest() =>
        web.Eventually(() => foods.RowIds("common").Count, Is.EqualTo(rowsBefore + more));
}
