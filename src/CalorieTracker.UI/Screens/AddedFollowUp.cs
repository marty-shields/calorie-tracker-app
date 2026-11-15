namespace CalorieTracker.UI.Screens;

/// <summary>What the snackbar offers after an entry is added.</summary>
public enum AddedFollowUp
{
    /// <summary>Undo, for a person sent on to the Dashboard where the entry shows.</summary>
    Undo,

    /// <summary>View day, for a person who stays where they came from, such as the search.</summary>
    ViewDay,
}
