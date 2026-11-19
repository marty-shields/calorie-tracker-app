using System.Text.RegularExpressions;
using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

[Binding]
public sealed partial class DashboardSteps(WebView web, DashboardScreen dashboard, Journeys journeys)
{
    [Then(@"^I am on the Dashboard titled ""(.*)""$")]
    public void ThenOnTheDashboardTitled(string title) => web.Eventually(() => web.Text("screen-title"), Is.EqualTo(title));

    [Then(@"^the Dashboard shows ""(.*)""$")]
    public void ThenTheDashboardShows(string text)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(dashboard.EmptyText, Is.EqualTo(text));
    }

    [Then(@"^no startup error is shown$")]
    public void ThenNoStartupError() => Assert.That(dashboard.HasStartupError(), Is.False);

    private static readonly string[] MealNames = ["Breakfast", "Lunch", "Dinner", "Snacks"];

    [Then(@"^every meal has a \+ button$")]
    public void ThenEveryMealHasAPlus()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(() => MealNames.Where(dashboard.HasAddButton).ToList(), Is.EqualTo(MealNames));
    }

    [Then(@"^Lunch lists 1 entry with a kcal total$")]
    public void ThenLunchListsOneEntry()
    {
        web.Eventually(() => dashboard.OpenAndCountEntries("Lunch"), Is.EqualTo(1));
        Assert.That(dashboard.OpenAndReadMealTotal("Lunch"), Does.EndWith(" kcal"));
    }

    [Then(@"^the other meals list no entries$")]
    public void ThenTheOtherMealsListNoEntries() =>
        Assert.That(new[] { "Breakfast", "Dinner", "Snacks" }.Sum(dashboard.OpenAndCountEntries), Is.Zero);

    [Then(@"^the calories eaten are above 0$")]
    public void ThenTheCaloriesEatenAreAbove0() =>
        web.Eventually(() => int.Parse(dashboard.Eaten().Replace(",", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThan(0));

    [Then(@"^the Dashboard no longer says nothing is logged$")]
    public void ThenTheDashboardNoLongerSaysNothingIsLogged() => Assert.That(web.Exists("dashboard-empty"), Is.False);

    [Then(@"^the ring is captioned ""(.*)""$")]
    public void ThenTheRingIsCaptioned(string caption)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        web.Eventually(dashboard.RingLabel, Is.EqualTo(caption));
    }

    [Then(@"^the Dashboard says ""(.*)""$")]
    public void ThenTheDashboardSays(string text) => web.Eventually(() => dashboard.NoTargetText(), Is.EqualTo(text));

    [When(@"^I tap Set a target$")]
    public void WhenITapSetATarget() => dashboard.SetTarget();

    [When(@"^I tap the next day$")]
    public void WhenITapTheNextDay() => dashboard.NextDay();

    [When(@"^I tap the date strip label$")]
    public void WhenITapTheDateStripLabel() => dashboard.ToToday();

    [Then(@"^the date strip says ""(.*)""$")]
    public void ThenTheDateStripSays(string text) => web.Eventually(dashboard.DateLabel, Does.StartWith(text));

    [When(@"^I go back to the Dashboard$")]
    [Given(@"^I go back to the Dashboard$")]
    public void WhenIGoBackToTheDashboard() => journeys.GoToDashboard();

    private bool backDated;

    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";

    /// <summary>The file <c>E2EClockSeam</c> reads; <c>ScanSeamValuesTests</c> checks it matches.</summary>
    private const string ClockSeamFileName = "e2e-clock.txt";

    /// <summary>
    /// Pins the Debug app's clock to a time of day on the device's date, so which meals are open does not depend on the wall clock,
    /// then goes to tomorrow and back to today so the Dashboard reads the clock again.
    /// </summary>
    [Given(@"^the app clock reads (\d\d:\d\d)$")]
    public void GivenTheAppClockReads(string time) => PinClock(time);

    /// <summary>
    /// Pins the Debug app's clock to a time of day some whole days before the device's date, for a scenario that saves something dated in the past.
    /// The Release app has no clock seam, so there it does nothing and everything saved is dated today.
    /// </summary>
    [Given(@"^the app clock reads (\d+) days? ago at (\d\d:\d\d)$")]
    public void GivenTheAppClockReadsDaysAgo(int days, string time)
    {
        if (AppiumSetup.App.IsDebug)
        {
            backDated = true;
            PinClock($"-{days}d {time}");
        }
    }

    /// <summary>A failed scenario must not leave a back-dated clock for the next one.</summary>
    [AfterScenario]
    public void RemoveBackDatedClock()
    {
        if (backDated)
        {
            RemoveClockFile();
        }
    }

    /// <summary>Removes the pinned clock, so the app reads the device's again; does nothing on the Release app.</summary>
    [Given(@"^the app clock is the device's$")]
    public void GivenTheAppClockIsTheDevices()
    {
        if (!AppiumSetup.App.IsDebug)
        {
            return;
        }

        RemoveClockFile();
        backDated = false;
        MakeTheDashboardReadTheClock();
    }

    private static void RemoveClockFile() => AppiumSetup.Adb.Shell("rm", "-f", $"{AppiumSetup.App.ExternalFilesFolder}/{ClockSeamFileName}");

    private void PinClock(string pin)
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        AppiumSetup.Adb.WriteText($"{AppiumSetup.App.ExternalFilesFolder}/{ClockSeamFileName}", pin);
        MakeTheDashboardReadTheClock();
    }

    /// <summary>Goes to tomorrow and back to today, so the Dashboard reads the clock again.</summary>
    private void MakeTheDashboardReadTheClock()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        dashboard.NextDay();
        web.Eventually(dashboard.DateLabel, Does.StartWith("Tomorrow"));
        dashboard.ToToday();
        web.Eventually(dashboard.DateLabel, Does.StartWith("Today"));
    }

    [When(@"^I leave Today and come back$")]
    public void WhenILeaveTodayAndComeBack()
    {
        journeys.OpenSettings();
        journeys.GoToDashboard();
    }

    [When(@"^I tap the previous day$")]
    public void WhenITapThePreviousDay() => dashboard.PreviousDay();

    [When(@"^I tap the " + Meals + " header$")]
    public void WhenITapTheMealHeader(string meal) => dashboard.ToggleMeal(meal);

    [Then(@"^" + Meals + " is open and marked Now$")]
    public void ThenMealIsOpenAndMarkedNow(string meal)
    {
        web.Eventually(() => dashboard.HasNowChip(meal), Is.True, $"{meal} is marked Now");
        Assert.That(dashboard.Expanded(meal), Is.Null.Or.EqualTo("true"), $"{meal} is open");
    }

    [Then(@"^" + Meals + " is still marked Now$")]
    public void ThenMealIsStillMarkedNow(string meal) => web.Eventually(() => dashboard.HasNowChip(meal), Is.True, $"{meal} is marked Now");

    [Then(@"^" + Meals + " is open$")]
    public void ThenMealIsOpen(string meal)
    {
        web.Eventually(() => dashboard.Expanded(meal), Is.Null.Or.EqualTo("true"), $"{meal} is open");
        Assert.That(dashboard.FromText(meal), Is.Null, $"{meal} shows no start time");
    }

    [Then(@"^" + Meals + @" shows ""(from \d\d:\d\d)"" with a \+ and no entries$")]
    public void ThenMealShowsFrom(string meal, string from)
    {
        web.Eventually(() => dashboard.FromText(meal), Is.EqualTo(from));
        Assert.Multiple(() =>
        {
            Assert.That(dashboard.HasAddButton(meal), Is.True, $"{meal} has a +");
            Assert.That(dashboard.OpenAndCountEntries(meal), Is.Zero, $"{meal} lists no entries");
            Assert.That(web.Exists($"meal-{meal.ToLowerInvariant()}-none"), Is.False, $"{meal} says nothing logged");
        });
    }

    [Then(@"^" + Meals + " is folded to one line with 1 item and its kcal$")]
    public void ThenMealIsFolded(string meal)
    {
        web.Eventually(() => dashboard.Expanded(meal), Is.EqualTo("false"), $"{meal} is folded");
        Assert.That(dashboard.FoldedLine(meal), Does.Match(OneItem()), $"{meal}'s folded line");
        Assert.That(dashboard.HasAddButton(meal), Is.True, $"{meal} keeps its +");
    }

    [Then(@"^" + Meals + @" is open with (\d+) entries shown$")]
    public void ThenMealIsOpenWithEntries(string meal, int count)
    {
        web.Eventually(() => dashboard.Expanded(meal), Is.EqualTo("true"), $"{meal} is open");
        Assert.That(dashboard.FoldedLine(meal), Is.Null, $"{meal} is no longer a single line");
        web.Eventually(() => dashboard.OpenAndCountEntries(meal), Is.EqualTo(count), $"{meal}'s entries");
    }

    [Then(@"^" + Meals + " is open with its entry shown$")]
    public void ThenMealIsOpenWithItsEntry(string meal)
    {
        web.Eventually(() => dashboard.Expanded(meal), Is.EqualTo("true"), $"{meal} is open");
        Assert.That(dashboard.FoldedLine(meal), Is.Null, $"{meal} is no longer a single line");
        Assert.That(dashboard.OpenAndCountEntries(meal), Is.EqualTo(1), $"{meal}'s entries");
    }

    [Then(@"^no meal is marked Now, shows a start time or is folded$")]
    public void ThenNoMealHasAWindow()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        Assert.Multiple(() =>
        {
            foreach (var meal in MealNames)
            {
                Assert.That(dashboard.HasNowChip(meal), Is.False, $"{meal} is marked Now");
                Assert.That(dashboard.FromText(meal), Is.Null, $"{meal} shows a start time");
                Assert.That(dashboard.Expanded(meal), Is.Null.Or.EqualTo("true"), $"{meal} is folded");
            }
        });
    }

    [GeneratedRegex(@"^1 item · [\d,]+ kcal$")]
    private static partial Regex OneItem();
}
