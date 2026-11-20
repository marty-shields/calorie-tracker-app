using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shields.UI.Navigation;

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

    /// <summary>The temporary database behind <see cref="UseFavourites"/>; null until a test asks for it.</summary>
    private FavouriteDatabase? favourites;

    [TearDown]
    public void DisposeContext() => Context.Dispose();

    [TearDown]
    public async Task DisposeFavourites()
    {
        if (favourites is not null)
        {
            await favourites.DisposeAsync();
        }
    }

    /// <summary>Registers Core's real <see cref="CalorieTracker.Core.Foods.FavouriteService"/> over a temporary database, for a page that needs it. Call once, from a test's set-up.</summary>
    protected async Task<FavouriteDatabase> UseFavourites()
    {
        favourites = await FavouriteDatabase.CreateAsync();
        Context.Services.AddSingleton(favourites.Service);
        return favourites;
    }

    /// <summary>
    /// Waits until the renderer has run the work queued so far, such as a component continuing after a fake call completes or
    /// a timer ticks. bUnit's synchronous events (<c>Click()</c>, <c>Input()</c>) are queued, not run, while the renderer is busy.
    /// </summary>
    protected Task WaitForRendererAsync() => Context.Renderer.Dispatcher.InvokeAsync(() => { });
}
