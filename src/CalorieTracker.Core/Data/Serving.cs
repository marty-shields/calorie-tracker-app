using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>Where a serving came from. Stored as its number, so keep the values; the number is also the serving's <see cref="Serving.SortOrder"/> when it is written.</summary>
public enum ServingSource
{
    /// <summary>The serving the product's source gives (Open Food Facts <c>serving_size</c>). A food has at most one.</summary>
    Product = 0,

    /// <summary>The whole pack, from a package size. A food can have several, one for each pack size seen.</summary>
    Pack = 1,

    /// <summary>Added by the person, and the only kind they can delete.</summary>
    User = 2,
}

/// <summary>
/// One way to measure a portion of a food, such as "1 slice (28 g)" or "Whole pack (330 ml)". A food has none, one or several; a diary entry added by servings points at the one it used.
/// Unique by <see cref="FoodId"/>, <see cref="Source"/> and <see cref="Amount"/>. CoFID foods have none until the person adds their own.
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

    /// <summary>Servings of a food are listed by this, then smallest amount first. Written as the number of <see cref="Source"/>, so product, then pack, then the person's own.</summary>
    [NotNull]
    public int SortOrder { get; set; }
}
