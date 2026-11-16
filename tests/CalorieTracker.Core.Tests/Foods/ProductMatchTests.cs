using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

public class ProductMatchTests
{
    private static Food Cola(Action<Food>? change = null)
    {
        var food = new Food
        {
            Source = FoodSource.OpenFoodFacts,
            Name = "Coca-Cola Original",
            Brand = "Coca-Cola",
            Unit = FoodUnit.Millilitre,
            Kcal = 42,
            ProteinG = 0,
            CarbsG = 10.6,
            FatG = 0,
            SugarsG = 10.6,
            SaltG = 0,
        };
        change?.Invoke(food);
        return food;
    }

    [TestCase("Coca-Cola Original", "coca cola original")]
    [TestCase("Coca-Cola Original 330 ml", "coca cola original")]
    [TestCase("Coca Cola Original Taste 1.5L", "coca cola original taste")]
    [TestCase("Coca Cola Original Taste 1,5 L", "coca cola original taste")]
    [TestCase("Cola 2 litres", "cola")]
    [TestCase("Cola 2 Liters", "cola")]
    [TestCase("Cola 33cl", "cola")]
    [TestCase("Oats 500g", "oats")]
    [TestCase("Oats 1 kg", "oats")]
    [TestCase("Crème brûlée 100 g", "creme brulee")]
    [TestCase("Cola Zero Sugar", "cola zero sugar")]
    [TestCase("Cola 7up", "cola 7up")]
    [TestCase("  Pack of 6  ", "pack of 6")]
    [TestCase("330 ml", "")]
    [TestCase("", "")]
    public void NormaliseName_FoldsAndDropsSizeWordsOnly(string name, string expected)
    {
        Assert.That(ProductMatch.NormaliseName(name), Is.EqualTo(expected));
    }

    [Test]
    public void NormaliseName_Null_IsEmpty() => Assert.That(ProductMatch.NormaliseName(null), Is.Empty);

    [Test]
    public void IsSameProduct_SameDrinkInAnotherSize_Matches()
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.Name = "Coca Cola ORIGINAL 1 L")), Is.True);
    }

    [Test]
    public void IsSameProduct_BrandAndNameAreFolded()
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => (f.Brand, f.Name) = ("COCA COLA", "coca-cola original"))), Is.True);
    }

    [TestCase("Coca-Cola Zero Sugar")]
    [TestCase("Coca-Cola Light")]
    [TestCase("Coca-Cola Diet")]
    [TestCase("Coca-Cola Vanilla")]
    [TestCase("Coca-Cola")]
    [TestCase("Coca-Cola Original Taste")]
    [TestCase("Oreo Original")]
    [TestCase("")]
    public void IsSameProduct_AnotherNameIsAnotherFood_EvenWithTheSameNutrients(string name)
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.Name = name)), Is.False);
    }

    [Test]
    public void IsSameProduct_AnotherBrand_IsAnotherFood()
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.Brand = "COCA-COLA SERVICES SA/NV")), Is.False);
    }

    [Test]
    public void IsSameProduct_NoBrandOnEitherSide_IsNotEnoughToMerge()
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(f => f.Brand = null), Cola(f => f.Brand = null)), Is.False);
        Assert.That(ProductMatch.IsSameProduct(Cola(f => f.Brand = "  "), Cola(f => f.Brand = "")), Is.False);
    }

    [Test]
    public void IsSameProduct_AnotherUnit_NeverMatches()
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.Unit = FoodUnit.Gram)), Is.False);
    }

    [TestCase(42.8, true)]
    [TestCase(42.84, true)]
    [TestCase(42.9, false)]
    [TestCase(45.0, false)]
    [TestCase(41.2, true)]
    public void IsSameProduct_KcalIsWithinTwoPercent(double kcal, bool expected)
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.Kcal = kcal)), Is.EqualTo(expected));
    }

    [TestCase(0.1, true)]
    [TestCase(0.11, false)]
    [TestCase(0.0, true)]
    public void IsSameProduct_ValuesNearZeroAreWithinOneTenthAbsolute(double protein, bool expected)
    {
        Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.ProteinG = protein)), Is.EqualTo(expected));
    }

    [Test]
    public void IsSameProduct_EachNutrientIsChecked()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.CarbsG = 11.5)), Is.False, "carbs");
            Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.FatG = 0.5)), Is.False, "fat");
            Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.SugarsG = 9.0)), Is.False, "sugars");
            Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.SaltG = 0.3)), Is.False, "salt");
            Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.FibreG = 1)), Is.False, "fibre known on one side only");
            Assert.That(ProductMatch.IsSameProduct(Cola(), Cola(f => f.SugarsG = null)), Is.False, "sugars unknown on one side only");
            Assert.That(ProductMatch.IsSameProduct(Cola(f => f.SugarsG = null), Cola(f => f.SugarsG = null)), Is.True, "unknown on both");
        });
    }

    [Test]
    public void IsSameProduct_TheDifferenceIsTheSameBothWays()
    {
        var a = Cola(f => f.Kcal = 100);
        var b = Cola(f => f.Kcal = 101.9);

        Assert.That(ProductMatch.IsSameProduct(a, b), Is.EqualTo(ProductMatch.IsSameProduct(b, a)));
    }
}
