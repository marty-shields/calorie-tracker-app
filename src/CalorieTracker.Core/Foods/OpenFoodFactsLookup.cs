using System.Globalization;
using System.Net;
using System.Text.Json;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Looks a barcode up on Open Food Facts. Network, timeout and malformed-response failures come back as a status;
/// only the caller's own cancellation throws.
/// </summary>
/// <remarks>The client must have its <c>BaseAddress</c> set; the request path is relative.</remarks>
internal sealed partial class OpenFoodFactsLookup : IBarcodeLookup
{
    private const string Fields = "product_name,brands,nutriments,product_quantity_unit,serving_quantity_unit,categories_tags,serving_size,serving_quantity";
    private const int MaxAttempts = 3;
    private const double KcalPerKj = 1 / 4.184;

    /// <summary>The wait before a retry when the response names none, or names one over <see cref="MaxRetryAfter"/>.</summary>
    internal static readonly TimeSpan DefaultRetryWait = TimeSpan.FromSeconds(2);

    internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(5);

    private readonly HttpClient client;
    private readonly ILogger<OpenFoodFactsLookup> logger;
    private readonly TimeProvider time;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    public OpenFoodFactsLookup(HttpClient client, ILogger<OpenFoodFactsLookup> logger, TimeProvider time, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.client = client;
        this.logger = logger;
        this.time = time;
        this.delay = delay ?? Task.Delay;
    }

    /// <summary>The User-Agent Open Food Facts asks for: app name and version, then a contact URL. Name and version come from the Core assembly.</summary>
    internal static string UserAgent { get; } = BuildUserAgent();

