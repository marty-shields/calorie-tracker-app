#if DEBUG
using System.Globalization;

namespace CalorieTracker.App.Diagnostics;

/// <summary>
/// Debug builds only, for the E2E scenarios that depend on the time of day (which meals are open on Today): an emulator's clock is not fixed.
/// While <see cref="FileName"/> exists in the app's external files folder and holds a time such as <c>19:30</c>, the app's <see cref="TimeProvider"/>
/// reads that time of day on the device's current date. Without the file, or when it holds anything else, the clock is the device's.
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
        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long GetTimestamp() => inner.GetTimestamp();

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow()
        {
            var now = inner.GetUtcNow();
            if (Read() is not { } time)
            {
                return now;
            }

            var local = TimeZoneInfo.ConvertTime(now, LocalTimeZone);
            var pinned = local.Date + time.ToTimeSpan();
            return new DateTimeOffset(pinned, LocalTimeZone.GetUtcOffset(pinned)).ToUniversalTime();
        }

        private static TimeOnly? Read()
        {
            var path = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? FileSystem.AppDataDirectory, FileName);
            try
            {
                return File.Exists(path) && TimeOnly.TryParseExact(File.ReadAllText(path).Trim(), "HH:mm", null, DateTimeStyles.None, out var time) ? time : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
#endif
