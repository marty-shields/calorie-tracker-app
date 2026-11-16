using CalorieTracker.Core.Data;
using CalorieTracker.Core.History;

namespace CalorieTracker.Core.Foods;

/// <summary>The lists search shows, in the order they appear: Usual and Recent before typing, then Diary, Common and Branded while typing.</summary>
public enum FoodGroupKind
{
    Usual,
    Recent,
    Diary,
    Common,
    Branded,
}

/// <summary>A food in a group, with what the diary says about it.</summary>
/// <param name="History">Set for every food the person has logged, whatever the group; null for a food never logged. Its last values depend on the meal asked for.</param>
public sealed record FoodListing(Food Food, FoodHistory? History);

/// <summary>One list of foods, best first.</summary>
public sealed record FoodGroup(FoodGroupKind Kind, IReadOnlyList<FoodListing> Foods);

/// <summary>The outcome of a grouped search or of the foods shown before typing. <see cref="Groups"/> holds only groups with foods, and is empty unless <see cref="Status"/> is <see cref="FoodSearchStatus.Found"/>.</summary>
public sealed record FoodGroupsResult
{
    private FoodGroupsResult(FoodSearchStatus status, IReadOnlyList<FoodGroup> groups)
    {
        Status = status;
        Groups = groups;
    }

    public FoodSearchStatus Status { get; }

    public IReadOnlyList<FoodGroup> Groups { get; }

    public static FoodGroupsResult Found(IReadOnlyList<FoodGroup> groups) =>
        new(FoodSearchStatus.Found, (groups ?? throw new ArgumentNullException(nameof(groups))).Where(g => g.Foods.Count > 0).ToList());

    public static FoodGroupsResult Failed() => new(FoodSearchStatus.Failed, []);
}
