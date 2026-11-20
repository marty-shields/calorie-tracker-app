using Shields.UI.Components.Layout;

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
}

public class StartupGateAppTextTests : ComponentTestBase
{
    [Test]
    public void Failed_ShowsTheAppsTitleAndMessageWithTryAgain()
    {
        var cut = Context.Render<StartupGate>(p => p
            .Add(x => x.Initialize, () => Task.FromException(new InvalidOperationException()))
            .Add(x => x.Title, "Calorie Tracker")
            .Add(x => x.FailedMessage, "Couldn't open the database"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Calorie Tracker"));
            Assert.That(cut.Markup, Does.Contain("Couldn't open the database"));
            Assert.That(cut.FindAll("[data-testid=startup-retry]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ErrorBoundary_ShowsTheErrorMessageAndRestart()
    {
        var cut = Context.Render<AppErrorBoundary>(p => p
            .Add(x => x.Title, "Calorie Tracker")
            .AddChildContent<ThrowOnRender>());

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=error-message]").TextContent, Is.EqualTo("Something went wrong"));
            Assert.That(cut.FindAll("[data-testid=error-restart]"), Has.Count.EqualTo(1));
        });
    }

    private sealed class ThrowOnRender : Microsoft.AspNetCore.Components.ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) =>
            throw new InvalidOperationException();
    }
}
