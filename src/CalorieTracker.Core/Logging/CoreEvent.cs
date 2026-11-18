namespace CalorieTracker.Core.Logging;

/// <summary>
/// Event ids of Core's log messages: 1000–1999, a block of 100 per type.
/// Reserved blocks: 1100 FoodStore, 1200 OpenFoodFactsAdapter, 1300 StoredLookup, 1400 SeedLoader, 1500 FoodSearch (the online ones sit inside it: 1520 OnlineSearch, 1540 OnlineImport, 1550 OnlineNotice), 1600 TargetService, 1700 DiaryService, 1800 DaySummaryService, 1900 FoodService; the <c>*Block</c> members hold them. FoodHistoryService has ten ids from 1050, FoodImportService ten from 1060, FavouriteService ten from 1070 and ServingService ten from 1010 because every block of 100 is taken.
/// Ids never change and are never reused; list a retired id here and in <c>CoreEventTests</c>.
/// </summary>
internal enum CoreEvent
{
    /// <summary><see cref="Data.DatabaseInitializer"/>: stored and current schema versions.</summary>
    SchemaVersion = 1001,

    /// <summary><see cref="Data.DatabaseInitializer"/>: new database created.</summary>
    DatabaseCreated = 1002,

    /// <summary><see cref="Data.DatabaseInitializer"/>: a schema upgrade step ran.</summary>
    SchemaStepApplied = 1003,

    /// <summary>Start of the ten ids reserved for <see cref="Foods.ServingService"/>.</summary>
    ServingServiceBlock = 1010,

    /// <summary><see cref="Foods.ServingService"/>: a serving was added; carries the serving and food ids.</summary>
    ServingAdded = 1011,

    /// <summary><see cref="Foods.ServingService"/>: a serving was deleted; carries its id.</summary>
    ServingRemoved = 1012,

    /// <summary><see cref="Foods.ServingService"/>: a label or amount was rejected; none is logged.</summary>
    ServingRejected = 1013,

    /// <summary><see cref="Foods.ServingService"/>: a serving was not added because its food does not exist; carries the food id.</summary>
    ServingFoodNotFound = 1014,

    /// <summary><see cref="Foods.ServingService"/>: a serving was not added because the food has one of the person's own with that amount; carries the food id.</summary>
    ServingDuplicate = 1015,

    /// <summary><see cref="Foods.ServingService"/>: no serving of the person's own has the id; carries the id.</summary>
    ServingNotFound = 1016,

    /// <summary><see cref="Foods.ServingService"/>: the database failed, so the call returned <see cref="Foods.ServingStatus.Failed"/>.</summary>
    ServingFailed = 1017,

    /// <summary>Start of the block reserved for <see cref="Data.FoodStore"/>.</summary>
    FoodStoreBlock = 1100,

    /// <summary><see cref="Data.FoodStore"/>: a food was inserted.</summary>
    FoodInserted = 1101,

    /// <summary><see cref="Data.FoodStore"/>: a food was updated.</summary>
    FoodUpdated = 1102,

    /// <summary>Start of the block reserved for the Open Food Facts adapter.</summary>
    OpenFoodFactsAdapterBlock = 1200,

    /// <summary>Open Food Facts adapter: a request is about to be sent; the attempt number is logged.</summary>
    LookupAttempted = 1201,

    /// <summary>Open Food Facts adapter: a product with every required value was found.</summary>
    LookupFound = 1202,

    /// <summary>Open Food Facts adapter: no usable product (missing, or a required value absent).</summary>
    LookupNotFound = 1203,

    /// <summary>Open Food Facts adapter: rate limited, waiting before the next attempt.</summary>
    LookupRetryWaiting = 1204,

    /// <summary>Open Food Facts adapter: still rate limited after the last attempt.</summary>
    LookupRateLimited = 1205,

    /// <summary>Open Food Facts adapter: no response (network error or timeout).</summary>
    LookupNoResponse = 1206,

    /// <summary>Open Food Facts adapter: the body was not the expected JSON.</summary>
    LookupMalformedResponse = 1207,

    /// <summary>Open Food Facts adapter: an HTTP status treated as offline (5xx, or a 4xx other than 404 and 429).</summary>
    LookupUnexpectedStatus = 1208,

