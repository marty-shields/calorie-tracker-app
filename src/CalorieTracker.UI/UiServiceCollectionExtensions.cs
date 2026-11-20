using Microsoft.Extensions.DependencyInjection;
using Shields.UI;

namespace CalorieTracker.UI;

public static class UiServiceCollectionExtensions
{
    /// <summary>Registers the UI's services and the kit's (<see cref="ShieldsUiServiceCollectionExtensions.AddShieldsUi"/>). The kit adds the scoped back button, app lifecycle (with <c>IAppStart</c> and <c>IAppLifecycle</c>) and <c>SnackbarService</c>; the host resolves the first two from the BlazorWebView's scope. The host must register Core's services, logging and an <see cref="Core.Services.IAppEnvironment"/>.</summary>
    public static IServiceCollection AddUi(this IServiceCollection services) =>
        services.AddShieldsUi();
}
