using CalorieTracker.UI.Components.Feedback;
using CalorieTracker.UI.Lifecycle;
using CalorieTracker.UI.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Shields.UI;

namespace CalorieTracker.UI;

public static class UiServiceCollectionExtensions
{
    /// <summary>Registers the UI's services and the kit's (<see cref="ShieldsUiServiceCollectionExtensions.AddShieldsUi"/>). The kit also adds its own scoped back button, app lifecycle (with <c>IAppStart</c> and <c>IAppLifecycle</c>) and snackbar service, which are different types from the app's and unused until the app's copies are removed. The host must register Core's services, logging and an <see cref="Core.Services.IAppEnvironment"/>.</summary>
    public static IServiceCollection AddUi(this IServiceCollection services) =>
        services
            .AddSingleton<IBackButton, BackButton>()
            .AddSingleton<AppLifecycle>()
            .AddSingleton<IAppStart>(s => s.GetRequiredService<AppLifecycle>())
            .AddSingleton<IAppLifecycle>(s => s.GetRequiredService<AppLifecycle>())
            .AddSingleton<SnackbarService>()
            .AddShieldsUi();
}
