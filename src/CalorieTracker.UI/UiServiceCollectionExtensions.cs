using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Lifecycle;
using CalorieTracker.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Shields.UI;

namespace CalorieTracker.UI;

public static class UiServiceCollectionExtensions
{
    /// <summary>Registers the UI's services and the kit's (<see cref="ShieldsUiServiceCollectionExtensions.AddShieldsUi"/>, after the app's own so the kit leaves them). The host must register Core's services, logging and an <see cref="Core.Services.IAppEnvironment"/>.</summary>
    public static IServiceCollection AddUi(this IServiceCollection services) =>
        services
            .AddSingleton<IBackButton, BackButton>()
            .AddSingleton<AppLifecycle>()
            .AddSingleton<IAppStart>(s => s.GetRequiredService<AppLifecycle>())
            .AddSingleton<IAppLifecycle>(s => s.GetRequiredService<AppLifecycle>())
            .AddSingleton<SnackbarService>()
            .AddShieldsUi();
}
