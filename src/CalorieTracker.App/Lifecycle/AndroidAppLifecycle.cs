using Shields.UI.Lifecycle;

namespace CalorieTracker.App.Lifecycle;

/// <summary>
/// Passes the activity's create, start and destroy to the kit's <see cref="IAppLifecycle"/>.
/// The lifecycle is scoped to the BlazorWebView, so <c>Routes</c> hands it over with <see cref="Connect"/> once its scope exists, and takes it back with <see cref="Disconnect(IAppLifecycle)"/>.
/// </summary>
internal sealed class AndroidAppLifecycle
{
    private Android.App.Activity? current;
    private IAppLifecycle? lifecycle;

    /// <summary>Makes a created activity current, which removes the earlier activity's handlers.</summary>
    public void Attach(Android.App.Activity activity)
    {
        current = activity;
        lifecycle?.Attach(activity);
    }

    /// <summary>Follows the lifecycle of the scope the pages use. Called before they register, so it also makes the current activity current there.</summary>
    public void Connect(IAppLifecycle scoped)
    {
        lifecycle = scoped;
        if (current is not null)
        {
            scoped.Attach(current);
        }
    }

    /// <summary>Stops following the connected lifecycle, if it is the one given.</summary>
    public void Disconnect(IAppLifecycle scoped)
    {
        if (ReferenceEquals(lifecycle, scoped))
        {
            lifecycle = null;
        }
    }

    /// <summary>Runs the pages' start handlers. Does nothing before the pages exist: they run their own first load.</summary>
    public Task StartedAsync() => lifecycle?.NotifyStartedAsync() ?? Task.CompletedTask;

    /// <summary>Removes the handlers when the current activity is destroyed. An earlier activity is ignored.</summary>
    public void Detach(Android.App.Activity activity)
    {
        if (ReferenceEquals(activity, current))
        {
            current = null;
            lifecycle?.Detach(activity);
        }
    }
}
