using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>Logs food ids, meals, counts and statuses of the Add sheet's usual foods only: never a food's name or amount.</summary>
public partial class DashboardPage
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.UsualRead, LogLevel.Debug, "Usual foods for the Add sheet read for {Meal}, rows {Count}")]
        public static partial void UsualRead(ILogger logger, Meal meal, int count);

        [LoggerMessage((int)UiEvent.UsualReadFailed, LogLevel.Warning, "Usual foods for the Add sheet could not be read for {Meal}")]
        public static partial void UsualReadFailed(ILogger logger, Meal meal);

        [LoggerMessage((int)UiEvent.UsualAdded, LogLevel.Information, "Usual food {FoodId} added to {Meal}")]
        public static partial void UsualAdded(ILogger logger, int foodId, Meal meal);

        [LoggerMessage((int)UiEvent.UsualAddFailed, LogLevel.Warning, "Usual food {FoodId} could not be added to {Meal}: {Status}")]
        public static partial void UsualAddFailed(ILogger logger, int foodId, Meal meal, DiaryStatus status);

        [LoggerMessage((int)UiEvent.UsualUndoFailed, LogLevel.Warning, "Undo could not remove the usual food just added: {Status}")]
        public static partial void UsualUndoFailed(ILogger logger, DiaryStatus status);
    }
}
