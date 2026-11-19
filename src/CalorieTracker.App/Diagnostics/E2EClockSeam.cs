#if DEBUG
namespace CalorieTracker.App.Diagnostics;

/// <summary>
/// Debug builds only, for the E2E scenarios that depend on the time of day (which meals are open on Today): an emulator's clock is not fixed.
/// While <see cref="FileName"/> exists in the app's external files folder and holds a time such as <c>19:30</c>, optionally after a whole-day offset such as <c>-2d 12:00</c>
/// (the format and the date maths are in <see cref="E2EClockPin"/>), the app's <see cref="TimeProvider"/> reads that time on the device's current date, moved by the offset,
/// when the file is read, and then keeps moving from it. Without the file, or when it holds anything else, the clock is the device's.
/// </summary>
internal static class E2EClockSeam
{
    public const string FileName = "e2e-clock.txt";

    public static IServiceCollection AddE2EClockSeam(this IServiceCollection services)
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(TimeProvider) && d.ImplementationInstance is TimeProvider)
            ?? throw new InvalidOperationException("E2E clock seam: no TimeProvider instance is registered to wrap.");
        services.Remove(original);
        services.AddSingleton<TimeProvider>(new FileClock((TimeProvider)original.ImplementationInstance!));
        return services;
    }

    private sealed class FileClock(TimeProvider inner) : TimeProvider
    {
        private readonly Lock gate = new();
        private string? path;
        private DateTime stamp = DateTime.MinValue;
        private TimeSpan? offset;

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long GetTimestamp() => inner.GetTimestamp();

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow()
        {
            var now = inner.GetUtcNow();
            return Offset(now) is { } shift ? now + shift : now;
        }

        /// <summary>Pinned time minus real time, set when the file appears or changes, so the clock keeps moving from the pinned time. Null without the file.</summary>
        private TimeSpan? Offset(DateTimeOffset now)
        {
            lock (gate)
            {
                try
                {
                    path ??= Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? FileSystem.AppDataDirectory, FileName);
                    var written = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                    if (written != stamp)
                    {
                        stamp = written;
                        offset = written == DateTime.MinValue ? null : Pin(now, File.ReadAllText(path));
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    offset = null;
                }

                return offset;
            }
        }

        private TimeSpan? Pin(DateTimeOffset now, string text)
        {
            if (!E2EClockPin.TryParse(text, out var days, out var time))
            {
                return null;
            }

            return E2EClockPin.OffsetFrom(now, LocalTimeZone, days, time);
        }
    }
}
#endif
