using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Tests;

/// <summary>Holds a <see cref="DatabaseGate"/> and lets a test check, without a delay, that a call is queued behind it.</summary>
internal static class GateHolder
{
    private static readonly TimeSpan GiveUp = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Takes the gate, runs <paramref name="call"/>, waits until the call is queued on the gate, lets the holder run <paramref name="whileHeld"/>, releases, and returns what the holder saw and the call's result.
    /// A call that skips the gate is never queued, so it finishes (or the wait gives up) and the test fails; the limit only ends a failing test.
    /// </summary>
    public static async Task<(TSeen Seen, T Result)> CallWhileHeld<T, TSeen>(DatabaseGate gate, Func<Task<T>> call, Func<Task<TSeen>> whileHeld)
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            return await whileHeld();
        });

        var pending = call();
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (gate.Waiting == 0 && !pending.IsCompleted && started.Elapsed < GiveUp)
        {
            await Task.Yield();
        }

        var queued = gate.Waiting;
        var finishedEarly = pending.IsCompleted;
        holding.SetResult();
        var seen = await busy;
        var result = await pending;
        Assert.Multiple(() =>
        {
            Assert.That(finishedEarly, Is.False, "the call finished while the gate was held");
            Assert.That(queued, Is.EqualTo(1), "the call was not queued on the gate");
        });
        return (seen, result);
    }
}
