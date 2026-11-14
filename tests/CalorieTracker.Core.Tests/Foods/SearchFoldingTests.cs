using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

public class SearchFoldingTests
{
    [TestCase("Crème fraîche", "creme fraiche")]
    [TestCase("PURÉE", "puree")]
    [TestCase("creme", "creme")]
    [TestCase("Jalapeño", "jalapeno")]
    [TestCase("", "")]
    public void Fold_RemovesAccentsAndLowercases(string input, string expected) =>
        Assert.That(SearchFolding.Fold(input), Is.EqualTo(expected));

    [Test]
    public void Fold_PrecomposedAndDecomposedInput_GiveTheSameResult() =>
        Assert.That(SearchFolding.Fold("é"), Is.EqualTo(SearchFolding.Fold("é")));
}
