using CalorieTracker.Core.Data;
using StoredServing = CalorieTracker.Core.Data.Serving;

namespace CalorieTracker.UI.Stories.Stories.Screens;

/// <summary>Sample foods for the Serving stories.</summary>
public static class ServingSamples
{
    /// <summary>A scanned product with a serving the person added.</summary>
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
    };

    /// <summary>The pizza's own serving.</summary>
    public static IReadOnlyList<StoredServing> PizzaServings { get; } =
    [
        new() { Id = 111, FoodId = 11, Label = "½ pizza (201.8 g)", Amount = 201.8, Source = ServingSource.User },
    ];

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

    /// <summary>A drink with a glass-sized serving and a can.</summary>
    public static Food Cola { get; } = new()
    {
        Id = 16,
        Name = "Cola",
        Brand = "Fizzo",
        Unit = FoodUnit.Millilitre,
        Kcal = 42,
        ProteinG = 0,
        CarbsG = 10.6,
        FatG = 0,
        PackageAmount = 330,
    };

    /// <summary>Two servings the person added, smallest first as the app lists them.</summary>
    public static IReadOnlyList<StoredServing> ColaServings { get; } =
    [
        new() { Id = 163, FoodId = 16, Label = "My mug", Amount = 200, Source = ServingSource.User },
        new() { Id = 161, FoodId = 16, Label = "1 glass (250 ml)", Amount = 250, Source = ServingSource.User },
    ];
}
