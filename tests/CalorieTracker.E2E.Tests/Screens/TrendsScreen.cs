using System.Globalization;
using OpenQA.Selenium;

namespace CalorieTracker.E2E.Tests.Screens;

/// <summary>Trends: the Week, Month, Year control, the period strip, and the calendar of Month.</summary>
public sealed class TrendsScreen(WebView web)
{
    public const string Title = "Trends";

    public bool IsShowing() => web.Exists("screen-title") && web.Text("screen-title") == Title;

    /// <summary>Taps Week, Month or Year. The radios are hidden, so the tap goes to the label round the radio.</summary>
    public void ChoosePeriod(string period)
    {
        var radio = $"trends-period-{period.ToLowerInvariant()}";
        web.Find(radio).FindElement(By.XPath("..")).Click();
        web.Eventually(() => web.Find(radio).Selected, Is.True, $"{period} chosen on Trends");
    }

    public string Label() => web.Text("trends-label");

    public void Previous() => web.Tap("trends-previous");

    public void Next() => web.Tap("trends-next");

    public bool NextIsEnabled() => web.IsEnabled("trends-next");

    /// <summary>Whether Trends is marked as the current page in the navigation bar.</summary>
    public bool IsCurrentInBar() => web.Attribute("trends-link", "aria-current") == "page";

    /// <summary>Taps a day of the calendar in Month.</summary>
    public void TapCalendarDay(DateOnly day) =>
        web.Tap($"trends-calendar-day-{day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
}
