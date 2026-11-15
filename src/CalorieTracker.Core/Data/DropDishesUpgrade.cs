using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 4: the Dish type is gone. Deletes the foods stored as Dish (type 2) and their barcodes, then renumbers Drink from 3 to 2.
/// A database whose CoFID rows were never typed Dish keeps them; the seed load removes CoFID foods the bundled seed no longer has.
/// Keeps a Dish a diary entry points at (as a Food), with its barcodes, if the database already has the diary table. Safe on a database with no Food, FoodBarcode or DiaryEntry table.
/// </summary>
internal sealed class DropDishesUpgrade : ISchemaUpgrade
{
    // The numbers the types had before this step: Ingredient 0, Food 1, Dish 2, Drink 3.
    private const int OldDish = 2;
    private const int OldDrink = 3;
    private const int NewDrink = 2;
    private const int NewDish = 1;

    public int Version => 4;

    public void Apply(SQLiteConnection db)
    {
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info('Food') WHERE name = 'Type'") == 0)
        {
            return;
        }

        var dishes = $"SELECT Id FROM Food WHERE Type = {OldDish}";
        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'DiaryEntry'") > 0)
        {
            dishes += " AND Id NOT IN (SELECT FoodId FROM DiaryEntry)";
        }

        if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'FoodBarcode'") > 0)
        {
            db.Execute($"DELETE FROM FoodBarcode WHERE FoodId IN ({dishes})");
        }

        db.Execute($"DELETE FROM Food WHERE Id IN ({dishes})");
        // A Dish kept for a diary entry becomes a Food, so it is not mistaken for a renumbered Drink.
        db.Execute($"UPDATE Food SET Type = {NewDish} WHERE Type = {OldDish}");
        db.Execute($"UPDATE Food SET Type = {NewDrink} WHERE Type = {OldDrink}");
    }
}
