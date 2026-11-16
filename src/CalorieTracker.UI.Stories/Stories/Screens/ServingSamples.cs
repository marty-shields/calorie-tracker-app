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
        Unit = FoodUnit.Gram,
        Kcal = 258,
        ProteinG = 11.2,
        CarbsG = 31.5,
        FatG = 9.9,
        ServingLabel = "½ pizza (201.8 g)",
        ServingAmount = 201.8,
    };

    /// <summary>A single-portion pack with no serving of its own: the Serving screen offers its pack as "1 serving".</summary>
    public static Food Mars { get; } = new()
    {
        Id = 15,
        Name = "Mars",
        Brand = "Mars",
        Brands = "Mars, Mars Wrigley",
        PackageLabel = "51 g",
        Unit = FoodUnit.Gram,
        Kcal = 449,
        ProteinG = 4,
        CarbsG = 69,
        FatG = 17,
        PackageAmount = 51,
        ServingLabel = "1 serving",
        ServingAmount = 51,
    };

    /// <summary>A scanned product with no serving.</summary>
    public static Food Biscuits { get; } = new()
    {
        Id = 12,
        Name = "Digestive biscuits",
        Brand = "Morning Mill",
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
        Unit = FoodUnit.Millilitre,
        Kcal = 47,
        ProteinG = 3.5,
        CarbsG = 4.8,
        FatG = 1.7,
    };
}
