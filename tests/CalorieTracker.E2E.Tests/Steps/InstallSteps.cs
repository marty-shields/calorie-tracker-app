using System.Globalization;
using System.Security.Cryptography;
using CalorieTracker.Core.Data;
using CalorieTracker.E2E.Tests.Device;
using CalorieTracker.E2E.Tests.Support;
using Reqnroll;

namespace CalorieTracker.E2E.Tests.Steps;

/// <summary>
/// Installing this build over the last release (<c>@upgrade</c>), and Android's backup and restore across a reinstall
/// (<c>@backup</c>). A scenario that stops with another build installed, or none, gets the tested build back.
/// </summary>
[Binding]
public sealed class InstallSteps(Native native, AppLog log, ScenarioState state, DeviceClock clock)
{
    private const string LocalTransport = "com.android.localtransport/.LocalTransport";

    private static Adb Adb => AppiumSetup.Adb;

    private static TestedApp App => AppiumSetup.App;

    /// <summary>
    /// The upgrade handoff: upgrade-e2e.sh has installed <see cref="TestedApp.PreviousApk"/> and saved the settings in
    /// UpgradeSeed.feature with the last release's own E2E project. Checks that build is the one installed.
    /// </summary>
    [Given(@"^the last release is installed with the settings it saved$")]
    public void GivenTheLastReleaseIsInstalledWithItsSettings()
    {
        var previous = App.PreviousApk
            ?? throw new InvalidOperationException("PreviousApk is not set: run @upgrade through .github/scripts/upgrade-e2e.sh (docs/e2e-tests.md).");
        state.TestedAppReplaced = true;
        Assert.That(Adb.TryRun(out var path, "shell", "pm", "path", App.Package), Is.True, $"{App.Package} is installed: upgrade-e2e.sh installs the last release first");
        var installed = Adb.Shell("sha256sum", path.Split('\n')[0]["package:".Length..]).Split(' ')[0];
        Assert.That(installed, Is.EqualTo(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(previous)))),
            "the installed build is PreviousApk, with the settings upgrade-e2e.sh saved in it");
    }

    [When(@"^I install this build over it$")]
    public void WhenIInstallThisBuildOverIt()
    {
        Adb.Run("install", "-r", App.Apk);
        state.TestedAppReplaced = false;
        native.Launch();
    }

    /// <summary>
    /// Android's "Test backup and restore" steps, on the local transport. No <c>bmgr restore</c> runs: the install itself
    /// restores the backup, as on a new phone.
    /// </summary>
    [When(@"^Android backs up the app, and it is uninstalled and installed again$")]
    public void WhenAndroidBacksUpAndReinstalls()
    {
        if (App.IsDebug)
        {
            throw new InvalidOperationException("The Debug app allows no backup: run @backup with AppPackage set to the Release app (docs/e2e-tests.md).");
        }

        state.LogBeforeBackup = log.Read();
        native.Home();
        Adb.Shell("bmgr", "enable", "true");
        Adb.Shell("bmgr", "transport", LocalTransport);
        Assert.That(Adb.Shell("bmgr", "backupnow", App.Package), Does.Contain($"Package {App.Package} with result: Success"), "backup");

        state.TestedAppReplaced = true;
        Adb.Run("uninstall", App.Package);
        Adb.Run("install", App.Apk);
        state.TestedAppReplaced = false;

        Assert.That(Adb.AppShell(App, "find", App.DataFolder, "-name", "calorietracker.db3"), Is.Not.Empty, "the install restored the database");
        native.Launch();
    }

    [Then(@"^the log has no line from before the backup$")]
    public void ThenTheLogHasNoLineFromBeforeTheBackup()
    {
        var before = Lines(state.LogBeforeBackup).ToHashSet();
        Assert.That(before, Is.Not.Empty, "log lines before the backup");
        Assert.That(Lines(log.Read()).Where(before.Contains), Is.Empty, "log lines from before the backup");
    }

    /// <summary>
    /// The seed set 1900 two days ago and 2100 today. Only the Debug app has the clock seam that back-dates the 1900, so on the
    /// Release app both land today and one row is left. Reads the <c>TargetHistory</c> rows from a copy of the database, since
    /// no screen shows a past day's target directly. Each day before today's row keeps the 1900, as <c>TargetService.ForRangeAsync</c> gives it.
    /// </summary>
    [Then(@"^the stored target history is 1900 from 2 days ago and 2100 from today$")]
    public void ThenTheStoredTargetHistoryIs()
    {
        var today = DateOnly.FromDateTime(clock.LocalNow());
        string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expected = App.IsDebug
            ? new[] { $"{Day(today.AddDays(-2))}=1900", $"{Day(today)}=2100" }
            : [$"{Day(today)}=2100"];
        Assert.That(TargetHistoryRows(), Is.EqualTo(expected), "the TargetHistory rows after the upgrade");
    }

    /// <summary>The <c>TargetHistory</c> rows as <c>yyyy-MM-dd=kcal</c>, oldest first, read from a copy of the app's database and its write-ahead log.</summary>
    private static List<string> TargetHistoryRows()
    {
        string Find(string name) => Adb.AppShell(App, "find", App.DataFolder, "-name", name).Split('\n')[0].Trim();
        var database = Find("calorietracker.db3");
        Assert.That(database, Is.Not.Empty, "the app's database");
        var log = Find("calorietracker.db3-wal");
        var folder = Directory.CreateTempSubdirectory("calorietracker-db-");
        try
        {
            var copy = Path.Combine(folder.FullName, "calorietracker.db3");
            File.WriteAllBytes(copy, Convert.FromBase64String(Adb.AppShell(App, "base64", database)));
            if (log.Length > 0)
            {
                File.WriteAllBytes(copy + "-wal", Convert.FromBase64String(Adb.AppShell(App, "base64", log)));
            }

            using var db = new SQLite.SQLiteConnection(copy);
            return db.Query<TargetHistory>("SELECT EffectiveFrom, Kcal FROM TargetHistory ORDER BY EffectiveFrom")
                .Select(row => $"{row.EffectiveFrom}={row.Kcal?.ToString(CultureInfo.InvariantCulture) ?? "none"}")
                .ToList();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>Runs after <see cref="Hooks.AppHooks.SaveFailure"/>, so a failure's files show the build the scenario stopped on.</summary>
    [AfterScenario(Order = 20000)]
    public void InstallTheTestedBuildAgain()
    {
        if (state.TestedAppReplaced)
        {
            Adb.TryRun(out _, "uninstall", App.Package);
            Adb.Run("install", "-r", App.Apk);
        }
    }

    private static string[] Lines(string log) => log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
