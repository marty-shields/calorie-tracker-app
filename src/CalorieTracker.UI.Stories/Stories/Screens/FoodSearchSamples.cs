using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Stories.Stories.Screens;

/// <summary>Sample foods for the food search stories, one list per chip.</summary>
public static class FoodSearchSamples
{
    public static IReadOnlyList<Food> Ingredients { get; } =
    [
        new() { Id = 1, Name = "Oats, rolled", Type = FoodType.Ingredient, Unit = FoodUnit.Gram, Kcal = 375, ProteinG = 11, CarbsG = 60, FatG = 9, FibreG = 8.5, SugarsG = 1, SaturatesG = 1.6, SaltG = 0 },
        new() { Id = 2, Name = "Oatmeal", Type = FoodType.Ingredient, Unit = FoodUnit.Gram, Kcal = 401, ProteinG = 12.1, CarbsG = 72, FatG = 8.7 },
    ];

    public static IReadOnlyList<Food> Foods { get; } =
    [
        new() { Id = 3, Name = "Porridge oats", Brand = "Quaker", Type = FoodType.Food, Unit = FoodUnit.Gram, Kcal = 372, ProteinG = 11, CarbsG = 60, FatG = 8.5 },
        new() { Id = 4, Name = "Oat cereal bar", Type = FoodType.Food, Unit = FoodUnit.Gram, Kcal = 430, ProteinG = 6, CarbsG = 64, FatG = 16 },
    ];

    public static IReadOnlyList<Food> Dishes { get; } =
    [
        new() { Id = 5, Name = "Oat porridge, made with milk", Type = FoodType.Dish, Unit = FoodUnit.Gram, Kcal = 116, ProteinG = 4.8, CarbsG = 13, FatG = 5 },
        new() { Id = 6, Name = "Oat flapjack", Type = FoodType.Dish, Unit = FoodUnit.Gram, Kcal = 484, ProteinG = 5.4, CarbsG = 60, FatG = 26 },
    ];

    public static IReadOnlyList<Food> Drinks { get; } =
    [
        new() { Id = 7, Name = "Oat drink", Brand = "Oatly", Type = FoodType.Drink, Unit = FoodUnit.Millilitre, Kcal = 46, ProteinG = 1, CarbsG = 6.7, FatG = 1.5 },
    ];

    public static IReadOnlyList<Food> All { get; } = [.. Ingredients, .. Foods, .. Dishes, .. Drinks];
}
