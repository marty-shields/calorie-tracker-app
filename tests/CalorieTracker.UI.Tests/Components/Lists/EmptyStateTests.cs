using CalorieTracker.UI.Components.Lists;

namespace CalorieTracker.UI.Tests.Components.Lists;

public class EmptyStateTests : ComponentTestBase
{
    [Test]
    public void Render_ShowsMessageAndContent()
    {
        var cut = Context.Render<EmptyState>(p => p
            .Add(x => x.Message, "Nothing here yet")
            .AddChildContent("<button>Add</button>"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find(".message").TextContent, Is.EqualTo("Nothing here yet"));
            Assert.That(cut.Find("button").TextContent, Is.EqualTo("Add"));
        });
    }

    [Test]
    public void Render_WithHint_ShowsHintUnderMessage()
    {
        var cut = Context.Render<EmptyState>(p => p
            .Add(x => x.Message, "Nothing here yet")
            .Add(x => x.Hint, "Tap Add to log one")
            .Add(x => x.TestId, "empty"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=empty-message]").TextContent, Is.EqualTo("Nothing here yet"));
            Assert.That(cut.Find("[data-testid=empty-hint]").TextContent, Is.EqualTo("Tap Add to log one"));
        });
    }

    [Test]
    public void Render_WithoutHint_ShowsNoHint()
    {
        var cut = Context.Render<EmptyState>(p => p
            .Add(x => x.Message, "Nothing here yet")
            .Add(x => x.TestId, "empty"));

        Assert.That(cut.FindAll("[data-testid=empty-hint]"), Is.Empty);
    }
}
