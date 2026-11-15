using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Tests;

/// <summary>The app's numbered schema upgrade steps, so a new step is added in one place.</summary>
internal static class SchemaSteps
{
    public static ISchemaUpgrade[] All() =>
        [new FoodBarcodeUpgrade(), new FoodTypeUpgrade(), new DropDishesUpgrade(), new FoodServingUpgrade(), new SettingsTableUpgrade()];
}
