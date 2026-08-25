using System.Globalization;
using System.Text.RegularExpressions;
using MuscleCuties.Core.Models.Entities.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

public static partial class FoodSearchResultFilter
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "and",
        "for",
        "in",
        "of",
        "or",
        "the",
        "to",
        "with"
    };

    public static List<FoodItem> PrepareFoodItems(
        string query,
        IEnumerable<FoodItem> foods)
    {
        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return [];

        var candidates = foods
            .Where(food => food.Calories > 0f)
            .ToList();

        var primaryMatches = candidates
            .Where(food => MatchesTokens(BuildPrimarySearchableText(food), tokens))
            .ToList();

        var matches = primaryMatches.Count > 0
            ? primaryMatches
            : candidates
                .Where(food => MatchesTokens(BuildFullSearchableText(food), tokens))
                .ToList();

        return matches
            .GroupBy(BuildDedupKey)
            .Select(group => group
                .OrderBy(food => RankName(food.Name, tokens))
                .ThenBy(food => WholeFoodRank(food.Name, tokens))
                .ThenBy(food => food.IsCustom ? 0 : 1)
                .ThenByDescending(food => CountPrimaryMacros(food))
                .ThenBy(food => food.FdcId.HasValue ? 0 : 1)
                .ThenBy(food => DataTypeRank(food.DataType))
                .ThenBy(food => food.Name.Length)
                .First())
            .OrderBy(food => RankName(food.Name, tokens))
            .ThenBy(food => WholeFoodRank(food.Name, tokens))
            .ThenBy(food => SearchFieldRank(BuildPrimarySearchableText(food), tokens))
            .ThenBy(food => food.IsCustom ? 0 : 1)
            .ThenBy(food => DataTypeRank(food.DataType))
            .ThenByDescending(food => CountPrimaryMacros(food))
            .ThenBy(food => food.Name.Length)
            .ThenBy(food => food.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<FdcFoodSearchResult> PrepareRemoteResults(
        string query,
        IEnumerable<FdcFoodSearchResult> results)
    {
        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return [];

        var candidates = results
            .Where(result => result.FdcId > 0)
            .ToList();

        var primaryMatches = candidates
            .Where(result => MatchesTokens(BuildPrimarySearchableText(result), tokens))
            .ToList();

        var matches = primaryMatches.Count > 0
            ? primaryMatches
            : candidates
                .Where(result => MatchesTokens(BuildFullSearchableText(result), tokens))
                .ToList();

        return matches
            .GroupBy(BuildDedupKey)
            .Select(group => group
                .OrderBy(result => RankName(result.Description, tokens))
                .ThenBy(result => WholeFoodRank(result.Description, tokens))
                .ThenBy(result => SearchFieldRank(BuildPrimarySearchableText(result), tokens))
                .ThenBy(result => DataTypeRank(result.DataType))
                .ThenByDescending(CountSearchPrimaryMacros)
                .ThenBy(result => HasBrandIdentifier(result) ? 0 : 1)
                .ThenBy(result => result.Description.Length)
                .First())
            .OrderBy(result => RankName(result.Description, tokens))
            .ThenBy(result => WholeFoodRank(result.Description, tokens))
            .ThenBy(result => SearchFieldRank(BuildPrimarySearchableText(result), tokens))
            .ThenBy(result => DataTypeRank(result.DataType))
            .ThenByDescending(CountSearchPrimaryMacros)
            .ThenBy(result => result.Description.Length)
            .ToList();
    }

    public static string FormatFoodName(string description)
    {
        var trimmed = description.Trim();
        if (trimmed.Length == 0)
            return trimmed;

        if (trimmed.Any(char.IsLetter) && trimmed == trimmed.ToUpperInvariant())
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }

        return trimmed;
    }

    private static bool MatchesTokens(string name, IReadOnlyCollection<string> tokens)
    {
        var normalized = NormalizeSearchText(name);
        return tokens.All(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static int RankName(string name, IReadOnlyList<string> tokens)
    {
        var normalizedName = NormalizeSearchText(name);
        var normalizedQuery = string.Join(" ", tokens);

        if (normalizedName == normalizedQuery)
            return 0;

        if (normalizedName.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            return 1;

        if (normalizedName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            return 2;

        if (tokens.Count > 0 && normalizedName.StartsWith(tokens.First(), StringComparison.OrdinalIgnoreCase))
            return 3;

        return 4;
    }

    private static int WholeFoodRank(string name, IReadOnlyList<string> tokens)
    {
        var normalizedName = NormalizeSearchText(name);
        if (tokens.Count == 0)
            return 4;

        var normalizedQuery = string.Join(" ", tokens);
        if (normalizedName == normalizedQuery)
            return 0;

        var firstToken = tokens.First();
        if (normalizedName == $"{firstToken} raw" ||
            normalizedName.StartsWith($"{firstToken} raw ", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.StartsWith($"{firstToken} fresh", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (normalizedName.StartsWith($"{firstToken} ", StringComparison.OrdinalIgnoreCase))
            return 2;

        if (ContainsPreparedFoodWord(normalizedName))
            return 4;

        return 3;
    }

    private static bool ContainsPreparedFoodWord(string normalizedName)
    {
        string[] words =
        [
            "babyfood",
            "beverage",
            "cake",
            "candy",
            "canned",
            "cereal",
            "chips",
            "drink",
            "juice",
            "mix",
            "sauce",
            "soup"
        ];

        return words.Any(word => normalizedName.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static int SearchFieldRank(string searchableText, IReadOnlyList<string> tokens)
    {
        var normalized = NormalizeSearchText(searchableText);
        return tokens.All(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? 0
            : 1;
    }

    private static int DataTypeRank(string? dataType) => dataType?.Trim().ToUpperInvariant() switch
    {
        "FOUNDATION" => 0,
        "SR LEGACY" => 1,
        "SURVEY (FNDDS)" => 2,
        "BRANDED" => 3,
        _ => 4
    };

    private static int CountPrimaryMacros(FoodItem food) =>
        Convert.ToInt32(food.Protein > 0f) +
        Convert.ToInt32(food.Carbs > 0f) +
        Convert.ToInt32(food.Fats > 0f);

    private static int CountSearchPrimaryMacros(FdcFoodSearchResult result)
    {
        var nutrientIds = result.FoodNutrients
            .Where(n => n.Value is > 0f)
            .Select(n => n.NutrientId)
            .ToHashSet();

        return Convert.ToInt32(nutrientIds.Contains(1003)) +
               Convert.ToInt32(nutrientIds.Contains(1005)) +
               Convert.ToInt32(nutrientIds.Contains(1004));
    }

    private static string BuildDedupKey(FoodItem food)
    {
        var name = NormalizeName(food.Name);
        if (!IsBranded(food.DataType))
            return $"generic:{name}";

        return string.Join(
            "|",
            "branded",
            name,
            NormalizeSearchText(food.BrandName ?? string.Empty),
            NormalizeSearchText(food.BrandOwner ?? string.Empty),
            NormalizeSearchText(food.GtinUpc ?? string.Empty));
    }

    private static string BuildPrimarySearchableText(FoodItem food) =>
        string.Join(
            " ",
            food.Name,
            food.BrandName,
            food.BrandOwner,
            food.GtinUpc);

    private static string BuildFullSearchableText(FoodItem food) =>
        string.Join(
            " ",
            BuildPrimarySearchableText(food),
            food.Ingredients);

    private static string BuildDedupKey(FdcFoodSearchResult result)
    {
        var name = NormalizeName(result.Description);
        if (!IsBranded(result.DataType))
            return $"generic:{name}";

        return string.Join(
            "|",
            "branded",
            name,
            NormalizeSearchText(result.BrandName ?? string.Empty),
            NormalizeSearchText(result.BrandOwner ?? string.Empty),
            NormalizeSearchText(result.GtinUpc ?? string.Empty));
    }

    private static string BuildPrimarySearchableText(FdcFoodSearchResult result) =>
        string.Join(
            " ",
            result.Description,
            result.BrandName,
            result.BrandOwner,
            result.GtinUpc);

    private static string BuildFullSearchableText(FdcFoodSearchResult result) =>
        string.Join(
            " ",
            BuildPrimarySearchableText(result),
            result.Ingredients);

    private static bool HasBrandIdentifier(FdcFoodSearchResult result) =>
        !string.IsNullOrWhiteSpace(result.BrandName) ||
        !string.IsNullOrWhiteSpace(result.BrandOwner) ||
        !string.IsNullOrWhiteSpace(result.GtinUpc);

    private static bool IsBranded(string? dataType) =>
        string.Equals(dataType?.Trim(), "Branded", StringComparison.OrdinalIgnoreCase);

    private static List<string> Tokenize(string query) =>
        NormalizeSearchText(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeName(string name) =>
        NormalizeSearchText(name);

    private static string NormalizeSearchText(string value)
    {
        var normalized = SearchCleanupRegex().Replace(value.ToLowerInvariant(), " ");
        var tokens = WhiteSpaceRegex()
            .Replace(normalized, " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken);

        return string.Join(" ", tokens).Trim();
    }

    private static string NormalizeToken(string token)
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
        {
            return token[..^2];
        }

        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return token[..^1];

        return token;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SearchCleanupRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhiteSpaceRegex();
}
