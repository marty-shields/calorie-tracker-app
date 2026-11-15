using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Scanning;
using CalorieTracker.UI.Theme;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CalorieTracker.UI.Screens;

/// <summary>Scans a barcode, looks it up once and shows the product. Logs barcodes and statuses, never the product.</summary>
public partial class ScanPage : IDisposable
{
    /// <summary>Largest nutrient value shown; anything above is not a real product and keeps decimal arithmetic far from overflow.</summary>
    private const double MaxValue = 1e12;

    private ScanState state = ScanState.OpeningCamera;
    private ScannedProduct? product;
    private CancellationTokenSource? cancellation;

    [Inject]
    private IBarcodeScanner Scanner { get; set; } = null!;

    [Inject]
    private IBarcodeLookup Lookup { get; set; } = null!;

    [Inject]
    private ThemeService Theme { get; set; } = null!;

    [Inject]
    private ILogger<ScanPage> Logger { get; set; } = null!;

    public void Dispose()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ScanCoreAsync();
        }
    }

    private Task RunScanAsync() => ScanCoreAsync();

    private async Task ScanCoreAsync()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        var scan = cancellation = new CancellationTokenSource();

        product = null;
        state = ScanState.OpeningCamera;
        StateHasChanged();

        var token = scan.Token;
        var result = await Scanner.ScanAsync(Accept, await SavedThemeAsync());
        if (token.IsCancellationRequested)
        {
            return;
        }

        switch (result.Status)
        {
            case ScanStatus.Scanned when BarcodeNormaliser.TryNormalise(result.Digits, out var barcode):
                await LookUpAsync(barcode, token);
                break;
            case ScanStatus.Cancelled:
                state = ScanState.Closing;
                StateHasChanged();
                await OnBack.InvokeAsync();
                break;
            case ScanStatus.PermissionDenied or ScanStatus.Unavailable:
                Log.CameraUnavailable(Logger, result.Status);
                state = ScanState.CameraUnavailable;
                StateHasChanged();
                break;
            default:
                state = ScanState.NotFound;
                StateHasChanged();
                break;
        }
    }

    /// <summary>The native camera screen follows the app's theme, which only the web view knows. System when it can't be read.</summary>
    private async Task<ThemePreference> SavedThemeAsync()
    {
        try
        {
            return await Theme.GetAsync();
        }
        catch (JSException)
        {
            return ThemePreference.System;
        }
        catch (JSDisconnectedException)
        {
            return ThemePreference.System;
        }
    }

    /// <summary>Called by the scanner for each detection, possibly off the UI thread. Only says whether the code is usable; the scanner then closes and the page looks up the one barcode it returns.</summary>
    private static bool Accept(string raw) => BarcodeNormaliser.TryNormalise(raw, out _);

    private static bool TryToDecimal(double value, out decimal result)
    {
        if (double.IsFinite(value) && Math.Abs(value) <= MaxValue)
        {
            result = (decimal)value;
            return true;
        }

        result = 0;
        return false;
    }

    /// <summary>Null when the source has no value or it is not a real one: the row is left out rather than losing the product.</summary>
    private static decimal? ToOptionalDecimal(double? value) =>
        value is { } number && TryToDecimal(number, out var result) ? result : null;

    private async Task LookUpAsync(string barcode, CancellationToken token)
    {
        state = ScanState.LookingUp;
        StateHasChanged();
        Log.LookupStarted(Logger, barcode);

        BarcodeLookupResult result;
        try
        {
            result = await Lookup.LookupAsync(barcode, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        Log.LookupFinished(Logger, barcode, result.Status);
        product = null;
        if (result is { Status: BarcodeLookupStatus.Found, Food: { } food }
            && TryToDecimal(food.Kcal, out var kcal)
            && TryToDecimal(food.ProteinG, out var protein)
            && TryToDecimal(food.CarbsG, out var carbs)
            && TryToDecimal(food.FatG, out var fat))
        {
            product = new ScannedProduct(food.Name, food.Brand, kcal, protein, carbs, fat, food.Unit == FoodUnit.Millilitre,
                ToOptionalDecimal(food.FibreG), ToOptionalDecimal(food.SugarsG), ToOptionalDecimal(food.SaturatesG), ToOptionalDecimal(food.SaltG));
            state = ScanState.Found;
        }
        else
        {
            state = ScanState.NotFound;
        }

        StateHasChanged();
    }

    private static partial class Log
    {
        [LoggerMessage((int)UiEvent.ScanLookupStarted, LogLevel.Information, "Looking up barcode {Barcode}")]
        public static partial void LookupStarted(ILogger logger, string barcode);

        [LoggerMessage((int)UiEvent.ScanLookupFinished, LogLevel.Information, "Lookup of barcode {Barcode} finished: {Status}")]
        public static partial void LookupFinished(ILogger logger, string barcode, BarcodeLookupStatus status);

        [LoggerMessage((int)UiEvent.ScanCameraUnavailable, LogLevel.Information, "Scan ended without a camera: {Status}")]
        public static partial void CameraUnavailable(ILogger logger, ScanStatus status);
    }
}
