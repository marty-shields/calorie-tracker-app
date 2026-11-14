using CalorieTracker.Core.Logging;

namespace CalorieTracker.Core.Tests.Logging;

public class CoreEventTests
{
    [Test]
    public void Values_AreUnique() =>
        Assert.That(Enum.GetValues<CoreEvent>().Select(e => (int)e), Is.Unique);

    [Test]
    public void Values_AreInCoresRange() =>
        Assert.That(Enum.GetValues<CoreEvent>().Select(e => (int)e), Is.All.InRange(1000, 1999));

    [Test]
    public void Values_AreFixed() =>
        Assert.That(Enum.GetValues<CoreEvent>().ToDictionary(e => e, e => (int)e), Is.EqualTo(new Dictionary<CoreEvent, int>
        {
            [CoreEvent.SchemaVersion] = 1001,
            [CoreEvent.DatabaseCreated] = 1002,
            [CoreEvent.SchemaStepApplied] = 1003,
            [CoreEvent.FoodStoreBlock] = 1100,
            [CoreEvent.FoodInserted] = 1101,
            [CoreEvent.FoodUpdated] = 1102,
            [CoreEvent.OpenFoodFactsAdapterBlock] = 1200,
            [CoreEvent.LookupAttempted] = 1201,
            [CoreEvent.LookupFound] = 1202,
            [CoreEvent.LookupNotFound] = 1203,
            [CoreEvent.LookupRetryWaiting] = 1204,
            [CoreEvent.LookupRateLimited] = 1205,
            [CoreEvent.LookupNoResponse] = 1206,
            [CoreEvent.LookupMalformedResponse] = 1207,
            [CoreEvent.LookupUnexpectedStatus] = 1208,
            [CoreEvent.StoredLookupBlock] = 1300,
            [CoreEvent.StoredLookupStoreHit] = 1301,
            [CoreEvent.StoredLookupFetched] = 1302,
            [CoreEvent.StoredLookupNotFoundRemembered] = 1303,
            [CoreEvent.StoredLookupNotFoundHit] = 1304,
            [CoreEvent.StoredLookupReadFailed] = 1305,
            [CoreEvent.StoredLookupSaveFailed] = 1306,
            [CoreEvent.SeedLoaderBlock] = 1400,
            [CoreEvent.SeedLoaded] = 1401,
            [CoreEvent.SeedUpToDate] = 1402,
            [CoreEvent.SeedLoadFailed] = 1403,
            [CoreEvent.FoodSearchBlock] = 1500,
            [CoreEvent.FoodSearched] = 1501,
            [CoreEvent.FoodSearchFailed] = 1502,
        }));
}
