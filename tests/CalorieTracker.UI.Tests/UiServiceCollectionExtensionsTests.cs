using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shields.UI.Components.Feedback;
using Shields.UI.Lifecycle;
using Shields.UI.Navigation;
using Shields.UI.Theme;

namespace CalorieTracker.UI.Tests;

public class UiServiceCollectionExtensionsTests
{
    [Test]
    public void AddUi_RegistersNoBackButtonOrLifecycleOfItsOwn() =>
        Assert.That(new ServiceCollection().AddUi().Where(s => s.ServiceType.Namespace!.StartsWith("CalorieTracker")), Is.Empty);

    [Test]
    public void AddUi_RegistersTheKitsServices()
    {
        var services = new ServiceCollection().AddUi();

        Assert.That(services.Where(s => s.ServiceType.Namespace!.StartsWith("Shields.UI")).Select(s => (s.ServiceType, s.Lifetime)), Is.EquivalentTo(new[]
        {
            (typeof(IBackButton), ServiceLifetime.Scoped),
            (typeof(AppLifecycle), ServiceLifetime.Scoped),
            (typeof(IAppStart), ServiceLifetime.Scoped),
            (typeof(IAppLifecycle), ServiceLifetime.Scoped),
            (typeof(SnackbarService), ServiceLifetime.Scoped),
            (typeof(ThemeService), ServiceLifetime.Scoped),
            (typeof(SystemBarInsetWriter), ServiceLifetime.Scoped),
            (typeof(BackButtonBridge), ServiceLifetime.Singleton),
            (typeof(AppLifecycleBridge), ServiceLifetime.Singleton),
        }));
    }

    [Test]
    public void AddUi_RegistersTheKitsHostBridgesAsSingletonsForTheActivity()
    {
        using var provider = new ServiceCollection().AddUi().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.Multiple(() =>
        {
            Assert.That(scope.ServiceProvider.GetRequiredService<BackButtonBridge>(), Is.SameAs(provider.GetRequiredService<BackButtonBridge>()));
            Assert.That(scope.ServiceProvider.GetRequiredService<AppLifecycleBridge>(), Is.SameAs(provider.GetRequiredService<AppLifecycleBridge>()));
        });
    }

    [Test]
    public void AddUi_RegistersTheKitsThemeServiceAndInsetWriterOnce()
    {
        var services = new ServiceCollection().AddUi();

        Assert.Multiple(() =>
        {
            Assert.That(services.Where(s => s.ServiceType == typeof(ThemeService)).Select(s => s.Lifetime), Is.EqualTo(new[] { ServiceLifetime.Scoped }));
            Assert.That(services.Where(s => s.ServiceType == typeof(SystemBarInsetWriter)).Select(s => s.Lifetime), Is.EqualTo(new[] { ServiceLifetime.Scoped }));
        });
    }

    [Test]
    public void AddUi_BackButtonAndAppLifecycleAreOnePerScopeAndNotFromTheRoot()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddUi()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var lifecycle = first.ServiceProvider.GetRequiredService<AppLifecycle>();
        var back = first.ServiceProvider.GetRequiredService<IBackButton>();

        Assert.Multiple(() =>
        {
            Assert.That(first.ServiceProvider.GetRequiredService<IAppStart>(), Is.SameAs(lifecycle));
            Assert.That(first.ServiceProvider.GetRequiredService<IAppLifecycle>(), Is.SameAs(lifecycle));
            Assert.That(first.ServiceProvider.GetRequiredService<IBackButton>(), Is.SameAs(back));
            Assert.That(second.ServiceProvider.GetRequiredService<IBackButton>(), Is.Not.SameAs(back));
            Assert.That(() => provider.GetRequiredService<IBackButton>(), Throws.InvalidOperationException);
            Assert.That(() => provider.GetRequiredService<IAppLifecycle>(), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void AddUi_SnackbarServiceIsOnePerScopeAndNotFromTheRoot()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddUi()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var inFirst = first.ServiceProvider.GetRequiredService<SnackbarService>();

        Assert.Multiple(() =>
        {
            Assert.That(first.ServiceProvider.GetRequiredService<SnackbarService>(), Is.SameAs(inFirst));
            Assert.That(second.ServiceProvider.GetRequiredService<SnackbarService>(), Is.Not.SameAs(inFirst));
            Assert.That(() => provider.GetRequiredService<SnackbarService>(), Throws.InvalidOperationException);
        });
    }
}
