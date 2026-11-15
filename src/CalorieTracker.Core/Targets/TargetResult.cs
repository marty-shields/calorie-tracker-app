namespace CalorieTracker.Core.Targets;

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
