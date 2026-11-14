using CalorieTracker.UI.Scanning;

namespace CalorieTracker.UI.Tests.Scanning;

public class ScanResultTests
{
    [Test]
    public void Scanned_CarriesTheDigits() =>
        Assert.That(ScanResult.Scanned("5000112637922"), Is.EqualTo(new ScanResult(ScanStatus.Scanned, "5000112637922")));

    [TestCase(ScanStatus.Cancelled)]
    [TestCase(ScanStatus.PermissionDenied)]
    public void OtherStatuses_CarryNoDigits(ScanStatus status)
    {
        var result = status == ScanStatus.Cancelled ? ScanResult.Cancelled : ScanResult.PermissionDenied;

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Digits, Is.Null);
        });
    }
}
