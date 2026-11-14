using BarcodeScanning;
using CalorieTracker.App.Logging;
using CalorieTracker.UI.Scanning;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.App.Scanning;

internal sealed partial class MauiBarcodeScanner(ILogger<MauiBarcodeScanner> logger) : IBarcodeScanner
{
    public async Task<ScanResult> ScanAsync()
    {
        var granted = await MainThread.InvokeOnMainThreadAsync(Methods.AskForRequiredPermissionAsync);
        if (!granted)
        {
            Log.PermissionDenied(logger);
            return ScanResult.PermissionDenied;
        }

        var navigation = Application.Current?.Windows is [var window, ..] ? window.Page?.Navigation : null;
        if (navigation is null)
        {
            Log.NoWindow(logger);
            return ScanResult.Cancelled;
        }

        var page = new ScanPage();
        await MainThread.InvokeOnMainThreadAsync(() => navigation.PushModalAsync(page));
        var result = await page.Result;
        Log.Finished(logger, result.Status);
        return result;
    }

    private static partial class Log
    {
        [LoggerMessage((int)AppEvent.ScanPermissionDenied, LogLevel.Information, "Camera permission denied; no scan")]
        public static partial void PermissionDenied(ILogger logger);

        [LoggerMessage((int)AppEvent.ScanNoWindow, LogLevel.Warning, "No window to show the scanner in")]
        public static partial void NoWindow(ILogger logger);

        [LoggerMessage((int)AppEvent.ScanFinished, LogLevel.Information, "Scan finished: {Status}")]
        public static partial void Finished(ILogger logger, ScanStatus status);
    }
}
