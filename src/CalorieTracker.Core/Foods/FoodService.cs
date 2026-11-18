using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Reads one food by id for the screens that show or multiply its values. The stored serving and optional nutrients are not bounded when saved,
/// so they are bounded here: a value out of range is dropped (no serving, or an unknown nutrient) instead of being shown or multiplied.
/// A failing database gives <see cref="FoodStatus.Failed"/>, never an exception. Logs carry ids and counts only.
/// </summary>
public sealed partial class FoodService
{
    /// <summary>Longest serving label kept, in characters after trimming.</summary>
    public const int MaxServingLabelLength = 60;

    /// <summary>Largest serving weight or volume kept, in g or ml.</summary>
    public const double MaxServingAmount = 10_000;

    /// <summary>Largest value kept for each optional nutrient (fibre, sugars, saturates, salt), in g per 100 g or 100 ml. A nutrient cannot exceed its own food.</summary>
    public const double MaxNutrientPer100 = 100;

    private readonly FoodStore store;
    private readonly ILogger<FoodService> logger;

    internal FoodService(FoodStore store, ILogger<FoodService> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    /// <summary>
    /// The food with this id and its servings. A serving needs both a label (not blank, at most <see cref="MaxServingLabelLength"/> characters) and a weight that is finite, above zero and at most <see cref="MaxServingAmount"/>; one that does not is left out.
    /// Only the person's own servings are offered, smallest amount first; a stored Product or Pack row (written before schema step 18) is never offered.
    /// Each optional nutrient that is not finite or outside 0 to <see cref="MaxNutrientPer100"/> comes back null.
    /// </summary>
    public async Task<FoodResult> GetAsync(int id)
    {
        Food? food;
        List<Serving> stored;
        try
        {
            (food, stored) = await store.GetWithServingsAsync(id);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return FoodResult.Failed();
        }

        if (food is null)
        {
            Log.NotFound(logger, id);
            return FoodResult.NotFound();
        }

        var offered = Offered(food.Id, stored, logger);
        Bound(food, logger);
        return FoodResult.Found(food, offered);
    }

    /// <summary>The servings to offer from the stored rows: the person's own, bounded. New objects; the stored rows are not changed. Null <paramref name="logger"/> drops silently.</summary>
    internal static List<Serving> Offered(int foodId, IReadOnlyList<Serving> stored, ILogger? logger = null)
    {
        var offered = new List<Serving>();
        var dropped = 0;
        foreach (var row in stored)
        {
            if (row.Source != ServingSource.User)
            {
                continue;
            }

            if (!TryBoundServing(row.Label, row.Amount, out var label, out var amount))
            {
                dropped++;
                continue;
            }

            offered.Add(new Serving { Id = row.Id, FoodId = row.FoodId, Label = label, Amount = amount, Source = row.Source, SortOrder = row.SortOrder });
        }

        if (dropped > 0 && logger is not null)
        {
            Log.ServingOutOfRange(logger, foodId);
        }

        return offered;
    }

    /// <summary>Applies the nutrient caps of <see cref="GetAsync"/> to a food read elsewhere, in place. Nothing is written back to the database. Null <paramref name="logger"/> bounds silently, as search does on every keystroke.</summary>
    internal static void Bound(Food food, ILogger? logger = null)
    {
        var dropped = 0;
        food.FibreG = Nutrient(food.FibreG, ref dropped);
        food.SugarsG = Nutrient(food.SugarsG, ref dropped);
        food.SaturatesG = Nutrient(food.SaturatesG, ref dropped);
        food.SaltG = Nutrient(food.SaltG, ref dropped);
        if (dropped > 0 && logger is not null)
        {
            Log.NutrientsOutOfRange(logger, food.Id, dropped);
        }
    }

    /// <summary>True when the label and weight make a serving: the trimmed label is not blank and at most <see cref="MaxServingLabelLength"/> characters, and the weight is finite, above zero and at most <see cref="MaxServingAmount"/>.</summary>
    /// <param name="trimmedLabel">The label without surrounding whitespace; empty when false.</param>
    /// <param name="servingAmount">The weight in g or ml; 0 when false.</param>
    public static bool TryBoundServing(string? label, double? amount, out string trimmedLabel, out double servingAmount)
    {
        var trimmed = label?.Trim();
        if (!string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaxServingLabelLength
            && amount is { } a && double.IsFinite(a) && a > 0 && a <= MaxServingAmount)
        {
            trimmedLabel = trimmed;
            servingAmount = a;
            return true;
        }

        trimmedLabel = string.Empty;
        servingAmount = 0;
        return false;
    }

    /// <summary>The value when it is finite and within 0 to <see cref="MaxNutrientPer100"/>; null when it is unknown or out of range.</summary>
    public static double? BoundNutrient(double? value) =>
        value is { } v && double.IsFinite(v) && v >= 0 && v <= MaxNutrientPer100 ? v : null;

    private static double? Nutrient(double? value, ref int dropped)
    {
        var bounded = BoundNutrient(value);
        if (value is not null && bounded is null)
        {
            dropped++;
        }

        return bounded;
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodServingOutOfRange, LogLevel.Debug, "Food {FoodId} has a serving out of range, so it is left out")]
        public static partial void ServingOutOfRange(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodNutrientsOutOfRange, LogLevel.Debug, "Food {FoodId} has {Count} optional nutrients out of range, so they are unknown")]
        public static partial void NutrientsOutOfRange(ILogger logger, int foodId, int count);

        [LoggerMessage((int)CoreEvent.FoodNotFound, LogLevel.Debug, "Food {FoodId} not found")]
        public static partial void NotFound(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodReadFailed, LogLevel.Warning, "Food read failed")]
        public static partial void Failed(ILogger logger);
    }
}
