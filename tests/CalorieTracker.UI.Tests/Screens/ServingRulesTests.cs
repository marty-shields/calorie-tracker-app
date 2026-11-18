using CalorieTracker.Core.Data;
using CalorieTracker.UI.Models.Serving;

namespace CalorieTracker.UI.Tests.Screens;

public class ServingRulesTests
{
    private static readonly Serving Glass = new() { Id = 1, Label = "1 glass (250 ml)", Amount = 250, Source = ServingSource.User };
    private static readonly Serving Can = new() { Id = 2, Label = "My can (330 ml)", Amount = 330, Source = ServingSource.User };
    private static readonly Serving Bottle = new() { Id = 3, Label = "My bottle (1 L)", Amount = 1000, Source = ServingSource.User };
    private static readonly Serving Mug = new() { Id = 4, Label = "My mug", Amount = 200, Source = ServingSource.User };

    [Test]
    public void Pick_TheLastLoggedServing_WinsOverTheSmallest()
    {
        Assert.That(ServingRules.Pick([Mug, Glass, Can, Bottle], 3)!.Id, Is.EqualTo(3));
    }

    [Test]
    public void Pick_NoLoggedServingOrOneTheFoodDoesNotHave_TakesTheSmallest()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRules.Pick([Glass, Can, Mug], null)!.Id, Is.EqualTo(4));
            Assert.That(ServingRules.Pick([Bottle, Can, Glass], 99)!.Id, Is.EqualTo(1));
            Assert.That(ServingRules.Pick([Can], null)!.Id, Is.EqualTo(2));
        });
    }

    [Test]
    public void Pick_TwoServingsAsSmall_TakesTheFirstListed()
    {
        var twin = new Serving { Id = 9, Label = "Twin", Amount = 250, Source = ServingSource.User };

        Assert.That(ServingRules.Pick([Glass, twin], null)!.Id, Is.EqualTo(1));
    }

    [Test]
    public void Pick_NoServings_IsNull() => Assert.That(ServingRules.Pick([], 1), Is.Null);
}
