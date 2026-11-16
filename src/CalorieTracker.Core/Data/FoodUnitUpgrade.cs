using CalorieTracker.Core.Seed;
using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 11: sets the unit of each stored CoFID food to the bundled seed's, so milk, juice, tea and other liquids are measured in millilitres. It does not wait for the seed reload, which can leave a food a diary entry points at as it was.
/// Only <c>Unit</c> changes: a diary entry keeps its amount, and its calories (amount / 100 * the food's per-100 values) are the same in either unit. Safe on a database with no Food table, and when run again.
/// </summary>
internal sealed class FoodUnitUpgrade : ISchemaUpgrade
{
    public int Version => 11;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Food'") == 0)
        {
            return;
        }

        SeedData seed;
        using (var stream = SeedLoader.OpenBundledSeed())
        {
            seed = SeedFile.Parse(stream);
        }

        db.RunInTransaction(() =>
        {
            foreach (var food in seed.Foods)
            {
                db.Execute("UPDATE Food SET Unit = ? WHERE Source = ? AND SourceId = ? AND Unit <> ?", (int)food.Unit, (int)FoodSource.CoFID, food.SourceId, (int)food.Unit);
            }
        });
    }
}
