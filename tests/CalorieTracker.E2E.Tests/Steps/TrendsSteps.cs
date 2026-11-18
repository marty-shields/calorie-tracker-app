using System.Globalization;
using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>Trends from the navigation bar. Expected labels come from the device's clock, which scenarios can't fix.</summary>
[Binding]
public sealed class TrendsSteps(TrendsScreen trends, NavigationBar navigation, AddSheetScreen sheet, DashboardScreen dashboard, Journeys journeys, DeviceClock clock, WebView web)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private DateOnly Today => DateOnly.FromDateTime(clock.LocalNow());

    private static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>The week's label as the strip shows it: "9 – 15 Nov", "30 Nov – 6 Dec", or with the years across New Year.</summary>
    private static string WeekLabel(DateOnly monday)
    {
        var sunday = monday.AddDays(6);
        if (monday.Year != sunday.Year)
        {
            return $"{monday.ToString("d MMM yyyy", Invariant)} – {sunday.ToString("d MMM yyyy", Invariant)}";
        }

        return monday.Month == sunday.Month
            ? $"{monday.Day} – {sunday.ToString("d MMM", Invariant)}"
            : $"{monday.ToString("d MMM", Invariant)} – {sunday.ToString("d MMM", Invariant)}";
    }

    private string LabelOf(string period, int offset = 0) => period.ToLowerInvariant() switch
    {
        "month" => Today.AddMonths(offset).ToString("MMMM yyyy", Invariant),
        "year" => Today.AddYears(offset).ToString("yyyy", Invariant),
        _ => WeekLabel(MondayOf(Today).AddDays(7 * offset)),
    };

    [Given(@"^I have opened Trends$")]
    [When(@"^I open Trends from the bar$")]
    public void OpenTrendsFromTheBar()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        navigation.Open("Trends");
        web.WaitUntil(trends.IsShowing, "Trends didn't open");
    }

    [Then(@"^I am on Trends showing the current week$")]
    public void ThenIAmOnTrendsShowingTheCurrentWeek()
    {
        web.WaitUntil(trends.IsShowing, "Trends didn't show");
        web.Eventually(trends.Label, Is.EqualTo(LabelOf("week")));
    }

    [Then(@"^Trends is the current item in the bar$")]
    public void ThenTrendsIsTheCurrentItem() => web.Eventually(trends.IsCurrentInBar, Is.True);

    [When(@"^I choose (Week|Month|Year) on Trends$")]
    public void ChoosePeriod(string period) => trends.ChoosePeriod(period);

    [Then(@"^Trends shows the current (week|month|year)$")]
    public void ThenTrendsShowsTheCurrent(string period) => web.Eventually(trends.Label, Is.EqualTo(LabelOf(period)));

    [When(@"^I go back a (week|month|year) on Trends$")]
    public void GoBack(string _) => trends.Previous();

    [When(@"^I go forward a (week|month|year) on Trends$")]
    public void GoForward(string _) => trends.Next();

    [Then(@"^Trends shows the (week|month|year) before the current one$")]
    public void ThenTrendsShowsTheOneBefore(string period) => web.Eventually(trends.Label, Is.EqualTo(LabelOf(period, -1)));

    [Then(@"^next is (available|not available) on Trends$")]
    public void ThenNextIs(string state) => web.Eventually(trends.NextIsEnabled, Is.EqualTo(state == "available"));

    [When(@"^I tap the first day of last month on the Trends calendar$")]
    public void TapTheFirstDayOfLastMonth() => trends.TapCalendarDay(FirstOfLastMonth());

    [Then(@"^I am on the Dashboard on the first day of last month$")]
    public void ThenIAmOnTheDashboardOnThatDay()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        var day = FirstOfLastMonth();
        var format = day.Year == Today.Year ? "d MMM" : "d MMM yyyy";
        web.Eventually(dashboard.DateLabel, Is.EqualTo($"{day.ToString("ddd", Invariant)}, {day.ToString(format, Invariant)}"));
    }

    [When(@"^I open the Add sheet on Trends$")]
    public void OpenTheAddSheetOnTrends() => sheet.Open();

    private DateOnly FirstOfLastMonth()
    {
        var last = Today.AddMonths(-1);
        return new DateOnly(last.Year, last.Month, 1);
    }
}
