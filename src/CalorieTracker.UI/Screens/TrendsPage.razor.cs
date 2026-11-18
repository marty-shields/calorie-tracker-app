using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Navigation;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>Logs the period, day counts and what was opened only: never a food, a date a person logged on or any calories.</summary>
public partial class TrendsPage
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.TrendsLoaded, LogLevel.Debug, "Trends read for {Period}, days {Days}")]
        public static partial void Loaded(ILogger logger, TrendsPeriod period, int days);

        [LoggerMessage((int)UiEvent.TrendsLoadFailed, LogLevel.Warning, "Trends could not be read for {Period}")]
        public static partial void LoadFailed(ILogger logger, TrendsPeriod period);

        [LoggerMessage((int)UiEvent.TrendsOpened, LogLevel.Information, "Trends opened a {Opened} from {Period}")]
        public static partial void Opened(ILogger logger, string opened, TrendsPeriod period);
    }
}
