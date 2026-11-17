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
        new() { Id = 4, Name = "Oat cereal bar", Unit = FoodUnit.Gram, Kcal = 430, ProteinG = 6, CarbsG = 64, FatG = 16 },
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

    /// <summary>Branded foods whose names leave out, repeat or share the brand: Weetabix Original, Nutella, Mars and Coca Cola.</summary>
    public static IReadOnlyList<Food> BrandedFoods { get; } =
    [
        new() { Id = 21, Name = "Original", Brand = "Weetabix", Brands = "Weetabix", PackageLabel = "430 gram", Unit = FoodUnit.Gram, Kcal = 362, ProteinG = 11.5, CarbsG = 69, FatG = 2, GenericName = "Wholegrain wheat biscuits" },
        new() { Id = 22, Name = "Nutella", Brand = "Ferrero", Brands = "Ferrero, Nutella", PackageLabel = "400 g e", Unit = FoodUnit.Gram, Kcal = 539, ProteinG = 6.3, CarbsG = 57.5, FatG = 30.9, GenericName = "Hazelnut spread with cocoa" },
        new() { Id = 23, Name = "Mars", Brand = "Mars", Brands = "Mars, Mars Wrigley", PackageLabel = "51 g", Unit = FoodUnit.Gram, Kcal = 449, ProteinG = 4, CarbsG = 69, FatG = 17 },
        new() { Id = 24, Name = "Coca Cola", Brand = "Coca-Cola", Brands = "Coca-Cola", PackageLabel = "330 ml", Unit = FoodUnit.Millilitre, Kcal = 42, ProteinG = 0, CarbsG = 10.6, FatG = 0 },
    ];

    /// <summary>Typing "mars": only branded foods, each shown as title, then brand line, pack and energy.</summary>
    public static IReadOnlyList<FoodGroup> BrandedGroup { get; } = [new(FoodGroupKind.Branded, BrandedFoods.Select(f => new FoodListing(f, null)).ToList())];

    /// <summary>Typing a word only common foods have.</summary>
    public static IReadOnlyList<FoodGroup> OneGroup { get; } = [new(FoodGroupKind.Common, [new(Oats[0], null)])];

    /// <summary>A common group of eight foods: five show, then "Show 3 more".</summary>
    public static IReadOnlyList<FoodGroup> LongGroup { get; } =
    [
        new(FoodGroupKind.Diary, [Logged(Oats[1], 12, 40)]),
        new(FoodGroupKind.Common, Enumerable.Range(1, 8).Select(i => new FoodListing(
            new Food { Id = 100 + i, Name = $"Oat flakes {i}", Unit = FoodUnit.Gram, Kcal = 360 + i, ProteinG = 11, CarbsG = 60, FatG = 8 }, null)).ToList()),
    ];

    /// <summary>Typing "cola", all the local group the foods already stored have, before Open Food Facts is searched.</summary>
    public static IReadOnlyList<FoodGroup> ColaStored { get; } = [new(FoodGroupKind.Branded, [new(BrandedFoods[3], null)])];

    /// <summary>What Open Food Facts offered for "coca-cola": eight products, the first stored already (no NEW marker), the rest new; five show, then "Show 3 more".</summary>
    public static IReadOnlyList<OnlineProduct> OnlineColas { get; } =
    [
        Online("5000112637922", "Coca Cola", "Coca-Cola", "330 ml", FoodUnit.Millilitre, 42, 0, 10.6, 0, isNew: false),
        Online("5000112641981", "Coca Cola", "Coca-Cola", "1.5L", FoodUnit.Millilitre, 42, 0, 10.6, 0),
        Online("5449000028921", "Coca-Cola", "Coca-Cola", "1,25 l", FoodUnit.Millilitre, 42, 0, 10.6, 0),
        Online("5449000043757", "Coca cola zero sugar", "Coca-Cola", "1,5l", FoodUnit.Millilitre, 0.3, 0, 0, 0),
        Online("5449000050205", "Coca-Cola Light", "Coca-Cola", "330 ml", FoodUnit.Millilitre, 0.2, 0, 0, 0),
        Online("5000112662115", "Coca-Cola Cherry", "Coca-Cola", "330 ml", FoodUnit.Millilitre, 46, 0, 11.2, 0),
        Online("5449000059727", "Coca-Cola Lemon", "Coca-Cola", "330 ml", FoodUnit.Millilitre, 43.2, 0, 10.8, 0),
        Online("5000328200453", "Coca Cola flavour crisps", "Walkers", "70 g", FoodUnit.Gram, 532, 6.2, 51, 33),
    ];

    /// <summary>Three products, so no "Show N more" row.</summary>
    public static IReadOnlyList<OnlineProduct> OnlineFew { get; } = [OnlineColas[0], OnlineColas[1], OnlineColas[3]];

    private static OnlineProduct Online(string barcode, string name, string brand, string pack, FoodUnit unit, double kcal, double protein, double carbs, double fat, bool isNew = true) =>
        new(barcode, new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = barcode,
            Name = name,
            Brand = brand,
            Brands = brand,
            PackageLabel = pack,
            Unit = unit,
            Kcal = kcal,
            ProteinG = protein,
            CarbsG = carbs,
            FatG = fat,
        }, isNew);

    private static FoodListing Logged(Food food, int times, double amount, double? servings = null) =>
        new(food, new FoodHistory(food.Id, times, 1, Day, amount, servings));
}
