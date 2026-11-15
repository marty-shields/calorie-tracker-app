using CalorieTracker.Core.Data;

namespace CalorieTracker.UI.Stories.Stories.Screens;

/// <summary>Sample foods for the Serving stories.</summary>
public static class ServingSamples
{
    /// <summary>A scanned product with a serving.</summary>
    public static Food Pizza { get; } = new()
    {
        Id = 11,
        Name = "Margherita pizza",
        Brand = "Casa Roma",
        Type = FoodType.Food,
        Unit = FoodUnit.Gram,
        Kcal = 258,
        ProteinG = 11.2,
        CarbsG = 31.5,
        FatG = 9.9,
        ServingLabel = "½ pizza (201.8 g)",
        ServingAmount = 201.8,
    };

    /// <summary>A scanned product with no serving.</summary>
    public static Food Biscuits { get; } = new()
    {
        Id = 12,
        Name = "Digestive biscuits",
        Brand = "Morning Mill",
        Type = FoodType.Food,
        Unit = FoodUnit.Gram,
        Kcal = 487,
        ProteinG = 7,
        CarbsG = 62,
        FatG = 22,
    };

    /// <summary>A CoFID ingredient, which has no serving.</summary>
    public static Food Cheddar { get; } = new()
    {
        Id = 13,
        Name = "Cheddar cheese",
        Type = FoodType.Ingredient,
        Unit = FoodUnit.Gram,
        Kcal = 416,
        ProteinG = 25.4,
        CarbsG = 0.1,
        FatG = 34.9,
    };

    /// <summary>A liquid, measured in ml.</summary>
    public static Food Milk { get; } = new()
    {
        Id = 14,
        Name = "Semi-skimmed milk",
        Type = FoodType.Drink,
        Unit = FoodUnit.Millilitre,
        Kcal = 47,
        ProteinG = 3.5,
        CarbsG = 4.8,
        FatG = 1.7,
    };
}
