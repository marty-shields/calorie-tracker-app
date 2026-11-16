using CalorieTracker.Core.Data;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>Splits the typed text into folded words and ranks the food table by <see cref="FoodRanker"/>. Each food comes back bounded like <see cref="FoodService.GetAsync"/> (serving and optional nutrients out of range are dropped). A failing store gives <see cref="FoodSearchStatus.Failed"/>, never an exception.</summary>
internal sealed partial class FoodSearch(FoodStore store, FoodHistoryService history, ILogger<FoodSearch> logger) : IFoodSearch
{
    private const int MaxTerms = 10;

    public async Task<FoodSearchResult> SearchAsync(string query, int limit = IFoodSearch.DefaultLimit, CancellationToken ct = default)
    {
        var terms = Terms(query);
        if (terms.Count == 0)
        {
            return FoodSearchResult.Found([]);
        }

        try
        {
            var foods = await store.ListAsync(ct).WaitAsync(ct);
            var ranked = FoodRanker.Rank(foods, terms, Math.Clamp(limit, 1, IFoodSearch.MaxLimit));
            foreach (var food in ranked)
            {
                FoodService.Bound(food);
            }

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

    public async Task<FoodGroupsResult> SearchGroupedAsync(string query, Meal? meal = null, CancellationToken ct = default)
    {
        var terms = Terms(query);
        if (terms.Count == 0)
        {
            return FoodGroupsResult.Found([]);
        }

        try
        {
            var foods = await store.ListAsync(ct).WaitAsync(ct);
            var matches = FoodRanker.Rank(foods, terms, int.MaxValue);
            var logged = await history.ForFoodsAsync(matches.Select(f => f.Id).ToList(), meal);
            ct.ThrowIfCancellationRequested();
            var byFood = logged.Status == FoodHistoryStatus.Found ? logged.Foods.ToDictionary(h => h.FoodId) : [];
            var listings = matches.Select(f => new FoodListing(f, byFood.GetValueOrDefault(f.Id))).ToList();
            var diary = listings.Where(l => l.History is not null).OrderByDescending(l => l.History!.TimesLogged).ToList();
            var rest = listings.Where(l => l.History is null).ToList();
            var groups = new[]
            {
                Group(FoodGroupKind.Diary, diary),
                Group(FoodGroupKind.Common, rest.Where(l => l.Food.Source == FoodSource.CoFID)),
                Group(FoodGroupKind.Branded, rest.Where(l => l.Food.Source == FoodSource.OpenFoodFacts)),
            };
            Log.Grouped(logger, terms.Count, groups[0].Foods.Count, groups[1].Foods.Count, groups[2].Foods.Count);
            return FoodGroupsResult.Found(groups);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return FoodGroupsResult.Failed();
        }
    }

    public async Task<FoodGroupsResult> SuggestAsync(Meal? meal = null, CancellationToken ct = default)
    {
        try
        {
            var usual = meal is { } m ? await history.UsualAsync(m, IFoodSearch.UsualLimit) : FoodHistoryResult.Found([]);
            var recent = await history.RecentAsync(IFoodSearch.RecentLimit + IFoodSearch.UsualLimit, meal);
            if (usual.Status != FoodHistoryStatus.Found || recent.Status != FoodHistoryStatus.Found)
            {
                return FoodGroupsResult.Failed();
            }

            var usualIds = usual.Foods.Select(h => h.FoodId).ToHashSet();
            var recentRows = recent.Foods.Where(h => !usualIds.Contains(h.FoodId)).Take(IFoodSearch.RecentLimit).ToList();
            var foods = (await store.GetManyAsync(usual.Foods.Concat(recentRows).Select(h => h.FoodId).ToList()).WaitAsync(ct)).ToDictionary(f => f.Id);
            var groups = new[]
            {
                Group(FoodGroupKind.Usual, Listings(usual.Foods, foods)),
                Group(FoodGroupKind.Recent, Listings(recentRows, foods)),
            };
            Log.Suggested(logger, groups[0].Foods.Count, groups[1].Foods.Count);
            return FoodGroupsResult.Found(groups);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return FoodGroupsResult.Failed();
        }
    }

    private static IEnumerable<FoodListing> Listings(IEnumerable<FoodHistory> rows, Dictionary<int, Food> foods) =>
        rows.Where(h => foods.ContainsKey(h.FoodId)).Select(h => new FoodListing(foods[h.FoodId], h));

    private static FoodGroup Group(FoodGroupKind kind, IEnumerable<FoodListing> listings)
    {
        var kept = listings.Take(kind is FoodGroupKind.Usual or FoodGroupKind.Recent ? int.MaxValue : IFoodSearch.GroupLimit).ToList();
        foreach (var listing in kept)
        {
            FoodService.Bound(listing.Food);
        }

        return new FoodGroup(kind, kept);
    }

    private static List<string> Terms(string? query) =>
        FoodRanker.Words(SearchFolding.Fold(query ?? ""))
            .DistinctBy(FoodRanker.Normalise)
            .Take(MaxTerms)
            .ToList();

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodSearchGrouped, LogLevel.Debug, "Grouped food search with {TermCount} terms found {DiaryCount} diary, {CommonCount} common and {BrandedCount} branded foods")]
        public static partial void Grouped(ILogger logger, int termCount, int diaryCount, int commonCount, int brandedCount);

        [LoggerMessage((int)CoreEvent.FoodSuggested, LogLevel.Debug, "Foods before typing: {UsualCount} usual and {RecentCount} recent")]
        public static partial void Suggested(ILogger logger, int usualCount, int recentCount);

        [LoggerMessage((int)CoreEvent.FoodSearched, LogLevel.Debug, "Food search with {TermCount} terms found {ResultCount} foods")]
        public static partial void Searched(ILogger logger, int termCount, int resultCount);

        [LoggerMessage((int)CoreEvent.FoodSearchFailed, LogLevel.Warning, "Food search failed")]
        public static partial void Failed(ILogger logger);
    }
}
