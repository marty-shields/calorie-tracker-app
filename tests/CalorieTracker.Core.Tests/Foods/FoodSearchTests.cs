using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

public class FoodSearchTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeLogger<FoodSearch> logger = null!;
    private FoodSearch search = null!;
    private int nextId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        gate = new DatabaseGate();
        logger = new FakeLogger<FoodSearch>();
        search = new FoodSearch(new FoodStore(connection, gate, new FakeLogger<FoodStore>()), logger);
        nextId = 0;
    }

    [TearDown]
    public async Task TearDown()
    {
        await connection.CloseAsync();
        File.Delete(databasePath);
    }

    [Test]
    public async Task Search_WordOfTheName_FindsTheFood()
    {
        await Add("Boiled potatoes", "boiled potatoes");
        await Add("Oats", "oats");

        Assert.That(await Names("potato"), Is.EqualTo(new[] { "Boiled potatoes" }));
    }

    [Test]
    public async Task Search_SeveralWords_MatchInAnyOrder()
    {
        await Add("Boiled potatoes", "boiled potatoes");

        Assert.That(await Names("potato boiled"), Is.EqualTo(new[] { "Boiled potatoes" }));
        Assert.That(await Names("boiled potato"), Is.EqualTo(new[] { "Boiled potatoes" }));
    }

    [Test]
    public async Task Search_TermInTheMiddleOfAWord_DoesNotMatch()
    {
        await Add("Boiled potatoes", "boiled potatoes");

        Assert.That(await Names("tato"), Is.Empty);
    }

    [Test]
    public async Task Search_OneUnmatchedWord_ExcludesTheFood()
    {
        await Add("Boiled potatoes", "boiled potatoes");

        Assert.That(await Names("potato fried"), Is.Empty);
    }

    [Test]
    public async Task Search_AnyCase_Matches()
    {
        await Add("Boiled potatoes", "boiled potatoes");

        Assert.That(await Names("POTATO"), Has.Count.EqualTo(1));
        Assert.That(await Names("Boiled"), Has.Count.EqualTo(1));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(" ,- ")]
    [TestCase(null)]
    public async Task Search_BlankQuery_FindsNothingAndReadsNothing(string? query)
    {
        await Add("Boiled potatoes", "boiled potatoes");

        var result = await search.SearchAsync(query!);

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(result.Foods, Is.Empty);
        Assert.That(logger.Collector.Count, Is.Zero);
    }

    [Test]
    public async Task Search_DefaultLimit_Returns50Of60()
    {
        for (var i = 0; i < 60; i++)
        {
            await Add($"Potato {i:00}", $"potato {i:00}");
        }

        Assert.That(await Names("potato"), Has.Count.EqualTo(50));
    }

    [Test]
    public async Task Search_ExplicitLimit_IsHonoured()
    {
        for (var i = 0; i < 10; i++)
        {
            await Add($"Potato {i}", $"potato {i}");
        }

        Assert.That((await search.SearchAsync("potato", limit: 3)).Foods, Has.Count.EqualTo(3));
        Assert.That((await search.SearchAsync("potato", limit: 0)).Foods, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Search_NamesStartingWithTheQueryComeFirstThenOtherMatchesEachByName()
    {
        await Add("Potatoes, baked", "potatoes, baked");
        await Add("Sweet potato", "sweet potato");
        await Add("Potato salad", "potato salad");
        await Add("Potatoes, roast", "potatoes, roast");

        Assert.That(await Names("potato"), Is.EqualTo(new[] { "Potato salad", "Potatoes, baked", "Potatoes, roast", "Sweet potato" }));
    }

    [Test]
    public async Task Search_FirstTermAtTheStartOfTheName_RanksBeforeAnEarlierAlphabeticalMatch()
    {
        await Add("Apple potato", "apple potato");
        await Add("Potato", "potato");
        await Add("Baked potato", "baked potato");

        Assert.That(await Names("potato"), Is.EqualTo(new[] { "Potato", "Apple potato", "Baked potato" }));
    }

    [Test]
    public async Task Search_SeveralWords_NamesStartingWithTheWholeQueryComeFirst()
    {
        await Add("Sweet and sour potato", "sweet and sour potato");
        await Add("Potato, sweet, boiled", "potato, sweet, boiled");
        await Add("Sweet potato, baked", "sweet potato, baked");
        await Add("Sweet potato", "sweet potato");
        await Add("Sweet red potato", "sweet red potato");

        Assert.That(await Names("Sweet Potato"), Is.EqualTo(new[] { "Sweet potato", "Sweet potato, baked", "Potato, sweet, boiled", "Sweet and sour potato", "Sweet red potato" }));
        Assert.That(await Names("potato sweet"), Is.EqualTo(new[] { "Potato, sweet, boiled", "Sweet and sour potato", "Sweet potato", "Sweet potato, baked", "Sweet red potato" }));
    }

    [Test]
    public async Task Search_WholeQueryAcrossAComma_StillRanksFirst()
    {
        await Add("Baked sweet potato", "baked sweet potato");
        await Add("Sweet, potato mash", "sweet, potato mash");

        Assert.That(await Names("sweet potato"), Is.EqualTo(new[] { "Sweet, potato mash", "Baked sweet potato" }));
    }

    [Test]
    public async Task Search_WholeQueryAcrossALongSeparatorRun_StillRanksFirst()
    {
        await Add("Baked cola zero", "baked cola zero");
        await Add("Cola) / (zero", "cola) / (zero");

        Assert.That(await Names("cola zero"), Is.EqualTo(new[] { "Cola) / (zero", "Baked cola zero" }));
    }

    [Test]
    public async Task Search_WithAType_FindsOnlyFoodsOfThatType()
    {
        await Add("Potato", "potato", type: FoodType.Ingredient);
        await Add("Potato salad", "potato salad", type: FoodType.Dish);
        await Add("Potato crisps", "potato crisps", type: FoodType.Food);
        await Add("Potato vodka", "potato vodka", type: FoodType.Drink);

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("potato", FoodType.Ingredient), Is.EqualTo(new[] { "Potato" }));
            Assert.That(await Names("potato", FoodType.Food), Is.EqualTo(new[] { "Potato crisps" }));
            Assert.That(await Names("potato", FoodType.Dish), Is.EqualTo(new[] { "Potato salad" }));
            Assert.That(await Names("potato", FoodType.Drink), Is.EqualTo(new[] { "Potato vodka" }));
            Assert.That(await Names("potato"), Has.Count.EqualTo(4));
            Assert.That(await Names("salad", FoodType.Drink), Is.Empty);
        });
    }

    [Test]
    public async Task Search_FindsFoodsOfBothSources()
    {
        await Add("Oat bar", "oat bar", FoodSource.CoFID);
        await Add("Oat drink", "oat drink acme", FoodSource.OpenFoodFacts);

        Assert.That(await Names("oat"), Is.EqualTo(new[] { "Oat bar", "Oat drink" }));
        Assert.That(await Names("acme"), Is.EqualTo(new[] { "Oat drink" }));
    }

    [Test]
    public async Task Search_PunctuationInTheStoredText_SeparatesWords()
    {
        await Add("Barley, pearl, raw", "barley, pearl, raw");
        await Add("Beef (raw)", "beef (raw)");
        await Add("Pork/lamb", "pork/lamb");
        await Add("Semi-skimmed", "semi-skimmed");

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("pearl"), Is.EqualTo(new[] { "Barley, pearl, raw" }));
            Assert.That(await Names("raw"), Is.EqualTo(new[] { "Barley, pearl, raw", "Beef (raw)" }));
            Assert.That(await Names("(raw)"), Is.EqualTo(new[] { "Barley, pearl, raw", "Beef (raw)" }));
            Assert.That(await Names("lamb"), Is.EqualTo(new[] { "Pork/lamb" }));
            Assert.That(await Names("skimmed"), Is.EqualTo(new[] { "Semi-skimmed" }));
            Assert.That(await Names("semi-skimmed"), Is.EqualTo(new[] { "Semi-skimmed" }));
        });
    }

    [TestCase("%")]
    [TestCase("_")]
    [TestCase("pot_to")]
    [TestCase("p%")]
    [TestCase("\\")]
    public async Task Search_LikeWildcardsInTheQuery_AreLiteral(string query)
    {
        await Add("Potato", "potato");
        await Add("Fifty% juice", "fifty% juice");

        Assert.That(await Names(query), Is.Empty);
        Assert.That(await Names("fifty%"), Is.EqualTo(new[] { "Fifty% juice" }));
    }

    [Test]
    public async Task Search_QuoteInTheQuery_IsNotInterpretedAsSql()
    {
        await Add("Potato", "potato");

        var result = await search.SearchAsync("x' OR '1'='1");

        Assert.That(result.Foods, Is.Empty);
    }

    [Test]
    public async Task Search_WhileTheGateIsBusy_RunsAfterTheHolderFinishes()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () =>
        {
            await holding.Task;
            await connection.InsertAsync(NewFood("potato", "potato"));
        });

        var found = search.SearchAsync("potato");
        holding.SetResult();

        await busy;
        Assert.That((await found).Foods, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Search_StoreFails_ReturnsFailedAndLogsIt()
    {
        await connection.DropTableAsync<Food>();

        var result = await search.SearchAsync("potato");

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Failed));
        Assert.That(result.Foods, Is.Empty);
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.FoodSearchFailed }));
    }

    [Test]
    public async Task Search_CancelledBeforeTheStoreAnswers_Throws()
    {
        var holding = new TaskCompletionSource();
        var busy = gate.RunAsync(async () => await holding.Task);
        using var cts = new CancellationTokenSource();

        var found = search.SearchAsync("potato", ct: cts.Token);
        await cts.CancelAsync();

        Assert.That(async () => await found, Throws.InstanceOf<OperationCanceledException>());
        holding.SetResult();
        await busy;
        await gate.RunAsync(() => Task.FromResult(0));
    }

    [Test]
    public async Task Store_CancelledTokenAfterTheGateIsFree_DoesNotRunTheQuery()
    {
        await Add("Potato", "potato");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var store = new FoodStore(connection, gate, new FakeLogger<FoodStore>());

        Assert.That(async () => await store.SearchAsync(["potato"], "potato", 10, ct: cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Search_TabAndNewlineInTheStoredText_SeparateWords()
    {
        await Add("Tabbed", "alpha\tbeta\ngamma\rdelta");

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("beta"), Is.EqualTo(new[] { "Tabbed" }));
            Assert.That(await Names("gamma"), Is.EqualTo(new[] { "Tabbed" }));
            Assert.That(await Names("delta"), Is.EqualTo(new[] { "Tabbed" }));
        });
    }

    [Test]
    public async Task Search_ManyWords_UsesTheFirstTenOnly()
    {
        await Add("Words", "w0 w1 w2 w3 w4 w5 w6 w7 w8 w9");
        var query = string.Join(' ', Enumerable.Range(0, 40).Select(i => $"w{i}"));

        var result = await search.SearchAsync(query);

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(result.Foods.Select(f => f.Name), Is.EqualTo(new[] { "Words" }));
        Assert.That(logger.Collector.GetSnapshot()[0].Message, Is.EqualTo("Food search with 10 terms and type (null) found 1 foods"));
    }

    [Test]
    public async Task Search_RepeatedWord_CountsOneTerm()
    {
        await Add("Potato", "potato");

        await search.SearchAsync("potato potato");

        Assert.That(logger.Collector.GetSnapshot()[0].Message, Is.EqualTo("Food search with 1 terms and type (null) found 1 foods"));
    }

    [Test]
    public async Task Search_NamesOrderIgnoringCase()
    {
        await Add("banana", "fruit banana");
        await Add("Apple", "fruit apple");
        await Add("cherry", "fruit cherry");

        Assert.That(await Names("fruit"), Is.EqualTo(new[] { "Apple", "banana", "cherry" }));
    }

    [Test]
    public async Task Search_NoSpaceAfterPunctuation_StillSeparatesWords()
    {
        await Add("Milk", "milk,whole");
        await Add("Beef", "beef (raw)");
        await Add("Rawhide", "rawhide");
        await Add("Pork", "pork/lamb");
        await Add("Semi", "semi-skimmed");
        await Add("Paren", "(open)close");

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("whole"), Is.EqualTo(new[] { "Milk" }));
            Assert.That(await Names("raw"), Is.EqualTo(new[] { "Rawhide", "Beef" }));
            Assert.That(await Names("raw)"), Is.EqualTo(new[] { "Rawhide", "Beef" }));
            Assert.That(await Names("close"), Is.EqualTo(new[] { "Paren" }));
            Assert.That(await Names("open"), Is.EqualTo(new[] { "Paren" }));
            Assert.That(await Names("lamb"), Is.EqualTo(new[] { "Pork" }));
            Assert.That(await Names("skimmed"), Is.EqualTo(new[] { "Semi" }));
        });
    }

    [Test]
    public async Task Search_BackslashInTheStoredText_IsLiteral()
    {
        await Add("Backslash", "a\\b");
        await Add("Other", "abc");

        Assert.That(await Names("a\\b"), Is.EqualTo(new[] { "Backslash" }));
        Assert.That(await Names("\\"), Is.Empty);
    }

    [Test]
    public async Task Search_LimitAboveTheMaximum_ReturnsTheMaximum()
    {
        for (var i = 0; i < 250; i++)
        {
            await Add($"Potato {i:000}", $"potato {i:000}");
        }

        Assert.That((await search.SearchAsync("potato", limit: 1000)).Foods, Has.Count.EqualTo(IFoodSearch.MaxLimit));
    }

    [Test]
    public async Task Logging_SearchWithAType_LogsTheTypeAndCounts()
    {
        await Add("Distinctive Name", "distinctive name", type: FoodType.Dish);

        await search.SearchAsync("Distinctive", FoodType.Dish);

        var message = logger.Collector.GetSnapshot()[0].Message;
        Assert.That(message, Is.EqualTo("Food search with 1 terms and type Dish found 1 foods"));
    }

    [Test]
    public async Task Logging_Search_LogsCountsOnlyNeverTheQueryOrFoodText()
    {
        await Add("Distinctive Name", "distinctive name secretbrand");

        await search.SearchAsync("Distinctive secretbrand");
        await connection.DropTableAsync<Food>();
        await search.SearchAsync("Distinctive secretbrand");

        var records = logger.Collector.GetSnapshot();
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.FoodSearched, CoreEvent.FoodSearchFailed }));
        foreach (var record in records)
        {
            var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
            Assert.That(text, Does.Not.Contain("istinctive").And.Not.Contain("secretbrand"));
        }

        Assert.That(records[0].Message, Is.EqualTo("Food search with 2 terms and type (null) found 1 foods"));
    }

    [TestCase("creme")]
    [TestCase("crème")]
    [TestCase("CRÈME")]
    public async Task Search_AccentedFood_IsFoundWithOrWithoutTheAccent(string query)
    {
        await Add("Crème fraîche", SearchFolding.Fold("Crème fraîche"));

        Assert.That(await Names(query), Is.EqualTo(new[] { "Crème fraîche" }));
    }

    [Test]
    public async Task Search_PureeWithoutAccent_FindsPuree()
    {
        await Add("Purée", SearchFolding.Fold("Purée"));

        Assert.That(await Names("puree"), Is.EqualTo(new[] { "Purée" }));
    }

    private async Task<List<string>> Names(string query, FoodType? type = null) =>
        (await search.SearchAsync(query, type)).Foods.Select(f => f.Name).ToList();

    private Task Add(string name, string searchText, FoodSource source = FoodSource.CoFID, FoodType type = FoodType.Ingredient) =>
        connection.InsertAsync(NewFood(name, searchText, source, type));

    private Food NewFood(string name, string searchText, FoodSource source = FoodSource.CoFID, FoodType type = FoodType.Ingredient) => new()
    {
        Type = type,
        Source = source,
        SourceId = (++nextId).ToString(),
        Name = name,
        Unit = FoodUnit.Gram,
        SearchText = searchText,
    };
}
