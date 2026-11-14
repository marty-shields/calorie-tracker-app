using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

public class BarcodeLookupResultTests
{
    [Test]
    public void Found_CarriesTheFood()
    {
        var food = new Food();

        var result = BarcodeLookupResult.Found(food);

        Assert.That(result.Status, Is.EqualTo(BarcodeLookupStatus.Found));
        Assert.That(result.Food, Is.SameAs(food));
    }

    [Test]
    public void Found_NullFood_Throws() =>
        Assert.That(() => BarcodeLookupResult.Found(null!), Throws.ArgumentNullException);

    [TestCase(BarcodeLookupStatus.NotFound)]
    [TestCase(BarcodeLookupStatus.Offline)]
    [TestCase(BarcodeLookupStatus.RateLimited)]
    public void OtherStatuses_HaveNoFood(BarcodeLookupStatus status)
    {
        var result = status switch
        {
            BarcodeLookupStatus.NotFound => BarcodeLookupResult.NotFound(),
            BarcodeLookupStatus.Offline => BarcodeLookupResult.Offline(),
            _ => BarcodeLookupResult.RateLimited(),
        };

        Assert.That(result.Status, Is.EqualTo(status));
        Assert.That(result.Food, Is.Null);
    }
}
