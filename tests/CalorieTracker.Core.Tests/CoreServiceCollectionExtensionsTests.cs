using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.Seed;
using CalorieTracker.Core.Targets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;

namespace CalorieTracker.Core.Tests;

public class CoreServiceCollectionExtensionsTests
{
    [Test]
    public void AddCore_RegistersSingletonsAndTheBarcodeLookupDecorator()
    {
        var services = new ServiceCollection().AddCore("calorietracker.db3");

        Assert.That(
            services.Where(s => s.ServiceType.Namespace?.StartsWith("Microsoft.Extensions.Http") != true
                    && s.ServiceType.Namespace?.StartsWith("Microsoft.Extensions.Options") != true
                    && s.ServiceType.Namespace?.StartsWith("Microsoft.Extensions.Logging") != true
                    && s.ServiceType != typeof(IHttpClientFactory) && s.ServiceType != typeof(IHttpMessageHandlerFactory))
                .Select(s => (s.ServiceType, s.ImplementationType, s.Lifetime)),
            Is.SupersetOf(new[]
            {
                (typeof(DatabaseGate), typeof(DatabaseGate), ServiceLifetime.Singleton),
                (typeof(FoodStore), (Type?)null, ServiceLifetime.Singleton),
                (typeof(IDatabaseInitializer), (Type?)null, ServiceLifetime.Singleton),
                (typeof(TimeProvider), (Type?)null, ServiceLifetime.Singleton),
                (typeof(IBarcodeLookup), (Type?)null, ServiceLifetime.Singleton),
                (typeof(OpenFoodFactsLookup), (Type?)null, ServiceLifetime.Transient),
                (typeof(ISeedLoader), (Type?)null, ServiceLifetime.Singleton),
                (typeof(IFoodSearch), typeof(FoodSearch), ServiceLifetime.Singleton),
                (typeof(TargetService), (Type?)null, ServiceLifetime.Singleton),
                (typeof(TargetStore), (Type?)null, ServiceLifetime.Singleton),
                (typeof(SeedProgress), typeof(SeedProgress), ServiceLifetime.Singleton),
            }));
    }

    [Test]
    public void AddCore_WithLogging_ResolvesTheStoredBarcodeLookup()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(TempDatabase.NewPath())
            .BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IBarcodeLookup>(), Is.InstanceOf<StoredBarcodeLookup>());
    }

    [Test]
    public void AddCore_WithLogging_ResolvesOneBarcodeLookup()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(TempDatabase.NewPath())
            .BuildServiceProvider();

        var first = provider.GetRequiredService<IBarcodeLookup>();
        var second = provider.GetRequiredService<IBarcodeLookup>();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public async Task AddCore_BarcodeLookupWrapsTheOpenFoodFactsAdapterAndStoresWhatItFinds()
    {
        var path = TempDatabase.NewPath();
        var handler = new CapturingHandler(System.Net.HttpStatusCode.OK,
            "{\"code\":\"1\",\"product\":{\"product_name\":\"Oats\",\"serving_size\":\"1 portion (50 g)\",\"serving_quantity\":50,\"nutriments\":{\"energy-kcal_100g\":100,\"proteins_100g\":1,\"carbohydrates_100g\":2,\"fat_100g\":3}},\"status\":1}");
        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(path);
        services.AddHttpClient(nameof(OpenFoodFactsLookup)).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
        var lookup = provider.GetRequiredService<IBarcodeLookup>();

        var first = await lookup.LookupAsync("3017620422003");
        var second = await lookup.LookupAsync("3017620422003");

        Assert.Multiple(() =>
        {
            Assert.That(first.Status, Is.EqualTo(BarcodeLookupStatus.Found));
            Assert.That(second.Status, Is.EqualTo(BarcodeLookupStatus.Found));
            Assert.That(handler.Calls, Is.EqualTo(1));
            Assert.That(handler.Uri!.GetLeftPart(UriPartial.Path), Is.EqualTo("https://world.openfoodfacts.org/api/v2/product/3017620422003"));
        });
    }

    [Test]
    public async Task AddCore_BarcodeLookupClientHasTheBaseAddressUserAgentAndTimeout()
    {
        var handler = new CapturingHandler();
        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(TempDatabase.NewPath());
        services.AddHttpClient(nameof(OpenFoodFactsLookup)).ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IBarcodeLookup>().LookupAsync("3017620422003");

        var assembly = typeof(OpenFoodFactsLookup).Assembly.GetName();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(OpenFoodFactsLookup));
        Assert.Multiple(() =>
        {
            Assert.That(handler.Uri!.GetLeftPart(UriPartial.Path), Is.EqualTo("https://world.openfoodfacts.org/api/v2/product/3017620422003"));
            Assert.That(handler.UserAgent, Does.StartWith(assembly.Name + "/" + assembly.Version!.Major + "."));
            Assert.That(handler.UserAgent, Does.EndWith("(https://github.com/marty-shields/calorie-tracker-app)"));
            Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://world.openfoodfacts.org/")));
            Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        });
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
            Assert.That(result.Inserted, Is.EqualTo(2174));
        });
        File.Delete(path);
    }

    [Test]
    public void AddCore_WithLogging_ResolvesOneFoodSearch()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddCore(TempDatabase.NewPath())
            .BuildServiceProvider();

        var first = provider.GetRequiredService<IFoodSearch>();

        Assert.That(first, Is.InstanceOf<FoodSearch>().And.SameAs(provider.GetRequiredService<IFoodSearch>()));
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

    private sealed class CapturingHandler(System.Net.HttpStatusCode status = System.Net.HttpStatusCode.NotFound, string body = "") : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public Uri? Uri { get; private set; }

        public string? UserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Uri = request.RequestUri;
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
