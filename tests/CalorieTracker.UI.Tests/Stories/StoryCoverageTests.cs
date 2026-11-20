using System.Text.RegularExpressions;
using CalorieTracker.UI.Screens;
using CalorieTracker.UI.Stories;
using Microsoft.AspNetCore.Components;

namespace CalorieTracker.UI.Tests.Stories;

public partial class StoryCoverageTests
{
    private static readonly Type[] UiTypes = typeof(DashboardView).Assembly.GetExportedTypes();

    private static IEnumerable<TestCaseData> Components() =>
        UiTypes
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && t.Namespace!.StartsWith("CalorieTracker.UI.Components."))
            .Select(t => (Group: t.Namespace!.Split('.')[^1], Name: t.Name.Split('`')[0]))
            .Select(c => new TestCaseData(c.Group, c.Name).SetArgDisplayNames(c.Name));

    private static IEnumerable<TestCaseData> Views() =>
        UiTypes
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && t.Namespace == "CalorieTracker.UI.Screens" && t.Name.EndsWith("View"))
            .Select(t => new TestCaseData(t.Name).SetArgDisplayNames(t.Name));

    [TestCaseSource(nameof(Components))]
    public void Component_HasOneStoriesFileInItsGroup(string group, string name)
    {
        var titles = StoriesFiles("Components").Where(f => f.Component == name).Select(f => f.Title);

        Assert.That(titles, Is.EqualTo(new[] { $"{StoryTitles.Prefix}Components/{group}/{name}" }));
    }

    [Test]
    public void ComponentStories_ShowOnlyTheirComponent()
    {
        var shown = StoriesFiles("Components").SelectMany(f => f.Shown.Where(s => s != f.Component).Select(s => $"{f.Title}: {s}"));

        Assert.That(shown, Is.Empty);
    }

    [Test]
    public void ComponentStories_AreForComponentsOnly()
    {
        var names = Components().Select(c => (string)c.Arguments[1]!).ToHashSet();

        Assert.That(StoriesFiles("Components").Select(f => f.Component).Where(c => !names.Contains(c)), Is.Empty);
    }

    [TestCaseSource(nameof(Views))]
    public void View_IsShownByAScreenStory(string name)
    {
        Assert.That(StoriesFiles("Screens").SelectMany(f => f.Shown), Has.Member(name));
    }

    [Test]
    public void ScreenStories_ShowOnlyViews()
    {
        var views = Views().Select(v => (string)v.Arguments[0]!).ToHashSet();

        Assert.That(StoriesFiles("Screens").SelectMany(f => f.Shown).Where(s => !views.Contains(s)), Is.Empty);
    }

    [Test]
    public void Stories_AreInFoundationsComponentsOrScreensOfTheApp()
    {
        var titles = StoriesFiles("").Select(f => f.Title.Split('/')).Select(t => $"{t[0]}/{t[1]}").Distinct();

        Assert.That(titles, Is.SubsetOf(new[] { "Foundations", "Components", "Screens" }.Select(section => StoryTitles.Prefix + section)));
    }

    [Test]
    public void Stories_TakeTheirTitleFromTheOnePrefixConstant()
    {
        var literal = RepoFiles.Find("*.stories.razor", "src", "CalorieTracker.UI.Stories", "Stories")
            .Where(f => !StoriesTitle().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(RepoFiles.Root, f));

        Assert.That(literal, Is.Empty);
    }

    /// <summary>
    /// The stories files in a folder under Stories: title, TComponent, and the component each story's template starts with and
    /// any view in its overlay.
    /// </summary>
    private static IEnumerable<(string Title, string Component, IReadOnlyList<string> Shown)> StoriesFiles(string folder) =>
        RepoFiles.Find("*.stories.razor", "src", "CalorieTracker.UI.Stories", "Stories", folder)
            .Select(File.ReadAllText)
            .Select(source => (
                StoryTitles.Prefix + StoriesTitle().Match(source).Groups[1].Value,
                TComponent().Match(source).Groups[1].Value,
                (IReadOnlyList<string>)Shown().Matches(source).Concat(ShownOver().Matches(source)).Select(m => m.Groups[1].Value).ToList()));

    [GeneratedRegex("""\[Stories\(StoryTitles\.Prefix \+ "([^"]+)"\)\]""")]
    private static partial Regex StoriesTitle();

    [GeneratedRegex(@"<Stories TComponent=""([A-Za-z]+)(?:<[^""]*>)?""")]
    private static partial Regex TComponent();

    [GeneratedRegex(@"<Template>\s*(?:@\{[^\n]*\}\s*)?<([A-Za-z]+)")]
    private static partial Regex Shown();

    /// <summary>A view shown in a screen's overlay, e.g. a picker over a form.</summary>
    [GeneratedRegex(@"<Overlay>\s*<([A-Za-z]+View)\b")]
    private static partial Regex ShownOver();
}
