using CalorieTracker.Core.Trends;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Stories.Stories.Screens;
using Microsoft.AspNetCore.Components;

namespace CalorieTracker.UI.Tests.Screens;

public class TrendsViewTests : ComponentTestBase
{
    private static readonly DateOnly Today = TrendsSamples.Today;

    private readonly List<DateOnly> days = [];
    private readonly List<DateOnly> weeks = [];
    private readonly List<DateOnly> months = [];
    private readonly List<TrendsPeriod> periods = [];
    private int previous;
    private int next;

    private IRenderedComponent<TrendsView> Render(TrendsPeriod period, DateOnly start, TrendsReport? report, bool loading = false, bool failed = false) =>
        Context.Render<TrendsView>(p => p
            .Add(x => x.Period, period)
            .Add(x => x.Start, start)
            .Add(x => x.Today, Today)
            .Add(x => x.Report, report)
            .Add(x => x.Loading, loading)
            .Add(x => x.LoadFailed, failed)
            .Add(x => x.OnSelectDay, EventCallback.Factory.Create<DateOnly>(this, days.Add))
            .Add(x => x.OnSelectWeek, EventCallback.Factory.Create<DateOnly>(this, weeks.Add))
            .Add(x => x.OnSelectMonth, EventCallback.Factory.Create<DateOnly>(this, months.Add))
            .Add(x => x.OnPeriodChanged, EventCallback.Factory.Create<TrendsPeriod>(this, periods.Add))
            .Add(x => x.OnPrevious, EventCallback.Factory.Create(this, () => previous++))
            .Add(x => x.OnNext, EventCallback.Factory.Create(this, () => next++)));

    private IRenderedComponent<TrendsView> Week() => Render(TrendsPeriod.Week, new(2026, 11, 9), TrendsSamples.Week);

    private IRenderedComponent<TrendsView> Month() => Render(TrendsPeriod.Month, new(2026, 11, 1), TrendsSamples.Month);

    private IRenderedComponent<TrendsView> Year() => Render(TrendsPeriod.Year, new(2026, 1, 1), TrendsSamples.Year);

    private static string Text(IRenderedComponent<TrendsView> cut, string testId) => cut.Find($"[data-testid={testId}]").TextContent.Trim();