    /// <summary>Start of the block reserved for the stored barcode lookup.</summary>
    StoredLookupBlock = 1300,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: a barcode was served from the food table.</summary>
    StoredLookupStoreHit = 1301,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: the inner lookup answered; carries its status and, when saved, the food id.</summary>
    StoredLookupFetched = 1302,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: a not-found barcode was remembered.</summary>
    StoredLookupNotFoundRemembered = 1303,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: a remembered not-found barcode was answered without a lookup.</summary>
    StoredLookupNotFoundHit = 1304,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: reading the food table failed; the inner lookup answered instead.</summary>
    StoredLookupReadFailed = 1305,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: saving the inner lookup's food failed; the unsaved food was returned.</summary>
    StoredLookupSaveFailed = 1306,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: a stored food with no fetch time was refreshed from a fresh lookup; carries the food id.</summary>
    StoredLookupRefreshed = 1307,

    // 1308 is retired: it logged a lookup that gave a stored food no serving; the number is not reused.

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: the refresh of a stored food threw; the stored food was returned as it was.</summary>
    StoredLookupRefreshFailed = 1309,

    /// <summary>Start of the block reserved for the CoFID seed loader.</summary>
    SeedLoaderBlock = 1400,

    /// <summary><see cref="Seed.SeedLoader"/>: a seed was loaded; version, inserted, updated and deleted counts.</summary>
    SeedLoaded = 1401,

    /// <summary><see cref="Seed.SeedLoader"/>: the stored seed is as new as the bundled one, so nothing was loaded.</summary>
    SeedUpToDate = 1402,

    /// <summary><see cref="Seed.SeedLoader"/>: loading failed; nothing is recorded as loaded, so the next start retries.</summary>
    SeedLoadFailed = 1403,

    /// <summary>Start of the block reserved for <see cref="Foods.FoodSearch"/>.</summary>
    FoodSearchBlock = 1500,

    /// <summary><see cref="Foods.FoodSearch"/>: a search ran; term and result counts, never the text.</summary>
    FoodSearched = 1501,

    /// <summary><see cref="Foods.FoodSearch"/>: the store failed, so the search returned <see cref="Foods.FoodSearchStatus.Failed"/>.</summary>
    FoodSearchFailed = 1502,

    /// <summary><see cref="Foods.FoodSearch"/>: a grouped search ran; term count and the number of foods in each group, never the text.</summary>
    FoodSearchGrouped = 1503,

    /// <summary><see cref="Foods.FoodSearch"/>: the foods shown before typing were read; the number of foods in each group.</summary>
    FoodSuggested = 1504,

    /// <summary>Start of the block reserved for <see cref="Foods.OnlineFoodSearch"/>.</summary>
    OnlineSearchBlock = 1520,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: a request was sent; carries whether it was worldwide, never the text.</summary>
    OnlineSearchSent = 1521,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: the same search was answered from the session cache; carries the product count.</summary>
    OnlineSearchCached = 1522,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: a response was read; carries the hits, the products kept and the hits skipped or collapsed.</summary>
    OnlineSearchAnswered = 1523,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: a request waited for the minimum gap after the last one; carries the wait in milliseconds.</summary>
    OnlineSearchWaiting = 1524,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: the country search gave nothing to show, so it was repeated worldwide.</summary>
    OnlineSearchWorldwideRepeat = 1525,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: the service answered HTTP 429.</summary>
    OnlineSearchLimited = 1526,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: no response (network error or timeout).</summary>
    OnlineSearchNoResponse = 1527,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: the body was not the expected JSON.</summary>
    OnlineSearchMalformed = 1528,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: an HTTP status other than success and 429; carries it.</summary>
    OnlineSearchUnexpectedStatus = 1529,

    /// <summary><see cref="Foods.OnlineFoodSearch"/>: reading which products are stored failed, so none is marked new.</summary>
    OnlineSearchStoredCheckFailed = 1530,

    /// <summary>Start of the block reserved for <see cref="Foods.OnlineFoodImport"/>.</summary>
    OnlineImportBlock = 1540,

    /// <summary><see cref="Foods.OnlineFoodImport"/>: a tapped product is stored; carries the food id and how it was stored.</summary>
    OnlineImported = 1541,

    /// <summary><see cref="Foods.OnlineFoodImport"/>: the barcode lookup gave no product; carries its status.</summary>
    OnlineImportLookupFailed = 1542,

    /// <summary><see cref="Foods.OnlineFoodImport"/>: the product could not be stored; carries the import status.</summary>
    OnlineImportStoreFailed = 1543,

