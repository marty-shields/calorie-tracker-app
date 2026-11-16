using System.Globalization;
using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

/// <summary>Works out the <see cref="Serving"/> rows a looked-up food gets: its own serving and its whole pack.</summary>
internal static class ServingRows
{
    /// <summary>
    /// The Product row when the food carries a usable serving (see <see cref="FoodService.TryBoundServing"/>), and the Pack row when its package size is finite, above zero and at most <see cref="FoodService.MaxPackServingAmount"/>
    /// and not the same amount as the product serving (that pack would only repeat it). Ids and food ids are not set.
    /// </summary>
    public static List<Serving> Of(Food food)
    {
        var rows = new List<Serving>();
        var hasProduct = FoodService.TryBoundServing(food.ServingLabel, food.ServingAmount, out var label, out var amount);
        if (hasProduct)
        {
            rows.Add(new Serving { Label = label, Amount = amount, Source = ServingSource.Product, SortOrder = (int)ServingSource.Product });
        }

        if (food.PackageAmount is { } pack && double.IsFinite(pack) && pack > 0 && pack <= FoodService.MaxPackServingAmount && !(hasProduct && pack == amount))
        {
            rows.Add(new Serving { Label = PackLabel(pack, food.Unit), Amount = pack, Source = ServingSource.Pack, SortOrder = (int)ServingSource.Pack });
        }

        return rows;
    }

    /// <summary>"Whole pack (330 ml)": says what the pack is so it is not mistaken for the product's own serving.</summary>
    public static string PackLabel(double amount, FoodUnit unit) =>
        $"Whole pack ({amount.ToString("0.##", CultureInfo.InvariantCulture)} {(unit == FoodUnit.Millilitre ? "ml" : "g")})";
}
