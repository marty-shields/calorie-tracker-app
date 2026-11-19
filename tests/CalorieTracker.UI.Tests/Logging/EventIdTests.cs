using System.Text.RegularExpressions;
using CalorieTracker.UI.Logging;

namespace CalorieTracker.UI.Tests.Logging;

/// <summary>
/// Event ids are named values: <c>UiEvent</c> 2000–4999 here, <c>AppEvent</c> 5000+ (the App can't be referenced, so its file is read),
/// <c>CoreEvent</c> in Core.Tests. Values are unique and fixed. No <c>[LoggerMessage]</c> in the source uses a bare number.
/// </summary>
public partial class EventIdTests
{
    private static IEnumerable<TestCaseData> SourceFiles() =>
        RepoFiles.Find("*.cs", "src")
            .Select(f => new TestCaseData(f).SetArgDisplayNames(Path.GetRelativePath(RepoFiles.Root, f)));

    [Test]
    public void UiEvent_ValuesAreUnique() =>
        Assert.That(Enum.GetValues<UiEvent>().Select(e => (int)e), Is.Unique);

    [Test]
    public void UiEvent_ValuesAreInTheUisRange() =>
        Assert.That(Enum.GetValues<UiEvent>().Select(e => (int)e), Is.All.InRange(2000, 4999));

    [Test]
    public void UiEvent_ValuesAreFixed() =>
        Assert.That(Enum.GetValues<UiEvent>().ToDictionary(e => e, e => (int)e), Is.EqualTo(new Dictionary<UiEvent, int>
        {
            [UiEvent.StartupFinished] = 2001,
            [UiEvent.StartupFailed] = 2002,
            [UiEvent.ScreenError] = 2101,
            [UiEvent.BackHandled] = 2201,
            [UiEvent.BackNotHandled] = 2202,
            [UiEvent.BackHandlerFailed] = 2203,
            [UiEvent.AddSheetOpened] = 2301,
            [UiEvent.AddSheetChose] = 2302,
            [UiEvent.AddSheetFocusFailed] = 2303,
            [UiEvent.UsualRead] = 2304,
            [UiEvent.UsualReadFailed] = 2305,
            [UiEvent.UsualAdded] = 2306,
            [UiEvent.UsualAddFailed] = 2307,
            [UiEvent.UsualUndoFailed] = 2308,
            [UiEvent.TrendsLoaded] = 2401,
            [UiEvent.TrendsLoadFailed] = 2402,
            [UiEvent.TrendsOpened] = 2403,
            [UiEvent.AppStartHandlerFailed] = 3201,
            [UiEvent.LoadFailedFocusFailed] = 3301,
            [UiEvent.ThemeLoadFailed] = 3403,
            [UiEvent.ThemeSaveFailed] = 3404,
            [UiEvent.ThemeNotSaved] = 3405,
            [UiEvent.TargetLoadFailed] = 3406,
            [UiEvent.TargetSaveFailed] = 3407,
            [UiEvent.FoodSeedWaited] = 3501,
            [UiEvent.FoodSeedRetried] = 3503,
            [UiEvent.ScanLookupStarted] = 3601,
            [UiEvent.ScanLookupFinished] = 3602,
            [UiEvent.ScanCameraUnavailable] = 3603,
            [UiEvent.NumberFieldFocusFailed] = 3701,
            [UiEvent.UndoFailed] = 3801,
            [UiEvent.RestoreFailed] = 3802,
            [UiEvent.FavouriteReadFailed] = 3901,
            [UiEvent.FavouriteWriteFailed] = 3902,
            [UiEvent.PullRefreshFailed] = 4001,
        }));

    [Test]
    public void AppEvent_ValuesAreUniqueAndInTheAppsRange()
    {
        var values = AppEvents().Values;

        Assert.Multiple(() =>
        {
            Assert.That(values, Is.Not.Empty);
            Assert.That(values, Is.Unique);
            Assert.That(values, Is.All.GreaterThanOrEqualTo(5000));
        });
    }

    [Test]
    public void AppEvent_ValuesAreFixed() =>
        Assert.That(AppEvents(), Is.EqualTo(new Dictionary<string, int>
        {
            ["ProcessStarted"] = 5001,
            ["PreviousExit"] = 5002,
            ["PreviousExitAbnormal"] = 5003,
            ["NoPreviousExit"] = 5004,
            ["ActivityCreated"] = 5011,
            ["ActivityStopped"] = 5012,
            ["ActivityDestroyed"] = 5013,
            ["ActivityStarted"] = 5014,
            ["TrimMemory"] = 5021,
            ["UrlBlocked"] = 5041,
            ["UnhandledException"] = 5031,
            ["UnobservedTaskException"] = 5033,
            ["KeyboardRestoreFailed"] = 5061,
            ["KeyboardPaused"] = 5062,
            ["KeyboardResumed"] = 5063,
            ["KeyboardWindowFocusRegained"] = 5064,
            ["KeyboardRestoreDecided"] = 5065,
            ["KeyboardWantedChanged"] = 5066,
            ["KeyboardShowRequested"] = 5067,
            ["KeyboardRestoreSkipped"] = 5068,
            ["KeyboardDevice"] = 5069,
            ["ScanPermissionDenied"] = 5071,
            ["ScanNoWindow"] = 5072,
            ["ScanFinished"] = 5073,
            ["ScanFailed"] = 5074,
        }));

    [TestCaseSource(nameof(SourceFiles))]
    public void LoggerMessage_UsesANamedEventId(string file) =>
        Assert.That(BareNumber().IsMatch(File.ReadAllText(file)), Is.False);

    /// <summary>Each <c>AppEvent</c> member's name and value, read from its source file.</summary>
    private static Dictionary<string, int> AppEvents() =>
        Member().Matches(RepoFiles.Read("src", "CalorieTracker.App", "Logging", "AppEvent.cs"))
            .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));

    [GeneratedRegex(@"(\w+)\s*=\s*(\d+)")]
    private static partial Regex Member();

    [GeneratedRegex(@"\[LoggerMessage\(\s*(EventId\s*=\s*)?\d")]
    private static partial Regex BareNumber();
}
