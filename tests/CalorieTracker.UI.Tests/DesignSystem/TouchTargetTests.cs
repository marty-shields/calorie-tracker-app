using Shields.UI.Testing;

namespace CalorieTracker.UI.Tests.DesignSystem;

public class TouchTargetTests
{
    private static IEnumerable<TestCaseData> Controls() =>
        RepoFiles.Find("*.razor", "src", "CalorieTracker.UI")
            .SelectMany(file => TouchTargetCheck.Controls(File.ReadAllText(file)).Select(control => (File: file, Control: control)))
            .Select(c => new TestCaseData(c.File, c.Control).SetArgDisplayNames($"{Path.GetFileName(c.File)} {c.Control.Tag}.{c.Control.CssClass}"));

    [TestCaseSource(nameof(Controls))]
    public void Control_IsAtLeastATouchTarget(string razorFile, TouchTargetControl control) =>
        Assert.That(TouchTargetCheck.Problems(control, File.ReadAllText(razorFile + ".css")), Is.Empty);
}
