using System.Runtime.CompilerServices;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Foods;

namespace CalorieTracker.Core.Tests.Foods;

/// <summary>
/// Test fixtures describe a looked-up product with its serving the way a lookup returns it: the food and, beside it, a <see cref="ProductServing"/>.
/// This keeps the serving of a fixture food in a side table (the food has no serving fields), settable as a label and a weight apart so a half serving can be tested.
/// </summary>
internal static class LookupServing
{
    private sealed class Carried
    {
        public string? Label { get; set; }

        public double? Amount { get; set; }
    }

    private static readonly ConditionalWeakTable<Food, Carried> Table = new();

    extension(Food food)
    {
        public string? ServingLabel
        {
            get => Table.TryGetValue(food, out var c) ? c.Label : null;
            set => Table.GetOrCreateValue(food).Label = value;
        }

        public double? ServingAmount
        {
            get => Table.TryGetValue(food, out var c) ? c.Amount : null;
            set => Table.GetOrCreateValue(food).Amount = value;
        }

        /// <summary>The serving the fixture carries; null unless it has both a label and a weight.</summary>
        public ProductServing? Product => food is { ServingLabel: { } label, ServingAmount: { } amount } ? new ProductServing(label, amount) : null;
    }

    /// <summary>A Found lookup answer for the fixture, with the serving it carries.</summary>
    public static BarcodeLookupResult Found(Food food, double? packAmount = null) => BarcodeLookupResult.Found(food, packAmount, food.Product);
}
