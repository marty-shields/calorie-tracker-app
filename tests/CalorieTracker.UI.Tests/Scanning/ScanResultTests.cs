using CalorieTracker.UI.Scanning;

namespace CalorieTracker.UI.Tests.Scanning;

public class ScanResultTests
{
    [Test]
    public void Scanned_CarriesTheDigits() =>
        Assert.That(ScanResult.Scanned("5000112637922"), Is.EqualTo(new ScanResult(ScanStatus.Scanned, "5000112637922")));

    [TestCase(ScanStatus.Cancelled)]
    [TestCase(ScanStatus.PermissionDenied)]
    [TestCase(ScanStatus.Unavailable)]
    public void OtherStatuses_CarryNoDigits(ScanStatus status)
    {
        var result = status switch
        {
            ScanStatus.Cancelled => ScanResult.Cancelled,
            ScanStatus.PermissionDenied => ScanResult.PermissionDenied,
            _ => ScanResult.Unavailable,
        };

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Digits, Is.Null);
        });
    }
}
