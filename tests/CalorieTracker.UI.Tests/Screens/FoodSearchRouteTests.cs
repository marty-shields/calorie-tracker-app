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
    public void ParseFilter_GivesTheTypeAllOrIngredients(string? text, FoodType? expected) =>
        Assert.That(FoodSearchRoute.ParseFilter(text), Is.EqualTo(expected));

    [Test]
    public void DashboardRoute_OpensTheDay() =>
        Assert.That(DashboardRoute.Url(new DateOnly(2031, 7, 4)), Is.EqualTo("/?date=2031-07-04"));
}
