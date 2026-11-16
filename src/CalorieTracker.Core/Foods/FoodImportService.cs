using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>How <see cref="FoodImportService.ImportAsync"/> ended.</summary>
public enum FoodImportStatus
{
    /// <summary>A new food was stored for the product.</summary>
    Created,

    /// <summary>The barcode was added to a stored food that is the same product in another size (see <see cref="ProductMatch"/>); that food's name, brand and nutrients are unchanged.</summary>
    Attached,

    /// <summary>The barcode, or the product itself, was stored already, so its food was returned.</summary>
    Known,

    /// <summary>The product lacks a required value (see <see cref="NotImportableReason"/>); nothing was stored.</summary>
    NotImportable,

    /// <summary>The database failed; nothing is known to be stored.</summary>
    Failed,
}

/// <summary>Why a product was not imported.</summary>
public enum NotImportableReason
{
    None,
    NoBarcode,
    NoName,
    NoUnit,
    NoNutrients,
    NotOpenFoodFacts,
}

/// <summary>
/// The outcome of an import. <see cref="Food"/> is the food the barcode now belongs to and <see cref="PackAmount"/> the pack of that barcode (null when it has none), both set for <see cref="FoodImportStatus.Created"/>, <see cref="FoodImportStatus.Attached"/> and <see cref="FoodImportStatus.Known"/>.
/// </summary>
public sealed record FoodImportResult(FoodImportStatus Status, Food? Food = null, double? PackAmount = null, NotImportableReason Reason = NotImportableReason.None);

/// <summary>
/// Stores a looked-up Open Food Facts product under its barcode and says which food it ended up on. The product comes from anywhere that has looked one up (a barcode today, a search by name later):
/// nothing here knows how it was found, so a person who "first saw" the barcode is all the service can say.
/// Importing is idempotent: a stored barcode returns its food and changes nothing; the same unknown product twice makes one food, and so do concurrent imports of one barcode (the database allows one row for each barcode).
/// A product that is the same food in another size (<see cref="ProductMatch"/>) gets the new barcode on the stored food instead of a food of its own; the stored food keeps its name, brand and nutrients.
/// An incomplete product comes back as <see cref="FoodImportStatus.NotImportable"/> and a failing database as <see cref="FoodImportStatus.Failed"/>, never as an exception. Logs carry food ids and counts only, never a barcode or a product's text.
/// </summary>
public sealed partial class FoodImportService
{
    private readonly FoodStore store;
    private readonly TimeProvider time;
    private readonly ILogger<FoodImportService> logger;

    internal FoodImportService(FoodStore store, TimeProvider time, ILogger<FoodImportService> logger)
    {
        this.store = store;
        this.time = time;
        this.logger = logger;
    }

    /// <summary>
    /// Imports <paramref name="product"/> under <paramref name="barcode"/>. The product is a <see cref="Food"/> as a lookup returns it, with no id: source Open Food Facts, a name, a unit, kcal, protein, carbs and fat (finite and not negative; optional nutrients are finite when set),
    /// and optionally brand, category, pack (<see cref="Food.PackageAmount"/> and <see cref="Food.PackageLabel"/>) and serving (<see cref="Food.ServingLabel"/> and <see cref="Food.ServingAmount"/>).
    /// A blank source id is set to the barcode and blank search text is filled in. Each barcode's pack is stored as a Pack serving (a pack over <see cref="FoodService.MaxPackServingAmount"/> is kept on the barcode only), and the product's serving as the Product serving when the food has none.
    /// </summary>
    /// <param name="barcode">Digits as <see cref="BarcodeNormaliser.TryNormalise"/> gives them.</param>
    public async Task<FoodImportResult> ImportAsync(string barcode, Food product)
    {
        var reason = Check(barcode, product);
        if (reason != NotImportableReason.None)
        {
            Log.NotImportable(logger, reason);
            return new FoodImportResult(FoodImportStatus.NotImportable, Reason: reason);
        }

        if (string.IsNullOrWhiteSpace(product.SourceId))
        {
            product.SourceId = barcode;
        }

        if (string.IsNullOrWhiteSpace(product.SearchText))
        {
            product.SearchText = SearchFolding.Fold(string.Join(' ', new[] { product.Name, product.Brands ?? product.Brand, product.GenericName, product.Category }.Where(t => !string.IsNullOrEmpty(t))));
        }

        try
        {
            var stored = await store.ImportAsync(barcode, product, time.GetUtcNow());
            var status = stored.Outcome switch
            {
                ImportOutcome.Created => FoodImportStatus.Created,
                ImportOutcome.Attached => FoodImportStatus.Attached,
                _ => FoodImportStatus.Known,
            };
            switch (status)
            {
                case FoodImportStatus.Created:
                    Log.Created(logger, stored.Food.Id);
                    break;
                case FoodImportStatus.Attached:
                    Log.Attached(logger, stored.Food.Id);
                    break;
                default:
                    Log.Known(logger, stored.Food.Id);
                    break;
            }

            return new FoodImportResult(status, stored.Food, stored.Barcode.PackageAmount);
        }
        catch (Exception)
        {
            Log.Failed(logger);
            return new FoodImportResult(FoodImportStatus.Failed);
        }
    }

    private static NotImportableReason Check(string barcode, Food product)
    {
        if (!BarcodeNormaliser.TryNormalise(barcode, out var normal) || normal != barcode)
        {
            return NotImportableReason.NoBarcode;
        }

        if (product.Source != FoodSource.OpenFoodFacts)
        {
            return NotImportableReason.NotOpenFoodFacts;
        }

        if (string.IsNullOrWhiteSpace(product.Name))
        {
            return NotImportableReason.NoName;
        }

        if (!Enum.IsDefined(product.Unit))
        {
            return NotImportableReason.NoUnit;
        }

        double?[] required = [product.Kcal, product.ProteinG, product.CarbsG, product.FatG];
        double?[] optional = [product.FibreG, product.SugarsG, product.SaturatesG, product.SaltG];
        return required.Concat(optional).Any(v => v is { } n && (!double.IsFinite(n) || n < 0)) ? NotImportableReason.NoNutrients : NotImportableReason.None;
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.FoodImportCreated, LogLevel.Debug, "Import created food {FoodId}")]
        public static partial void Created(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodImportAttached, LogLevel.Debug, "Import attached a barcode to food {FoodId}")]
        public static partial void Attached(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodImportKnown, LogLevel.Debug, "Import found food {FoodId} already stored")]
        public static partial void Known(ILogger logger, int foodId);

        [LoggerMessage((int)CoreEvent.FoodImportNotImportable, LogLevel.Debug, "Import skipped, reason {Reason}")]
        public static partial void NotImportable(ILogger logger, NotImportableReason reason);

        [LoggerMessage((int)CoreEvent.FoodImportFailed, LogLevel.Warning, "Import failed")]
        public static partial void Failed(ILogger logger);
    }
}
