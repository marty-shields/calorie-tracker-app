using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Step 19: creates the empty <c>Favourite</c> table (food id, created at). Foods and diary are untouched. Safe to run again.</summary>
internal sealed class FavouriteTableUpgrade : ISchemaUpgrade
{
    public int Version => 19;

    public void Apply(SQLiteConnection db) =>
        db.Execute("CREATE TABLE IF NOT EXISTS \"Favourite\" (\"FoodId\" integer not null primary key, \"CreatedAt\" bigint not null)");
}
