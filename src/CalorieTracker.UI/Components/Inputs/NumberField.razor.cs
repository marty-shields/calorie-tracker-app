using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Components.Inputs;

/// <summary>Logs a failed focus on show; the field works without it.</summary>
public partial class NumberField
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.NumberFieldFocusFailed, LogLevel.Warning, "Focusing the number field failed")]
        public static partial void AutoFocusFailed(ILogger logger, Exception exception);
    }
}
