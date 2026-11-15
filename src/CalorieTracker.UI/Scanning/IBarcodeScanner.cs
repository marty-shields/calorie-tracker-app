using CalorieTracker.UI.Models.Settings;

namespace CalorieTracker.UI.Scanning;

public interface IBarcodeScanner
{
    /// <summary>
    /// Opens the camera and keeps it open, offering each EAN-13, EAN-8 or UPC-A code seen to <paramref name="accept"/>.
    /// Closes on the first code it accepts and returns it, or returns why it ended without one.
    /// </summary>
    /// <param name="accept">Called with each raw detection, possibly off the UI thread; true closes the scanner with that code.</param>
    /// <param name="theme">The app's chosen theme, which the native camera screen follows instead of the phone's.</param>
    Task<ScanResult> ScanAsync(Func<string, bool> accept, ThemePreference theme);
}
