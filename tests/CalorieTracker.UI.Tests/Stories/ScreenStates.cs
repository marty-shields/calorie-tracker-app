using AngleSharp.Dom;
using CalorieTracker.Core.Data;
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
    private static readonly IReadOnlyList<Food> SampleFoods =
    [
        new() { Id = 1, Name = "Oats, rolled", Unit = FoodUnit.Gram, Kcal = 375, ProteinG = 11, CarbsG = 60, FatG = 9, FibreG = 8.5, SugarsG = 1, SaturatesG = 1.6, SaltG = 0 },
        new() { Id = 2, Name = "Porridge oats", Brand = "Quaker", Unit = FoodUnit.Gram, Kcal = 372, ProteinG = 11, CarbsG = 60, FatG = 8.5 },
        new() { Id = 3, Name = "Oat drink", Brand = "Oatly", Unit = FoodUnit.Millilitre, Kcal = 46, ProteinG = 1, CarbsG = 6.7, FatG = 1.5 },
    ];

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

        new("Screens/FoodSearch", "Loading", c => FoodSearch(c, query: "oat", loading: true)),
        new("Screens/FoodSearch", "Nothing typed", c => FoodSearch(c)),
        new("Screens/FoodSearch", "Results", c => FoodSearch(c, query: "oat", results: SampleFoods)),
        new("Screens/FoodSearch", "No results", c => FoodSearch(c, query: "zzz")),
        new("Screens/FoodSearch", "Food selected", c => FoodSearch(c, query: "oat", results: SampleFoods, selected: SampleFoods[0])),
        new("Screens/FoodSearch", "Search failed", c => FoodSearch(c, query: "oat", failed: true)),

        new("Screens/Error", "Error", c => c.Render<ErrorView>(p => p.Add(x => x.OnRestart, () => { })).FindAll("*")),
    ];

    private static IReadOnlyList<IElement> FoodSearch(BunitContext context, string query = "", IReadOnlyList<Food>? results = null, Food? selected = null, bool loading = false, bool failed = false) =>
        context.Render<FoodSearchView>(p => p
            .Add(x => x.Query, query)
            .Add(x => x.Results, results ?? [])
            .Add(x => x.Selected, selected)
            .Add(x => x.Loading, loading)
            .Add(x => x.Failed, failed)
            .Add(x => x.OnQueryChanged, (string _) => { })
            .Add(x => x.OnSelect, (Food _) => { })
            .Add(x => x.OnClearSelection, () => { })
            .Add(x => x.OnRetry, () => { })
            .Add(x => x.OnNavigate, (NavDestination _) => { }))
            .FindAll("*");

    private static IReadOnlyList<IElement> Settings(BunitContext context, string? version = null, ThemePreference theme = ThemePreference.System) =>
        context.Render<SettingsView>(p => p
            .Add(x => x.Version, version)
            .Add(x => x.Theme, theme)
            .Add(x => x.OnThemeChanged, (ThemePreference _) => { })
            .Add(x => x.OnBack, () => { }))
            .FindAll("*");
}
