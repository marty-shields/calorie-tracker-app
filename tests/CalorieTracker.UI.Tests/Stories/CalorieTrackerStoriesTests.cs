using CalorieTracker.UI.Stories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shields.UI.Components.Feedback;

namespace CalorieTracker.UI.Tests.Stories;

public class CalorieTrackerStoriesTests
{
    [Test]
    public void AddCalorieTrackerStories_RegistersTheUiAndASystemClock()
    {
        using var provider = new ServiceCollection().AddCalorieTrackerStories().BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetService<SnackbarService>(), Is.Not.Null, "AddUi was not called");
            Assert.That(provider.GetRequiredService<TimeProvider>(), Is.SameAs(TimeProvider.System));
        });
    }

    [Test]
    public void AddCalorieTrackerStories_KeepsTheHostsTimeProvider()
    {
        var clock = new FakeTimeProvider();
        using var provider = new ServiceCollection().AddSingleton<TimeProvider>(clock).AddCalorieTrackerStories().BuildServiceProvider();

        Assert.That(provider.GetRequiredService<TimeProvider>(), Is.SameAs(clock));
    }
}