    /// <summary>Start of the block reserved for <see cref="Foods.OnlineSearchNotice"/>.</summary>
    OnlineNoticeBlock = 1550,

    /// <summary><see cref="Foods.OnlineSearchNotice"/>: reading whether the notice was shown failed.</summary>
    OnlineNoticeReadFailed = 1551,

    /// <summary><see cref="Foods.OnlineSearchNotice"/>: saving that the notice was shown failed.</summary>
    OnlineNoticeSaveFailed = 1552,

    /// <summary>Start of the block reserved for <see cref="Targets.TargetService"/>.</summary>
    TargetServiceBlock = 1600,

    /// <summary><see cref="Targets.TargetService"/>: a target was saved; no value is logged.</summary>
    TargetSaved = 1601,

    /// <summary><see cref="Targets.TargetService"/>: the target was cleared.</summary>
    TargetCleared = 1602,

    /// <summary><see cref="Targets.TargetService"/>: a value was rejected as not a whole number above zero; the value is not logged.</summary>
    TargetRejected = 1603,

    /// <summary><see cref="Targets.TargetService"/>: the database failed, so the call returned <see cref="Targets.TargetStatus.Failed"/>.</summary>
    TargetFailed = 1604,

    /// <summary><see cref="Targets.TargetService"/>: the target of one day was read.</summary>
    TargetDateRead = 1605,

    /// <summary><see cref="Targets.TargetService"/>: the targets of a range of days were read; the log holds the day count only.</summary>
    TargetRangeRead = 1606,

    /// <summary><see cref="Targets.TargetService"/>: a range with its end before its start, or longer than the maximum, was rejected.</summary>
    TargetRangeRejected = 1607,

    /// <summary>Start of the block reserved for <see cref="Diary.DiaryService"/>.</summary>
    DiaryServiceBlock = 1700,

    /// <summary><see cref="Diary.DiaryService"/>: an entry was added; carries the entry and food ids.</summary>
    DiaryEntryAdded = 1701,

    /// <summary><see cref="Diary.DiaryService"/>: an entry was updated; carries its id.</summary>
    DiaryEntryUpdated = 1702,

    /// <summary><see cref="Diary.DiaryService"/>: an entry was removed; carries its id.</summary>
    DiaryEntryRemoved = 1703,

    /// <summary><see cref="Diary.DiaryService"/>: an entry was restored; carries its id.</summary>
    DiaryEntryRestored = 1704,

    /// <summary><see cref="Diary.DiaryService"/>: a day was listed; carries the entry count, never the date.</summary>
    DiaryListed = 1705,

    /// <summary><see cref="Diary.DiaryService"/>: values were rejected; none is logged.</summary>
    DiaryRejected = 1706,

    /// <summary><see cref="Diary.DiaryService"/>: no entry with the id was in the state the call needs; carries the id.</summary>
    DiaryEntryNotFound = 1707,

    /// <summary><see cref="Diary.DiaryService"/>: an entry was not added because its food does not exist; carries the food id.</summary>
    DiaryFoodNotFound = 1708,

    /// <summary><see cref="Diary.DiaryService"/>: the database failed, so the call returned <see cref="Diary.DiaryStatus.Failed"/>.</summary>
    DiaryFailed = 1709,

    /// <summary>Start of the block reserved for <see cref="Summary.DaySummaryService"/>.</summary>
    DaySummaryServiceBlock = 1800,

    /// <summary><see cref="Summary.DaySummaryService"/>: a summary was built; carries the entry count, never the date.</summary>
    DaySummaryBuilt = 1801,

    /// <summary><see cref="Summary.DaySummaryService"/>: an entry's food no longer exists, so the call returned <see cref="Summary.DaySummaryStatus.FoodMissing"/>; carries the entry and food ids.</summary>
    DaySummaryFoodMissing = 1802,

    /// <summary><see cref="Summary.DaySummaryService"/>: an unexpected failure (the database, say), so the call returned <see cref="Summary.DaySummaryStatus.Failed"/>.</summary>
    DaySummaryFailed = 1803,

    /// <summary><see cref="Summary.DaySummaryService"/>: an amount or total was too large to work out, so the call returned <see cref="Summary.DaySummaryStatus.Failed"/>.</summary>
    DaySummaryOverflow = 1804,

