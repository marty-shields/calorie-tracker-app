using System.Reflection;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Tests.Logging;

/// <summary>
/// The log lines the food search epic added (search, suggestions, food history, the stored-lookup refresh) carry ids, counts and statuses only.
/// The behaviour tests (<c>Logging_Search_LogsCountsOnly...</c>, <c>Logging_GroupedAndSuggest_LogCountsOnly</c>, <c>Calls_LogOnlyCountsAndNothingTheUserEntered</c>, the stored-lookup content test) check each line's text with distinctive content typed;
/// this test checks the shape, so a new parameter that could hold what a person typed (a string, a date, a double amount) fails here.
/// </summary>
public class FoodSearchLogLinesTests
{
    private static readonly CoreEvent[] EpicLines =
    [
        CoreEvent.FoodSearched,
        CoreEvent.FoodSearchGrouped,
        CoreEvent.FoodSuggested,
        CoreEvent.FoodSearchFailed,
        CoreEvent.FoodHistoryRecent,
        CoreEvent.FoodHistoryUsual,
        CoreEvent.FoodHistoryForFoods,
        CoreEvent.FoodHistoryFailed,
        CoreEvent.StoredLookupRefreshed,
        CoreEvent.StoredLookupRefreshFailed,
        CoreEvent.OnlineSearchSent,
        CoreEvent.OnlineSearchCached,
        CoreEvent.OnlineSearchAnswered,
        CoreEvent.OnlineSearchWaiting,
        CoreEvent.OnlineSearchWorldwideRepeat,
        CoreEvent.OnlineSearchLimited,
        CoreEvent.OnlineSearchNoResponse,
        CoreEvent.OnlineSearchMalformed,
        CoreEvent.OnlineSearchUnexpectedStatus,
        CoreEvent.OnlineSearchStoredCheckFailed,
        CoreEvent.OnlineImported,
        CoreEvent.OnlineImportLookupFailed,
        CoreEvent.OnlineImportStoreFailed,
        CoreEvent.OnlineNoticeReadFailed,
        CoreEvent.OnlineNoticeSaveFailed,
    ];

    [Test]
    public void EveryEpicLogLine_TakesOnlyCountsIdsFlagsAndStatuses()
    {
        var declared = typeof(CoreEvent).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<LoggerMessageAttribute>()))
            .Where(x => x.Attribute is not null)
            .ToDictionary(x => (CoreEvent)x.Attribute!.EventId, x => x.Method);

        Assert.Multiple(() =>
        {
            foreach (var line in EpicLines)
            {
                Assert.That(declared, Does.ContainKey(line), $"{line} has a logger message");
                var content = declared[line].GetParameters().Where(p => p.ParameterType != typeof(ILogger) && p.ParameterType != typeof(Exception));
                Assert.That(content.Select(p => p.ParameterType), Is.All.Matches<Type>(t => t == typeof(int) || t == typeof(bool) || t!.IsEnum), $"{line} takes counts, ids, flags and statuses only");
            }
        });
    }
}
