using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Tests;

/// <summary>Seeds and reads foods through the store's production methods, so no test needs a store method of its own.</summary>
internal static class FoodStoreSeeding
{
    /// <summary>
    /// Stores the food (an insert, or an update of the row with the same source and source id) and with <paramref name="barcode"/> links it, as a lookup that scanned that barcode would.
    /// Returns the food as stored, with its id. A barcode that is linked to another food gives that food, as <see cref="FoodStore.ImportAsync"/> does.
    /// </summary>
    public static async Task<Food> UpsertAsync(this FoodStore store, Food food, string? barcode = null)
    {
        if (barcode is not null)
        {
            return (await store.ImportAsync(barcode, food, food.FetchedAt ?? DateTimeOffset.UnixEpoch)).Food;
        }

        await store.UpsertManyAsync([food]);
        await store.RefreshAsync(food.Id, food);
        return food;
    }

    public static async Task<Food?> FindByBarcodeAsync(this FoodStore store, string barcode) => (await store.FindWithBarcodeAsync(barcode)).Food;
}
