using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CalorieTracker.Core.Data;
using CalorieTracker.Core.Logging;
using Microsoft.Extensions.Logging;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Searches Open Food Facts by text through Search-a-licious (<c>search.openfoodfacts.org</c>), limited to the device's country by <c>countries_tags:"en:..."</c> inside the query.
/// One request for each explicit call, at most one every <see cref="MinGap"/> (a call that comes sooner waits; the worldwide repeat of a country that gave nothing goes at once, as part of the same search, and the gap counts from it), and a finished search is kept in memory for the session, so the same text asks once.
/// Hits are cleaned before they are returned: a hit with no barcode, name, unit or nutrition is skipped, hits that match on name, pack size and per-100 values collapse into the first, and at most <see cref="IOnlineFoodSearch.MaxProducts"/> stay.
/// Sends the query and the country filter only, with a User-Agent of the app's name and version. The query and barcodes are never logged.
/// </summary>
/// <remarks>The client must have its <c>BaseAddress</c> set; the request path is relative.</remarks>
internal sealed partial class OnlineFoodSearch : IOnlineFoodSearch, IDisposable
{
    /// <summary>The least time between two requests.</summary>
    internal static readonly TimeSpan MinGap = TimeSpan.FromSeconds(2);

    private const string Fields = "code,product_name,brands,quantity,categories_tags,nutriments";
    private const int PageSize = 50;
    private const int MaxQueryLength = 100;
    private const int MaxLabelLength = 100;
    private const int MaxBrandsLength = 200;
    private const int MaxCached = 20;
    private const double KcalPerKj = 1 / 4.184;

    private static readonly Regex Amount = new(@"\d[\d.,]*\s*(ml|cl|dl|litres?|liters?|l|kg|g)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly HttpClient client;
    private readonly FoodStore store;
    private readonly ILogger<OnlineFoodSearch> logger;
    private readonly TimeProvider time;
    private readonly Func<string?> regionCode;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly SemaphoreSlim one = new(1, 1);
    private readonly Dictionary<string, Cached> cache = [];
    private readonly Queue<string> cacheOrder = new();
    private DateTimeOffset lastSent = DateTimeOffset.MinValue;

    public OnlineFoodSearch(HttpClient client, FoodStore store, ILogger<OnlineFoodSearch> logger, TimeProvider time, Func<string?>? regionCode = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.client = client;
        this.store = store;
        this.logger = logger;
        this.time = time;
        this.regionCode = regionCode ?? OffCountries.DeviceRegionCode;
        this.delay = delay ?? ((wait, ct) => Task.Delay(wait, time, ct));
    }

    /// <summary>The User-Agent: the app's name and version from the Core assembly, with no contact address.</summary>
    internal static string UserAgent { get; } = BuildUserAgent();

    public async Task<OnlineSearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var text = CleanQuery(query);
        if (text.Length == 0)
        {
            return OnlineSearchResult.None();
        }

        var inCountry = OffCountries.TryGet(regionCode(), out var tag, out var name);
        var key = $"{SearchFolding.Fold(text)}|{(inCountry ? tag : "")}";
        await one.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(key, out var cached))
            {
                Log.Cached(logger, cached.Products.Count);
                return await ToResultAsync(cached);
            }

            var answer = await RequestAsync(text, inCountry ? tag : null, ct);
            var country = inCountry ? name : null;
            if (answer.Status == OnlineSearchStatus.None && inCountry)
            {
                Log.WorldwideRepeat(logger);
                answer = await RequestAsync(text, null, ct, skipGap: true);
                country = null;
            }

