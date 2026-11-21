using AndroidX.Activity;
using Shields.UI.Navigation;

namespace CalorieTracker.App.Navigation;

/// <summary>
/// Puts an <see cref="OnBackPressedCallback"/> on the created activity that passes back to <see cref="BackButtonBridge"/> while a page handler is registered.
/// Without one, the callback is off and Android handles back: the app closes.
/// </summary>
internal sealed class AndroidBackButton
{
    private readonly BackButtonBridge bridge;
    private ComponentActivity? current;
    private Callback? callback;

    public AndroidBackButton(BackButtonBridge bridge)
    {
        this.bridge = bridge;
        bridge.EnabledChanged += Update;
    }

    /// <summary>Adds the back callback to a created activity, which replaces any earlier one.</summary>
    public void Attach(ComponentActivity activity)
    {
        current = activity;
        callback = new Callback(bridge, activity.OnBackPressedDispatcher);
        activity.OnBackPressedDispatcher.AddCallback(activity, callback);
        bridge.Attach(activity);
    }

    /// <summary>Drops the callback when the current activity is destroyed; the dispatcher removes it itself. An earlier activity is ignored.</summary>
    public void Detach(Android.App.Activity activity)
    {
        if (ReferenceEquals(activity, current))
        {
            current = null;
            callback = null;
        }

        bridge.Detach(activity);
    }

    private void Update() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (callback is not null)
        {
            callback.Enabled = bridge.Enabled;
        }
    });

    private sealed class Callback(BackButtonBridge bridge, OnBackPressedDispatcher dispatcher) : OnBackPressedCallback(false)
    {
        public override void HandleOnBackPressed()
        {
            // The kit's handler never throws, so the task is not awaited.
            if (bridge.HandleAsync() is not null)
            {
                return;
            }

            Enabled = false;
            dispatcher.OnBackPressed();
        }
    }
}
