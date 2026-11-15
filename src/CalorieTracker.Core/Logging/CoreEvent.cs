namespace CalorieTracker.Core.Logging;

/// <summary>
/// Event ids of Core's log messages: 1000–1999, a block of 100 per type.
/// Reserved blocks: 1100 FoodStore, 1200 OpenFoodFactsAdapter, 1300 StoredLookup, 1400 SeedLoader, 1500 FoodSearch, 1600 TargetService, 1700 DiaryService, 1800 DaySummaryService; the <c>*Block</c> members hold them.
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

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: a stored food with no serving got one from a fresh lookup; carries the food id.</summary>
    StoredLookupServingFilled = 1307,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: the lookup for a stored food's serving gave none; carries the food id and the lookup's status.</summary>
    StoredLookupServingMissing = 1308,

    /// <summary><see cref="Foods.StoredBarcodeLookup"/>: saving a stored food's new serving failed, or the refresh threw; the stored food was returned as it was.</summary>
    StoredLookupServingFailed = 1309,

    /// <summary>Start of the block reserved for the CoFID seed loader.</summary>
    SeedLoaderBlock = 1400,

    /// <summary><see cref="Seed.SeedLoader"/>: a seed was loaded; version, inserted and updated counts.</summary>
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
}
