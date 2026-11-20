using Shields.UI.Navigation;

namespace CalorieTracker.UI.Navigation;

/// <summary>
/// Joins the host's current activity to the kit's <see cref="IBackButton"/>, which is scoped to the BlazorWebView.
/// The host's <c>Routes</c> hands the scoped back button over with <see cref="Connect"/> once its scope exists and takes it back with <see cref="Disconnect(IBackButton)"/>.
/// The host turns its back callback on and off with <see cref="Enabled"/>: without a handler Android handles back and the app closes.
/// </summary>
public sealed class BackButtonBridge
{
    private object? current;
    private IBackButton? backButton;

    /// <summary>Raised when <see cref="Enabled"/> may have changed; any thread.</summary>
    public event Action? EnabledChanged;

    /// <summary>True when an activity is current and a page registered a back handler.</summary>
    public bool Enabled => current is not null && backButton?.HasHandler == true;

    /// <summary>Makes a created activity current, which replaces any earlier one and removes its handlers.</summary>
    public void Attach(object activity)
    {
        current = activity;
        backButton?.Attach(activity);
        EnabledChanged?.Invoke();
    }

    /// <summary>Follows the back button of the scope the pages use. Called before they register, so it also makes the current activity current there.</summary>
    public void Connect(IBackButton scoped)
    {
        Release();
        backButton = scoped;
        scoped.Changed += Update;
        if (current is not null)
        {
            scoped.Attach(current);
        }

        Update();
    }

    /// <summary>Stops following the connected back button, if it is the one given.</summary>
    public void Disconnect(IBackButton scoped)
    {
        if (ReferenceEquals(backButton, scoped))
        {
            Release();
            Update();
        }
    }

    /// <summary>
    /// Removes the handlers and stops following the back button when the current activity is destroyed: its pages are not disposed with it,
    /// and a later activity must not reach them. An earlier activity is ignored.
    /// </summary>
    public void Detach(object activity)
    {
        if (!ReferenceEquals(activity, current))
        {
            return;
        }

        current = null;
        backButton?.Detach(activity);
        Release();
        Update();
    }

    /// <summary>Runs the newest handler. Null when there is none, so the host lets Android handle back: the handler can be removed between the gesture starting and back being pressed.</summary>
    public Task? HandleAsync() => backButton is { HasHandler: true } scoped ? scoped.HandleAsync() : null;

    private void Release()
    {
        if (backButton is not null)
        {
            backButton.Changed -= Update;
            backButton = null;
        }
    }

    private void Update() => EnabledChanged?.Invoke();
}
