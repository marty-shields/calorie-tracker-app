using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.History;

public class FoodHistoryServiceTests
{
    private static readonly DateOnly Today = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<FoodHistoryService> logger = null!;
    private DiaryService diary = null!;
    private FoodHistoryService service = null!;
    private int oats, eggs, soup;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        oats = await AddFood("Zebra oats");
        eggs = await AddFood("Zebra eggs");
        soup = await AddFood("Zebra soup");
        var gate = new DatabaseGate();
        logger = new FakeLogger<FoodHistoryService>();
        var time = new FakeTimeProvider(new DateTimeOffset(2031, 7, 19, 12, 0, 0, TimeSpan.Zero));
        service = new FoodHistoryService(new FoodHistoryStore(connection, gate), time, logger);
        diary = new DiaryService(new DiaryStore(connection, gate), new FakeLogger<DiaryService>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private async Task<int> AddFood(string name)
    {
        var food = new Food { Source = FoodSource.CoFID, SourceId = name, Name = name, SearchText = name.ToLowerInvariant() };
        await connection.InsertAsync(food);
        return food.Id;
    }

    private async Task<int> Log(int days, Meal meal, int foodId, double amount = 100, double? servings = null) =>
        (await diary.AddAsync(Today.AddDays(-days), meal, foodId, amount, servings)).Entry!.Id;

    [Test]
    public async Task RecentAndAll_CarryTheServingOfTheNewestEntry_NullWhenItWasByWeightOrItsServingWasDeleted()
    {
        var rows = new List<Serving>
        {
            new() { FoodId = oats, Label = "1 bowl", Amount = 40, Source = ServingSource.User },
            new() { FoodId = oats, Label = "1 cup", Amount = 90, Source = ServingSource.User },
        };
        await connection.InsertAllAsync(rows);
        await diary.AddAsync(Today.AddDays(-1), Meal.Lunch, oats, 40, 1, rows[0].Id);
        var newest = await diary.AddAsync(Today, Meal.Dinner, oats, 180, 2, rows[1].Id);
        await Log(0, Meal.Lunch, eggs, 100);

        var inDinner = (await service.RecentAsync(10, Meal.Dinner)).Foods.Single(f => f.FoodId == oats);
        var inLunch = (await service.RecentAsync(10, Meal.Lunch)).Foods.Single(f => f.FoodId == oats);
        var any = (await service.RecentAsync(10)).Foods.Single(f => f.FoodId == oats);
        var eggsLast = (await service.RecentAsync(10)).Foods.Single(f => f.FoodId == eggs);
        await connection.ExecuteAsync("UPDATE DiaryEntry SET ServingId = NULL WHERE Id = ?", newest.Entry!.Id);
        var cleared = (await service.RecentAsync(10)).Foods.Single(f => f.FoodId == oats);

        Assert.Multiple(() =>
        {
            Assert.That((inDinner.LastServingId, inDinner.LastServings, inDinner.LastAmount), Is.EqualTo(((int?)rows[1].Id, (double?)2, 180.0)));
            Assert.That(inLunch.LastServingId, Is.EqualTo(rows[0].Id));
            Assert.That(any.LastServingId, Is.EqualTo(rows[1].Id));
            Assert.That(eggsLast.LastServingId, Is.Null);
            Assert.That(cleared.LastServingId, Is.Null);
        });
    }

    [Test]
    public async Task RecentAsync_ListsEachFoodOnceNewestFirstAndRespectsTheLimit()
    {
        await Log(3, Meal.Lunch, oats);
        await Log(2, Meal.Lunch, eggs);
        await Log(1, Meal.Lunch, oats);
        await Log(0, Meal.Lunch, soup);

        var all = await service.RecentAsync(10);
        var two = await service.RecentAsync(2);

        Assert.That(all.Status, Is.EqualTo(FoodHistoryStatus.Found));
        Assert.That(all.Foods.Select(f => f.FoodId), Is.EqualTo(new[] { soup, oats, eggs }));
        Assert.That(all.Foods.Single(f => f.FoodId == oats).TimesLogged, Is.EqualTo(2));
        Assert.That(two.Foods.Select(f => f.FoodId), Is.EqualTo(new[] { soup, oats }));
    }

    [Test]
    public async Task RecentAsync_NewestMeansHighestEntryIdEvenForAnOlderDay()
    {
        await Log(0, Meal.Lunch, oats);
        await Log(5, Meal.Lunch, eggs);

        Assert.That((await service.RecentAsync()).Foods.Select(f => f.FoodId), Is.EqualTo(new[] { eggs, oats }));
    }

    [Test]
    public async Task RecentAsync_LimitBelowOne_ReturnsOneFood()
    {
        await Log(1, Meal.Lunch, oats);
        await Log(0, Meal.Lunch, eggs);

        Assert.That((await service.RecentAsync(0)).Foods, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UsualAsync_RanksByDaysThenRecencyAndLeavesOutOtherMeals()
    {
        foreach (var d in new[] { 1, 2, 3, 4, 5 })
        {
            await Log(d, Meal.Breakfast, oats);
        }

        await Log(7, Meal.Breakfast, eggs);
        await Log(6, Meal.Breakfast, eggs);
        await Log(0, Meal.Dinner, soup);
        await Log(0, Meal.Dinner, soup);

        var usual = await service.UsualAsync(Meal.Breakfast);

        Assert.That(usual.Status, Is.EqualTo(FoodHistoryStatus.Found));
        Assert.That(usual.Foods.Select(f => (f.FoodId, f.TimesLogged)), Is.EqualTo(new[] { (oats, 5), (eggs, 2) }));
        Assert.That((await service.UsualAsync(Meal.Dinner)).Foods.Select(f => (f.FoodId, f.TimesLogged)), Is.EqualTo(new[] { (soup, 1) }));
    }

    [Test]
    public async Task UsualAsync_SameDaysOrdersByNewestEntryAndRespectsTheLimit()
    {
        await Log(2, Meal.Lunch, oats);
        await Log(1, Meal.Lunch, eggs);
        await Log(0, Meal.Lunch, soup);

        var usual = await service.UsualAsync(Meal.Lunch, 2);

        Assert.That(usual.Foods.Select(f => f.FoodId), Is.EqualTo(new[] { soup, eggs }));
    }

    [Test]
    public async Task UsualAsync_CountsSeveralEntriesOnOneDayOnce()
    {
        await Log(1, Meal.Lunch, oats);
        await Log(1, Meal.Lunch, oats);

        Assert.That((await service.UsualAsync(Meal.Lunch)).Foods.Single().TimesLogged, Is.EqualTo(1));
    }

    [Test]
    public async Task UsualAsync_EntryOlderThanThirtyDays_DoesNotCountButStillShowsInRecent()
    {
        await Log(29, Meal.Breakfast, oats);
        await Log(30, Meal.Breakfast, eggs);

        var usual = await service.UsualAsync(Meal.Breakfast);

        Assert.That(usual.Foods.Select(f => f.FoodId), Is.EqualTo(new[] { oats }));
        Assert.That((await service.RecentAsync()).Foods.Select(f => f.FoodId), Is.EquivalentTo(new[] { oats, eggs }));
    }

    [Test]
    public async Task RemovedEntries_CountForNothingAndRestoringCountsAgain()
    {
        await Log(2, Meal.Breakfast, eggs, 50);
        var removedId = await Log(1, Meal.Breakfast, oats, 80);
        await diary.RemoveAsync(removedId);

        Assert.That((await service.RecentAsync()).Foods.Select(f => f.FoodId), Is.EqualTo(new[] { eggs }));
        Assert.That((await service.UsualAsync(Meal.Breakfast)).Foods.Select(f => f.FoodId), Is.EqualTo(new[] { eggs }));
        Assert.That((await service.AllAsync()).Foods.Select(f => f.FoodId), Is.EqualTo(new[] { eggs }));

        await diary.RestoreAsync(removedId);

        Assert.That((await service.RecentAsync()).Foods.Select(f => f.FoodId), Is.EqualTo(new[] { oats, eggs }));
        Assert.That((await service.UsualAsync(Meal.Breakfast)).Foods.Select(f => f.FoodId), Is.EqualTo(new[] { oats, eggs }));
        Assert.That((await service.AllAsync()).Foods.Single(f => f.FoodId == oats).LastEntryId, Is.EqualTo(removedId));
    }

    [Test]
    public async Task WithAMeal_LastValuesAreFromThatMealElseTheNewestEntry()
    {
        await Log(3, Meal.Dinner, oats, 200);
        await Log(2, Meal.Breakfast, oats, 150, servings: 1.5);
        await Log(1, Meal.Dinner, oats, 210);
        await Log(1, Meal.Dinner, eggs, 90);

        var forBreakfast = (await service.AllAsync(Meal.Breakfast)).Foods.ToDictionary(f => f.FoodId);
        var forDinner = (await service.AllAsync(Meal.Dinner)).Foods.ToDictionary(f => f.FoodId);
        var recentBreakfast = (await service.RecentAsync(10, Meal.Breakfast)).Foods.ToDictionary(f => f.FoodId);
        var usualBreakfast = (await service.UsualAsync(Meal.Breakfast)).Foods.Single();
        var anyMeal = (await service.AllAsync()).Foods.Single(f => f.FoodId == oats);

        Assert.Multiple(() =>
        {
            Assert.That((forBreakfast[oats].LastAmount, forBreakfast[oats].LastServings, forBreakfast[oats].TimesLogged), Is.EqualTo((150.0, (double?)1.5, 3)));
            Assert.That(forBreakfast[eggs].LastAmount, Is.EqualTo(90));
            Assert.That(forDinner[oats].LastAmount, Is.EqualTo(210));
            Assert.That(recentBreakfast[oats].LastAmount, Is.EqualTo(150));
            Assert.That(recentBreakfast[eggs].LastAmount, Is.EqualTo(90));
            Assert.That(usualBreakfast.LastAmount, Is.EqualTo(150));
            Assert.That(anyMeal.LastAmount, Is.EqualTo(210));
        });
    }

    [Test]
    public async Task WithAMeal_RemovedEntryOfThatMealIsIgnored()
    {
        await Log(2, Meal.Dinner, oats, 200);
        var breakfast = await Log(1, Meal.Breakfast, oats, 150);
        await diary.RemoveAsync(breakfast);

        Assert.That((await service.AllAsync(Meal.Breakfast)).Foods.Single().LastAmount, Is.EqualTo(200));
    }

    [Test]
    public async Task LastAmount_IsTheNewestKeptEntrysAmountAndServings()
    {
        await Log(3, Meal.Lunch, oats, 100, 1);
        await Log(2, Meal.Lunch, oats, 250, 2.5);
        var removed = await Log(1, Meal.Lunch, oats, 999, 9);
        await diary.RemoveAsync(removed);

        var last = (await service.RecentAsync()).Foods.Single();

        Assert.That((last.LastAmount, last.LastServings, last.LastDate), Is.EqualTo((250.0, (double?)2.5, Today.AddDays(-2))));
    }

    [Test]
    public async Task LastAmount_WithNullServings_IsNull()
    {
        await Log(3, Meal.Lunch, oats, 100, 1);
        await Log(2, Meal.Lunch, oats, 40);

        Assert.That((await service.RecentAsync()).Foods.Single().LastServings, Is.Null);
        Assert.That((await service.UsualAsync(Meal.Lunch)).Foods.Single().LastServings, Is.Null);
        Assert.That((await service.AllAsync()).Foods.Single(f => f.FoodId == oats).LastServings, Is.Null);
    }

    [Test]
    public async Task AllAsync_GivesCountAndLastForEachLoggedFoodAndSkipsTheRest()
    {
        await Log(3, Meal.Lunch, oats);
        var last = await Log(1, Meal.Dinner, oats, 70);
        await Log(2, Meal.Lunch, eggs);

        var result = await service.AllAsync();

        var only = result.Foods.Single(f => f.FoodId == oats);
        Assert.That(result.Foods.Select(f => f.FoodId), Is.EquivalentTo(new[] { oats, eggs }));
        Assert.That((only.FoodId, only.TimesLogged, only.LastEntryId, only.LastAmount, only.LastDate), Is.EqualTo((oats, 2, last, 70.0, Today.AddDays(-1))));
    }

    [Test]
    public async Task EmptyDiary_ReturnsFoundWithNoFoods()
    {
        foreach (var result in new[] { await service.RecentAsync(), await service.UsualAsync(Meal.Lunch), await service.AllAsync() })
        {
            Assert.That(result.Status, Is.EqualTo(FoodHistoryStatus.Found));
            Assert.That(result.Foods, Is.Empty);
        }
    }

    [Test]
    public async Task EveryCall_WhenTheDatabaseFails_ReturnsFailedInsteadOfThrowing()
    {
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");

        foreach (var result in new[] { await service.RecentAsync(), await service.UsualAsync(Meal.Lunch), await service.AllAsync() })
        {
            Assert.That(result.Status, Is.EqualTo(FoodHistoryStatus.Failed));
            Assert.That(result.Foods, Is.Empty);
        }

        var lines = LoggedEvents.Of(logger);
        Assert.That(lines.Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.FoodHistoryFailed, CoreEvent.FoodHistoryFailed, CoreEvent.FoodHistoryFailed }));
        Assert.That(lines.Select(e => e.Level), Is.All.EqualTo(LogLevel.Warning));
    }

    [Test]
    public async Task RecentAndUsual_LimitAboveTheMaximum_ReturnAtMostTwoHundredFoods()
    {
        var foods = new List<Food>();
        for (var i = 0; i < 201; i++)
        {
            foods.Add(new Food { Source = FoodSource.CoFID, SourceId = $"bulk{i}", Name = $"Bulk {i}", SearchText = $"bulk {i}" });
        }

        await connection.InsertAllAsync(foods);
        await connection.InsertAllAsync(foods.Select(f => new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = f.Id, Amount = 1 }));

        Assert.That((await service.RecentAsync(5000)).Foods, Has.Count.EqualTo(FoodHistoryService.MaxLimit));
        Assert.That((await service.UsualAsync(Meal.Lunch, 5000)).Foods, Has.Count.EqualTo(FoodHistoryService.MaxLimit));
    }

