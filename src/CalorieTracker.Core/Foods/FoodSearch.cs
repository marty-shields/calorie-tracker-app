using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>Splits the typed text into folded words and ranks the food table by <see cref="FoodRanker"/>. A failing store gives <see cref="FoodSearchStatus.Failed"/>, never an exception.</summary>
internal sealed partial class FoodSearch(FoodStore store, ILogger<FoodSearch> logger) : IFoodSearch
{
    private const int MaxTerms = 10;

    public async Task<FoodSearchResult> SearchAsync(string query, FoodType? type = null, int limit = IFoodSearch.DefaultLimit, CancellationToken ct = default)
    {
        var terms = FoodRanker.Words(SearchFolding.Fold(query ?? ""))
            .Select(FoodRanker.Normalise)
            .Distinct()
            .Take(MaxTerms)
            .ToList();
        if (terms.Count == 0)
        {
            return FoodSearchResult.Found([]);
        }

        try
        {
            var foods = await store.ListAsync(type, ct).WaitAsync(ct);
            var ranked = FoodRanker.Rank(foods, terms, Math.Clamp(limit, 1, IFoodSearch.MaxLimit));
            Log.Searched(logger, terms.Count, ranked.Count);
            return FoodSearchResult.Found(ranked);
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
