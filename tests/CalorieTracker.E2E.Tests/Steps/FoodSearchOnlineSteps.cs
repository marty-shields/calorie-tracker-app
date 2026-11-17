using CalorieTracker.E2E.Tests.Screens;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed class FoodSearchOnlineSteps(FoodSearchScreen foods, ServingScreen serving, WebView web)
{
    [Given(@"^Open Food Facts search is answered by the test seam$")]
    public void GivenTheSeamAnswers() =>
        AppiumSetup.Adb.WriteText($"{AppiumSetup.App.ExternalFilesFolder}/{SeamFileName}", "on");

    /// <summary>The file <c>E2EOnlineSearchSeam</c> looks for; <c>ScanSeamValuesTests</c> checks it matches.</summary>
    private const string SeamFileName = "e2e-online-search.txt";

    [Then(@"^Search Open Food Facts is offered under the results$")]
    public void ThenTheRowIsOffered() =>
        web.Eventually(foods.OnlineEntry, Is.EqualTo("row"), "Search Open Food Facts as a row");

    [Then(@"^Search Open Food Facts is offered as a button$")]
    public void ThenTheButtonIsOffered() =>
        web.Eventually(foods.OnlineEntry, Is.EqualTo("button"), "Search Open Food Facts as a button");

    [Then(@"^the first-use notice reads ""(.*)""$")]
    public void ThenTheNoticeReads(string text) => web.Eventually(foods.OnlineNotice, Is.EqualTo(text));

    [Then(@"^(?:the first-use notice is gone|no first-use notice shows)$")]
    public void ThenNoNotice() => web.Eventually(foods.OnlineNotice, Is.Null, "no first-use notice");

    [Then(@"^no Open Food Facts group shows$")]
    public void ThenNoOnlineGroup() => Assert.That(foods.Groups(), Does.Not.Contain("online"));

    [Given(@"^I have tapped Search Open Food Facts$")]
    [When(@"^I tap Search Open Food Facts$")]
    public void WhenITapSearchOpenFoodFacts() => foods.SearchOnline();

    [Then(@"^the Open Food Facts group is headed ""(.*)""$")]
    public void ThenTheGroupIsHeaded(string heading) => web.Eventually(foods.OnlineHeading, Is.EqualTo(heading).IgnoreCase, "the heading, which shows in capitals");

    [Then(@"^the Open Food Facts group lists (\d+) products, each marked NEW$")]
    public void ThenTheGroupLists(int count) =>
        web.Eventually(foods.OnlineRows, Has.Count.EqualTo(count).And.All.EndsWith("NEW"));

    [When(@"^I open the Open Food Facts product ""(.*)"" of ""(.*)""$")]
    public void WhenIOpenTheProduct(string title, string pack) => foods.OpenOnlineProduct(title, pack);

    [Then(@"^Open Food Facts says ""(.*)"" and offers Try again$")]
    public void ThenOnlineSays(string message)
    {
        web.Eventually(foods.OnlineMessage, Is.EqualTo(message));
        Assert.That(foods.OnlineHasTryAgain(), Is.True, "Try again");
    }

    [Then(@"^Open Food Facts says nothing was found$")]
    public void ThenNothingFound() => web.Eventually(foods.OnlineNothingFound, Is.EqualTo("Nothing found on Open Food Facts"));

    [Then(@"^This adds names ""(.*)""$")]
    public void ThenThisAddsNames(string text) => web.Eventually(serving.AddsDetail, Is.EqualTo(text));

    [Then(@"^the local results still show$")]
    public void ThenTheLocalResultsStillShow() => web.Eventually(foods.ResultRows, Has.Count.GreaterThan(0));
}
