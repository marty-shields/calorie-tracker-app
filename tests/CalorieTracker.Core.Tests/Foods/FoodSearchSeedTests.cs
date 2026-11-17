using System.Diagnostics;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Seed;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

/// <summary>The acceptance criteria of the ranked search, run against the embedded CoFID seed in a real store.</summary>
public class FoodSearchSeedTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private FakeLogger<FoodSearch> logger = null!;
    private FoodSearch search = null!;

    [OneTimeSetUp]
    public async Task LoadSeed()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<Barcode>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<SeedVersion>();
        var gate = new DatabaseGate();
        var store = new FoodStore(connection, gate, new FakeLogger<FoodStore>());
        var loaded = await new SeedLoader(store, new FakeLogger<SeedLoader>(), SeedLoader.OpenBundledSeed).LoadAsync();
        Assert.That(loaded.Status, Is.EqualTo(SeedLoadStatus.Loaded));
        await store.UpsertAsync(new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "5010029000024",
            Name = "Original",
            Brand = "Weetabix",
            Unit = FoodUnit.Gram,
            Type = FoodType.Food,
            SearchText = SearchFolding.Fold("Original Weetabix"),
        });
        await store.UpsertAsync(new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "3000000000001",
            Name = "Crème caramel",
            Unit = FoodUnit.Gram,
            Type = FoodType.Food,
            SearchText = SearchFolding.Fold("Crème caramel"),
        });
        logger = new FakeLogger<FoodSearch>();
        search = new FoodSearch(store, new FoodHistoryService(new FoodHistoryStore(connection, gate), TimeProvider.System, new FakeLogger<FoodHistoryService>()), logger);
    }

    [OneTimeTearDown]
    public async Task DeleteSeed()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task Milk_FirstThreeAreTheCommonMilks()
    {
        Assert.That((await Names("milk")).Take(3), Is.EquivalentTo(new[] { "Pasteurised skimmed milk", "Pasteurised whole milk", "Semi-skimmed milk (pasteurised)" }));
    }

    [Test]
    public async Task Bread_FirstThreeAreWhiteBrownAndWholemeal()
    {
        Assert.That((await Names("bread")).Take(3), Is.EquivalentTo(new[] { "White bread", "Brown bread", "Wholemeal bread" }));
    }

    [Test]
    public async Task Butter_SaltedAndUnsaltedComeFirst()
    {
        Assert.That((await Names("butter")).Take(2), Is.EquivalentTo(new[] { "Salted butter", "Unsalted butter" }));
    }

    [Test]
    public async Task Apple_EatingAndCookingApplesAreInTheFirstThree()
    {
        Assert.That((await Names("apple")).Take(3), Is.SupersetOf(new[] { "Eating apples (with skin)", "Cooking apples (peeled)" }));
    }

    [Test]
    public async Task Egg_ChickenEggsInTheFirstFiveAndEggNogIsNot()
    {
        var names = (await Names("egg")).Take(5).ToList();

        Assert.That(names, Does.Contain("Chicken eggs"));
        Assert.That(names, Does.Not.Contain("Egg nog"));
    }

    [Test]
    public async Task SkimmedMilk_PasteurisedSkimmedMilkIsFirst_AndBracketOnlyMatchesComeAfterBothInTheMainName()
    {
        var foods = (await search.SearchAsync("skimmed milk")).Foods;

        Assert.That(foods[0].Name, Is.EqualTo("Pasteurised skimmed milk"));
        var lastBothInMain = foods.ToList().FindLastIndex(f => MainWords(f.Name).Contains("skimmed") && MainWords(f.Name).Contains("milk"));
        var firstBracketOnly = foods.ToList().FindIndex(f => !(MainWords(f.Name).Contains("skimmed") && MainWords(f.Name).Contains("milk")));
        Assert.That(firstBracketOnly, Is.GreaterThan(lastBothInMain));
    }

    [Test]
    public async Task Milk_ListsDrinksIngredientsAndFoodsTogether()
    {
        var types = (await search.SearchAsync("milk")).Foods.Select(f => f.Type).Distinct().ToList();

        Assert.That(types, Is.SupersetOf(new[] { FoodType.Drink, FoodType.Ingredient, FoodType.Food }));
    }

    [Test]
    public async Task Typos_FindTheIntendedFood()
    {
        Assert.That((await Names("chedar"))[0], Is.EqualTo("English Cheddar cheese"));
        Assert.That((await Names("potatos"))[0], Is.EqualTo("Potatoes"));
        var yoghurts = (await Names("yogurt")).Take(10).ToList();
        Assert.That(yoghurts, Is.Not.Empty.And.All.Contain("yoghurt"));
    }

    [TestCase("pot")]
    [TestCase("tom")]
    [TestCase("ric")]
    [TestCase("mil")]
    public async Task ShortTypedWord_NeverMatchesByTypo(string typed)
    {
        // Every hit must have a word that the typed word starts, or an exact word; never one letter off.
        foreach (var name in await Names(typed))
        {
            var words = FoodRanker.Words(SearchFolding.Fold(name)).Select(FoodRanker.Normalise);
            Assert.That(words.Any(w => w.StartsWith(FoodRanker.Normalise(typed), StringComparison.Ordinal)), Is.True, name);
        }
    }

    [Test]
    public async Task ShortTypedWordOneLetterOffAnEgg_FindsNothing()
    {
        // "egs" is one edit from "egg" but has only three letters, so it must not match by typo.
        Assert.That(await Names("egs"), Is.Empty);
    }

    [Test]
    public async Task Bread_ListsNoTypoMatchesSuchAsBroadBeans()
    {
        var names = await Names("bread");

        Assert.That(names, Is.Not.Empty);
        Assert.That(names, Has.None.Contains("Broad beans"));
        Assert.That(names, Has.All.Matches<string>(n => n!.Contains("bread", StringComparison.OrdinalIgnoreCase)), "every hit has bread in its name");
    }

    [Test]
    public async Task ChickenBreast_ReturnsOnlyFoodsWithBothWords()
    {
        var names = await Names("chicken breast");

        Assert.That(names, Has.Count.EqualTo(8));
        Assert.That(names.Select(n => SearchFolding.Fold(n)), Is.All.Contain("chicken").And.All.Contain("breast"));
    }

    [TestCase("weetabix")]
    [TestCase("weetabix original")]
    [TestCase("original weetabix")]
    public async Task StoredOpenFoodFactsFood_IsFoundByBrandAndName(string query)
    {
        Assert.That(await Names(query), Does.Contain("Original"));
    }

    [TestCase("creme")]
    [TestCase("CRÈME")]
    public async Task Accent_AndCase_AreIgnored(string query)
    {
        Assert.That(await Names(query), Does.Contain("Crème caramel").And.Contain("Creme egg"));
    }

    [Test]
    public async Task BlankAndUnmatchedQueries_FindNothing()
    {
        Assert.That((await search.SearchAsync("  ")).Foods, Is.Empty);
        var none = await search.SearchAsync("zzzzqqqq");
        Assert.That(none.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(none.Foods, Is.Empty);
    }

    [Test]
    public async Task Search_LogsTermAndResultCountOnly()
    {
        logger.Collector.Clear();

        await search.SearchAsync("chedar potatos");

        var record = logger.Collector.GetSnapshot().Single();
        Assert.That(record.Message, Does.StartWith("Food search with 2 terms found "));
        var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
        Assert.That(text, Does.Not.Contain("chedar").And.Not.Contain("potato").And.Not.Contain("Cheddar"));
        Assert.That(record.StructuredState!.Select(p => p.Key), Is.EquivalentTo(new[] { "TermCount", "ResultCount", "{OriginalFormat}" }));
    }

    [Test]
    public async Task Milk_SearchTimeOverTheFullSeed_IsReported()
    {
        await search.SearchAsync("milk");
        var times = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew();
            await search.SearchAsync("milk");
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        TestContext.Out.WriteLine($"milk over {await connection.Table<Food>().CountAsync()} foods: median {times[times.Count / 2]:F1} ms, max {times[^1]:F1} ms");
    }

    [TestCase("a", "a", false)]
    [TestCase("cheddar", "chedar", true)]
    [TestCase("chedar", "cheddar", true)]
    [TestCase("cheese", "chesee", true)]
    [TestCase("cheese", "chaese", true)]
    [TestCase("cheese", "chaise", false)]
    [TestCase("milk", "milkshake", false)]
    public void IsOneEditAway_CoversInsertDeleteSubstituteAndSwap(string a, string b, bool expected)
    {
        Assert.That(FoodRanker.IsOneEditAway(a, b), Is.EqualTo(expected));
    }

    [TestCase("potatoes", "potato")]
    [TestCase("potatos", "potato")]
    [TestCase("eggs", "egg")]
    [TestCase("berries", "berry")]
    [TestCase("cookies", "cooky")]
    [TestCase("cookie", "cooky")]
    [TestCase("cheese", "cheese")]
    [TestCase("cheeses", "cheese")]
    [TestCase("hummus", "hummus")]
    [TestCase("glass", "glass")]
    [TestCase("yogurt", "yoghurt")]
    [TestCase("yoghurts", "yoghurt")]
    public void Normalise_FoldsPluralsAndYogurt(string word, string expected)
    {
        Assert.That(FoodRanker.Normalise(word), Is.EqualTo(expected));
    }

    private async Task<List<string>> Names(string query) =>
        (await search.SearchAsync(query)).Foods.Select(f => f.Name).ToList();

    private static HashSet<string> MainWords(string name) =>
        FoodRanker.Words(SearchFolding.Fold(name.Contains('(') ? name[..name.IndexOf('(')] : name)).ToHashSet();
}
