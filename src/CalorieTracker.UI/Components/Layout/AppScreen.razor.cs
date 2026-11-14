using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Components.Layout;

/// <summary>Logs a pull-to-refresh gesture that couldn't be set up; the screen works without it.</summary>
public partial class AppScreen
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.PullRefreshFailed, LogLevel.Warning, "Setting up pull to refresh failed")]
        public static partial void PullRefreshFailed(ILogger logger, Exception exception);
    }
}
