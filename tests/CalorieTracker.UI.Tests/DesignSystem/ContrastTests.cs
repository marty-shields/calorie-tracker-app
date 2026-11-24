using Shields.UI.Testing;
using static Shields.UI.Testing.ContrastCheck;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>Contrast of what only this app decides; the kit's own token pairs are checked by the kit.</summary>
public class ContrastTests
{
    // The App keeps the status bar's icons light (StatusBarTheme.Dark in App.xaml.cs) in both themes, and Android draws them
    // white over the top bar.
    [TestCase(ColourScheme.Light)]
    [TestCase(ColourScheme.Dark)]
    public void StatusBarIcons_OnTheTopBar_MeetWcagAA(ColourScheme scheme)
    {
        var bar = AppTokens.Colour("--color-bar").In(scheme);

        Assert.That(Rgb.Contrast(Rgb.Parse("#ffffff"), bar), Is.GreaterThanOrEqualTo(LargeTextOrControl), $"#ffffff on {bar}");
    }
}
