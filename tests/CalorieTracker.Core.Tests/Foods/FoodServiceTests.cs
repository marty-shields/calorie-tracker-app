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
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        logger = new FakeLogger<FoodService>();
        service = new FoodService(new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>()), logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    /// <summary>Stores a food. Its product serving (label and weight set on the <see cref="ProductRow"/>) is stored as a Product row as it is, unbounded, the way an older app version could have left it; <paramref name="extra"/> rows are stored for the food too.</summary>
    private async Task<int> Store(Action<Food>? change = null, Action<ProductRow>? product = null, params Serving[] extra)
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
            SearchText = "zebra pie",
        };
        var productRow = new ProductRow();
        change?.Invoke(food);
        product?.Invoke(productRow);
        await connection.InsertAsync(food);
        if (productRow.ServingAmount is { } amount)
        {
            await connection.InsertAsync(new Serving { FoodId = food.Id, Label = productRow.ServingLabel ?? "", Amount = amount, Source = ServingSource.Product, SortOrder = 0 });
        }

        foreach (var row in extra)
        {
            row.FoodId = food.Id;
            await connection.InsertAsync(row);
        }

        return food.Id;
    }

    private sealed class ProductRow
    {
        public string? ServingLabel { get; set; } = "1/2 pie (201.8 g)";

        public double? ServingAmount { get; set; } = 201.8;
    }

    private static Serving Pack(double amount, string label = "Whole pack") => new() { Label = label, Amount = amount, Source = ServingSource.Pack, SortOrder = 1 };

    private static Serving Own(string label, double amount) => new() { Label = label, Amount = amount, Source = ServingSource.User, SortOrder = 2 };

    private static (string?, double?) First(FoodResult result) => result.Servings.Count == 0 ? (null, null) : (result.Servings[0].Label, result.Servings[0].Amount);

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
            Assert.That(First(result), Is.EqualTo(((string?)"1/2 pie (201.8 g)", (double?)201.8)));
            Assert.That((result.Food.FibreG, result.Food.SugarsG, result.Food.SaturatesG, result.Food.SaltG), Is.EqualTo(((double?)2, (double?)3, (double?)4, (double?)0.5)));
        });
    }

    [Test]
    public async Task GetAsync_Servings_AreListedSmallestFirstWhateverTheSource()
    {
        var id = await Store(f => f.Unit = FoodUnit.Millilitre, p => (p.ServingLabel, p.ServingAmount) = ("1 glass (250 ml)", 250), Pack(330, "Whole pack (330 ml)"), Own("My mug", 200));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => s.Amount), Is.EqualTo(new[] { 200.0, 250.0, 330.0 }));
    }

    [Test]
    public async Task GetAsync_ServingsOfOneAmount_GoProductThenPackThenOwn()
    {
        var id = await Store(f => f.Unit = FoodUnit.Millilitre, p => (p.ServingLabel, p.ServingAmount) = ("1 glass (250 ml)", 250), Own("My glass", 250), Pack(250, "Whole pack (250 ml)"));

        var stored = await connection.QueryAsync<Serving>("SELECT * FROM Serving WHERE FoodId = ?", id);
        var result = await service.GetAsync(id);

        Assert.That(stored, Has.Count.EqualTo(3));
        Assert.That(result.Servings.Select(s => s.Source), Is.EqualTo(new[] { ServingSource.Product, ServingSource.User }), "a pack equal to the product serving is left out as a repeat");
    }

    [Test]
    public async Task GetAsync_PackAndOwnOfOneAmount_ListsPackFirst()
    {
        var id = await Store(null, p => { p.ServingLabel = null; p.ServingAmount = null; }, Own("My bar", 51), Pack(51, "Whole pack (51 g)"), Own("My small bar", 20));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => (s.Amount, s.Source)), Is.EqualTo(new[] { (20.0, ServingSource.User), (51.0, ServingSource.Pack), (51.0, ServingSource.User) }));
    }

    [Test]
    public async Task GetAsync_OnlyAPackServing_IsWordedAsOneServing()
    {
        var id = await Store(null, p => { p.ServingLabel = null; p.ServingAmount = null; }, Pack(51, "Whole pack (51 g)"));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => (s.Label, s.Amount, s.Source)), Is.EqualTo(new[] { ("1 serving", 51.0, ServingSource.Pack) }));
    }

    [Test]
    public async Task GetAsync_ServingsOfAllSources_AreListedSmallestFirst()
    {
        var id = await Store(f => f.Unit = FoodUnit.Millilitre, p => (p.ServingLabel, p.ServingAmount) = ("1 glass (250 ml)", 250), Pack(330, "Whole pack (330 ml)"), Own("My mug", 200));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => s.Amount), Is.EqualTo(new[] { 200.0, 250.0, 330.0 }));
    }

    [Test]
    public async Task GetAsync_TiesAcrossSizes_GoProductThenPackThenOwn()
    {
        var id = await Store(f => f.Unit = FoodUnit.Millilitre, p => (p.ServingLabel, p.ServingAmount) = ("1 can (330 ml)", 330), Pack(330, "Whole pack (330 ml)"), Own("My can", 330), Own("My sip", 100));
        await connection.ExecuteAsync("INSERT INTO Serving (FoodId, Label, Amount, Source, SortOrder) VALUES (?, 'Bottle 500 ml', 500, 1, 1)", id);

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => (s.Amount, s.Source)), Is.EqualTo(new[]
        {
            (100.0, ServingSource.User),
            (330.0, ServingSource.Product),
            (330.0, ServingSource.User),
            (500.0, ServingSource.Pack),
        }));
    }

    [Test]
    public async Task GetAsync_PackNextToAProductServing_KeepsItsClearLabel()
    {
        var id = await Store(f => f.Unit = FoodUnit.Millilitre, p => (p.ServingLabel, p.ServingAmount) = ("1 glass (250 ml)", 250), Pack(330, "Whole pack (330 ml)"));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => (s.Label, s.Amount, s.Source)), Is.EqualTo(new[]
        {
            ("1 glass (250 ml)", 250.0, ServingSource.Product),
            ("Whole pack (330 ml)", 330.0, ServingSource.Pack),
        }));
    }

    [Test]
    public async Task GetAsync_PackOfTheProductServingsAmount_IsLeftOutAsARepeat()
    {
        var id = await Store(null, p => (p.ServingLabel, p.ServingAmount) = ("1 bar", 51), Pack(51, "Whole pack (51 g)"));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => (s.Label, s.Source)), Is.EqualTo(new[] { ("1 bar", ServingSource.Product) }));
    }

    [Test]
    public async Task GetAsync_ABadProductServing_IsLeftOutAndThePackStays()
    {
        var id = await Store(null, p => p.ServingAmount = 99_999, Pack(51, "Whole pack (51 g)"));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => (s.Label, s.Amount)), Is.EqualTo(new[] { ("1 serving", 51.0) }));
    }

    [Test]
    public async Task GetAsync_Servings_ComeSmallestFirstWhateverTheirSource()
    {
        var id = await Store(null, null, Own("Big", 300), Own("Small", 100), Pack(500, "Whole pack (500 g)"), Pack(400, "Whole pack (400 g)"));

        var result = await service.GetAsync(id);

        Assert.That(result.Servings.Select(s => s.Amount), Is.EqualTo(new[] { 100.0, 201.8, 300.0, 400.0, 500.0 }));
    }

    [Test]
    public async Task GetAsync_ARowThePersonMadeWithAProductsAmount_IsKept()
    {
        var id = await Store(null, p => (p.ServingLabel, p.ServingAmount) = ("1 bar", 50), Own("My bar", 50));

        Assert.That((await service.GetAsync(id)).Servings.Select(s => s.Label), Is.EqualTo(new[] { "1 bar", "My bar" }));
    }

    [Test]
    public async Task GetAsync_NoServingRows_OffersNoServing()
    {
        var id = await Store(f => f.PackageAmount = 51, p => { p.ServingLabel = null; p.ServingAmount = null; });

        Assert.That((await service.GetAsync(id)).Servings, Is.Empty);
    }

    [Test]
    public void MaxPackServingAmount_IsAThousand() =>
        Assert.That(FoodService.MaxPackServingAmount, Is.EqualTo(1000));

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
        var id = await Store(null, p => (p.ServingLabel, p.ServingAmount) = (label, FoodService.MaxServingAmount));

        var result = await service.GetAsync(id);

        Assert.That(First(result), Is.EqualTo(((string?)label, (double?)FoodService.MaxServingAmount)));
    }

    [Test]
    public async Task GetAsync_LabelPaddedWithSpaces_IsTrimmed()
    {
        var id = await Store(null, p => p.ServingLabel = "  1 pot  ");

        Assert.That(First(await service.GetAsync(id)).Item1, Is.EqualTo("1 pot"));
    }

    [Test]
    public async Task GetAsync_LabelTooLong_DropsTheWholeServing()
    {
        var id = await Store(null, p => p.ServingLabel = new string('x', FoodService.MaxServingLabelLength + 1));

        Assert.That(First(await service.GetAsync(id)), Is.EqualTo(((string?)null, (double?)null)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task GetAsync_LabelMissingOrBlank_DropsTheWholeServing(string? label)
    {
        var id = await Store(null, p => p.ServingLabel = label);

        Assert.That(First(await service.GetAsync(id)), Is.EqualTo(((string?)null, (double?)null)));
    }

    [TestCase(0.0)]
    [TestCase(-5.0)]
    [TestCase(10_000.5)]
    [TestCase(double.PositiveInfinity)]
    public async Task GetAsync_WeightOutOfRange_DropsTheWholeServing(double? amount)
    {
        var id = await Store(null, p => p.ServingAmount = amount);

        Assert.That(First(await service.GetAsync(id)), Is.EqualTo(((string?)null, (double?)null)));
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
        var id = await Store(null, p => p.ServingAmount = 20_000);

        await service.GetAsync(id);

        Assert.That(await connection.QueryScalarsAsync<double>($"SELECT Amount FROM Serving WHERE FoodId = {id}"), Is.EqualTo(new[] { 20_000.0 }));
    }

    [Test]
    public async Task GetAsync_Logs_CarryIdsAndCountsNeverWhatTheFoodIs()
    {
        var id = await Store(f => (f.SaltG, f.FibreG) = (500, 500), p => p.ServingAmount = 0);

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
