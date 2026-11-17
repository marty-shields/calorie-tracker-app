namespace CalorieTracker.Core.Foods;

public enum FavouriteStatus
{
    Added,
    AlreadyFavourite,

    /// <summary>No food has the id; nothing was written.</summary>
    FoodNotFound,

    Removed,
    NotFavourite,
    Failed,
}

/// <summary>The outcome of marking or unmarking a favourite.</summary>
public sealed record FavouriteResult(FavouriteStatus Status);

public enum FavouriteReadStatus
{
    Found,
    Failed,
}

/// <summary>The outcome of asking whether one food is a favourite. <see cref="IsFavourite"/> is false unless <see cref="FavouriteReadStatus.Found"/>.</summary>
public sealed record FavouriteCheckResult
{
    private FavouriteCheckResult(FavouriteReadStatus status, bool isFavourite)
    {
        Status = status;
        IsFavourite = isFavourite;
    }

    public FavouriteReadStatus Status { get; }

    public bool IsFavourite { get; }

    public static FavouriteCheckResult Found(bool isFavourite) => new(FavouriteReadStatus.Found, isFavourite);

    public static FavouriteCheckResult Failed() => new(FavouriteReadStatus.Failed, false);
}

/// <summary>The outcome of listing the favourites. <see cref="FoodIds"/> is empty unless <see cref="FavouriteReadStatus.Found"/>.</summary>
public sealed record FavouriteIdsResult
{
    private FavouriteIdsResult(FavouriteReadStatus status, IReadOnlyList<int> foodIds)
    {
        Status = status;
        FoodIds = foodIds;
    }

    public FavouriteReadStatus Status { get; }

    /// <summary>The favourite food ids, lowest first.</summary>
    public IReadOnlyList<int> FoodIds { get; }

    public static FavouriteIdsResult Found(IReadOnlyList<int> foodIds) => new(FavouriteReadStatus.Found, foodIds ?? throw new ArgumentNullException(nameof(foodIds)));

    public static FavouriteIdsResult Failed() => new(FavouriteReadStatus.Failed, []);
}
