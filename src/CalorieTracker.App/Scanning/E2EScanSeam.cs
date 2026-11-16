#if DEBUG
using CalorieTracker.Core.Foods;
using CalorieTracker.UI.Models.Settings;
using CalorieTracker.UI.Scanning;

namespace CalorieTracker.App.Scanning;

/// <summary>
/// Debug builds only, for the E2E journeys: an emulator has no barcode to point a camera at.
/// When <see cref="FileName"/> exists in the app's external files folder, the scanner returns its digits and deletes it instead of opening the camera.
/// <see cref="Barcode"/> then finds the top search result for "oat" in the seeded foods, without the network. Every other scan and lookup is untouched, except the barcodes of <see cref="E2EOnlineSearchSeam"/>, which answer for Open Food Facts search, whose search call is also replaced.
/// </summary>
internal static class E2EScanSeam
{
    public const string FileName = "e2e-barcode.txt";

    public const string Barcode = "5012345678900";

    public static IServiceCollection AddE2EScanSeam(this IServiceCollection services)
    {
        Wrap<IBarcodeLookup>(services, (inner, provider) => new SeededLookup(inner, provider.GetRequiredService<IFoodSearch>()));
        Wrap<IBarcodeScanner>(services, (inner, _) => new FileScanner(inner));
        Wrap<IOnlineFoodSearch>(services, (_, _) => new E2EOnlineSearchSeam.Search());
        return services;
    }

    /// <summary>Replaces the last registration of <typeparamref name="T"/> with one that builds the original and wraps it. Fails at start-up when there is none or it has an unknown shape.</summary>
    private static void Wrap<T>(IServiceCollection services, Func<T, IServiceProvider, T> wrap)
        where T : class
    {
        var original = services.LastOrDefault(d => d.ServiceType == typeof(T))
            ?? throw new InvalidOperationException($"E2E scan seam: no {typeof(T).Name} is registered to wrap.");
        services.Remove(original);
        services.AddSingleton(provider => wrap(Create<T>(original, provider), provider));
    }

    private static T Create<T>(ServiceDescriptor original, IServiceProvider provider)
        where T : class =>
        (original.ImplementationInstance ?? original.ImplementationFactory?.Invoke(provider)
            ?? (original.ImplementationType is { } type ? ActivatorUtilities.CreateInstance(provider, type) : null)) as T
        ?? throw new InvalidOperationException($"E2E scan seam: can't build the registered {typeof(T).Name}.");

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
            if (E2EOnlineSearchSeam.Lookup(barcode) is { } online)
            {
                return online;
            }

            if (barcode != Barcode)
            {
                return await inner.LookupAsync(barcode, ct);
            }

            var found = await search.SearchAsync("oat", 1, ct);
            return found.Foods is [var food, ..] ? BarcodeLookupResult.Found(food) : BarcodeLookupResult.NotFound();
        }
    }
}
#endif
