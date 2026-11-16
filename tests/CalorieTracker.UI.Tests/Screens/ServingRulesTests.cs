using CalorieTracker.Core.Data;
using CalorieTracker.UI.Models.Serving;

namespace CalorieTracker.UI.Tests.Screens;

public class ServingRulesTests
{
    private static readonly Serving Glass = new() { Id = 1, Label = "1 glass (250 ml)", Amount = 250, Source = ServingSource.Product };
    private static readonly Serving Can = new() { Id = 2, Label = "Can 330 ml", Amount = 330, Source = ServingSource.Pack };
    private static readonly Serving Bottle = new() { Id = 3, Label = "Bottle 1 L", Amount = 1000, Source = ServingSource.Pack };
    private static readonly Serving Mug = new() { Id = 4, Label = "My mug", Amount = 200, Source = ServingSource.User };

    [Test]
    public void Pick_TheLastLoggedServing_WinsOverThePackAndTheSmallest()
    {
        Assert.That(ServingRules.Pick([Glass, Can, Bottle], 3, 330)!.Id, Is.EqualTo(3));
    }

    [Test]
    public void Pick_NoLoggedServing_TakesTheServingOfTheScannedPack()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRules.Pick([Glass, Can, Bottle], null, 1000)!.Id, Is.EqualTo(3));
            Assert.That(ServingRules.Pick([Glass, Can, Bottle], 99, 330)!.Id, Is.EqualTo(2), "an id the food does not have counts as none");
        });
    }

    [Test]
    public void Pick_NoLoggedServingAndNoServingOfThePack_TakesTheSmallest()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRules.Pick([Glass, Can, Mug], null, 2000)!.Id, Is.EqualTo(4));
            Assert.That(ServingRules.Pick([Bottle, Can, Glass], null)!.Id, Is.EqualTo(1));
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
    public void Pick_NoServings_IsNull() => Assert.That(ServingRules.Pick([], 1, 330), Is.Null);
}
