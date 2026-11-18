using CalorieTracker.Core.Data;
using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Screens;

public class ServingRouteTests
{
    private static readonly DateOnly Today = new(2031, 7, 19);

    [Test]
    public void Url_BuildsTheRouteWithOnlyTheQueryGiven()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRoute.Url(5), Is.EqualTo("/serving/5"));
            Assert.That(ServingRoute.Url(5, Meal.Dinner), Is.EqualTo("/serving/5?meal=Dinner"));
            Assert.That(ServingRoute.Url(5, Meal.Lunch, backTo: "/foods?meal=Lunch"), Is.EqualTo("/serving/5?meal=Lunch&back=%2Ffoods%3Fmeal%3DLunch"));
            Assert.That(ServingRoute.Url(5, Meal.Snacks, new DateOnly(2031, 7, 4)), Is.EqualTo("/serving/5?meal=Snacks&date=2031-07-04"));
        });
    }

    [Test]
    public void Url_WithAnAmount_AddsItAndTheServingsOnlyWithIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRoute.Url(5, Meal.Lunch, amount: 150), Is.EqualTo("/serving/5?meal=Lunch&amount=150"));
            Assert.That(ServingRoute.Url(5, amount: 80.5, servings: 2), Is.EqualTo("/serving/5?amount=80.5&servings=2"));
            Assert.That(ServingRoute.Url(5, servings: 2), Is.EqualTo("/serving/5"));
        });
    }

    [Test]
    public void ParseNumber_ReadsAPositiveNumber() =>
        Assert.That(ServingRoute.ParseNumber("1000"), Is.EqualTo(1000));

    [Test]
    public void Url_WithBackTo_AddsBackAlone() =>
        Assert.That(ServingRoute.Url(5, backTo: "/"), Is.EqualTo("/serving/5?back=%2F"));

    [TestCase("Lunch", Meal.Lunch)]
    [TestCase("lunch", Meal.Lunch)]
    [TestCase("Snacks", Meal.Snacks)]
    public void ParseMeal_AFourMealName_GivesIt(string text, Meal expected) =>
        Assert.That(ServingRoute.ParseMeal(text), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Brunch")]
    [TestCase("1")]
    [TestCase("9")]
    [TestCase("Lunch,Dinner")]
    public void ParseMeal_Anything_Else_GivesNull(string? text) =>
        Assert.That(ServingRoute.ParseMeal(text), Is.Null);

    [Test]
    public void ParseDate_AWellFormedDay_GivesIt() =>
        Assert.That(ServingRoute.ParseDate("2031-07-04", Today), Is.EqualTo(new DateOnly(2031, 7, 4)));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("4 July")]
    [TestCase("2031-13-01")]
    [TestCase("04/07/2031")]
    public void ParseDate_Anything_Else_GivesToday(string? text) =>
        Assert.That(ServingRoute.ParseDate(text, Today), Is.EqualTo(Today));

    [TestCase("/foods", "/foods")]
    [TestCase("/", "/")]
    [TestCase(null, "/fallback")]
    [TestCase("", "/fallback")]
    [TestCase("foods", "/fallback")]
    [TestCase("//evil.example", "/fallback")]
    [TestCase("https://evil.example", "/fallback")]
    [TestCase("/\\evil.example", "/fallback")]
    [TestCase("/\t/evil.example", "/fallback")]
    [TestCase("/\r\nevil", "/fallback")]
    public void ReturnOr_OnlyAnInAppPathIsKept(string? returnTo, string expected) =>
        Assert.That(ServingRoute.ReturnOr(returnTo, "/fallback"), Is.EqualTo(expected));

    [TestCase("/back", "/back")]
    [TestCase("/", "/")]
    [TestCase("//evil.example", "/foods")]
    [TestCase("https://evil.example", "/foods")]
    [TestCase(null, "/foods")]
    public void BackTarget_IsTheInAppBackRouteOrTheDefault(string? backTo, string expected) =>
        Assert.That(ServingRoute.BackTarget(backTo), Is.EqualTo(expected));

    [Test]
    public void AfterAddTarget_IsThatDayOnTheDashboardWithTheMealOpen()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRoute.AfterAddTarget(null, Meal.Lunch), Is.EqualTo("/?open=Lunch"));
            Assert.That(ServingRoute.AfterAddTarget(new DateOnly(2031, 7, 4), Meal.Dinner), Is.EqualTo("/?date=2031-07-04&open=Dinner"));
        });
    }

    [Test]
    public void TheAppsRoutePage_UsesTheTemplateAndReadsTheQuery()
    {
        var page = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Serving.razor");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain($"@page \"{ServingRoute.Template}\""));
            Assert.That(page, Does.Contain("Name = \"meal\"").And.Contain("Name = \"date\"").And.Contain("Name = \"back\""));
            Assert.That(page, Does.Contain("ServingRoute.BackTarget(BackTo)").And.Not.Contain("ReturnTo"));
        });
    }

    [Test]
    public void EditUrl_CarriesTheEntryItsDayMealAndAmounts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRoute.EditUrl(7, 23, Meal.Lunch, new DateOnly(2031, 7, 15), 30), Is.EqualTo("/serving/23?entry=7&meal=Lunch&date=2031-07-15&amount=30"));
            Assert.That(ServingRoute.EditUrl(8, 22, Meal.Dinner, new DateOnly(2031, 7, 15), 150.5, 0.75), Is.EqualTo("/serving/22?entry=8&meal=Dinner&date=2031-07-15&amount=150.5&servings=0.75"));
        });
    }

    [TestCase(0.74)]
    [TestCase(201.8)]
    [TestCase(0.1)]
    [TestCase(10000)]
    public void EditUrl_AmountsRoundTripThroughParseNumber(double value)
    {
        var url = ServingRoute.EditUrl(1, 2, Meal.Lunch, Today, value, value);
        var query = System.Web.HttpUtility.ParseQueryString(url[(url.IndexOf('?') + 1)..]);

        Assert.Multiple(() =>
        {
            Assert.That(ServingRoute.ParseNumber(query["amount"]), Is.EqualTo(value));
            Assert.That(ServingRoute.ParseNumber(query["servings"]), Is.EqualTo(value));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("abc")]
    [TestCase("0")]
    [TestCase("-3")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("1e999")]
    [TestCase("30,5")]
    public void ParseNumber_NotAPositiveFiniteNumber_GivesNull(string? text) =>
        Assert.That(ServingRoute.ParseNumber(text), Is.Null);

    [Test]
    public void TheAppsPages_WireEditThroughTheEditRouteAndTheDashboard()
    {
        var serving = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Serving.razor");
        var home = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Home.razor");

        Assert.Multiple(() =>
        {
            Assert.That(serving, Does.Contain("Name = \"entry\"").And.Contain("Name = \"amount\"").And.Contain("Name = \"servings\""));
            Assert.That(serving, Does.Contain("EntryId=\"EntryId\"").And.Contain("OnSaved=").And.Contain("OnRemoved=").And.Contain("DashboardRoute.Url(EntryDay)"));
            Assert.That(home, Does.Contain("ServingRoute.EditUrl(request.Entry.EntryId, request.Entry.FoodId, request.Meal, request.Date, request.Entry.Amount, request.Entry.Servings, request.Entry.ServingId)"));
        });
    }
}
