using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Targets;

/// <summary>Stores the daily calorie target in <see cref="AppSettings"/> through <see cref="TargetStore"/>. A failing database gives <see cref="TargetStatus.Failed"/>, never an exception. Logs never carry the value.</summary>
public sealed partial class TargetService
{
    private readonly TargetStore store;
    private readonly ILogger<TargetService> logger;

    internal TargetService(TargetStore store, ILogger<TargetService> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    /// <summary>The saved target, or <see cref="TargetResult.Kcal"/> null when none is set (the default).</summary>
    public async Task<TargetResult> GetAsync()
    {
        try
        {
            return TargetResult.Ok(await store.GetAsync());
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return TargetResult.Failed();
        }
    }

    /// <summary>The largest target that can be saved, in kcal.</summary>
    public const int MaxKcal = 10_000;

    /// <summary>
    /// Saves the target, replacing any earlier one. The value must be a whole number from 1 to <see cref="MaxKcal"/>;
    /// anything else (zero, negative, fractional, NaN, infinite, too large) returns <see cref="TargetStatus.Invalid"/> and changes nothing.
    /// </summary>
    public async Task<TargetResult> SaveAsync(double kcal)
    {
        if (!double.IsFinite(kcal) || kcal < 1 || kcal > MaxKcal || kcal != Math.Floor(kcal))
        {
            Log.Rejected(logger);
            return TargetResult.Invalid();
        }

        var whole = (int)kcal;
        try
        {
            await store.SetAsync(whole);
            Log.Saved(logger);
            return TargetResult.Ok(whole);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return TargetResult.Failed();
        }
    }

    /// <summary>Removes the target, so none is set. Clearing when none is set succeeds.</summary>
    public async Task<TargetResult> ClearAsync()
    {
        try
        {
            await store.SetAsync(null);
            Log.Cleared(logger);
            return TargetResult.Ok(null);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return TargetResult.Failed();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.TargetSaved, LogLevel.Debug, "Daily target saved")]
        public static partial void Saved(ILogger logger);

        [LoggerMessage((int)CoreEvent.TargetCleared, LogLevel.Debug, "Daily target cleared")]
        public static partial void Cleared(ILogger logger);

        [LoggerMessage((int)CoreEvent.TargetRejected, LogLevel.Debug, "Daily target rejected as not a whole number above zero")]
        public static partial void Rejected(ILogger logger);

        [LoggerMessage((int)CoreEvent.TargetFailed, LogLevel.Warning, "Daily target call failed")]
        public static partial void Failed(ILogger logger);
    }
}
