#if DEBUG
using System.Globalization;

namespace CalorieTracker.App.Diagnostics;

/// <summary>The text of the clock seam's file: a time of day such as <c>19:30</c>, optionally after a whole-day offset from the device's date such as <c>-2d 12:00</c>.</summary>
internal static class E2EClockPin
{
    /// <summary>False when <paramref name="text"/> is anything else, which leaves the device's clock. <paramref name="days"/> is 0 for a plain time.</summary>
    public static bool TryParse(string text, out int days, out TimeOnly time)
    {
        days = 0;
        time = default;
        var parts = text.Trim().Split(' ');
        if (parts.Length == 2)
        {
            var offset = parts[0];
            if (offset.Length < 3 || offset[0] is not ('-' or '+') || offset[^1] != 'd'
                || !int.TryParse(offset[..^1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out days))
            {
                return false;
            }
        }
        else if (parts.Length != 1)
        {
            return false;
        }

        return TimeOnly.TryParseExact(parts[^1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}
#endif
