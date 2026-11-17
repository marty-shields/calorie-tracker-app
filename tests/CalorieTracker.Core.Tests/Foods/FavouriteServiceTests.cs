using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

public class FavouriteServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<FavouriteService> logger = null!;
    private FavouriteService service = null!;
    private int foodId;
    private int otherId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Favourite>();
        var food = new Food { Source = FoodSource.CoFID, SourceId = "x", Name = "Zebra pie", SearchText = "zebra pie" };
        var other = new Food { Source = FoodSource.CoFID, SourceId = "y", Name = "Yak pie", SearchText = "yak pie" };
        await connection.InsertAsync(food);
        await connection.InsertAsync(other);
        foodId = food.Id;
        otherId = other.Id;
        logger = new FakeLogger<FavouriteService>();
        service = new FavouriteService(new FavouriteStore(connection, new DatabaseGate()), new FakeTimeProvider(Now), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task AddAsync_Twice_LeavesOneRowWithTheTime_AndTheSecondIsAlreadyFavourite()
    {
        var first = await service.AddAsync(foodId);
        var second = await service.AddAsync(foodId);

        var rows = await connection.Table<Favourite>().ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That((first.Status, second.Status), Is.EqualTo((FavouriteStatus.Added, FavouriteStatus.AlreadyFavourite)));
            Assert.That(rows.Select(r => (r.FoodId, r.CreatedAt)), Is.EqualTo(new[] { (foodId, Now) }));
        });
    }

    [Test]
    public async Task AddAsync_UnknownFood_IsFoodNotFound_AndWritesNothing()
    {
        var result = await service.AddAsync(999);

        Assert.That((result.Status, await connection.Table<Favourite>().CountAsync()), Is.EqualTo((FavouriteStatus.FoodNotFound, 0)));
    }

    [Test]
    public async Task RemoveAsync_Twice_IsRemovedThenNotFavourite_AndKeepsTheOthers()
    {
        await service.AddAsync(foodId);
        await service.AddAsync(otherId);

        var first = await service.RemoveAsync(foodId);
        var second = await service.RemoveAsync(foodId);

        Assert.Multiple(async () =>
        {
            Assert.That((first.Status, second.Status), Is.EqualTo((FavouriteStatus.Removed, FavouriteStatus.NotFavourite)));
            Assert.That(await connection.QueryScalarsAsync<int>("SELECT FoodId FROM Favourite"), Is.EqualTo(new[] { otherId }));
        });
    }

    [Test]
    public async Task IsFavouriteAsync_IsTrueForAFavouriteAndFalseOtherwise()
    {
        await service.AddAsync(foodId);

        var yes = await service.IsFavouriteAsync(foodId);
        var no = await service.IsFavouriteAsync(otherId);
        var unknown = await service.IsFavouriteAsync(999);

        Assert.That(
            (yes.Status, yes.IsFavourite, no.Status, no.IsFavourite, unknown.IsFavourite),
            Is.EqualTo((FavouriteReadStatus.Found, true, FavouriteReadStatus.Found, false, false)));
    }

    [Test]
    public async Task IdsAsync_ReturnsExactlyTheFavouriteIds()
    {
        var empty = await service.IdsAsync();
        await service.AddAsync(otherId);
        await service.AddAsync(foodId);
        await service.AddAsync(999);
        await service.RemoveAsync(otherId);
        await service.AddAsync(otherId);

        var ids = await service.IdsAsync();

        Assert.Multiple(() =>
        {
            Assert.That((empty.Status, empty.FoodIds), Is.EqualTo((FavouriteReadStatus.Found, Array.Empty<int>())));
            Assert.That((ids.Status, ids.FoodIds), Is.EqualTo((FavouriteReadStatus.Found, new[] { foodId, otherId })));
        });
    }

    [Test]
    public async Task EveryCall_DatabaseFails_IsFailedWithoutThrowing()
    {
        await connection.DropTableAsync<Favourite>();

        Assert.Multiple(async () =>
        {
            Assert.That((await service.AddAsync(foodId)).Status, Is.EqualTo(FavouriteStatus.Failed));
            Assert.That((await service.RemoveAsync(foodId)).Status, Is.EqualTo(FavouriteStatus.Failed));
            Assert.That((await service.IsFavouriteAsync(foodId)).Status, Is.EqualTo(FavouriteReadStatus.Failed));
            Assert.That((await service.IdsAsync()).Status, Is.EqualTo(FavouriteReadStatus.Failed));
        });
    }

    [Test]
    public async Task Logs_CarryFoodIdsAndCountsOnly_NeverAFoodName()
    {
        await service.AddAsync(foodId);
        await service.AddAsync(foodId);
        await service.AddAsync(999);
        await service.IdsAsync();
        await service.RemoveAsync(foodId);
        await service.RemoveAsync(foodId);
        await connection.DropTableAsync<Favourite>();
        await service.IdsAsync();

        var lines = LoggedEvents.Of(logger);
        Assert.Multiple(() =>
        {
            Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[]
            {
                CoreEvent.FavouriteAdded, CoreEvent.FavouriteAlreadyFavourite, CoreEvent.FavouriteFoodNotFound, CoreEvent.FavouriteListed,
                CoreEvent.FavouriteRemoved, CoreEvent.FavouriteNotFavourite, CoreEvent.FavouriteFailed,
            }));
            Assert.That(lines.Select(l => l.Message), Has.None.Contain("Zebra").And.None.Contain("pie"));
            Assert.That(lines[0].Message, Is.EqualTo($"Food {foodId} marked as a favourite"));
            Assert.That(lines[3].Message, Is.EqualTo("Favourites read, 1 foods"));
            Assert.That(lines.Where(l => l.Id == CoreEvent.FavouriteFailed).Select(l => l.Level), Is.All.EqualTo(LogLevel.Warning));
        });
    }

    [Test]
    public void EventIds_AreInTheTenIdsFrom1070()
    {
        var ids = Enum.GetValues<CoreEvent>().Where(e => e.ToString().StartsWith("Favourite", StringComparison.Ordinal)).ToList();

        Assert.That(ids.Select(i => (int)i), Is.All.InRange(1070, 1079));
        Assert.That(ids, Has.Count.EqualTo(8));
    }
}
