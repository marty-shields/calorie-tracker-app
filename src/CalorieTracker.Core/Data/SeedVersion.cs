using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>The version of a source's bundled seed that was last fully loaded into the Food table.</summary>
public class SeedVersion
{
    [PrimaryKey]
    public FoodSource Source { get; set; }

    [NotNull]
    public int Version { get; set; }
}
