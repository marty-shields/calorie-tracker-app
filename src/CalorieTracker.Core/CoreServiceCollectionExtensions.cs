using CalorieTracker.Core.Data;
using CalorieTracker.Core.Diary;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Seed;
using CalorieTracker.Core.Summary;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SQLite;

namespace CalorieTracker.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core's services as singletons. The barcode lookup is the stored decorator over the Open Food Facts adapter, which is registered as its concrete type only. The initializer is registered only as a gated decorator
    /// that runs each call through one <see cref="DatabaseGate"/>; the connection is not registered.
    /// The host must register logging.
    /// </summary>
    /// <param name="databasePath">Full path of the SQLite database file.</param>
    public static IServiceCollection AddCore(this IServiceCollection services, string databasePath) =>
        services
            .AddDatabase(databasePath)
            .AddSchemaUpgrades()
            .AddServices();

    /// <summary>The gated initializer, and each repository and unit of work added later, share one connection to <paramref name="databasePath"/>.</summary>
    private static IServiceCollection AddDatabase(this IServiceCollection services, string databasePath)
    {
        var connection = new SQLiteAsyncConnection(databasePath);
        return services
            .AddSingleton<DatabaseGate>()
            .AddSingleton(provider => ActivatorUtilities.CreateInstance<TargetStore>(provider, connection))
            .AddSingleton(provider => ActivatorUtilities.CreateInstance<DiaryStore>(provider, connection))
            .AddSingleton(provider => ActivatorUtilities.CreateInstance<DaySummaryStore>(provider, connection))
            .AddSingleton(provider => ActivatorUtilities.CreateInstance<FoodStore>(provider, connection))
            .AddDecoratedSingleton<IDatabaseInitializer, GatedDatabaseInitializer>(provider =>
                ActivatorUtilities.CreateInstance<DatabaseInitializer>(provider, connection))
            .AddDecoratedSingleton<IBarcodeLookup, StoredBarcodeLookup>(provider => provider.GetRequiredService<OpenFoodFactsLookup>());
    }

    /// <summary>Registers each numbered schema upgrade step, one for each version after 1.</summary>
    private static IServiceCollection AddSchemaUpgrades(this IServiceCollection services) =>
        services
            .AddSingleton<ISchemaUpgrade, FoodBarcodeUpgrade>()
            .AddSingleton<ISchemaUpgrade, FoodTypeUpgrade>()
            .AddSingleton<ISchemaUpgrade, DropDishesUpgrade>()
            .AddSingleton<ISchemaUpgrade, FoodServingUpgrade>()
            .AddSingleton<ISchemaUpgrade, SettingsTableUpgrade>()
            .AddSingleton<ISchemaUpgrade, DiaryTableUpgrade>()
            .AddSingleton<ISchemaUpgrade, FoodServingCheckedUpgrade>();

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IFoodSearch, FoodSearch>()
            .AddSingleton(provider => new FoodService(provider.GetRequiredService<FoodStore>(), provider.GetRequiredService<ILogger<FoodService>>()))
            .AddSingleton(provider => new TargetService(provider.GetRequiredService<TargetStore>(), provider.GetRequiredService<ILogger<TargetService>>()))
            .AddSingleton(provider => new DiaryService(provider.GetRequiredService<DiaryStore>(), provider.GetRequiredService<ILogger<DiaryService>>()))
            .AddSingleton(provider => new DaySummaryService(provider.GetRequiredService<DaySummaryStore>(), provider.GetRequiredService<ILogger<DaySummaryService>>()))
            .AddSingleton<ISeedLoader>(provider =>
                ActivatorUtilities.CreateInstance<SeedLoader>(provider, (Func<Stream>)SeedLoader.OpenBundledSeed))
            .AddSingleton<SeedProgress>()
            .AddHttpClient<OpenFoodFactsLookup>(client =>
            {
                client.BaseAddress = new Uri("https://world.openfoodfacts.org/");
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(OpenFoodFactsLookup.UserAgent);
            })
            // The singleton lookup keeps this client for the app's life, so connections must still be recycled to follow DNS changes.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
        return services;
    }
}
