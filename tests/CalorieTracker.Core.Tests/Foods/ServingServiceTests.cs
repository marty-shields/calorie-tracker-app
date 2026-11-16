using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

public class ServingServiceTests
{
    private const string Secret = "Grandma's secret pie slice";

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<ServingService> logger = null!;
    private ServingService service = null!;
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
        logger = new FakeLogger<ServingService>();
        service = new ServingService(new ServingStore(connection, new DatabaseGate()), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task AddAsync_ValidServing_IsStoredTrimmedAsAUserRow()
    {
        var result = await service.AddAsync(foodId, "  1 slice  ", 28);

        var row = await connection.Table<Serving>().FirstAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ServingStatus.Ok));
            Assert.That(result.Serving!.Id, Is.EqualTo(row.Id));
            Assert.That((row.Label, row.Amount, row.Source, row.FoodId), Is.EqualTo(("1 slice", 28.0, ServingSource.User, foodId)));
        });
    }

    [TestCase(null, 28.0)]
    [TestCase("", 28.0)]
    [TestCase("   ", 28.0)]
    [TestCase("1 slice", 0.0)]
    [TestCase("1 slice", -1.0)]
    [TestCase("1 slice", 10_000.5)]
    [TestCase("1 slice", double.NaN)]
    [TestCase("1 slice", double.PositiveInfinity)]
    public async Task AddAsync_BadLabelOrAmount_IsInvalidAndStoresNothing(string? label, double amount)
    {
        var result = await service.AddAsync(foodId, label, amount);

        Assert.That((result.Status, await connection.Table<Serving>().CountAsync()), Is.EqualTo((ServingStatus.Invalid, 0)));
    }

    [Test]
    public async Task AddAsync_LabelAtAndOverTheLimit()
    {
        var atLimit = await service.AddAsync(foodId, new string('x', FoodService.MaxServingLabelLength), 10);
        var over = await service.AddAsync(foodId, new string('x', FoodService.MaxServingLabelLength + 1), 11);

        Assert.That((atLimit.Status, over.Status), Is.EqualTo((ServingStatus.Ok, ServingStatus.Invalid)));
    }

    [Test]
    public async Task AddAsync_SameAmountTwice_IsADuplicate()
    {
        await service.AddAsync(foodId, "1 slice", 28);

        var again = await service.AddAsync(foodId, "A slice", 28);

        Assert.That((again.Status, again.Serving, await connection.Table<Serving>().CountAsync()), Is.EqualTo((ServingStatus.Duplicate, (Serving?)null, 1)));
    }

    [Test]
    public async Task AddAsync_UnknownFood_IsNotFound()
    {
        Assert.That((await service.AddAsync(999, "1 slice", 28)).Status, Is.EqualTo(ServingStatus.NotFound));
    }

    [Test]
    public async Task AddAsync_DatabaseFails_IsFailedWithoutThrowing()
    {
        await connection.DropTableAsync<Serving>();

        Assert.That((await service.AddAsync(foodId, "1 slice", 28)).Status, Is.EqualTo(ServingStatus.Failed));
    }

    [Test]
    public async Task RemoveAsync_OwnServing_IsDeleted_AndOthersAreNotFound()
    {
        var own = (await service.AddAsync(foodId, "1 slice", 28)).Serving!;
        await connection.InsertAsync(new Serving { FoodId = foodId, Label = "Whole pack (51 g)", Amount = 51, Source = ServingSource.Pack, SortOrder = 1 });
        var pack = (await connection.Table<Serving>().ToListAsync()).Single(r => r.Source == ServingSource.Pack);

        var removed = await service.RemoveAsync(own.Id);
        var packResult = await service.RemoveAsync(pack.Id);
        var again = await service.RemoveAsync(own.Id);

        Assert.That((removed.Status, packResult.Status, again.Status), Is.EqualTo((ServingStatus.Ok, ServingStatus.NotFound, ServingStatus.NotFound)));
        Assert.That((await connection.Table<Serving>().ToListAsync()).Select(r => r.Source), Is.EqualTo(new[] { ServingSource.Pack }));
    }

    [Test]
    public async Task RemoveAsync_DatabaseFails_IsFailedWithoutThrowing()
    {
        await connection.DropTableAsync<Serving>();

        Assert.That((await service.RemoveAsync(1)).Status, Is.EqualTo(ServingStatus.Failed));
    }

    [Test]
    public async Task Logs_CarryIdsAndCountsNeverALabelOrAnAmount()
    {
        var added = await service.AddAsync(foodId, Secret, 123.456);
        await service.AddAsync(foodId, Secret, 123.456);
        await service.AddAsync(foodId, "", 1);
        await service.AddAsync(999, Secret, 77.7);
        await service.RemoveAsync(added.Serving!.Id);
        await service.RemoveAsync(added.Serving.Id);
        await connection.DropTableAsync<Serving>();
        await service.AddAsync(foodId, Secret, 9.99);

        var lines = LoggedEvents.Of(logger);
        Assert.Multiple(() =>
        {
            Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[]
            {
                CoreEvent.ServingAdded, CoreEvent.ServingDuplicate, CoreEvent.ServingRejected, CoreEvent.ServingFoodNotFound,
                CoreEvent.ServingRemoved, CoreEvent.ServingNotFound, CoreEvent.ServingFailed,
            }));
            Assert.That(lines.Select(l => l.Message), Has.None.Contain("Grandma").And.None.Contain("pie").And.None.Contain("123").And.None.Contain("77.7").And.None.Contain("9.99"));
            Assert.That(lines[0].Message, Is.EqualTo($"Serving {added.Serving!.Id} added for food {foodId}"));
            Assert.That(lines.Where(l => l.Id == CoreEvent.ServingFailed).Select(l => l.Level), Is.All.EqualTo(LogLevel.Warning));
        });
    }

    [Test]
    public void EventIds_AreInTheTenIdsFrom1010()
    {
        var ids = new[] { CoreEvent.ServingServiceBlock, CoreEvent.ServingAdded, CoreEvent.ServingRemoved, CoreEvent.ServingRejected, CoreEvent.ServingFoodNotFound, CoreEvent.ServingDuplicate, CoreEvent.ServingNotFound, CoreEvent.ServingFailed };

        Assert.That(ids.Select(i => (int)i), Is.All.InRange(1010, 1019));
    }
}
