namespace CalorieTracker.Core.Targets;

public interface ITargetService
{
    /// <summary>The saved target, or <see cref="TargetResult.Kcal"/> null when none is set (the default).</summary>
    Task<TargetResult> GetAsync();

    /// <summary>
    /// Saves the target, replacing any earlier one. The value must be a whole number above zero that fits an int;
    /// anything else (zero, negative, fractional, NaN, infinite, too large) returns <see cref="TargetStatus.Invalid"/> and changes nothing.
    /// </summary>
    Task<TargetResult> SaveAsync(double kcal);

    /// <summary>Removes the target, so none is set. Clearing when none is set succeeds.</summary>
    Task<TargetResult> ClearAsync();
}

public enum TargetStatus
{
    Ok,
    Invalid,
    Failed,
}

/// <summary>The outcome of a target call. <see cref="Kcal"/> is the target now stored; null when none is set or <see cref="Status"/> is not <see cref="TargetStatus.Ok"/>.</summary>
public sealed record TargetResult
{
    private TargetResult(TargetStatus status, int? kcal)
    {
        Status = status;
        Kcal = kcal;
    }

    public TargetStatus Status { get; }

    public int? Kcal { get; }

    public static TargetResult Ok(int? kcal) => new(TargetStatus.Ok, kcal);

    public static TargetResult Invalid() => new(TargetStatus.Invalid, null);

    public static TargetResult Failed() => new(TargetStatus.Failed, null);
}
