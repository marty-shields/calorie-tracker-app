using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>A food the person marked as a favourite. Kept apart from <see cref="Food"/> because seed upserts rewrite Food rows; the seed clean-up keeps every food with a row here.</summary>
public class Favourite
{
    [PrimaryKey]
    public int FoodId { get; set; }

    /// <summary>When the food was marked (UTC).</summary>
    [NotNull]
    public DateTimeOffset CreatedAt { get; set; }
}
