using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Diary;

public class DiaryServiceTests
{
    private static readonly DateOnly Day = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeLogger<DiaryService> logger = null!;
    private DiaryService service = null!;
    private int foodId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        var food = new Food { Source = FoodSource.CoFID, SourceId = "x", Name = "Zebra pie", SearchText = "zebra pie" };
        await connection.InsertAsync(food);
        foodId = food.Id;
        gate = new DatabaseGate();
        logger = new FakeLogger<DiaryService>();
        service = new DiaryService(new DiaryStore(connection, gate), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private async Task<(int Slice, int Bowl, int Other)> AddServings()
    {
        var other = new Food { Source = FoodSource.CoFID, SourceId = "y", Name = "Other", SearchText = "other" };
        await connection.InsertAsync(other);
        var rows = new List<Serving>
        {
            new() { FoodId = foodId, Label = "1 slice", Amount = 30, Source = ServingSource.User, SortOrder = 2 },
            new() { FoodId = foodId, Label = "My bowl", Amount = 90, Source = ServingSource.User, SortOrder = 2 },
            new() { FoodId = other.Id, Label = "1 cup", Amount = 240, Source = ServingSource.User, SortOrder = 2 },
        };
        await connection.InsertAllAsync(rows);
        return (rows[0].Id, rows[1].Id, rows[2].Id);
    }

    [Test]
    public async Task AddAsync_WithAServingOfTheFood_StoresTheLinkAlongsideTheCountAndAmount()
    {
        var (_, bowl, _) = await AddServings();

        var added = await service.AddAsync(Day, Meal.Lunch, foodId, 180, 2, bowl);

        var listed = (await service.ListAsync(Day)).Entries!.Single();
        Assert.That((added.Entry!.ServingId, listed.ServingId, listed.Amount, listed.Servings), Is.EqualTo(((int?)bowl, (int?)bowl, 180.0, (double?)2)));
    }

    [Test]
    public async Task AddAsync_ByWeightOrWithAServingOfAnotherFoodOrNone_StoresNoLink()
    {
        var (_, _, other) = await AddServings();

        await service.AddAsync(Day, Meal.Lunch, foodId, 100);
        await service.AddAsync(Day, Meal.Lunch, foodId, 240, 1, other);
        await service.AddAsync(Day, Meal.Lunch, foodId, 30, 1, 9999);

        Assert.That((await service.ListAsync(Day)).Entries!.Select(e => e.ServingId), Is.All.Null);
    }

    [Test]
    public async Task UpdateAsync_ChangesTheServingLinkWithTheAmount_AndClearsItForAWeight()
    {
        var (slice, bowl, other) = await AddServings();
        var added = await service.AddAsync(Day, Meal.Lunch, foodId, 30, 1, slice);

        await service.UpdateAsync(added.Entry!.Id, Day, Meal.Lunch, 180, 2, bowl);
        var toBowl = (await service.ListAsync(Day)).Entries!.Single().ServingId;
        await service.UpdateAsync(added.Entry.Id, Day, Meal.Lunch, 100);
        var toWeight = (await service.ListAsync(Day)).Entries!.Single().ServingId;
        await service.UpdateAsync(added.Entry.Id, Day, Meal.Lunch, 240, 1, other);
        var foreign = (await service.ListAsync(Day)).Entries!.Single().ServingId;

        Assert.That((toBowl, toWeight, foreign), Is.EqualTo(((int?)bowl, (int?)null, (int?)null)));
    }

    [Test]
    public async Task Logs_NeverCarryAServingId_OrLabel()
    {
        var (_, bowl, _) = await AddServings();

        await service.AddAsync(Day, Meal.Lunch, foodId, 180, 2, bowl);

        Assert.That(LoggedEvents.Of(logger).Select(e => e.Message), Has.None.Contain("bowl").And.None.Contain("180"));
    }

    [Test]
    public async Task Changed_IsRaisedOnlyWhenAnEntryChanges()
    {
        var raised = 0;
        service.Changed += () => raised++;

        var added = await service.AddAsync(Day, Meal.Lunch, foodId, 150);
        await service.UpdateAsync(added.Entry!.Id, Day, Meal.Dinner, 200);
        await service.RemoveAsync(added.Entry.Id);
        await service.RestoreAsync(added.Entry.Id);
        Assert.That(raised, Is.EqualTo(4));

        await service.AddAsync(Day, Meal.Lunch, foodId, -1);
        await service.AddAsync(Day, Meal.Lunch, foodId + 99, 10);
        await service.RestoreAsync(added.Entry.Id);
        await service.RemoveAsync(added.Entry.Id + 99);
        Assert.That(raised, Is.EqualTo(4));
    }

    [Test]
    public async Task AddListUpdateRemoveRestore_RoundTrip()
    {
        var added = await service.AddAsync(Day, Meal.Lunch, foodId, 150, 1.5);

        Assert.That(added.Status, Is.EqualTo(DiaryStatus.Ok));
        var entry = added.Entry!;
        Assert.That((entry.Date, entry.Meal, entry.FoodId, entry.Amount, entry.Servings, entry.IsRemoved), Is.EqualTo(("2031-07-19", Meal.Lunch, foodId, 150.0, (double?)1.5, false)));

        var listed = (await service.ListAsync(Day)).Entries.Single();
        Assert.That((listed.Id, listed.Amount, listed.Servings), Is.EqualTo((entry.Id, 150.0, (double?)1.5)));

        Assert.That((await service.UpdateAsync(entry.Id, Day, Meal.Dinner, 200)).Status, Is.EqualTo(DiaryStatus.Ok));
        listed = (await service.ListAsync(Day)).Entries.Single();
        Assert.That((listed.Meal, listed.Amount, listed.Servings, listed.FoodId), Is.EqualTo((Meal.Dinner, 200.0, (double?)null, foodId)));

        Assert.That((await service.RemoveAsync(entry.Id)).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That((await service.ListAsync(Day)).Entries, Is.Empty);

        Assert.That((await service.RestoreAsync(entry.Id)).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That((await service.ListAsync(Day)).Entries.Select(e => (e.Id, e.Amount)), Is.EqualTo(new[] { (entry.Id, 200.0) }));
    }

    [Test]
    public async Task ListAsync_ReturnsOnlyThatDateInTheOrderAdded()
    {
        var other = Day.AddDays(1);
        var a = (await service.AddAsync(Day, Meal.Snacks, foodId, 10)).Entry!.Id;
        await service.AddAsync(other, Meal.Breakfast, foodId, 20);
        var b = (await service.AddAsync(Day, Meal.Breakfast, foodId, 30)).Entry!.Id;
        var c = (await service.AddAsync(Day, Meal.Dinner, foodId, 40)).Entry!.Id;

        var ids = (await service.ListAsync(Day)).Entries.Select(e => e.Id);

        Assert.That(ids, Is.EqualTo(new[] { a, b, c }));
        Assert.That((await service.ListAsync(other)).Entries, Has.Count.EqualTo(1));
        Assert.That((await service.ListAsync(Day.AddDays(2))).Entries, Is.Empty);
    }

    [Test]
    public async Task RestoreAsync_BringsTheEntryBackToItsPlace()
    {
        var a = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;
        var b = (await service.AddAsync(Day, Meal.Lunch, foodId, 20)).Entry!.Id;
        var c = (await service.AddAsync(Day, Meal.Lunch, foodId, 30)).Entry!.Id;

        await service.RemoveAsync(b);
        await service.RestoreAsync(b);

        Assert.That((await service.ListAsync(Day)).Entries.Select(e => e.Id), Is.EqualTo(new[] { a, b, c }));
    }

    [Test]
    public async Task UpdateAsync_ToAnotherDay_MovesTheEntryAndKeepsItsPlaceInTheOrder()
    {
        var a = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;
        var b = (await service.AddAsync(Day, Meal.Lunch, foodId, 20)).Entry!.Id;
        var other = Day.AddDays(1);
        var c = (await service.AddAsync(other, Meal.Lunch, foodId, 30)).Entry!.Id;

        await service.UpdateAsync(a, other, Meal.Lunch, 10);

        Assert.That((await service.ListAsync(Day)).Entries.Select(e => e.Id), Is.EqualTo(new[] { b }));
        Assert.That((await service.ListAsync(other)).Entries.Select(e => e.Id), Is.EqualTo(new[] { a, c }));
    }

    [Test]
    public async Task AddAsync_UnknownFood_ReturnsNotFoundAndAddsNothing()
    {
        var result = await service.AddAsync(Day, Meal.Lunch, foodId + 99, 10);

        Assert.That(result.Status, Is.EqualTo(DiaryStatus.NotFound));
        Assert.That(await connection.Table<DiaryEntry>().CountAsync(), Is.Zero);
    }

    [TestCase(0.0)]
    [TestCase(-5.0)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public async Task AddAndUpdateAsync_BadAmountOrServings_AreInvalidAndChangeNothing(double bad)
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;

        Assert.That((await service.AddAsync(Day, Meal.Lunch, foodId, bad)).Status, Is.EqualTo(DiaryStatus.Invalid));
        Assert.That((await service.AddAsync(Day, Meal.Lunch, foodId, 10, bad)).Status, Is.EqualTo(DiaryStatus.Invalid));
        Assert.That((await service.UpdateAsync(id, Day, Meal.Lunch, bad)).Status, Is.EqualTo(DiaryStatus.Invalid));
        Assert.That((await service.UpdateAsync(id, Day, Meal.Lunch, 10, bad)).Status, Is.EqualTo(DiaryStatus.Invalid));
        var kept = (await service.ListAsync(Day)).Entries.Single();
        Assert.That((kept.Amount, kept.Servings), Is.EqualTo((10.0, (double?)null)));
    }

    [Test]
    public async Task AddAndUpdateAsync_AmountAtTheCap_Saves()
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10_000)).Entry!.Id;

        Assert.That((await service.UpdateAsync(id, Day, Meal.Lunch, 10)).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That((await service.UpdateAsync(id, Day, Meal.Lunch, 10_000)).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That((await service.ListAsync(Day)).Entries.Single().Amount, Is.EqualTo(10_000));
    }

    [TestCase(10_000.01)]
    [TestCase(10_001.0)]
    [TestCase(1e300)]
    public async Task AddAndUpdateAsync_AmountOverTheCap_AreInvalidAndChangeNothing(double tooMuch)
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;

        Assert.That((await service.AddAsync(Day, Meal.Lunch, foodId, tooMuch)).Status, Is.EqualTo(DiaryStatus.Invalid));
        Assert.That((await service.UpdateAsync(id, Day, Meal.Lunch, tooMuch)).Status, Is.EqualTo(DiaryStatus.Invalid));
        var entries = (await service.ListAsync(Day)).Entries;
        Assert.That(entries.Select(e => e.Amount), Is.EqualTo(new[] { 10.0 }));
    }

    [Test]
    public async Task AddAndUpdateAsync_UndefinedMeal_AreInvalid()
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;

        Assert.That((await service.AddAsync(Day, (Meal)9, foodId, 10)).Status, Is.EqualTo(DiaryStatus.Invalid));
        Assert.That((await service.UpdateAsync(id, Day, (Meal)9, 10)).Status, Is.EqualTo(DiaryStatus.Invalid));
    }

