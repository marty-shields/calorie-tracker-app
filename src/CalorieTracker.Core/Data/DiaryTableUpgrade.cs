using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>
/// Step 7: creates the <c>DiaryEntry</c> table and its indexes, with no row. Safe on a database that already has them.
/// </summary>
internal sealed class DiaryTableUpgrade : ISchemaUpgrade
{
    public int Version => 7;

    public void Apply(SQLiteConnection db)
    {
        db.Execute(
            "CREATE TABLE IF NOT EXISTS \"DiaryEntry\" (\"Id\" integer not null primary key autoincrement, \"Date\" varchar not null, \"Meal\" integer not null, \"FoodId\" integer not null, \"Amount\" float not null, \"Servings\" float, \"IsRemoved\" integer not null)");
        db.Execute("CREATE INDEX IF NOT EXISTS \"DiaryEntry_Date\" ON \"DiaryEntry\"(\"Date\")");
        db.Execute("CREATE INDEX IF NOT EXISTS \"DiaryEntry_FoodId\" ON \"DiaryEntry\"(\"FoodId\")");
    }
}
