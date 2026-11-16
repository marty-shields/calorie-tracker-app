using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Seed;
using CalorieTracker.Core.Summary;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Seed;

/// <summary>The units of the bundled CoFID seed (liquids in millilitres, powders in grams) and how an installed database gets them.</summary>
public class SeedUnitsTests
{
    private static readonly DateOnly Day = new(2031, 7, 19);

    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FoodStore store = null!;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<FoodBarcode>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<SeedVersion>();
        store = new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public void Seed_MilkJuiceSoftDrinkSquashTeaAndCoffee_AreInMillilitres()
    {
        var foods = BundledSeed();

        Assert.Multiple(() =>
        {
            Assert.That(foods.Where(f => f.FoodGroup is "BA" or "BAB" or "BAE" or "BAH" or "BAK" or "BC").Select(f => f.Unit), Is.All.EqualTo(FoodUnit.Millilitre));
            Assert.That(foods.Where(f => f.FoodGroup is "FC" && !f.OriginalName!.Contains("weighed as whole fruit")).Select(f => f.Unit), Is.All.EqualTo(FoodUnit.Millilitre));
            Assert.That(foods.Where(f => f.FoodGroup is "PCA" or "PAC").Select(f => f.Unit), Is.All.EqualTo(FoodUnit.Millilitre));
            Assert.That(foods.Where(f => f.FoodGroup is "PCC" && f.OriginalName!.Contains("diluted") && !f.OriginalName.Contains("undiluted")).Select(f => f.Unit), Is.All.EqualTo(FoodUnit.Millilitre));
            foreach (var name in new[] { "Milk, semi-skimmed, UHT", "Milk, soya, non-dairy alternative to milk, unsweetened, fortified", "Orange juice, chilled", "Cola", "Tea, green, infusion", "Coffee, infusion, average", "Smoothies", "Carrot juice", "Water, distilled" })
            {
                Assert.That(foods.Single(f => f.OriginalName == name).Unit, Is.EqualTo(FoodUnit.Millilitre), name);
            }

            // The step's frozen list is the exact, sorted set of millilitre foods, so a seed change that moves a food shows here.
            Assert.That(foods.Where(f => f.Unit == FoodUnit.Millilitre).Select(f => f.SourceId).Order(StringComparer.Ordinal), Is.EqualTo(FoodUnitUpgrade.MillilitreSourceIds));
        });
    }

    [Test]
    public void Seed_PowdersAndDryMixes_StayInGrams()
    {
        var foods = BundledSeed();

        Assert.Multiple(() =>
        {
            foreach (var name in new[] { "Cocoa powder", "Complan powder, original and sweet", "Complan powder, savoury", "Milk shake, powder", "Horlicks, powder", "Drinking chocolate, powder", "Coffee, powder, instant", "Ovaltine, powder", "Milk, skimmed, dried, fortified", "Milk, evaporated, whole" })
            {
                Assert.That(foods.Single(f => f.OriginalName == name).Unit, Is.EqualTo(FoodUnit.Gram), name);
            }

            Assert.That(foods.Where(f => f.OriginalName!.Contains("powder", StringComparison.OrdinalIgnoreCase) && !f.OriginalName.Contains("made up")).Select(f => f.Unit), Is.All.EqualTo(FoodUnit.Gram));
        });
    }

    [Test]
    public async Task NewInstall_AndUpgradedInstall_EndWithTheSameUnits()
    {
        // The upgraded database holds the old seed: every non-alcohol CoFID food in grams.
        var fresh = await UnitsAfterLoadingTheSeed();
        var upgradedPath = TempDatabase.NewPath();
        var upgradedConnection = new SQLiteAsyncConnection(upgradedPath);
        try
        {
            await upgradedConnection.CreateTableAsync<Food>();
            foreach (var food in BundledSeed())
            {
                food.Unit = food.FoodGroup!.StartsWith('Q') ? FoodUnit.Millilitre : FoodUnit.Gram;
                await upgradedConnection.InsertAsync(food);
            }

            await upgradedConnection.CloseAsync();
            var db = new SQLiteConnection(upgradedPath);
            try
            {
                new FoodUnitUpgrade().Apply(db);
            }
            finally
            {
                db.Close();
            }

            upgradedConnection = new SQLiteAsyncConnection(upgradedPath);
            var upgraded = (await upgradedConnection.Table<Food>().ToListAsync()).ToDictionary(f => f.SourceId, f => f.Unit);

            Assert.That(upgraded, Is.EquivalentTo(fresh));
        }
        finally
        {
            await upgradedConnection.CloseAsync();
            File.Delete(upgradedPath);
        }
    }

