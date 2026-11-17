using SQLite;

namespace CalorieTracker.Core.Data;

public enum FoodSource
{
    CoFID,
    OpenFoodFacts,
}

/// <summary>What a food's nutrients are measured per 100 of.</summary>
public enum FoodUnit
{
    Gram,
    Millilitre,
}

/// <summary>The kind of food a search filters by. Stored as its number, so keep the order; schema step 4 removed Dish and renumbered Drink.</summary>
public enum FoodType
{
    Ingredient,
    Food,
    Drink,
}

/// <summary>A food and its nutrients per 100 g or 100 ml. Unique by <see cref="Source"/> and <see cref="SourceId"/>.</summary>
public class Food
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed("UX_Food_Source_SourceId", 1, Unique = true)]
    public FoodSource Source { get; set; }

    /// <summary>The id the source gives this food, such as a CoFID code or an Open Food Facts barcode.</summary>
    [NotNull, Indexed("UX_Food_Source_SourceId", 2, Unique = true)]
    public string SourceId { get; set; } = "";

    [NotNull]
    public string Name { get; set; } = "";

    public string? Brand { get; set; }

    [NotNull]
    public FoodUnit Unit { get; set; }

    public double Kcal { get; set; }

    public double ProteinG { get; set; }

    public double CarbsG { get; set; }

    public double FatG { get; set; }

    public double? FibreG { get; set; }

    public double? SugarsG { get; set; }

    public double? SaturatesG { get; set; }

    public double? SaltG { get; set; }

    /// <summary>
    /// CoFID foods get the seed's type. Open Food Facts foods: when the product has categories, Drink iff they include en:beverages (so oils and sauces sold in ml are Food); only with no usable categories does a millilitre unit make it Drink. Never Ingredient.
    /// A row stored before types existed is set the same way by schema step 3 (Open Food Facts by its unit, since no categories are stored; a CoFID row is Food until the seed refresh sets its own).
    /// </summary>
    [NotNull]
    public FoodType Type { get; set; }

    /// <summary>True once the source was asked for this food's serving and had no usable one (not found, or found without a serving), so it is not asked again. Null while unasked; an offline or rate-limited answer leaves it null.</summary>
    public bool? ServingChecked { get; set; }

    /// <summary>CoFID's own name for the food, such as "Barley, pearl, raw". Set for every CoFID food in the bundled seed; null for Open Food Facts foods and for removed CoFID foods kept for the diary.</summary>
    public string? OriginalName { get; set; }

    /// <summary>CoFID's food group code, such as "AA". Set for every CoFID food in the bundled seed; null for Open Food Facts foods and for removed CoFID foods kept for the diary.</summary>
    public string? FoodGroup { get; set; }

    /// <summary>Every brand Open Food Facts lists, such as "Ferrero, Nutella", joined with ", " in the source's order. <see cref="Brand"/> is the first. Null for CoFID and when none is listed.</summary>
    public string? Brands { get; set; }

    /// <summary>The product's generic name in the source's language, such as "Hazelnut And Cocoa Spread". Null when the source gives none.</summary>
    public string? GenericName { get; set; }

    /// <summary>The pack size as the source words it, such as "430 gram". Null when the source gives none.</summary>
    public string? PackageLabel { get; set; }

    /// <summary>The pack size in grams, or millilitres to match <see cref="Unit"/>. Above zero when set; null when the source's unit is not this food's unit.</summary>
    public double? PackageAmount { get; set; }

    /// <summary>The source's food group, such as "Sweets". Null when the source gives none or calls it unknown.</summary>
    public string? Category { get; set; }

    /// <summary>How many people have scanned the product on Open Food Facts; a measure of how well known it is. Null when the source gives none.</summary>
    public int? Popularity { get; set; }

    /// <summary>How complete the source's record is, from 0 to 1. Null when the source gives none.</summary>
    public double? Completeness { get; set; }

    /// <summary>When the source last changed the record (UTC). Null when the source gives none.</summary>
    public DateTimeOffset? SourceUpdated { get; set; }

    /// <summary>When the lookup that wrote the row ran (UTC). Null for CoFID and for an Open Food Facts food stored before this column existed, which is looked up again on its next scan.</summary>
    public DateTimeOffset? FetchedAt { get; set; }

    /// <summary>Lowercased text with accents removed, written when the food is saved. Ranking folds <see cref="Name"/>, <see cref="Brand"/>, <see cref="Brands"/>, <see cref="GenericName"/> and <see cref="Category"/> itself and does not read this.</summary>
    [NotNull]
    public string SearchText { get; set; } = "";
}
