using CalorieTracker.Core.Data;
using StoredServing = CalorieTracker.Core.Data.Serving;

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
    };

    /// <summary>The pizza's own serving.</summary>
    public static IReadOnlyList<StoredServing> PizzaServings { get; } =
    [
        new() { Id = 111, FoodId = 11, Label = "½ pizza (201.8 g)", Amount = 201.8, Source = ServingSource.Product },
    ];

    /// <summary>A single-portion pack with no serving of its own: its pack is its only serving, shown as "1 serving".</summary>
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
    };

    /// <summary>The pack as the only serving, as <c>FoodService</c> words it.</summary>
    public static IReadOnlyList<StoredServing> MarsServings { get; } =
    [
        new() { Id = 151, FoodId = 15, Label = "1 serving", Amount = 51, Source = ServingSource.Pack },
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

    /// <summary>The cola's own serving and its can.</summary>
    public static IReadOnlyList<StoredServing> ColaServings { get; } =
    [
        new() { Id = 161, FoodId = 16, Label = "1 glass (250 ml)", Amount = 250, Source = ServingSource.Product },
        new() { Id = 162, FoodId = 16, Label = "Whole pack (330 ml)", Amount = 330, Source = ServingSource.Pack },
    ];

    /// <summary>The cola's servings with one the person added.</summary>
    public static IReadOnlyList<StoredServing> ColaServingsWithOwn { get; } =
    [
        .. ColaServings,
        new() { Id = 163, FoodId = 16, Label = "My mug", Amount = 200, Source = ServingSource.User },
    ];
}
