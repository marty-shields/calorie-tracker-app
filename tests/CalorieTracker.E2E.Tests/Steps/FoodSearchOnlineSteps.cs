using CalorieTracker.E2E.Tests.Screens;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class FoodSearchOnlineSteps(FoodSearchScreen foods, ServingScreen serving, WebView web)
{
    [Given(@"^the online search is answered by the test seam$")]
    public void GivenTheSeamAnswers() =>
        AppiumSetup.Adb.WriteText($"{AppiumSetup.App.ExternalFilesFolder}/{SeamFileName}", "on");

    /// <summary>The file <c>E2EOnlineSearchSeam</c> looks for; <c>ScanSeamValuesTests</c> checks it matches.</summary>
    private const string SeamFileName = "e2e-online-search.txt";

    [Then(@"^Search online is offered as a button pinned in the bottom bar$")]
    public void ThenTheButtonIsOffered() =>
        web.Eventually(foods.OnlineEntry, Is.EqualTo("secondary"), "Search online as a secondary button");

    [Then(@"^Search online is offered as the main button$")]
    public void ThenTheMainButtonIsOffered() =>
        web.Eventually(foods.OnlineEntry, Is.EqualTo("primary"), "Search online as the main button");

    [Then(@"^the first-use notice reads ""(.*)""$")]
    public void ThenTheNoticeReads(string text) => web.Eventually(foods.OnlineNotice, Is.EqualTo(text));

    [Then(@"^(?:the first-use notice is gone|no first-use notice shows)$")]
    public void ThenNoNotice() => web.Eventually(foods.OnlineNotice, Is.Null, "no first-use notice");

    [Then(@"^no online group shows$")]
    public void ThenNoOnlineGroup() => Assert.That(foods.Groups(), Does.Not.Contain("online"));

    [Given(@"^I have tapped Search online$")]
    [When(@"^I tap Search online$")]
    public void WhenITapSearchOpenFoodFacts() => foods.SearchOnline();

    [Then(@"^the online group is headed ""(.*)""$")]
    public void ThenTheGroupIsHeaded(string heading) => web.Eventually(foods.OnlineHeading, Is.EqualTo(heading).IgnoreCase, "the heading, which shows in capitals");

    [Then(@"^the online group lists (\d+) products, each marked NEW$")]
    public void ThenTheGroupLists(int count) =>
        web.Eventually(foods.OnlineRows, Has.Count.EqualTo(count).And.All.EndsWith("NEW"));

    [When(@"^I open the online product ""(.*)"" of ""(.*)""$")]
    public void WhenIOpenTheProduct(string title, string pack) => foods.OpenOnlineProduct(title, pack);

    [Then(@"^the online search says ""(.*)"" and offers Try again$")]
    public void ThenOnlineSays(string message)
    {
        web.Eventually(foods.OnlineMessage, Is.EqualTo(message));
        Assert.That(foods.OnlineHasTryAgain(), Is.True, "Try again");
    }

    [Then(@"^the online search says nothing was found$")]
    public void ThenNothingFound() => web.Eventually(foods.OnlineNothingFound, Is.EqualTo("Nothing found online"));

    [Then(@"^Serving opens in (g|ml) with no servings offered$")]
    public void ThenServingOpensInUnitWithNoServings(string unit)
    {
        web.Eventually(serving.Unit, Is.EqualTo(unit));
        Assert.That(serving.HasModeSwitch(), Is.False, "the Grams and Servings switch");
        Assert.That(serving.ServingOptions(), Is.Empty, "the servings list");
    }

    [Then(@"^Serving is headed ""(.*)""$")]
    public void ThenServingIsHeaded(string title) => web.Eventually(serving.Title, Is.EqualTo(title));

    [Then(@"^Serving has no list of servings$")]
    public void ThenServingHasNoList() => web.Eventually(serving.ServingOptions, Is.Empty);

    [Then(@"^the local results still show$")]
    public void ThenTheLocalResultsStillShow() => web.Eventually(foods.ResultRows, Has.Count.GreaterThan(0));
}
