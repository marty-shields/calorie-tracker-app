namespace CalorieTracker.UI.Tests.DesignSystem;

public class KitComponentsTests
{
    private static readonly string[] KitComponents =
    [
        "AppButton", "ButtonVariant", "Icon", "IconName", "IconSize", "IconTone", "IconButton", "IconButtonStyle",
        "Spinner", "LoadFailed", "Snackbar", "SnackbarService",
    ];

    [TestCaseSource(nameof(KitComponents))]
    public void Component_IsTheKitsNotACopyInTheApp(string name) =>
        Assert.That(
            RepoFiles.Find($"{name}.*", "src", "CalorieTracker.UI").Select(f => Path.GetRelativePath(RepoFiles.Root, f)),
            Is.Empty);
}
