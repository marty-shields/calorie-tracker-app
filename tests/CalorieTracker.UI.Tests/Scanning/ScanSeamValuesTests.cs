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

    [Test]
    public void OnlineSearchSteps_UseTheSeamsFileName()
    {
        var seam = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "E2EOnlineSearchSeam.cs");
        var steps = RepoFiles.Read("tests", "CalorieTracker.E2E.Tests", "Steps", "FoodSearchOnlineSteps.cs");

        Assert.That(Value(Constant(steps, "SeamFileName")), Is.EqualTo(Value(Constant(seam, "FileName"))));
    }

    [Test]
    public void ClockSteps_UseTheSeamsFileName()
    {
        var seam = RepoFiles.Read("src", "CalorieTracker.App", "Diagnostics", "E2EClockSeam.cs");
        var steps = RepoFiles.Read("tests", "CalorieTracker.E2E.Tests", "Steps", "DashboardSteps.cs");

        Assert.That(Value(Constant(steps, "ClockSeamFileName")), Is.EqualTo(Value(Constant(seam, "FileName"))));
    }

    private static Match Constant(string source, string name) => Regex.Match(source, $@"const string {name} = ""([^""]+)"";");

    private static string Value(Match match)
    {
        Assert.That(match.Success, Is.True, "constant not found");
        return match.Groups[1].Value;
    }
}
