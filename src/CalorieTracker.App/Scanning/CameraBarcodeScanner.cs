using BarcodeScanning;
using CalorieTracker.App.Logging;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Scanning;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.App.Scanning;

internal sealed partial class CameraBarcodeScanner(ILogger<CameraBarcodeScanner> logger) : IBarcodeScanner
{
    // Only touched on the main thread, so a second tap while a scan is open joins it instead of pushing another page.
    private Task<ScanResult>? pending;

    public Task<ScanResult> ScanAsync(Func<string, bool> accept, ThemePreference theme) => MainThread.InvokeOnMainThreadAsync(() => JoinOrOpenAsync(accept, theme));

    private async Task<ScanResult> JoinOrOpenAsync(Func<string, bool> accept, ThemePreference theme)
    {
        if (pending is not null)
        {
            return await pending;
        }

        pending = OpenAsync(accept, theme);
        try
        {
            return await pending;
        }
        finally
        {
            pending = null;
        }
    }

    private async Task<ScanResult> OpenAsync(Func<string, bool> accept, ThemePreference theme)
    {
        var result = ScanResult.Unavailable;
        var app = Application.Current;
        var previousTheme = app?.UserAppTheme ?? AppTheme.Unspecified;
        try
        {
            if (!await Methods.AskForRequiredPermissionAsync())
            {
                Log.PermissionDenied(logger);
                result = ScanResult.PermissionDenied;
            }
            else if (app?.Windows is [var window, ..] && window.Page?.Navigation is { } navigation)
            {
                // AppThemeBinding on the camera page follows UserAppTheme, so give it the app's choice rather than the phone's.
                app.UserAppTheme = theme switch
                {
                    ThemePreference.Light => AppTheme.Light,
                    ThemePreference.Dark => AppTheme.Dark,
                    _ => AppTheme.Unspecified,
                };
                var page = new ScanPage(accept);
                await navigation.PushModalAsync(page);
                result = await page.Result;
            }
            else
            {
                Log.NoWindow(logger);
            }
        }
        catch (Exception ex)
        {
            Log.Failed(logger, ex.GetType().Name);
        }
        finally
        {
            // Runs on the main thread, as ScanAsync marshals the whole scan there.
            if (app is not null)
            {
                app.UserAppTheme = previousTheme;
            }
        }

        Log.Finished(logger, result.Status);
        return result;
    }

    private static partial class Log
    {
        [LoggerMessage((int)AppEvent.ScanPermissionDenied, LogLevel.Information, "Camera permission denied; no scan")]
        public static partial void PermissionDenied(ILogger logger);

        [LoggerMessage((int)AppEvent.ScanNoWindow, LogLevel.Warning, "No window to show the scanner in")]
        public static partial void NoWindow(ILogger logger);

        [LoggerMessage((int)AppEvent.ScanFailed, LogLevel.Warning, "Scanner failed to open: {ExceptionType}")]
        public static partial void Failed(ILogger logger, string exceptionType);

        [LoggerMessage((int)AppEvent.ScanFinished, LogLevel.Information, "Scan finished: {Status}")]
        public static partial void Finished(ILogger logger, ScanStatus status);
    }
}
