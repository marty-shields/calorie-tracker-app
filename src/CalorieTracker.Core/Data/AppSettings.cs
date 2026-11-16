using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>The app's settings: one row, with <see cref="Id"/> always 1. A null value means the setting is not set.</summary>
public class AppSettings
{
    public const int RowId = 1;

    [PrimaryKey]
    public int Id { get; set; } = RowId;

    /// <summary>Calories a day, a whole number above zero; null when no target is set.</summary>
    public int? DailyTargetKcal { get; set; }

    /// <summary>True once the first-use notice of Open Food Facts search was shown; null before. A column added after schema 14, which the table creation at start adds to a database that has the table.</summary>
    public bool? OnlineSearchNoticeSeen { get; set; }
}
