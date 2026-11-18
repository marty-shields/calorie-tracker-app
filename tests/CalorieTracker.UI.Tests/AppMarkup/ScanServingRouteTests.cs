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

        Assert.That(scan, Does.Contain("ServingRoute.Url(add.FoodId, meal, date, backTo: date is { } day ? DashboardRoute.Url(day) : \"/\", amount: add.LastAmount, servings: add.LastServings, servingId: add.LastServingId)"));
    }

    [Test]
    public void Scan_ReadsTheMealAndDayFromTheRoute()
    {
        var scan = AppPage("Scan.razor");

        Assert.Multiple(() =>
        {
            Assert.That(scan, Does.Contain("<ScanPage Meal=\"meal\""));
            Assert.That(scan, Does.Contain("OnBack=\"@(() => Navigation.GoTo(date is { } day ? DashboardRoute.Url(day) : string.Empty))\""));
            Assert.That(scan, Does.Contain("SupplyParameterFromQuery(Name = \"meal\")"));
            Assert.That(scan, Does.Contain("SupplyParameterFromQuery(Name = \"date\")"));
        });
    }

    [Test]
    public void Home_RoutesTheAddSheetToSearchAndScanWithTheMealAndDay()
    {
        var home = AppPage("Home.razor");

        Assert.Multiple(() =>
        {
            Assert.That(home, Does.Contain("OnAdd=\"@(request => Navigation.GoTo(FoodSearchRoute.AddUrl(request.Meal, request.Date)))\""));
            Assert.That(home, Does.Not.Contain("OnSearch="));
            Assert.That(home, Does.Contain("OnScan=\"@(request => Navigation.GoTo(ScanRoute.AddUrl(request.Meal, request.Date)))\""));
        });
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
    public void Scan_RoutesSearchByNameToFoodsWithTheMealAndDay()
    {
        var scan = AppPage("Scan.razor");
        var scanPage = RepoFiles.Read("src", "CalorieTracker.UI", "Screens", "ScanPage.razor.cs");

        Assert.Multiple(() =>
        {
            Assert.That(scanPage, Does.Not.Contain("OnNavigate"));
            Assert.That(scan, Does.Contain("OnSearchByName=\"@(() => Navigation.GoTo(meal is { } preset ? FoodSearchRoute.AddUrl(preset, date) : FoodSearchRoute.Template))\""));
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
