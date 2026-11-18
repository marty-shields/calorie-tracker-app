namespace CalorieTracker.UI.Stories.Shared;

/// <summary>A clock that always reads the same local time, so a story shows what the app guesses at that time.</summary>
public sealed class FixedTimeProvider(TimeOnly time) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => new(new DateTime(2026, 11, 18) + time.ToTimeSpan(), TimeSpan.Zero);
}
