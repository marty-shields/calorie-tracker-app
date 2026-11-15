using CalorieTracker.UI.Components.Actions;
using CalorieTracker.UI.Components.Lists;

namespace CalorieTracker.UI.Tests.Components.Lists;

public class MealHeaderTests : ComponentTestBase
{
    [Test]
    public void Render_ShowsNameAndCalories()
    {
        var cut = Context.Render<MealHeader>(p => p
            .Add(x => x.Name, "Breakfast")
            .Add(x => x.Calories, 240)
            .Add(x => x.TestId, "meal"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=meal-name]").TextContent, Is.EqualTo("Breakfast"));
            Assert.That(cut.Find("[data-testid=meal-calories]").TextContent, Is.EqualTo("240 kcal"));
        });
    }

    [Test]
    public void Render_WithoutCalories_ShowsNoTotal()
    {
        var cut = Context.Render<MealHeader>(p => p
            .Add(x => x.Name, "Lunch")
            .Add(x => x.TestId, "meal"));

        Assert.That(cut.FindAll("[data-testid=meal-calories]"), Is.Empty);
    }

    [TestCase("Lunch")]
    [TestCase("Snacks")]
    public void AddButton_IsNamedForItsMeal(string meal)
    {
        var cut = Context.Render<MealHeader>(p => p
            .Add(x => x.Name, meal)
            .Add(x => x.TestId, "meal"));

        Assert.That(cut.Find("[data-testid=meal-add]").GetAttribute("aria-label"), Is.EqualTo($"Add to {meal}"));
    }

    [Test]
    public void AddButton_ShowsAPlusIcon()
    {
        var cut = Context.Render<MealHeader>(p => p.Add(x => x.Name, "Lunch").Add(x => x.TestId, "meal"));
        var plus = Context.Render<Icon>(p => p.Add(x => x.Name, IconName.Plus));

        Assert.That(cut.Find("button path").GetAttribute("d"), Is.EqualTo(plus.Find("path").GetAttribute("d")));
    }

    [Test]
    public void AddButton_Click_RaisesOnAdd()
    {
        var added = false;
        var cut = Context.Render<MealHeader>(p => p
            .Add(x => x.Name, "Dinner")
            .Add(x => x.TestId, "meal")
            .Add(x => x.OnAdd, () => added = true));

        cut.Find("[data-testid=meal-add]").Click();

        Assert.That(added, Is.True);
    }

    [Test]
    public void Stories_ShowEntriesEmptyAndLongName()
    {
        var stories = RepoFiles.Read("src", "CalorieTracker.UI.Stories", "Stories", "Components", "Lists", "MealHeader.stories.razor");

        Assert.That(stories, Does.Contain("<Story Name=\"With entries\">")
            .And.Contain("<Story Name=\"Empty\">")
            .And.Contain("<Story Name=\"Long name\">"));
    }
}
