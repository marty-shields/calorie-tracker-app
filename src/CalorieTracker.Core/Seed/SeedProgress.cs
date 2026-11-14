namespace CalorieTracker.Core.Seed;

/// <summary>Starts the seed load and lets a screen wait for it, so a search on the first launch does not read a half-filled Food table.</summary>
public sealed class SeedProgress(ISeedLoader loader)
{
    private readonly object gate = new();
    private Task<SeedLoadResult>? current;

    /// <summary>The load started last; null until <see cref="Start"/> is first called. Completes with a status, never faults.</summary>
    public Task<SeedLoadResult>? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public bool IsLoading => Current is { IsCompleted: false };

    /// <summary>
    /// Starts a load and returns at once. A load still running is returned instead of starting a second; a finished one, such as a failed one, is replaced
    /// so Try again retries.
    /// </summary>
    public Task<SeedLoadResult> Start()
    {
        lock (gate)
        {
            if (current is { IsCompleted: false })
            {
                return current;
            }

            return current = loader.LoadAsync();
        }
    }
}
