using System.Globalization;
using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

/// <summary>Works out the <see cref="Serving"/> rows a looked-up food gets: its own serving and its whole pack.</summary>
internal static class ServingRows
{
    /// <summary>
    /// The Product row when <paramref name="serving"/> is usable (see <see cref="FoodService.TryBoundServing"/>), and the Pack row when its package size is finite, above zero and at most <see cref="FoodService.MaxPackServingAmount"/>
    /// and not the same amount as the product serving (that pack would only repeat it). Ids and food ids are not set.
    /// </summary>
    public static List<Serving> Of(Food food, ProductServing? serving)
    {
        var rows = new List<Serving>();
        var hasProduct = FoodService.TryBoundServing(serving?.Label, serving?.Amount, out var label, out var amount);
        if (hasProduct)
        {
            rows.Add(new Serving { Label = label, Amount = amount, Source = ServingSource.Product, SortOrder = (int)ServingSource.Product });
        }

        if (food.PackageAmount is { } pack && double.IsFinite(pack) && pack > 0 && pack <= FoodService.MaxPackServingAmount && !(hasProduct && pack == amount))
        {
            rows.Add(new Serving { Label = PackLabel(pack, food.Unit, food.PackageLabel), Amount = pack, Source = ServingSource.Pack, SortOrder = (int)ServingSource.Pack });
        }

        return rows;
    }

    private static readonly int[] CanMillilitres = [330, 355, 440, 473];

    /// <summary>
    /// "Can 330 ml", "Bottle 1 L" or "Pack 400 g": what the pack is, then its size, so it is not mistaken for the product's own serving.
    /// It is a can or bottle when the source's wording of the size says so (<paramref name="packageLabel"/> has the word can, tin or bottle), or by shape for millilitres:
    /// a common can size (330, 355, 440 or 473 ml) is a can and 1 L or more is a bottle. Anything else is a pack. The shape is a guess (a 330 ml bottle of beer reads as a can); the food's type is not used. Millilitres from 1000 read as litres and grams from 1000 as kilograms.
    /// </summary>
    public static string PackLabel(double amount, FoodUnit unit, string? packageLabel = null)
    {
        var size = unit == FoodUnit.Millilitre
            ? amount >= 1000 ? $"{(amount / 1000).ToString("0.##", CultureInfo.InvariantCulture)} L" : $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} ml"
            : amount >= 1000 ? $"{(amount / 1000).ToString("0.##", CultureInfo.InvariantCulture)} kg" : $"{amount.ToString("0.##", CultureInfo.InvariantCulture)} g";
        return $"{PackKind(amount, unit, packageLabel)} {size}";
    }

    private static string PackKind(double amount, FoodUnit unit, string? packageLabel)
    {
        var words = packageLabel is null ? Array.Empty<string>() : packageLabel.ToLowerInvariant().Split([' ', ',', '.', '(', ')', '-', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w is "can" or "cans" or "tin" or "canette"))
        {
            return "Can";
        }

        if (words.Any(w => w is "bottle" or "bottles" or "bouteille" or "btl"))
        {
            return "Bottle";
        }

        if (unit != FoodUnit.Millilitre)
        {
            return "Pack";
        }

        return amount >= 1000 ? "Bottle" : CanMillilitres.Contains((int)amount) && amount == Math.Floor(amount) ? "Can" : "Pack";
    }
}
