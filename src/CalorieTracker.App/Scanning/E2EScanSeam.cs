#if DEBUG
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Scanning;

namespace CalorieTracker.App.Scanning;

/// <summary>
/// Debug builds only, for the E2E journeys: an emulator has no barcode to point a camera at.
/// When <see cref="FileName"/> exists in the app's external files folder, the scanner returns its digits and deletes it instead of opening the camera.
/// <see cref="Barcode"/> then finds the first Ingredient matching "oat" in the seeded foods, without the network. Every other scan and lookup is untouched.
/// </summary>
internal static class E2EScanSeam
{
    public const string FileName = "e2e-barcode.txt";

    public const string Barcode = "5012345678900";

    public static IServiceCollection AddE2EScanSeam(this IServiceCollection services)
    {
        var lookup = services.Last(d => d.ServiceType == typeof(IBarcodeLookup));
        services.Remove(lookup);
        services.AddSingleton<IBarcodeLookup>(provider => new SeededLookup((IBarcodeLookup)lookup.ImplementationFactory!(provider), provider.GetRequiredService<IFoodSearch>()));

        var scanner = services.Last(d => d.ServiceType == typeof(IBarcodeScanner));
        services.Remove(scanner);
        services.AddSingleton<IBarcodeScanner>(provider => new FileScanner(ActivatorUtilities.CreateInstance<CameraBarcodeScanner>(provider)));
        return services;
    }

    private sealed class FileScanner(IBarcodeScanner camera) : IBarcodeScanner
    {
        public Task<ScanResult> ScanAsync(Func<string, bool> accept, ThemePreference theme)
        {
            var path = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath ?? FileSystem.AppDataDirectory, FileName);
            if (!File.Exists(path))
            {
                return camera.ScanAsync(accept, theme);
            }

            var digits = File.ReadAllText(path).Trim();
            File.Delete(path);
            return Task.FromResult(accept(digits) ? ScanResult.Scanned(digits) : ScanResult.Cancelled);
        }
    }

    private sealed class SeededLookup(IBarcodeLookup inner, IFoodSearch search) : IBarcodeLookup
    {
        public async Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
        {
            if (barcode != Barcode)
            {
                return await inner.LookupAsync(barcode, ct);
            }

            var found = await search.SearchAsync("oat", FoodType.Ingredient, 1, ct);
            return found.Foods is [var food, ..] ? BarcodeLookupResult.Found(food) : BarcodeLookupResult.NotFound();
        }
    }
}
#endif
