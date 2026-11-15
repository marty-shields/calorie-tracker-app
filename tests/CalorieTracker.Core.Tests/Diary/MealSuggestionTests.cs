using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;

namespace CalorieTracker.Core.Tests.Diary;

public class MealSuggestionTests
{
    [TestCase(0, 0, Meal.Breakfast)]
    [TestCase(10, 59, Meal.Breakfast)]
    [TestCase(11, 0, Meal.Lunch)]
    [TestCase(14, 59, Meal.Lunch)]
    [TestCase(15, 0, Meal.Dinner)]
    [TestCase(20, 59, Meal.Dinner)]
    [TestCase(21, 0, Meal.Snacks)]
    [TestCase(23, 59, Meal.Snacks)]
    public void For_TheTimeOfDay_GivesTheMealOfThatPartOfTheDay(int hour, int minute, Meal expected) =>
        Assert.That(MealSuggestion.For(new TimeOnly(hour, minute)), Is.EqualTo(expected));
}
