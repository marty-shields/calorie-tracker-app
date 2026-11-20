using Shields.UI.Testing;
using static Shields.UI.Testing.ContrastCheck;

namespace CalorieTracker.UI.Tests.DesignSystem;

/// <summary>Contrast of the tokens only this app declares; the kit's own pairs are checked by the kit.</summary>
public class ContrastTests
{
    // Minimums are WCAG AA contrast ratios.
    private static readonly (string Foreground, string Background, double Minimum)[] Pairs =
    [
        ("--color-protein", "--color-bg", LargeTextOrControl),
        ("--color-protein", "--color-surface", LargeTextOrControl),
        ("--color-carbs", "--color-bg", LargeTextOrControl),
        ("--color-carbs", "--color-surface", LargeTextOrControl),
        ("--color-fat", "--color-bg", LargeTextOrControl),
        ("--color-fat", "--color-surface", LargeTextOrControl),
        ("--color-accent", "--color-ring-track", LargeTextOrControl),
        ("--color-error", "--color-ring-track", LargeTextOrControl),
        ("--color-ring-track", "--color-surface", LargeTextOrControl),
    ];

    private static IEnumerable<TestCaseData> Cases() =>
        Pairs.SelectMany(p => Enum.GetValues<ColourScheme>().Select(scheme =>
            new TestCaseData(p.Foreground, p.Background, p.Minimum, scheme)
                .SetArgDisplayNames($"{p.Foreground} on {p.Background} ({scheme})")));

    [TestCaseSource(nameof(Cases))]
    public void Pair_MeetsWcagAA(string foreground, string background, double minimum, ColourScheme scheme)
    {
        var result = Measure(AppTokens.KitAndAppCss, foreground, background, minimum, scheme);

        Assert.That(result.Passes, Is.True, result.ToString());
    }

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
