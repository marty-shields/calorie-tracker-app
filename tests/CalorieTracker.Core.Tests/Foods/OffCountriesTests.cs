using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

public class OffCountriesTests
{
    [Test]
    public void Gb_MapsToTheUnitedKingdomTag_ThoughOpenFoodFactsWritesUk()
    {
        var found = OffCountries.TryGet("GB", out var tag, out var name);

        Assert.Multiple(() =>
        {
            Assert.That(found);
            Assert.That(tag, Is.EqualTo("en:united-kingdom"));
            Assert.That(name, Is.EqualTo("United Kingdom"));
        });
    }

    [TestCase("gb")]
    [TestCase("Gb")]
    public void ACodeInAnyCase_IsFound(string code) =>
        Assert.That(OffCountries.TryGet(code, out var tag, out _) && tag == "en:united-kingdom");

    [TestCase("FR", "en:france")]
    [TestCase("DE", "en:germany")]
    [TestCase("US", "en:united-states")]
    [TestCase("IE", "en:ireland")]
    public void OrdinaryCodes_MapToTheirTags(string code, string expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(OffCountries.TryGet(code, out var tag, out _));
            Assert.That(tag, Is.EqualTo(expected));
        });
    }

    [TestCase("ZZ")]
    [TestCase("UK")]
    [TestCase("XX")]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("G")]
    [TestCase("GBR")]
    [TestCase(null)]
    public void AnUnmappedCodeOrNoRegion_IsNotFound_SoTheSearchGoesWorldwide(string? code)
    {
        var found = OffCountries.TryGet(code, out var tag, out var name);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.False);
            Assert.That(tag, Is.Empty);
            Assert.That(name, Is.Empty);
        });
    }

    [Test]
    public void TheTable_HasAUniqueTwoLetterCodeAndAnEnglishTagForEveryEntry()
    {
        var codes = new List<string>();
        for (var first = 'A'; first <= 'Z'; first++)
        {
            for (var second = 'A'; second <= 'Z'; second++)
            {
                var code = $"{first}{second}";
                if (OffCountries.TryGet(code, out var tag, out var name))
                {
                    codes.Add(code);
                    Assert.That(tag, Does.StartWith("en:"), code);
                    Assert.That(name, Is.Not.Empty, code);
                }
            }
        }

        // 252 countries have a code in the Open Food Facts list of 16 Nov 2026; "world" is not two letters.
        Assert.That(codes, Has.Count.EqualTo(251));
    }

    [Test]
    public void TheDeviceRegion_IsATwoLetterCodeOrNull_AndNeverThrows()
    {
        var code = OffCountries.DeviceRegionCode();

        Assert.That(code, Is.Null.Or.Length.EqualTo(2));
    }
}
