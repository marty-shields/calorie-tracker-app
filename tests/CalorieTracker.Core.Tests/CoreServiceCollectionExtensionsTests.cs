using CalorieTracker.Core.Data;
using CalorieTracker.Core.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;

namespace CalorieTracker.Core.Tests;

public class CoreServiceCollectionExtensionsTests
{
    [Test]
    public void AddCore_RegistersSingletons()
    {
        var services = new ServiceCollection().AddCore("calorietracker.db3");

        Assert.That(services.Select(s => (s.ServiceType, s.ImplementationType, s.Lifetime)), Is.EquivalentTo(new[]
        {
            (typeof(DatabaseGate), typeof(DatabaseGate), ServiceLifetime.Singleton),
            (typeof(FoodStore), (Type?)null, ServiceLifetime.Singleton),
            (typeof(IDatabaseInitializer), (Type?)null, ServiceLifetime.Singleton),
            (typeof(TimeProvider), (Type?)null, ServiceLifetime.Singleton),
            (typeof(ISeedLoader), (Type?)null, ServiceLifetime.Singleton),
        }));
    }

    [Test]
    public void AddCore_WithLogging_ResolvesTheGatedInitializer()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(TempDatabase.NewPath())
            .BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IDatabaseInitializer>(), Is.InstanceOf<GatedDatabaseInitializer>());
    }

    [Test]
    public void AddCore_WithLogging_ResolvesOneFoodStore()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(TempDatabase.NewPath())
            .BuildServiceProvider();

        var first = provider.GetRequiredService<FoodStore>();
        var second = provider.GetRequiredService<FoodStore>();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public async Task AddCore_WithLogging_ResolvesASeedLoaderThatLoadsTheBundledSeed()
    {
        var path = TempDatabase.NewPath();
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(path)
            .BuildServiceProvider();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

        var result = await provider.GetRequiredService<ISeedLoader>().LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SeedLoadStatus.Loaded));
            Assert.That(result.Inserted, Is.EqualTo(2853));
        });
        File.Delete(path);
    }

    [Test]
    public void AddCore_UngatedDatabaseTypesCannotBeResolved()
    {
        using var provider = new ServiceCollection().AddCore(TempDatabase.NewPath()).BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetService<SQLiteAsyncConnection>(), Is.Null);
            Assert.That(provider.GetService<DatabaseInitializer>(), Is.Null);
        });
    }

    [Test]
    public async Task AddCore_InitializerUsesTheDatabasePath()
    {
        var path = TempDatabase.NewPath();
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(path)
            .BuildServiceProvider();

        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();

        var check = new SQLiteAsyncConnection(path);
        try
        {
            Assert.That(await check.ExecuteScalarAsync<int>("PRAGMA user_version"), Is.EqualTo(DatabaseInitializer.CurrentVersion));
        }
        finally
        {
            await check.CloseAsync();
            File.Delete(path);
        }
    }

    [Test]
    public void AddCore_TimeIsTheSystemClock()
    {
        var time = new ServiceCollection().AddCore("calorietracker.db3")
            .Single(s => s.ServiceType == typeof(TimeProvider)).ImplementationInstance;

        Assert.That(time, Is.SameAs(TimeProvider.System));
    }
}
