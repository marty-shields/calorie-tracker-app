using CalorieTracker.Core.Data;

namespace CalorieTracker.Core.Foods;

/// <summary>
/// Ranks foods against typed words in memory. A food matches when every word matches one of its words; the score decides the order.
/// Main words are the name before its first bracket plus the brand; qualifier words are what is inside the brackets.
/// </summary>
internal static class FoodRanker
{
    /// <summary>The characters that split names and typed queries into words.</summary>
    internal static readonly char[] Separators = [' ', '\t', '\r', '\n', ',', '(', ')', '/', '-', ';'];

    /// <summary>A typed word needs this many letters before one wrong letter is forgiven.</summary>
    internal const int MinTypoLength = 4;

    private const double MainEquals = 100;
    private const double MainPrefix = 80;
    private const double MainTypo = 40;
    private const double QualifierEquals = 15;
    private const double QualifierPrefix = 10;
    private const double HeadBonus = 15;
    private const double ExtraMainWordPenalty = 2;
    private const double ExtraQualifierWordPenalty = 0.5;
    private const int MaxPenalisedWords = 10;
    private const double CommonFirstPart = 30;
    private const double CommonAverage = 30;
    private const double CommonRaw = 5;

    public static string[] Words(string folded) => folded.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Folds a plural to its singular and "yogurt" to "yoghurt", so "potatoes", "potatos" and "potato" meet. Applied to typed and stored words alike, so it need not produce a real word.</summary>
    public static string Normalise(string word)
    {
        word = word.Replace("yogurt", "yoghurt", StringComparison.Ordinal);
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal))
        {
            return word[..^3] + "y";
        }

        if (word.Length > 4 && word.EndsWith("ie", StringComparison.Ordinal))
        {
            return word[..^2] + "y";
        }

        if (word.Length > 4 && (word.EndsWith("oes", StringComparison.Ordinal) || word.EndsWith("ches", StringComparison.Ordinal)
            || word.EndsWith("shes", StringComparison.Ordinal) || word.EndsWith("xes", StringComparison.Ordinal) || word.EndsWith("sses", StringComparison.Ordinal)))
        {
            return word[..^2];
        }

        if (word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) && !word.EndsWith("us", StringComparison.Ordinal) && !word.EndsWith("is", StringComparison.Ordinal))
        {
            return word[..^1];
        }

        return word;
    }

    /// <summary>The matching foods, best first; ties break by name (ignoring case), then id. <paramref name="typed"/> must be folded but not normalised.</summary>
    public static List<Food> Rank(IEnumerable<Food> foods, IReadOnlyList<string> typed, int limit)
    {
        var terms = typed.Select(t => new Word(t)).ToList();
        var ranked = new List<(Food Food, double Score)>();
        foreach (var food in foods)
        {
            if (Score(food, terms) is { } score)
            {
                ranked.Add((food, score));
            }
        }

        return ranked
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Food.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Food.Id)
            .Take(limit)
            .Select(r => r.Food)
            .ToList();
    }

    /// <summary>A word as written plus its normalised form, and for "-oes" words the form with only the "s" dropped, so "sloe" meets "sloes" and "potatoe" meets "potatoes".</summary>
    private readonly struct Word
    {
        public Word(string raw)
        {
            Raw = raw;
            Norm = Normalise(raw);
            Alt = raw.Length > 4 && raw.EndsWith("oes", StringComparison.Ordinal) ? raw[..^1] : null;
        }

        public string Raw { get; }

        public string Norm { get; }

        public string? Alt { get; }

        public bool Matches(Word other) => Norm == other.Norm || (Alt is not null && (Alt == other.Norm || Alt == other.Alt)) || (other.Alt is not null && Norm == other.Alt);

        public bool StartsWith(Word typed) =>
            Norm.StartsWith(typed.Norm, StringComparison.Ordinal)
            || Raw.StartsWith(typed.Raw, StringComparison.Ordinal)
            || (typed.Alt is not null && (Norm.StartsWith(typed.Alt, StringComparison.Ordinal) || Raw.StartsWith(typed.Alt, StringComparison.Ordinal)));
    }

    private static List<Word> ToWords(string folded) => Words(folded).Select(w => new Word(w)).ToList();

    private static double? Score(Food food, IReadOnlyList<Word> terms)
    {
        var name = SearchFolding.Fold(food.Name);
        var bracket = name.IndexOf('(', StringComparison.Ordinal);
        var nameMain = ToWords(bracket < 0 ? name : name[..bracket]);
        var qualifier = bracket < 0 ? [] : ToWords(name[bracket..]);
        var main = new List<Word>(nameMain);
        if (!string.IsNullOrWhiteSpace(food.Brand))
        {
            main.AddRange(ToWords(SearchFolding.Fold(food.Brand)));
        }

        var head = nameMain.Count - 1;
        var score = 0d;
        var singleByTypo = false;
        foreach (var term in terms)
        {
            var best = BestMatch(term, main, qualifier, head);
            if (best is null)
            {
                return null;
            }

            score += best.Value;
            singleByTypo = best.Value == MainTypo;
        }

        score -= ExtraMainWordPenalty * Math.Min(Math.Max(0, main.Count - terms.Count), MaxPenalisedWords);
        score -= ExtraQualifierWordPenalty * Math.Min(qualifier.Count, MaxPenalisedWords);
        if (terms.Count == 1 && !singleByTypo)
        {
            score += CommonFoodBoost(food, terms[0].Norm);
        }

        return score;
    }

    private static double? BestMatch(Word term, List<Word> main, List<Word> qualifier, int head)
    {
        double? best = null;
        for (var i = 0; i < main.Count; i++)
        {
            var bonus = i == head ? HeadBonus : 0;
            if (main[i].Matches(term))
            {
                best = Math.Max(best ?? 0, MainEquals + bonus);
            }
            else if (main[i].StartsWith(term))
            {
                best = Math.Max(best ?? 0, MainPrefix + bonus);
            }
            else if (term.Norm.Length >= MinTypoLength && IsOneEditAway(term.Norm, main[i].Norm))
            {
                best = Math.Max(best ?? 0, MainTypo);
            }
        }

        if (best is not null)
        {
            return best;
        }

        foreach (var word in qualifier)
        {
            if (word.Matches(term))
            {
                best = Math.Max(best ?? 0, QualifierEquals);
            }
            else if (word.StartsWith(term))
            {
                best = Math.Max(best ?? 0, QualifierPrefix);
            }
        }

        return best;
    }

    /// <summary>CoFID names its common foods "Milk, whole, pasteurised, average": the first part is the food itself, and "average" and "raw" mark the plain version.</summary>
    private static double CommonFoodBoost(Food food, string term)
    {
        if (food.Source != FoodSource.CoFID || food.OriginalName is null)
        {
            return 0;
        }

        var parts = SearchFolding.Fold(food.OriginalName).Split(',', StringSplitOptions.TrimEntries);
        if (Normalise(parts[0]) != term)
        {
            return 0;
        }

        var boost = CommonFirstPart;
        if (parts.Skip(1).Contains("average"))
        {
            boost += CommonAverage;
        }

        if (parts.Skip(1).Contains("raw"))
        {
            boost += CommonRaw;
        }

        return boost;
    }

    /// <summary>True when the words differ by exactly one insert, delete, substitute or swap of neighbours.</summary>
    internal static bool IsOneEditAway(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1 || a == b)
        {
            return false;
        }

        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }

        var restA = a.AsSpan(i);
        var restB = b.AsSpan(i);
        if (restA.Length == restB.Length)
        {
            return restA[1..].SequenceEqual(restB[1..])
                || (restA.Length >= 2 && restA[0] == restB[1] && restA[1] == restB[0] && restA[2..].SequenceEqual(restB[2..]));
        }

        return restA.Length > restB.Length ? restA[1..].SequenceEqual(restB) : restA.SequenceEqual(restB[1..]);
    }
}
