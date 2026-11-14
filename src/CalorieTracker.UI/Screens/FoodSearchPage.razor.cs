using CalorieTracker.Core.Seed;
using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>Logs only statuses; what the user typed is never logged.</summary>
public partial class FoodSearchPage
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.FoodSeedWaited, LogLevel.Information, "Food search waited for the food seed: {Status}")]
        public static partial void SeedWaited(ILogger logger, SeedLoadStatus status);

        [LoggerMessage((int)UiEvent.FoodSearchFailed, LogLevel.Warning, "Food search failed")]
        public static partial void SearchFailed(ILogger logger);

        [LoggerMessage((int)UiEvent.FoodSeedRetried, LogLevel.Information, "Try again restarted the food seed load")]
        public static partial void SeedRetried(ILogger logger);
    }
}
