using CalorieTracker.Core.Data;
using CalorieTracker.Core.History;

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
    /// <param name="limit">Most foods to return; kept between 1 and <see cref="MaxLimit"/>.</param>
    Task<FoodSearchResult> SearchAsync(string query, int limit = DefaultLimit, CancellationToken ct = default);

    /// <summary>Most foods a typed group holds (its <see cref="FoodGroup.Total"/> can be higher), and so most shown once its "Show N more" row is tapped; N counts the foods held, not <see cref="FoodGroup.Total"/>.</summary>
    public const int GroupLimit = 50;

    /// <summary>Most foods in the usual group before typing.</summary>
    public const int UsualLimit = 5;

    /// <summary>Most foods in the recent group before typing.</summary>
    public const int RecentLimit = 10;

    /// <summary>
    /// The foods <see cref="SearchAsync"/> matches, ranked the same way, split into up to three groups in this order: <see cref="FoodGroupKind.Diary"/> (foods the person has logged, most logged first, ties in ranked order),
    /// <see cref="FoodGroupKind.Common"/> (CoFID) and <see cref="FoodGroupKind.Branded"/> (Open Food Facts). A food is in one group only, and an empty group is left out. Each holds the best <see cref="GroupLimit"/> foods; <see cref="FoodGroup.Total"/> counts them all.
    /// A failing history read leaves out the diary group instead of failing the search. A blank query finds nothing.
    /// </summary>
    /// <param name="meal">The meal being added to: a food's last values come from that meal when it was logged there (see <see cref="FoodHistory"/>).</param>
    Task<FoodGroupsResult> SearchGroupedAsync(string query, Meal? meal = null, CancellationToken ct = default);

    /// <summary>
    /// The foods to show before typing: <see cref="FoodGroupKind.Usual"/> (with a <paramref name="meal"/> only, up to <see cref="UsualLimit"/>) then <see cref="FoodGroupKind.Recent"/> (up to <see cref="RecentLimit"/>, without any food already in Usual).
    /// An empty diary gives no groups. A failing history read gives <see cref="FoodSearchStatus.Failed"/>.
    /// </summary>
    Task<FoodGroupsResult> SuggestAsync(Meal? meal = null, CancellationToken ct = default);
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
