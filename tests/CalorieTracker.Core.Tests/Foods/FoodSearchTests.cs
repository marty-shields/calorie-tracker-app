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
    public async Task Search_WithAType_FindsOnlyFoodsOfThatType()
    {
        await Add("Potato", "potato", type: FoodType.Ingredient);
        await Add("Potato crisps", "potato crisps", type: FoodType.Food);
        await Add("Potato vodka", "potato vodka", type: FoodType.Drink);

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("potato", FoodType.Ingredient), Is.EqualTo(new[] { "Potato" }));
            Assert.That(await Names("potato", FoodType.Food), Is.EqualTo(new[] { "Potato crisps" }));
            Assert.That(await Names("potato", FoodType.Drink), Is.EqualTo(new[] { "Potato vodka" }));
            Assert.That(await Names("potato"), Has.Count.EqualTo(3));
            Assert.That(await Names("crisps", FoodType.Drink), Is.Empty);
        });
    }

    [Test]
    public async Task Search_FindsFoodsOfBothSources()
    {
        await Add("Oat bar", "oat bar", FoodSource.CoFID);
        await Add("Oat drink", "oat drink acme", FoodSource.OpenFoodFacts, brand: "Acme");

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
    [TestCase("p%")]
    [TestCase("\\")]
    public async Task Search_PunctuationOnlyOrPartlyPunctuatedQuery_MatchesOnlyWhatItSpells(string query)
    {
        await Add("Potato", "potato");
        await Add("Fifty% juice", "fifty% juice");

        Assert.That(await Names(query), Is.Empty);
        Assert.That(await Names("fifty%"), Is.EqualTo(new[] { "Fifty% juice" }));
    }

    [Test]
    public async Task Search_PartlyTypedWordOfAPlural_StillPrefixMatches()
    {
        await Add("Cookies", "cookies");

        Assert.That(await Names("cooki"), Is.EqualTo(new[] { "Cookies" }));
    }

    [Test]
    public async Task Search_SingularOrPluralOfAnOesWord_MeetsTheOtherForm()
    {
        await Add("Sloes", "sloes");
        await Add("Potatoe", "potatoe");

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("sloe"), Is.EqualTo(new[] { "Sloes" }));
            Assert.That(await Names("sloes"), Is.EqualTo(new[] { "Sloes" }));
            Assert.That(await Names("potatoes"), Is.EqualTo(new[] { "Potatoe" }));
        });
    }

    [Test]
    public async Task Search_MatchInTheHeadWord_RanksAboveTheSameMatchElsewhere()
    {
        await Add("Zzz egg", "zzz egg");
        await Add("Egg aaa", "egg aaa");

        Assert.That(await Names("egg"), Is.EqualTo(new[] { "Zzz egg", "Egg aaa" }));
    }

    [Test]
    public async Task Search_BrandWord_IsAMainWordButNotTheHead()
    {
        await Add("Zzz bar", "zzz bar", brand: "Egg");
        await Add("Egg aaa", "egg aaa");
        await Add("Zzz egg", "zzz egg");

        Assert.That(await Names("egg"), Is.EqualTo(new[] { "Zzz egg", "Egg aaa", "Zzz bar" }));
    }

    [Test]
    public async Task Search_ExactMainWord_RanksAbovePrefixMatch()
    {
        await Add("Aaa pearl", "aaa pearl");
        await Add("Pear", "pear");

        Assert.That(await Names("pear"), Is.EqualTo(new[] { "Pear", "Aaa pearl" }));
    }

    [Test]
    public async Task Search_TypoOnAMainWord_RanksAboveMatchOnlyInBrackets()
    {
        await Add("Aaa (milk)", "aaa (milk)");
        await Add("Zzz mlik", "zzz mlik");

        Assert.That(await Names("milk"), Is.EqualTo(new[] { "Zzz mlik", "Aaa (milk)" }));
    }

    [Test]
    public async Task Search_CommonFoodBoost_NotAppliedWhenTheWordMatchedByTypo()
    {
        await Add("Zzz mlik", "zzz mlik", originalName: "Milk, whole, average");
        await Add("Aaa milky", "aaa milky");

        Assert.That(await Names("milk"), Is.EqualTo(new[] { "Aaa milky", "Zzz mlik" }));
    }

    [Test]
    public async Task Search_CommonFoodBoost_AppliesToOneWordQueriesOfCoFidFoodsOnly()
    {
        await Add("Aaa milk", "aaa milk");
        await Add("Milk plain", "milk plain", originalName: "Milk, whole, average");
        await Add("Milk off", "milk off", FoodSource.OpenFoodFacts, originalName: "Milk, whole, average");

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("milk"), Is.EqualTo(new[] { "Milk plain", "Aaa milk", "Milk off" }));
            Assert.That(await Names("milk plain"), Is.EqualTo(new[] { "Milk plain" }));
        });
    }

    [Test]
    public async Task Search_CommonFoodBoost_NotAppliedToTwoWordQueries()
    {
        await Add("Milk whole extra", "milk whole extra", originalName: "Milk, whole, average");
        await Add("Aaa milk whole", "aaa milk whole");

        Assert.That(await Names("milk whole"), Is.EqualTo(new[] { "Aaa milk whole", "Milk whole extra" }));
    }

    [Test]
    public async Task Search_RawVersionOfACommonFood_BeatsAnAlphabeticallyEarlierTie()
    {
        await Add("Aaa potato", "aaa potato", originalName: "Potato, boiled");
        await Add("Zzz potato", "zzz potato", originalName: "Potato, raw");

        Assert.That(await Names("potato"), Is.EqualTo(new[] { "Zzz potato", "Aaa potato" }));
    }

    [Test]
    public async Task Search_FoodsWithTheSameName_OrderById()
    {
        await Add("Twin", "twin");
        await Add("TWIN", "twin");
        await Add("twin", "twin");

        var ids = (await search.SearchAsync("twin")).Foods.Select(f => f.Id).ToList();
        Assert.That(ids, Is.Ordered);
        Assert.That(ids, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Search_FourLetterTypedWord_MatchesByTypo()
    {
        await Add("Lamb", "lamb");

        Assert.That(await Names("lmab"), Is.EqualTo(new[] { "Lamb" }));
        Assert.That(await Names("lmb"), Is.Empty);
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

        Assert.That(async () => await store.ListAsync(ct: cts.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task Search_TabAndNewlineInTheStoredText_SeparateWords()
    {
        await Add("alpha\tbeta\ngamma\rdelta", "alpha\tbeta\ngamma\rdelta");

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("beta"), Is.EqualTo(new[] { "alpha\tbeta\ngamma\rdelta" }));
            Assert.That(await Names("gamma"), Is.EqualTo(new[] { "alpha\tbeta\ngamma\rdelta" }));
            Assert.That(await Names("delta"), Is.EqualTo(new[] { "alpha\tbeta\ngamma\rdelta" }));
        });
    }

    [Test]
    public async Task Search_ManyWords_UsesTheFirstTenOnly()
    {
        await Add("w0 w1 w2 w3 w4 w5 w6 w7 w8 w9", "w0 w1 w2 w3 w4 w5 w6 w7 w8 w9");
        var query = string.Join(' ', Enumerable.Range(0, 40).Select(i => $"w{i}"));

        var result = await search.SearchAsync(query);

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(result.Foods.Select(f => f.Name), Is.EqualTo(new[] { "w0 w1 w2 w3 w4 w5 w6 w7 w8 w9" }));
        Assert.That(logger.Collector.GetSnapshot()[0].Message, Is.EqualTo("Food search with 10 terms found 1 foods"));
    }

    [Test]
    public async Task Search_RepeatedWord_CountsOneTerm()
    {
        await Add("Potato", "potato");

        await search.SearchAsync("potato potato");

        Assert.That(logger.Collector.GetSnapshot()[0].Message, Is.EqualTo("Food search with 1 terms found 1 foods"));
    }

    [Test]
    public async Task Search_NamesOrderIgnoringCase()
    {
        await Add("fruit banana", "fruit banana");
        await Add("Fruit apple", "fruit apple");
        await Add("fruit cherry", "fruit cherry");

        Assert.That(await Names("fruit"), Is.EqualTo(new[] { "Fruit apple", "fruit banana", "fruit cherry" }));
    }

    [Test]
    public async Task Search_NoSpaceAfterPunctuation_StillSeparatesWords()
    {
        await Add("Milk,whole", "milk,whole");
        await Add("Beef (raw)", "beef (raw)");
        await Add("Rawhide", "rawhide");
        await Add("Pork/lamb", "pork/lamb");
        await Add("Semi-skimmed", "semi-skimmed");
        await Add("(open)close", "(open)close");

        Assert.Multiple(async () =>
        {
            Assert.That(await Names("whole"), Is.EqualTo(new[] { "Milk,whole" }));
            Assert.That(await Names("raw"), Is.EqualTo(new[] { "Rawhide", "Beef (raw)" }));
            Assert.That(await Names("raw)"), Is.EqualTo(new[] { "Rawhide", "Beef (raw)" }));
            Assert.That(await Names("close"), Is.EqualTo(new[] { "(open)close" }));
            Assert.That(await Names("open"), Is.EqualTo(new[] { "(open)close" }));
            Assert.That(await Names("lamb"), Is.EqualTo(new[] { "Pork/lamb" }));
            Assert.That(await Names("skimmed"), Is.EqualTo(new[] { "Semi-skimmed" }));
        });
    }

    [Test]
    public async Task Search_BackslashInTheStoredText_IsLiteral()
    {
        await Add("a\\b", "a\\b");
        await Add("Other", "abc");

        Assert.That(await Names("a\\b"), Is.EqualTo(new[] { "a\\b" }));
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
    public async Task Logging_Search_LogsCountsOnlyNeverTheQueryOrFoodText()
    {
        await Add("Distinctive Name", "distinctive name secretbrand", brand: "Secretbrand");

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

        Assert.That(records[0].Message, Is.EqualTo("Food search with 2 terms found 1 foods"));
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

    [Test]
    public async Task Search_BrandsGenericNameAndCategory_FindTheStoredProduct()
    {
        await connection.InsertAsync(new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "3017620422003",
            Name = "Nutella",
            Brand = "Ferrero",
            Brands = "Ferrero, Nutella",
            GenericName = "Hazelnut And Cocoa Spread",
            Category = "Sweets",
            Unit = FoodUnit.Gram,
            Type = FoodType.Food,
            SearchText = "nutella ferrero nutella hazelnut and cocoa spread sweets",
        });
        await connection.InsertAsync(new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "5010029000023",
            Name = "Original",
            Brand = "Weetabix",
            Brands = "Weetabix",
            Category = "Breakfast cereals",
            Unit = FoodUnit.Gram,
            Type = FoodType.Food,
            SearchText = "original weetabix breakfast cereals",
        });

        Assert.That(await Names("ferrero"), Is.EqualTo(new[] { "Nutella" }));
        Assert.That(await Names("nutella"), Is.EqualTo(new[] { "Nutella" }));
        Assert.That(await Names("hazelnut"), Is.EqualTo(new[] { "Nutella" }));
        Assert.That(await Names("hazelnut spread"), Is.EqualTo(new[] { "Nutella" }));
        Assert.That(await Names("cereal"), Is.EqualTo(new[] { "Original" }));
    }

    private async Task<List<string>> Names(string query, FoodType? type = null) =>
        (await search.SearchAsync(query, type)).Foods.Select(f => f.Name).ToList();

    private Task Add(string name, string searchText, FoodSource source = FoodSource.CoFID, FoodType type = FoodType.Ingredient, string? brand = null, string? originalName = null) =>
        connection.InsertAsync(NewFood(name, searchText, source, type, brand, originalName));

    private Food NewFood(string name, string searchText, FoodSource source = FoodSource.CoFID, FoodType type = FoodType.Ingredient, string? brand = null, string? originalName = null) => new()
    {
        OriginalName = originalName,
        Brand = brand,
        Type = type,
        Source = source,
        SourceId = (++nextId).ToString(),
        Name = name,
        Unit = FoodUnit.Gram,
        SearchText = searchText,
    };
}
