using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>How <see cref="FavouriteStore.AddAsync"/> ended.</summary>
internal enum AddFavouriteOutcome
{
    Added,
    AlreadyFavourite,
    FoodNotFound,
}

/// <summary>Reads and writes the <see cref="Favourite"/> rows, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed class FavouriteStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    public Task<AddFavouriteOutcome> AddAsync(int foodId, DateTimeOffset now) =>
        gate.RunAsync(async () =>
        {
            var outcome = AddFavouriteOutcome.Added;
            await connection.RunInTransactionAsync(db =>
            {
                if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM Food WHERE Id = ?", foodId) == 0)
                {
                    outcome = AddFavouriteOutcome.FoodNotFound;
                }
                else if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM Favourite WHERE FoodId = ?", foodId) > 0)
                {
                    outcome = AddFavouriteOutcome.AlreadyFavourite;
                }
                else
                {
                    db.Insert(new Favourite { FoodId = foodId, CreatedAt = now });
                }
            });

            return outcome;
        });

    /// <returns>False when the food was not a favourite.</returns>
    public Task<bool> RemoveAsync(int foodId) =>
        gate.RunAsync(async () => await connection.ExecuteAsync("DELETE FROM Favourite WHERE FoodId = ?", foodId) > 0);

    public Task<bool> IsFavouriteAsync(int foodId) =>
        gate.RunAsync(async () => await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Favourite WHERE FoodId = ?", foodId) > 0);

    /// <summary>The food ids, lowest first.</summary>
    public Task<List<int>> IdsAsync() =>
        gate.RunAsync(() => connection.QueryScalarsAsync<int>("SELECT FoodId FROM Favourite ORDER BY FoodId"));
}
