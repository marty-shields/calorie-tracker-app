using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;
using CalorieTracker.Core.History;

namespace CalorieTracker.UI.Stories.Stories.Screens;

/// <summary>Sample foods and groups for the food search stories.</summary>
public static class FoodSearchSamples
{
    private static readonly DateOnly Day = new(2031, 7, 19);

    public static IReadOnlyList<Food> Oats { get; } =
    [
        new() { Id = 1, Name = "Oats, rolled", Unit = FoodUnit.Gram, Kcal = 375, ProteinG = 11, CarbsG = 60, FatG = 9, FibreG = 8.5, SugarsG = 1, SaturatesG = 1.6, SaltG = 0 },
        new() { Id = 3, Name = "Porridge oats", Brand = "Quaker", Unit = FoodUnit.Gram, Kcal = 372, ProteinG = 11, CarbsG = 60, FatG = 8.5 },
        new() { Id = 5, Name = "Oat drink", Brand = "Oatly", Unit = FoodUnit.Millilitre, Kcal = 46, ProteinG = 1, CarbsG = 6.7, FatG = 1.5 },
        new() { Id = 4, Name = "Oat cereal bar", Unit = FoodUnit.Gram, Kcal = 430, ProteinG = 6, CarbsG = 64, FatG = 16, ServingLabel = "1 bar", ServingAmount = 40 },
    ];

    /// <summary>Before typing in add mode: foods usual for the meal, then recent foods.</summary>
    public static IReadOnlyList<FoodGroup> Idle { get; } =
    [
        new(FoodGroupKind.Usual,
        [
            Logged(Oats[1], 12, 40),
            Logged(Oats[2], 8, 250),
        ]),
        new(FoodGroupKind.Recent,
        [
            Logged(Oats[3], 2, 40, servings: 1),
            Logged(Oats[0], 1, 60),
        ]),
    ];

    /// <summary>Before typing on the Foods tab: recent foods only.</summary>
    public static IReadOnlyList<FoodGroup> IdleRecent { get; } = [Idle[1]];

    /// <summary>Typing "oat": a logged food, common foods and a branded food.</summary>
    public static IReadOnlyList<FoodGroup> AllGroups { get; } =
    [
        new(FoodGroupKind.Diary, [Logged(Oats[1], 12, 40), Logged(Oats[3], 2, 80, servings: 2)]),
        new(FoodGroupKind.Common, [new(Oats[0], null)]),
        new(FoodGroupKind.Branded, [new(Oats[2], null)]),
    ];

    /// <summary>Typing a word only common foods have.</summary>
    public static IReadOnlyList<FoodGroup> OneGroup { get; } = [new(FoodGroupKind.Common, [new(Oats[0], null)])];

    /// <summary>A common group of eight foods: five show, then "Show 3 more".</summary>
    public static IReadOnlyList<FoodGroup> LongGroup { get; } =
    [
        new(FoodGroupKind.Diary, [Logged(Oats[1], 12, 40)]),
        new(FoodGroupKind.Common, Enumerable.Range(1, 8).Select(i => new FoodListing(
            new Food { Id = 100 + i, Name = $"Oat flakes {i}", Unit = FoodUnit.Gram, Kcal = 360 + i, ProteinG = 11, CarbsG = 60, FatG = 8 }, null)).ToList()),
    ];

    private static FoodListing Logged(Food food, int times, double amount, double? servings = null) =>
        new(food, new FoodHistory(food.Id, times, 1, Day, amount, servings));
}
