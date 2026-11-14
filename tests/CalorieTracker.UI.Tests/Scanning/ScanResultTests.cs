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

    [Test]
    public void FromRawValues_TakesTheFirstNonEmpty() =>
        Assert.That(ScanResult.FromRawValues([null, "", "12345670", "5000112637922"], _ => true), Is.EqualTo(ScanResult.Scanned("12345670")));

    [Test]
    public void FromRawValues_SkipsValuesNotAccepted() =>
        Assert.That(ScanResult.FromRawValues(["123", "12345670"], v => v.Length == 8), Is.EqualTo(ScanResult.Scanned("12345670")));

    [Test]
    public void FromRawValues_OffersOnlyNonEmptyValues()
    {
        var offered = new List<string>();

        ScanResult.FromRawValues([null, "", "123"], v =>
        {
            offered.Add(v);
            return false;
        });

        Assert.That(offered, Is.EqualTo(new[] { "123" }));
    }

    [TestCase]
    [TestCase(null, "")]
    public void FromRawValues_WithNoValue_GivesNoResult(params string?[] values) =>
        Assert.That(ScanResult.FromRawValues(values, _ => true), Is.Null);

    [Test]
    public void FromRawValues_WithNothingAccepted_GivesNoResult() =>
        Assert.That(ScanResult.FromRawValues(["123"], _ => false), Is.Null);

    [Test]
    public void FromRawValues_KeepsLeadingZeros() =>
        Assert.That(ScanResult.FromRawValues(["0012345678905"], _ => true)!.Digits, Is.EqualTo("0012345678905"));
}
