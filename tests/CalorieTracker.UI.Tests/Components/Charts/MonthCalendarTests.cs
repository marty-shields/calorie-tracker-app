using AngleSharp.Dom;
using CalorieTracker.UI.Components.Charts;
using Microsoft.AspNetCore.Components;

namespace CalorieTracker.UI.Tests.Components.Charts;

public class MonthCalendarTests : ComponentTestBase
{
    // November 2026 starts on a Sunday and today is Wednesday the 18th.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private readonly List<DateOnly> selected = [];

    [SetUp]
    public void ClearSelection() => selected.Clear();

    private IRenderedComponent<MonthCalendar> Render(params CalendarDay[] days) =>
        Context.Render<MonthCalendar>(p => p
            .Add(x => x.Month, new DateOnly(2026, 11, 20)).Add(x => x.Days, days).Add(x => x.Target, 2000).Add(x => x.Today, Today)
            .Add(x => x.TestId, "cal").Add(x => x.OnSelect, EventCallback.Factory.Create<DateOnly>(this, selected.Add)));

    private static CalendarDay Day(int day, double? value) => new(new DateOnly(2026, 11, day), value);

    /// <summary>The cell's accessible name: its visually hidden text, as the number beside it is aria-hidden.</summary>
    private static string NameOf(IElement cell) => cell.QuerySelector(".visually-hidden")!.TextContent;

    private static string Selector(int day) => $"[data-testid=cal-day-2026-11-{day:00}]";

    [Test]
    public void Render_StartsOnMondayAndShowsEveryDay()
    {
        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".weekdays span").Select(s => s.TextContent), Is.EqualTo(new[] { "M", "T", "W", "T", "F", "S", "S" }));
            Assert.That(cut.FindAll("button.cell"), Has.Count.EqualTo(30));
            Assert.That(cut.FindAll(".days .blank"), Has.Count.EqualTo(6), "the 1st is a Sunday, the seventh column");
        });
    }

    [TestCase(2026, 6, 0)]
    [TestCase(2026, 7, 2)]
    [TestCase(2026, 11, 6)]
    public void Render_PadsTheFirstRowToTheFirstWeekday(int year, int month, int blanks)
    {
        var cut = Context.Render<MonthCalendar>(p => p.Add(x => x.Month, new DateOnly(year, month, 1)).Add(x => x.Today, Today));

        Assert.That(cut.FindAll(".days .blank"), Has.Count.EqualTo(blanks));
    }

    [TestCase(1000, "under", "Tue 3 Nov, 1,000 kcal, under target")]
    [TestCase(2000, "on", "Tue 3 Nov, 2,000 kcal, on target")]
    [TestCase(2500, "over", "Tue 3 Nov, 2,500 kcal, over target")]
    public void Render_GivesEachLoggedDayAStatusClassAndAccessibleName(double value, string statusClass, string name)
    {
        var cut = Render(Day(3, value));

        var cell = cut.Find(Selector(3));
        Assert.Multiple(() =>
        {
            Assert.That(cell.ClassList, Does.Contain(statusClass));
            Assert.That(NameOf(cell), Is.EqualTo(name));
        });
    }

    [Test]
    public void Render_OverDayHasAMarkerAndOthersDoNot()
    {
        var cut = Render(Day(3, 2500), Day(4, 2000), Day(5, 1000));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=cal-marker-2026-11-03]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll(".marker"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Render_PastDayWithoutData_IsNotLogged()
    {
        var cut = Render(Day(3, 2000), Day(4, null), Day(5, 0));

        Assert.Multiple(() =>
        {
            foreach (var day in new[] { 4, 5, 6 })
            {
                Assert.That(cut.Find(Selector(day)).ClassList, Does.Contain("none"));
                Assert.That(NameOf(cut.Find(Selector(day))), Does.EndWith(", not logged"));
            }
        });
    }

    [Test]
    public void Render_FutureDays_AreDisabledAndNotSelectable()
    {
        var cut = Render(Day(25, 2000));

        var future = cut.Find(Selector(19));
        future.Click();

        Assert.Multiple(() =>
        {
            Assert.That(future.HasAttribute("disabled"), Is.True);
            Assert.That(future.ClassList, Does.Contain("future"));
            Assert.That(NameOf(future), Is.EqualTo("Thu 19 Nov, future"));
            Assert.That(cut.Find(Selector(25)).ClassList, Does.Contain("future"), "data on a future day is ignored");
            Assert.That(selected, Is.Empty);
        });
    }

    [Test]
    public void Render_TodayIsOutlinedAndEnabled()
    {
        var cut = Render(Day(18, 1000));

        var today = cut.Find(Selector(18));
        Assert.Multiple(() =>
        {
            Assert.That(today.ClassList, Does.Contain("today"));
            Assert.That(today.ClassList, Does.Contain("under"));
            Assert.That(today.HasAttribute("disabled"), Is.False);
            Assert.That(NameOf(today), Is.EqualTo("Wed 18 Nov, today, 1,000 kcal, under target"));
            Assert.That(cut.FindAll(".today"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Render_TodayWithNothingLoggedYet_IsNotLoggedAndOutlined()
    {
        var cut = Render();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find(Selector(18)).ClassList, Is.SupersetOf(new[] { "none", "today" }));
            Assert.That(NameOf(cut.Find(Selector(18))), Is.EqualTo("Wed 18 Nov, today, not logged"));
        });
    }

    [Test]
    public void Render_EnabledDays_AreFocusableButtons()
    {
        var cut = Render(Day(3, 2000));

        var cells = cut.FindAll("button.cell").Where(c => !c.HasAttribute("disabled")).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(cells, Has.Count.EqualTo(18));
            Assert.That(cells.Select(c => c.GetAttribute("type")), Is.All.EqualTo("button"));
            Assert.That(cells.Select(c => c.GetAttribute("tabindex")), Is.All.Null, "a native button is in the tab order without a tabindex");
        });
    }

    [Test]
    public void Click_EnabledDay_RaisesOnSelectWithItsDate()
    {
        var cut = Render(Day(3, 2000));

        cut.Find(Selector(3)).Click();
        cut.Find(Selector(4)).Click();

        Assert.That(selected, Is.EqualTo(new[] { new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4) }));
    }

    [Test]
    public void Render_DayCarryingItsOwnTarget_IsJudgedAgainstIt()
    {
        var cut = Render(new CalendarDay(new DateOnly(2026, 11, 3), 2500, 2400));

        Assert.That(cut.Find(Selector(3)).ClassList, Does.Contain("on"));
    }

    [Test]
    public void Render_WithoutATarget_ShowsLoggedDaysWithoutStatus()
    {
        var cut = Context.Render<MonthCalendar>(p => p
            .Add(x => x.Month, new DateOnly(2026, 11, 1)).Add(x => x.Days, [Day(3, 2500)]).Add(x => x.Today, Today).Add(x => x.TestId, "cal"));

        Assert.Multiple(() =>
        {
            Assert.That(NameOf(cut.Find(Selector(3))), Is.EqualTo("Tue 3 Nov, 2,500 kcal"));
            Assert.That(cut.FindAll(".marker"), Is.Empty);
        });
    }

    [Test]
    public void Render_WithoutToday_UsesTheClock()
    {
        var cut = Context.Render<MonthCalendar>(p => p.Add(x => x.Month, new DateOnly(2026, 11, 1)).Add(x => x.TestId, "cal"));

        // ComponentTestBase's FakeTimeProvider never moves off its start, 1 Jan 2000, so every day of November 2026 is in the future.
        Assert.That(cut.FindAll("button.cell:disabled"), Has.Count.EqualTo(30));
    }
}
