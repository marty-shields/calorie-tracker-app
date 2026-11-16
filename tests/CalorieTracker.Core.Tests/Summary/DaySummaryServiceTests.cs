using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using CalorieTracker.Core.Summary;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Summary;

public class DaySummaryServiceTests
{
    private static readonly DateOnly Day = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeLogger<DaySummaryService> logger = null!;
    private DiaryService diary = null!;
    private TargetService targets = null!;
    private DaySummaryService service = null!;
    private int cheddar;
    private int cola;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<AppSettings>();
        cheddar = await AddFood("Cheddar", FoodUnit.Gram, 416, 25.4, 0.1, 34.9);
        cola = await AddFood("Cola", FoodUnit.Millilitre, 42, 0, 10.6, 0);
        gate = new DatabaseGate();
        logger = new FakeLogger<DaySummaryService>();
        diary = new DiaryService(new DiaryStore(connection, gate), NullLogger<DiaryService>.Instance);
        targets = new TargetService(new TargetStore(connection, gate), NullLogger<TargetService>.Instance);
        service = new DaySummaryService(new DaySummaryStore(connection, gate), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task DaySummary_CheddarWorkedExample_ScalesRoundsAndSharesCalories()
    {
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 30);
        await targets.SaveAsync(2100);

        var result = await service.GetAsync(Day);

        Assert.That(result.Status, Is.EqualTo(DaySummaryStatus.Ok));
        var s = result.Summary!;
        Assert.Multiple(() =>
        {
            Assert.That(s.Totals, Is.EqualTo(new Nutrition(125, 7.6, 0.0, 10.5)));
            Assert.That(s.Shares, Is.EqualTo(new MacroShares(24, 0, 75)));
            Assert.That((s.TargetKcal, s.KcalLeft, s.IsOverTarget), Is.EqualTo(((int?)2100, (int?)1975, false)));
            var entry = s.Meals.Single(m => m.Meal == Meal.Lunch).Entries.Single();
            Assert.That((entry.Name, entry.Unit, entry.Amount, entry.Nutrition), Is.EqualTo(("Cheddar", FoodUnit.Gram, 30.0, new Nutrition(125, 7.6, 0.0, 10.5))));
        });
    }

    [Test]
    public async Task DaySummary_ExactMidpoints_RoundHalfAwayFromZero()
    {
        var food = await AddFood("Midpoints", FoodUnit.Gram, 45, 2.5, 1.5, 0);
        await diary.AddAsync(Day, Meal.Lunch, food, 10);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That(s.Totals, Is.EqualTo(new Nutrition(5, 0.3, 0.2, 0.0)));
    }

    [Test]
    public async Task DaySummary_TwoDecimalPerHundredValues_RoundAsTheFoodListShowsThem()
    {
        var food = await AddFood("Two decimals", FoodUnit.Gram, 100, 1.45, 0.15, 0.35);
        await diary.AddAsync(Day, Meal.Lunch, food, 100);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That(s.Totals, Is.EqualTo(new Nutrition(100, 1.5, 0.2, 0.4)));
    }

