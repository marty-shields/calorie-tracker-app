using OpenQA.Selenium;

namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>Settings: a back arrow and title, the theme, the daily target and the version.</summary>
public sealed class SettingsScreen(WebView web)
{
    public const string Title = "Settings";

    /// <summary>Taps a theme in Appearance (system, light or dark) and waits until it is selected.</summary>
    public void ChooseTheme(string theme)
    {
        var radio = $"settings-theme-{theme}";
        web.Find(radio).FindElement(By.XPath("..")).Click();
        web.Eventually(() => web.Find(radio).Selected, Is.True, $"{theme} theme selected");
    }

    /// <summary>Whether a theme (system, light or dark) is the selected one.</summary>
    public bool IsThemeSelected(string theme) => web.Find($"settings-theme-{theme}").Selected;

    /// <summary>Types a number into Calories per day, one key at a time.</summary>
    public void TypeTarget(string text) => web.Type("settings-target", text);

    /// <summary>Empties Calories per day.</summary>
    public void ClearTarget() => web.Clear("settings-target");

    /// <summary>What Calories per day holds.</summary>
    public string TargetValue() => web.Value("settings-target");

    /// <summary>The message under Calories per day, or null when there is none.</summary>
    public string? TargetError() => web.Exists("settings-target-error") ? web.Text("settings-target-error") : null;

    /// <summary>The version line at the bottom, once Settings has opened.</summary>
    public string Version()
    {
        web.WaitUntil(() => web.Exists("app-version"), "Settings didn't show the version");
        return web.Text("app-version");
    }

    public void Back() => web.Tap("back");
}