    [Test]
    public async Task EveryRead_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var gate = new DatabaseGate();
        var held = new FoodHistoryService(new FoodHistoryStore(connection, gate), new FakeTimeProvider(new DateTimeOffset(2031, 7, 19, 12, 0, 0, TimeSpan.Zero)), new FakeLogger<FoodHistoryService>());
        var reads = new Func<Task<FoodHistoryResult>>[]
        {
            () => held.RecentAsync(),
            () => held.UsualAsync(Meal.Lunch),
            () => held.AllAsync(),
        };

        foreach (var read in reads)
        {
            var holding = new TaskCompletionSource();
            var busy = gate.RunAsync(async () =>
            {
                await holding.Task;
                return 0;
            });

            var pending = read();
            await Task.Delay(50);
            Assert.That(pending.IsCompleted, Is.False);
            holding.SetResult();

            await busy;
            Assert.That((await pending).Status, Is.EqualTo(FoodHistoryStatus.Found));
        }
    }

    [Test]
    public async Task Calls_LogOnlyCountsAndNothingTheUserEntered()
    {
        var date = new DateOnly(2031, 7, 18);
        await diary.AddAsync(date, Meal.Snacks, soup, 7531.25, 8642.5);
        await service.RecentAsync();
        await service.UsualAsync(Meal.Snacks);
        await service.AllAsync();

        var lines = LoggedEvents.Of(logger);
        Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[] { CoreEvent.FoodHistoryRecent, CoreEvent.FoodHistoryUsual, CoreEvent.FoodHistoryForFoods }));
        var text = string.Join("\n", lines.Select(l => l.Message))
            + "\n" + string.Join("\n", logger.Collector.GetSnapshot().SelectMany(r => r.StructuredState ?? []).Select(p => $"{p.Key}={p.Value}"));
        Assert.That(text, Does.Not.Contain("2031").And.Not.Contain("07-18").And.Not.Contain("7531").And.Not.Contain("8642").And.Not.Contain("Zebra").And.Not.Contain("Snacks"));
        Assert.That(lines[0].Message, Does.Contain("1 foods"));
    }
}
