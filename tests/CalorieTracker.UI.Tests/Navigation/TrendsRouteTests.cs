using CalorieTracker.UI.Navigation;

namespace CalorieTracker.UI.Tests.Navigation;

public class TrendsRouteTests
{
    [TestCase(TrendsPeriod.Week, "2026-11-12", "/trends?period=week&from=2026-11-12")]
    [TestCase(TrendsPeriod.Month, "2026-03-01", "/trends?period=month&from=2026-03-01")]
    [TestCase(TrendsPeriod.Year, "2026-01-01", "/trends?period=year&from=2026-01-01")]
    public void Url_NamesThePeriodAndTheDay(TrendsPeriod period, string day, string expected) =>
        Assert.That(TrendsRoute.Url(period, DateOnly.Parse(day)), Is.EqualTo(expected));

    [TestCase("week", TrendsPeriod.Week)]
    [TestCase("MONTH", TrendsPeriod.Month)]
    [TestCase("year", TrendsPeriod.Year)]
    [TestCase(null, TrendsPeriod.Week)]
    [TestCase("", TrendsPeriod.Week)]
    [TestCase("decade", TrendsPeriod.Week)]
    public void ParsePeriod_FallsBackToTheWeek(string? text, TrendsPeriod expected) =>
        Assert.That(TrendsRoute.ParsePeriod(text), Is.EqualTo(expected));

    [TestCase(TrendsPeriod.Week, "2026-11-15", "2026-11-09", "2026-11-15")]
    [TestCase(TrendsPeriod.Week, "2026-11-16", "2026-11-16", "2026-11-22")]
    [TestCase(TrendsPeriod.Week, "2027-01-01", "2026-12-28", "2027-01-03")]
    [TestCase(TrendsPeriod.Month, "2026-02-17", "2026-02-01", "2026-02-28")]
    [TestCase(TrendsPeriod.Month, "2028-02-17", "2028-02-01", "2028-02-29")]
    [TestCase(TrendsPeriod.Year, "2026-11-18", "2026-01-01", "2026-12-31")]
    public void StartAndEnd_AreTheMondayMonthOrYearBounds(TrendsPeriod period, string day, string start, string end)
    {
        var first = TrendsRoute.Start(period, DateOnly.Parse(day));

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(DateOnly.Parse(start)));
            Assert.That(TrendsRoute.End(period, first), Is.EqualTo(DateOnly.Parse(end)));
        });
    }

    [TestCase(TrendsPeriod.Week, "2026-11-09", 1, "2026-11-16")]
    [TestCase(TrendsPeriod.Week, "2026-11-09", -1, "2026-11-02")]
    [TestCase(TrendsPeriod.Month, "2026-01-01", -1, "2025-12-01")]
    [TestCase(TrendsPeriod.Month, "2026-12-01", 1, "2027-01-01")]
    [TestCase(TrendsPeriod.Year, "2026-01-01", -1, "2025-01-01")]
    public void Step_MovesAWeekMonthOrYear(TrendsPeriod period, string start, int direction, string expected) =>
        Assert.That(TrendsRoute.Step(period, DateOnly.Parse(start), direction), Is.EqualTo(DateOnly.Parse(expected)));

    [TestCase(TrendsPeriod.Week, "2026-11-09", "2026-11-16", false, TestName = "Monday today, the week before")]
    [TestCase(TrendsPeriod.Week, "2026-11-09", "2026-11-15", true, TestName = "the week that ends today")]
    [TestCase(TrendsPeriod.Week, "2026-11-02", "2026-11-09", false, TestName = "a Monday, the week before it is past")]
    [TestCase(TrendsPeriod.Month, "2026-11-01", "2026-11-30", true, TestName = "last day of the month")]
    [TestCase(TrendsPeriod.Month, "2026-10-01", "2026-11-01", false, TestName = "first of the next month")]
    [TestCase(TrendsPeriod.Year, "2026-01-01", "2026-12-31", true, TestName = "last day of the year")]
    public void IsCurrent_IsTrueUntilTheDayAfterThePeriodEnds(TrendsPeriod period, string start, string today, bool expected) =>
        Assert.That(TrendsRoute.IsCurrent(period, DateOnly.Parse(start), DateOnly.Parse(today)), Is.EqualTo(expected));

    [TestCase(TrendsPeriod.Week, "2026-11-09", "9 – 15 Nov")]
    [TestCase(TrendsPeriod.Week, "2026-11-30", "30 Nov – 6 Dec")]
    [TestCase(TrendsPeriod.Week, "2026-12-28", "28 Dec 2026 – 3 Jan 2027")]
    [TestCase(TrendsPeriod.Month, "2026-11-01", "November 2026")]
    [TestCase(TrendsPeriod.Year, "2026-01-01", "2026")]
    public void Label_ReadsAsTheDesignShowsIt(TrendsPeriod period, string start, string expected) =>
        Assert.That(TrendsRoute.Label(period, DateOnly.Parse(start)), Is.EqualTo(expected));

    [TestCase("0001-01-01", "2000-01-01")]
    [TestCase("1999-12-31", "2000-01-01")]
    [TestCase("2026-11-12", "2026-11-12")]
    [TestCase("2026-11-19", "2026-11-19")]
    [TestCase("2026-11-20", "2026-11-19")]
    [TestCase("9999-12-31", "2026-11-19")]
    public void ClampFrom_HoldsTheDayBetweenTheEarliestAndToday(string day, string expected) =>
        Assert.That(TrendsRoute.ClampFrom(DateOnly.Parse(day), new DateOnly(2026, 11, 19)), Is.EqualTo(DateOnly.Parse(expected)));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-date")]
    [TestCase("2026-13-45")]
    public void ClampFrom_KeepsAMalformedValueNull(string? text) =>
        Assert.That(TrendsRoute.ClampFrom(RouteDate.Parse(text), new DateOnly(2026, 11, 19)), Is.Null);

    [Test]
    public void ClampedFarValues_GiveSpansThePeriodMathsCanHandle()
    {
        foreach (var from in new[] { new DateOnly(1, 1, 1), DateOnly.MaxValue })
        {
            var day = TrendsRoute.ClampFrom(from, new DateOnly(2026, 11, 19))!.Value;
            foreach (var period in Enum.GetValues<TrendsPeriod>())
            {
                var start = TrendsRoute.Start(period, day);
                Assert.DoesNotThrow(() => TrendsRoute.Label(period, start));
                Assert.DoesNotThrow(() => TrendsRoute.Step(period, start, 1));
                Assert.DoesNotThrow(() => TrendsRoute.End(period, start));
            }
        }
    }
}
