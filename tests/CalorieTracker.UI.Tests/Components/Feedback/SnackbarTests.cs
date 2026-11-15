using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Components.Layout;
using CalorieTracker.UI.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CalorieTracker.UI.Tests.Components.Feedback;

public class SnackbarTests : ComponentTestBase
{
    private SnackbarService Service => Context.Services.GetRequiredService<SnackbarService>();

    private FakeTimeProvider Clock => (FakeTimeProvider)Context.Services.GetRequiredService<TimeProvider>();

    [Test]
    public void NoMessage_RendersNothing()
    {
        var cut = Context.Render<Snackbar>();

        Assert.That(cut.Markup.Trim(), Is.Empty);
    }

    [Test]
    public async Task MessageOnly_ShowsTheTextAndNoAction()
    {
        var cut = Context.Render<Snackbar>();

        await Context.Renderer.Dispatcher.InvokeAsync(() => Service.Show("Entry added"));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=snackbar-message]").TextContent, Is.EqualTo("Entry added"));
            Assert.That(cut.FindAll("[data-testid=snackbar-action]"), Is.Empty);
        });
    }

    [Test]
    public async Task Action_FiresOnceThenDismisses()
    {
        var fired = 0;
        var cut = Context.Render<Snackbar>();
        await Context.Renderer.Dispatcher.InvokeAsync(() => Service.Show("Entry deleted", "Undo", () =>
        {
            fired++;
            return Task.CompletedTask;
        }));
        var shown = Service.Current!;
        var action = cut.Find("[data-testid=snackbar-action]");

        Assert.That(action.TextContent, Is.EqualTo("Undo"));
        action.Click();
        await WaitForRendererAsync();
        await Service.InvokeActionAsync(shown);

        Assert.Multiple(() =>
        {
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=snackbar]"), Is.Empty);
            Assert.That(Service.Current, Is.Null);
        });
    }

    [Test]
    public void Action_IsANativeButtonSoTheKeyboardReachesIt()
    {
        var cut = Context.Render<Snackbar>();
        Service.Show("Entry deleted", "Undo", () => Task.CompletedTask);
        cut.WaitForState(() => cut.FindAll("[data-testid=snackbar-action]").Count == 1);

        var action = cut.Find("[data-testid=snackbar-action]");

        Assert.Multiple(() =>
        {
            Assert.That(action.TagName, Is.EqualTo("BUTTON"));
            Assert.That(action.GetAttribute("tabindex"), Is.Null);
            Assert.That(action.HasAttribute("disabled"), Is.False);
        });
    }

    [Test]
    public async Task Timeout_DismissesAfterTheDefaultDuration()
    {
        var cut = Context.Render<Snackbar>();
        await Context.Renderer.Dispatcher.InvokeAsync(() => Service.Show("Entry added"));

        Clock.Advance(SnackbarService.DefaultDuration - TimeSpan.FromMilliseconds(1));
        Assert.That(cut.FindAll("[data-testid=snackbar]"), Has.Count.EqualTo(1));

        Clock.Advance(TimeSpan.FromMilliseconds(1));
        cut.WaitForState(() => cut.FindAll("[data-testid=snackbar]").Count == 0);

        Assert.That(Service.Current, Is.Null);
    }

    [Test]
    public void DefaultDuration_IsFiveSeconds() =>
        Assert.That(SnackbarService.DefaultDuration, Is.EqualTo(TimeSpan.FromSeconds(5)));

    [Test]
    public async Task NewMessage_ReplacesTheOldOneAndRestartsTheTimeout()
    {
        var oldFired = 0;
        var cut = Context.Render<Snackbar>();
        await Context.Renderer.Dispatcher.InvokeAsync(() => Service.Show("First", "Undo", () =>
        {
            oldFired++;
            return Task.CompletedTask;
        }));
        Clock.Advance(TimeSpan.FromSeconds(4));

        await Context.Renderer.Dispatcher.InvokeAsync(() => Service.Show("Second"));
        Clock.Advance(TimeSpan.FromSeconds(4));

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=snackbar-message]").Select(e => e.TextContent), Is.EqualTo(new[] { "Second" }));
            Assert.That(cut.FindAll("[data-testid=snackbar-action]"), Is.Empty);
            Assert.That(oldFired, Is.Zero);
        });

        Clock.Advance(TimeSpan.FromSeconds(1));
        cut.WaitForState(() => cut.FindAll("[data-testid=snackbar]").Count == 0);

        Assert.That(Service.Current, Is.Null);
    }

    [Test]
    public async Task ClickOnAReplacedMessage_DoesNothingToTheNewOne()
    {
        var oldFired = 0;
        var newFired = 0;
        await Context.Renderer.Dispatcher.InvokeAsync(() => Service.Show("First", "Undo", () =>
        {
            oldFired++;
            return Task.CompletedTask;
        }));
        var stale = Service.Current!;
        await Context.Renderer.Dispatcher.InvokeAsync(() => Service.Show("Second", "Redo", () =>
        {
            newFired++;
            return Task.CompletedTask;
        }));

        await Service.InvokeActionAsync(stale);

        Assert.Multiple(() =>
        {
            Assert.That(oldFired, Is.Zero);
            Assert.That(newFired, Is.Zero);
            Assert.That(Service.Current?.Text, Is.EqualTo("Second"));
        });
    }

    [Test]
    public void NegativeDuration_ThrowsAndLeavesTheShowingMessage()
    {
        Service.Show("First", duration: Timeout.InfiniteTimeSpan);

        Assert.Throws<ArgumentOutOfRangeException>(() => Service.Show("Second", duration: TimeSpan.FromSeconds(-1)));

        Assert.That(Service.Current?.Text, Is.EqualTo("First"));
    }

    [Test]
    public void WhitespaceLabel_DropsTheAction()
    {
        Service.Show("Entry added", "  ", () => Task.CompletedTask);

        Assert.Multiple(() =>
        {
            Assert.That(Service.Current!.ActionLabel, Is.Null);
            Assert.That((object?)Service.Current.OnAction, Is.Null);
        });
    }

    [Test]
    public void LabelWithoutAHandler_DropsTheAction()
    {
        Service.Show("Entry added", "Undo");

        Assert.Multiple(() =>
        {
            Assert.That(Service.Current!.ActionLabel, Is.Null);
            Assert.That((object?)Service.Current.OnAction, Is.Null);
        });
    }

    [Test]
    public async Task MessagePostedBeforeAScreenChange_ShowsOnTheNextScreen()
    {
        Service.Show("Entry deleted", "Undo", () => Task.CompletedTask);

        var next = Context.Render<AppScreen>(p => p
            .Add(x => x.Title, "Foods")
            .Add(x => x.Destination, NavDestination.Dashboard)
            .Add(x => x.Status, (RenderFragment)(b =>
            {
                b.OpenComponent<Snackbar>(0);
                b.CloseComponent();
            })));
        await WaitForRendererAsync();

        Assert.That(next.Find("[data-testid=snackbar-message]").TextContent, Is.EqualTo("Entry deleted"));
    }

    [Test]
    public void InAppScreen_SitsInThePoliteStatusRegion()
    {
        var cut = Context.Render<AppScreen>(p => p
            .Add(x => x.Title, "Foods")
            .Add(x => x.Destination, NavDestination.Dashboard)
            .Add(x => x.Status, (RenderFragment)(b =>
            {
                b.OpenComponent<Snackbar>(0);
                b.CloseComponent();
            })));
        Service.Show("Entry deleted", "Undo", () => Task.CompletedTask);
        cut.WaitForState(() => cut.FindAll("[data-testid=snackbar]").Count == 1);

        var region = cut.Find(".status-slot");
        Assert.Multiple(() =>
        {
            Assert.That(region.GetAttribute("aria-live"), Is.EqualTo("polite"));
            Assert.That(region.QuerySelector("[data-testid=snackbar-action]"), Is.Not.Null);
        });
    }

    [Test]
    public async Task OwnService_ShowsInsteadOfTheAppsAndDoesNotTimeOutWhenInfinite()
    {
        using var own = new SnackbarService(Clock);
        own.Show("Story", duration: Timeout.InfiniteTimeSpan);
        var cut = Context.Render<Snackbar>(p => p.Add(x => x.Service, own));
        await WaitForRendererAsync();

        Clock.Advance(TimeSpan.FromHours(1));

        Assert.Multiple(() =>
        {
            Assert.That(cut.Find("[data-testid=snackbar-message]").TextContent, Is.EqualTo("Story"));
            Assert.That(Service.Current, Is.Null);
        });
    }

    [Test]
    public void ActionWithoutALabel_IsDropped()
    {
        Service.Show("Entry added", null, () => Task.CompletedTask);

        var message = Service.Current;
        Assert.That(message, Is.Not.Null);
        Assert.That((object?)message!.OnAction, Is.Null);
    }
}
