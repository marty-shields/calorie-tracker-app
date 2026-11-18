using System.Globalization;

namespace CalorieTracker.UI.Components.Charts;

public static class TargetStatuses
{
    /// <summary>A day is on target within this share either side of its target.</summary>
    public const double Tolerance = 0.1;

    public static TargetStatus Of(double value, double? target)
    {
        if (target is not > 0)
        {
            return TargetStatus.None;
        }

        return value < target.Value * (1 - Tolerance) - 1e-9 ? TargetStatus.Under
            : value > target.Value * (1 + Tolerance) + 1e-9 ? TargetStatus.Over
            : TargetStatus.On;
    }

    public static string Word(TargetStatus status) => status switch
    {
        TargetStatus.Under => "under target",
        TargetStatus.On => "on target",
        TargetStatus.Over => "over target",
        _ => "",
    };

    /// <summary>The CSS class of a status; "plain" when there is none.</summary>
    public static string Class(TargetStatus status) => status switch
    {
        TargetStatus.Under => "under",
        TargetStatus.On => "on",
        TargetStatus.Over => "over",
        _ => "plain",
    };

    /// <summary>Calories rounded to a whole number with thousands separators, the same in every culture.</summary>
    public static string Kcal(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
}
