using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>A change of the daily target: the target that applies from <see cref="EffectiveFrom"/> until the next row. Kept apart from <see cref="AppSettings"/>, which holds only the current target.</summary>
public class TargetHistory
{
    /// <summary>The local date as <c>yyyy-MM-dd</c>. <c>0001-01-01</c> is the target the person had when the history began.</summary>
    [PrimaryKey]
    public string EffectiveFrom { get; set; } = "";

    /// <summary>Null means the target was cleared from that day.</summary>
    public double? Kcal { get; set; }
}
