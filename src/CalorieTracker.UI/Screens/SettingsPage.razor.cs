using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>Logs a failed theme read or save. A theme failure keeps the previous choice.</summary>
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
    }
}
