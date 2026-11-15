using System.Reflection;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Seed;

public enum SeedLoadStatus
{
    Loaded,
    UpToDate,
    Failed,
}

/// <param name="Version">The bundled seed's version; 0 when the status is Failed.</param>
public sealed record SeedLoadResult(SeedLoadStatus Status, int Version, int Inserted, int Updated);

/// <summary>Loads the bundled CoFID seed into the Food table.</summary>
public interface ISeedLoader
{
    /// <summary>
    /// Loads the seed unless the stored version is already the bundled one or newer. Never throws and parses the seed off the caller's thread, so a UI caller returns at once; a failure is returned
    /// and logged, and the version is stored only after every food is written, so the next call retries.
    /// </summary>
    Task<SeedLoadResult> LoadAsync();
}

internal sealed partial class SeedLoader(FoodStore store, ILogger<SeedLoader> logger, Func<Stream> openSeed) : ISeedLoader
{
    internal const int ChunkSize = 500;
    private const string ResourceName = "cofid-foods.json";

    public static Stream OpenBundledSeed() =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
        ?? throw new FileNotFoundException("The seed is not embedded");

    public async Task<SeedLoadResult> LoadAsync()
    {
        try
        {
            var seed = await Task.Run(() =>
            {
                using var stream = openSeed();
                return SeedFile.Parse(stream);
            });

            var stored = await store.GetSeedVersionAsync(FoodSource.CoFID);
            if (stored >= seed.Version)
            {
                Log.UpToDate(logger, stored.Value, seed.Version);
                return new SeedLoadResult(SeedLoadStatus.UpToDate, seed.Version, 0, 0);
            }

            var inserted = 0;
            var updated = 0;
            foreach (var chunk in seed.Foods.Chunk(ChunkSize))
            {
                var result = await store.UpsertManyAsync(chunk);
                inserted += result.Inserted;
                updated += result.Updated;
            }

            await store.DeleteMissingAsync(FoodSource.CoFID, seed.Foods.Select(f => f.SourceId).ToHashSet());
            await store.SetSeedVersionAsync(FoodSource.CoFID, seed.Version);
            Log.Loaded(logger, seed.Version, inserted, updated);
            return new SeedLoadResult(SeedLoadStatus.Loaded, seed.Version, inserted, updated);
        }
        catch (Exception ex)
        {
            Log.Failed(logger, ex);
            return new SeedLoadResult(SeedLoadStatus.Failed, 0, 0, 0);
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.SeedLoaded, LogLevel.Information, "Seed version {Version} loaded: {Inserted} foods inserted, {Updated} updated")]
        public static partial void Loaded(ILogger logger, int version, int inserted, int updated);

        [LoggerMessage((int)CoreEvent.SeedUpToDate, LogLevel.Information, "Seed up to date: stored version {Stored}, bundled version {Bundled}")]
        public static partial void UpToDate(ILogger logger, int stored, int bundled);

        [LoggerMessage((int)CoreEvent.SeedLoadFailed, LogLevel.Error, "Seed load failed")]
        public static partial void Failed(ILogger logger, Exception exception);
    }
}
