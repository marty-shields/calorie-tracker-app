using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 3: gives every stored food a type and makes the column not null. A food stored before types existed is Drink when it is an Open Food Facts food measured in
/// millilitres, otherwise Food (a CoFID row too, until the seed refresh sets its own). Safe on a database with no Food table.
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

        var food = (int)FoodType.Food;
        var drink = (int)FoodType.Drink;
        var offMillilitre = $"Source = {(int)FoodSource.OpenFoodFacts} AND Unit = {(int)FoodUnit.Millilitre}";
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Type'") == 0)
        {
            // The column is added here because sqlite-net can't add a not null column without a default.
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
