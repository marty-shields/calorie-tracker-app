using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>This app's own tokens.css, read next to the kit's through Shields.UI.Testing.</summary>
internal static class AppTokens
{
    /// <summary>This app's tokens.css: only the tokens the kit does not declare, and a few temporary overrides.</summary>
    public static string AppCss { get; } = RepoFiles.Read("src", "CalorieTracker.UI", "wwwroot", "css", "tokens.css");

    /// <summary>The kit's tokens followed by this app's, which is loaded after the kit's and so wins.</summary>
    public static string KitAndAppCss { get; } = KitCss.Tokens + "\n" + AppCss;

    public static IReadOnlyList<string> AppNames { get; } = DesignTokens.Declares(AppCss).ToList();

    /// <summary>Light and dark value of a colour token, as the app's pages load it.</summary>
    public static ThemedColour Colour(string name) => DesignTokens.Colour(KitAndAppCss, name);
}
