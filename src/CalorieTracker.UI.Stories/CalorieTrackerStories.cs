using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CalorieTracker.UI.Stories;

public static class CalorieTrackerStories
{
    /// <summary>Registers what this app's stories need; a host that has its own <see cref="TimeProvider"/> keeps it.</summary>
    public static IServiceCollection AddCalorieTrackerStories(this IServiceCollection services)
    {
        services.AddUi();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
