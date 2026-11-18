using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using SQLite;

namespace CalorieTracker.Core.Tests.Foods;

public class FoodSearchTests
{
    private string databasePath = "";
    private SQLiteAsyncConnection connection = null!;
    private DatabaseGate gate = null!;
    private FakeLogger<FoodSearch> logger = null!;
    private FoodSearch search = null!;
    private FoodHistoryService history = null!;
    private FavouriteService favourites = null!;
    private DiaryService diary = null!;
    private FakeTimeProvider time = null!;
    private int nextId;

    [SetUp]
    public async Task SetUp()
    {
        databasePath = TempDatabase.NewPath();
        connection = new SQLiteAsyncConnection(databasePath);
        await connection.CreateTableAsync<Food>();
        await connection.CreateTableAsync<Serving>();
        await connection.CreateTableAsync<DiaryEntry>();
        await connection.CreateTableAsync<Favourite>();
        gate = new DatabaseGate();
        logger = new FakeLogger<FoodSearch>();
        time = new FakeTimeProvider(new DateTimeOffset(2031, 7, 19, 12, 0, 0, TimeSpan.Zero));
        history = new FoodHistoryService(new FoodHistoryStore(connection, gate), time, new FakeLogger<FoodHistoryService>());
        diary = new DiaryService(new DiaryStore(connection, gate), new FakeLogger<DiaryService>());
        favourites = new FavouriteService(new FavouriteStore(connection, gate), time, new FakeLogger<FavouriteService>());
        search = new FoodSearch(new FoodStore(connection, gate, new FakeLogger<FoodStore>()), history, favourites, logger);
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
    public async Task Search_FindsEveryTypeInOneList()
    {
        await Add("Milk", "milk", type: FoodType.Ingredient);
        await Add("Milk chocolate bar", "milk chocolate bar", type: FoodType.Food);
        await Add("Milk drink", "milk drink", type: FoodType.Drink);

        Assert.That(await Names("milk"), Is.EquivalentTo(new[] { "Milk", "Milk chocolate bar", "Milk drink" }));
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
    public async Task Search_TypoOnAMainWord_IsDroppedWhenAMatchInBracketsExists()
    {
        await Add("Aaa (milk)", "aaa (milk)");
        await Add("Zzz mlik", "zzz mlik");

        Assert.That(await Names("milk"), Is.EqualTo(new[] { "Aaa (milk)" }));
    }

    [Test]
    public async Task Search_TypoMatches_AppearOnlyWhenNoExactPrefixOrBracketMatchExists()
    {
        await Add("Broad beans", "broad beans");
        await Add("Brown bread", "brown bread");

        Assert.That(await Names("bread"), Is.EqualTo(new[] { "Brown bread" }));

        await connection.ExecuteAsync("DELETE FROM Food WHERE Name = 'Brown bread'");

        Assert.That(await Names("bread"), Is.EqualTo(new[] { "Broad beans" }));
    }

    [Test]
    public async Task Search_TwoWordQuery_DropsAFoodNeedingATypoWhenAnotherMatchesBothWordsPlainly()
    {
        await Add("Milk chocolate", "milk chocolate");
        await Add("Milk chocolat", "milk chocolat");
        await Add("Mlik chocolate", "mlik chocolate");

        Assert.That(await Names("milk chocolate"), Is.EqualTo(new[] { "Milk chocolate" }));
    }

    [Test]
    public async Task Search_CommonFoodBoost_NotAppliedWhenTheWordMatchedByTypo()
    {
        await Add("Zzz mlik", "zzz mlik", originalName: "Milk, whole, average");
        await Add("Aaa mlik", "aaa mlik");

        Assert.That(await Names("milk"), Is.EqualTo(new[] { "Aaa mlik", "Zzz mlik" }));
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

    [Test]
    public async Task Search_ASecondaryBrand_FindsTheProductThroughBrandsNotBrand()
    {
        await connection.InsertAsync(new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = "5000159407236",
            Name = "Bar",
            Brand = "Mars",
            Brands = "Mars, Mars Wrigley",
            Unit = FoodUnit.Gram,
            Type = FoodType.Food,
            SearchText = "",
        });

        Assert.That(await Names("wrigley"), Is.EqualTo(new[] { "Bar" }));
    }

    [TestCase(-0.1)]
    [TestCase(100.1)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NaN)]
    public async Task Search_NutrientOutOfRange_IsUnknownAndOthersStay(double value)
    {
        var food = NewFood("Oats", "oats");
        (food.FibreG, food.SugarsG, food.SaturatesG, food.SaltG) = (2, value, 4, 0.5);
        await connection.InsertAsync(food);

        var found = (await search.SearchAsync("oats")).Foods.Single();

        Assert.That((found.FibreG, found.SugarsG, found.SaturatesG, found.SaltG), Is.EqualTo(((double?)2, (double?)null, (double?)4, (double?)0.5)));
    }

    [TestCase(0.0)]
    [TestCase(100.0)]
    public async Task Search_NutrientAtTheLimits_IsKept(double value)
    {
        var food = NewFood("Oats", "oats");
        (food.FibreG, food.SugarsG, food.SaturatesG, food.SaltG) = (value, value, value, value);
        await connection.InsertAsync(food);

        var found = (await search.SearchAsync("oats")).Foods.Single();

        Assert.That((found.FibreG, found.SugarsG, found.SaturatesG, found.SaltG), Is.EqualTo(((double?)value, (double?)value, (double?)value, (double?)value)));
    }

    [Test]
    public async Task Search_OutOfRangeFood_LogsOnlyTheSearchAndNoContent()
    {
        var food = NewFood("Secret oats", "secret oats");
        (food.Brand, food.SaltG) = ("Hidden brand", 500);
        await connection.InsertAsync(food);

        await search.SearchAsync("secret");

        Assert.Multiple(() =>
        {
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.FoodSearched }));
            var text = string.Join('\n', logger.Collector.GetSnapshot().Select(r => r.Message));
            Assert.That(text, Does.Not.Contain("Secret").And.Not.Contain("Hidden"));
        });
    }

    [Test]
    public async Task Search_Bounds_AreNotWrittenBack()
    {
        var food = NewFood("Oats", "oats");
        food.SaltG = 500;
        await connection.InsertAsync(food);

        await search.SearchAsync("oats");

        var stored = (await connection.Table<Food>().ToListAsync()).Single();
        Assert.That(stored.SaltG, Is.EqualTo((double?)500));
    }

    private async Task<int> IdOf(string name) => (await connection.Table<Food>().FirstAsync(f => f.Name == name)).Id;

    private Task<DiaryResult> Log(string name, Meal meal, double amount, int daysAgo = 0, double? servings = null) =>
        IdOf(name).ContinueWith(id => diary.AddAsync(DateOnly.FromDateTime(time.GetLocalNow().DateTime).AddDays(-daysAgo), meal, id.Result, amount, servings)).Unwrap();

    private static string[] Names(FoodGroupsResult result, FoodGroupKind kind) =>
        result.Groups.Where(g => g.Kind == kind).SelectMany(g => g.Foods).Select(l => l.Food.Name).ToArray();

    [Test]
    public async Task Grouped_LoggedFoodIsOnlyInDiary_AndGroupsComeInOrder()
    {
        await Add("Zed pasta", "zed pasta");
        await Add("Zed rice", "zed rice");
        await Add("Zed crisps", "zed crisps", FoodSource.OpenFoodFacts, brand: "Acme");
        await Add("Zed beans", "zed beans", FoodSource.OpenFoodFacts, brand: "Acme");
        await Log("Zed beans", Meal.Lunch, 100);

        var result = await search.SearchGroupedAsync("zed");

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Diary, FoodGroupKind.Common, FoodGroupKind.Branded }));
            Assert.That(Names(result, FoodGroupKind.Diary), Is.EqualTo(new[] { "Zed beans" }));
            Assert.That(Names(result, FoodGroupKind.Common), Is.EquivalentTo(new[] { "Zed pasta", "Zed rice" }));
            Assert.That(Names(result, FoodGroupKind.Branded), Is.EqualTo(new[] { "Zed crisps" }));
        });
    }

    [Test]
    public async Task Grouped_GroupWithNoMatchesIsAbsent()
    {
        await Add("Zed pasta", "zed pasta");

        var result = await search.SearchGroupedAsync("zed");

        Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Common }));
    }

    [Test]
    public async Task Grouped_DiaryGroupHasMostLoggedFirst_AndCarriesTheLastAmount()
    {
        await Add("Zed pasta", "zed pasta");
        await Add("Zed rice", "zed rice");
        await Log("Zed pasta", Meal.Lunch, 80);
        await Log("Zed rice", Meal.Lunch, 120, servings: 2);
        await Log("Zed rice", Meal.Dinner, 140, servings: 2.5);

        var result = await search.SearchGroupedAsync("zed");

        var diaryFoods = result.Groups.Single().Foods;
        Assert.Multiple(() =>
        {
            Assert.That(diaryFoods.Select(l => l.Food.Name), Is.EqualTo(new[] { "Zed rice", "Zed pasta" }));
            Assert.That((diaryFoods[0].History!.LastAmount, diaryFoods[0].History!.LastServings), Is.EqualTo((140.0, (double?)2.5)));
        });
    }

    [Test]
    public async Task LastLogged_IsTheFoodsNewestEntry_ThatMealsWhenGiven_AndNullForAFoodNeverLogged()
    {
        await Add("Zed milk", "zed milk");
        await Add("Zed tea", "zed tea");
        await Log("Zed milk", Meal.Dinner, 200);
        await Log("Zed milk", Meal.Breakfast, 150);
        await Log("Zed milk", Meal.Dinner, 210);

        var newest = await search.LastLoggedAsync(await IdOf("Zed milk"));
        var breakfast = await search.LastLoggedAsync(await IdOf("Zed milk"), Meal.Breakfast);
        var never = await search.LastLoggedAsync(await IdOf("Zed tea"));

        Assert.Multiple(() =>
        {
            Assert.That(newest!.LastAmount, Is.EqualTo(210));
            Assert.That(breakfast!.LastAmount, Is.EqualTo(150));
            Assert.That(never, Is.Null);
        });
    }

    [Test]
    public async Task Grouped_WithAMeal_LastAmountIsThatMealsOrElseTheNewest()
    {
        await Add("Zed milk", "zed milk");
        await Add("Zed tea", "zed tea");
        await Log("Zed milk", Meal.Dinner, 200);
        await Log("Zed milk", Meal.Breakfast, 150);
        await Log("Zed milk", Meal.Dinner, 210);
        await Log("Zed tea", Meal.Dinner, 90);

        var fromBreakfast = (await search.SearchGroupedAsync("zed", Meal.Breakfast)).Groups.Single().Foods.ToDictionary(l => l.Food.Name, l => l.History!.LastAmount);
        var fromDinner = (await search.SearchGroupedAsync("zed", Meal.Dinner)).Groups.Single().Foods.ToDictionary(l => l.Food.Name, l => l.History!.LastAmount);

        Assert.Multiple(() =>
        {
            Assert.That(fromBreakfast["Zed milk"], Is.EqualTo(150));
            Assert.That(fromBreakfast["Zed tea"], Is.EqualTo(90));
            Assert.That(fromDinner["Zed milk"], Is.EqualTo(210));
        });
    }

    [Test]
    public async Task Grouped_LongGroup_IsCappedAtTheGroupLimit_InRankedOrder()
    {
        for (var i = 0; i < IFoodSearch.GroupLimit + 5; i++)
        {
            await Add($"Zed item {i:000}", "zed item");
        }

        var group = (await search.SearchGroupedAsync("zed")).Groups.Single();

        Assert.Multiple(() =>
        {
            Assert.That(group.Foods.Select(l => l.Food.Name), Is.EqualTo(Enumerable.Range(0, IFoodSearch.GroupLimit).Select(i => $"Zed item {i:000}")));
            Assert.That(group.Total, Is.EqualTo(IFoodSearch.GroupLimit + 5));
        });
    }

    [Test]
    public async Task Grouped_MoreThanTheGroupLimitMatches_CountsEveryMatch()
    {
        for (var i = 0; i < 65; i++)
        {
            await Add($"Zed item {i:000}", "zed item");
        }

        var group = (await search.SearchGroupedAsync("zed")).Groups.Single();

        Assert.Multiple(() =>
        {
            Assert.That(group.Total, Is.EqualTo(65));
            Assert.That(group.Foods, Has.Count.EqualTo(IFoodSearch.GroupLimit));
        });
    }

    [Test]
    public async Task Grouped_BlankQuery_FindsNothing()
    {
        await Add("Zed pasta", "zed pasta");

        Assert.That((await search.SearchGroupedAsync("  ")).Groups, Is.Empty);
    }

    [Test]
    public async Task Grouped_HistoryFails_StillSearchesWithoutTheDiaryGroup()
    {
        await Add("Zed pasta", "zed pasta");
        await connection.DropTableAsync<DiaryEntry>();

        var result = await search.SearchGroupedAsync("zed");

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Common }));
    }

    [Test]
    public async Task Grouped_StoreFails_ReturnsFailed()
    {
        await connection.DropTableAsync<Food>();

        var result = await search.SearchGroupedAsync("zed");

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Failed));
        Assert.That(result.Groups, Is.Empty);
    }

    [Test]
    public async Task Suggest_WithAMeal_UsualThenRecentWithNoFoodInBoth()
    {
        await Add("Zed oats", "zed oats");
        await Add("Zed eggs", "zed eggs");
        await Add("Zed soup", "zed soup");
        await Log("Zed oats", Meal.Breakfast, 40, daysAgo: 1);
        await Log("Zed oats", Meal.Breakfast, 50);
        await Log("Zed soup", Meal.Lunch, 300);
        await Log("Zed eggs", Meal.Dinner, 60);

        var result = await search.SuggestAsync(Meal.Breakfast);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Usual, FoodGroupKind.Recent }));
            Assert.That(Names(result, FoodGroupKind.Usual), Is.EqualTo(new[] { "Zed oats" }));
            Assert.That(Names(result, FoodGroupKind.Recent), Is.EqualTo(new[] { "Zed eggs", "Zed soup" }));
        });
    }

    [Test]
    public async Task Suggest_WithoutAMeal_HasNoUsualGroup()
    {
        await Add("Zed oats", "zed oats");
        await Log("Zed oats", Meal.Breakfast, 50);

        var result = await search.SuggestAsync();

        Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Recent }));
    }

    [Test]
    public async Task Suggest_EmptyDiary_HasNoGroups()
    {
        await Add("Zed oats", "zed oats");

        var result = await search.SuggestAsync(Meal.Breakfast);

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(result.Groups, Is.Empty);
    }

    [Test]
    public async Task Suggest_LimitsUsualToFiveAndRecentToTen()
    {
        for (var i = 0; i < 20; i++)
        {
            await Add($"Zed food {i:00}", "zed food");
            await Log($"Zed food {i:00}", i >= 13 ? Meal.Breakfast : Meal.Dinner, 50);
        }

        var result = await search.SuggestAsync(Meal.Breakfast);

        var usual = result.Groups.Single(g => g.Kind == FoodGroupKind.Usual).Foods;
        var recent = result.Groups.Single(g => g.Kind == FoodGroupKind.Recent).Foods;

        Assert.Multiple(() =>
        {
            Assert.That(usual, Has.Count.EqualTo(IFoodSearch.UsualLimit));
            Assert.That(recent, Has.Count.EqualTo(IFoodSearch.RecentLimit));
            Assert.That(recent.Select(l => l.Food.Id).Intersect(usual.Select(l => l.Food.Id)), Is.Empty);
        });
    }

    [Test]
    public async Task Suggest_FoodLoggedInAnotherMeal_ShowsTheNewestAmountAndOwnMealWins()
    {
        await Add("Zed milk", "zed milk");
        await Log("Zed milk", Meal.Dinner, 200);
        await Log("Zed milk", Meal.Breakfast, 150, daysAgo: 1);
        await Log("Zed milk", Meal.Dinner, 220);

        var fromBreakfast = (await search.SuggestAsync(Meal.Breakfast)).Groups.Single().Foods.Select(l => l.History!.LastAmount).ToArray();
        var fromLunch = (await search.SuggestAsync(Meal.Lunch)).Groups.Single().Foods[0].History!.LastAmount;

        Assert.That(fromBreakfast, Is.EqualTo(new[] { 150.0 }));
        Assert.That(fromLunch, Is.EqualTo(220));
    }

    [Test]
    public async Task Suggest_HistoryFails_ReturnsFailed()
    {
        await connection.DropTableAsync<DiaryEntry>();

        Assert.That((await search.SuggestAsync(Meal.Lunch)).Status, Is.EqualTo(FoodSearchStatus.Failed));
    }

    [Test]
    public async Task Logging_GroupedAndSuggest_LogCountsOnly()
    {
        await Add("Distinctive Name", "distinctive name", brand: "Secretbrand");
        await Log("Distinctive Name", Meal.Lunch, 123.4);

        await search.SearchGroupedAsync("Distinctive secretbrand", Meal.Lunch);
        await search.SuggestAsync(Meal.Lunch);

        var records = logger.Collector.GetSnapshot();
        Assert.That(LoggedEvents.Of(logger).Select(e => e.Id), Is.EqualTo(new[] { CoreEvent.FoodSearchGrouped, CoreEvent.FoodSuggested }));
        foreach (var record in records)
        {
            var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
            Assert.That(text, Does.Not.Contain("istinctive").And.Not.Contain("ecretbrand").And.Not.Contain("123"));
        }

        Assert.That(records[0].Message, Is.EqualTo("Grouped food search with 2 terms found 0 favourite, 1 diary, 0 common and 0 branded foods"));
        Assert.That(records[1].Message, Is.EqualTo("Foods before typing: 0 favourite, 1 usual and 0 recent"));
    }

    private async Task Star(params string[] names)
    {
        foreach (var name in names)
        {
            Assert.That((await favourites.AddAsync(await IdOf(name))).Status, Is.EqualTo(FavouriteStatus.Added));
        }
    }

    [Test]
    public async Task Suggest_Favourites_ComeFirstAtoZIgnoringCaseAndAccents_WithNoLimit()
    {
        foreach (var name in new[] { "Zed pear", "Éclair zed", "zed apple", "Zed Banana", "Zed cherry", "Zed date", "Zed egg" })
        {
            await Add(name, name.ToLowerInvariant());
        }

        await Star("Zed pear", "Éclair zed", "zed apple", "Zed Banana", "Zed cherry", "Zed date", "Zed egg");

        var result = await search.SuggestAsync(Meal.Breakfast);

        var group = result.Groups.Single();
        Assert.Multiple(() =>
        {
            Assert.That(group.Kind, Is.EqualTo(FoodGroupKind.Favourites));
            Assert.That(group.Foods.Select(l => l.Food.Name), Is.EqualTo(new[] { "zed apple", "Zed Banana", "Zed cherry", "Zed date", "Éclair zed", "Zed egg", "Zed pear" }.OrderBy(n => n == "Éclair zed" ? "eclair zed" : n.ToLowerInvariant(), StringComparer.Ordinal)));
            Assert.That(group.Total, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task Suggest_FavouritesWithEmptyDiary_ShowOnlyTheFavouritesGroup()
    {
        await Add("Zed oats", "zed oats");
        await Star("Zed oats");

        var result = await search.SuggestAsync(Meal.Lunch);

        Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Favourites }));
        Assert.That(result.Groups[0].Foods[0].History, Is.Null);
    }

    [Test]
    public async Task Suggest_LoggedFavourite_CarriesItsHistory_AndIsNotRepeatedInUsualOrRecent()
    {
        await Add("Zed oats", "zed oats");
        await Add("Zed eggs", "zed eggs");
        await Add("Zed soup", "zed soup");
        await Log("Zed oats", Meal.Breakfast, 50);
        await Log("Zed eggs", Meal.Breakfast, 60);
        await Log("Zed soup", Meal.Lunch, 300);
        await Star("Zed oats", "Zed soup");

        var result = await search.SuggestAsync(Meal.Breakfast);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Favourites, FoodGroupKind.Usual }));
            Assert.That(Names(result, FoodGroupKind.Favourites), Is.EqualTo(new[] { "Zed oats", "Zed soup" }));
            Assert.That(result.Groups[0].Foods.Select(l => l.History?.LastAmount), Is.EqualTo(new double?[] { 50, 300 }));
            Assert.That(Names(result, FoodGroupKind.Usual), Is.EqualTo(new[] { "Zed eggs" }));
        });
    }

    [Test]
    public async Task Suggest_FavouritesTakeNoneOfTheUsualAndRecentRoom()
    {
        for (var i = 0; i < 20; i++)
        {
            await Add($"Zed food {i:00}", "zed food");
            await Log($"Zed food {i:00}", i >= 8 ? Meal.Breakfast : Meal.Dinner, 50);
        }

        await Star("Zed food 19", "Zed food 18", "Zed food 17", "Zed food 05", "Zed food 04");

        var result = await search.SuggestAsync(Meal.Breakfast);

        var favouriteIds = result.Groups.Single(g => g.Kind == FoodGroupKind.Favourites).Foods.Select(l => l.Food.Id).ToHashSet();
        var usual = result.Groups.Single(g => g.Kind == FoodGroupKind.Usual).Foods;
        var recent = result.Groups.Single(g => g.Kind == FoodGroupKind.Recent).Foods;
        Assert.Multiple(() =>
        {
            Assert.That(usual, Has.Count.EqualTo(IFoodSearch.UsualLimit));
            Assert.That(recent, Has.Count.EqualTo(IFoodSearch.RecentLimit));
            Assert.That(usual.Concat(recent).Select(l => l.Food.Id).Intersect(favouriteIds), Is.Empty);
            Assert.That(usual.Select(l => l.Food.Id).Intersect(recent.Select(l => l.Food.Id)), Is.Empty);
        });
    }

    [Test]
    public async Task Suggest_Favourites_AreSortedByTheirShownTitle_NotByName()
    {
        await Add("Oat drink", "oat drink", brand: "Alpro");
        await Add("Bananas", "bananas");
        await Star("Bananas", "Oat drink");

        var result = await search.SuggestAsync();

        Assert.That(result.Groups.Single().Foods.Select(l => FoodDisplay.Of(l.Food).Title), Is.EqualTo(new[] { "Alpro Oat drink", "Bananas" }));
    }

    [Test]
    public async Task Suggest_FavouritesWithTheSameTitle_ComeInAscendingIdOrder()
    {
        await Add("Zed twin", "zed twin");
        await Add("zed twin", "zed twin");
        var ids = new[] { await IdOf("Zed twin"), await IdOf("zed twin") };
        await favourites.AddAsync(ids[1]);
        await favourites.AddAsync(ids[0]);

        var result = await search.SuggestAsync();

        Assert.That(result.Groups.Single().Foods.Select(l => l.Food.Id), Is.EqualTo(ids.Order()));
    }

    [Test]
    public async Task Suggest_FavouriteHistory_TakesTheMealIntoAccount()
    {
        await Add("Zed oats", "zed oats");
        await Log("Zed oats", Meal.Breakfast, 50);
        await Log("Zed oats", Meal.Lunch, 300);
        await Star("Zed oats");

        var breakfast = await search.SuggestAsync(Meal.Breakfast);
        var any = await search.SuggestAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(breakfast.Groups.Single(g => g.Kind == FoodGroupKind.Favourites).Foods[0].History?.LastAmount, Is.EqualTo(50));
            Assert.That(any.Groups.Single(g => g.Kind == FoodGroupKind.Favourites).Foods[0].History?.LastAmount, Is.EqualTo(300));
        });
    }

    [Test]
    public async Task Suggest_FavouritesReadFails_LeavesOutTheGroupOnly()
    {
        await Add("Zed oats", "zed oats");
        await Log("Zed oats", Meal.Breakfast, 50);
        await Star("Zed oats");
        await connection.DropTableAsync<Favourite>();

        var result = await search.SuggestAsync(Meal.Breakfast);

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Usual }));
    }

    [Test]
    public async Task Grouped_Favourites_AreFirstAndOnlyTheMatchingOnesInRankedOrder()
    {
        await Add("Zed milk", "zed milk");
        await Add("Zed milk shake", "zed milk shake");
        await Add("Zed rice", "zed rice");
        await Add("Zed milk logged", "zed milk logged");
        await Add("Zed milk brand", "zed milk brand", FoodSource.OpenFoodFacts, brand: "Acme");
        await Log("Zed milk logged", Meal.Lunch, 100);
        await Star("Zed milk shake", "Zed milk", "Zed rice", "Zed milk logged", "Zed milk brand");

        var result = await search.SearchGroupedAsync("zed milk", Meal.Lunch);

        var ranked = (await search.SearchAsync("zed milk")).Foods.Select(f => f.Name).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Favourites }));
            Assert.That(Names(result, FoodGroupKind.Favourites), Is.EqualTo(ranked.Where(n => n != "Zed rice")));
            Assert.That(result.Groups[0].Foods.Single(l => l.Food.Name == "Zed milk logged").History!.LastAmount, Is.EqualTo(100));
        });
    }

    [Test]
    public async Task Grouped_FavouriteIsNotRepeatedInTheOtherGroups_AndNoMatchGivesNoGroup()
    {
        await Add("Zed pasta", "zed pasta");
        await Add("Zed rice", "zed rice");
        await Add("Zed crisps", "zed crisps", FoodSource.OpenFoodFacts, brand: "Acme");
        await Add("Zed beans", "zed beans", FoodSource.OpenFoodFacts, brand: "Acme");
        await Log("Zed beans", Meal.Lunch, 100);
        await Star("Zed pasta", "Zed crisps", "Zed beans");

        var result = await search.SearchGroupedAsync("zed");
        var none = await search.SearchGroupedAsync("rice");

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Favourites, FoodGroupKind.Common }));
            Assert.That(Names(result, FoodGroupKind.Favourites), Is.EquivalentTo(new[] { "Zed pasta", "Zed crisps", "Zed beans" }));
            Assert.That(Names(result, FoodGroupKind.Common), Is.EqualTo(new[] { "Zed rice" }));
            Assert.That(none.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Common }));
        });
    }

    [Test]
    public async Task Grouped_FavouritesHoldTheBestGroupLimit_AndTotalCountsAll()
    {
        for (var i = 0; i < IFoodSearch.GroupLimit + 5; i++)
        {
            await Add($"Zed food {i:000}", "zed food");
        }

        await Star((await connection.Table<Food>().ToListAsync()).Select(f => f.Name).ToArray());

        var group = (await search.SearchGroupedAsync("zed")).Groups.Single();

        Assert.That(group.Kind, Is.EqualTo(FoodGroupKind.Favourites));
        Assert.That(group.Foods, Has.Count.EqualTo(IFoodSearch.GroupLimit));
        Assert.That(group.Total, Is.EqualTo(IFoodSearch.GroupLimit + 5));
    }

    [Test]
    public async Task Grouped_FavouritesReadFails_LeavesTheOtherGroupsWorking()
    {
        await Add("Zed pasta", "zed pasta");
        await Add("Zed beans", "zed beans");
        await Log("Zed beans", Meal.Lunch, 100);
        await Star("Zed pasta");
        await connection.DropTableAsync<Favourite>();

        var result = await search.SearchGroupedAsync("zed");

        Assert.That(result.Status, Is.EqualTo(FoodSearchStatus.Found));
        Assert.That(result.Groups.Select(g => g.Kind), Is.EqualTo(new[] { FoodGroupKind.Diary, FoodGroupKind.Common }));
    }

    [Test]
    public async Task Logging_GroupedAndSuggestWithFavourites_LogCountsOnly()
    {
        await Add("Distinctive Name", "distinctive name", brand: "Secretbrand");
        await Star("Distinctive Name");

        await search.SearchGroupedAsync("Distinctive secretbrand", Meal.Lunch);
        await search.SuggestAsync(Meal.Lunch);

        var records = logger.Collector.GetSnapshot();
        Assert.Multiple(() =>
        {
            foreach (var record in records)
            {
                var text = record.Message + string.Join(" ", record.StructuredState!.Select(p => $"{p.Key}={p.Value}"));
                Assert.That(text, Does.Not.Contain("istinctive").And.Not.Contain("ecretbrand"));
            }

            Assert.That(records[0].Message, Is.EqualTo("Grouped food search with 2 terms found 1 favourite, 0 diary, 0 common and 0 branded foods"));
            Assert.That(records[1].Message, Is.EqualTo("Foods before typing: 1 favourite, 0 usual and 0 recent"));
        });
    }

    private async Task<List<string>> Names(string query) =>
        (await search.SearchAsync(query)).Foods.Select(f => f.Name).ToList();

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
