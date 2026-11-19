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
    [TestCase("-2d")]
    [TestCase("-2d 12:00 extra")]
    [TestCase("-2d  12:00")]
    public void TryParse_RejectsAnythingElse(string text) =>
        Assert.That(E2EClockPin.TryParse(text, out _, out _), Is.False);
}
#endif
