using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>Splits the typed text into lowercase words and searches the food table. A failing store gives <see cref="FoodSearchStatus.Failed"/>, never an exception.</summary>
internal sealed partial class FoodSearch(FoodStore store, ILogger<FoodSearch> logger) : IFoodSearch
{
    private const int MaxTerms = 10;

    public async Task<FoodSearchResult> SearchAsync(string query, int limit = IFoodSearch.DefaultLimit, CancellationToken ct = default)
    {
        var terms = (query ?? "")
            .ToLowerInvariant()
            .Split(FoodStore.Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .Take(MaxTerms)
            .ToList();
        if (terms.Count == 0)
        {
            return FoodSearchResult.Found([]);
        }

        try
        {
            var foods = await store.SearchAsync(terms, Math.Clamp(limit, 1, IFoodSearch.MaxLimit), ct).WaitAsync(ct);
            Log.Searched(logger, terms.Count, foods.Count);
            return FoodSearchResult.Found(foods);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return FoodSearchResult.Failed();
        }
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodSearched, LogLevel.Debug, "Food search with {TermCount} terms found {ResultCount} foods")]
        public static partial void Searched(ILogger logger, int termCount, int resultCount);

        [LoggerMessage((int)CoreEvent.FoodSearchFailed, LogLevel.Warning, "Food search failed")]
        public static partial void Failed(ILogger logger);
    }
}
