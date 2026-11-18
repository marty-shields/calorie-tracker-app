namespace CalorieTracker.Core.Data;

/// <summary>Runs database work one call at a time.</summary>
internal sealed class DatabaseGate
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private int waiting;

    /// <summary>How many calls are queued behind the running one. Lets a test see that a call is waiting for the gate instead of guessing with a delay.</summary>
    internal int Waiting => Volatile.Read(ref waiting);

    /// <summary>Waits for earlier work to finish, then runs <paramref name="work"/>.</summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        Interlocked.Increment(ref waiting);
        try
        {
            await semaphore.WaitAsync();
        }
        finally
        {
            Interlocked.Decrement(ref waiting);
        }

        try
        {
            return await work();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>Waits for earlier work to finish, then runs <paramref name="work"/>.</summary>
    public Task RunAsync(Func<Task> work) =>
        RunAsync(async () =>
        {
            await work();
            return true;
        });
}
