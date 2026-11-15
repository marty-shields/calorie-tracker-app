using CalorieTracker.Core.Data;
using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Screens;

public class FoodSearchRouteTests
{
    [Test]
    public void Url_KeepsTheQueryAndType_EscapedAndAllForNull()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FoodSearchRoute.Url("oat", FoodType.Food), Is.EqualTo("/foods?q=oat&type=Food"));
            Assert.That(FoodSearchRoute.Url("a&b c", null), Is.EqualTo("/foods?q=a%26b%20c&type=All"));
            Assert.That(FoodSearchRoute.Url("", FoodType.Drink), Is.EqualTo("/foods?type=Drink"));
        });
    }

    [Test]
    public void AddUrl_NamesTheMealAndOptionallyTheDay()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FoodSearchRoute.AddUrl(Meal.Lunch), Is.EqualTo("/foods?meal=Lunch"));
            Assert.That(FoodSearchRoute.AddUrl(Meal.Snacks, new DateOnly(2031, 7, 4)), Is.EqualTo("/foods?meal=Snacks&date=2031-07-04"));
            Assert.That(FoodSearchRoute.AddUrl(Meal.Lunch, null, "a&b", FoodType.Drink), Is.EqualTo("/foods?meal=Lunch&q=a%26b&type=Drink"));
            Assert.That(FoodSearchRoute.AddUrl(Meal.Lunch, new DateOnly(2031, 7, 4), "oat", null), Is.EqualTo("/foods?meal=Lunch&date=2031-07-04&q=oat&type=All"));
            Assert.That(FoodSearchRoute.AddUrl(Meal.Lunch, null, "", FoodType.Food), Is.EqualTo("/foods?meal=Lunch&type=Food"));
        });
    }

    [TestCase("Ingredient", FoodType.Ingredient)]
    [TestCase("food", FoodType.Food)]
    [TestCase("Drink", FoodType.Drink)]
    [TestCase("All", null)]
    [TestCase("all", null)]
    [TestCase(null, FoodType.Ingredient)]
    [TestCase("", FoodType.Ingredient)]
    [TestCase("Dish", FoodType.Ingredient)]
    [TestCase("7", FoodType.Ingredient)]
    [TestCase(" 1", FoodType.Ingredient)]
    [TestCase("Food,Drink", FoodType.Ingredient)]
    public void ParseFilter_GivesTheTypeAllOrIngredients(string? text, FoodType? expected) =>
        Assert.That(FoodSearchRoute.ParseFilter(text), Is.EqualTo(expected));

    [Test]
    public void TheFoodsPage_ComesBackToTheSearchFromServingInAddMode()
    {
        var page = RepoFiles.Read("src", "CalorieTracker.App", "Components", "Pages", "Foods.razor");

        Assert.That(page, Does.Contain("FoodSearchRoute.AddUrl(preset, date, request.Query, request.Filter)"));
    }

    [Test]
    public void DashboardRoute_OpensTheDay() =>
        Assert.That(DashboardRoute.Url(new DateOnly(2031, 7, 4)), Is.EqualTo("/?date=2031-07-04"));

    [TestCase("2031-07-04", "2031-07-04")]
    [TestCase("2031-7-4", null)]
    [TestCase("04/07/2031", null)]
    [TestCase("2031-02-30", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void DashboardRoute_ParsesTheDateQueryOrOpensToday(string? text, string? expected) =>
        Assert.That(DashboardRoute.ParseDate(text)?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(expected));
}
