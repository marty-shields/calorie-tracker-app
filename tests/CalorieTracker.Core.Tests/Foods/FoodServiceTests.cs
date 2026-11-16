using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

public class FoodServiceTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<FoodService> logger = null!;
    private FoodService service = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        logger = new FakeLogger<FoodService>();
        service = new FoodService(new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>()), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private async Task<int> Store(Action<Food>? change = null)
    {
        var food = new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = Guid.NewGuid().ToString("N"),
            Name = "Zebra pie",
            Brand = "Stripe Co",
            Kcal = 258,
            ProteinG = 10,
            CarbsG = 30,
            FatG = 9,
            FibreG = 2,
            SugarsG = 3,
            SaturatesG = 4,
            SaltG = 0.5,
            ServingLabel = "1/2 pie (201.8 g)",
            ServingAmount = 201.8,
            SearchText = "zebra pie",
        };
        change?.Invoke(food);
        await connection.InsertAsync(food);
        return food.Id;
    }

    [Test]
    public async Task GetAsync_StoredFood_ReturnsItWithItsValues()
    {
        var id = await Store();

        var result = await service.GetAsync(id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FoodStatus.Found));
            Assert.That(result.Food!.Id, Is.EqualTo(id));
            Assert.That(result.Food.Name, Is.EqualTo("Zebra pie"));
            Assert.That(result.Food.Kcal, Is.EqualTo(258));
            Assert.That(result.Food.ServingLabel, Is.EqualTo("1/2 pie (201.8 g)"));
            Assert.That(result.Food.ServingAmount, Is.EqualTo(201.8));
            Assert.That((result.Food.FibreG, result.Food.SugarsG, result.Food.SaturatesG, result.Food.SaltG), Is.EqualTo(((double?)2, (double?)3, (double?)4, (double?)0.5)));
        });
    }

    [Test]
    public async Task GetAsync_NoSuchFood_ReturnsNotFound()
    {
        var result = await service.GetAsync(99);

        Assert.That((result.Status, result.Food), Is.EqualTo((FoodStatus.NotFound, (Food?)null)));
    }

    [Test]
    public async Task GetAsync_DatabaseFails_ReturnsFailedWithoutThrowing()
    {
        await connection.DropTableAsync<Food>();

        var result = await service.GetAsync(1);

        Assert.That((result.Status, result.Food), Is.EqualTo((FoodStatus.Failed, (Food?)null)));
    }

    [Test]
    public async Task GetAsync_ServingAtTheLimits_IsKept()
    {
        var label = new string('x', FoodService.MaxServingLabelLength);
        var id = await Store(f => (f.ServingLabel, f.ServingAmount) = (label, FoodService.MaxServingAmount));

        var food = (await service.GetAsync(id)).Food!;

        Assert.That((food.ServingLabel, food.ServingAmount), Is.EqualTo((label, (double?)FoodService.MaxServingAmount)));
    }

    [Test]
    public async Task GetAsync_LabelPaddedWithSpaces_IsTrimmed()
    {
        var id = await Store(f => f.ServingLabel = "  1 pot  ");

        Assert.That((await service.GetAsync(id)).Food!.ServingLabel, Is.EqualTo("1 pot"));
    }

    [Test]
    public async Task GetAsync_LabelTooLong_DropsTheWholeServing()
    {
        var id = await Store(f => f.ServingLabel = new string('x', FoodService.MaxServingLabelLength + 1));

        var food = (await service.GetAsync(id)).Food!;

        Assert.That((food.ServingLabel, food.ServingAmount), Is.EqualTo(((string?)null, (double?)null)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task GetAsync_LabelMissingOrBlank_DropsTheWholeServing(string? label)
    {
        var id = await Store(f => f.ServingLabel = label);

        var food = (await service.GetAsync(id)).Food!;

        Assert.That((food.ServingLabel, food.ServingAmount), Is.EqualTo(((string?)null, (double?)null)));
    }

    [TestCase(null)]
    [TestCase(0.0)]
    [TestCase(-5.0)]
    [TestCase(10_000.5)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NaN)]
    public async Task GetAsync_WeightMissingOrOutOfRange_DropsTheWholeServing(double? amount)
    {
        var id = await Store(f => f.ServingAmount = amount);

        var food = (await service.GetAsync(id)).Food!;

        Assert.That((food.ServingLabel, food.ServingAmount), Is.EqualTo(((string?)null, (double?)null)));
    }

    [TestCase(0.0)]
    [TestCase(100.0)]
    public async Task GetAsync_NutrientAtTheLimits_IsKept(double value)
    {
        var id = await Store(f => (f.FibreG, f.SugarsG, f.SaturatesG, f.SaltG) = (value, value, value, value));

        var food = (await service.GetAsync(id)).Food!;

        Assert.That((food.FibreG, food.SugarsG, food.SaturatesG, food.SaltG), Is.EqualTo(((double?)value, (double?)value, (double?)value, (double?)value)));
    }

    [TestCase(-0.1)]
    [TestCase(100.1)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NaN)]
    public async Task GetAsync_NutrientOutOfRange_IsTreatedAsUnknownAndOthersStay(double value)
    {
        var id = await Store(f => f.SugarsG = value);

        var food = (await service.GetAsync(id)).Food!;

        Assert.That((food.FibreG, food.SugarsG, food.SaturatesG, food.SaltG), Is.EqualTo(((double?)2, (double?)null, (double?)4, (double?)0.5)));
    }

    [Test]
    public async Task GetAsync_EachNutrientIsBounded()
    {
        var id = await Store(f => (f.FibreG, f.SugarsG, f.SaturatesG, f.SaltG) = (101, 101, 101, 101));

        var food = (await service.GetAsync(id)).Food!;

        Assert.That((food.FibreG, food.SugarsG, food.SaturatesG, food.SaltG), Is.EqualTo(((double?)null, (double?)null, (double?)null, (double?)null)));
    }

    [Test]
    public async Task GetAsync_BoundsApplyToTheReadOnly_NothingIsWrittenBack()
    {
        var id = await Store(f => f.ServingAmount = 20_000);

        await service.GetAsync(id);

        Assert.That((await connection.FindAsync<Food>(id)).ServingAmount, Is.EqualTo(20_000));
    }

    [Test]
    public async Task GetAsync_Logs_CarryIdsAndCountsNeverWhatTheFoodIs()
    {
        var id = await Store(f => (f.ServingAmount, f.SaltG, f.FibreG) = (0, 500, 500));

        await service.GetAsync(id);
        await service.GetAsync(id + 1);
        await connection.DropTableAsync<Food>();
        await service.GetAsync(id);

        var lines = LoggedEvents.Of(logger);
        Assert.Multiple(() =>
        {
            Assert.That(lines.Select(l => l.Id), Is.EqualTo(new[] { CoreEvent.FoodServingOutOfRange, CoreEvent.FoodNutrientsOutOfRange, CoreEvent.FoodNotFound, CoreEvent.FoodReadFailed }));
            Assert.That(lines[1].Message, Does.Contain($"Food {id} has 2 optional"));
            Assert.That(lines.Select(l => l.Message), Has.None.Contain("Zebra").And.None.Contain("Stripe").And.None.Contain("201.8").And.None.Contain("pie"));
            Assert.That(lines.Where(l => l.Id == CoreEvent.FoodReadFailed).Select(l => l.Level), Is.All.EqualTo(LogLevel.Warning));
        });
    }

    [TestCase(" 1 biscuit ", 15.0, true, "1 biscuit", 15.0)]
    [TestCase("x", 10_000.0, true, "x", 10_000.0)]
    [TestCase(null, 15.0, false, "", 0.0)]
    [TestCase("  ", 15.0, false, "", 0.0)]
    [TestCase("1 biscuit", null, false, "", 0.0)]
    [TestCase("1 biscuit", 0.0, false, "", 0.0)]
    [TestCase("1 biscuit", 10_000.5, false, "", 0.0)]
    [TestCase("1 biscuit", double.NaN, false, "", 0.0)]
    [TestCase("1 biscuit", double.PositiveInfinity, false, "", 0.0)]
    public void TryBoundServing_KeepsOnlyAWellFormedServing(string? label, double? amount, bool expected, string expectedLabel, double expectedAmount)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FoodService.TryBoundServing(label, amount, out var kept, out var weight), Is.EqualTo(expected));
            Assert.That(kept, Is.EqualTo(expectedLabel));
            Assert.That(weight, Is.EqualTo(expectedAmount));
        });
    }

    [Test]
    public void TryBoundServing_ALabelOverTheLimit_IsRejected() =>
        Assert.That(FoodService.TryBoundServing(new string('a', FoodService.MaxServingLabelLength + 1), 10, out _, out _), Is.False);

    [TestCase(0.0, 0.0)]
    [TestCase(5.5, 5.5)]
    [TestCase(100.0, 100.0)]
    public void BoundNutrient_InRange_IsKept(double value, double expected) =>
        Assert.That(FoodService.BoundNutrient(value), Is.EqualTo(expected));

    [TestCase(-0.1)]
    [TestCase(100.1)]
    [TestCase(double.NaN)]
    [TestCase(double.NegativeInfinity)]
    public void BoundNutrient_OutOfRange_IsNull(double value) =>
        Assert.That(FoodService.BoundNutrient(value), Is.Null);

    [Test]
    public void BoundNutrient_Unknown_StaysNull() =>
        Assert.That(FoodService.BoundNutrient(null), Is.Null);
}
