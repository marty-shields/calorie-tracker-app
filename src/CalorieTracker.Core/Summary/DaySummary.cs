using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Summary;

/// <summary>
/// Energy and macros of an amount of food. Energy is whole kcal and each macro grams to one decimal, rounded half away from zero, as the app shows them.
/// A total is the sum of the rounded values it covers, so it equals what a person gets adding up the rows.
/// </summary>
public sealed record Nutrition(int Kcal, double ProteinG, double CarbsG, double FatG)
{
    public static Nutrition Zero { get; } = new(0, 0, 0, 0);

    internal Nutrition Plus(Nutrition other) =>
        new(
            checked(Kcal + other.Kcal),
            Round((decimal)ProteinG + (decimal)other.ProteinG),
            Round((decimal)CarbsG + (decimal)other.CarbsG),
            Round((decimal)FatG + (decimal)other.FatG));

    internal static double Round(decimal grams) => (double)Math.Round(grams, 1, MidpointRounding.AwayFromZero);
}

/// <summary>Each macro's share of the day's calories as a whole percent (protein and carbs 4 kcal a gram, fat 9). They are zero on an empty day and may add up to 99 or 101.</summary>
public sealed record MacroShares(int ProteinPercent, int CarbsPercent, int FatPercent);

/// <summary>A kept entry with its food looked up. <see cref="Nutrition"/> is for the entry's amount.</summary>
public sealed record SummaryEntry(int EntryId, int FoodId, string Name, string? Brand, FoodUnit Unit, double Amount, double? Servings, Nutrition Nutrition);

/// <summary>One meal's entries in the order added, and their total. Empty meals are still listed.</summary>
public sealed record MealSummary(Meal Meal, Nutrition Totals, IReadOnlyList<SummaryEntry> Entries);

/// <summary>Everything the dashboard shows for a day.</summary>
/// <param name="Meals">All four meals, in <see cref="Meal"/> order.</param>
/// <param name="TargetKcal">The daily target; null when none is set.</param>
/// <param name="KcalLeft">Target minus eaten, negative when over; null when there is no target.</param>
/// <param name="IsOverTarget">True when more than the target was eaten; always false with no target.</param>
public sealed record DaySummary(
    Nutrition Totals,
    MacroShares Shares,
    IReadOnlyList<MealSummary> Meals,
    int? TargetKcal,
    int? KcalLeft,
    bool IsOverTarget);
