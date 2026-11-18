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

/// <summary>Scans a barcode, looks it up once and hands a found product straight on to Serving. Logs barcodes and statuses, never the product.</summary>
public partial class ScanPage : IDisposable
{
    private ScanState state = ScanState.OpeningCamera;
    private CancellationTokenSource? cancellation;

    [Inject]
    private IBarcodeScanner Scanner { get; set; } = null!;

    [Inject]
    private IBarcodeLookup Lookup { get; set; } = null!;

    [Inject]
    private IFoodSearch Search { get; set; } = null!;

    [Inject]
    private TimeProvider Time { get; set; } = null!;

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
        if (result is { Status: BarcodeLookupStatus.Found, Food.Id: > 0 and var id })
        {
            // The spinner stays up while the route changes: a found product never shows a screen of its own.
            var last = await Search.LastLoggedAsync(id, Meal);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await OnFound.InvokeAsync(new ScanAddRequest(id, last?.LastAmount, last?.LastServings, last?.LastServingId));
            return;
        }

        state = ScanState.NotFound;
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
