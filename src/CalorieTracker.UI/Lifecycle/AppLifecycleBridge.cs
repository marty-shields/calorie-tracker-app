using Shields.UI.Lifecycle;

namespace CalorieTracker.UI.Lifecycle;

/// <summary>
/// Joins the host's current activity to the kit's <see cref="IAppLifecycle"/>, which is scoped to the BlazorWebView.
/// The host's <c>Routes</c> hands the scoped lifecycle over with <see cref="Connect"/> once its scope exists and takes it back with <see cref="Disconnect(IAppLifecycle)"/>.
/// </summary>
public sealed class AppLifecycleBridge
{
    private object? current;
    private IAppLifecycle? lifecycle;

    /// <summary>Makes a created activity current, which removes the earlier activity's handlers.</summary>
    public void Attach(object activity)
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

    /// <summary>Removes the handlers and stops following the lifecycle when the current activity is destroyed: its pages are not disposed with it. An earlier activity is ignored.</summary>
    public void Detach(object activity)
    {
        if (ReferenceEquals(activity, current))
        {
            current = null;
            lifecycle?.Detach(activity);
            lifecycle = null;
        }
    }
}
