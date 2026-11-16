using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The E2E steps and the Debug scan seam each hold the seam's file name and barcode; they must agree.</summary>
public class ScanSeamValuesTests
{
    [Test]
    public void ScanSteps_UseTheSeamsFileNameAndBarcode()
    {
        var seam = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "E2EScanSeam.cs");
        var steps = RepoFiles.Read("tests", "CalorieTracker.E2E.Tests", "Steps", "ScanSteps.cs");

        Assert.That(Value(Constant(steps, "ScanSeamFileName")), Is.EqualTo(Value(Constant(seam, "FileName"))));
        Assert.That(Value(Constant(steps, "ScanSeamBarcode")), Is.EqualTo(Value(Constant(seam, "Barcode"))));
    }

    private static Match Constant(string source, string name) => Regex.Match(source, $@"const string {name} = ""([^""]+)"";");

    private static string Value(Match match)
    {
        Assert.That(match.Success, Is.True, "constant not found");
        return match.Groups[1].Value;
    }
}
