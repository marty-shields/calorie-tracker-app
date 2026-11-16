using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

public class ServingRowsTests
{
    private static Food Food(string? label = null, double? amount = null, double? pack = null, FoodUnit unit = FoodUnit.Gram) =>
        new() { ServingLabel = label, ServingAmount = amount, PackageAmount = pack, Unit = unit };

    [Test]
    public void Of_ServingAndADifferentPack_GivesAProductRowAndAPackRow()
    {
        var rows = ServingRows.Of(Food("1 glass (250 ml)", 250, 330, FoodUnit.Millilitre));

        Assert.That(rows.Select(r => (r.Label, r.Amount, r.Source, r.SortOrder)), Is.EqualTo(new[]
        {
            ("1 glass (250 ml)", 250.0, ServingSource.Product, 0),
            ("Can 330 ml", 330.0, ServingSource.Pack, 1),
        }));
    }

    [Test]
    public void Of_PackOfTheServingsAmount_GivesOnlyTheProductRow()
    {
        Assert.That(ServingRows.Of(Food("1 bar", 51, 51)).Select(r => r.Source), Is.EqualTo(new[] { ServingSource.Product }));
    }

    [Test]
    public void Of_NoServing_GivesJustThePackRow()
    {
        var rows = ServingRows.Of(Food(pack: 51));

        Assert.That(rows.Select(r => (r.Label, r.Source)), Is.EqualTo(new[] { ("Pack 51 g", ServingSource.Pack) }));
    }

    [Test]
    public void Of_LabelIsTrimmed_AndABadServingGivesNoProductRow()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServingRows.Of(Food("  1 pot  ", 100)).Single().Label, Is.EqualTo("1 pot"));
            Assert.That(ServingRows.Of(Food("1 pot", 99_999)), Is.Empty);
            Assert.That(ServingRows.Of(Food("   ", 100)), Is.Empty);
        });
    }

    [TestCase(1000.0, true)]
    [TestCase(350.1, true)]
    [TestCase(1000.1, false)]
    [TestCase(0.0, false)]
    [TestCase(-1.0, false)]
    [TestCase(double.NaN, false)]
    [TestCase(double.PositiveInfinity, false)]
    [TestCase(null, false)]
    public void Of_PackAboveTheLimit_GivesNoPackRow(double? pack, bool expected)
    {
        Assert.That(ServingRows.Of(Food(pack: pack)).Any(r => r.Source == ServingSource.Pack), Is.EqualTo(expected));
    }

    [TestCase(330.0, FoodUnit.Millilitre, null, "Can 330 ml")]
    [TestCase(440.0, FoodUnit.Millilitre, null, "Can 440 ml")]
    [TestCase(330.0, FoodUnit.Millilitre, "6 can", "Can 330 ml")]
    [TestCase(500.0, FoodUnit.Millilitre, null, "Pack 500 ml")]
    [TestCase(250.0, FoodUnit.Millilitre, null, "Pack 250 ml")]
    [TestCase(500.0, FoodUnit.Millilitre, "1 bottle (500 ml)", "Bottle 500 ml")]
    [TestCase(1000.0, FoodUnit.Millilitre, null, "Bottle 1 L")]
    [TestCase(1500.0, FoodUnit.Millilitre, "1.5 L", "Bottle 1.5 L")]
    [TestCase(330.5, FoodUnit.Millilitre, null, "Pack 330.5 ml")]
    [TestCase(51.5, FoodUnit.Gram, null, "Pack 51.5 g")]
    [TestCase(51.456, FoodUnit.Gram, "51 g", "Pack 51.46 g")]
    [TestCase(1000.0, FoodUnit.Gram, null, "Pack 1 kg")]
    [TestCase(330.0, FoodUnit.Gram, "330 g", "Pack 330 g")]
    [TestCase(330.0, FoodUnit.Gram, "1 tin", "Can 330 g")]
    public void PackLabel_NamesWhatThePackIsAndItsSize(double amount, FoodUnit unit, string? packageLabel, string expected)
    {
        Assert.That(ServingRows.PackLabel(amount, unit, packageLabel), Is.EqualTo(expected));
    }
}
