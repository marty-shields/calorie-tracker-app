using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 3: gives every stored food a type: adds the not null column, or fills the nulls of a development build's nullable one. A food stored before types existed is Drink when it is an Open Food Facts food measured in
/// millilitres (no categories are stored, so the unit is all there is), otherwise Food (a CoFID row too, until the seed refresh sets its own). Safe on a database with no Food table.
/// </summary>
internal sealed class FoodTypeUpgrade : ISchemaUpgrade
{
    public int Version => 3;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food')") == 0)
        {
            return;
        }

        // The numbers the types had at this step (Dish was 2, so Drink was 3); step 4 renumbers them.
        const int food = 1;
        const int drink = 3;
        var offMillilitre = $"Source = {(int)FoodSource.OpenFoodFacts} AND Unit = {(int)FoodUnit.Millilitre}";
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Type'") == 0)
        {
            // SQLite cannot ADD COLUMN NOT NULL without a default, so an upgraded database keeps DEFAULT 1 that a new one (created without it) lacks.
            db.Execute($"ALTER TABLE Food ADD COLUMN \"Type\" integer not null DEFAULT {food}");
            db.Execute($"UPDATE Food SET Type = {drink} WHERE {offMillilitre}");
        }
        else
        {
            // A nullable column from a development build: fill the nulls.
            db.Execute($"UPDATE Food SET Type = CASE WHEN {offMillilitre} THEN {drink} ELSE {food} END WHERE Type IS NULL");
        }
    }
}
