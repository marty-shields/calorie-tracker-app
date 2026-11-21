namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The Add sheet the bar's Add button opens: meal chips, Search foods, Scan barcode and the usual foods.</summary>
public sealed class AddSheetScreen(WebView web, NavigationBar navigation)
{
    /// <summary>Taps Add and waits for the sheet.</summary>
    public void Open()
    {
        navigation.TapAdd();
        web.WaitUntil(IsOpen, "the Add sheet didn't open");
    }

    public bool IsOpen() => web.Exists("add-sheet");

    public string Title() => web.FindAll($"#{web.Attribute("add-sheet", "aria-labelledby")}").Single().Text.Trim();

    public void ChooseMeal(string meal)
    {
        web.Tap(MealTestId(meal));
        web.Eventually(() => IsMealChosen(meal), Is.True, $"{meal} chosen on the Add sheet");
    }

    public bool IsMealChosen(string meal) => web.Find(MealTestId(meal)).Selected;

    /// <summary>The meals with a chip, in order.</summary>
    public IReadOnlyList<string> MealLabels() =>
        web.FindAll("[data-testid=\"add-sheet-meals\"] .text").Select(label => label.Text.Trim()).ToList();

    /// <summary>The last amount and kcal of each usual food listed, such as "35 g · 130 kcal", in order. Empty while the section is hidden.</summary>
    public IReadOnlyList<string> UsualDetails() =>
        web.FindAll("[data-testid^=\"add-sheet-usual-\"][data-testid$=\"-detail\"]").Select(row => row.Text.Trim()).ToList();

    /// <summary>Taps the + of the first usual food listed.</summary>
    public void AddFirstUsual() =>
        web.FindAll("[data-testid^=\"add-sheet-usual-\"][data-testid$=\"-add\"]").First().Click();

    public void Search() => web.Tap("add-sheet-search");

    public void Scan() => web.Tap("add-sheet-scan");

    /// <summary>Taps the dimmed screen above the sheet.</summary>
    public void TapScrim() => web.Tap("add-sheet-scrim");

    /// <summary>Whether the Add button has focus, as it should after the sheet closes.</summary>
    public bool AddButtonHasFocus() => web.Focused() == "add-button";

    public bool FocusIsInSheet() => web.FocusIsWithin("add-sheet");

    /// <summary>The <c>data-testid</c> of the focused element.</summary>
    public string Focused() => web.Focused();

    /// <summary>Presses Tab <paramref name="times"/> times, checking after each press that focus is still in the sheet.</summary>
    public void TabThroughSheet(int times, bool shift)
    {
        for (var press = 1; press <= times; press++)
        {
            web.PressTab(shift);
            web.Eventually(FocusIsInSheet, Is.True, $"focus in the Add sheet after {(shift ? "Shift+" : "")}Tab press {press}");
        }
    }

    /// <summary>How many Tab stops the sheet has: the chosen meal, Search foods, Scan barcode and the + of each usual food.</summary>
    public int TabStopCount() => 3 + UsualAddIds().Count;

    /// <summary>The <c>data-testid</c> of the sheet's last Tab stop: the last usual food's + when any are listed, else Scan barcode.</summary>
    public string LastControl() => UsualAddIds().LastOrDefault() ?? "add-sheet-scan";

    private List<string> UsualAddIds() =>
        web.FindAll("[data-testid^=\"add-sheet-usual-\"][data-testid$=\"-add\"]").Select(button => button.GetDomAttribute("data-testid") ?? "").ToList();

    private static string MealTestId(string meal) => $"add-sheet-meal-{meal.ToLowerInvariant()}";
}
