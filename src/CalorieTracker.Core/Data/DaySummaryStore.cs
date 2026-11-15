using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Reads what a day summary needs in one <see cref="DatabaseGate"/> call and one transaction, so the target, the entries and the foods agree with each other.</summary>
internal sealed class DaySummaryStore(SQLiteAsyncConnection connection, DatabaseGate gate)
{
    /// <summary>The saved target (null when none), the kept entries of the day in the order added, and the foods they point at; a food that no longer exists is missing from <see cref="DayRead.Foods"/>.</summary>
    public Task<DayRead> ReadDayAsync(string date) =>
        gate.RunAsync(async () =>
        {
            DayRead? read = null;
            await connection.RunInTransactionAsync(db =>
            {
                var target = db.Find<AppSettings>(AppSettings.RowId)?.DailyTargetKcal;
                var entries = db.Query<DiaryEntry>("SELECT * FROM DiaryEntry WHERE Date = ? AND IsRemoved = 0 ORDER BY Id", date);
                var foods = new Dictionary<int, Food>();
                // Chunked so a long list stays under SQLite's limit on bound variables.
                foreach (var chunk in entries.Select(e => e.FoodId).Distinct().Chunk(500))
                {
                    var marks = string.Join(",", chunk.Select(_ => "?"));
                    foreach (var food in db.Query<Food>($"SELECT * FROM Food WHERE Id IN ({marks})", chunk.Cast<object>().ToArray()))
                    {
                        foods[food.Id] = food;
                    }
                }

                read = new DayRead(target, entries, foods);
            });

            return read!;
        });
}

/// <summary>What <see cref="DaySummaryStore.ReadDayAsync"/> read.</summary>
internal sealed record DayRead(int? TargetKcal, IReadOnlyList<DiaryEntry> Entries, IReadOnlyDictionary<int, Food> Foods);
