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

    /// <summary><see cref="Lifecycle.AppLifecycle"/>: an app start handler failed.</summary>
    AppStartHandlerFailed = 3201,

    /// <summary><see cref="Components.Feedback.LoadFailed"/>: focusing Try again failed.</summary>
    LoadFailedFocusFailed = 3301,

    /// <summary><see cref="Screens.SettingsPage"/>: reading the saved theme failed.</summary>
    ThemeLoadFailed = 3403,

    /// <summary><see cref="Screens.SettingsPage"/>: saving the chosen theme failed.</summary>
    ThemeSaveFailed = 3404,

    /// <summary><see cref="Screens.SettingsPage"/>: storage refused the chosen theme, so it is lost at the next start.</summary>
    ThemeNotSaved = 3405,

    /// <summary><see cref="Screens.FoodSearchPage"/>: the search waited for the food seed, with how it ended.</summary>
    FoodSeedWaited = 3501,

    /// <summary><see cref="Screens.FoodSearchPage"/>: a food search failed.</summary>
    FoodSearchFailed = 3502,

    /// <summary><see cref="Screens.FoodSearchPage"/>: Try again restarted the food seed load.</summary>
    FoodSeedRetried = 3503,

    /// <summary><see cref="Components.Layout.AppScreen"/>: setting up pull to refresh failed, so the screen can't be pulled.</summary>
    PullRefreshFailed = 4001,
}
