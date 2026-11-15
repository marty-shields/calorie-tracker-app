namespace CalorieTracker.UI.Components.Feedback;

/// <summary>
/// The one snackbar message the app shows, kept apart from any screen so a message posted before a screen change is there on the
/// next screen. A new message replaces the one showing. It goes away when its action runs, after its duration, or on <see cref="Dismiss"/>.
/// </summary>
public sealed class SnackbarService : IDisposable
{
    /// <summary>How long a message shows when <see cref="Show"/> gets no duration.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);

    private readonly TimeProvider time;
    private readonly object gate = new();
    private SnackbarMessage? current;
    private ITimer? timer;

    public SnackbarService(TimeProvider time) => this.time = time;

    /// <summary>The message showing, or null.</summary>
    public SnackbarMessage? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    /// <summary>Raised, possibly on another thread, whenever <see cref="Current"/> changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Shows a message in place of any showing. The action button shows only when both <paramref name="actionLabel"/> and
    /// <paramref name="onAction"/> are set.
    /// </summary>
    /// <param name="duration"><see cref="DefaultDuration"/> when null; <see cref="Timeout.InfiniteTimeSpan"/> keeps it until dismissed.</param>
    public void Show(string text, string? actionLabel = null, Func<Task>? onAction = null, TimeSpan? duration = null)
    {
        if (duration is { } d && d != Timeout.InfiniteTimeSpan && d < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A duration can't be negative.");
        }

        var hasAction = !string.IsNullOrWhiteSpace(actionLabel) && onAction is not null;
        var message = new SnackbarMessage(text, hasAction ? actionLabel : null, hasAction ? onAction : null);
        lock (gate)
        {
            timer?.Dispose();
            current = message;
            timer = time.CreateTimer(_ => DismissOnly(message), null, duration ?? DefaultDuration, Timeout.InfiniteTimeSpan);
        }

        Changed?.Invoke();
    }

    /// <summary>Removes the message showing, if any.</summary>
    public void Dismiss() => DismissOnly(null);

    /// <summary>
    /// Dismisses <paramref name="message"/> if it is still the one showing, then runs its action. A call for a message
    /// already gone or replaced does nothing, so the action runs once. The message is gone before the action runs, so an
    /// exception from the action reaches the caller: actions must not throw.
    /// </summary>
    public async Task InvokeActionAsync(SnackbarMessage message)
    {
        if (Take(message) is null)
        {
            return;
        }

        Changed?.Invoke();
        if (message.OnAction is { } action)
        {
            await action();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            timer?.Dispose();
            timer = null;
        }
    }

    private void DismissOnly(SnackbarMessage? only)
    {
        if (Take(only) is not null)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Clears and returns the message showing, or null when none shows or it isn't <paramref name="only"/>.</summary>
    private SnackbarMessage? Take(SnackbarMessage? only)
    {
        SnackbarMessage? taken;
        lock (gate)
        {
            if (current is null || (only is not null && !ReferenceEquals(current, only)))
            {
                return null;
            }

            taken = current;
            current = null;
            timer?.Dispose();
            timer = null;
        }

        return taken;
    }
}

/// <param name="ActionLabel">Null when the message has no action.</param>
public sealed record SnackbarMessage(string Text, string? ActionLabel, Func<Task>? OnAction);
