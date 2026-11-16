using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Stories.Stories.Screens;

/// <summary>Sample foods for the food search stories: one mixed list, as search returns it.</summary>
public static class FoodSearchSamples
{
    public static IReadOnlyList<Food> Oats { get; } =
    [
        new() { Id = 1, Name = "Oats, rolled", Unit = FoodUnit.Gram, Kcal = 375, ProteinG = 11, CarbsG = 60, FatG = 9, FibreG = 8.5, SugarsG = 1, SaturatesG = 1.6, SaltG = 0 },
        new() { Id = 3, Name = "Porridge oats", Brand = "Quaker", Unit = FoodUnit.Gram, Kcal = 372, ProteinG = 11, CarbsG = 60, FatG = 8.5 },
        new() { Id = 5, Name = "Oat drink", Brand = "Oatly", Unit = FoodUnit.Millilitre, Kcal = 46, ProteinG = 1, CarbsG = 6.7, FatG = 1.5 },
        new() { Id = 4, Name = "Oat cereal bar", Unit = FoodUnit.Gram, Kcal = 430, ProteinG = 6, CarbsG = 64, FatG = 16 },
    ];
}
