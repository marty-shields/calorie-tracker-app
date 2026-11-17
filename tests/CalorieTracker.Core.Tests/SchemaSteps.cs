using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Tests;

/// <summary>The app's numbered schema upgrade steps, so a new step is added in one place.</summary>
internal static class SchemaSteps
{
    public static ISchemaUpgrade[] All() =>
        [new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade(), new SettingsTableUpgrade(), new DiaryTableUpgrade(), new FoodServingCheckedUpgrade(), new FoodOriginalNameUpgrade(), new FoodSourceDetailsUpgrade(), new FoodUnitUpgrade(), new FoodCordialUnitUpgrade(), new ServingTableUpgrade(), new BarcodeTableUpgrade(TimeProvider.System), new PackServingUpgrade(), new StorageCleanupUpgrade()];

    /// <summary>The steps up to and including <paramref name="version"/>, for a test of what an older step leaves behind (pair it with an initializer at that version).</summary>
    public static ISchemaUpgrade[] Through(int version) => [.. All().Where(s => s.Version <= version)];

    /// <summary>The <c>FoodBarcode</c> table as step 2 made it; the app has no type for it any more, so a test of an older schema creates it with this.</summary>
    public const string FoodBarcodeTable = "CREATE TABLE IF NOT EXISTS \"FoodBarcode\" (\"Barcode\" varchar primary key not null, \"FoodId\" integer not null)";
}
