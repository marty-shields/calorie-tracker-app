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

    /// <summary>EAN-8 or EAN-13 digits; null when the food has none.</summary>
    [Indexed]
    public string? Barcode { get; set; }

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

    /// <summary>Lowercased text with accents removed that searches match against.</summary>
    [NotNull]
    public string SearchText { get; set; } = "";
}