    [Test]
    public async Task UnitStep_OnAnUpgradedDatabase_ChangesTheUnitsAndKeepsEveryDiaryEntryAndItsCalories()
    {
        var milk = BundledSeed().Single(f => f.OriginalName == "Milk, semi-skimmed, UHT");
        var cocoa = BundledSeed().Single(f => f.OriginalName == "Cocoa powder");
        milk.Unit = FoodUnit.Gram;
        cocoa.Unit = FoodUnit.Millilitre;
        var milkId = (await store.UpsertAsync(milk)).Id;
        var cocoaId = (await store.UpsertAsync(cocoa)).Id;
        await connection.CreateTableAsync<AppSettings>();
        var diary = new DiaryService(new DiaryStore(connection, new DatabaseGate()), NullLogger<DiaryService>.Instance);
        await diary.AddAsync(Day, Meal.Breakfast, milkId, 250);
        await diary.AddAsync(Day, Meal.Breakfast, cocoaId, 10, 2);
        var before = await Summary();
        var entriesBefore = await connection.Table<DiaryEntry>().ToListAsync();
        await connection.CloseAsync();

        var db = new SQLiteConnection(databasePath);
        try
        {
            new FoodUnitUpgrade().Apply(db);
            new FoodUnitUpgrade().Apply(db);
        }
        finally
        {
            db.Close();
        }

        connection = new SQLiteAsyncConnection(databasePath);
        store = new FoodStore(connection, new DatabaseGate(), new FakeLogger<FoodStore>());
        var after = await Summary();
        var entriesAfter = await connection.Table<DiaryEntry>().ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(before.Meals[0].Entries.Select(e => e.Unit), Is.EqualTo(new[] { FoodUnit.Gram, FoodUnit.Millilitre }));
            // The step only sets millilitres for its frozen ids; cocoa is not one, so it keeps whatever it had.
            Assert.That(after.Meals[0].Entries.Select(e => e.Unit), Is.EqualTo(new[] { FoodUnit.Millilitre, FoodUnit.Millilitre }));
            Assert.That(after.Meals[0].Entries.Select(e => e.Nutrition), Is.EqualTo(before.Meals[0].Entries.Select(e => e.Nutrition)));
            Assert.That(after.Meals[0].Entries.Select(e => (e.Amount, e.Servings)), Is.EqualTo(before.Meals[0].Entries.Select(e => (e.Amount, e.Servings))));
            Assert.That(after.Meals[0].Totals, Is.EqualTo(before.Meals[0].Totals));
            Assert.That(entriesAfter.Select(e => (e.Id, e.FoodId, e.Amount, e.Servings, e.IsRemoved)), Is.EqualTo(entriesBefore.Select(e => (e.Id, e.FoodId, e.Amount, e.Servings, e.IsRemoved))));
        });
    }

    [Test]
    public async Task UnitStep_LeavesOpenFoodFactsFoodsAndUnknownCoFidFoodsAlone()
    {
        await connection.InsertAsync(new Food { Source = FoodSource.OpenFoodFacts, SourceId = "12-314", Name = "Same id", Unit = FoodUnit.Gram, SearchText = "same id" });
        await connection.InsertAsync(new Food { Source = FoodSource.CoFID, SourceId = "99-999", Name = "Removed", Unit = FoodUnit.Gram, SearchText = "removed" });
        await connection.CloseAsync();

        var db = new SQLiteConnection(databasePath);
        try
        {
            new FoodUnitUpgrade().Apply(db);
            Assert.That(db.QueryScalars<int>("SELECT Unit FROM Food ORDER BY Id"), Is.EqualTo(new[] { 0, 0 }));
        }
        finally
        {
            db.Close();
            connection = new SQLiteAsyncConnection(databasePath);
        }
    }

    [Test]
    public void UnitStep_OnADatabaseWithNoFoodTable_DoesNothing()
    {
        var path = TempDatabase.NewPath();
        var db = new SQLiteConnection(path);
        try
        {
            new FoodUnitUpgrade().Apply(db);

            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'Food'"), Is.Zero);
        }
        finally
        {
            db.Close();
            File.Delete(path);
        }
    }

    private static List<Food> BundledSeed()
    {
        using var stream = SeedLoader.OpenBundledSeed();
        return SeedFile.Parse(stream).Foods.ToList();
    }

    private async Task<DaySummary> Summary()
    {
        var result = await new DaySummaryService(new DaySummaryStore(connection, new DatabaseGate()), NullLogger<DaySummaryService>.Instance).GetAsync(Day);
        return result.Summary!;
    }

    private async Task<Dictionary<string, FoodUnit>> UnitsAfterLoadingTheSeed()
    {
        var loaded = await new SeedLoader(store, new FakeLogger<SeedLoader>(), SeedLoader.OpenBundledSeed).LoadAsync();
        Assert.That(loaded.Status, Is.EqualTo(SeedLoadStatus.Loaded));
        var units = (await connection.Table<Food>().ToListAsync()).ToDictionary(f => f.SourceId, f => f.Unit);
        await connection.ExecuteAsync("DELETE FROM Food");
        return units;
    }
}
