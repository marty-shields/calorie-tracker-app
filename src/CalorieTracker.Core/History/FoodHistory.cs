using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.History;

/// <summary>
/// What the person's kept diary entries say about one food. The last values are those of its newest entry (the highest entry id), whatever the day: in the meal the read was asked for when the food was logged there, else in any meal (all meals when no meal was asked for).
/// </summary>
/// <param name="TimesLogged">Entries of the food; for usual foods, the days it was logged on in that meal.</param>
/// <param name="LastDate">The day of the newest entry.</param>
/// <param name="LastAmount">Grams or millilitres of the newest entry.</param>
/// <param name="LastServings">Servings of the newest entry; null when it was entered as grams or millilitres.</param>
public sealed record FoodHistory(int FoodId, int TimesLogged, int LastEntryId, DateOnly LastDate, double LastAmount, double? LastServings)
{
    internal static FoodHistory From(FoodHistoryRow row) =>
        new(row.FoodId, row.TimesLogged, row.LastEntryId, DiaryEntry.ParseDate(row.LastDate), row.LastAmount, row.LastServings);
}

public enum FoodHistoryStatus
{
    Found,
    Failed,
}

/// <summary>The outcome of a history read. <see cref="Foods"/> is empty unless <see cref="Status"/> is <see cref="FoodHistoryStatus.Found"/>; an empty diary is Found with no foods.</summary>
public sealed record FoodHistoryResult
{
    private FoodHistoryResult(FoodHistoryStatus status, IReadOnlyList<FoodHistory> foods)
    {
        Status = status;
        Foods = foods;
    }

    public FoodHistoryStatus Status { get; }

    public IReadOnlyList<FoodHistory> Foods { get; }

    public static FoodHistoryResult Found(IReadOnlyList<FoodHistory> foods) => new(FoodHistoryStatus.Found, foods ?? throw new ArgumentNullException(nameof(foods)));

    public static FoodHistoryResult Failed() => new(FoodHistoryStatus.Failed, []);
}