            switch (answer.Status)
            {
                case OnlineSearchStatus.Found or OnlineSearchStatus.None:
                    var entry = new Cached(answer.Products, country);
                    Remember(key, entry);
                    return await ToResultAsync(entry);
                case OnlineSearchStatus.Limited:
                    return OnlineSearchResult.Limited();
                case OnlineSearchStatus.Offline:
                    return OnlineSearchResult.Offline();
                default:
                    return OnlineSearchResult.Failed();
            }
        }
        finally
        {
            one.Release();
        }
    }

    public void Dispose() => one.Dispose();

    private void Remember(string key, Cached entry)
    {
        if (cache.Count >= MaxCached && cacheOrder.TryDequeue(out var oldest))
        {
            cache.Remove(oldest);
        }

        cache[key] = entry;
        cacheOrder.Enqueue(key);
    }

    /// <summary>The cached products with <see cref="OnlineProduct.IsNew"/> read from the store now, since a product tapped meanwhile is stored.</summary>
    private async Task<OnlineSearchResult> ToResultAsync(Cached cached)
    {
        HashSet<string> stored;
        try
        {
            stored = cached.Products.Count == 0 ? [] : await store.StoredBarcodesAsync(cached.Products.Select(p => p.Barcode).ToList());
        }
        catch (Exception)
        {
            Log.StoredCheckFailed(logger);
            stored = cached.Products.Select(p => p.Barcode).ToHashSet();
        }

        return OnlineSearchResult.Found(cached.Products.Select(p => p with { IsNew = !stored.Contains(p.Barcode) }).ToList(), cached.Country);
    }

    private async Task<Answer> RequestAsync(string text, string? countryTag, CancellationToken ct, bool skipGap = false)
    {
        var wait = lastSent + MinGap - time.GetUtcNow();
        if (!skipGap && wait > TimeSpan.Zero)
        {
            Log.Waiting(logger, (int)wait.TotalMilliseconds);
            await delay(wait, ct);
        }

        lastSent = time.GetUtcNow();
        var q = countryTag is null ? text : $"{text} countries_tags:\"{countryTag}\"";
        var url = $"search?q={Uri.EscapeDataString(q)}&page_size={PageSize}&fields={Fields}";
        Log.Sent(logger, countryTag is null);
        try
        {
            using var response = await client.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Log.Limited(logger);
                return new Answer(OnlineSearchStatus.Limited, []);
            }

            if (!response.IsSuccessStatusCode)
            {
                Log.UnexpectedStatus(logger, (int)response.StatusCode);
                return new Answer(OnlineSearchStatus.Failed, []);
            }

            return await ReadBodyAsync(response, ct);
        }
        catch (HttpRequestException)
        {
            Log.NoResponse(logger);
            return new Answer(OnlineSearchStatus.Offline, []);
        }
        catch (IOException)
        {
            Log.NoResponse(logger);
            return new Answer(OnlineSearchStatus.Offline, []);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Log.NoResponse(logger);
            return new Answer(OnlineSearchStatus.Offline, []);
        }
    }

    private async Task<Answer> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException)
        {
            Log.Malformed(logger);
            return new Answer(OnlineSearchStatus.Failed, []);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("hits", out var hits) || hits.ValueKind != JsonValueKind.Array)
            {
                Log.Malformed(logger);
                return new Answer(OnlineSearchStatus.Failed, []);
            }

            try
            {
                var (products, hitCount, skipped, collapsed) = Clean(hits, time.GetUtcNow());
                Log.Answered(logger, hitCount, products.Count, skipped, collapsed);
                return new Answer(products.Count == 0 ? OnlineSearchStatus.None : OnlineSearchStatus.Found, products);
            }
            catch (InvalidOperationException)
            {
                // GetString throws on a lone surrogate escape or invalid UTF-8, which parsing accepts.
                Log.Malformed(logger);
                return new Answer(OnlineSearchStatus.Failed, []);
            }
        }
    }

    /// <summary>The usable products of the hits in order, with how many hits there were, were skipped as incomplete and were collapsed into an earlier one.</summary>
    private static (List<OnlineProduct> Products, int Hits, int Skipped, int Collapsed) Clean(JsonElement hits, DateTimeOffset now)
    {
        var products = new List<OnlineProduct>();
        var seen = new HashSet<string>();
        var count = 0;
        var skipped = 0;
        var collapsed = 0;
        foreach (var hit in hits.EnumerateArray())
        {
            count++;
            if (hit.ValueKind != JsonValueKind.Object || Map(hit, now) is not { } mapped)
            {
                skipped++;
                continue;
            }

            if (!seen.Add(mapped.Key))
            {
                collapsed++;
                continue;
            }

            if (products.Count < IOnlineFoodSearch.MaxProducts)
            {
                products.Add(mapped.Product);
            }
        }

        return (products, count, skipped, collapsed);
    }

    /// <summary>The product and the key it collapses on (name, pack size and per-100 values), or null when the hit lacks a barcode, a name, a unit or any of kcal, protein, carbs and fat.</summary>
    private static (OnlineProduct Product, string Key)? Map(JsonElement hit, DateTimeOffset now)
    {
        if (!BarcodeNormaliser.TryNormalise(ReadString(hit, "code"), out var barcode) || ReadString(hit, "product_name") is not { } name)
        {
            return null;
        }

        var quantity = ReadString(hit, "quantity");
        var beverage = IsBeverage(hit);
        var unit = UnitOf(quantity) ?? (beverage is null ? (FoodUnit?)null : beverage == true ? FoodUnit.Millilitre : FoodUnit.Gram);
        if (unit is null || !hit.TryGetProperty("nutriments", out var nutriments) || nutriments.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var kcal = ReadNumber(nutriments, "energy-kcal_100g") ?? ReadNumber(nutriments, "energy-kj_100g") * KcalPerKj;
        var protein = ReadNumber(nutriments, "proteins_100g");
        var carbs = ReadNumber(nutriments, "carbohydrates_100g");
        var fat = ReadNumber(nutriments, "fat_100g");
        if (kcal is null || protein is null || carbs is null || fat is null)
        {
            return null;
        }

        name = Truncate(name, MaxLabelLength);
        var brands = ReadBrands(hit);
        var food = new Food
        {
            Source = FoodSource.OpenFoodFacts,
            SourceId = barcode,
            Name = name,
            Brand = brands.Count == 0 ? null : Truncate(brands[0], MaxLabelLength),
            Brands = brands.Count == 0 ? null : Truncate(string.Join(", ", brands), MaxBrandsLength),
            Unit = unit.Value,
            Kcal = kcal.Value,
            ProteinG = protein.Value,
            CarbsG = carbs.Value,
            FatG = fat.Value,
            PackageLabel = quantity is null ? null : Truncate(quantity, MaxLabelLength),
            FetchedAt = now,
            SearchText = SearchFolding.Fold(string.Join(' ', new[] { name }.Concat(brands))),
        };
        var pack = quantity is null ? "" : new string(quantity.Where(c => !char.IsWhiteSpace(c)).ToArray()).Replace(',', '.').ToLowerInvariant();
        var key = string.Join('|', ProductMatch.NormaliseName(name), pack, unit, Round(kcal.Value), Round(protein.Value), Round(carbs.Value), Round(fat.Value));
        return (new OnlineProduct(barcode, food, true), key);
    }

    private static string Round(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>The unit of the last amount in a quantity such as "330 ml", "1,5 l" or "6 x 250 ml": millilitres for ml, cl, dl and l, grams for g and kg. Null when there is none.</summary>
    private static FoodUnit? UnitOf(string? quantity)
    {
        if (quantity is null)
        {
            return null;
        }

        Match? last = null;
        try
        {
            foreach (Match match in Amount.Matches(quantity))
            {
                last = match;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        return last is null ? null : last.Groups[1].Value.ToLowerInvariant() is "g" or "kg" ? FoodUnit.Gram : FoodUnit.Millilitre;
    }

    /// <summary>Whether <c>categories_tags</c> has <c>en:beverages</c>, when it has any string tag; null when it has none.</summary>
    private static bool? IsBeverage(JsonElement hit)
    {
        if (!hit.TryGetProperty("categories_tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var strings = tags.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()).ToList();
        return strings.Count == 0 ? null : strings.Any(t => string.Equals(t, "en:beverages", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The brands as a list: Search-a-licious sends an array, an older shape was one comma-separated string. Trimmed, blanks and repeats (ignoring case) dropped.</summary>
    private static List<string> ReadBrands(JsonElement hit)
    {
        if (!hit.TryGetProperty("brands", out var value))
        {
            return [];
        }

        IEnumerable<string?> raw = value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Where(b => b.ValueKind == JsonValueKind.String).Select(b => b.GetString()),
            JsonValueKind.String => (value.GetString() ?? "").Split(','),
            _ => [],
        };
        return raw.Select(b => b?.Trim() ?? "").Where(b => b.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static readonly char[] QuerySyntax = ['"', '\\', '(', ')', '[', ']', '{', '}', ':', '/', '^', '~', '*', '?', '!', '&', '|'];

    /// <summary>The typed text with the characters of the search's query syntax removed (quotes, backslashes, brackets, colons, slashes, wildcards, operators, a leading + or - on a word, and the words AND, OR and NOT), so it is only words: otherwise it could break the country filter or change how the filter combines. Spaces collapsed, cut to <see cref="MaxQueryLength"/>.</summary>
    internal static string CleanQuery(string? query)
    {
        var plain = string.Concat((query ?? "").Select(c => Array.IndexOf(QuerySyntax, c) >= 0 ? ' ' : c));
        var words = plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.TrimStart('+', '-'))
            .Where(w => w.Length > 0 && w is not ("AND" or "OR" or "NOT"));
        return Truncate(string.Join(' ', words), MaxQueryLength);
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var cut = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..cut].TrimEnd();
    }

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

    private static string BuildUserAgent()
    {
        var name = typeof(OnlineFoodSearch).Assembly.GetName();
        var version = name.Version is { } v ? $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}" : "0.0.0";
        return $"{name.Name}/{version}";
    }

    private sealed record Cached(IReadOnlyList<OnlineProduct> Products, string? Country);

    private sealed record Answer(OnlineSearchStatus Status, IReadOnlyList<OnlineProduct> Products);

    private static partial class Log
    {
        [LoggerMessage((int)CoreEvent.OnlineSearchSent, LogLevel.Information, "Open Food Facts search sent, worldwide {Worldwide}")]
        public static partial void Sent(ILogger logger, bool worldwide);

        [LoggerMessage((int)CoreEvent.OnlineSearchCached, LogLevel.Debug, "Open Food Facts search answered from the session cache with {Products} products")]
        public static partial void Cached(ILogger logger, int products);

        [LoggerMessage((int)CoreEvent.OnlineSearchAnswered, LogLevel.Information, "Open Food Facts search answered: {Hits} hits, {Products} products kept, {Skipped} skipped, {Collapsed} collapsed")]
        public static partial void Answered(ILogger logger, int hits, int products, int skipped, int collapsed);

        [LoggerMessage((int)CoreEvent.OnlineSearchWaiting, LogLevel.Debug, "Open Food Facts search waiting {WaitMs} ms for the minimum gap")]
        public static partial void Waiting(ILogger logger, int waitMs);

        [LoggerMessage((int)CoreEvent.OnlineSearchWorldwideRepeat, LogLevel.Debug, "The country search gave no products, repeating it worldwide")]
        public static partial void WorldwideRepeat(ILogger logger);

        [LoggerMessage((int)CoreEvent.OnlineSearchLimited, LogLevel.Warning, "Open Food Facts search rate limited (HTTP 429)")]
        public static partial void Limited(ILogger logger);

        [LoggerMessage((int)CoreEvent.OnlineSearchNoResponse, LogLevel.Information, "Open Food Facts search got no response (network error or timeout)")]
        public static partial void NoResponse(ILogger logger);

        [LoggerMessage((int)CoreEvent.OnlineSearchMalformed, LogLevel.Warning, "Open Food Facts search response was not the expected JSON")]
        public static partial void Malformed(ILogger logger);

        [LoggerMessage((int)CoreEvent.OnlineSearchUnexpectedStatus, LogLevel.Warning, "Open Food Facts search got HTTP {StatusCode}")]
        public static partial void UnexpectedStatus(ILogger logger, int statusCode);

        [LoggerMessage((int)CoreEvent.OnlineSearchStoredCheckFailed, LogLevel.Warning, "Reading which searched products are stored failed, none is marked new")]
        public static partial void StoredCheckFailed(ILogger logger);
    }
}
