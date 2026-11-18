using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Where a serving came from. Stored as its number, so keep the values; the number is also the serving's <see cref="Serving.SortOrder"/> when it is written. Only <see cref="User"/> is written now.</summary>
public enum ServingSource
{
    /// <summary>No longer written; the value is kept because schema steps 13 and 15 use it.</summary>
    Product = 0,

    /// <summary>No longer written; the value is kept because schema steps 13 and 15 use it.</summary>
    Pack = 1,

    /// <summary>Added by the person; the only kind a food has.</summary>
    User = 2,
}

/// <summary>
/// One way to measure a portion of a food, such as "1 slice (28 g)" or "Bowl (40 g)". A food has none, one or several; a diary entry added by servings points at the one it used.
/// Unique by <see cref="FoodId"/>, <see cref="Source"/> and <see cref="Amount"/>. CoFID foods have none until the person adds their own; so do Open Food Facts foods.
/// </summary>
public class Serving
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, Indexed("UX_Serving_Food_Source_Amount", 1, Unique = true)]
    public int FoodId { get; set; }

    /// <summary>Not blank, at most <see cref="Foods.FoodService.MaxServingLabelLength"/> characters when saved by the app; a person's own text, so never logged.</summary>
    [NotNull]
    public string Label { get; set; } = "";

    /// <summary>Grams, or millilitres to match the food's unit. Above zero.</summary>
    [NotNull, Indexed("UX_Serving_Food_Source_Amount", 3, Unique = true)]
    public double Amount { get; set; }

    [NotNull, Indexed("UX_Serving_Food_Source_Amount", 2, Unique = true)]
    public ServingSource Source { get; set; }

    /// <summary>Breaks a tie when servings of a food have the same amount: they are listed smallest amount first, then by this. Written as the number of <see cref="Source"/>.</summary>
    [NotNull]
    public int SortOrder { get; set; }
}
