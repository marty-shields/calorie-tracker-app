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
    [TestCase(14, 59, Meal.Lunch)]
    [TestCase(15, 0, Meal.Snacks)]
    [TestCase(16, 59, Meal.Snacks)]
    [TestCase(17, 0, Meal.Dinner)]
    [TestCase(20, 59, Meal.Dinner)]
    [TestCase(21, 0, Meal.Snacks)]
    [TestCase(23, 59, Meal.Snacks)]
    public void MealFor_TheTimeOfDay_GivesTheMealOfItsWindow(int hour, int minute, Meal expected) =>
        Assert.That(MealWindows.MealFor(new TimeOnly(hour, minute)), Is.EqualTo(expected));

    [TestCase(Meal.Breakfast, 4, 59, MealWindowState.Before)]
    [TestCase(Meal.Breakfast, 5, 0, MealWindowState.During)]
    [TestCase(Meal.Breakfast, 10, 59, MealWindowState.During)]
    [TestCase(Meal.Breakfast, 11, 0, MealWindowState.After)]
    [TestCase(Meal.Lunch, 10, 59, MealWindowState.Before)]
    [TestCase(Meal.Lunch, 11, 0, MealWindowState.During)]
    [TestCase(Meal.Lunch, 14, 59, MealWindowState.During)]
    [TestCase(Meal.Lunch, 15, 0, MealWindowState.After)]
    [TestCase(Meal.Dinner, 16, 59, MealWindowState.Before)]
    [TestCase(Meal.Dinner, 17, 0, MealWindowState.During)]
    [TestCase(Meal.Dinner, 20, 59, MealWindowState.During)]
    [TestCase(Meal.Dinner, 21, 0, MealWindowState.After)]
    [TestCase(Meal.Dinner, 23, 59, MealWindowState.After)]
    [TestCase(Meal.Snacks, 0, 0, MealWindowState.AnyTime)]
    [TestCase(Meal.Snacks, 12, 0, MealWindowState.AnyTime)]
    public void StateFor_AMealAndATime_SaysBeforeDuringAfterOrAnyTime(Meal meal, int hour, int minute, MealWindowState expected) =>
        Assert.That(MealWindows.StateFor(meal, new TimeOnly(hour, minute)), Is.EqualTo(expected));

    [Test]
    public void MealFor_EveryMinute_AgreesWithStateFor()
    {
        for (var minute = 0; minute < 24 * 60; minute++)
        {
            var time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute));
            var meal = MealWindows.MealFor(time);
            var expected = meal == Meal.Snacks ? MealWindowState.AnyTime : MealWindowState.During;
            Assert.That(MealWindows.StateFor(meal, time), Is.EqualTo(expected), time.ToString());
        }
    }
}
