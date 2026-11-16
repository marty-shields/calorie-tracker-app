using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 12: sets the stored CoFID undiluted cordials and squashes (seed 8 measures them in millilitres) to <c>Millilitre</c>. Step 11 already ran on some installs with its list as it was, so its list stays unchanged and this step adds these foods.
/// The ids are frozen here; <c>SeedUnitsTests</c> checks this list and step 11's together equal the seed's millilitre foods. Only <c>Unit</c> changes: a diary entry keeps its amount and its calories. Safe on a database with no Food table, and when run again.
/// </summary>
internal sealed class FoodCordialUnitUpgrade : ISchemaUpgrade
{
    /// <summary>The CoFID sourceIds of the undiluted cordials and squashes, sorted ordinally.</summary>
    internal static readonly IReadOnlyList<string> MillilitreSourceIds =
    [
        "17-200", "17-674", "17-737", "17-738", "17-739", "17-740", "17-743",
    ];

    public int Version => 12;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'") == 0)
        {
            return;
        }

        var marks = string.Join(", ", MillilitreSourceIds.Select(_ => "?"));
        var args = new List<object> { (int)FoodUnit.Millilitre, (int)FoodSource.CoFID, (int)FoodUnit.Millilitre };
        args.AddRange(MillilitreSourceIds);
        db.Execute($"UPDATE Food SET Unit = ? WHERE Source = ? AND Unit <> ? AND SourceId IN ({marks})", args.ToArray());
    }
}
