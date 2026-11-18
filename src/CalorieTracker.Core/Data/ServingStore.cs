using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>How <see cref="ServingStore.AddUserAsync"/> ended.</summary>
internal enum AddUserServingOutcome
{
    Added,
    FoodNotFound,
    Duplicate,
}

/// <summary>Adds and deletes the servings a person made, each call through the <see cref="DatabaseGate"/>.</summary>
internal sealed class ServingStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>Adds a User serving to the food. <see cref="AddUserServingOutcome.Duplicate"/> when the food already has a User serving of that amount; <see cref="AddUserServingOutcome.FoodNotFound"/> when no food has the id.</summary>
    public Task<(AddUserServingOutcome Outcome, Serving? Serving)> AddUserAsync(int foodId, string label, double amount) =>
        gate.RunAsync(async () =>
        {
            var outcome = AddUserServingOutcome.Added;
            Serving? added = null;
            await connection.RunInTransactionAsync(db =>
            {
                if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM Food WHERE Id = ?", foodId) == 0)
                {
                    outcome = AddUserServingOutcome.FoodNotFound;
                    return;
                }

                if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM Serving WHERE FoodId = ? AND Source = ? AND Amount = ?", foodId, (int)ServingSource.User, amount) > 0)
                {
                    outcome = AddUserServingOutcome.Duplicate;
                    return;
                }

                added = new Serving { FoodId = foodId, Label = label, Amount = amount, Source = ServingSource.User };
                db.Insert(added);
            });

            return (outcome, added);
        });

    /// <summary>
    /// Deletes the User serving with this id and clears the serving link of the diary entries that used it, so they keep their amount, count and calories. False when no User serving has the id.
    /// </summary>
    public Task<bool> RemoveUserAsync(int id) =>
        gate.RunAsync(async () =>
        {
            var removed = false;
            await connection.RunInTransactionAsync(db =>
            {
                removed = db.Execute("DELETE FROM Serving WHERE Id = ? AND Source = ?", id, (int)ServingSource.User) > 0;
                if (removed)
                {
                    db.Execute("UPDATE DiaryEntry SET ServingId = NULL WHERE ServingId = ?", id);
                }
            });
            return removed;
        });
}
