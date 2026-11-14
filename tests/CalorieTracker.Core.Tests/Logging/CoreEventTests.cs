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
        }));
}