    [Test]
    public async Task DaySummary_AStoredInfiniteMacro_ReturnsFailedAndLogsOverflow()
    {
        var food = await AddFood("Infinite", FoodUnit.Gram, 100, 1, 1, 1);
        await connection.ExecuteAsync("UPDATE Food SET ProteinG = 1e999 WHERE Id = ?", food);
        await diary.AddAsync(Day, Meal.Lunch, food, 100);

        var result = await service.GetAsync(Day);

        Assert.That(result.Status, Is.EqualTo(DaySummaryStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.DaySummaryOverflow }));
    }

    [Test]
    public async Task DaySummary_CarbHeavyMix_SharesRoundUpAndCountCarbsAtFourKcalAGram()
    {
        // 30 g cheddar and 330 ml cola: 30.48 + 140.04 + 94.23 = 264.75 kcal from macros, shares 11.51, 52.89, 35.59.
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 30);
        await diary.AddAsync(Day, Meal.Lunch, cola, 330);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That(s.Shares, Is.EqualTo(new MacroShares(12, 53, 36)));
    }

    [Test]
    public async Task DaySummary_ShareOfExactlyAHalf_RoundsAwayFromZero()
    {
        // 7 g protein and 1 g carbs give 28 and 4 kcal: 87.5 and 12.5 percent.
        var food = await AddFood("Halves", FoodUnit.Gram, 32, 7, 1, 0);
        await diary.AddAsync(Day, Meal.Lunch, food, 100);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That(s.Shares, Is.EqualTo(new MacroShares(88, 13, 0)));
    }

    [Test]
    public async Task DaySummary_PerHundredMillilitres_ScalesLikePerHundredGrams()
    {
        var sameValuesPerGram = await AddFood("Same as cola but per gram", FoodUnit.Gram, 42, 0, 10.6, 0);
        await diary.AddAsync(Day, Meal.Snacks, cola, 330);
        await diary.AddAsync(Day, Meal.Snacks, sameValuesPerGram, 330);

        var entries = (await service.GetAsync(Day)).Summary!.Meals.Single(m => m.Meal == Meal.Snacks).Entries;

        Assert.That(entries.Select(e => e.Nutrition), Is.All.EqualTo(new Nutrition(139, 0, 35.0, 0)));
        Assert.That(entries[0].Unit, Is.EqualTo(FoodUnit.Millilitre));
    }

    [Test]
    public async Task DaySummary_EmptyDay_GivesZerosFourEmptyMealsAndNoLeftWithoutATarget()
    {
        var s = (await service.GetAsync(Day)).Summary!;

        Assert.Multiple(() =>
        {
            Assert.That(s.Totals, Is.EqualTo(Nutrition.Zero));
            Assert.That(s.Shares, Is.EqualTo(new MacroShares(0, 0, 0)));
            Assert.That(s.Meals.Select(m => m.Meal), Is.EqualTo(new[] { Meal.Breakfast, Meal.Lunch, Meal.Dinner, Meal.Snacks }));
            Assert.That(s.Meals.Select(m => (m.Entries.Count, m.Totals)), Is.All.EqualTo((0, Nutrition.Zero)));
            Assert.That((s.TargetKcal, s.KcalLeft, s.IsOverTarget), Is.EqualTo(((int?)null, (int?)null, false)));
        });
    }

    [Test]
    public async Task DaySummary_EmptyDayWithATarget_LeavesTheWholeTarget()
    {
        await targets.SaveAsync(2000);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That((s.KcalLeft, s.IsOverTarget), Is.EqualTo(((int?)2000, false)));
    }

    [Test]
    public async Task DaySummary_NoTargetButFood_GivesNoLeftFigureAndIsNotOver()
    {
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 1000);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That((s.KcalLeft, s.IsOverTarget), Is.EqualTo(((int?)null, false)));
    }

    [Test]
    public async Task DaySummary_OverTarget_IsFlaggedWithNegativeLeft()
    {
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 30);
        await targets.SaveAsync(100);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That((s.KcalLeft, s.IsOverTarget), Is.EqualTo(((int?)-25, true)));
    }

    [Test]
    public async Task DaySummary_ExactlyOnTarget_IsNotOver()
    {
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 30);
        await targets.SaveAsync(125);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That((s.KcalLeft, s.IsOverTarget), Is.EqualTo(((int?)0, false)));
    }

    [Test]
    public async Task DaySummary_GroupsByMealInOrderAddedAndTotalsTheSumOfRoundedEntries()
    {
        // 33 g gives 137.28 kcal, 8.382 g protein: rounded 137 and 8.4 each; three together total 411 and 25.2, where summing unrounded values would give 25.1.
        var a = (await diary.AddAsync(Day, Meal.Dinner, cheddar, 33)).Entry!.Id;
        var other = (await diary.AddAsync(Day, Meal.Breakfast, cola, 100)).Entry!.Id;
        var b = (await diary.AddAsync(Day, Meal.Dinner, cheddar, 33)).Entry!.Id;
        var c = (await diary.AddAsync(Day, Meal.Dinner, cheddar, 33)).Entry!.Id;

        var s = (await service.GetAsync(Day)).Summary!;

        var dinner = s.Meals.Single(m => m.Meal == Meal.Dinner);
        Assert.Multiple(() =>
        {
            Assert.That(dinner.Entries.Select(e => e.EntryId), Is.EqualTo(new[] { a, b, c }));
            Assert.That(dinner.Totals, Is.EqualTo(new Nutrition(411, 25.2, 0.0, 34.5)));
            Assert.That(s.Meals.Single(m => m.Meal == Meal.Breakfast).Entries.Single().EntryId, Is.EqualTo(other));
            Assert.That(s.Totals.Kcal, Is.EqualTo(411 + 42));
            Assert.That(s.Totals.ProteinG, Is.EqualTo(25.2));
        });
    }

    [Test]
    public async Task DaySummary_RemovedEntriesAndOtherDaysAreLeftOut()
    {
        var removed = (await diary.AddAsync(Day, Meal.Lunch, cheddar, 30)).Entry!.Id;
        await diary.AddAsync(Day.AddDays(1), Meal.Lunch, cheddar, 30);
        await diary.RemoveAsync(removed);

        var s = (await service.GetAsync(Day)).Summary!;

        Assert.That(s.Totals, Is.EqualTo(Nutrition.Zero));
    }

    [Test]
    public async Task DaySummary_FoodGone_GivesFoodMissingWithTheEntryIdInsteadOfThrowing()
    {
        await diary.AddAsync(Day, Meal.Lunch, cola, 100);
        var entry = (await diary.AddAsync(Day, Meal.Lunch, cheddar, 30)).Entry!.Id;
        await connection.ExecuteAsync("DELETE FROM Food WHERE Id = ?", cheddar);

        var result = await service.GetAsync(Day);

        Assert.That((result.Status, result.MissingEntryId, result.Summary), Is.EqualTo((DaySummaryStatus.FoodMissing, (int?)entry, (DaySummary?)null)));
        var line = LoggedEvents.Of(logger).Single();
        Assert.That((line.Id, line.Message), Is.EqualTo((CoreEvent.DaySummaryFoodMissing, $"Day summary not built, entry {entry} points at missing food {cheddar}")));
    }

    [Test]
    public async Task DaySummary_WhenADatabaseTableIsMissing_ReturnsFailedInsteadOfThrowing()
    {
        await connection.ExecuteAsync("DROP TABLE DiaryEntry");
        Assert.That((await service.GetAsync(Day)).Status, Is.EqualTo(DaySummaryStatus.Failed));

        await connection.CreateTableAsync<DiaryEntry>();
        await connection.ExecuteAsync("DROP TABLE AppSettings");
        Assert.That((await service.GetAsync(Day)).Status, Is.EqualTo(DaySummaryStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.DaySummaryFailed, CoreEvent.DaySummaryFailed }));
        logger.Collector.Clear();

        await connection.CreateTableAsync<AppSettings>();
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 30);
        await connection.ExecuteAsync("DROP TABLE Food");
        Assert.That((await service.GetAsync(Day)).Status, Is.EqualTo(DaySummaryStatus.Failed));
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.DaySummaryFailed }));
    }

    [Test]
    public async Task DaySummary_TotalsTooLargeForAnInt_ReturnFailedAndLogOverflow()
    {
        // Stored directly: the diary service caps an amount, but a stored row can hold more.
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = cheddar, Amount = 5e8 });
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Dinner, FoodId = cheddar, Amount = 5e8 });
        await targets.SaveAsync(2100);

        var result = await service.GetAsync(Day);

        Assert.That(result.Status, Is.EqualTo(DaySummaryStatus.Failed));
        var line = LoggedEvents.Of(logger).Single();
        Assert.That((line.Id, line.Message), Is.EqualTo((CoreEvent.DaySummaryOverflow, "Day summary not built, a figure was too large")));
    }

    [Test]
    public async Task DaySummary_AStoredMealThatDoesNotExist_ReturnsFailedAndLogsTheEntryId()
    {
        var entry = (await diary.AddAsync(Day, Meal.Lunch, cheddar, 30)).Entry!.Id;
        await connection.ExecuteAsync("UPDATE DiaryEntry SET Meal = 9 WHERE Id = ?", entry);

        var result = await service.GetAsync(Day);

        Assert.That(result.Status, Is.EqualTo(DaySummaryStatus.Failed));
        var line = LoggedEvents.Of(logger).Single();
        Assert.That((line.Id, line.Message), Is.EqualTo((CoreEvent.DaySummaryMealUnknown, $"Day summary not built, entry {entry} has a meal that does not exist")));
    }

    [Test]
    public async Task DaySummary_AnAmountTooLargeToScale_ReturnsFailedInsteadOfThrowing()
    {
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = cheddar, Amount = 1e300 });

        Assert.That((await service.GetAsync(Day)).Status, Is.EqualTo(DaySummaryStatus.Failed));
    }

    [Test]
    public async Task DaySummary_Calls_LogOnlyIdsAndCountsAndNothingTheUserEntered()
    {
        var date = new DateOnly(2047, 3, 29);
        await diary.AddAsync(date, Meal.Snacks, cheddar, 7531.25);
        await targets.SaveAsync(8642);
        await service.GetAsync(date);
        await connection.ExecuteAsync("DELETE FROM Food WHERE Id = ?", cheddar);
        await service.GetAsync(date);
        await connection.ExecuteAsync("DROP TABLE Food");
        await service.GetAsync(date);

        var lines = LoggedEvents.Of(logger);
        Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[] { CoreEvent.DaySummaryBuilt, CoreEvent.DaySummaryFoodMissing, CoreEvent.DaySummaryFailed }));
        var text = string.Join("\n", lines.Select(l => l.Message))
            + "\n" + string.Join("\n", logger.Collector.GetSnapshot().SelectMany(r => r.StructuredState ?? []).Select(p => $"{p.Key}={p.Value}"));
        Assert.That(text, Does.Not.Contain("2047").And.Not.Contain("03-29").And.Not.Contain("7531").And.Not.Contain("8642")
            .And.Not.Contain("Cheddar").And.Not.Contain("Snacks").And.Not.Contain("31330").And.Not.Contain("22688").And.Not.Contain("kcal"));
        Assert.That(lines[0].Message, Does.Contain("from 1 entries"));
    }

    [Test]
    public async Task DaySummary_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            return await connection.Table<DiaryEntry>().CountAsync();
        });

        var summary = service.GetAsync(Day);
        Assert.That(summary.IsCompleted, Is.False);
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = cheddar, Amount = 30 }); // the holder's own write, ahead of the queued call
        holding.SetResult();

        Assert.That(await busy, Is.EqualTo(1));
        Assert.That((await summary).Summary!.Totals.Kcal, Is.EqualTo(125));
    }

    [Test]
    public async Task DaySummary_WritesQueuedBehindTheSummary_NeverShowInHalfOfIt()
    {
        // One gate call reads the target, the entries and the foods, so a write queued behind it cannot land between them.
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 30);
        await targets.SaveAsync(2000);
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(() => holding.Task);

        var summary = service.GetAsync(Day);
        var write = gate.RunAsync(async () =>
        {
            await connection.ExecuteAsync("UPDATE AppSettings SET DailyTargetKcal = 1000");
            await connection.ExecuteAsync("UPDATE Food SET Kcal = 832 WHERE Id = ?", cheddar);
            await connection.ExecuteAsync("DELETE FROM DiaryEntry");
        });
        holding.SetResult();

        await busy;
        await write;
        var s = (await summary).Summary!;
        Assert.That((s.Totals.Kcal, s.TargetKcal, s.Meals.Sum(m => m.Entries.Count)), Is.EqualTo((125, (int?)2000, 1)));
    }

    private async Task<(int FoodId, int ProductId, int PackId, int OwnId)> AddDrink()
    {
        var drink = new Food { Source = FoodSource.OpenFoodFacts, SourceId = "drink", Name = "Cola", Unit = FoodUnit.Millilitre, Kcal = 42, SearchText = "cola" };
        await connection.InsertAsync(drink);
        var product = new Serving { FoodId = drink.Id, Label = "1 glass (250 ml)", Amount = 250, Source = ServingSource.Product, SortOrder = 0 };
        var pack = new Serving { FoodId = drink.Id, Label = "Whole pack (330 ml)", Amount = 330, Source = ServingSource.Pack, SortOrder = 1 };
        var own = new Serving { FoodId = drink.Id, Label = "My mug", Amount = 200, Source = ServingSource.User, SortOrder = 2 };
        await connection.InsertAllAsync(new List<Serving> { product, pack, own });
        return (drink.Id, product.Id, pack.Id, own.Id);
    }

    [Test]
    public async Task DaySummary_EntryCarriesTheLabelOfTheServingItWasAddedBy_AndNullWhenByWeight()
    {
        var (drink, product, pack, own) = await AddDrink();
        await diary.AddAsync(Day, Meal.Lunch, drink, 330, 1, pack);
        await diary.AddAsync(Day, Meal.Lunch, drink, 400, 2, own);
        await diary.AddAsync(Day, Meal.Lunch, drink, 100);
        await diary.AddAsync(Day, Meal.Lunch, cheddar, 30);

        var entries = (await service.GetAsync(Day)).Summary!.Meals.Single(m => m.Meal == Meal.Lunch).Entries;

        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(e => e.ServingLabel), Is.EqualTo(new[] { "Whole pack (330 ml)", "My mug", null, null }));
            Assert.That(entries.Select(e => e.ServingId), Is.EqualTo(new int?[] { pack, own, null, null }));
            Assert.That(entries[0].Servings, Is.EqualTo(1));
            Assert.That(product, Is.Not.EqualTo(pack));
        });
    }

    [Test]
    public async Task DaySummary_EntrySavedBeforeServingsHadATable_NamesNoServingAndKeepsItsCount()
    {
        var (drink, _, _, _) = await AddDrink();
        await diary.AddAsync(Day, Meal.Lunch, drink, 500, 2);

        var entry = (await service.GetAsync(Day)).Summary!.Meals.Single(m => m.Meal == Meal.Lunch).Entries.Single();

        Assert.That((entry.ServingId, entry.ServingLabel, entry.Servings, entry.Amount), Is.EqualTo(((int?)null, (string?)null, (double?)2, 500.0)));
    }

    [Test]
    public async Task DaySummary_ADeletedServing_LeavesTheEntryItsAmountCountAndCalories()
    {
        var (drink, _, _, own) = await AddDrink();
        await diary.AddAsync(Day, Meal.Lunch, drink, 400, 2, own);
        var before = (await service.GetAsync(Day)).Summary!.Meals.Single(m => m.Meal == Meal.Lunch).Entries.Single();
        await new ServingService(new ServingStore(connection, new DatabaseGate()), NullLogger<ServingService>.Instance).RemoveAsync(own);

        var after = (await service.GetAsync(Day)).Summary!.Meals.Single(m => m.Meal == Meal.Lunch).Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(before.ServingLabel, Is.EqualTo("My mug"));
            Assert.That((after.Amount, after.Servings, after.Nutrition), Is.EqualTo((before.Amount, before.Servings, before.Nutrition)));
            Assert.That((after.ServingId, after.ServingLabel), Is.EqualTo(((int?)null, (string?)null)));
        });
    }

    [Test]
    public async Task DaySummary_StoredBrands_GiveTheEntryItsTitleAndBrandLine()
    {
        var nutella = await AddFood("Nutella", FoodUnit.Gram, 539, 6.3, 57.5, 30.9);
        await connection.ExecuteAsync("UPDATE Food SET Brands = ? WHERE Id = ?", "Ferrero, Nutella", nutella);
        var weetabix = await AddFood("Original", FoodUnit.Gram, 362, 11.5, 69, 2);
        await connection.ExecuteAsync("UPDATE Food SET Brands = ? WHERE Id = ?", "Weetabix", weetabix);
        await diary.AddAsync(Day, Meal.Lunch, nutella, 15);
        await diary.AddAsync(Day, Meal.Lunch, weetabix, 40);

        var entries = (await service.GetAsync(Day)).Summary!.Meals.Single(m => m.Meal == Meal.Lunch).Entries;

        Assert.That(entries.Select(e => (e.Display.Title, e.Display.BrandLine)), Is.EqualTo(new[] { ("Nutella", (string?)"Ferrero"), ("Weetabix Original", null) }));
    }

    private async Task<int> AddFood(string name, FoodUnit unit, double kcal, double protein, double carbs, double fat)
    {
        var food = new Food
        {
            Source = FoodSource.CoFID,
            SourceId = name,
            Name = name,
            Unit = unit,
            Kcal = kcal,
            ProteinG = protein,
            CarbsG = carbs,
            FatG = fat,
            SearchText = name.ToLowerInvariant(),
        };
        await connection.InsertAsync(food);
        return food.Id;
    }
}
