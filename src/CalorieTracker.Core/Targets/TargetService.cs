using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Targets;

/// <summary>Stores the daily calorie target in <see cref="AppSettings"/> through <see cref="TargetStore"/>, and each change in the dated <see cref="TargetHistory"/>. A failing database gives <see cref="TargetStatus.Failed"/>, never an exception. Logs never carry the value.</summary>
public sealed partial class TargetService
{
    private readonly TargetStore store;
    private readonly TimeProvider time;
    private readonly ILogger<TargetService> logger;

    internal TargetService(TargetStore store, TimeProvider time, ILogger<TargetService> logger)
    {
        this.store = store;
        this.time = time;
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
            await store.SetAsync(whole, Today());
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
            await store.SetAsync(null, Today());
            Log.Cleared(logger);
            return TargetResult.Ok(null);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return TargetResult.Failed();
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>
    /// The target that applied on <paramref name="date"/>: the latest change on or before it, with <see cref="TargetResult.Kcal"/> null when there was none or it had been cleared.
    /// </summary>
    public async Task<TargetResult> ForDateAsync(DateOnly date)
    {
        try
        {
            var kcal = Round(await store.LatestOnAsync(date));
            Log.DateRead(logger);
            return TargetResult.Ok(kcal);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return TargetResult.Failed();
        }
    }

    /// <summary>The longest range <see cref="ForRangeAsync"/> accepts, about ten years.</summary>
    public const int MaxRangeDays = 3_660;

    /// <summary>
    /// The target that applied on each day from <paramref name="from"/> to <paramref name="to"/> inclusive, oldest first, from one read.
    /// A range that ends before it starts, or spans more than <see cref="MaxRangeDays"/> days, returns <see cref="TargetStatus.Invalid"/>.
    /// </summary>
    public async Task<TargetRangeResult> ForRangeAsync(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber >= MaxRangeDays)
        {
            Log.RangeRejected(logger);
            return TargetRangeResult.Invalid();
        }

        try
        {
            var rows = await store.UpToAsync(to);
            var count = to.DayNumber - from.DayNumber + 1;
            var days = new List<DayTarget>(count);
            var next = 0;
            TargetHistory? latest = null;
            for (var offset = 0; offset < count; offset++)
            {
                var date = DateOnly.FromDayNumber(from.DayNumber + offset);
                var key = DiaryEntry.FormatDate(date);
                while (next < rows.Count && string.CompareOrdinal(rows[next].EffectiveFrom, key) <= 0)
                {
                    latest = rows[next++];
                }

                days.Add(new DayTarget(date, Round(latest)));
            }

            Log.RangeRead(logger, days.Count);
            return TargetRangeResult.Ok(days);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return TargetRangeResult.Failed();
        }
    }

    private static int? Round(TargetHistory? row) => row?.Kcal is { } kcal ? (int)Math.Round(kcal) : null;

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.TargetDateRead, LogLevel.Debug, "Daily target read for one day")]
        public static partial void DateRead(ILogger logger);

        [LoggerMessage((int)CoreEvent.TargetRangeRead, LogLevel.Debug, "Daily targets read for {Days} days")]
        public static partial void RangeRead(ILogger logger, int days);

        [LoggerMessage((int)CoreEvent.TargetRangeRejected, LogLevel.Debug, "Daily target range rejected as backwards or too long")]
        public static partial void RangeRejected(ILogger logger);

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
