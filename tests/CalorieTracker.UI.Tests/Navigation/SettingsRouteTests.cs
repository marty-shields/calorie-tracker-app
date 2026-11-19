using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Navigation;

public class SettingsRouteTests
{
    [Test]
    public void Url_CarriesAnEscapedOpener() =>
        Assert.That(SettingsRoute.Url("/trends?period=month&from=2026-10-01"), Is.EqualTo("/settings?back=%2Ftrends%3Fperiod%3Dmonth%26from%3D2026-10-01"));

    [TestCase("/trends?period=month&from=2026-10-01", "/trends?period=month&from=2026-10-01")]
    [TestCase("/?date=2026-11-12", "/?date=2026-11-12")]
    [TestCase(null, "/")]
    [TestCase("", "/")]
    [TestCase("//evil.example", "/")]
    [TestCase("https://evil.example", "/")]
    [TestCase("/a\\b", "/")]
    public void BackTarget_IsTheInAppOpenerOrToday(string? back, string expected) =>
        Assert.That(SettingsRoute.BackTarget(back), Is.EqualTo(expected));

    [Test]
    public void Url_RoundTripsThroughBackTarget() =>
        Assert.That(SettingsRoute.BackTarget(Uri.UnescapeDataString(Uri.EscapeDataString("/trends?period=year&from=2026-01-01"))), Is.EqualTo("/trends?period=year&from=2026-01-01"));
}
