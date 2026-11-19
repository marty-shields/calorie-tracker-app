namespace CalorieTracker.UI.Components.Actions;

/// <summary>Look of an <see cref="IconButton"/>.</summary>
public enum IconButtonStyle
{
    /// <summary>Icon only, on a transparent background.</summary>
    Plain,

    /// <summary>Icon on a round chip filled with <c>--color-chip</c>, for the main add action of a section.</summary>
    Filled,

    /// <summary>Icon on a lighter round chip filled with <c>--color-chip-soft</c>, for a secondary add action next to the filled one.</summary>
    Soft,
}
