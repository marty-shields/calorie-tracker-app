namespace CalorieTracker.UI.Logging;

/// <summary>
/// Event ids of the UI's log messages: 2000–4999, a block of 100 per type. Values never change once logged.
/// Ids are never reused; list a retired id here and in <c>EventIdTests</c>.
/// </summary>
internal enum UiEvent
{
    /// <summary><see cref="Components.Layout.StartupGate"/>: startup finished, with its duration.</summary>
    StartupFinished = 2001,

    /// <summary><see cref="Components.Layout.StartupGate"/>: startup failed.</summary>
    StartupFailed = 2002,

    /// <summary><see cref="Components.Layout.AppErrorBoundary"/>: unhandled error in a screen.</summary>
    ScreenError = 2101,

    /// <summary><see cref="Navigation.BackButton"/>: back handled by the page.</summary>
    BackHandled = 2201,

    /// <summary><see cref="Navigation.BackButton"/>: back pressed with no handler.</summary>
    BackNotHandled = 2202,

    /// <summary><see cref="Navigation.BackButton"/>: a back handler failed.</summary>
    BackHandlerFailed = 2203,

    /// <summary><see cref="Components.Layout.AddSheet"/>: the sheet opened, with the meal it guessed.</summary>
    AddSheetOpened = 2301,

    /// <summary><see cref="Components.Layout.AddSheet"/>: Search foods, Scan barcode or a dismissal, with the meal chosen.</summary>
    AddSheetChose = 2302,

    /// <summary><see cref="Components.Layout.AddSheet"/>: keeping focus in the sheet or returning it to Add failed; the sheet still works.</summary>
    AddSheetFocusFailed = 2303,

    /// <summary><see cref="Screens.UsualFoodsFeed"/>: the usual foods for the Add sheet were read; carries the meal and the count of rows.</summary>
    UsualRead = 2304,

    /// <summary><see cref="Screens.UsualFoodsFeed"/>: the usual foods could not be read, so the sheet hides them; carries the meal.</summary>
    UsualReadFailed = 2305,

    /// <summary><see cref="Screens.UsualFoodsFeed"/>: a usual food was added from the sheet; carries the food id and the meal.</summary>
    UsualAdded = 2306,

    /// <summary><see cref="Screens.UsualFoodsFeed"/>: adding a usual food failed, so the sheet stays open; carries the food id, the meal and the status.</summary>
    UsualAddFailed = 2307,

    /// <summary><see cref="Screens.UsualFoodsFeed"/>: Undo could not remove the usual food just added; carries the status.</summary>
    UsualUndoFailed = 2308,

    /// <summary><see cref="Screens.TrendsPage"/>: a period's trends were read; carries the period and the number of days.</summary>
    TrendsLoaded = 2401,

    /// <summary><see cref="Screens.TrendsPage"/>: a period's trends could not be read, so the screen offers Try again; carries the period.</summary>
    TrendsLoadFailed = 2402,

    /// <summary><see cref="Screens.TrendsPage"/>: a day, week or month was opened from the screen; carries what was opened (day, week or month) and the period it was opened from.</summary>
    TrendsOpened = 2403,

    /// <summary><see cref="Lifecycle.AppLifecycle"/>: an app start handler failed.</summary>
    AppStartHandlerFailed = 3201,

    // 3301 is retired: LoadFailed moved to the shared kit, which logs it as KitEvent.LoadFailedFocusFailed.

    /// <summary><see cref="Screens.SettingsPage"/>: reading the saved theme failed.</summary>
    ThemeLoadFailed = 3403,

    /// <summary><see cref="Screens.SettingsPage"/>: saving the chosen theme failed.</summary>
    ThemeSaveFailed = 3404,

    /// <summary><see cref="Screens.SettingsPage"/>: storage refused the chosen theme, so it is lost at the next start.</summary>
    ThemeNotSaved = 3405,

    /// <summary><see cref="Screens.SettingsPage"/>: reading the daily target failed, so the field shows empty.</summary>
    TargetLoadFailed = 3406,

    /// <summary><see cref="Screens.SettingsPage"/>: saving or clearing the daily target failed, so the last saved one stays.</summary>
    TargetSaveFailed = 3407,

    /// <summary><see cref="Screens.FoodSearchPage"/>: the search waited for the food seed, with how it ended.</summary>
    FoodSeedWaited = 3501,

    // 3502 is retired: the search failure is logged by Core (CoreEvent.FoodSearchFailed).

    /// <summary><see cref="Screens.FoodSearchPage"/>: Try again restarted the food seed load.</summary>
    FoodSeedRetried = 3503,

    /// <summary><see cref="Screens.ScanPage"/>: a barcode was accepted and its lookup started.</summary>
    ScanLookupStarted = 3601,

    /// <summary><see cref="Screens.ScanPage"/>: a barcode lookup finished, with its status.</summary>
    ScanLookupFinished = 3602,

    /// <summary><see cref="Screens.ScanPage"/>: the scanner ended without a barcode because the camera is unavailable.</summary>
    ScanCameraUnavailable = 3603,

    // 3701 is retired: NumberField moved to the shared kit, which logs it as KitEvent.NumberFieldFocusFailed.

    /// <summary><see cref="Screens.ServingPage"/>: Undo could not remove the entry just added, with the status.</summary>
    UndoFailed = 3801,

    /// <summary><see cref="Screens.ServingPage"/>: Undo could not restore the entry just removed, with the status.</summary>
    RestoreFailed = 3802,

    /// <summary><see cref="Screens.FavouriteStar"/>: reading whether the food is a favourite failed, so no star shows.</summary>
    FavouriteReadFailed = 3901,

    /// <summary><see cref="Screens.FavouriteStar"/>: starring or unstarring the food failed, so the star stays as it was.</summary>
    FavouriteWriteFailed = 3902,

    /// <summary><see cref="Components.Layout.AppScreen"/>: setting up pull to refresh failed, so the screen can't be pulled.</summary>
    PullRefreshFailed = 4001,
}
