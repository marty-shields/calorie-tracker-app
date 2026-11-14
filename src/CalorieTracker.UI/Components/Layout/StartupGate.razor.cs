using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Components.Layout;

/// <summary>Runs <see cref="Initialize"/>, showing the Startup screen until it completes. When it fails, the screen offers Try again, which runs it again.</summary>
public partial class StartupGate
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.StartupFinished, LogLevel.Information, "Startup finished in {Milliseconds} ms")]
        public static partial void Started(ILogger logger, long milliseconds);

        [LoggerMessage((int)UiEvent.StartupFailed, LogLevel.Error, "Startup failed")]
        public static partial void Failed(ILogger logger, Exception exception);
    }
}
