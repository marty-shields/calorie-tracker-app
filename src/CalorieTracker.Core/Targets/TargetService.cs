using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Targets;

/// <summary>Stores the daily calorie target in <see cref="AppSettings"/> through <see cref="TargetStore"/>. A failing database gives <see cref="TargetStatus.Failed"/>, never an exception. Logs never carry the value.</summary>
internal sealed partial class TargetService(TargetStore store, ILogger<TargetService> logger) : ITargetService
{
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

    public async Task<TargetResult> SaveAsync(double kcal)
    {
        if (!double.IsFinite(kcal) || kcal < 1 || kcal > int.MaxValue || kcal != Math.Floor(kcal))
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
