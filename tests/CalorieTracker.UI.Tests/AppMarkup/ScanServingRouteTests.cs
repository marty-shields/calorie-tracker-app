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

        Assert.That(scan, Does.Contain("OnAddToDiary=\"@(foodId => Navigation.GoTo(ServingRoute.Url(foodId, backTo: \"/\")))\""));
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
