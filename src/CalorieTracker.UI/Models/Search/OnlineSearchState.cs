namespace CalorieTracker.UI.Models.Search;

/// <summary>Where the online part of the search screen is. Nothing is sent until the person taps, so a search starts at <see cref="Idle"/>.</summary>
public enum OnlineSearchState
{
    /// <summary>Nothing sent: the "Search online" row, or the button when no stored food matches.</summary>
    Idle,

    /// <summary>A search is running: a spinner.</summary>
    Searching,

    /// <summary>A result was tapped and its product is being fetched and stored: the results with a spinner.</summary>
    Importing,

    /// <summary>The results, as a group under the local ones.</summary>
    Results,

    /// <summary>The search ran and found nothing usable.</summary>
    None,

    /// <summary>The online search answered HTTP 429: "The online search is busy. Try again in a minute." with Try again.</summary>
    Busy,

    /// <summary>No response: "You are offline" with Try again.</summary>
    Offline,

    /// <summary>Any other failure: "Couldn't reach the online search" with Try again.</summary>
    Failed,

    /// <summary>A tapped product has no usable data online: "That product can't be added" without Try again, the results stay.</summary>
    Unavailable,
}
