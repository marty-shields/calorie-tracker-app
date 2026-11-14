using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Logging;
using CalorieTracker.UI.Models.Scan;
using CalorieTracker.UI.Scanning;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.UI.Screens;

/// <summary>Scans a barcode, looks it up once and shows the product. Logs barcodes and statuses, never the product.</summary>
public partial class ScanPage : IDisposable
{
    private readonly object gate = new();
    private ScanState state = ScanState.Scanning;
    private ScannedProduct? product;
    private string? inFlight;
    private CancellationTokenSource? cancellation;
    private bool started;

    [Inject]
    private IBarcodeScanner Scanner { get; set; } = null!;

    [Inject]
    private IBarcodeLookup Lookup { get; set; } = null!;

    [Inject]
    private ILogger<ScanPage> Logger { get; set; } = null!;

    public void Dispose()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !started)
        {
            await RunScanAsync();
        }
    }

    private Task ScanAgainAsync() => RunScanAsync();

    private async Task RunScanAsync()
    {
        started = true;
        CancellationTokenSource scan;
        lock (gate)
        {
            cancellation?.Cancel();
            scan = cancellation = new CancellationTokenSource();
            inFlight = null;
        }

        product = null;
        state = ScanState.Scanning;
        StateHasChanged();

        var token = scan.Token;
        var result = await Scanner.ScanAsync(raw => Accept(raw, token));
        if (token.IsCancellationRequested)
        {
            return;
        }

        switch (result.Status)
        {
            case ScanStatus.Scanned when BarcodeNormaliser.TryNormalise(result.Digits, out var barcode):
                await BeginLookupAsync(barcode, token);
                break;
            case ScanStatus.Cancelled:
                await scan.CancelAsync();
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

    /// <summary>Called by the scanner for each detection, possibly off the UI thread. A usable barcode closes the scanner and is looked up once.</summary>
    private bool Accept(string raw, CancellationToken token)
    {
        if (!BarcodeNormaliser.TryNormalise(raw, out var barcode))
        {
            return false;
        }

        _ = InvokeAsync(() => BeginLookupAsync(barcode, token));
        return true;
    }

    private async Task BeginLookupAsync(string barcode, CancellationToken token)
    {
        lock (gate)
        {
            if (inFlight is not null || token.IsCancellationRequested)
            {
                return;
            }

            inFlight = barcode;
        }

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
        if (result is { Status: BarcodeLookupStatus.Found, Food: { } food })
        {
            product = new ScannedProduct(food.Name, food.Brand, (decimal)food.Kcal, (decimal)food.ProteinG, (decimal)food.CarbsG, (decimal)food.FatG, food.Unit == FoodUnit.Millilitre);
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
