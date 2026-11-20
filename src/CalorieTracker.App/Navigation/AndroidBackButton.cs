using AndroidX.Activity;
using Shields.UI.Navigation;

namespace CalorieTracker.App.Navigation;

/// <summary>
/// Passes the Android back button to the kit's <see cref="IBackButton"/> while a handler is registered.
/// Without one, the callback is off and Android handles back: the app closes.
/// The back button is scoped to the BlazorWebView, so <c>Routes</c> hands it over with <see cref="Connect"/> once its scope exists, and takes it back with <see cref="Disconnect(IBackButton)"/>.
/// </summary>
internal sealed class AndroidBackButton
{
    private ComponentActivity? current;
    private Callback? callback;
    private IBackButton? backButton;

    /// <summary>Adds the back callback to a created activity, which replaces any earlier one.</summary>
    public void Attach(ComponentActivity activity)
    {
        current = activity;
        callback = new Callback(this, activity.OnBackPressedDispatcher);
        activity.OnBackPressedDispatcher.AddCallback(activity, callback);
        backButton?.Attach(activity);
        Update();
    }

    /// <summary>
    /// Follows the back button of the scope the pages use. Called before they register, so it also makes the current activity current there.
    /// </summary>
    public void Connect(IBackButton scoped)
    {
        Disconnect();
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
            Disconnect();
            Update();
        }
    }

    /// <summary>
    /// Stops following the handlers when the current activity is destroyed, and removes them: its pages are not disposed with it.
    /// An earlier activity is ignored. The dispatcher removes the callback itself.
    /// </summary>
    public void Detach(Android.App.Activity activity)
    {
        if (!ReferenceEquals(activity, current))
        {
            return;
        }

        current = null;
        callback = null;
        backButton?.Detach(activity);
    }

    private void Disconnect()
    {
        if (backButton is not null)
        {
            backButton.Changed -= Update;
            backButton = null;
        }
    }

    private void Update() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (callback is not null)
        {
            callback.Enabled = backButton?.HasHandler == true;
        }
    });

    private sealed class Callback(AndroidBackButton owner, OnBackPressedDispatcher dispatcher) : OnBackPressedCallback(false)
    {
        // The handler can be removed between the gesture starting and back being pressed; Android then handles it.
        public override void HandleOnBackPressed()
        {
            if (owner.backButton is { HasHandler: true } scoped)
            {
                _ = scoped.HandleAsync();
                return;
            }

            Enabled = false;
            dispatcher.OnBackPressed();
        }
    }
}
