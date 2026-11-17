using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

public enum FoodStatus
{
    Found,
    NotFound,
    Failed,
}

/// <summary>The outcome of a food read. <see cref="Food"/> and <see cref="Servings"/> are set only when <see cref="Status"/> is <see cref="FoodStatus.Found"/>.</summary>
public sealed record FoodResult
{
    private FoodResult(FoodStatus status, Food? food, IReadOnlyList<Serving> servings)
    {
        Status = status;
        Food = food;
        Servings = servings;
    }

    public FoodStatus Status { get; }

    public Food? Food { get; }

    /// <summary>The servings on offer, in listing order (see <see cref="FoodService.GetAsync"/>); empty when the food has none.</summary>
    public IReadOnlyList<Serving> Servings { get; }

    public static FoodResult Found(Food food, IReadOnlyList<Serving>? servings = null) =>
        new(FoodStatus.Found, food ?? throw new ArgumentNullException(nameof(food)), servings ?? []);

    public static FoodResult NotFound() => new(FoodStatus.NotFound, null, []);

    public static FoodResult Failed() => new(FoodStatus.Failed, null, []);
}
