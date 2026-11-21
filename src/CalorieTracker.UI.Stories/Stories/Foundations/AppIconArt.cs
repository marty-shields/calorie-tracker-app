namespace CalorieTracker.UI.Stories.Stories.Foundations;

/// <summary>The App's icon and splash SVGs, which this project embeds as brand.&lt;file&gt;.svg, as data URIs for the kit's app icon sheet.</summary>
public static class AppIconArt
{
    public static string Background { get; } = Art("appicon.svg");

    public static string Foreground { get; } = Art("appiconfg.svg");

    public static string Monochrome { get; } = Art("appicon_monochrome.svg");

    public static string SplashLight { get; } = Art("splash.svg");

    /// <summary>The dark splash shows the icon's foreground, whose shadow suits a dark background.</summary>
    public static string SplashDark => Foreground;

    private static string Art(string file)
    {
        using var stream = typeof(AppIconArt).Assembly.GetManifestResourceStream($"brand.{file}")!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(memory.ToArray())}";
    }
}
