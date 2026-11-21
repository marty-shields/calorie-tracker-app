using CalorieTracker.Core.Data;
using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Components.Layout;

/// <summary>Logs the action and the meal only: nothing a person typed or ate.</summary>
public partial class AddSheet
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.AddSheetOpened, LogLevel.Information, "Add sheet opened for {Meal}")]
        public static partial void Opened(ILogger logger, Meal meal);

        [LoggerMessage((int)UiEvent.AddSheetChose, LogLevel.Information, "Add sheet {Action} for {Meal}")]
        public static partial void Chose(ILogger logger, AddSheetAction action, Meal meal);
    }
}
