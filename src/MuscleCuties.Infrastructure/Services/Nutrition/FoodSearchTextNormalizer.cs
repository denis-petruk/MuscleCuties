using System.Text.RegularExpressions;

namespace MuscleCuties.Core.Services.Nutrition;

internal static partial class FoodSearchTextNormalizer
{
    public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "for", "in", "of", "or", "the", "to", "with"
    };

    public static string NormalizeSearchText(string value)
    {
        var normalized = SearchCleanupRegex().Replace(value.ToLowerInvariant(), " ");
        var tokens = WhiteSpaceRegex()
            .Replace(normalized, " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken);

        return string.Join(" ", tokens).Trim();
    }

    public static string NormalizeToken(string token)
    {
        if (token.Length <= 3 || token.Any(char.IsDigit))
            return token;

        if (token.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            return $"{token[..^3]}y";

        if (token.EndsWith("oes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("ses", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("zes", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
            token.EndsWith("shes", StringComparison.OrdinalIgnoreCase))
            return token[..^2];

        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return token[..^1];

        return token;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SearchCleanupRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhiteSpaceRegex();
}