    [Test]
    public async Task UpdateRemoveRestoreAsync_WrongStateOrUnknownId_ReturnNotFound()
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;

        Assert.That((await service.UpdateAsync(id + 9, Day, Meal.Lunch, 5)).Status, Is.EqualTo(DiaryStatus.NotFound));
        Assert.That((await service.RemoveAsync(id + 9)).Status, Is.EqualTo(DiaryStatus.NotFound));
        Assert.That((await service.RestoreAsync(id)).Status, Is.EqualTo(DiaryStatus.NotFound));
        await service.RemoveAsync(id);
        Assert.That((await service.RemoveAsync(id)).Status, Is.EqualTo(DiaryStatus.NotFound));
        Assert.That((await service.UpdateAsync(id, Day, Meal.Lunch, 5)).Status, Is.EqualTo(DiaryStatus.NotFound));
    }

    [Test]
    public async Task Date_IsStoredAsPlainText()
    {
        await service.AddAsync(new DateOnly(2031, 1, 2), Meal.Lunch, foodId, 10);

        Assert.That(await connection.ExecuteScalarAsync<string>("SELECT Date FROM DiaryEntry"), Is.EqualTo("2031-01-02"));
    }

    [Test]
    public async Task EveryCall_WhenTheDatabaseFails_ReturnsFailedInsteadOfThrowing()
    {
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        Assert.That((await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Status, Is.EqualTo(DiaryStatus.Failed));
        Assert.That((await service.UpdateAsync(1, Day, Meal.Lunch, 10)).Status, Is.EqualTo(DiaryStatus.Failed));
        Assert.That((await service.RemoveAsync(1)).Status, Is.EqualTo(DiaryStatus.Failed));
        Assert.That((await service.RestoreAsync(1)).Status, Is.EqualTo(DiaryStatus.Failed));
        Assert.That((await service.ListAsync(Day)).Status, Is.EqualTo(DiaryStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.All.EqualTo(CoreEvent.DiaryFailed));
    }

    [Test]
    public async Task Calls_LogOnlyIdsAndCountsAndNothingTheUserEntered()
    {
        // Distinctive values: none may appear in a log line, message or structured value.
        var date = new DateOnly(2047, 3, 29);
        var entry = (await service.AddAsync(date, Meal.Snacks, foodId, 7531.25, 8642.5)).Entry!;
        await service.ListAsync(date);
        await service.UpdateAsync(entry.Id, date, Meal.Snacks, 9753.5);
        await service.RemoveAsync(entry.Id);
        await service.RestoreAsync(entry.Id);
        await service.AddAsync(date, Meal.Snacks, foodId, -1357.5);
        await service.AddAsync(date, Meal.Snacks, foodId + 99, 1);
        await service.RemoveAsync(entry.Id + 50);

        var lines = LoggedEvents.Of(logger);
        Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[]
        {
            CoreEvent.DiaryEntryAdded, CoreEvent.DiaryListed, CoreEvent.DiaryEntryUpdated, CoreEvent.DiaryEntryRemoved, CoreEvent.DiaryEntryRestored,
            CoreEvent.DiaryRejected, CoreEvent.DiaryFoodNotFound, CoreEvent.DiaryEntryNotFound,
        }));
        var text = string.Join("\n", lines.Select(l => l.Message))
            + "\n" + string.Join("\n", logger.Collector.GetSnapshot().SelectMany(r => r.StructuredState ?? []).Select(p => $"{p.Key}={p.Value}"));
        Assert.That(text, Does.Not.Contain("2047").And.Not.Contain("03-29").And.Not.Contain("7531").And.Not.Contain("8642").And.Not.Contain("9753").And.Not.Contain("1357").And.Not.Contain("Zebra").And.Not.Contain("Snacks"));
        Assert.That(lines[1].Message, Does.Contain("1 entries"));
        Assert.That(lines[0].Message, Does.Contain($"entry {entry.Id} added for food {foodId}"));
    }

    [Test]
    public async Task AddAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var (holding, busy) = HoldGate(() => connection.Table<DiaryEntry>().CountAsync());

        var add = service.AddAsync(Day, Meal.Lunch, foodId, 10);
        holding.SetResult();

        Assert.That(await busy, Is.Zero);
        Assert.That((await add).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That(await connection.Table<DiaryEntry>().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;
        var (holding, busy) = HoldGate(() => connection.ExecuteScalarAsync<double>("SELECT Amount FROM DiaryEntry WHERE Id = ?", id));

        var update = service.UpdateAsync(id, Day, Meal.Lunch, 99);
        holding.SetResult();

        Assert.That(await busy, Is.EqualTo(10.0));
        Assert.That((await update).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That(await connection.ExecuteScalarAsync<double>("SELECT Amount FROM DiaryEntry WHERE Id = ?", id), Is.EqualTo(99.0));
    }

    [Test]
    public async Task RemoveAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;
        var (holding, busy) = HoldGate(() => connection.ExecuteScalarAsync<int>("SELECT IsRemoved FROM DiaryEntry WHERE Id = ?", id));

        var remove = service.RemoveAsync(id);
        holding.SetResult();

        Assert.That(await busy, Is.Zero);
        Assert.That((await remove).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT IsRemoved FROM DiaryEntry WHERE Id = ?", id), Is.EqualTo(1));
    }

    [Test]
    public async Task RestoreAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var id = (await service.AddAsync(Day, Meal.Lunch, foodId, 10)).Entry!.Id;
        await service.RemoveAsync(id);
        var (holding, busy) = HoldGate(() => connection.ExecuteScalarAsync<int>("SELECT IsRemoved FROM DiaryEntry WHERE Id = ?", id));

        var restore = service.RestoreAsync(id);
        holding.SetResult();

        Assert.That(await busy, Is.EqualTo(1));
        Assert.That((await restore).Status, Is.EqualTo(DiaryStatus.Ok));
        Assert.That(await connection.ExecuteScalarAsync<int>("SELECT IsRemoved FROM DiaryEntry WHERE Id = ?", id), Is.Zero);
    }

    [Test]
    public async Task ListAsync_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var (holding, busy) = HoldGate(() => Task.FromResult(0));

        var list = service.ListAsync(Day);
        Assert.That(list.IsCompleted, Is.False);
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = foodId, Amount = 5 }); // the holder's own write, ahead of the queued list
        holding.SetResult();

        await busy;
        Assert.That((await list).Entries, Has.Count.EqualTo(1));
    }

    private (TaskCompletionSource Holding, Task<T> Busy) HoldGate<T>(Func<Task<T>> readJustBeforeRelease)
    {
        var holding = new TaskCompletionSource();
        return (holding, gate.RunAsync(async () =>
        {
            await holding.Task;
            return await readJustBeforeRelease();
        }));
    }
}
