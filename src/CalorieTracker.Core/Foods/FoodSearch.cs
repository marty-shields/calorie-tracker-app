using CalorieTracker.Core.Data;
using CalorieTracker.Core.History;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>Splits the typed text into folded words and ranks the food table by <see cref="FoodRanker"/>. Each food comes back bounded like <see cref="FoodService.GetAsync"/> (serving and optional nutrients out of range are dropped). A failing store gives <see cref="FoodSearchStatus.Failed"/>, never an exception.</summary>
internal sealed partial class FoodSearch(FoodStore store, FoodHistoryService history, FavouriteService favourites, ILogger<FoodSearch> logger) : IFoodSearch
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
            // The foods ever logged are few next to the matches, so one read of them beats asking for every match.
            var logged = await history.AllAsync(meal);
            ct.ThrowIfCancellationRequested();
            var favouriteIds = await FavouriteIdsAsync();
            ct.ThrowIfCancellationRequested();
            var byFood = logged.Status == FoodHistoryStatus.Found ? logged.Foods.ToDictionary(h => h.FoodId) : [];
            var listings = matches.Select(f => new FoodListing(f, byFood.GetValueOrDefault(f.Id))).ToList();
            var favourite = listings.Where(l => favouriteIds.Contains(l.Food.Id)).ToList();
            var others = listings.Where(l => !favouriteIds.Contains(l.Food.Id)).ToList();
            var diary = others.Where(l => l.History is not null).OrderByDescending(l => l.History!.TimesLogged).ToList();
            var rest = others.Where(l => l.History is null).ToList();
            var groups = new[]
            {
                Group(FoodGroupKind.Favourites, favourite),
                Group(FoodGroupKind.Diary, diary),
                Group(FoodGroupKind.Common, rest.Where(l => l.Food.Source == FoodSource.CoFID)),
                Group(FoodGroupKind.Branded, rest.Where(l => l.Food.Source == FoodSource.OpenFoodFacts)),
            };
            Log.Grouped(logger, terms.Count, groups[0].Total, groups[1].Total, groups[2].Total, groups[3].Total);
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
            var favouriteIds = await FavouriteIdsAsync();
            var spare = favouriteIds.Count;
            var usual = meal is { } m ? await history.UsualAsync(m, IFoodSearch.UsualLimit + spare) : FoodHistoryResult.Found([]);
            var recent = await history.RecentAsync(IFoodSearch.RecentLimit + IFoodSearch.UsualLimit + spare, meal);
            if (usual.Status != FoodHistoryStatus.Found || recent.Status != FoodHistoryStatus.Found)
            {
                return FoodGroupsResult.Failed();
            }

            var usualRows = usual.Foods.Where(h => !favouriteIds.Contains(h.FoodId)).Take(IFoodSearch.UsualLimit).ToList();
            var usualIds = usualRows.Select(h => h.FoodId).ToHashSet();
            var recentRows = recent.Foods.Where(h => !usualIds.Contains(h.FoodId) && !favouriteIds.Contains(h.FoodId)).Take(IFoodSearch.RecentLimit).ToList();
            // Usual and Recent are limited reads, so they can't stand in for the favourites' own history: ask for just those foods.
            // A failing read leaves the favourites without last amounts (each shows its values per 100), the way a failing favourites read leaves out the group and nothing else.
            var favouriteHistory = await history.ByFoodIdsAsync(favouriteIds, meal) is { Status: FoodHistoryStatus.Found } own ? own.Foods.ToDictionary(h => h.FoodId) : [];
            var foods = (await store.GetManyAsync(usualRows.Concat(recentRows).Select(h => h.FoodId).Concat(favouriteIds).ToList()).WaitAsync(ct)).ToDictionary(f => f.Id);
            var favouriteListings = favouriteIds
                .Where(foods.ContainsKey)
                .Select(id => new FoodListing(foods[id], favouriteHistory.GetValueOrDefault(id)))
                .OrderBy(l => SearchFolding.Fold(FoodDisplay.Of(l.Food).Title), StringComparer.Ordinal)
                .ThenBy(l => l.Food.Id)
                .ToList();
            var groups = new[]
            {
                Group(FoodGroupKind.Favourites, favouriteListings, int.MaxValue),
                Group(FoodGroupKind.Usual, Listings(usualRows, foods), int.MaxValue),
                Group(FoodGroupKind.Recent, Listings(recentRows, foods), int.MaxValue),
            };
            Log.Suggested(logger, groups[0].Foods.Count, groups[1].Foods.Count, groups[2].Foods.Count);
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

    public async Task<FoodHistory?> LastLoggedAsync(int foodId, Meal? meal = null)
    {
        var logged = await history.AllAsync(meal);
        return logged.Status == FoodHistoryStatus.Found ? logged.Foods.FirstOrDefault(h => h.FoodId == foodId) : null;
    }

    /// <summary>The favourite food ids, or none when the read fails: a failing favourites read leaves out the group and nothing else.</summary>
    private async Task<HashSet<int>> FavouriteIdsAsync()
    {
        var ids = await favourites.IdsAsync();
        return ids.Status == FavouriteReadStatus.Found ? ids.FoodIds.ToHashSet() : [];
    }

    private static IEnumerable<FoodListing> Listings(IEnumerable<FoodHistory> rows, Dictionary<int, Food> foods) =>
        rows.Where(h => foods.ContainsKey(h.FoodId)).Select(h => new FoodListing(foods[h.FoodId], h));

    private static FoodGroup Group(FoodGroupKind kind, IEnumerable<FoodListing> listings, int limit = IFoodSearch.GroupLimit)
    {
        var all = listings.ToList();
        var kept = all.Take(limit).ToList();
        foreach (var listing in kept)
        {
            FoodService.Bound(listing.Food);
        }

        return new FoodGroup(kind, kept, all.Count);
    }

    private static List<string> Terms(string? query) =>
        FoodRanker.Words(SearchFolding.Fold(query ?? ""))
            .DistinctBy(FoodRanker.Normalise)
            .Take(MaxTerms)
            .ToList();

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodSearchGrouped, LogLevel.Debug, "Grouped food search with {TermCount} terms found {FavouriteCount} favourite, {DiaryCount} diary, {CommonCount} common and {BrandedCount} branded foods")]
        public static partial void Grouped(ILogger logger, int termCount, int favouriteCount, int diaryCount, int commonCount, int brandedCount);

        [LoggerMessage((int)CoreEvent.FoodSuggested, LogLevel.Debug, "Foods before typing: {FavouriteCount} favourite, {UsualCount} usual and {RecentCount} recent")]
        public static partial void Suggested(ILogger logger, int favouriteCount, int usualCount, int recentCount);

        [LoggerMessage((int)CoreEvent.FoodSearched, LogLevel.Debug, "Food search with {TermCount} terms found {ResultCount} foods")]
        public static partial void Searched(ILogger logger, int termCount, int resultCount);

        [LoggerMessage((int)CoreEvent.FoodSearchFailed, LogLevel.Warning, "Food search failed")]
        public static partial void Failed(ILogger logger);
    }
}
