using System.Globalization;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;

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
            Round(ToDecimal(ProteinG) + ToDecimal(other.ProteinG)),
            Round(ToDecimal(CarbsG) + ToDecimal(other.CarbsG)),
            Round(ToDecimal(FatG) + ToDecimal(other.FatG)));

    // Via the shortest round-trip text, so 1.45 is 1.45 and not its binary neighbour 1.4499999999999999556, matching what the app shows. A non-finite or too large value overflows.
    internal static decimal ToDecimal(double value) =>
        double.IsFinite(value)
            ? decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture)
            : throw new OverflowException();

    /// <summary>The nutrition of <paramref name="amount"/> grams or millilitres of the food: its per-100 values scaled by amount over 100 and rounded as above.</summary>
    internal static Nutrition ForAmount(Food food, double amount)
    {
        var scale = (decimal)amount / 100m;
        return new Nutrition(
            (int)Math.Round(ToDecimal(food.Kcal) * scale, MidpointRounding.AwayFromZero),
            Round(ToDecimal(food.ProteinG) * scale),
            Round(ToDecimal(food.CarbsG) * scale),
            Round(ToDecimal(food.FatG) * scale));
    }

    internal static double Round(decimal grams) => (double)Math.Round(grams, 1, MidpointRounding.AwayFromZero);
}

/// <summary>Each macro's share of the day's calories as a whole percent (protein and carbs 4 kcal a gram, fat 9). They are zero on an empty day and may add up to 99 or 101.</summary>
public sealed record MacroShares(int ProteinPercent, int CarbsPercent, int FatPercent);

/// <summary>A kept entry with its food looked up. <see cref="Nutrition"/> is for the entry's amount. <see cref="ServingLabel"/> is the label of the serving the entry was added by (such as "1 slice (28 g)"), null when the entry was added by weight, was saved before servings had their own table, or its serving was deleted.</summary>
/// <param name="ServingId">The serving the entry was added by; null when it has none (see <see cref="DiaryEntry.ServingId"/>).</param>
public sealed record SummaryEntry(int EntryId, int FoodId, string Name, string? Brand, FoodUnit Unit, double Amount, double? Servings, Nutrition Nutrition, string? ServingLabel = null, string? Brands = null, int? ServingId = null)
{
    /// <summary>The title and brand line the diary row shows. The pack is not read for a diary row, so <see cref="FoodDisplay.Pack"/> is null.</summary>
    public FoodDisplay Display { get; } = FoodDisplay.Of(Name, Brand, Brands, null);
}

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
