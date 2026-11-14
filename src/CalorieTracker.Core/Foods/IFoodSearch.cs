using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

public interface IFoodSearch
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <summary>
    /// Finds foods whose words start with every word typed, in any order and either case. Whole-word matches come before prefix matches, then by name (ignoring case). Only the first 10 distinct words are used.
    /// A blank query finds nothing.
    /// </summary>
    /// <param name="limit">Most foods to return; kept between 1 and <see cref="MaxLimit"/>.</param>
    Task<FoodSearchResult> SearchAsync(string query, int limit = DefaultLimit, CancellationToken ct = default);
}

public enum FoodSearchStatus
{
    Found,
    Failed,
}

/// <summary>The outcome of a food search. <see cref="Foods"/> is empty unless <see cref="Status"/> is <see cref="FoodSearchStatus.Found"/>.</summary>
public sealed record FoodSearchResult
{
    private FoodSearchResult(FoodSearchStatus status, IReadOnlyList<Food> foods)
    {
        Status = status;
        Foods = foods;
    }

    public FoodSearchStatus Status { get; }

    public IReadOnlyList<Food> Foods { get; }

    public static FoodSearchResult Found(IReadOnlyList<Food> foods) => new(FoodSearchStatus.Found, foods ?? throw new ArgumentNullException(nameof(foods)));

    public static FoodSearchResult Failed() => new(FoodSearchStatus.Failed, []);
}
