using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

public interface IFoodSearch
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <summary>
    /// Finds the foods every typed word matches, in any case, ignoring accents, best first. A word matches a food word that equals it or starts with it, a main word one letter off (only for typed words of 4 or more letters), or a word inside the name's brackets, weakest.
    /// Plurals and "yogurt" are treated as their singular and "yoghurt". Foods whose last main word matches, with fewer other words, and common CoFID foods rank higher; ties break by name, then id. Only the first 10 distinct words are used.
    /// A blank query finds nothing.
    /// </summary>
    /// <param name="type">Only foods of this type; null finds every type.</param>
    /// <param name="limit">Most foods to return; kept between 1 and <see cref="MaxLimit"/>.</param>
    Task<FoodSearchResult> SearchAsync(string query, FoodType? type = null, int limit = DefaultLimit, CancellationToken ct = default);
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
