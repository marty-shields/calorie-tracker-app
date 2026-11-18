using CalorieTracker.Core.Data;
using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Navigation;

public class ScanRouteTests
{
    [Test]
    public void AddUrl_CarriesTheMealAndTheDay() =>
        Assert.That(ScanRoute.AddUrl(Meal.Lunch, new DateOnly(2026, 11, 12)), Is.EqualTo("/scan?meal=Lunch&date=2026-11-12"));

    [Test]
    public void AddUrl_WithoutADay_CarriesTheMealOnly() =>
        Assert.That(ScanRoute.AddUrl(Meal.Snacks), Is.EqualTo("/scan?meal=Snacks"));

    [Test]
    public void AddUrl_CarriesAnEscapedBackRoute() =>
        Assert.That(ScanRoute.AddUrl(Meal.Dinner, new DateOnly(2026, 11, 12), "/foods?meal=Dinner&q=a b"), Is.EqualTo("/scan?meal=Dinner&date=2026-11-12&back=%2Ffoods%3Fmeal%3DDinner%26q%3Da%20b"));

    [TestCase("/foods?meal=Dinner", "2026-11-12", "/foods?meal=Dinner")]
    [TestCase(null, "2026-11-12", "/?date=2026-11-12")]
    [TestCase(null, null, "/")]
    [TestCase("//evil.example", null, "/")]
    [TestCase("https://evil.example", "2026-11-12", "/?date=2026-11-12")]
    public void BackTarget_IsTheInAppBackRouteOrTheDay(string? back, string? day, string expected) =>
        Assert.That(ScanRoute.BackTarget(back, day is null ? null : DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture)), Is.EqualTo(expected));
}
