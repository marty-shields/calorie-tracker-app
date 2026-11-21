namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>
/// The UI project holds screens and food-specific rows only; any other component is requested in app-ui-kit.
/// Screens (<c>*View</c>, <c>*Page</c> and their helpers under <c>Screens/</c>) need no entry. The App and Stories hosts are not scanned: they only wire the kit's shell.
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
        ["Navigation/NavScreen.razor"] = FoodSpecific + ": binds the kit's AppScreen to this app's destinations",
    };

    /// <summary>Components outside <c>Screens/</c> in a folder, as paths relative to it with <c>/</c> separators.</summary>
    internal static IReadOnlyList<string> Components(IEnumerable<string> files, string folder) =>
        files
            .Select(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !path.StartsWith("Screens/", StringComparison.Ordinal) && !Path.GetFileName(path).StartsWith('_'))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<string> Found() => Components(RepoFiles.Find("*.razor", "src", "CalorieTracker.UI"), UiFolder);

    [Test]
    public void Components_AreAllOnTheAllowList() =>
        Assert.That(Found().Where(path => !Allowed.ContainsKey(path)), Is.Empty,
            "a general component goes to app-ui-kit; a food-specific one needs an entry with its reason");

    [Test]
    public void AllowList_HasNoMissingComponents() =>
        Assert.That(Allowed.Keys.Where(path => !Found().Contains(path)), Is.Empty,
            "remove the entry: the list only shrinks");

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
}
