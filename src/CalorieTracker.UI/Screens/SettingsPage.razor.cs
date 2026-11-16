using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>Logs a failed theme or target read or save. A failure keeps the previous choice. No log line carries the target.</summary>
public partial class SettingsPage
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.ThemeLoadFailed, LogLevel.Warning, "Reading the saved theme failed")]
        public static partial void ThemeLoadFailed(ILogger logger, Exception exception);

        [LoggerMessage((int)UiEvent.ThemeNotSaved, LogLevel.Warning, "Storage refused the saved theme; it applies until the app closes")]
        public static partial void ThemeNotSaved(ILogger logger);

        [LoggerMessage((int)UiEvent.ThemeSaveFailed, LogLevel.Warning, "Saving the theme failed")]
        public static partial void ThemeSaveFailed(ILogger logger, Exception exception);

        [LoggerMessage((int)UiEvent.TargetLoadFailed, LogLevel.Warning, "Reading the daily target failed")]
        public static partial void TargetLoadFailed(ILogger logger);

        [LoggerMessage((int)UiEvent.TargetSaveFailed, LogLevel.Warning, "Saving the daily target failed; the last saved target stays")]
        public static partial void TargetSaveFailed(ILogger logger);
    }
}
