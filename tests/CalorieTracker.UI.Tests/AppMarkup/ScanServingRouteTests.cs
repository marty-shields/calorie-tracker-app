using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>The App can't be referenced, so its Scan and Serving pages are read to check how they route.</summary>
public partial class ScanServingRouteTests
{
    private static string AppPage(string name) => RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", name);

    [Test]
    public void Scan_RoutesAddToDiaryToTheServingScreenWithBackToTheStartScreen()
    {
        var scan = AppPage("Scan.razor");

        Assert.That(scan, Does.Contain("OnAddToDiary=\"@(add => Navigation.GoTo(ServingRoute.Url(add.FoodId, backTo: \"/\", amount: add.LastAmount, servings: add.LastServings, servingId: add.LastServingId)))\""));
    }

    [Test]
    public void Foods_CarriesNoPackToTheServingScreen_InEitherMode()
    {
        var foods = AppPage("Foods.razor");

        Assert.Multiple(() =>
        {
            Assert.That(Regex.Matches(foods, @"servingId: request\.LastServingId\)"), Has.Count.EqualTo(2));
            Assert.That(foods, Does.Not.Contain("pack:"));
        });
    }

    [Test]
    public void Scan_RoutesSearchByNameToFoods()
    {
        var scan = AppPage("Scan.razor");
        var scanPage = RepoFiles.Read("src", "CalorieTracker.UI", "Screens", "ScanPage.razor.cs");

        Assert.Multiple(() =>
        {
            Assert.That(scanPage, Does.Contain("SearchByNameAsync() => OnNavigate.InvokeAsync(NavDestination.Foods)"));
            Assert.That(scan, Does.Contain("OnNavigate=\"@(destination => Navigation.GoTo(destination))\""));
        });
    }

    [Test]
    public void Serving_PassesTheBackQueryValueToBackTarget()
    {
        var serving = AppPage("Serving.razor");

        Assert.Multiple(() =>
        {
            Assert.That(serving, Does.Match(BackQueryParameter()));
            Assert.That(serving, Does.Contain("ServingRoute.BackTarget(BackTo, ReturnTo)"));
        });
    }

    [GeneratedRegex(@"SupplyParameterFromQuery\(Name = ""back""\)\]\s*public string\? BackTo")]
    private static partial Regex BackQueryParameter();
}
