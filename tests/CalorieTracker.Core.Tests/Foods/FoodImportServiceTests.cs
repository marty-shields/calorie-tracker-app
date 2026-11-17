using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

/// <summary>
/// Calls the import service directly, as a search import would, with products read from the saved Open Food Facts responses in <c>Fixtures</c> (see the README there).
/// The 1 L and 2 L sizes of Coca-Cola Original are not saved responses: they are the Original fixture with the pack, the name and the serving changed (see <see cref="Resized"/>).
/// </summary>
public class FoodImportServiceTests
{
    private const string CanCode = "5000112637922";
    private const string OriginalCode = "5449000214911";
    private const string ZeroCode = "5449000131805";
    private const string LightCode = "5449000050205";
    private const string OriginalTasteCode = "5449000000439";
    private const string OreoCode = "7622300336738";
    private const string OneLitreCode = "5449000000019";
    private const string TwoLitreCode = "5449000000026";
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FoodStore store = null!;
    private FakeLogger<FoodImportService> logger = null!;
    private FakeTimeProvider clock = null!;
    private FoodImportService importer = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<Barcode>();
        store = new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>());
        logger = new FakeLogger<FoodImportService>();
        clock = new FakeTimeProvider(Now);
        importer = new FoodImportService(store, clock, logger);
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    private static string FixtureText(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Foods", "Fixtures", file));

    /// <summary>The product a lookup of the fixture gives (the same mapping a scan uses).</summary>
    private static async Task<Food> Looked(string json, string barcode)
    {
        using var http = new HttpClient(new JsonHandler(json)) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };
        var lookup = new OpenFoodFactsLookup(http, new FakeLogger<OpenFoodFactsLookup>(), TimeProvider.System);
        var result = await lookup.LookupAsync(barcode);
        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        var food = result.Food!;
        food.ServingLabel = result.Serving?.Label;
        food.ServingAmount = result.Serving?.Amount;
        return food;
    }

    /// <summary>Imports the way the lookup does: the product with the serving its fixture carries.</summary>
    private Task<FoodImportResult> Import(string barcode, Food product) => importer.ImportAsync(barcode, product, product.Product);

    private static Task<Food> Fixture(string file, string barcode) => Looked(FixtureText(file), barcode);

    private static Task<Food> Can() => Fixture("details-cola-can-5000112637922.json", CanCode);

    private static Task<Food> Original() => Fixture("details-cola-original-5449000214911.json", OriginalCode);

    private static Task<Food> Zero() => Fixture("details-cola-zero-5449000131805.json", ZeroCode);

    private static Task<Food> Light() => Fixture("details-cola-light-5449000050205.json", LightCode);

    private static Task<Food> OriginalTaste() => Fixture("details-cola-original-taste-1-5l-5449000000439.json", OriginalTasteCode);

    private static Task<Food> Oreo() => Fixture("details-oreo-7622300336738.json", OreoCode);

    /// <summary>Coca-Cola Original's saved response with another pack: the quantity, the pack amount, the name and a serving of 250 ml.</summary>
    private static Task<Food> Resized(string barcode, string name, string quantity, double packMl, Action<JsonObject>? change = null)
    {
        var root = JsonNode.Parse(FixtureText("details-cola-original-5449000214911.json"))!.AsObject();
        root["code"] = barcode;
        var product = root["product"]!.AsObject();
        product["product_name"] = name;
        product["quantity"] = quantity;
        product["product_quantity"] = packMl;
        product["serving_size"] = "250 ml";
        product["serving_quantity"] = 250;
        change?.Invoke(product);
        return Looked(root.ToJsonString(), barcode);
    }

    private async Task<Food> OnlyFood() => (await connection.Table<Food>().ToListAsync()).Single();

    /// <summary>The can response's own values (its brand, its nutrients with no salt or saturates, and no serving) on a resized product.</summary>
    private static void LikeTheCan(JsonObject product)
    {
        product["brands"] = "Coca-Cola";
        var nutriments = product["nutriments"]!.AsObject();
        nutriments["energy-kcal_100g"] = 42.12;
        nutriments["carbohydrates_100g"] = 10.6;
        nutriments["sugars_100g"] = 10.6;
        nutriments.Remove("salt_100g");
        nutriments.Remove("saturated-fat_100g");
        product.Remove("serving_size");
        product.Remove("serving_quantity");
    }

    private Task<List<Barcode>> BarcodeRows() => connection.QueryAsync<Barcode>("SELECT * FROM Barcode ORDER BY Id");

    private Task<List<Serving>> ServingRows(int foodId) =>
        connection.QueryAsync<Serving>("SELECT * FROM Serving WHERE FoodId = ? ORDER BY SortOrder, Amount, Id", foodId);

    [Test]
    public async Task Import_AnUnknownProduct_CreatesTheFoodItsBarcodeRowAndItsPackServing()
    {
        var result = await Import(CanCode, await Can());

        var food = await OnlyFood();
        var barcode = (await BarcodeRows()).Single();
        Assert.Multiple(async () =>
        {
            Assert.That((result.Status, result.Food!.Id, result.PackAmount), Is.EqualTo((FoodImportStatus.Created, food.Id, (double?)330)));
            Assert.That((food.Name, food.Brand, food.Unit, food.SourceId), Is.EqualTo(("Coca Cola", "Coca-Cola", FoodUnit.Millilitre, CanCode)));
            Assert.That((barcode.FoodId, barcode.Code, barcode.PackageAmount, barcode.PackageLabel, barcode.FirstSeen), Is.EqualTo((food.Id, CanCode, (double?)330, "330 ml", Now)));
            Assert.That((await ServingRows(food.Id)).Select(s => (s.Label, s.Amount, s.Source)), Is.EqualTo(new[] { ("Can 330 ml", 330.0, ServingSource.Pack) }));
        });
    }

    [Test]
    public async Task Import_ABarcodeAlreadyStored_ReturnsItsFoodAndChangesNothing()
    {
        var first = await Import(CanCode, await Can());
        var changed = await Can();
        changed.Kcal = 999;
        changed.Name = "Something else";

        var second = await Import(CanCode, changed);

        var food = await OnlyFood();
        Assert.Multiple(async () =>
        {
            Assert.That((second.Status, second.Food!.Id), Is.EqualTo((FoodImportStatus.Known, first.Food!.Id)));
            Assert.That((food.Name, food.Kcal), Is.EqualTo(("Coca Cola", first.Food.Kcal)));
            Assert.That(await BarcodeRows(), Has.Count.EqualTo(1));
            Assert.That(await ServingRows(food.Id), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_TheSameUnknownProductTwice_MakesOneFood()
    {
        await Import(OriginalCode, await Original());
        var again = await Import(OriginalCode, await Original());

        Assert.Multiple(async () =>
        {
            Assert.That(again.Status, Is.EqualTo(FoodImportStatus.Known));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
            Assert.That(await connection.Table<Barcode>().CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_ConcurrentImportsOfOneBarcode_MakeOneFoodAndOneBarcodeRow()
    {
        var products = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Original()));

        var results = await Task.WhenAll(products.Select(p => Import(OriginalCode, p)));

        Assert.Multiple(async () =>
        {
            Assert.That(results.Count(r => r.Status == FoodImportStatus.Created), Is.EqualTo(1));
            Assert.That(results.Count(r => r.Status == FoodImportStatus.Known), Is.EqualTo(7));
            Assert.That(results.Select(r => r.Food!.Id).Distinct().Count(), Is.EqualTo(1));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
            Assert.That(await connection.Table<Barcode>().CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_ConcurrentImportsOfOneProductInThreeSizes_MakeOneFood()
    {
        var sizes = new[]
        {
            (OriginalCode, await Original()),
            (OneLitreCode, await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000)),
            (TwoLitreCode, await Resized(TwoLitreCode, "Coca-Cola Original 2 litres", "2 litres", 2000)),
        };

        await Task.WhenAll(sizes.Select(s => Import(s.Item1, s.Item2)));

        Assert.Multiple(async () =>
        {
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
            Assert.That(await connection.Table<Barcode>().CountAsync(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Import_CokeOriginalInThreeSizes_IsOneFoodWithThreeBarcodes_KeepingTheStoredFoodsData()
    {
        var can = await Import(OriginalCode, await Original());
        var oneLitre = await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000, p => p["nutriments"]!["energy-kcal_100g"] = 42.5);
        var twoLitre = await Resized(TwoLitreCode, "Coca-Cola Original 2 litres", "2 litres", 2000);

        var one = await Import(OneLitreCode, oneLitre);
        var two = await Import(TwoLitreCode, twoLitre);

        var food = await OnlyFood();
        var barcodes = await BarcodeRows();
        Assert.Multiple(async () =>
        {
            Assert.That(new[] { one.Status, two.Status }, Is.EqualTo(new[] { FoodImportStatus.Attached, FoodImportStatus.Attached }));
            Assert.That(new[] { one.Food!.Id, two.Food!.Id }, Is.All.EqualTo(can.Food!.Id));
            Assert.That((food.Name, food.Brand, food.Kcal, food.SourceId), Is.EqualTo(("Coca-Cola Original", "Coca-Cola", 42.0, OriginalCode)), "the stored food keeps its name, brand and nutrients");
            Assert.That(barcodes.Select(b => (b.Code, b.FoodId, b.PackageAmount, b.PackageLabel)), Is.EqualTo(new[]
            {
                (OriginalCode, food.Id, (double?)330, "330ml"),
                (OneLitreCode, food.Id, (double?)1000, "1L"),
                (TwoLitreCode, food.Id, (double?)2000, "2 litres"),
            }));
            Assert.That(barcodes.Select(b => b.FirstSeen), Is.All.EqualTo(Now));
            Assert.That((one.PackAmount, two.PackAmount), Is.EqualTo(((double?)1000, (double?)2000)));
        });
    }

    [Test]
    public async Task Import_CokeOriginalSizes_GetPackServingsByAmount_ProductServingStaysTheStoredFoods()
    {
        await Import(OriginalCode, await Original());
        await Import(OneLitreCode, await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000));
        await Import(TwoLitreCode, await Resized(TwoLitreCode, "Coca-Cola Original 2 litres", "2 litres", 2000));

        var food = await OnlyFood();

        // 330 ml is the product serving already ("330ml"), so no pack row repeats it; 2 L is over the pack limit and stays on its barcode only.
        Assert.That((await ServingRows(food.Id)).Select(s => (s.Label, s.Amount, s.Source)), Is.EqualTo(new[]
        {
            ("330ml", 330.0, ServingSource.Product),
            ("Bottle 1 L", 1000.0, ServingSource.Pack),
        }));
    }

    [Test]
    public async Task Import_ACanThenALitreOfTheSameProduct_GivesCanAndBottlePackServings_AndTheSameAmountCollapses()
    {
        await Import(CanCode, await Can());
        var litre = await Resized("5000112637939", "Coca Cola 1 L", "1 L", 1000, LikeTheCan);
        var another = await Resized("5000112637946", "Coca Cola 330 ml", "330 ml", 330, LikeTheCan);

        var one = await Import("5000112637939", litre);
        var same = await Import("5000112637946", another);

        var food = await OnlyFood();
        Assert.Multiple(async () =>
        {
            Assert.That((one.Status, same.Status), Is.EqualTo((FoodImportStatus.Attached, FoodImportStatus.Attached)));
            Assert.That(await BarcodeRows(), Has.Count.EqualTo(3));
            Assert.That((await ServingRows(food.Id)).Select(s => (s.Label, s.Amount, s.Source)), Is.EqualTo(new[]
            {
                ("Can 330 ml", 330.0, ServingSource.Pack),
                ("Bottle 1 L", 1000.0, ServingSource.Pack),
            }));
        });
    }

    [Test]
    public async Task Import_APackServingNamedByTheLabelOrByShape_OtherwiseJustPack()
    {
        var weetabix = await Import("5010029000023", await Fixture("details-weetabix-5010029000023.json", "5010029000023"));
        var mars = await Import("5000159407236", await Fixture("details-mars-5000159407236.json", "5000159407236"));
        var nutella = await Import("3017620422003", await Fixture("details-nutella-3017620422003.json", "3017620422003"));

        Assert.Multiple(async () =>
        {
            Assert.That((await ServingRows(weetabix.Food!.Id)).Select(s => (s.Label, s.Source)), Is.EqualTo(new[] { ("37.5 gram", ServingSource.Product), ("Pack 430 g", ServingSource.Pack) }));
            Assert.That((await ServingRows(mars.Food!.Id)).Select(s => (s.Label, s.Source)), Is.EqualTo(new[] { ("Pack 51 g", ServingSource.Pack) }), "its serving is flagged wrong, so only the pack");
            Assert.That((await ServingRows(nutella.Food!.Id)).Select(s => (s.Label, s.Source)), Is.EqualTo(new[] { ("Pack 400 g", ServingSource.Pack) }));
        });
    }

    [Test]
    public async Task Import_ZeroLightOriginalTasteAndOreo_NeverMergeIntoColaOrEachOther()
    {
        var results = new List<FoodImportResult>
        {
            await Import(OriginalCode, await Original()),
            await Import(ZeroCode, await Zero()),
            await Import(LightCode, await Light()),
            await Import(OriginalTasteCode, await OriginalTaste()),
            await Import(OreoCode, await Oreo()),
            await Import(CanCode, await Can()),
        };

        Assert.Multiple(async () =>
        {
            Assert.That(results.Select(r => r.Status), Is.All.EqualTo(FoodImportStatus.Created));
            Assert.That(results.Select(r => r.Food!.Id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(6));
            Assert.That(await connection.Table<Barcode>().CountAsync(), Is.EqualTo(6));
        });
    }

    [Test]
    public async Task Import_VariantsWithTheNameOfTheStoredFood_StillDoNotMergeWhenNutrientsDiffer()
    {
        await Import(OriginalCode, await Original());
        var sugarFree = await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000, p =>
        {
            var n = p["nutriments"]!;
            n["energy-kcal_100g"] = 0.2;
            n["carbohydrates_100g"] = 0;
            n["sugars_100g"] = 0;
        });
        var olderRecipe = await Resized(TwoLitreCode, "Coca-Cola Original 2 litres", "2 litres", 2000, p => p["nutriments"]!["energy-kcal_100g"] = 45);

        var first = await Import(OneLitreCode, sugarFree);
        var second = await Import(TwoLitreCode, olderRecipe);

        Assert.Multiple(async () =>
        {
            Assert.That((first.Status, second.Status), Is.EqualTo((FoodImportStatus.Created, FoodImportStatus.Created)));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Import_ASameNameAndNutrientsInGrams_NeverMergesIntoTheMillilitreFood()
    {
        await Import(OriginalCode, await Original());
        var grams = await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000, p =>
        {
            p["product_quantity_unit"] = "g";
            p["serving_quantity_unit"] = "g";
        });

        var result = await Import(OneLitreCode, grams);

        Assert.That((result.Status, result.Food!.Unit), Is.EqualTo((FoodImportStatus.Created, FoodUnit.Gram)));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Import_TheRealOriginalAnd1_5LOriginalTaste_AreTwoFoods_BecauseTheirNamesAndNutrientsDiffer()
    {
        await Import(OriginalCode, await Original());

        var result = await Import(OriginalTasteCode, await OriginalTaste());

        Assert.That(result.Status, Is.EqualTo(FoodImportStatus.Created));
    }

    [Test]
    public async Task Import_Nutella_TheTwoSavedResponsesOfOneBarcode_AreOneFood()
    {
        var first = await Import("3017620422003", await Fixture("solid-3017620422003.json", "3017620422003"));
        var second = await Import("3017620422003", await Fixture("details-nutella-3017620422003.json", "3017620422003"));

        Assert.That((first.Status, second.Status, second.Food!.Id), Is.EqualTo((FoodImportStatus.Created, FoodImportStatus.Known, first.Food!.Id)));
    }

    [Test]
    public async Task Import_ASourceIdAlreadyStoredWithoutABarcodeRow_IsThatFoodRefreshedAndLinked()
    {
        var stored = await store.UpsertAsync(await Can());
        await connection.ExecuteAsync("DELETE FROM Barcode");

        var result = await Import(CanCode, await Can());

        Assert.Multiple(async () =>
        {
            Assert.That((result.Status, result.Food!.Id), Is.EqualTo((FoodImportStatus.Known, stored.Id)));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.EqualTo(1));
            Assert.That((await BarcodeRows()).Select(b => b.FoodId), Is.EqualTo(new[] { stored.Id }));
        });
    }

    [Test]
    public async Task Import_AMergedFood_KeepsItsDiaryEntriesAndOwnServings()
    {
        var can = await Import(OriginalCode, await Original());
        await connection.InsertAsync(new DiaryEntry { Date = "2031-07-19", Meal = Meal.Lunch, FoodId = can.Food!.Id, Amount = 330 });
        await connection.InsertAsync(new Serving { FoodId = can.Food.Id, Label = "My glass", Amount = 200, Source = ServingSource.User, SortOrder = 2 });

        await Import(OneLitreCode, await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000));

        Assert.Multiple(async () =>
        {
            Assert.That((await connection.Table<DiaryEntry>().ToListAsync()).Select(e => (e.FoodId, e.Amount)), Is.EqualTo(new[] { (can.Food.Id, 330.0) }));
            Assert.That((await ServingRows(can.Food.Id)).Select(s => s.Label), Is.EqualTo(new[] { "330ml", "Bottle 1 L", "My glass" }));
        });
    }

    [Test]
    public async Task Import_ARowForAFoodThatNoLongerExists_DoesNotBlockTheBarcode()
    {
        await connection.ExecuteAsync("INSERT INTO Barcode (FoodId, Barcode, FirstSeen) VALUES (999, ?, 0)", OriginalCode);

        var result = await Import(OriginalCode, await Original());

        Assert.That(result.Status, Is.EqualTo(FoodImportStatus.Created));
        Assert.That((await BarcodeRows()).Single().FoodId, Is.EqualTo(result.Food!.Id));
    }

    [Test]
    public async Task Import_FillsInSearchTextAndSourceIdWhenBlank()
    {
        var product = await Original();
        product.SearchText = "";
        product.SourceId = " ";

        var result = await Import(OriginalCode, product);

        Assert.That((result.Food!.SourceId, result.Food.SearchText), Is.EqualTo((OriginalCode, "coca-cola original coca-cola boisson rafraichissante aux extraits vegetaux sweetened beverages")));
    }

    [TestCase("noname")]
    [TestCase("nokcal")]
    [TestCase("negativefat")]
    [TestCase("nounit")]
    [TestCase("cofid")]
    [TestCase("badbarcode")]
    [TestCase("shortbarcode")]
    [TestCase("badoptional")]
    public async Task Import_AnIncompleteProduct_IsNotImportable_StoresNothingAndDoesNotThrow(string problem)
    {
        var product = await Original();
        var barcode = OriginalCode;
        var expected = NotImportableReason.NoNutrients;
        switch (problem)
        {
            case "noname":
                product.Name = "  ";
                expected = NotImportableReason.NoName;
                break;
            case "nokcal":
                product.Kcal = double.NaN;
                break;
            case "negativefat":
                product.FatG = -1;
                break;
            case "nounit":
                product.Unit = (FoodUnit)7;
                expected = NotImportableReason.NoUnit;
                break;
            case "cofid":
                product.Source = FoodSource.CoFID;
                expected = NotImportableReason.NotOpenFoodFacts;
                break;
            case "badbarcode":
                barcode = "not-a-barcode";
                expected = NotImportableReason.NoBarcode;
                break;
            case "shortbarcode":
                barcode = "123";
                expected = NotImportableReason.NoBarcode;
                break;
            case "badoptional":
                product.SugarsG = double.PositiveInfinity;
                break;
        }

        var result = await Import(barcode, product);

        Assert.Multiple(async () =>
        {
            Assert.That((result.Status, result.Reason, result.Food), Is.EqualTo((FoodImportStatus.NotImportable, expected, (Food?)null)));
            Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero);
            Assert.That(await connection.Table<Barcode>().CountAsync(), Is.Zero);
            Assert.That(await connection.Table<Serving>().CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task Import_ANotImportableProductForAStoredBarcode_ChangesNothing()
    {
        await Import(OriginalCode, await Original());
        var broken = await Original();
        broken.Name = "";

        var result = await Import(OriginalCode, broken);

        Assert.That(result.Status, Is.EqualTo(FoodImportStatus.NotImportable));
        Assert.That((await OnlyFood()).Name, Is.EqualTo("Coca-Cola Original"));
    }

    [Test]
    public async Task Import_WhenTheDatabaseFails_ReturnsFailedWithoutThrowing()
    {
        await connection.DropTableAsync<Barcode>();

        var result = await Import(OriginalCode, await Original());

        Assert.That((result.Status, result.Food), Is.EqualTo((FoodImportStatus.Failed, (Food?)null)));
        Assert.That(await connection.Table<Food>().CountAsync(), Is.Zero, "the failed transaction was rolled back");
    }

    [Test]
    public async Task Logging_EveryPath_CarriesIdsAndReasonsOnly_NeverABarcodeNameBrandOrErrorText()
    {
        var oneLitre = await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000);
        var incomplete = await Original();
        incomplete.Name = "";
        await Import(OriginalCode, await Original());
        await Import(OriginalCode, await Original());
        await Import(OneLitreCode, oneLitre);
        await Import(OreoCode, await Oreo());
        await Import(ZeroCode, incomplete);
        await connection.DropTableAsync<Barcode>();
        await Import(LightCode, await Light());

        var ids = LoggedEvents.Of(logger).Select(e => e.Id).ToList();
        Assert.That(ids, Is.EqualTo(new[]
        {
            CoreEvent.FoodImportCreated,
            CoreEvent.FoodImportKnown,
            CoreEvent.FoodImportAttached,
            CoreEvent.FoodImportCreated,
            CoreEvent.FoodImportNotImportable,
            CoreEvent.FoodImportFailed,
        }));
        Assert.That(ids.Select(i => (int)i), Is.All.InRange(1060, 1069));
        foreach (var record in logger.Collector.GetSnapshot())
        {
            var text = record.Message + " " + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
            Assert.That(record.Exception, Is.Null);
            foreach (var secret in new[] { OriginalCode, OneLitreCode, OreoCode, ZeroCode, LightCode, "Coca", "Cola", "Oreo", "SQLite", "no such table", ".json" })
            {
                Assert.That(text, Does.Not.Contain(secret).IgnoreCase);
            }
        }

        Assert.That(logger.Collector.GetSnapshot().Select(r => r.Message), Has.Some.EqualTo("Import skipped, reason NoName"));
    }

    [Test]
    public async Task DuplicateGroups_OverTheSavedProducts_AreNoneUnderTheStrictRule_AndOneOverTheResizedColas()
    {
        var saved = new[] { await Can(), await Original(), await Zero(), await Light(), await OriginalTaste(), await Oreo(),
            await Fixture("details-mars-5000159407236.json", "5000159407236"), await Fixture("details-weetabix-5010029000023.json", "5010029000023"), await Fixture("details-nutella-3017620422003.json", "3017620422003") };
        var constructed = new List<Food>
        {
            await Original(),
            await Resized(OneLitreCode, "Coca-Cola Original 1L", "1L", 1000),
            await Resized(TwoLitreCode, "Coca-Cola Original 2 litres", "2 litres", 2000),
        };

        Assert.Multiple(() =>
        {
            Assert.That(Groups(saved), Is.EqualTo(0), "no two saved responses are one product by the strict rule, though 'Coca Cola' and 'Coca-Cola Original' are one drink to a person");
            Assert.That(Groups(constructed), Is.EqualTo(1));
        });
    }

    private static int Groups(IReadOnlyList<Food> foods)
    {
        var parent = Enumerable.Range(0, foods.Count).ToArray();
        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
        for (var i = 0; i < foods.Count; i++)
        {
            for (var j = i + 1; j < foods.Count; j++)
            {
                if (ProductMatch.IsSameProduct(foods[i], foods[j]))
                {
                    parent[Find(j)] = Find(i);
                }
            }
        }

        return Enumerable.Range(0, foods.Count).GroupBy(Find).Count(g => g.Count() > 1);
    }

    [Test]
    public void TheServiceTakesAFoodAndABarcodeAndNothingAboutScanning()
    {
        var method = typeof(FoodImportService).GetMethod(nameof(FoodImportService.ImportAsync))!;

        Assert.Multiple(() =>
        {
            Assert.That(method.GetParameters().Select(p => (p.Name, p.ParameterType)), Is.EqualTo(new[] { ("barcode", typeof(string)), ("product", typeof(Food)), ("serving", typeof(ProductServing)) }));
            Assert.That(typeof(FoodImportService).Assembly.GetTypes().Where(t => t.Namespace == typeof(FoodImportService).Namespace && t.Name.StartsWith("FoodImport", StringComparison.Ordinal)).Select(t => t.Name), Has.None.Contain("Scan"));
        });
    }

    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
