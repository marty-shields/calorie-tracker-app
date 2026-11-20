using CalorieTracker.UI.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Shields.UI.Lifecycle;

namespace CalorieTracker.UI.Tests.Lifecycle;

public class AppLifecycleBridgeTests
{
    private readonly object activity = new();
    private AppLifecycleBridge bridge = null!;
    private AppLifecycle scoped = null!;
    private int started;

    [SetUp]
    public void Create()
    {
        bridge = new AppLifecycleBridge();
        scoped = NewLifecycle();
        started = 0;
    }

    private static AppLifecycle NewLifecycle() => new(NullLogger<AppLifecycle>.Instance);

    private void RegisterStart(AppLifecycle lifecycle) => lifecycle.Register(() =>
    {
        started++;
        return Task.CompletedTask;
    });

    [Test]
    public async Task Connect_AfterAttach_MakesTheCurrentActivityCurrentInTheScope()
    {
        bridge.Attach(activity);
        bridge.Connect(scoped);
        RegisterStart(scoped);
        bridge.Detach(activity);

        await bridge.StartedAsync();

        Assert.That(started, Is.Zero, "detach reached the scope, so it cleared the handler");
    }

    [Test]
    public async Task StartedAsync_RunsTheScopesHandlers()
    {
        bridge.Attach(activity);
        bridge.Connect(scoped);
        RegisterStart(scoped);

        await bridge.StartedAsync();

        Assert.That(started, Is.EqualTo(1));
    }

    [Test]
    public async Task StartedAsync_BeforeConnect_DoesNothing()
    {
        bridge.Attach(activity);

        await bridge.StartedAsync();

        Assert.That(started, Is.Zero);
    }

    [Test]
    public async Task Attach_RemovesTheEarlierHandlers()
    {
        bridge.Connect(scoped);
        RegisterStart(scoped);

        bridge.Attach(activity);
        await bridge.StartedAsync();

        Assert.That(started, Is.Zero);
    }

    [Test]
    public async Task Disconnect_OfAStaleScope_KeepsTheNewOne()
    {
        var current = NewLifecycle();
        bridge.Attach(activity);
        bridge.Connect(scoped);
        bridge.Connect(current);
        RegisterStart(current);

        bridge.Disconnect(scoped);
        await bridge.StartedAsync();

        Assert.That(started, Is.EqualTo(1));
    }

    [Test]
    public async Task Disconnect_OfTheConnectedScope_StopsStarts()
    {
        bridge.Attach(activity);
        bridge.Connect(scoped);
        RegisterStart(scoped);

        bridge.Disconnect(scoped);
        await bridge.StartedAsync();

        Assert.That(started, Is.Zero);
    }

    [Test]
    public async Task Detach_OfAnEarlierActivity_IsIgnored()
    {
        bridge.Attach(new object());
        bridge.Attach(activity);
        bridge.Connect(scoped);
        RegisterStart(scoped);

        bridge.Detach(new object());
        await bridge.StartedAsync();

        Assert.That(started, Is.EqualTo(1));
    }

    [Test]
    public async Task Detach_OfTheCurrentActivity_DisconnectsTheScopeSoALaterActivityNeverReachesIt()
    {
        bridge.Attach(activity);
        bridge.Connect(scoped);
        bridge.Detach(activity);

        bridge.Attach(new object());
        RegisterStart(scoped);
        await bridge.StartedAsync();

        Assert.That(started, Is.Zero, "the old scope is no longer followed, so it is neither attached nor started");
    }
}
