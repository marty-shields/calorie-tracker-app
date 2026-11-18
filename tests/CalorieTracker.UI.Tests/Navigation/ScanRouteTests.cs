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
}
