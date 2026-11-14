using AngleSharp.Dom;
using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Screens;

namespace CalorieTracker.UI.Tests.Stories;

/// <summary>A screen state: its stories section and story name, and how to render it.</summary>
/// <param name="Section">Stories title, e.g. "Screens/Settings".</param>
/// <param name="Name">Story name.</param>
/// <param name="Render">Renders the state with every callback set and returns its elements.</param>
public sealed record ScreenState(string Section, string Name, Func<BunitContext, IReadOnlyList<IElement>> Render)
{
    public override string ToString() => $"{Section}/{Name}";
}

/// <summary>Every screen state that has a story, in story order. Renders mirror the stories, with every callback set.</summary>
public static class ScreenStates
{
    public static IReadOnlyList<ScreenState> All { get; } =
    [
        new("Screens/Startup", "Loading", c => c.Render<StartupView>().FindAll("*")),
        new("Screens/Startup", "Load failed", c => c.Render<StartupView>(p => p.Add(x => x.Failed, true)).FindAll("*")),

        new("Screens/Dashboard", "Empty", c => c.Render<DashboardView>(p => p
            .Add(x => x.OnSettings, () => { })
            .Add(x => x.OnNavigate, (NavDestination _) => { })).FindAll("*")),

        new("Screens/Settings", "Default", c => Settings(c, version: "1.0.0")),
        new("Screens/Settings", "Dark chosen", c => Settings(c, version: "1.0.0", theme: ThemePreference.Dark)),
        new("Screens/Settings", "No version", c => Settings(c)),

        new("Screens/Scan", "Scanning", c => Scan(c, ScanState.Scanning)),
        new("Screens/Scan", "Looking up", c => Scan(c, ScanState.LookingUp)),
        new("Screens/Scan", "Found", c => Scan(c, ScanState.Found, new("Porridge oats", "Morning Mill", 372, 11, 60, 8))),
        new("Screens/Scan", "Found liquid", c => Scan(c, ScanState.Found, new("Semi-skimmed milk", null, 47, 3.5m, 4.8m, 1.7m, IsLiquid: true))),
        new("Screens/Scan", "Found with warning", c => Scan(c, ScanState.Found, new("Chocolate spread", "Nutty Co", 250, 6, 55, 31))),
        new("Screens/Scan", "Not found", c => Scan(c, ScanState.NotFound)),

        new("Screens/Error", "Error", c => c.Render<ErrorView>(p => p.Add(x => x.OnRestart, () => { })).FindAll("*")),
    ];

    private static IReadOnlyList<IElement> Scan(BunitContext context, ScanState state, ScannedProduct? product = null) =>
        context.Render<ScanView>(p => p
            .Add(x => x.State, state)
            .Add(x => x.Product, product)
            .Add(x => x.OnScanAgain, () => { })
            .Add(x => x.OnBack, () => { }))
            .FindAll("*");

    private static IReadOnlyList<IElement> Settings(BunitContext context, string? version = null, ThemePreference theme = ThemePreference.System) =>
        context.Render<SettingsView>(p => p
            .Add(x => x.Version, version)
            .Add(x => x.Theme, theme)
            .Add(x => x.OnThemeChanged, (ThemePreference _) => { })
            .Add(x => x.OnBack, () => { }))
            .FindAll("*");
}