    /// <summary><see cref="Summary.DaySummaryService"/>: an entry holds a meal that is not one of the four, so the call returned <see cref="Summary.DaySummaryStatus.Failed"/>; carries the entry id.</summary>
    DaySummaryMealUnknown = 1805,

    /// <summary>Start of the block reserved for <see cref="History.FoodHistoryService"/>.</summary>
    FoodHistoryServiceBlock = 1050,

    /// <summary><see cref="History.FoodHistoryService"/>: recent foods were read; carries the food count.</summary>
    FoodHistoryRecent = 1051,

    /// <summary><see cref="History.FoodHistoryService"/>: usual foods were read; carries the food count, never the meal.</summary>
    FoodHistoryUsual = 1052,

    /// <summary><see cref="History.FoodHistoryService"/>: the history of every logged food was read in one call, for search; carries the food count.</summary>
    FoodHistoryForFoods = 1053,

    /// <summary><see cref="History.FoodHistoryService"/>: the database failed, so the call returned <see cref="History.FoodHistoryStatus.Failed"/>.</summary>
    FoodHistoryFailed = 1054,

    /// <summary><see cref="History.FoodHistoryService"/>: the history of the given foods was read; carries the count of foods that had history, never the ids asked for.</summary>
    FoodHistoryByIds = 1055,

    /// <summary>Start of the ten ids reserved for <see cref="Foods.FoodImportService"/>.</summary>
    FoodImportServiceBlock = 1060,

    /// <summary><see cref="Foods.FoodImportService"/>: a new food was stored; carries the food id.</summary>
    FoodImportCreated = 1061,

    /// <summary><see cref="Foods.FoodImportService"/>: a barcode was added to a stored food that is the same product; carries the food id.</summary>
    FoodImportAttached = 1062,

    /// <summary><see cref="Foods.FoodImportService"/>: the barcode or product was stored already; carries the food id.</summary>
    FoodImportKnown = 1063,

    /// <summary><see cref="Foods.FoodImportService"/>: the product lacked a required value, so nothing was stored; carries the reason, never the product.</summary>
    FoodImportNotImportable = 1064,

    /// <summary><see cref="Foods.FoodImportService"/>: the database failed, so the call returned <see cref="Foods.FoodImportStatus.Failed"/>.</summary>
    FoodImportFailed = 1065,

    /// <summary>Start of the ten ids reserved for <see cref="Foods.FavouriteService"/>.</summary>
    FavouriteServiceBlock = 1070,

    /// <summary><see cref="Foods.FavouriteService"/>: a food was marked as a favourite; carries the food id.</summary>
    FavouriteAdded = 1071,

    /// <summary><see cref="Foods.FavouriteService"/>: the food was a favourite already; carries the food id.</summary>
    FavouriteAlreadyFavourite = 1072,

    /// <summary><see cref="Foods.FavouriteService"/>: no food has the id, so nothing was written; carries the food id.</summary>
    FavouriteFoodNotFound = 1073,

    /// <summary><see cref="Foods.FavouriteService"/>: a favourite was unmarked; carries the food id.</summary>
    FavouriteRemoved = 1074,

    /// <summary><see cref="Foods.FavouriteService"/>: the food was not a favourite; carries the food id.</summary>
    FavouriteNotFavourite = 1075,

    /// <summary><see cref="Foods.FavouriteService"/>: the favourites were listed; carries the count.</summary>
    FavouriteListed = 1076,

    /// <summary><see cref="Foods.FavouriteService"/>: the database failed, so the call returned a failed status.</summary>
    FavouriteFailed = 1077,

    /// <summary>Start of the block reserved for <see cref="Foods.FoodService"/>.</summary>
    FoodServiceBlock = 1900,

    /// <summary><see cref="Foods.FoodService"/>: a stored serving was out of range, so the food was returned with no serving; carries the food id.</summary>
    FoodServingOutOfRange = 1901,

    /// <summary><see cref="Foods.FoodService"/>: stored optional nutrients were out of range, so they were returned as unknown; carries the food id and how many.</summary>
    FoodNutrientsOutOfRange = 1902,

    /// <summary><see cref="Foods.FoodService"/>: no food has the id; carries the id.</summary>
    FoodNotFound = 1903,

    /// <summary><see cref="Foods.FoodService"/>: the database failed, so the call returned <see cref="Foods.FoodStatus.Failed"/>.</summary>
    FoodReadFailed = 1904,
}
