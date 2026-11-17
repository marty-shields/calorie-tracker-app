using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 10: adds the empty Open Food Facts detail columns to Food (brands, generic name, package, category, popularity, completeness, source update time and fetch time). Food ids are kept.
/// A stored Open Food Facts food has no fetch time, so its next scan looks it up again and fills them.
/// Safe on a database with no Food table, or one that already has the columns.
/// </summary>
internal sealed class FoodSourceDetailsUpgrade : ISchemaUpgrade
{
    private static readonly (string Name, string Type)[] Columns =
    [
        ("Brands", "varchar"),
        ("GenericName", "varchar"),
        ("PackageLabel", "varchar"),
        ("PackageAmount", "float"),
        ("Category", "varchar"),
        ("Popularity", "integer"),
        ("Completeness", "float"),
        ("SourceUpdated", "bigint"),
        ("FetchedAt", "bigint"),
    ];

    public int Version => 10;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'") == 0)
        {
            return;
        }

        foreach (var (name, type) in Columns)
        {
            if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = ?", name) == 0)
            {
                db.Execute($"ALTER TABLE Food ADD COLUMN {name} {type}");
            }
        }
    }
}
