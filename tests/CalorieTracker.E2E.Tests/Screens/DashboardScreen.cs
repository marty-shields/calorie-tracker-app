namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The Dashboard: the date strip, the day's totals, the meals, the navigation bar and the Settings gear.</summary>
public sealed class DashboardScreen(WebView web)
{
    public const string Title = "Calorie Tracker";

    /// <summary>The empty state's message, once the Dashboard has loaded.</summary>
    public string EmptyText()
    {
        web.WaitUntil(() => web.Exists("dashboard-empty-message"), "the Dashboard didn't load");
        return web.Text("dashboard-empty-message");
    }

    private static string Meal(string meal) => meal.ToLowerInvariant();

    /// <summary>Taps a meal's + and waits for search to open in add mode for it.</summary>
    public void AddTo(string meal)
    {
        web.Tap($"meal-{Meal(meal)}-add");
        web.WaitUntil(() => web.Exists("screen-title") && web.Text("screen-title") == $"Add to {meal}", $"search for {meal} didn't show");
    }

    public bool HasAddButton(string meal) => web.Exists($"meal-{Meal(meal)}-add");

    /// <summary>Opens the meal when a scenario folded it to one line, as a tap on its header does. An open meal is left alone.</summary>
    private void Expand(string meal)
    {
        if (Expanded(meal) == "false")
        {
            ToggleMeal(meal);
            web.WaitUntil(() => Expanded(meal) == "true", $"{meal} didn't open");
        }
    }

    /// <summary>The meal header's <c>aria-expanded</c>: every meal's header is a fold button.</summary>
    public string? Expanded(string meal) => web.Attribute($"meal-{Meal(meal)}-toggle", "aria-expanded");

    /// <summary>The folded line, such as "1 item · 125 kcal"; null when the meal is not folded.</summary>
    public string? FoldedLine(string meal) => Hint(meal) is { } hint && (hint.Contains(" item") || hint == "Nothing logged") ? hint : null;

    /// <summary>Whether the meal header shows a chip (the kit's <c>SectionHeader</c> badge); Today shows none.</summary>
    public bool HasBadge(string meal) => web.Exists($"meal-{Meal(meal)}-badge");

    /// <summary>Taps the meal's header, which opens a folded meal and folds an open one.</summary>
    public void ToggleMeal(string meal) => web.Tap($"meal-{Meal(meal)}-toggle");

    /// <summary>The number of entries listed under a meal, opening it first when it is folded.</summary>
    public int OpenAndCountEntries(string meal)
    {
        Expand(meal);
        return web.FindAll($"[data-testid=\"meal-{Meal(meal)}-section\"] ul > li").Count;
    }

    /// <summary>The meal's total, such as "125 kcal"; null while it has no entries. Opens a folded meal first.</summary>
    public string? OpenAndReadMealTotal(string meal)
    {
        Expand(meal);
        return web.Exists($"meal-{Meal(meal)}-total") ? web.Text($"meal-{Meal(meal)}-total") : null;
    }

    /// <summary>The text beside the meal's name, such as its folded line; null when there is none.</summary>
    public string? Hint(string meal) => web.Exists($"meal-{Meal(meal)}-hint") ? web.Text($"meal-{Meal(meal)}-hint") : null;

    /// <summary>The calories eaten on the day, as the card shows them.</summary>
    public string Eaten() => web.Text("dashboard-eaten");

    /// <summary>The ring's caption: "kcal left", "kcal over" or "kcal eaten".</summary>
    public string RingLabel() => web.Text("dashboard-ring-caption");

    public string? NoTargetText() => web.Exists("dashboard-no-target") ? web.Text("dashboard-no-target") : null;

    public void SetTarget() => web.Tap("dashboard-set-target");

    public string DateLabel() => web.Text("date-strip-label");

    public void PreviousDay() => web.Tap("date-strip-previous");

    public void NextDay() => web.Tap("date-strip-next");

    public void ToToday() => web.Tap("date-strip-label");

    public bool HasStartupError() => web.Exists("startup-error");

    public void OpenSettings() => web.Tap("settings");

    /// <summary>The entries listed under a meal, each as its row text (name, amount and calories).</summary>
    public IReadOnlyList<string> OpenAndReadEntries(string meal)
    {
        Expand(meal);
        return web.FindAll($"[data-testid=\"meal-{Meal(meal)}-section\"] ul > li").Select(row => row.Text).ToList();
    }

    /// <summary>Taps the first entry under a meal, opening it first when it is folded, and waits for Serving in edit mode.</summary>
    public void OpenEntry(string meal)
    {
        Expand(meal);
        web.FindAll($"[data-testid=\"meal-{Meal(meal)}-section\"] ul > li button").First().Click();
        web.WaitUntil(() => web.Exists("serving-save"), "the Serving screen didn't open in edit mode");
    }

    /// <summary>The calories eaten on the day as a number.</summary>
    public int EatenKcal() => int.Parse(Eaten().Replace(",", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture);
}
