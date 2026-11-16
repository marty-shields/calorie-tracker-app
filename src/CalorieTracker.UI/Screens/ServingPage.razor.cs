using CalorieTracker.Core.Diary;
using CalorieTracker.UI.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>Logs only statuses; no food, amount or entry content.</summary>
public partial class ServingPage
{
    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.UndoFailed, LogLevel.Warning, "Undo could not remove the entry just added: {Status}")]
        public static partial void UndoFailed(ILogger logger, DiaryStatus status);

        [LoggerMessage((int)UiEvent.RestoreFailed, LogLevel.Warning, "Undo could not restore the entry just removed: {Status}")]
        public static partial void RestoreFailed(ILogger logger, DiaryStatus status);
    }
}
