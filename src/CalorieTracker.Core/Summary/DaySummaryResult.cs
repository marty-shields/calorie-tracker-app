namespace CalorieTracker.Core.Summary;

public enum DaySummaryStatus
{
    Ok,
    FoodMissing,
    Failed,
}

/// <summary>
/// The outcome of a day summary. <see cref="Summary"/> is set only when <see cref="Status"/> is <see cref="DaySummaryStatus.Ok"/>;
/// <see cref="MissingEntryId"/> only when it is <see cref="DaySummaryStatus.FoodMissing"/>.
/// </summary>
public sealed record DaySummaryResult
{
    private DaySummaryResult(DaySummaryStatus status, DaySummary? summary, int? missingEntryId)
    {
        Status = status;
        Summary = summary;
        MissingEntryId = missingEntryId;
    }

    public DaySummaryStatus Status { get; }

    public DaySummary? Summary { get; }

    /// <summary>The first entry, in the order added, whose food no longer exists.</summary>
    public int? MissingEntryId { get; }

    public static DaySummaryResult Ok(DaySummary summary) => new(DaySummaryStatus.Ok, summary, null);

    public static DaySummaryResult FoodMissing(int entryId) => new(DaySummaryStatus.FoodMissing, null, entryId);

    public static DaySummaryResult Failed() => new(DaySummaryStatus.Failed, null, null);
}
