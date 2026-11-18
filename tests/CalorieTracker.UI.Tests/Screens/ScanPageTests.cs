using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Scanning;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace CalorieTracker.UI.Tests.Screens;

public class ScanPageTests : ComponentTestBase
{
    private const string Barcode = "5000112637922";

    private FakeScanner scanner = null!;
    private FakeLookup lookup = null!;
    private FakeFoodSearch search = null!;
    private FakeLogger<ScanPage> logger = null!;

    [SetUp]
    public void SetUp()
    {
        scanner = new FakeScanner();
        lookup = new FakeLookup();
        search = new FakeFoodSearch();
        logger = new FakeLogger<ScanPage>();
        Context.JSInterop.Setup<string>("calorieTrackerTheme.get").SetResult("dark");
        Context.Services.AddSingleton<IBarcodeScanner>(scanner);
        Context.Services.AddSingleton<IBarcodeLookup>(lookup);
        Context.Services.AddSingleton<IFoodSearch>(search);
        Context.Services.AddSingleton<ILogger<ScanPage>>(logger);
    }

    [Test]
    public void OnFirstRender_StartsOneScanAndShowsTheCamera()
    {
        var cut = Context.Render<ScanPage>();

        Assert.Multiple(() =>
        {
            Assert.That(scanner.Scans, Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-camera]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-hint]"), Is.Empty);
        });
    }

    [Test]
    public async Task AcceptedBarcode_IsLookedUpExactlyOnce()
    {
        lookup.Hold = true;
        var cut = Context.Render<ScanPage>();

        Assert.That(scanner.Detect(Barcode), Is.True);
        await WaitForRendererAsync();
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();

        Assert.That(lookup.Barcodes, Is.EqualTo(new[] { Barcode }));
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-looking-up]"), Has.Count.EqualTo(1)));
    }

    [Test]
    public async Task ScannerThatAcceptsTheSameBarcodeSeveralTimes_StillLooksItUpOnce()
    {
        lookup.Hold = true;
        scanner.Immediate = accept =>
        {
            Assert.That(accept(Barcode), Is.True);
            Assert.That(accept(Barcode), Is.True);
            Assert.That(accept(Barcode), Is.True);
            return ScanResult.Scanned(Barcode);
        };

        var cut = Context.Render<ScanPage>();
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(lookup.Barcodes, Is.EqualTo(new[] { Barcode }));
            Assert.That(cut.FindAll("[data-testid=scan-looking-up]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task UpcA_IsLookedUpWithALeadingZero()
    {
        Context.Render<ScanPage>();

        scanner.Detect("036000291452");
        scanner.Complete(ScanResult.Scanned("036000291452"));
        await WaitForRendererAsync();

        Assert.That(lookup.Barcodes, Is.EqualTo(new[] { "0036000291452" }));
    }

    [TestCase("12345")]
    [TestCase("")]
    [TestCase("not a barcode")]
    public async Task InvalidLengthCode_IsRefusedAndNotLookedUp(string raw)
    {
        var cut = Context.Render<ScanPage>();

        var accepted = scanner.Detect(raw);
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.False);
            Assert.That(lookup.Barcodes, Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Has.Count.EqualTo(1));
        });
    }

    [TestCase(BarcodeLookupStatus.NotFound)]
    [TestCase(BarcodeLookupStatus.Offline)]
    [TestCase(BarcodeLookupStatus.RateLimited)]
    public async Task EveryStatusButFound_ShowsTheOneMessageAndScanAgain(BarcodeLookupStatus status)
    {
        lookup.Result = status switch
        {
            BarcodeLookupStatus.Offline => BarcodeLookupResult.Offline(),
            BarcodeLookupStatus.RateLimited => BarcodeLookupResult.RateLimited(),
            _ => BarcodeLookupResult.NotFound(),
        };
        var cut = await ScannedAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-not-found-message]").TextContent, Is.EqualTo("Unable to find this product"));
            Assert.That(cut.FindAll("[data-testid=scan-again]"), Has.Count.EqualTo(1));
        });
    }

    [TestCase(ScanStatus.PermissionDenied)]
    [TestCase(ScanStatus.Unavailable)]
    public async Task CameraRefused_ShowsTheCameraUnavailableState(ScanStatus status)
    {
        var cut = Context.Render<ScanPage>();

        scanner.Complete(status == ScanStatus.PermissionDenied ? ScanResult.PermissionDenied : ScanResult.Unavailable);
        await WaitForRendererAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-camera-unavailable-error]").TextContent, Is.EqualTo("Couldn't open the camera"));
            Assert.That(cut.FindAll("[data-testid=scan-camera-unavailable-retry]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-again]"), Is.Empty);
        });
        Assert.Multiple(() =>
        {
            Assert.That(lookup.Barcodes, Is.Empty);
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Item1), Is.EqualTo(new[] { UiEvent.ScanCameraUnavailable }));
        });
    }

    [Test]
    public async Task ScannedCodeThatDoesNotNormalise_ShowsNotFoundWithoutALookup()
    {
        var cut = Context.Render<ScanPage>();

        scanner.Complete(ScanResult.Scanned("123"));
        await WaitForRendererAsync();

        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Has.Count.EqualTo(1)));
        Assert.That(lookup.Barcodes, Is.Empty);
    }

    [Test]
    public async Task Cancelled_RaisesOnBack()
    {
        var backs = 0;
        Context.Render<ScanPage>(p => p.Add(x => x.OnBack, () => backs++));

        scanner.Complete(ScanResult.Cancelled);
        await WaitForRendererAsync();

        Assert.That(backs, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchByName_RaisesOnSearchByName_NotOnBack_AndShowsClosingCamera()
    {
        var searches = 0;
        var backs = 0;
        var cut = Context.Render<ScanPage>(p => p.Add(x => x.OnSearchByName, () => searches++).Add(x => x.OnBack, () => backs++));

        scanner.Complete(ScanResult.SearchByName);
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(searches, Is.EqualTo(1));
            Assert.That(backs, Is.Zero);
            Assert.That(cut.FindAll("[data-testid=scan-closing-camera]"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void TheScanner_ReceivesTheAddingToCaptionForTheMealAndDay()
    {
        Context.Render<ScanPage>(p => p.Add(x => x.Meal, Meal.Dinner).Add(x => x.Date, new DateOnly(2026, 11, 18)));

        Assert.That(scanner.Captions, Is.EqualTo(new[] { "Adding to Dinner · 18 Nov" }));
    }

    [Test]
    public async Task Cancelled_ShowsClosingCameraNotOpeningCamera_WhenOnBackIsRaised()
    {
        string? markupAtBack = null;
        IRenderedComponent<ScanPage>? cut = null;
        cut = Context.Render<ScanPage>(p => p.Add(x => x.OnBack, () => markupAtBack = cut!.Markup));

        scanner.Complete(ScanResult.Cancelled);
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(markupAtBack, Does.Contain("scan-closing-camera").And.Contain("Closing camera"));
            Assert.That(markupAtBack, Does.Not.Contain("scan-opening-camera").And.Not.Contain("Opening camera"));
            Assert.That(cut.FindAll("[data-testid=scan-closing-camera]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Is.Empty);
        });
    }

    [TestCase("light", ThemePreference.Light)]
    [TestCase("dark", ThemePreference.Dark)]
    [TestCase("system", ThemePreference.System)]
    public void Scan_PassesTheSavedThemeToTheScanner(string saved, ThemePreference expected)
    {
        Context.JSInterop.Setup<string>("calorieTrackerTheme.get").SetResult(saved);

        Context.Render<ScanPage>();

        Assert.That(scanner.Themes, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void Scan_ThemeCannotBeRead_StillScansWithSystem()
    {
        Context.JSInterop.Setup<string>("calorieTrackerTheme.get").SetException(new JSException("storage unavailable"));

        Context.Render<ScanPage>();

        Assert.That(scanner.Themes, Is.EqualTo(new[] { ThemePreference.System }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ScanResultAfterDispose_IsIgnored(bool cancelled)
    {
        var backs = 0;
        Context.Render<ScanPage>(p => p.Add(x => x.OnBack, () => backs++));
        await Context.DisposeComponentsAsync();

        scanner.Complete(cancelled ? ScanResult.Cancelled : ScanResult.Unavailable);
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(backs, Is.Zero);
            Assert.That(LoggedEvents.Of(logger), Is.Empty);
        });
    }

    [Test]
    public async Task LookupThatIgnoresItsToken_AndReturnsAfterScanAgain_IsDiscarded()
    {
        var late = new TaskCompletionSource<BarcodeLookupResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        lookup.Late = late;
        var found = new List<ScanAddRequest>();
        var cut = Context.Render<ScanPage>(p => p.Add(x => x.OnFound, found.Add));
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-looking-up]"), Has.Count.EqualTo(1)));

        // Scan again has no button while looking up, so call the handler the button is bound to.
        var runScan = typeof(ScanPage).GetMethod("RunScanAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        _ = cut.InvokeAsync(() => (Task)runScan.Invoke(cut.Instance, null)!);
        cut.WaitForAssertion(() => Assert.That(scanner.Scans, Has.Count.EqualTo(2)));

        late.SetResult(BarcodeLookupResult.Found(Stored(42)));
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Is.Empty);
            Assert.That(found, Is.Empty);
        });
    }

    [Test]
    public async Task TryAgainAfterCameraRefused_StartsANewScan()
    {
        var cut = Context.Render<ScanPage>();
        scanner.Complete(ScanResult.PermissionDenied);
        await WaitForRendererAsync();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-camera-unavailable-retry]"), Has.Count.EqualTo(1)));

        cut.Find("[data-testid=scan-camera-unavailable-retry]").Click();
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(scanner.Scans, Has.Count.EqualTo(2));
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-camera-unavailable-error]"), Is.Empty);
        });
    }

    [Test]
    public async Task Found_WithAStoredFood_RaisesOnFoundAtOnceWithItsId_AndShowsNoValuesScreen()
    {
        lookup.Result = BarcodeLookupResult.Found(Stored(42));
        var found = new List<ScanAddRequest>();
        var cut = await ScannedAsync(p => p.Add(x => x.OnFound, found.Add));

        cut.WaitForAssertion(() => Assert.That(found, Is.EqualTo(new[] { new ScanAddRequest(42) })));
        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=scan-product]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-add]"), Is.Empty);
            Assert.That(cut.FindAll("footer"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Is.Empty);
        });
    }

    [Test]
    public async Task Found_WithAStoredFoodLoggedBefore_CarriesItsLastServing()
    {
        lookup.Result = BarcodeLookupResult.Found(Stored(42));
        search.Last[42] = new CalorieTracker.Core.History.FoodHistory(42, 3, 9, new DateOnly(2031, 7, 19), 45, 3, 77);
        var found = new List<ScanAddRequest>();
        var cut = await ScannedAsync(p => p.Add(x => x.OnFound, found.Add));

        cut.WaitForAssertion(() => Assert.That(found, Is.EqualTo(new[] { new ScanAddRequest(42, 45, 3, 77) })));
    }

    [Test]
    public async Task Found_AsksForTheLastServingInTheMealBeingAddedTo()
    {
        lookup.Result = BarcodeLookupResult.Found(Stored(42));
        var found = new List<ScanAddRequest>();
        var cut = await ScannedAsync(p => p.Add(x => x.Meal, Meal.Dinner).Add(x => x.OnFound, found.Add));

        cut.WaitForAssertion(() => Assert.That(found, Has.Count.EqualTo(1)));
        Assert.That(search.MealsAsked, Is.EqualTo(new Meal?[] { Meal.Dinner }));
    }

    [Test]
    public async Task Found_WithAFoodThatWasNotSaved_ShowsNotFoundAndHandsNothingOn()
    {
        lookup.Result = BarcodeLookupResult.Found(Product("Porridge oats", null, FoodUnit.Gram, 372, 11, 60, 8));
        var found = new List<ScanAddRequest>();
        var cut = await ScannedAsync(p => p.Add(x => x.OnFound, found.Add));

        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Has.Count.EqualTo(1)));
        Assert.That(found, Is.Empty);
    }

    [Test]
    public async Task ScanAgain_AfterNotFound_ThenAFoundProduct_HandsItOn()
    {
        lookup.Result = BarcodeLookupResult.NotFound();
        var found = new List<ScanAddRequest>();
        var cut = await ScannedAsync(p => p.Add(x => x.OnFound, found.Add));
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-again]"), Has.Count.EqualTo(1)));
        lookup.Result = BarcodeLookupResult.Found(Stored(7));

        cut.Find("[data-testid=scan-again]").Click();
        scanner.Detect(Barcode);
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();

        cut.WaitForAssertion(() => Assert.That(found, Is.EqualTo(new[] { new ScanAddRequest(7) })));
    }

    [Test]
    public void TheChip_ShowsTheMealAndTheGivenDay()
    {
        var cut = Context.Render<ScanPage>(p => p.Add(x => x.Meal, Meal.Dinner).Add(x => x.Date, new DateOnly(2026, 11, 12)));

        Assert.That(cut.Find("[data-testid=scan-adding]").TextContent, Is.EqualTo("Adding to Dinner · 12 Nov"));
    }

    [Test]
    public void TheChip_WithoutADay_ShowsToday()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        Context.Services.AddSingleton<TimeProvider>(clock);

        var cut = Context.Render<ScanPage>(p => p.Add(x => x.Meal, Meal.Lunch));

        Assert.That(cut.Find("[data-testid=scan-adding]").TextContent, Is.EqualTo("Adding to Lunch · 18 Nov"));
    }

    [Test]
    public async Task NotFound_SearchByName_RaisesOnSearchByName()
    {
        lookup.Result = BarcodeLookupResult.NotFound();
        var searched = 0;
        var cut = await ScannedAsync(p => p.Add(x => x.OnSearchByName, () => searched++));
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-search]"), Has.Count.EqualTo(1)));

        cut.Find("[data-testid=scan-search]").Click();

        Assert.That(searched, Is.EqualTo(1));
    }

    [Test]
    public async Task ScanAgain_StartsANewScan()
    {
        lookup.Result = BarcodeLookupResult.NotFound();
        var cut = await ScannedAsync();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-again]"), Has.Count.EqualTo(1)));

        cut.Find("[data-testid=scan-again]").Click();
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(scanner.Scans, Has.Count.EqualTo(2));
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Has.Count.EqualTo(1));
        });

        scanner.Detect(Barcode);
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();
        Assert.That(lookup.Barcodes, Is.EqualTo(new[] { Barcode, Barcode }), "the same barcode is looked up again after Scan again");
    }

    [Test]
    public async Task Dispose_CancelsTheLookup()
    {
        lookup.Hold = true;
        Context.Render<ScanPage>();
        scanner.Detect(Barcode);
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();

        await Context.DisposeComponentsAsync();

        Assert.That(lookup.Token!.Value.IsCancellationRequested, Is.True);
    }

    [Test]
    public async Task ThereIsNoNavigationBar()
    {
        var cut = Context.Render<ScanPage>();
        await WaitForRendererAsync();

        Assert.That(cut.FindAll("[data-testid=bottom-nav]"), Is.Empty);
    }

    [Test]
    public async Task BackArrowAndAndroidBack_RaiseOnBack()
    {
        var back = 0;
        var cut = Context.Render<ScanPage>(p => p.Add(x => x.OnBack, () => back++));
        await WaitForRendererAsync();

        await cut.Find("[data-testid=back]").ClickAsync(new());
        await BackButton.HandleAsync();

        Assert.That(back, Is.EqualTo(2));
    }

    [Test]
    public async Task Logs_TheBarcodeAndStatus_ButNeverTheProductNameBrandOrMeal()
    {
        var food = Product("Porridge oats", "Morning Mill", FoodUnit.Gram, 372, 11, 60, 8);
        food.Id = 42;
        lookup.Result = BarcodeLookupResult.Found(food);
        var found = new List<ScanAddRequest>();
        var cut = await ScannedAsync(p => p.Add(x => x.OnFound, found.Add));
        cut.WaitForAssertion(() => Assert.That(found, Has.Count.EqualTo(1)));

        var lines = logger.Collector.GetSnapshot().Select(r => r.Message).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Item1), Is.EqualTo(new[] { UiEvent.ScanLookupStarted, UiEvent.ScanLookupFinished }));
            Assert.That(lines, Has.Some.Contain(Barcode));
            Assert.That(lines, Has.None.Contain("Porridge").And.None.Contain("Morning Mill"));
        });
    }

    private async Task<IRenderedComponent<ScanPage>> ScannedAsync(Action<ComponentParameterCollectionBuilder<ScanPage>>? parameters = null)
    {
        var cut = Context.Render<ScanPage>(parameters ?? (_ => { }));
        scanner.Detect(Barcode);
        await WaitForRendererAsync();
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();
        return cut;
    }

    private static Food Stored(int id) => new() { Id = id, Source = FoodSource.OpenFoodFacts, SourceId = Barcode, Name = "Porridge oats", Brand = "Morning Mill", Unit = FoodUnit.Gram, Kcal = 372, ProteinG = 11, CarbsG = 60, FatG = 8 };

    private static Food Product(string name, string? brand, FoodUnit unit, double kcal, double protein, double carbs, double fat) =>
        new() { Source = FoodSource.OpenFoodFacts, SourceId = Barcode, Name = name, Brand = brand, Unit = unit, Kcal = kcal, ProteinG = protein, CarbsG = carbs, FatG = fat };

    private sealed class FakeFoodSearch : IFoodSearch
    {
        public Dictionary<int, CalorieTracker.Core.History.FoodHistory> Last { get; } = [];

        public List<Meal?> MealsAsked { get; } = [];

        public Task<CalorieTracker.Core.History.FoodHistory?> LastLoggedAsync(int foodId, Meal? meal = null)
        {
            MealsAsked.Add(meal);
            return Task.FromResult(Last.GetValueOrDefault(foodId));
        }

        public Task<FoodSearchResult> SearchAsync(string query, int limit = IFoodSearch.DefaultLimit, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<FoodGroupsResult> SearchGroupedAsync(string query, Meal? meal = null, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<FoodGroupsResult> SuggestAsync(Meal? meal = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeScanner : IBarcodeScanner
    {
        public List<(Func<string, bool> Accept, TaskCompletionSource<ScanResult> Completion)> Scans { get; } = [];

        /// <summary>When set, a scan runs this at once with the caller's accept and completes with its result.</summary>
        public Func<Func<string, bool>, ScanResult>? Immediate { get; set; }

        public List<ThemePreference> Themes { get; } = [];

        public List<string> Captions { get; } = [];

        public Task<ScanResult> ScanAsync(Func<string, bool> accept, ThemePreference theme, string caption)
        {
            Themes.Add(theme);
            Captions.Add(caption);
            if (Immediate is not null)
            {
                return Task.FromResult(Immediate(accept));
            }

            var completion = new TaskCompletionSource<ScanResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Scans.Add((accept, completion));
            return completion.Task;
        }

        /// <summary>Offers a detection to the latest scan, as the camera does.</summary>
        public bool Detect(string raw) => Scans[^1].Accept(raw);

        public void Complete(ScanResult result) => Scans[^1].Completion.TrySetResult(result);
    }

    private sealed class FakeLookup : IBarcodeLookup
    {
        public List<string> Barcodes { get; } = [];

        public BarcodeLookupResult Result { get; set; } = BarcodeLookupResult.NotFound();

        /// <summary>When set, a lookup never finishes on its own.</summary>
        public bool Hold { get; set; }

        /// <summary>When set, the lookup waits for this and ignores its token.</summary>
        public TaskCompletionSource<BarcodeLookupResult>? Late { get; set; }

        public CancellationToken? Token { get; private set; }

        public async Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
        {
            Barcodes.Add(barcode);
            Token = ct;
            if (Late is not null)
            {
                return await Late.Task;
            }

            if (Hold)
            {
                await Task.Delay(Timeout.Infinite, ct);
            }

            return Result;
        }
    }
}
