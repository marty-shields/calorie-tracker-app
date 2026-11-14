using AngleSharp.Dom;
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

        new("Screens/Error", "Error", c => c.Render<ErrorView>(p => p.Add(x => x.OnRestart, () => { })).FindAll("*")),
    ];

    private static IReadOnlyList<IElement> Settings(BunitContext context, string? version = null, ThemePreference theme = ThemePreference.System) =>
        context.Render<SettingsView>(p => p
            .Add(x => x.Version, version)
            .Add(x => x.Theme, theme)
            .Add(x => x.OnThemeChanged, (ThemePreference _) => { })
            .Add(x => x.OnBack, () => { }))
            .FindAll("*");
}
