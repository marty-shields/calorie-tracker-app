using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads what a day summary needs in one <see cref="DatabaseGate"/> call and one transaction, so the target, the entries and the foods agree with each other.</summary>
internal sealed class DaySummaryStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>The saved target (null when none), the kept entries of the day in the order added, the foods they point at (a food that no longer exists is missing from <see cref="DayRead.Foods"/>) and the servings the entries were added by.</summary>
    public Task<DayRead> ReadDayAsync(string date) =>
        gate.RunAsync(async () =>
        {
            DayRead? read = null;
            // The transaction is the read-consistency backstop: the gate already keeps other writers out, but a write that bypassed it must not split the three reads.
            await connection.RunInTransactionAsync(db =>
            {
                var target = TargetStore.Get(db);
                var entries = DiaryStore.ListDay(db, date);
                var foods = FoodStore.FindIn(db, entries.Select(e => e.FoodId).ToList()).ToDictionary(f => f.Id);
                var servings = new Dictionary<int, Serving>();
                foreach (var chunk in entries.Where(e => e.ServingId is not null).Select(e => e.ServingId!.Value).Distinct().Chunk(500))
                {
                    var marks = string.Join(",", chunk.Select(_ => "?"));
                    foreach (var serving in db.Query<Serving>($"SELECT * FROM Serving WHERE Id IN ({marks})", chunk.Cast<object>().ToArray()))
                    {
                        servings[serving.Id] = serving;
                    }
                }

                var byFood = new Dictionary<int, List<Serving>>();
                foreach (var foodId in entries.Where(e => e.ServingId is not null).Select(e => e.FoodId).Distinct())
                {
                    byFood[foodId] = FoodStore.ServingsOf(db, foodId);
                }

                read = new DayRead(target, entries, foods, servings, byFood);
            });

            return read!;
        });
}

/// <summary>What <see cref="DaySummaryStore.ReadDayAsync"/> read.</summary>
/// <param name="Servings">The servings the entries point at, by serving id.</param>
/// <param name="FoodServings">Every serving, in listing order, of each food that has an entry added by servings, by food id.</param>
internal sealed record DayRead(int? TargetKcal, IReadOnlyList<DiaryEntry> Entries, IReadOnlyDictionary<int, Food> Foods, IReadOnlyDictionary<int, Serving> Servings, IReadOnlyDictionary<int, List<Serving>> FoodServings);
