using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Scanning;
using CalorieTracker.UI.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace CalorieTracker.UI.Tests.Screens;

public class ScanPageTests : ComponentTestBase
{
    private const string Barcode = "5000112637922";

    private FakeScanner scanner = null!;
    private FakeLookup lookup = null!;
    private FakeLogger<ScanPage> logger = null!;

    [SetUp]
    public void SetUp()
    {
        scanner = new FakeScanner();
        lookup = new FakeLookup();
        logger = new FakeLogger<ScanPage>();
        Context.Services.AddSingleton<IBarcodeScanner>(scanner);
        Context.Services.AddSingleton<IBarcodeLookup>(lookup);
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
    public async Task ScanAgain_ShowsOpeningCameraAgain()
    {
        var cut = Context.Render<ScanPage>();
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-again]"), Has.Count.EqualTo(1)));

        cut.Find("[data-testid=scan-again]").Click();
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(scanner.Scans, Has.Count.EqualTo(2));
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Has.Count.EqualTo(1));
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

    [Test]
    public async Task Found_ShowsTheProductPer100g()
    {
        lookup.Result = BarcodeLookupResult.Found(Product("Porridge oats", "Morning Mill", FoodUnit.Gram, 372, 11, 60, 8));
        var cut = await ScannedAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-name]").TextContent, Is.EqualTo("Porridge oats"));
            Assert.That(cut.Find("[data-testid=scan-brand]").TextContent, Is.EqualTo("Morning Mill"));
            Assert.That(cut.Find("[data-testid=scan-per]").TextContent, Is.EqualTo("Per 100 g"));
            Assert.That(cut.Find("[data-testid=scan-kcal]").TextContent, Is.EqualTo("372 kcal"));
            Assert.That(cut.Find("[data-testid=scan-protein]").TextContent, Is.EqualTo("11 g"));
            Assert.That(cut.Find("[data-testid=scan-carbs]").TextContent, Is.EqualTo("60 g"));
            Assert.That(cut.Find("[data-testid=scan-fat]").TextContent, Is.EqualTo("8 g"));
            Assert.That(cut.FindAll("[data-testid=scan-warning]"), Is.Empty);
        });
    }

    [Test]
    public async Task Found_Liquid_ShowsPer100mlAndNoBrandWhenThereIsNone()
    {
        lookup.Result = BarcodeLookupResult.Found(Product("Semi-skimmed milk", null, FoodUnit.Millilitre, 47, 3.5, 4.8, 1.7));
        var cut = await ScannedAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-per]").TextContent, Is.EqualTo("Per 100 ml"));
            Assert.That(cut.FindAll("[data-testid=scan-brand]"), Is.Empty);
        });
    }

    [Test]
    public async Task Found_WithValuesThatDoNotAddUp_ShowsTheWarning()
    {
        lookup.Result = BarcodeLookupResult.Found(Product("Chocolate spread", "Nutty Co", FoodUnit.Gram, 250, 6, 55, 31));
        var cut = await ScannedAsync();

        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-warning]"), Has.Count.EqualTo(1)));
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
            Assert.That(cut.Find("[data-testid=scan-not-found]").TextContent, Is.EqualTo("Unable to find this product"));
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

    [TestCase(double.MaxValue)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NaN)]
    public async Task Found_WithAValueThatCannotBeShown_ShowsNotFound(double kcal)
    {
        lookup.Result = BarcodeLookupResult.Found(Product("Odd", null, FoodUnit.Gram, kcal, 1, 1, 1));
        var cut = await ScannedAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-product]"), Is.Empty);
        });
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
        var cut = Context.Render<ScanPage>();
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();
        cut.WaitForAssertion(() => Assert.That(cut.FindAll("[data-testid=scan-looking-up]"), Has.Count.EqualTo(1)));

        // Scan again has no button while looking up, so call the handler the button is bound to.
        var runScan = typeof(ScanPage).GetMethod("RunScanAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        _ = cut.InvokeAsync(() => (Task)runScan.Invoke(cut.Instance, null)!);
        cut.WaitForAssertion(() => Assert.That(scanner.Scans, Has.Count.EqualTo(2)));

        late.SetResult(BarcodeLookupResult.Found(Product("Porridge oats", null, FoodUnit.Gram, 372, 11, 60, 8)));
        await WaitForRendererAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cut.FindAll("[data-testid=scan-opening-camera]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-product]"), Is.Empty);
            Assert.That(cut.FindAll("[data-testid=scan-not-found]"), Is.Empty);
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
    public async Task Found_WithExtraNutrients_ShowsTheirRows()
    {
        var food = Product("Porridge oats", null, FoodUnit.Gram, 372, 11, 60, 8);
        (food.FibreG, food.SugarsG, food.SaturatesG, food.SaltG) = (9, 1.1, 1.4, 0.3);
        lookup.Result = BarcodeLookupResult.Found(food);
        var cut = await ScannedAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.Find("[data-testid=scan-fibre]").TextContent, Is.EqualTo("9 g"));
            Assert.That(cut.Find("[data-testid=scan-sugars]").TextContent, Is.EqualTo("1.1 g"));
            Assert.That(cut.Find("[data-testid=scan-saturates]").TextContent, Is.EqualTo("1.4 g"));
            Assert.That(cut.Find("[data-testid=scan-salt]").TextContent, Is.EqualTo("0.3 g"));
        });
    }

    [Test]
    public async Task Found_WithAnExtraNutrientThatCannotBeShown_ShowsTheProductWithoutThatRow()
    {
        var food = Product("Porridge oats", null, FoodUnit.Gram, 372, 11, 60, 8);
        (food.FibreG, food.SugarsG) = (double.MaxValue, 2);
        lookup.Result = BarcodeLookupResult.Found(food);
        var cut = await ScannedAsync();

        cut.WaitForAssertion(() =>
        {
            Assert.That(cut.FindAll("[data-testid=scan-product]"), Has.Count.EqualTo(1));
            Assert.That(cut.FindAll("[data-testid=scan-fibre]"), Is.Empty);
            Assert.That(cut.Find("[data-testid=scan-sugars]").TextContent, Is.EqualTo("2 g"));
        });
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
    public async Task NavigationBar_RaisesOnNavigate()
    {
        var navigated = new List<NavDestination>();
        var cut = Context.Render<ScanPage>(p => p.Add(x => x.OnNavigate, navigated.Add));
        await WaitForRendererAsync();

        cut.Find("[data-testid=dashboard-link]").Click();

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Dashboard }));
    }

    [Test]
    public async Task AndroidBack_GoesToTheDashboard()
    {
        var navigated = new List<NavDestination>();
        Context.Render<ScanPage>(p => p.Add(x => x.OnNavigate, navigated.Add));
        await WaitForRendererAsync();

        await BackButton.HandleAsync();

        Assert.That(navigated, Is.EqualTo(new[] { NavDestination.Dashboard }));
    }

    [Test]
    public async Task Logs_TheBarcodeAndStatus_ButNeverTheProductNameOrBrand()
    {
        lookup.Result = BarcodeLookupResult.Found(Product("Porridge oats", "Morning Mill", FoodUnit.Gram, 372, 11, 60, 8));
        await ScannedAsync();

        var lines = logger.Collector.GetSnapshot().Select(r => r.Message).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(LoggedEvents.Of(logger).Select(e => e.Item1), Is.EqualTo(new[] { UiEvent.ScanLookupStarted, UiEvent.ScanLookupFinished }));
            Assert.That(lines, Has.Some.Contain(Barcode));
            Assert.That(lines, Has.None.Contain("Porridge").And.None.Contain("Morning Mill"));
        });
    }

    private async Task<IRenderedComponent<ScanPage>> ScannedAsync()
    {
        var cut = Context.Render<ScanPage>();
        scanner.Detect(Barcode);
        await WaitForRendererAsync();
        scanner.Complete(ScanResult.Scanned(Barcode));
        await WaitForRendererAsync();
        return cut;
    }

    private static Food Product(string name, string? brand, FoodUnit unit, double kcal, double protein, double carbs, double fat) =>
        new() { Source = FoodSource.OpenFoodFacts, SourceId = Barcode, Name = name, Brand = brand, Unit = unit, Kcal = kcal, ProteinG = protein, CarbsG = carbs, FatG = fat };

    private sealed class FakeScanner : IBarcodeScanner
    {
        public List<(Func<string, bool> Accept, TaskCompletionSource<ScanResult> Completion)> Scans { get; } = [];

        /// <summary>When set, a scan runs this at once with the caller's accept and completes with its result.</summary>
        public Func<Func<string, bool>, ScanResult>? Immediate { get; set; }

        public Task<ScanResult> ScanAsync(Func<string, bool> accept)
        {
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
