using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

public enum ServingStatus
{
    Ok,

    /// <summary>The label is blank or too long, or the amount is not above zero and within <see cref="FoodService.MaxServingAmount"/>.</summary>
    Invalid,

    /// <summary>No food has the id (for an add), or no serving the person made has the id (for a delete).</summary>
    NotFound,

    /// <summary>The food already has a serving of the person's own with that amount.</summary>
    Duplicate,

    Failed,
}

/// <summary>The outcome of adding or deleting a serving. <see cref="Serving"/> is set only for an add that is <see cref="ServingStatus.Ok"/>.</summary>
public sealed record ServingResult
{
    private ServingResult(ServingStatus status, Serving? serving)
    {
        Status = status;
        Serving = serving;
    }

    public ServingStatus Status { get; }

    public Serving? Serving { get; }

    public static ServingResult Ok(Serving? serving = null) => new(ServingStatus.Ok, serving);

    public static ServingResult Invalid() => new(ServingStatus.Invalid, null);

    public static ServingResult NotFound() => new(ServingStatus.NotFound, null);

    public static ServingResult Duplicate() => new(ServingStatus.Duplicate, null);

    public static ServingResult Failed() => new(ServingStatus.Failed, null);
}