    private static bool Exists(IRenderedComponent<TrendsView> cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    [Test]
    public void TopBar_HasTheTitleAndTheSettingsGear_AndTrendsIsCurrentInTheBar()
    {
        var cut = Week();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "screen-title"), Is.EqualTo("Trends"));
            Assert.That(cut.Find("[data-testid=settings]").GetAttribute("aria-label"), Is.EqualTo("Settings"));
            Assert.That(cut.FindAll("[data-testid=back]"), Is.Empty);
            Assert.That(cut.Find("[aria-current=page]").GetAttribute("data-testid"), Is.EqualTo("trends-link"));
            Assert.That(cut.FindAll("nav button").Select(b => b.GetAttribute("data-testid")), Is.EqualTo(new[] { "today-link", "add-button", "trends-link" }));
        });
    }

    [Test]
    public void Controls_ShowTheSegmentsAndTheLabelOfThePeriod()
    {
        Assert.Multiple(() =>
        {
            var week = Week();
            Assert.That(week.FindAll("[data-testid^=trends-period-]").Select(e => e.GetAttribute("data-testid")), Is.EqualTo(new[] { "trends-period-week", "trends-period-month", "trends-period-year" }));
            Assert.That(week.Find("[data-testid=trends-period-week]").HasAttribute("checked"), Is.True);
            Assert.That(Text(week, "trends-label"), Is.EqualTo("9 – 15 Nov"));
            Assert.That(Text(Month(), "trends-label"), Is.EqualTo("November 2026"));
            Assert.That(Text(Year(), "trends-label"), Is.EqualTo("2026"));
            Assert.That(Month().Find("[data-testid=trends-period-month]").HasAttribute("checked"), Is.True);
        });
    }

    [Test]
    public void Week_ShowsTheFixtureFigures()
    {
        var cut = Week();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-average-value"), Is.EqualTo("2,124"));
            Assert.That(Text(cut, "trends-average-caption"), Is.EqualTo("kcal a day, 24 over target"));
            Assert.That(Text(cut, "trends-on-target-value"), Is.EqualTo("3 of 7"));
            Assert.That(Text(cut, "trends-on-target-caption"), Is.EqualTo("days within 10% of target"));
            Assert.That(Text(cut, "trends-legend-3"), Is.EqualTo("Target 2,100, band ±10%"));
            Assert.That(Exists(cut, "trends-share"), Is.True);
            Assert.That(Text(cut, "trends-meal-list-label-0"), Is.EqualTo("Breakfast"));
            Assert.That(cut.FindAll("[data-testid^=trends-meal-list-row-]"), Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void Week_ShowsTheMacrosAsTheKitsStackedBarInDataSeriesOneTwoAndThree()
    {
        var cut = Week();

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=trends-share]").GetAttribute("aria-label"), Does.StartWith("Average energy share: Protein "));
            Assert.That(Text(cut, "trends-share-label-0"), Does.StartWith("Protein ").And.EndWith(" kcal"));
            Assert.That(Text(cut, "trends-share-label-1"), Does.StartWith("Carbs ").And.EndWith(" kcal"));
            Assert.That(Text(cut, "trends-share-label-2"), Does.StartWith("Fat ").And.EndWith(" kcal"));
            Assert.That(cut.FindAll("[data-testid^=trends-share-segment-]").Select(e => e.ClassList.Single(c => c.StartsWith("series-", StringComparison.Ordinal))), Is.EqualTo(new[] { "series-1", "series-2", "series-3" }));
        });
    }

    [Test]
    public void Week_DrawsTheTargetAsTheKitsLineAndBandOutline_AndNamesBarsInKcal()
    {
        var cut = Week();

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "trends-chart-target"), Is.True);
            Assert.That(Exists(cut, "trends-chart-band-top"), Is.True);
            Assert.That(Exists(cut, "trends-chart-band-bottom"), Is.True);
            Assert.That(cut.Find("[data-testid=trends-chart-bar-2026-11-13]").GetAttribute("aria-label"), Does.EndWith("kcal, over target"));
        });
    }

    [Test]
    public void Month_ShowsTheCalendarFromMondayWithLettersForEveryDay()
    {
        var cut = Month();

        var headings = cut.Find("[data-testid=trends-calendar] .weekdays").Children.Select(e => e.TextContent);
        var blanks = cut.FindAll("[data-testid=trends-calendar] .blank");
        Assert.Multiple(() =>
        {
            Assert.That(headings, Is.EqualTo(new[] { "M", "T", "W", "T", "F", "S", "S" }));
            Assert.That(blanks, Has.Count.EqualTo(6), "1 November 2026 is a Sunday, the last day of a Monday-first week");
            Assert.That(cut.Find("[data-testid=trends-calendar-day-2026-11-01]").TextContent, Does.Contain("kcal"), "the day's text alternative names the unit");
            Assert.That(cut.Find("[data-testid=trends-calendar-day-2026-11-01]").QuerySelector("[data-testid=trends-calendar-marker-2026-11-01]")!.TextContent, Is.EqualTo("✓"), "an on-target day carries a tick, as the kit draws it");
        });
    }

    [Test]
    public void Week_ColoursEachDayByItsStatus_FridayAndSaturdayOverAndWednesdayAndSundayUnder()
    {
        var cut = Week();

        string Status(int day) => cut.Find($"[data-testid=trends-chart-bar-2026-11-{day:00}]").ClassList.First(c => c is "under" or "on" or "over");

        Assert.That(new[] { 9, 10, 11, 12, 13, 14, 15 }.Select(Status), Is.EqualTo(new[] { "on", "on", "under", "on", "over", "over", "under" }));
    }

    [Test]
    public void EveryChartAndTheCalendar_AreButtonsReachableByKeyboard()
    {
        var week = Week();
        var month = Month();
        var year = Year();

        Assert.Multiple(() =>
        {
            var bars = week.FindAll("[data-testid^=trends-chart-bar-]");
            Assert.That(bars, Has.Count.EqualTo(7));
            Assert.That(bars.All(b => b.GetAttribute("role") == "button" && b.GetAttribute("tabindex") == "0"), Is.True);
            var cells = month.FindAll("[data-testid^=trends-calendar-day-]");
            Assert.That(cells, Is.Not.Empty);
            Assert.That(cells.All(c => c.TagName == "BUTTON"), Is.True);
            var weekBars = month.FindAll("[data-testid^=trends-weeks-chart-bar-]");
            Assert.That(weekBars, Is.Not.Empty);
            Assert.That(weekBars.All(w => w.GetAttribute("role") == "button"), Is.True);
            var monthBars = year.FindAll("[data-testid^=trends-months-chart-bar-]");
            Assert.That(monthBars, Is.Not.Empty);
            Assert.That(monthBars.All(m => m.GetAttribute("role") == "button"), Is.True);
        });
    }

    [Test]
    public void TappingADaysBar_AndACalendarCell_RaiseThatDay()
    {
        Week().Find("[data-testid=trends-chart-bar-2026-11-12]").Click();
        Month().Find("[data-testid=trends-calendar-day-2026-11-12]").Click();

        Assert.That(days, Is.EqualTo(new[] { new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 12) }));
    }

    [Test]
    public void TappingAMonthsBar_InYear_RaisesThatMonth_AndAWeeksBar_InMonth_RaisesThatWeek()
    {
        Year().Find("[data-testid=trends-months-chart-bar-2026-03-01]").Click();
        Month().Find("[data-testid=trends-weeks-chart-bar-2026-11-09]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(months, Is.EqualTo(new[] { new DateOnly(2026, 3, 1) }));
            Assert.That(weeks, Is.EqualTo(new[] { new DateOnly(2026, 11, 9) }));
        });
    }

    [Test]
    public void Month_ShowsTheTilesTheCalendarTheWeeksAndTheTopFive()
    {
        var cut = Month();

        string Status(int day) => cut.Find($"[data-testid=trends-calendar-day-2026-11-{day:00}]").ClassList.First(c => c is "under" or "on" or "over" or "none" or "future");

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-average-value"), Is.EqualTo("2,103"));
            Assert.That(Text(cut, "trends-on-target-value"), Is.EqualTo("10 of 16"));
            Assert.That(Text(cut, "trends-on-target-caption"), Is.EqualTo("logged days within 10% of target"));
            Assert.That(Text(cut, "trends-streak-value"), Is.EqualTo("13 days"));
            Assert.That(Text(cut, "trends-streak-label"), Is.EqualTo("Streak"));
            Assert.That(Text(cut, "trends-logged-value"), Is.EqualTo("16 of 17"));
            Assert.That(new[] { 1, 5, 7, 8, 11, 13, 18, 19 }.Select(Status), Is.EqualTo(new[] { "on", "none", "over", "under", "under", "over", "none", "future" }), "today's own figure is left out, as in every other count");
            Assert.That(cut.Find("[data-testid=trends-calendar-day-2026-11-18]").ClassList, Does.Contain("today"));
            Assert.That(cut.FindAll("[data-testid^=trends-weeks-chart-bar-]"), Has.Count.EqualTo(4));
            Assert.That(cut.FindAll("[data-testid^=trends-top-list-row-]"), Has.Count.EqualTo(5));
            Assert.That(Text(cut, "trends-top-list-label-0"), Is.EqualTo("Chicken curry"));
            Assert.That(Text(cut, "trends-top-list-value-0"), Is.EqualTo("3,180 kcal"));
        });
    }

    [Test]
    public void Month_FadesAWeekOfFewDays_AndSaysWhy()
    {
        var cut = Month();

        var thin = cut.FindAll("[data-testid^=trends-weeks-chart-bar-].thin").Select(e => e.GetAttribute("aria-label")!);

        Assert.Multiple(() =>
        {
            Assert.That(thin, Is.EqualTo(new[] { "Week of 26 Oct, 2,090 kcal, on target, based on 1 day", "Week of 16 Nov, 2,090 kcal, on target, based on 2 days" }));
            Assert.That(Text(cut, "trends-thin-note"), Is.EqualTo("Faded bars rest on fewer than 3 logged days."));
        });
    }

    [Test]
    public void Month_ForAPastMonth_ShowsItsLongestRunInPlaceOfTheStreakThroughToday()
    {
        var cut = Context.Render<TrendsView>(p => p
            .Add(x => x.Period, TrendsPeriod.Month)
            .Add(x => x.Start, new DateOnly(2026, 11, 1))
            .Add(x => x.Today, new DateOnly(2026, 12, 20))
            .Add(x => x.Report, TrendsSamples.Month));

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-streak-value"), Is.EqualTo("13 days"));
            Assert.That(Text(cut, "trends-streak-label"), Is.EqualTo("Longest streak"));
            Assert.That(cut.Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public void Year_ShowsTheTilesTheMonthlyBarsAndTheDaysLoggedPerMonth()
    {
        var cut = Year();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-on-target-value"), Is.EqualTo("11 of 11"));
            Assert.That(Text(cut, "trends-on-target-caption"), Is.EqualTo("months averaging within 10% of target"));
            Assert.That(Text(cut, "trends-logged-value"), Is.EqualTo("88%"));
            Assert.That(Text(cut, "trends-logged-caption"), Is.EqualTo("283 of 321 days"));
            Assert.That(Text(cut, "trends-lowest-value"), Is.EqualTo("Oct"));
            Assert.That(Text(cut, "trends-lowest-caption"), Is.EqualTo("1,990 kcal a day"));
            Assert.That(cut.FindAll("[data-testid^=trends-months-chart-bar-]"), Has.Count.EqualTo(11));
            Assert.That(Exists(cut, "trends-months-chart-band"), Is.True);
            Assert.That(Text(cut, "trends-logged-list-value-0"), Is.EqualTo("27 of 31"));
            Assert.That(cut.FindAll("[data-testid^=trends-logged-list-row-]"), Has.Count.EqualTo(11), "months to come are not listed");
        });
    }

    private IRenderedComponent<TrendsView> YearWith(Func<TrendBucket, int, TrendBucket> change, Func<TrendDay, TrendDay>? changeDay = null) =>
        Render(TrendsPeriod.Year, new(2026, 1, 1), TrendsSamples.Year with
        {
            Buckets = TrendsSamples.Year.Buckets.Select(change).ToList(),
            Days = TrendsSamples.Year.Days.Select(changeDay ?? (d => d)).ToList(),
        });

    [Test]
    public void Year_CountsOnlyMonthsOnTarget_SoAnUnderAndAnOverMonthAreNotCounted()
    {
        var cut = YearWith((b, i) => i switch { 0 => b with { AverageKcal = 1500 }, 1 => b with { AverageKcal = 2800 }, _ => b });

        Assert.That(Text(cut, "trends-on-target-value"), Is.EqualTo("9 of 11"));
    }

    [Test]
    public void Year_LeavesAMonthOfFewerThanThreeLoggedDaysOutOfTheMonthsOnTargetAndTheLowestMonth()
    {
        var cut = YearWith((b, i) => i == 0 ? b with { AverageKcal = 900, DaysLogged = 2 } : b);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-on-target-value"), Is.EqualTo("10 of 10"));
            Assert.That(Text(cut, "trends-lowest-value"), Is.EqualTo("Oct"));
            Assert.That(cut.Find("[data-testid=trends-months-chart-bar-2026-01-01]").ClassList, Does.Contain("thin"));
        });
    }

    [Test]
    public void Year_AMonthOfExactlyThreeLoggedDays_IsNotThin_SoItCounts()
    {
        var cut = YearWith((b, i) => i == 0 ? b with { AverageKcal = 900, DaysLogged = 3 } : b);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-on-target-value"), Is.EqualTo("10 of 11"));
            Assert.That(Text(cut, "trends-lowest-value"), Is.EqualTo("Jan"));
            Assert.That(cut.Find("[data-testid=trends-months-chart-bar-2026-01-01]").ClassList, Does.Not.Contain("thin"));
        });
    }

    [Test]
    public void Year_AMonthWithNoTargetOnItsLoggedDays_IsNotJudged()
    {
        var cut = YearWith((b, _) => b, d => d.Date.Month == 1 ? d with { TargetKcal = null } : d);

        Assert.That(Text(cut, "trends-on-target-value"), Is.EqualTo("10 of 10"));
    }

    [Test]
    public void Week_CountsDaysOnTargetOutOfTheSevenDaysOfTheWeek()
    {
        var cut = Render(TrendsPeriod.Week, new(2026, 11, 16), TrendsSamples.Week);

        Assert.That(Text(cut, "trends-on-target-value"), Does.EndWith("of 7"));
    }

    [Test]
    public void AWeekWithOnlyTodayLogged_AndNothingBefore_ShowsTheEmptyState()
    {
        var report = TrendsSamples.LittleData with { AnyLoggedBeforeToday = false, Days = TrendsSamples.LittleData.Days.Select(d => d with { IsLogged = d.Date == Today }).ToList() };

        var cut = Render(TrendsPeriod.Week, new(2026, 11, 16), report);

        Assert.That(Exists(cut, "trends-empty"), Is.True);
    }

    [Test]
    public void AnEmptyWeek_WhenOtherDaysWereLogged_KeepsTheLayoutWithNotEnoughDataYet()
    {
        var cut = Render(TrendsPeriod.Week, new(2026, 11, 2), TrendsSamples.EmptyWeekWithHistory);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "trends-empty"), Is.False);
            Assert.That(Text(cut, "trends-average-note"), Is.EqualTo("Not enough data yet"));
        });
    }

    [Test]
    public void Next_IsOnWhenTodayIsAMondayAndTheShownWeekStartedTheMondayBefore()
    {
        DateOnly monday = new(2026, 11, 16);
        IRenderedComponent<TrendsView> Shown(DateOnly start) => Context.Render<TrendsView>(p => p
            .Add(x => x.Period, TrendsPeriod.Week)
            .Add(x => x.Start, start)
            .Add(x => x.Today, monday)
            .Add(x => x.Report, TrendsSamples.Week));

        Assert.Multiple(() =>
        {
            Assert.That(Shown(monday.AddDays(-7)).Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.False);
            Assert.That(Shown(monday).Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.True);
        });
    }

    [Test]
    public void Next_IsOffOnTheCurrentPeriodOnly_AndPreviousNever()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Week().Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.False, "9 - 15 Nov is a past week");
            Assert.That(Render(TrendsPeriod.Week, new(2026, 11, 16), TrendsSamples.LittleData).Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.True);
            Assert.That(Month().Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.True);
            Assert.That(Year().Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.True);
            Assert.That(Render(TrendsPeriod.Year, new(2025, 1, 1), TrendsSamples.Year).Find("[data-testid=trends-next]").HasAttribute("disabled"), Is.False);
            Assert.That(Month().Find("[data-testid=trends-previous]").HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public void TheArrowsAndTheSegments_RaiseTheirCallbacks()
    {
        var cut = Week();

        cut.Find("[data-testid=trends-previous]").Click();
        cut.Find("[data-testid=trends-next]").Click();
        cut.Find("[data-testid=trends-period-year]").Change(true);

        Assert.Multiple(() =>
        {
            Assert.That((previous, next), Is.EqualTo((1, 1)));
            Assert.That(periods, Is.EqualTo(new[] { TrendsPeriod.Year }));
        });
    }

    [TestCase(TrendsPeriod.Week, "week")]
    [TestCase(TrendsPeriod.Month, "month")]
    [TestCase(TrendsPeriod.Year, "year")]
    public void TheArrows_AreNamedForThePeriod(TrendsPeriod period, string unit)
    {
        var cut = period switch { TrendsPeriod.Week => Week(), TrendsPeriod.Month => Month(), _ => Year() };

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=trends-previous]").GetAttribute("aria-label"), Is.EqualTo($"Previous {unit}"));
            Assert.That(cut.Find("[data-testid=trends-next]").GetAttribute("aria-label"), Is.EqualTo($"Next {unit}"));
        });
    }

    [Test]
    public void Loading_KeepsTheControlsAndShowsASpinner()
    {
        var cut = Render(TrendsPeriod.Week, new(2026, 11, 9), null, loading: true);

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "trends-loading"), Is.True);
            Assert.That(Exists(cut, "trends-period"), Is.True);
            Assert.That(Text(cut, "trends-label"), Is.EqualTo("9 – 15 Nov"));
            Assert.That(Exists(cut, "trends-average"), Is.False);
        });
    }

    [Test]
    public void Failed_SaysSo_AndTryAgainRaisesOnRetry()
    {
        var retried = 0;
        var cut = Context.Render<TrendsView>(p => p
            .Add(x => x.Period, TrendsPeriod.Week)
            .Add(x => x.Start, new DateOnly(2026, 11, 9))
            .Add(x => x.Today, Today)
            .Add(x => x.LoadFailed, true)
            .Add(x => x.OnRetry, EventCallback.Factory.Create(this, () => retried++)));

        cut.Find("[data-testid=trends-load-failed-retry]").Click();

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-load-failed-error"), Is.EqualTo("Couldn't load your trends"));
            Assert.That(Text(cut, "trends-load-failed-retry"), Is.EqualTo("Try again"));
            Assert.That(retried, Is.EqualTo(1));
        });
    }

    [Test]
    public void NoData_ShowsTheEmptyStateInPlaceOfTheFigures()
    {
        var cut = Render(TrendsPeriod.Week, new(2026, 11, 2), TrendsSamples.NoData);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-empty-message"), Is.EqualTo("Nothing logged yet"));
            Assert.That(Text(cut, "trends-empty-hint"), Is.EqualTo("Your trends show here once you've logged a day"));
            Assert.That(Exists(cut, "trends-average"), Is.False);
            Assert.That(Exists(cut, "trends-period"), Is.True);
        });
    }

    [Test]
    public void LittleData_KeepsTheLayoutAndSaysNotEnoughDataYet()
    {
        var cut = Render(TrendsPeriod.Week, new(2026, 11, 16), TrendsSamples.LittleData);

        Assert.Multiple(() =>
        {
            Assert.That(Text(cut, "trends-average-value"), Is.EqualTo("–"));
            Assert.That(Text(cut, "trends-average-note"), Is.EqualTo("Not enough data yet"));
            Assert.That(Text(cut, "trends-average-caption"), Is.EqualTo("kcal a day"), "no gap to a target is claimed");
            Assert.That(Text(cut, "trends-on-target-note"), Is.EqualTo("Not enough data yet"));
            Assert.That(Text(cut, "trends-macros-note"), Is.EqualTo("Not enough data yet"));
            Assert.That(Text(cut, "trends-meals-note"), Is.EqualTo("Not enough data yet"));
            Assert.That(Exists(cut, "trends-share"), Is.False);
            Assert.That(cut.FindAll("[data-testid^=trends-chart-bar-]"), Has.Count.EqualTo(2), "the logged days still show");
        });
    }

    [Test]
    public void TheCalendar_And_TheWeeksChart_HaveTheirOwnLegendEntries()
    {
        var cut = Month();

        Assert.That(cut.FindAll("[data-testid^=trends-legend-]").Select(e => e.GetAttribute("data-testid")), Is.EqualTo(new[] { "trends-legend-0", "trends-legend-1", "trends-legend-2", "trends-legend-3", "trends-legend-4" }));
    }

    [Test]
    public void EveryCard_HasASixteenPixelKitTitle_AndTheStepperLabelIsNotAHeading()
    {
        var cut = Week();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll(".card h2.title").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Calories by day", "Average macros", "Kcal by meal" }));
            Assert.That(cut.Find("[data-testid=trends-label]").LocalName, Is.EqualTo("span"));
            Assert.That(cut.FindAll("h2").Count, Is.EqualTo(3), "the period label adds no heading between the screen title and the card titles");
            Assert.That(cut.FindAll("h3"), Is.Empty);
        });
    }

    [Test]
    public void Month_And_Year_CardsAreTitledByTheKitToo()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Month().FindAll(".card h2.title").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Each day", "Average a day, by week", "Top foods" }));
            Assert.That(Year().FindAll(".card h2.title").Select(h => h.TextContent.Trim()), Is.EqualTo(new[] { "Average a day, by month", "Days logged per month" }));
        });
    }

    [Test]
    public void Tiles_ShowTheLabelBeforeTheValue()
    {
        var cut = Week();

        var parts = cut.Find("[data-testid=trends-average]").Children.Select(c => c.GetAttribute("data-testid")).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(parts, Is.EqualTo(new[] { "trends-average-label", "trends-average-value", "trends-average-caption" }));
            Assert.That(Text(cut, "trends-average-label"), Is.EqualTo("Average"));
            Assert.That(Text(cut, "trends-on-target-label"), Is.EqualTo("On target"));
        });
    }

    [Test]
    public void Legend_UsesDotsAndMarksOverWithATriangleSoColourIsNotTheOnlyCue()
    {
        var cut = Week();

        var items = cut.FindAll("[data-testid^=trends-legend-]").Where(e => e.LocalName == "li").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(i => i.QuerySelector(".swatch")!.ClassList.Last()), Is.EqualTo(new[] { "chip", "accent", "error", "line" }));
            Assert.That(items[2].QuerySelector(".marker")!.TextContent, Is.EqualTo("▲"));
            Assert.That(items[0].QuerySelector(".marker"), Is.Null);
            Assert.That(Text(cut, "trends-legend-2"), Does.Contain("Over"));
        });
    }

    [Test]
    public void Legend_InMonth_AddsNotLoggedOnAnOutlineSwatchBeforeTheTarget()
    {
        var cut = Month();

        var items = cut.FindAll("[data-testid^=trends-legend-]").Where(e => e.LocalName == "li").ToList();

        Assert.That(items.Select(i => i.QuerySelector(".swatch")!.ClassList.Last()), Is.EqualTo(new[] { "chip", "accent", "error", "outline", "line" }));
        Assert.That(Text(cut, "trends-legend-3"), Is.EqualTo("Not logged"));
    }

    [Test]
    public void Year_WithNoMonthOfEnoughDays_HasNoLowestMonth()
    {
        var cut = YearWith((b, _) => b with { DaysLogged = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(Exists(cut, "trends-lowest-caption"), Is.False);
            Assert.That(Text(cut, "trends-lowest-note"), Is.EqualTo("Not enough data yet"));
        });
    }
}
