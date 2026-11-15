using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Lifecycle;
using CalorieTracker.UI.Navigation;
using CalorieTracker.UI.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace CalorieTracker.UI;

public static class UiServiceCollectionExtensions
{
    /// <summary>Registers the UI's services. The host must register Core's services, logging and an <see cref="Core.Services.IAppEnvironment"/>.</summary>
    public static IServiceCollection AddUi(this IServiceCollection services) =>
        services
            .AddSingleton<IBackButton, BackButton>()
            .AddSingleton<AppLifecycle>()
            .AddSingleton<IAppStart>(s => s.GetRequiredService<AppLifecycle>())
            .AddSingleton<IAppLifecycle>(s => s.GetRequiredService<AppLifecycle>())
            .AddSingleton<SnackbarService>()
            .AddScoped<ThemeService>()
            .AddScoped<SystemBarInsetWriter>();
}