    public async Task<BarcodeLookupResult> LookupAsync(string barcode, CancellationToken ct = default)
    {
        // Normalised barcodes are 8 or 13 digits; anything outside 8-14 ASCII digits never reaches the network.
        if (!IsAsciiDigits(barcode, 8, 14))
        {
            Log.NotFound(logger);
            return BarcodeLookupResult.NotFound();
        }

        var url = $"api/v2/product/{Uri.EscapeDataString(barcode)}?fields={Fields}";
        for (var attempt = 1; ; attempt++)
        {
            Log.Attempted(logger, attempt);
            try
            {
                using var response = await client.GetAsync(url, ct);
                var status = response.StatusCode;
                if (status is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt == MaxAttempts)
                    {
                        Log.RateLimited(logger, attempt);
                        return BarcodeLookupResult.RateLimited();
                    }

                    var wait = RetryWait(response);
                    Log.RetryWaiting(logger, attempt, (int)status, (int)wait.TotalMilliseconds);
                    await delay(wait, ct);
                    continue;
                }

                if (status == HttpStatusCode.NotFound)
                {
                    Log.NotFound(logger);
                    return BarcodeLookupResult.NotFound();
                }

                if (!response.IsSuccessStatusCode)
                {
                    Log.UnexpectedStatus(logger, (int)status);
                    return BarcodeLookupResult.Offline();
                }

                return await ReadBodyAsync(response, barcode, ct);
            }
            catch (HttpRequestException)
            {
                Log.NoResponse(logger);
                return BarcodeLookupResult.Offline();
            }
            catch (IOException)
            {
                Log.NoResponse(logger);
                return BarcodeLookupResult.Offline();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Log.NoResponse(logger);
                return BarcodeLookupResult.Offline();
            }
        }
    }

    private async Task<BarcodeLookupResult> ReadBodyAsync(HttpResponseMessage response, string barcode, CancellationToken ct)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException)
        {
            Log.Malformed(logger);
            return BarcodeLookupResult.Offline();
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("status", out var statusElement)
                || statusElement.ValueKind != JsonValueKind.Number
                || !statusElement.TryGetInt32(out var status))
            {
                Log.Malformed(logger);
                return BarcodeLookupResult.Offline();
            }

            if (status == 0)
            {
                Log.NotFound(logger);
                return BarcodeLookupResult.NotFound();
            }

            if (status != 1 || !root.TryGetProperty("product", out var product) || product.ValueKind != JsonValueKind.Object)
            {
                Log.Malformed(logger);
                return BarcodeLookupResult.Offline();
            }

            Food? food;
            try
            {
                var code = ReadString(root, "code");
                food = Map(product, code is not null && IsAsciiDigits(code, 1, int.MaxValue) ? code : barcode);
            }
            catch (InvalidOperationException)
            {
                // GetString throws on a lone surrogate escape or invalid UTF-8, which parsing accepts.
                Log.Malformed(logger);
                return BarcodeLookupResult.Offline();
            }

            if (food is null)
            {
                Log.NotFound(logger);
                return BarcodeLookupResult.NotFound();
            }

            Log.Found(logger);
            return BarcodeLookupResult.Found(food);
        }
    }

    /// <summary>
    /// Null when the name or any of kcal, protein, carbs and fat is missing.
    /// <paramref name="sourceId"/> is the product's own code, so several barcodes of one product map to one food.
    /// </summary>
    private static Food? Map(JsonElement product, string sourceId)
    {
        var name = ReadString(product, "product_name");
        if (name is null)
        {
            return null;
        }

        var hasNutriments = product.TryGetProperty("nutriments", out var nutriments) && nutriments.ValueKind == JsonValueKind.Object;
        double? Nutrient(string key) => hasNutriments ? ReadNumber(nutriments, key) : null;

        var kcal = Nutrient("energy-kcal_100g") ?? Nutrient("energy-kj_100g") * KcalPerKj;
        var protein = Nutrient("proteins_100g");
        var carbs = Nutrient("carbohydrates_100g");
        var fat = Nutrient("fat_100g");
        if (kcal is null || protein is null || carbs is null || fat is null)
        {
            return null;
        }

        var brand = ReadString(product, "brands")?.Split(',')[0].Trim();
        brand = string.IsNullOrEmpty(brand) ? null : brand;
        var millilitre = IsMillilitre(product, "product_quantity_unit") || IsMillilitre(product, "serving_quantity_unit");
        var (servingLabel, servingAmount) = ReadServing(product, millilitre);

        return new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = sourceId,
            Name = name,
            Brand = brand,
            Unit = millilitre ? FoodUnit.Millilitre : FoodUnit.Gram,
            Type = (IsBeverage(product) ?? millilitre) ? FoodType.Drink : FoodType.Food,
            Kcal = kcal.Value,
            ProteinG = protein.Value,
            CarbsG = carbs.Value,
            FatG = fat.Value,
            FibreG = Nutrient("fiber_100g"),
            SugarsG = Nutrient("sugars_100g"),
            SaturatesG = Nutrient("saturated-fat_100g"),
            SaltG = Nutrient("salt_100g"),
            ServingLabel = servingLabel,
            ServingAmount = servingAmount,
            SearchText = SearchFolding.Fold($"{name} {brand}".Trim()),
        };
    }

    /// <summary>
    /// Both parts or neither: a label, a weight above zero, and a <c>serving_quantity_unit</c> that is the food's unit (absent counts as grams, as OFF defaults to).
    /// </summary>
    private static (string? Label, double? Amount) ReadServing(JsonElement product, bool millilitre)
    {
        var label = ReadString(product, "serving_size");
        var amount = ReadNumber(product, "serving_quantity");
        var unit = ReadString(product, "serving_quantity_unit");
        var matches = string.Equals(unit ?? "g", millilitre ? "ml" : "g", StringComparison.OrdinalIgnoreCase);
        return label is not null && amount is > 0 && matches ? (label, amount) : (null, null);
    }

    private static bool IsAsciiDigits(string text, int minLength, int maxLength) =>
        text.Length >= minLength && text.Length <= maxLength && text.All(char.IsAsciiDigit);

    /// <summary>
    /// Whether <c>categories_tags</c> has <c>en:beverages</c>, when it has any string tag, so oils or soy sauce sold in ml are not drinks.
    /// Null when the field is missing, empty or malformed, and the unit decides.
    /// </summary>
    private static bool? IsBeverage(JsonElement product)
    {
        if (!product.TryGetProperty("categories_tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var strings = tags.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()).ToList();
        return strings.Count == 0 ? null : strings.Any(t => string.Equals(t, "en:beverages", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMillilitre(JsonElement product, string key) =>
        string.Equals(ReadString(product, key), "ml", StringComparison.OrdinalIgnoreCase);

    /// <summary>The trimmed string, or null when absent, not a string or blank.</summary>
    private static string? ReadString(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>A JSON number or a numeric string; null when absent, unparseable, negative or not finite.</summary>
    private static double? ReadNumber(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var value))
        {
            return null;
        }

        double number;
        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetDouble(out var parsedNumber):
                number = parsedNumber;
                break;
            case JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                number = parsed;
                break;
            default:
                return null;
        }

        return double.IsFinite(number) && number >= 0 ? number : null;
    }

    private TimeSpan RetryWait(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var wait = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - time.GetUtcNow() : null);
        return wait is { } w && w >= TimeSpan.Zero && w <= MaxRetryAfter ? w : DefaultRetryWait;
    }

    private static string BuildUserAgent()
    {
        var name = typeof(OpenFoodFactsLookup).Assembly.GetName();
        var version = name.Version is { } v ? $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}" : "0.0.0";
        return $"{name.Name}/{version} (https://github.com/marty-shields/calorie-tracker-app)";
    }

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.LookupAttempted, LogLevel.Debug, "Barcode lookup attempt {Attempt}")]
        public static partial void Attempted(ILogger logger, int attempt);

        [LoggerMessage((int)CoreEvent.LookupFound, LogLevel.Debug, "Barcode lookup found a product")]
        public static partial void Found(ILogger logger);

        [LoggerMessage((int)CoreEvent.LookupNotFound, LogLevel.Debug, "Barcode lookup found no usable product")]
        public static partial void NotFound(ILogger logger);

        [LoggerMessage((int)CoreEvent.LookupRetryWaiting, LogLevel.Debug, "Barcode lookup attempt {Attempt} got HTTP {StatusCode}, waiting {WaitMs} ms")]
        public static partial void RetryWaiting(ILogger logger, int attempt, int statusCode, int waitMs);

        [LoggerMessage((int)CoreEvent.LookupRateLimited, LogLevel.Warning, "Barcode lookup rate limited after {Attempts} attempts")]
        public static partial void RateLimited(ILogger logger, int attempts);

        [LoggerMessage((int)CoreEvent.LookupNoResponse, LogLevel.Information, "Barcode lookup got no response (network error or timeout)")]
        public static partial void NoResponse(ILogger logger);

        [LoggerMessage((int)CoreEvent.LookupMalformedResponse, LogLevel.Warning, "Barcode lookup response was not the expected JSON")]
        public static partial void Malformed(ILogger logger);

        [LoggerMessage((int)CoreEvent.LookupUnexpectedStatus, LogLevel.Warning, "Barcode lookup got HTTP {StatusCode}, treated as offline")]
        public static partial void UnexpectedStatus(ILogger logger, int statusCode);
    }
}
