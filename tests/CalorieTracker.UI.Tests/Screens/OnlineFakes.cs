using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.UI.Tests.Screens;

/// <summary>Answers each search from a queue (the last answer repeats) and records the text sent.</summary>
internal sealed class FakeOnlineSearch : IOnlineFoodSearch
{
    private readonly Queue<Func<Task<OnlineSearchResult>>> answers = new();

    public List<string> Queries { get; } = [];

    public void Answer(OnlineSearchResult result) => answers.Enqueue(() => Task.FromResult(result));

    /// <summary>The next search waits for the returned source.</summary>
    public TaskCompletionSource<OnlineSearchResult> Hold()
    {
        var held = new TaskCompletionSource<OnlineSearchResult>();
        answers.Enqueue(() => held.Task);
        return held;
    }

    public Task<OnlineSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        Queries.Add(query);
        return (answers.Count > 1 ? answers.Dequeue() : answers.Count == 1 ? answers.Peek() : () => Task.FromResult(OnlineSearchResult.None()))();
    }
}

internal sealed class FakeOnlineImport : IOnlineFoodImport
{
    private readonly Queue<Func<Task<OnlineImportResult>>> answers = new();

    public List<string> Barcodes { get; } = [];

    public void Answer(OnlineImportResult result) => answers.Enqueue(() => Task.FromResult(result));

    public TaskCompletionSource<OnlineImportResult> Hold()
    {
        var held = new TaskCompletionSource<OnlineImportResult>();
        answers.Enqueue(() => held.Task);
        return held;
    }

    public Task<OnlineImportResult> ImportAsync(string barcode, CancellationToken ct = default)
    {
        Barcodes.Add(barcode);
        return (answers.Count > 1 ? answers.Dequeue() : answers.Count == 1 ? answers.Peek() : () => Task.FromResult(new OnlineImportResult(OnlineImportStatus.NotFound)))();
    }
}

internal sealed class FakeOnlineNotice : IOnlineSearchNotice
{
    public bool Shown { get; set; }

    public int Marked { get; private set; }

    /// <summary>When set, the read and the mark wait for these sources.</summary>
    public TaskCompletionSource? HoldRead { get; set; }

    public TaskCompletionSource? HoldMark { get; set; }

    public async Task<bool> WasShownAsync()
    {
        if (HoldRead is { } read)
        {
            await read.Task;
        }

        return Shown;
    }

    public async Task MarkShownAsync()
    {
        Marked++;
        Shown = true;
        if (HoldMark is { } mark)
        {
            await mark.Task;
        }
    }
}

internal static class OnlineSamples
{
    public static OnlineProduct Can { get; } = Product("5000112637922", "Coca Cola", "330 ml");

    public static OnlineProduct Bottle { get; } = Product("5000112637939", "Coca Cola", "1 L");

    public static OnlineProduct Product(string barcode, string name, string pack, bool isNew = true) =>
        new(barcode, new Food { Source = FoodSource.OpenFoodFacts, SourceId = barcode, Name = name, Brand = "Coca-Cola", Brands = "Coca-Cola", PackageLabel = pack, Unit = FoodUnit.Millilitre, Kcal = 42, ProteinG = 0, CarbsG = 10.6, FatG = 0 }, isNew);
}
