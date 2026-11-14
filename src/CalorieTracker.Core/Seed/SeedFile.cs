using System.Text.Json;
using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Seed;

/// <summary>The foods of a seed file and its version.</summary>
internal sealed record SeedData(int Version, IReadOnlyList<Food> Foods);

/// <summary>Reads a seed file in the format <c>build-food-seed.py</c> writes.</summary>
internal static class SeedFile
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Throws <see cref="JsonException"/> or <see cref="FormatException"/> when the file is malformed,
    /// a food lacks a required value, is not CoFID, or repeats a source id.
    /// </summary>
    public static SeedData Parse(Stream stream)
    {
        var file = JsonSerializer.Deserialize<FileDto>(stream, Options) ?? throw new FormatException("Seed file is empty");
        if (file.SeedVersion is not > 0 || file.Foods is null)
        {
            throw new FormatException("Seed file has no version or foods");
        }

        var seen = new HashSet<string>();
        var foods = new List<Food>(file.Foods.Count);
        foreach (var dto in file.Foods)
        {
            var food = ToFood(dto);
            if (!seen.Add(food.SourceId))
            {
                throw new FormatException("Seed file repeats a source id");
            }

            foods.Add(food);
        }

        return new SeedData(file.SeedVersion.Value, foods);
    }

    private static Food ToFood(FoodDto? dto)
    {
        if (dto is null
            || dto.Source != nameof(FoodSource.CoFID)
            || string.IsNullOrWhiteSpace(dto.SourceId)
            || string.IsNullOrWhiteSpace(dto.Name)
            || !Enum.TryParse<FoodUnit>(dto.Unit, out var unit)
            || dto.EnergyKcal is null
            || dto.ProteinG is null
            || dto.CarbsG is null
            || dto.FatG is null)
        {
            throw new FormatException("Seed food is missing a required value or is not CoFID");
        }

        var name = dto.Name.Trim();
        return new Food
        {
            Source = FoodSource.CoFID,
            SourceId = dto.SourceId,
            Name = name,
            Unit = unit,
            Kcal = dto.EnergyKcal.Value,
            ProteinG = dto.ProteinG.Value,
            CarbsG = dto.CarbsG.Value,
            FatG = dto.FatG.Value,
            FibreG = dto.FibreG,
            SugarsG = dto.SugarsG,
            SaturatesG = dto.SaturatesG,
            SaltG = dto.SaltG,
            SearchText = name.ToLowerInvariant(),
        };
    }

    private sealed record FileDto(int? SeedVersion, List<FoodDto?>? Foods);

    private sealed record FoodDto(
        string? Source,
        string? SourceId,
        string? Name,
        string? Unit,
        double? EnergyKcal,
        double? ProteinG,
        double? CarbsG,
        double? FatG,
        double? FibreG,
        double? SugarsG,
        double? SaturatesG,
        double? SaltG);
}
