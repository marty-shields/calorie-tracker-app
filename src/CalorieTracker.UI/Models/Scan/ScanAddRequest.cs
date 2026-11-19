namespace CalorieTracker.UI.Models.Scan;

/// <summary>What the scan screen hands on when it finds a product.</summary>
/// <param name="FoodId">Id of the stored food.</param>
/// <param name="LastAmount">Grams or millilitres last logged of the food; null for a food never logged.</param>
/// <param name="LastServings">The count of servings that last entry was entered as; null when it was by weight.</param>
/// <param name="LastServingId">The serving that last entry counted its servings in; null when it was by weight or its serving was deleted.</param>
public sealed record ScanAddRequest(int FoodId, double? LastAmount = null, double? LastServings = null, int? LastServingId = null);
