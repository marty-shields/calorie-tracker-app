namespace CalorieTracker.Core.Logging;

/// <summary>
/// Event ids of Core's log messages: 1000–1999, a block of 100 per type.
/// Reserved blocks: 1100 FoodStore, 1200 OpenFoodFactsAdapter, 1300 StoredLookup, 1400 SeedLoader; the <c>*Block</c> members hold them.
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

    /// <summary>Start of the block reserved for the CoFID seed loader.</summary>
    SeedLoaderBlock = 1400,
}
