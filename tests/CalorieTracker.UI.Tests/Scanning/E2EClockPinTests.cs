#if DEBUG
using CalorieTracker.App.Diagnostics;

namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The App's Debug-only clock seam file format; the App's source file is compiled into this project because it can't be referenced.</summary>
public class E2EClockPinTests
{
    [TestCase("19:30", 0, 19, 30)]
    [TestCase("  07:05\n", 0, 7, 5)]
    [TestCase("-2d 12:00", -2, 12, 0)]
    [TestCase("+1d 00:15", 1, 0, 15)]
    [TestCase("-0d 09:00", 0, 9, 0)]
    public void TryParse_ReadsATimeWithAnOptionalDayOffset(string text, int days, int hour, int minute)
    {
        Assert.That(E2EClockPin.TryParse(text, out var parsedDays, out var time), Is.True);
        Assert.That((parsedDays, time), Is.EqualTo((days, new TimeOnly(hour, minute))));
    }

    [TestCase("")]
    [TestCase("noon")]
    [TestCase("25:00")]
    [TestCase("2d 12:00")]
    [TestCase("-d 12:00")]
    [TestCase("-2 12:00")]
    [TestCase("12d 12:00")]
    [TestCase("-12 12:00")]
    [TestCase("-2x 12:00")]
    [TestCase("-2d")]
    [TestCase("-2d 12:00 extra")]
    [TestCase("-2d  12:00")]
    public void TryParse_RejectsAnythingElse(string text) =>
        Assert.That(E2EClockPin.TryParse(text, out _, out _), Is.False);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(1), "Test", "Test", "Test DST",
    [
        TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 10),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 20)),
    ]);

    [Test]
    public void OffsetFrom_PinsATimeOfDayOnTheZonesDate()
    {
        var now = new DateTimeOffset(2026, 1, 15, 8, 0, 0, TimeSpan.FromHours(1));

        var offset = E2EClockPin.OffsetFrom(now, Zone, 0, new TimeOnly(19, 30));

        Assert.That(now + offset, Is.EqualTo(new DateTimeOffset(2026, 1, 15, 19, 30, 0, TimeSpan.FromHours(1))));
    }

    [Test]
    public void OffsetFrom_MovesBackWholeDaysFromTheZonesDateNotUtcs()
    {
        // 23:30 UTC is already 00:30 the next day in the zone.
        var now = new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero);

        var offset = E2EClockPin.OffsetFrom(now, Zone, -2, new TimeOnly(12, 0));

        Assert.That(now + offset, Is.EqualTo(new DateTimeOffset(2026, 1, 14, 11, 0, 0, TimeSpan.Zero)));
        Assert.That(offset, Is.LessThan(TimeSpan.Zero));
    }

    [Test]
    public void OffsetFrom_UsesTheZonesOffsetAtThePinnedMomentAcrossADstChange()
    {
        // Daylight time (UTC+2) is in force on 12 Mar 2026 at 09:00; 10 days earlier the zone is on standard time (UTC+1).
        var now = new DateTimeOffset(2026, 3, 12, 9, 0, 0, TimeSpan.FromHours(2));

        var offset = E2EClockPin.OffsetFrom(now, Zone, -10, new TimeOnly(12, 0));

        Assert.That(now + offset, Is.EqualTo(new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.FromHours(1))));
        Assert.That(offset, Is.EqualTo(new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.FromHours(1)) - now));
    }
}
#endif
