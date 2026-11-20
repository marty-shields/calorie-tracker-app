using CalorieTracker.UI.Lifecycle;
using CalorieTracker.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shields.UI.Components.Feedback;
using Shields.UI.Theme;

namespace CalorieTracker.UI.Tests;

public class UiServiceCollectionExtensionsTests
{
    [Test]
    public void AddUi_RegistersTheServices()
    {
        var services = new ServiceCollection().AddUi();

        Assert.That(services.Where(s => s.ServiceType.Namespace!.StartsWith("CalorieTracker")).Select(s => (s.ServiceType, s.ImplementationType, s.Lifetime)), Is.EquivalentTo(new[]
        {
            (typeof(IBackButton), typeof(BackButton), ServiceLifetime.Singleton),
            (typeof(AppLifecycle), typeof(AppLifecycle), ServiceLifetime.Singleton),
            (typeof(IAppStart), (Type?)null, ServiceLifetime.Singleton),
            (typeof(IAppLifecycle), (Type?)null, ServiceLifetime.Singleton),
        }));
    }

    [Test]
    public void AddUi_RegistersTheKitsOwnServicesAlongsideTheApps()
    {
        var services = new ServiceCollection().AddUi();

        Assert.That(services.Where(s => s.ServiceType.Namespace!.StartsWith("Shields.UI")).Select(s => (s.ServiceType, s.Lifetime)), Is.EquivalentTo(new[]
        {
            (typeof(Shields.UI.Navigation.IBackButton), ServiceLifetime.Scoped),
            (typeof(Shields.UI.Lifecycle.AppLifecycle), ServiceLifetime.Scoped),
            (typeof(Shields.UI.Lifecycle.IAppStart), ServiceLifetime.Scoped),
            (typeof(Shields.UI.Lifecycle.IAppLifecycle), ServiceLifetime.Scoped),
            (typeof(SnackbarService), ServiceLifetime.Scoped),
            (typeof(ThemeService), ServiceLifetime.Scoped),
            (typeof(SystemBarInsetWriter), ServiceLifetime.Scoped),
        }));
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
    public void AddUi_AppStartAndAppLifecycleAreOneInstance()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddUi()
            .BuildServiceProvider();

        var lifecycle = provider.GetRequiredService<AppLifecycle>();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<IAppStart>(), Is.SameAs(lifecycle));
            Assert.That(provider.GetRequiredService<IAppLifecycle>(), Is.SameAs(lifecycle));
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
