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

/// <summary>The target that applied on one day; <see cref="Kcal"/> is null when none did.</summary>
public sealed record DayTarget(DateOnly Date, int? Kcal);

/// <summary>The outcome of <see cref="TargetService.ForRangeAsync"/>. <see cref="Days"/> holds one entry for each day, oldest first; it is empty when <see cref="Status"/> is not <see cref="TargetStatus.Ok"/>.</summary>
public sealed record TargetRangeResult
{
    private TargetRangeResult(TargetStatus status, IReadOnlyList<DayTarget> days)
    {
        Status = status;
        Days = days;
    }

    public TargetStatus Status { get; }

    public IReadOnlyList<DayTarget> Days { get; }

    public static TargetRangeResult Ok(IReadOnlyList<DayTarget> days) => new(TargetStatus.Ok, days);

    public static TargetRangeResult Invalid() => new(TargetStatus.Invalid, []);

    public static TargetRangeResult Failed() => new(TargetStatus.Failed, []);
}
