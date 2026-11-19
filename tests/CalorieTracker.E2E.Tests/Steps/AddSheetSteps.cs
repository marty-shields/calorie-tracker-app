using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Screens;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>The Add sheet and the navigation bar's Add button.</summary>
[Binding]
public sealed class AddSheetSteps(AddSheetScreen sheet, NavigationBar navigation, DashboardScreen dashboard, Journeys journeys, Native native, WebView web)
{
    private const string Meals = "(Breakfast|Lunch|Dinner|Snacks)";

    [Given(@"^I have opened the Add sheet$")]
    [When(@"^I open the Add sheet$")]
    public void OpenTheAddSheet()
    {
        journeys.WaitFor(Journeys.Screen.Dashboard);
        sheet.Open();
    }

    [When(@"^I choose " + Meals + @" on the Add sheet$")]
    public void ChooseMeal(string meal) => sheet.ChooseMeal(meal);

    [When(@"^I tap Search foods on the Add sheet$")]
    public void TapSearchFoods() => sheet.Search();

    [When(@"^I tap Scan barcode on the Add sheet$")]
    public void TapScanBarcode() => sheet.Scan();

    [When(@"^I open Foods from the Add sheet$")]
    public void OpenFoodsFromTheAddSheet() => journeys.OpenFoodsFromTheAddSheet();

    [When(@"^I open Scan from the Add sheet$")]
    [Given(@"^I have opened Scan$")]
    public void OpenScanFromTheAddSheet() => journeys.OpenScan();

    [Then(@"^I am on search to add to a meal$")]
    public void ThenIAmOnSearchToAddToAMeal() => journeys.WaitFor(Journeys.Screen.AddFood);

    [Then(@"^the navigation bar shows Today, Add and Trends only$")]
    public void ThenTheNavigationBarShowsTodayAddAndTrendsOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(navigation.Has("Today"), Is.True, "Today");
            Assert.That(navigation.HasAdd(), Is.True, "Add");
            Assert.That(navigation.Has("Trends"), Is.True, "Trends");
            Assert.That(navigation.Has("Foods"), Is.False, "Foods");
            Assert.That(navigation.Has("Scan"), Is.False, "Scan");
        });
    }

    [Then(@"^the Add sheet is titled ""(.*)"" with the meals Breakfast, Lunch, Dinner and Snacks$")]
    public void ThenTheAddSheetIsTitled(string title)
    {
        Assert.Multiple(() =>
        {
            Assert.That(sheet.Title(), Is.EqualTo(title));
            Assert.That(sheet.MealLabels(), Is.EqualTo(new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }));
        });
    }

    [Then(@"^the Add sheet has exactly one meal selected$")]
    public void ThenTheAddSheetHasOneMealSelected() =>
        Assert.That(new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }.Count(sheet.IsMealChosen), Is.EqualTo(1));

    [Then(@"^" + Meals + @" is selected on the Add sheet$")]
    public void ThenMealIsSelected(string meal) => web.Eventually(() => sheet.IsMealChosen(meal), Is.True, $"{meal} selected");

    [Then(@"^the Add sheet lists a usual food showing ""(\d+)"" g and its calories$")]
    public void ThenTheAddSheetListsAUsualFood(string grams) =>
        web.Eventually(() => sheet.UsualDetails().FirstOrDefault() ?? "", Does.Match($@"^{grams} g · \d+ kcal$"));

    [When(@"^I tap the plus of the first usual food on the Add sheet$")]
    public void TapThePlusOfTheFirstUsualFood() => sheet.AddFirstUsual();

    [Then(@"^" + Meals + @" lists (\d+) entries$")]
    public void ThenMealListsEntries(string meal, int count) => web.Eventually(() => dashboard.OpenAndCountEntries(meal), Is.EqualTo(count));

    [Then(@"^the Add sheet is (open|closed)$")]
    public void ThenTheAddSheetIs(string state) =>
        web.Eventually(sheet.IsOpen, Is.EqualTo(state == "open"), "Add sheet open");

    [Then(@"^focus is back on the Add button$")]
    public void ThenFocusIsBackOnTheAddButton() =>
        web.Eventually(sheet.AddButtonHasFocus, Is.True, "focus on the Add button");

    [Then(@"^focus is in the Add sheet$")]
    public void ThenFocusIsInTheAddSheet() => web.Eventually(sheet.FocusIsInSheet, Is.True, "focus in the Add sheet");

    [When(@"^I press Tab once for each control on the Add sheet$")]
    public void PressTabForEachControl() => sheet.TabThroughSheet(sheet.TabStopCount(), shift: false);

    [When(@"^I press Shift\+Tab (\d+) times on the Add sheet$")]
    public void PressShiftTab(int times) => sheet.TabThroughSheet(times, shift: true);

    [Then(@"^focus is on the Add sheet's (first|last) control$")]
    public void ThenFocusIsOn(string which) =>
        web.Eventually(sheet.Focused, Is.EqualTo(which == "first" ? "add-sheet-meal-" + FirstMeal() : "add-sheet-scan"), "focused control");

    private string FirstMeal() => new[] { "Breakfast", "Lunch", "Dinner", "Snacks" }.First(sheet.IsMealChosen).ToLowerInvariant();

    [When(@"^I tap the dimmed screen above the Add sheet$")]
    public void TapTheScrim() => sheet.TapScrim();

    [When(@"^I press Android back on the Add sheet$")]
    public void PressAndroidBack() => native.BackKey();
}
