using CalorieTracker.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CalorieTracker.UI.Tests;

/// <summary>
/// Creates a fresh bUnit context for each test with the UI's services (<see cref="UiServiceCollectionExtensions.AddUi"/>),
/// a <see cref="BackButton"/> and a <see cref="FakeTimeProvider"/> that never moves.
/// Tests may register their own services over these.
/// </summary>
public abstract class ComponentTestBase
{
    protected BunitContext Context { get; private set; } = null!;

    /// <summary>The back button the rendered components register with.</summary>
    protected BackButton BackButton { get; private set; } = null!;

    [SetUp]
    public void CreateContext()
    {
        Context = new BunitContext();
        BackButton = new BackButton(NullLogger<BackButton>.Instance);
        Context.Services.AddUi();
        Context.Services.AddSingleton<IBackButton>(BackButton);
        Context.Services.AddSingleton<TimeProvider>(new FakeTimeProvider());
    }

    [TearDown]
    public void DisposeContext() => Context.Dispose();

    /// <summary>
    /// Waits until the renderer has run the work queued so far, such as a component continuing after a fake call completes or
    /// a timer ticks. bUnit's synchronous events (<c>Click()</c>, <c>Input()</c>) are queued, not run, while the renderer is busy.
    /// </summary>
    protected Task WaitForRendererAsync() => Context.Renderer.Dispatcher.InvokeAsync(() => { });
}
