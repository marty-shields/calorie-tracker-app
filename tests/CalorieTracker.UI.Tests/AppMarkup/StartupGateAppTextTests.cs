using Shields.UI.Components.Layout;

namespace CalorieTracker.UI.Tests.AppMarkup;

/// <summary>The kit's start-up gate and error boundary show the app's name and text when the App gives it.</summary>
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
            Assert.That(cut.Find("[data-testid=startup-top-bar]").QuerySelector("h1"), Is.Not.Null, "the title sits in the kit's top bar");
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
            Assert.That(cut.Find("h1").TextContent, Is.EqualTo("Calorie Tracker"));
            Assert.That(cut.Find("[data-testid=error-top-bar]").QuerySelector("h1"), Is.Not.Null, "the title sits in the kit's top bar");
            Assert.That(cut.Find("[data-testid=error-bottom-bar]").QuerySelector("[data-testid=error-restart]"), Is.Not.Null, "Restart sits in the kit's bottom bar");
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
