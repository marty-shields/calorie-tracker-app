using CalorieTracker.Core.Data;
using CalorieTracker.Core.Summary;

namespace CalorieTracker.UI.Stories.Stories.Screens;

/// <summary>Sample days for the Dashboard stories. Figures are whole kcal and grams to one decimal, as the day summary rounds them.</summary>
public static class DashboardSamples
{
    public static DateOnly Today { get; } = new(2031, 7, 19);

    private static SummaryEntry Oats { get; } = Entry(1, 21, "Porridge oats", "Quaker", FoodUnit.Gram, 40, null, new(149, 4.4, 24, 3.4));

    private static SummaryEntry Pizza { get; } = Entry(2, 22, "Margherita pizza", "Casa Roma", FoodUnit.Gram, 201.8, 1, new(521, 22.6, 63.6, 20), "½ pizza (201.8 g)");

    private static SummaryEntry Cheese { get; } = Entry(3, 23, "Cheddar cheese", null, FoodUnit.Gram, 30, null, new(125, 7.6, 0, 10.5));

    private static SummaryEntry OatDrink { get; } = Entry(4, 24, "Oat drink", "Oatly", FoodUnit.Millilitre, 200, null, new(92, 2, 13.4, 3));

    private static SummaryEntry Biscuits { get; } = Entry(5, 25, "Digestive biscuits", "Morning Mill", FoodUnit.Gram, 90, 1.5, new(438, 6.3, 55.8, 19.8), "2 biscuits (30 g)");

    /// <summary>Nothing logged, with a target of 2,100.</summary>
    public static DaySummary FirstRun { get; } = Day(2100);

    /// <summary>Breakfast, lunch and a snack, with a target of 2,100.</summary>
    public static DaySummary Loaded { get; } = Day(2100, [Oats], [Pizza], [], [OatDrink]);

    /// <summary>The same day with no target set.</summary>
    public static DaySummary NoTarget { get; } = Day(null, [Oats], [Pizza], [], [OatDrink]);

    /// <summary>More than the target of 1,000 eaten.</summary>
    public static DaySummary OverTarget { get; } = Day(1000, [Oats], [Pizza, Cheese], [], [Biscuits]);

    private static SummaryEntry Entry(int entryId, int foodId, string name, string? brand, FoodUnit unit, double amount, double? servings, Nutrition nutrition, string? servingLabel = null) =>
        new(entryId, foodId, name, brand, unit, amount, servings, nutrition, servingLabel);

    private static DaySummary Day(int? target, params IReadOnlyList<SummaryEntry>[] meals)
    {
        var names = Enum.GetValues<Meal>();
        var summaries = names
            .Select((meal, index) =>
            {
                var entries = index < meals.Length ? meals[index] : [];
                return new MealSummary(meal, Sum(entries.Select(e => e.Nutrition)), entries);
            })
            .ToList();
        var totals = Sum(summaries.Select(m => m.Totals));
        var protein = totals.ProteinG * 4;
        var carbs = totals.CarbsG * 4;
        var fat = totals.FatG * 9;
        var all = protein + carbs + fat;
        var shares = all == 0
            ? new MacroShares(0, 0, 0)
            : new MacroShares((int)Math.Round(100 * protein / all, MidpointRounding.AwayFromZero), (int)Math.Round(100 * carbs / all, MidpointRounding.AwayFromZero), (int)Math.Round(100 * fat / all, MidpointRounding.AwayFromZero));
        return new DaySummary(totals, shares, summaries, target, target - totals.Kcal, target is { } goal && totals.Kcal > goal);
    }

    private static Nutrition Sum(IEnumerable<Nutrition> parts) =>
        parts.Aggregate(Nutrition.Zero, (sum, n) => new Nutrition(sum.Kcal + n.Kcal, Math.Round(sum.ProteinG + n.ProteinG, 1, MidpointRounding.AwayFromZero), Math.Round(sum.CarbsG + n.CarbsG, 1, MidpointRounding.AwayFromZero), Math.Round(sum.FatG + n.FatG, 1, MidpointRounding.AwayFromZero)));
}
