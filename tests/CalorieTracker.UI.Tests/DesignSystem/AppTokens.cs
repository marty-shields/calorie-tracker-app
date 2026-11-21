using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>The tokens the app's pages load: the kit's, since this app declares none.</summary>
internal static class AppTokens
{
    /// <summary>Light and dark value of a colour token, as the app's pages load it.</summary>
    public static ThemedColour Colour(string name) => DesignTokens.Colour(KitCss.Tokens, name);
}
