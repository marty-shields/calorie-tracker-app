using CalorieTracker.UI.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>
/// The UI project holds screens and food-specific rows only; any other component is requested in app-ui-kit.
/// Screens (<c>*View.razor</c> and <c>*Page.razor</c> under <c>Screens/</c>) need no entry; any other file there, and any component class written in C#, does. The App and Stories hosts are not scanned: they only wire the kit's shell.
/// </summary>
public class AppComponentsAllowListTests
{
    private const string FoodSpecific = "food-specific";
    private const string MovesToKit = "moves to the kit: #164";

    private static readonly string UiFolder = RepoFiles.PathOf("src", "CalorieTracker.UI");

    /// <summary>Components outside <c>Screens/</c>, keyed by path relative to the UI project, with why each stays.</summary>
    private static readonly IReadOnlyDictionary<string, string> Allowed = new Dictionary<string, string>
    {
        ["Components/Charts/CalorieRing.razor"] = MovesToKit,
        ["Components/Charts/TrendBarChart.razor"] = MovesToKit,
        ["Components/Charts/MonthCalendar.razor"] = MovesToKit,
        ["Components/Charts/BarList.razor"] = MovesToKit,
        ["Components/Charts/EnergyShareBar.razor"] = MovesToKit,
        ["Components/Layout/AddSheet.razor"] = MovesToKit,
        ["Components/Lists/MealHeader.razor"] = MovesToKit,
        ["Screens/AddSheetHost.razor"] = FoodSpecific + ": opens this app's AddSheet and adds the usual foods for the day",
        ["Navigation/NavScreen.razor"] = FoodSpecific + ": binds the kit's AppScreen to this app's destinations",
    };

    /// <summary>Components that are not a <c>*View</c> or <c>*Page</c> under <c>Screens/</c>, as paths relative to the folder with <c>/</c> separators.</summary>
    internal static IReadOnlyList<string> Components(IEnumerable<string> files, string folder) =>
        files
            .Select(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !IsScreen(path) && !Path.GetFileName(path).StartsWith('_'))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static bool IsScreen(string path) =>
        path.StartsWith("Screens/", StringComparison.Ordinal)
        && (path.EndsWith("View.razor", StringComparison.Ordinal) || path.EndsWith("Page.razor", StringComparison.Ordinal));

    private static IReadOnlyList<string> Found() => Components(RepoFiles.Find("*.razor", "src", "CalorieTracker.UI"), UiFolder);

    /// <summary>Names of the component classes in an assembly that are not a <c>*View</c> or <c>*Page</c> in a <c>Screens</c> namespace, whatever file or language they come from.</summary>
    internal static IReadOnlyList<string> ComponentTypes(IEnumerable<Type> types) =>
        types
            .Where(type => type is { IsClass: true, IsAbstract: false, IsNested: false } && typeof(IComponent).IsAssignableFrom(type))
            .Where(type => !(type.Namespace?.Split('.').Contains("Screens") == true && (type.Name.EndsWith("View", StringComparison.Ordinal) || type.Name.EndsWith("Page", StringComparison.Ordinal))))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IReadOnlySet<string> AllowedNames() =>
        Allowed.Keys.Select(path => Path.GetFileNameWithoutExtension(path)).ToHashSet(StringComparer.Ordinal);

    [Test]
    public void Components_AreAllOnTheAllowList() =>
        Assert.That(Found().Where(path => !Allowed.ContainsKey(path)), Is.Empty,
            "a general component goes to app-ui-kit; a food-specific one needs an entry with its reason");

    [Test]
    public void ComponentClasses_AreAllOnTheAllowList()
    {
        var allowed = AllowedNames();
        Assert.That(ComponentTypes(typeof(AddSheet).Assembly.GetTypes()).Where(name => !allowed.Contains(name)), Is.Empty,
            "a component class in C# needs the same entry as a .razor file, named after the class");
    }

    [Test]
    public void AllowList_HasNoMissingComponents()
    {
        var found = Found();
        Assert.That(Allowed.Keys.Where(path => !found.Contains(path)), Is.Empty,
            "remove the entry: the list only shrinks");
    }

    [Test]
    public void AllowList_GivesEachComponentAReason() =>
        Assert.That(Allowed.Where(entry => !entry.Value.StartsWith(FoodSpecific, StringComparison.Ordinal) && entry.Value != MovesToKit), Is.Empty);

    [Test]
    public void Components_SkipsScreensAndImports() =>
        Assert.That(
            Components(
                [Path.Combine(UiFolder, "Screens", "TrendsView.razor"), Path.Combine(UiFolder, "_Imports.razor"), Path.Combine(UiFolder, "Components", "Widget.razor")],
                UiFolder),
            Is.EqualTo(new[] { "Components/Widget.razor" }));

    [Test]
    public void Components_ListsHelpersUnderScreensButNotViewsOrPages() =>
        Assert.That(
            Components(
                [Path.Combine(UiFolder, "Screens", "AddSheetHost.razor"), Path.Combine(UiFolder, "Screens", "TrendsPage.razor"), Path.Combine(UiFolder, "Screens", "Sub", "FooView.razor")],
                UiFolder),
            Is.EqualTo(new[] { "Screens/AddSheetHost.razor" }));

    [Test]
    public void ComponentTypes_FlagsAComponentClassOutsideScreensViewsAndPages() =>
        Assert.That(ComponentTypes([typeof(Fake.Widget), typeof(Fake.Screens.FooView), typeof(Fake.Screens.Helper), typeof(Fake.NotAComponent)]),
            Is.EqualTo(new[] { "Helper", "Widget" }));
}
