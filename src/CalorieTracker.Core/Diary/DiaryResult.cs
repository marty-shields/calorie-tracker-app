using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Diary;

public enum DiaryStatus
{
    Ok,
    Invalid,
    NotFound,
    Failed,
}

/// <summary>
/// The outcome of a diary call. <see cref="Entry"/> is the entry added; <see cref="Entries"/> is the list for a day.
/// Each is empty (null, or no entries) unless <see cref="Status"/> is <see cref="DiaryStatus.Ok"/> and the call gives that value.
/// </summary>
public sealed record DiaryResult
{
    private DiaryResult(DiaryStatus status, DiaryEntry? entry, IReadOnlyList<DiaryEntry> entries)
    {
        Status = status;
        Entry = entry;
        Entries = entries;
    }

    public DiaryStatus Status { get; }

    public DiaryEntry? Entry { get; }

    public IReadOnlyList<DiaryEntry> Entries { get; }

    public static DiaryResult Ok(DiaryEntry? entry = null) => new(DiaryStatus.Ok, entry, []);

    public static DiaryResult Listed(IReadOnlyList<DiaryEntry> entries) => new(DiaryStatus.Ok, null, entries);

    public static DiaryResult Invalid() => new(DiaryStatus.Invalid, null, []);

    public static DiaryResult NotFound() => new(DiaryStatus.NotFound, null, []);

    public static DiaryResult Failed() => new(DiaryStatus.Failed, null, []);
}
