using CalorieTracker.Core.Diary;
using Shields.UI.Components.Feedback;

namespace CalorieTracker.UI.Components.Feedback;

/// <summary>The Undo of a snackbar posted after an entry was added.</summary>
public static class AddUndo
{
    /// <summary>Removes the entry just added. An entry already gone counts as undone. A failure goes to <paramref name="logFailure"/> and a snackbar; it never throws. Takes the services rather than a screen, which may be gone.</summary>
    public static async Task RunAsync(DiaryService diary, SnackbarService snackbar, Action<DiaryStatus> logFailure, int entryId)
    {
        var result = await diary.RemoveAsync(entryId);
        if (result.Status is not (DiaryStatus.Ok or DiaryStatus.NotFound))
        {
            logFailure(result.Status);
            snackbar.Show("Couldn't undo. Remove it from the day.");
        }
    }
}
