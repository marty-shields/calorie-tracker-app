using System.Text.RegularExpressions;

namespace CalorieTracker.UI.Tests.Scanning;

/// <summary>The App can't be referenced, so its scanner source is read: a log method takes a status or an exception type name, never the barcode.</summary>
public partial class ScannerLoggingTests
{
    private static readonly string[] Files = ["CameraBarcodeScanner.cs", "ScanPage.xaml.cs"];

    private static readonly string[] AllowedParameters = ["ScanStatus status", "string exceptionType"];

    private static readonly string[] AllowedArguments = ["result.Status", "ex.GetType().Name"];

    [TestCaseSource(nameof(Files))]
    public void NoLoggingOutsideTheLogClass(string file)
    {
        var source = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", file);

        Assert.That(source, Does.Not.Contain("logger.Log").And.Not.Contain("Debug.WriteLine").And.Not.Contain("Console.Write"));
    }

    [Test]
    public void LogMethods_TakeOnlyTheLoggerAndAStatusOrExceptionType()
    {
        var source = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "CameraBarcodeScanner.cs");

        var declarations = Declaration().Matches(source).ToList();

        Assert.That(declarations, Is.Not.Empty);
        foreach (var declaration in declarations)
        {
            var parameters = declaration.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries);
            Assert.That(parameters[0], Is.EqualTo("ILogger logger"), declaration.Value);
            Assert.That(parameters.Skip(1), Is.SubsetOf(AllowedParameters), declaration.Value);
        }
    }

    [Test]
    public void LogCalls_PassOnlyTheStatusOrTheExceptionTypeName()
    {
        var source = RepoFiles.Read("src", "CalorieTracker.App", "Scanning", "CameraBarcodeScanner.cs");

        var calls = Call().Matches(source).ToList();

        Assert.That(calls, Is.Not.Empty);
        foreach (var call in calls)
        {
            var argument = call.Groups[2].Value;
            Assert.That(argument == string.Empty || AllowedArguments.Contains(argument), Is.True, call.Value);
        }
    }

    [GeneratedRegex(@"static partial void (\w+)\(([^)]*)\)")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"Log\.(\w+)\(logger(?:, ([^;]*))?\);")]
    private static partial Regex Call();
}
