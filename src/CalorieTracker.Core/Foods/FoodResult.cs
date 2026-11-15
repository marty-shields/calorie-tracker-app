using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

public enum FoodStatus
{
    Found,
    NotFound,
    Failed,
}

/// <summary>The outcome of a food read. <see cref="Food"/> is set only when <see cref="Status"/> is <see cref="FoodStatus.Found"/>.</summary>
public sealed record FoodResult
{
    private FoodResult(FoodStatus status, Food? food)
    {
        Status = status;
        Food = food;
    }

    public FoodStatus Status { get; }

    public Food? Food { get; }

    public static FoodResult Found(Food food) => new(FoodStatus.Found, food ?? throw new ArgumentNullException(nameof(food)));

    public static FoodResult NotFound() => new(FoodStatus.NotFound, null);

    public static FoodResult Failed() => new(FoodStatus.Failed, null);
}
