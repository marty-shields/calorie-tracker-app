namespace CalorieTracker.UI.Stories.Stories.Foundations;

/// <summary>The tokens this app declares itself in its tokens.css (the kit's tokens are already on the kit's token sheet).</summary>
public static class AppTokenNames
{
    public static IReadOnlyList<string> Colours { get; } =
    [
        "--color-ring-track", "--color-protein", "--color-carbs", "--color-fat", "--color-target-band",
    ];

    public static IReadOnlyList<string> Sizes { get; } =
    [
        "--font-size-xs", "--opacity-thin",
        "--ring-size", "--ring-width", "--ring-size-compact", "--ring-width-compact", "--share-bar-height", "--chart-dash",
        "--sheet-enter-duration", "--sheet-max-width", "--sheet-handle-width", "--sheet-handle-height",
        "--sheet-tile-min-height", "--sheet-tile-icon-size",
    ];
}
