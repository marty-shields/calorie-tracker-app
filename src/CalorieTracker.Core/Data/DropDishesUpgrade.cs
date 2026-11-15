using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 4: the Dish type is gone. Deletes the foods stored as Dish (type 2) and their barcodes, then renumbers Drink from 3 to 2.
/// A database whose CoFID rows were never typed Dish keeps them; the seed load removes CoFID foods the bundled seed no longer has.
/// Must run before any diary entry points at a food. Safe on a database with no Food or FoodBarcode table.
/// </summary>
internal sealed class DropDishesUpgrade : ISchemaUpgrade
{
    // The numbers the types had before this step: Ingredient 0, Food 1, Dish 2, Drink 3.
    private const int OldDish = 2;
    private const int OldDrink = 3;
    private const int NewDrink = 2;

    public int Version => 4;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Type'") == 0)
        {
            return;
        }

        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FoodBarcode'") > 0)
        {
            db.Execute($"DELETE FROM FoodBarcode WHERE FoodId IN (SELECT Id FROM Food WHERE Type = {OldDish})");
        }

        db.Execute($"DELETE FROM Food WHERE Type = {OldDish}");
        db.Execute($"UPDATE Food SET Type = {NewDrink} WHERE Type = {OldDrink}");
    }
}
