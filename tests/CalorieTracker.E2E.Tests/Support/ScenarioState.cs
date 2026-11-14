namespace CalorieTracker.E2E.Tests.Support;

/// <summary>What a scenario has done so far, for later steps to check against. One per scenario.</summary>
public sealed class ScenarioState
{
    /// <summary>The app's log when the scenario's starting point was reached, for counting new lines.</summary>
    internal string LogAtStart { get; set; } = "";

    /// <summary>The app's log just before Android backed it up.</summary>
    internal string LogBeforeBackup { get; set; } = "";

    /// <summary>The phone's dark theme before the scenario changed it, as a <c>cmd uimode night</c> value; null when unchanged.</summary>
    internal string? PhoneDarkTheme { get; set; }

    /// <summary>Whether another build of the app is installed, or none, so the tested build must be installed again.</summary>
    internal bool TestedAppReplaced { get; set; }
}
