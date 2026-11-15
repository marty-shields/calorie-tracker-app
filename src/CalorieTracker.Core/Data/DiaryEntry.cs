using SQLite;

namespace CalorieTracker.Core.Data;

/// <summary>The meal group of a diary entry. Stored as its number, so keep the values.</summary>
public enum Meal
{
    Breakfast = 0,
    Lunch = 1,
    Dinner = 2,
    Snacks = 3,
}

/// <summary>
/// One food eaten on a day. Nutrition is looked up from the food each time, so a food an entry points at is never deleted.
/// Entries are listed in <see cref="Id"/> order, which is the order added.
/// </summary>
public class DiaryEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>The <see cref="Date"/> text of a day, so every writer and reader of the column agrees.</summary>
    internal static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The local calendar day as <c>yyyy-MM-dd</c>, with no time zone.</summary>
    [NotNull, Indexed]
    public string Date { get; set; } = "";

    [NotNull]
    public Meal Meal { get; set; }

    [NotNull, Indexed]
    public int FoodId { get; set; }

    /// <summary>Grams or millilitres, by the food's unit; above zero.</summary>
    [NotNull]
    public double Amount { get; set; }

    /// <summary>How many servings the amount was entered as; null when it was entered as grams or millilitres.</summary>
    public double? Servings { get; set; }

    /// <summary>True once removed. The row stays so the entry can be restored to its place.</summary>
    [NotNull]
    public bool IsRemoved { get; set; }
}
