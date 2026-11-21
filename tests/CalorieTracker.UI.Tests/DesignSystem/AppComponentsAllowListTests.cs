using CalorieTracker.UI.Components.Layout;
using Microsoft.AspNetCore.Components;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>
/// The UI project holds screens and food-specific rows only; any other component is requested in app-ui-kit.
/// Screens (<c>*View.razor</c> and <c>*Page.razor</c> under <c>Screens/</c>) need no entry; any other file there, and any component class written in C#, does.
/// The App project is scanned too, except its routed pages (<c>Components/Pages/</c>) and <c>Routes.razor</c>; its entries start with <c>App/</c>. The Stories hosts are not scanned: they only wire the kit's shell.
/// </summary>
public class AppComponentsAllowListTests
{
    private const string FoodSpecific = "food-specific";
    private const string AppWiring = "app wiring";

    private static readonly string UiFolder = RepoFiles.PathOf("src", "CalorieTracker.UI");
    private static readonly string AppFolder = RepoFiles.PathOf("src", "CalorieTracker.App");

    /// <summary>Components outside <c>Screens/</c>, keyed by path relative to the UI project (the App project's start with <c>App/</c>), with why each stays.</summary>
    private static readonly IReadOnlyDictionary<string, string> Allowed = new Dictionary<string, string>
    {
        ["Components/Layout/AddSheet.razor"] = FoodSpecific + ": the meal chips and usual foods inside the kit's Sheet",
        ["Screens/AddSheetHost.razor"] = AppWiring + ": opens this app's AddSheet and adds the usual foods for the day",
        ["Navigation/NavScreen.razor"] = AppWiring + ": binds the kit's AppScreen to this app's destinations",
        ["App/Components/SystemBarInsets.cs"] = AppWiring + ": hands the Android system bar insets to the page",
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

    /// <summary>Components of the App project that are not a routed page or <c>Routes.razor</c>: its .razor files, and the .cs files that derive from <c>ComponentBase</c>.</summary>
    internal static IReadOnlyList<string> AppComponents(IEnumerable<string> razorFiles, IEnumerable<string> sourceFiles, string folder) =>
        razorFiles.Concat(sourceFiles.Where(file => File.ReadAllText(file).Contains(": ComponentBase", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !path.StartsWith("Components/Pages/", StringComparison.Ordinal) && path != "Components/Routes.razor" && !Path.GetFileName(path).StartsWith('_'))
            .Select(path => "App/" + path)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> Found() =>
        [.. Components(RepoFiles.Find("*.razor", "src", "CalorieTracker.UI"), UiFolder), .. AppComponents(RepoFiles.Find("*.razor", "src", "CalorieTracker.App"), RepoFiles.Find("*.cs", "src", "CalorieTracker.App"), AppFolder)];

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
        Assert.That(Allowed.Where(entry => !entry.Value.StartsWith(FoodSpecific, StringComparison.Ordinal) && !entry.Value.StartsWith(AppWiring, StringComparison.Ordinal)), Is.Empty);

    [Test]
    public void AppComponents_SkipsPagesRoutesAndImportsAndFindsComponentClasses()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"app-components-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(folder, "Components"));
        var widget = Path.Combine(folder, "Components", "Widget.cs");
        var helper = Path.Combine(folder, "Components", "Helper.cs");
        File.WriteAllText(widget, "public sealed class Widget : ComponentBase;");
        File.WriteAllText(helper, "public static class Helper;");
        try
        {
            Assert.That(
                AppComponents(
                    [Path.Combine(folder, "Components", "Pages", "Home.razor"), Path.Combine(folder, "Components", "Routes.razor"), Path.Combine(folder, "Components", "_Imports.razor"), Path.Combine(folder, "Components", "Shell.razor")],
                    [widget, helper],
                    folder),
                Is.EqualTo(new[] { "App/Components/Shell.razor", "App/Components/Widget.cs" }));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

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
