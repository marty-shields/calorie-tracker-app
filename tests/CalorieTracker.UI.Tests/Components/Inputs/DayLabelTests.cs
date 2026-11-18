using CalorieTracker.UI.Components.Inputs;

namespace CalorieTracker.UI.Tests.Components.Inputs;

public class DayLabelTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [TestCase(0, "Today, 18 Nov")]
    [TestCase(-1, "Yesterday, 17 Nov")]
    [TestCase(1, "Tomorrow, 19 Nov")]
    [TestCase(-4, "Sat, 14 Nov")]
    public void For_NamesTheDayRelativeToToday(int offset, string expected) =>
        Assert.That(DayLabel.For(Today.AddDays(offset), Today), Is.EqualTo(expected));

    [Test]
    public void For_AnotherYear_AddsTheYear() =>
        Assert.That(DayLabel.For(new DateOnly(2025, 3, 5), Today), Is.EqualTo("Wed, 5 Mar 2025"));
}
