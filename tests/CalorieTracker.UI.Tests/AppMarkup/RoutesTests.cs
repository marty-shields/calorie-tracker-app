namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>The App's Routes.razor gives the kit's start-up gate and error boundary the app's text. The App can't be referenced, so its file is read.</summary>
public class RoutesTests
{
    private static string Routes => RepoFiles.Read("src", "CalorieTracker.App", "Components", "Routes.razor");

    [Test]
    public void Routes_GivesTheStartupGateTheAppNameAndTheFailureText() =>
        Assert.That(Routes, Does.Contain("<StartupGate Initialize=\"InitializeAsync\" Title=\"Calorie Tracker\" FailedMessage=\"Couldn't open the database\">"));

    [Test]
    public void Routes_GivesTheErrorBoundaryTheAppName() =>
        Assert.That(Routes, Does.Contain("<AppErrorBoundary Title=\"Calorie Tracker\""));

    [Test]
    public void Routes_RestartsOnTheStartDestination() =>
        Assert.That(Routes, Does.Contain("OnRestart=\"() => Navigation.GoTo(string.Empty)\""));
}
