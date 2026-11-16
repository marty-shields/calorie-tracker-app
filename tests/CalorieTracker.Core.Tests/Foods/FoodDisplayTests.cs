using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

public class FoodDisplayTests
{
    [TestCase("Original", "Weetabix", "Weetabix", "430 gram", "Weetabix Original", null, "430 gram")]
    [TestCase("Nutella", "Ferrero", "Ferrero, Nutella", "400 g e", "Nutella", "Ferrero", "400 g e")]
    [TestCase("Mars", "Mars", "Mars, Mars Wrigley", "51 g", "Mars", "Mars Wrigley", "51 g")]
    [TestCase("Coca Cola", "Coca-Cola", "Coca-Cola", "330 ml", "Coca Cola", null, "330 ml")]
    [TestCase("Pasteurised whole milk", null, null, null, "Pasteurised whole milk", null, null)]
    public void Of_TheIssueTable_GivesTheTitleBrandLineAndPack(string name, string? brand, string? brands, string? pack, string title, string? line, string? expectedPack)
    {
        var display = FoodDisplay.Of(name, brand, brands, pack);

        Assert.That((display.Title, display.BrandLine, display.Pack), Is.EqualTo((title, line, expectedPack)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(" , ")]
    public void Of_BlankOrMissingBrand_ShowsTheNameAloneWithNoSeparator(string? brand)
    {
        var display = FoodDisplay.Of("Original", brand, brand, null);

        Assert.That((display.Title, display.BrandLine, display.Detail), Is.EqualTo(("Original", (string?)null, (string?)null)));
    }

    [Test]
    public void Of_BrandContainedWithAccentsCaseAndHyphens_IsNotRepeated()
    {
        var display = FoodDisplay.Of("CRÈME  de la crème", null, "Creme-De-La-Creme", null);

        Assert.That((display.Title, display.BrandLine), Is.EqualTo(("CRÈME  de la crème", (string?)null)));
    }

    [TestCase("Goat Milk", "Oat", "Oat Goat Milk", null)]
    [TestCase("Coat", "Oat", "Oat Coat", null)]
    [TestCase("Oat Milk", "Oat", "Oat Milk", null)]
    [TestCase("Creamy oat drink", "OAT", "Creamy oat drink", null)]
    [TestCase("Cocacola Zero", "Coca-Cola", "Cocacola Zero", null)]
    [TestCase("Cola Zero", "Coca-Cola", "Coca-Cola Cola Zero", null)]
    public void Of_BrandMatchesWholeWordsOnly_NotPartOfAWord(string name, string brand, string title, string? line)
    {
        var display = FoodDisplay.Of(name, brand, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(display.Title, Is.EqualTo(title));
            Assert.That(display.BrandLine, Is.EqualTo(line));
        });
    }

    [Test]
    public void Of_NoBrandsListed_UsesTheSingleBrand()
    {
        var display = FoodDisplay.Of("Oat bar", "Acme", null, null);

        Assert.That(display.Title, Is.EqualTo("Acme Oat bar"));
    }

    [Test]
    public void Of_NameMissingEveryBrand_PutsTheFirstInTheTitleAndTheNextOnTheLine()
    {
        var display = FoodDisplay.Of("Spread", "Ferrero", "Ferrero, Nutella", null);

        Assert.That((display.Title, display.BrandLine), Is.EqualTo(("Ferrero Spread", "Nutella")));
    }

    [Test]
    public void Detail_JoinsTheBrandLineAndPackInOrder_SkippingEmptyParts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FoodDisplay.Of("Nutella", "Ferrero", "Ferrero, Nutella", "400 g e").Detail, Is.EqualTo("Ferrero · 400 g e"));
            Assert.That(FoodDisplay.Of("Original", "Weetabix", null, "430 gram").Detail, Is.EqualTo("430 gram"));
            Assert.That(FoodDisplay.Of("Mars", null, "Mars, Mars Wrigley", null).Detail, Is.EqualTo("Mars Wrigley"));
        });
    }

    [TestCase("Hazelnut spread", "Hazelnut spread")]
    [TestCase("  Hazelnut spread ", "Hazelnut spread")]
    [TestCase("  ", null)]
    [TestCase(null, null)]
    public void GenericName_DifferentFromTheTitle_IsKeptTrimmedWhenNotBlank(string? generic, string? expected)
    {
        var display = FoodDisplay.Of("Nutella", "Ferrero", "Ferrero, Nutella", null, generic);

        Assert.That(display.GenericName, Is.EqualTo(expected));
    }

    [Test]
    public void GenericName_SameAsTheOriginalNameBehindABrandPrefix_IsDropped()
    {
        var display = FoodDisplay.Of("Baked Beans", "Heinz", "Heinz", null, "Baked Beans");

        Assert.That((display.Title, display.GenericName), Is.EqualTo(("Heinz Baked Beans", (string?)null)));
    }

    [Test]
    public void Of_ABlankFirstBrand_IsSkippedForTheNextOne()
    {
        var display = FoodDisplay.Of("Original", null, "-, Weetabix", null);

        Assert.That((display.Title, display.BrandLine), Is.EqualTo(("Weetabix Original", (string?)null)));
    }

    [Test]
    public void Of_EmptyNameWithABrand_TitlesWithTheBrandAloneAndNoTrailingSpace()
    {
        var display = FoodDisplay.Of("  ", null, "Weetabix", null);

        Assert.That(display.Title, Is.EqualTo("Weetabix"));
    }

    [Test]
    public void GenericName_SameAsTheTitle_IsDropped()
    {
        Assert.That(FoodDisplay.Of("Nutella", null, "Ferrero, Nutella", null, "nutella").GenericName, Is.Null);
    }
}
