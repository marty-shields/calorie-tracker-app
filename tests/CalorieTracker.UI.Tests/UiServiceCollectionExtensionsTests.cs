using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Lifecycle;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Theme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CalorieTracker.UI.Tests;

public class UiServiceCollectionExtensionsTests
{
    [Test]
    public void AddUi_RegistersTheServices()
    {
        var services = new ServiceCollection().AddUi();

        Assert.That(services.Select(s => (s.ServiceType, s.ImplementationType, s.Lifetime)), Is.EquivalentTo(new[]
        {
            (typeof(IBackButton), typeof(BackButton), ServiceLifetime.Singleton),
            (typeof(AppLifecycle), typeof(AppLifecycle), ServiceLifetime.Singleton),
            (typeof(IAppStart), (Type?)null, ServiceLifetime.Singleton),
            (typeof(IAppLifecycle), (Type?)null, ServiceLifetime.Singleton),
            (typeof(SnackbarService), typeof(SnackbarService), ServiceLifetime.Singleton),
            (typeof(ThemeService), typeof(ThemeService), ServiceLifetime.Scoped),
            (typeof(SystemBarInsetWriter), typeof(SystemBarInsetWriter), ServiceLifetime.Scoped),
        }));
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
}
