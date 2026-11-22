using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;

namespace CalorieTracker.Core.Tests.Diary;

public class MealWindowsTests
{
    [TestCase(0, 0, Meal.Snacks)]
    [TestCase(4, 59, Meal.Snacks)]
    [TestCase(5, 0, Meal.Breakfast)]
    [TestCase(10, 59, Meal.Breakfast)]
    [TestCase(11, 0, Meal.Lunch)]
    [TestCase(12, 0, Meal.Lunch)]
    [TestCase(14, 59, Meal.Lunch)]
    [TestCase(15, 0, Meal.Snacks)]
    [TestCase(16, 59, Meal.Snacks)]
    [TestCase(17, 0, Meal.Dinner)]
    [TestCase(20, 59, Meal.Dinner)]
    [TestCase(21, 0, Meal.Snacks)]
    [TestCase(23, 59, Meal.Snacks)]
    public void MealFor_TheTimeOfDay_GivesTheMealOfItsWindow(int hour, int minute, Meal expected) =>
        Assert.That(MealWindows.MealFor(new TimeOnly(hour, minute)), Is.EqualTo(expected));
}
