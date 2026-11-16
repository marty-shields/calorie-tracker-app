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

    /// <summary>How Open Food Facts words one serving, such as "0.5 pizza (201.8 g)". Null when the source gives none (CoFID never does); set together with <see cref="ServingAmount"/>.</summary>
    public string? ServingLabel { get; set; }

    /// <summary>The serving's weight in grams, or volume in millilitres to match <see cref="Unit"/>. Above zero when set.</summary>
    public double? ServingAmount { get; set; }

    /// <summary>True once the source was asked for this food's serving and had no usable one (not found, or found without a serving), so it is not asked again. Null while unasked; an offline or rate-limited answer leaves it null.</summary>
    public bool? ServingChecked { get; set; }

    /// <summary>CoFID's own name for the food, such as "Barley, pearl, raw". Set for every CoFID food in the bundled seed; null for Open Food Facts foods and for removed CoFID foods kept for the diary.</summary>
    public string? OriginalName { get; set; }

    /// <summary>CoFID's food group code, such as "AA". Set for every CoFID food in the bundled seed; null for Open Food Facts foods and for removed CoFID foods kept for the diary.</summary>
    public string? FoodGroup { get; set; }

    /// <summary>Lowercased text with accents removed that searches match against.</summary>
    [NotNull]
    public string SearchText { get; set; } = "";
}
