using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

public class BarcodeNormaliserTests
{
    [TestCase("123456789012", "0123456789012")]
    [TestCase("12345670", "12345670")]
    [TestCase("1234567890123", "1234567890123")]
    [TestCase("5000 1234-5678 9", "5000123456789")]
    public void TryNormalise_ValidLength_ReturnsTheNormalisedBarcode(string input, string expected)
    {
        Assert.That(BarcodeNormaliser.TryNormalise(input, out var barcode), Is.True);
        Assert.That(barcode, Is.EqualTo(expected));
    }

    [TestCase("1234567")]
    [TestCase("123456789")]
    [TestCase("12345678901234")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("abc")]
    [TestCase("١٢٣٤٥٦٧٨")]
    public void TryNormalise_OtherLength_IsInvalid(string? input)
    {
        Assert.That(BarcodeNormaliser.TryNormalise(input, out var barcode), Is.False);
        Assert.That(barcode, Is.Empty);
    }
}
