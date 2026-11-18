namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>The Add sheet the bar's Add button opens: meal chips, Search foods and Scan barcode.</summary>
public sealed class AddSheetScreen(WebView web, NavigationBar navigation)
{
    /// <summary>Taps Add and waits for the sheet.</summary>
    public void Open()
    {
        navigation.TapAdd();
        web.WaitUntil(IsOpen, "the Add sheet didn't open");
    }

    public bool IsOpen() => web.Exists("add-sheet");

    public string Title() => web.FindAll("#add-sheet-title").Single().Text.Trim();

    public void ChooseMeal(string meal)
    {
        web.Tap(MealTestId(meal));
        web.Eventually(() => IsMealChosen(meal), Is.True, $"{meal} chosen on the Add sheet");
    }

    public bool IsMealChosen(string meal) => web.Find(MealTestId(meal)).Selected;

    /// <summary>The meals with a chip, in order.</summary>
    public IReadOnlyList<string> MealLabels() =>
        web.FindAll("[data-testid=\"add-sheet-meals\"] .text").Select(label => label.Text.Trim()).ToList();

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

    private static string MealTestId(string meal) => $"add-sheet-meal-{meal.ToLowerInvariant()}";
}
