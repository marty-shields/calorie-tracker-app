namespace CalorieTracker.UI.Tests.DesignSystem;

public class KitComponentsTests
{
    private static readonly string[] KitComponents =
    [
        "AppButton", "ButtonVariant", "Icon", "IconName", "IconSize", "IconTone", "IconButton", "IconButtonStyle",
        "Spinner", "LoadFailed", "Snackbar", "SnackbarService",
        "SegmentedControl", "FilterChips", "NumberField", "TextField", "SelectField", "DateStrip", "DayLabel", "RadioOption",
        "ListRow", "EmptyState", "SummaryCard", "StatTile", "ChartLegend", "LegendItem", "LegendKind",
        "StartupGate", "StartupView", "AppErrorBoundary", "ErrorView", "BackHandler", "BackButton", "IBackButton",
        "AppLifecycle", "IAppLifecycle", "IAppStart", "AppScreen", "BottomNav", "BottomNavItem",
    ];

    [TestCaseSource(nameof(KitComponents))]
    public void Component_IsTheKitsNotACopyInTheApp(string name) =>
        Assert.That(
            RepoFiles.Find($"{name}.*", "src", "CalorieTracker.UI").Select(f => Path.GetRelativePath(RepoFiles.Root, f)),
            Is.Empty);
}
