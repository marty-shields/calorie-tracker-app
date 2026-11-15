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
            Assert.That(ServingRoute.Url(5, Meal.Snacks, new DateOnly(2031, 7, 4), "/foods"), Is.EqualTo("/serving/5?meal=Snacks&date=2031-07-04&return=%2Ffoods"));
        });
    }

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

    [Test]
    public void TheAppsRoutePage_UsesTheTemplateAndReadsTheQuery()
    {
        var page = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Serving.razor");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain($"@page \"{ServingRoute.Template}\""));
            Assert.That(page, Does.Contain("Name = \"meal\"").And.Contain("Name = \"date\"").And.Contain("Name = \"return\""));
        });
    }
}
