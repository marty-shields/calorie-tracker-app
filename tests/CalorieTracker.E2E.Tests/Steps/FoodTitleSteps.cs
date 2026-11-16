using CalorieTracker.E2E.Tests.Screens;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>Checks that a food's title is the same on search, its values, Serving, Scan and the diary row.</summary>
[Binding]
public sealed class FoodTitleSteps(FoodSearchScreen foods, ServingScreen serving, ScanScreen scan, DashboardScreen dashboard, WebView web)
{
    private string noted = "";

    [Given(@"^I have noted the title of the first result$")]
    public void GivenIHaveNotedTheTitleOfTheFirstResult()
    {
        web.WaitUntil(() => foods.FirstResultTitle() is { Length: > 0 }, "no result to note");
        noted = foods.FirstResultTitle();
    }

    [Given(@"^I have noted the product's title on Scan$")]
    [Then(@"^I have noted the product's title on Scan$")]
    public void GivenIHaveNotedTheProductsTitleOnScan() => noted = scan.ProductTitle();

    [Then(@"^the food's values are headed with the noted title$")]
    public void ThenTheFoodsValuesAreHeadedWithTheNotedTitle() => web.Eventually(foods.DetailName, Is.EqualTo(noted));

    [Then(@"^Serving is headed with the noted title$")]
    public void ThenServingIsHeadedWithTheNotedTitle() => web.Eventually(serving.Title, Is.EqualTo(noted));

    [Then(@"^the day has an entry headed with the noted title$")]
    public void ThenTheDayHasAnEntryHeadedWithTheNotedTitle()
    {
        web.WaitUntil(() => web.Exists("dashboard-ring"), "the Dashboard didn't show the day");
        web.Eventually(
            () => new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }.SelectMany(dashboard.Entries).Any(row => row.StartsWith(noted, StringComparison.Ordinal)),
            Is.True,
            "a diary row that starts with the noted title");
    }
}
