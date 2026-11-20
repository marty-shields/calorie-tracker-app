using System.Text.RegularExpressions;
using CalorieTracker.UI.Components.Lists;
using Shields.UI.Components.Actions;

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
    public void Render_ShowsAThousandsSeparator()
    {
        var cut = Context.Render<MealHeader>(p => p
            .Add(x => x.Name, "Snacks")
            .Add(x => x.Calories, 1250)
            .Add(x => x.TestId, "meal"));

        Assert.That(cut.Find("[data-testid=meal-calories]").TextContent, Is.EqualTo("1,250 kcal"));
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
    public void AddButton_IsTheFilledChip()
    {
        var cut = Context.Render<MealHeader>(p => p.Add(x => x.Name, "Lunch").Add(x => x.TestId, "meal"));

        Assert.That(cut.Find("[data-testid=meal-add]").ClassList, Does.Contain("filled"));
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
    public void Stories_ShowEntriesEmptyAndLargeTotal()
    {
        var stories = RepoFiles.Read("src", "CalorieTracker.UI.Stories", "Stories", "Components", "Lists", "MealHeader.stories.razor");

        var withEntries = StoryMarkup(stories, "With entries");
        var empty = StoryMarkup(stories, "Empty");
        var largeTotal = StoryMarkup(stories, "Large total");

        Assert.Multiple(() =>
        {
            Assert.That(withEntries, Does.Contain("Calories=\""));
            Assert.That(empty, Does.Not.Contain("Calories="));
            Assert.That(largeTotal, Does.Contain("Name=\"Snacks\"").And.Contain("Calories=\"1234\""));
        });
    }

    [Test]
    public void Render_VeryLongName_WrapsAndKeepsAddButtonFullSize()
    {
        var longName = string.Join(' ', Enumerable.Repeat("Late night snacks and second helpings", 4));
        var cut = Context.Render<MealHeader>(p => p
            .Add(x => x.Name, longName)
            .Add(x => x.Calories, 1234)
            .Add(x => x.TestId, "meal"));
        var css = RepoFiles.Read("src", "CalorieTracker.UI", "Components", "Lists", "MealHeader.razor.css");
        var name = css.Split(".name {")[1].Split('}')[0];
        var main = css.Split(".main {")[1].Split('}')[0];

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=meal-name]").TextContent, Is.EqualTo(longName));
            Assert.That(cut.Find("[data-testid=meal-name]").HasAttribute("data-wrap-ok"), Is.True);
            Assert.That(name, Does.Contain("min-width: 0"));
            Assert.That(main, Does.Contain("min-width: 0").And.Contain("flex: 1"));
            Assert.That(name, Does.Not.Contain("nowrap").And.Not.Contain("ellipsis"));
            Assert.That(cut.Find("[data-testid=meal-add]").GetAttribute("aria-label"), Is.EqualTo($"Add to {longName}"));
            Assert.That(cut.Find("[data-testid=meal-add]").ClassList, Does.Not.Contain("compact"));
        });
    }

    private static string StoryMarkup(string stories, string storyName)
    {
        var match = Regex.Match(stories, $"<Story Name=\"{Regex.Escape(storyName)}\">(.*?)</Story>", RegexOptions.Singleline);
        Assert.That(match.Success, Is.True, $"Story '{storyName}' is missing");
        return match.Groups[1].Value;
    }

    [Test]
    public void Now_ShowsTheChipAfterTheName()
    {
        var cut = Context.Render<MealHeader>(p => p.Add(x => x.Name, "Dinner").Add(x => x.Now, true).Add(x => x.TestId, "meal"));

        Assert.That(cut.Find("[data-testid=meal-now]").TextContent, Is.EqualTo("Now"));
    }

    [Test]
    public void Hint_ShowsAfterTheName_AndNoChipByDefault()
    {
        var cut = Context.Render<MealHeader>(p => p.Add(x => x.Name, "Dinner").Add(x => x.Hint, "from 17:00").Add(x => x.TestId, "meal"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=meal-hint]").TextContent, Is.EqualTo("from 17:00"));
            Assert.That(cut.FindAll("[data-testid=meal-now]"), Is.Empty);
        });
    }

    [Test]
    public void WithoutOnToggle_TheHeaderIsNotAButton()
    {
        var cut = Context.Render<MealHeader>(p => p.Add(x => x.Name, "Lunch").Add(x => x.Calories, 100).Add(x => x.TestId, "meal"));

        Assert.That(cut.FindAll("[data-testid=meal-toggle]"), Is.Empty);
        Assert.That(cut.FindAll("button"), Has.Count.EqualTo(1), "only the +");
    }

    [TestCase(false, "false")]
    [TestCase(true, "true")]
    public void WithOnToggle_TheHeaderIsAButtonWithAriaExpanded(bool expanded, string aria)
    {
        var raised = 0;
        var cut = Context.Render<MealHeader>(p => p
            .Add(x => x.Name, "Lunch")
            .Add(x => x.Summary, "3 items · 348 kcal")
            .Add(x => x.Expanded, expanded)
            .Add(x => x.OnToggle, () => raised++)
            .Add(x => x.TestId, "meal"));
        var toggle = cut.Find("[data-testid=meal-toggle]");

        toggle.Click();

        Assert.Multiple(() =>
        {
            Assert.That(toggle.TagName, Is.EqualTo("BUTTON"));
            Assert.That(toggle.GetAttribute("aria-expanded"), Is.EqualTo(aria));
            Assert.That(cut.Find("[data-testid=meal-summary]").TextContent, Is.EqualTo("3 items · 348 kcal"));
            Assert.That(raised, Is.EqualTo(1));
            var icon = Context.Render<Icon>(p => p.Add(x => x.Name, expanded ? IconName.Up : IconName.Down));
            Assert.That(toggle.QuerySelector("svg path")!.GetAttribute("d"), Is.EqualTo(icon.Find("path").GetAttribute("d")));
        });
    }

    [Test]
    public void Summary_TakesThePlaceOfTheTotal()
    {
        var cut = Context.Render<MealHeader>(p => p.Add(x => x.Name, "Lunch").Add(x => x.Calories, 348).Add(x => x.Summary, "3 items · 348 kcal").Add(x => x.TestId, "meal"));

        Assert.That(cut.FindAll("[data-testid=meal-calories]"), Is.Empty);
    }
}
