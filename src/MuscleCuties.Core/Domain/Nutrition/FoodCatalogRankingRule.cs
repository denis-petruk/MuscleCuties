using System.Globalization;
using System.Text.RegularExpressions;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>One candidate the search ranks, with the fields the ranking compares.</summary>
public sealed record FoodCatalogCandidate(
    int Id,
    string Name,
    float? Calories,
    float? Protein,
    float? Carbs,
    float? Fats,
    bool IsCustom,
    int? FdcId,
    string? DataType,
    string? BrandName,
    string? BrandOwner,
    string? GtinUpc,
    string? Ingredients,
    float? ServingSize,
    string? ServingSizeUnit,
    string? ServingOptionsJson,
    Guid? UserFoodItemId = null
);

/// <summary>
///     Pure domain rule for ranking, deduplicating, and scoring food catalog items deterministically.
///     Zero framework, ORM, or UI dependencies.
/// </summary>
public static partial class FoodCatalogRankingRule
{
    public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "for", "in", "of", "or", "the", "to", "with"
    };

    private static readonly string[] PreparedFoodWords =
    [
        "babyfood", "beverage", "cake", "candy", "canned", "cereal",
        "chips", "drink", "juice", "mix", "sauce", "soup"
    ];

    /// <summary>Orders catalog candidates by how well they match the search text.</summary>
    public static IReadOnlyList<FoodCatalogCandidate> Rank(
        string? query,
        IEnumerable<FoodCatalogCandidate>? candidates)
    {
        if (string.IsNullOrWhiteSpace(query) || candidates is null)
            return [];

        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return [];

        var validCandidates = candidates
            .Where(FoodCatalogNutritionRule.HasDisplayableNutrition)
            .ToList();

        if (validCandidates.Count == 0)
            return [];

        var primaryMatches = validCandidates
            .Where(food => MatchesTokens(BuildPrimarySearchableText(food.Name, food.BrandName, food.BrandOwner, food.GtinUpc), tokens))
            .ToList();

        var matches = primaryMatches.Count > 0
            ? primaryMatches
            : validCandidates
                .Where(food => MatchesTokens(BuildFullSearchableText(BuildPrimarySearchableText(food.Name, food.BrandName, food.BrandOwner, food.GtinUpc), food.Ingredients), tokens))
                .ToList();

        if (matches.Count == 0)
            return [];

        return matches
            .GroupBy(BuildDedupKey)
            .Select(group => group
                .OrderBy(food => RankName(food.Name, tokens))
                .ThenBy(food => WholeFoodRank(food.Name, tokens))
                .ThenBy(food => food.IsCustom ? 0 : 1)
                .ThenByDescending(CountPrimaryMacros)
                .ThenBy(food => food.FdcId.HasValue ? 0 : 1)
                .ThenBy(food => DataTypeRank(food.DataType))
                .ThenBy(food => food.Name.Length)
                .First())
            .OrderBy(food => RankName(food.Name, tokens))
            .ThenBy(food => WholeFoodRank(food.Name, tokens))
            .ThenBy(food => SearchFieldRank(BuildPrimarySearchableText(food.Name, food.BrandName, food.BrandOwner, food.GtinUpc), tokens))
            .ThenBy(food => food.IsCustom ? 0 : 1)
            .ThenBy(food => DataTypeRank(food.DataType))
            .ThenByDescending(CountPrimaryMacros)
            .ThenBy(food => food.Name.Length)
            .ThenBy(food => food.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Cleans a catalogue description into the name the list shows.</summary>
    public static string FormatFoodName(string description)
    {
        var trimmed = description.Trim();
        if (trimmed.Length == 0)
            return trimmed;

        if (trimmed.Any(char.IsLetter) && string.Equals(trimmed, trimmed.ToUpperInvariant(), StringComparison.Ordinal))
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());

        return trimmed;
    }

    /// <summary>Splits the query into tokens, so a multi-word query still matches.</summary>
    public static List<string> Tokenize(string query)
    {
        return NormalizeSearchText(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Folds case and accents, so a search matches however she typed it.</summary>
    public static string NormalizeSearchText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = SearchCleanupRegex().Replace(value.ToLowerInvariant(), " ");
        var tokens = WhiteSpaceRegex()
            .Replace(normalized, " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken);

        return string.Join(" ", tokens).Trim();
    }

    /// <summary>Folds one token to its comparison form.</summary>
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

    /// <summary>
    ///     SQL prefilters must include inflected spellings before the full domain match runs.
    ///     Removing the final y or o broadens the prefilter for berries/berry and potatoes/potato.
    /// </summary>
    public static string SearchPrefilterStem(string normalizedToken)
    {
        if (normalizedToken.Length > 2 &&
            (normalizedToken.EndsWith('y') || normalizedToken.EndsWith('o')))
            return normalizedToken[..^1];

        return normalizedToken;
    }

    /// <summary>Whether the name carries every query token, the minimum to appear at all.</summary>
    private static bool MatchesTokens(string name, IReadOnlyCollection<string> tokens)
    {
        var normalized = NormalizeSearchText(name);
        return tokens.All(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Scores how early and completely the name matches, so an exact name outranks a mention.</summary>
    private static int RankName(string name, List<string> tokens)
    {
        var normalizedName = NormalizeSearchText(name);
        var normalizedQuery = string.Join(" ", tokens);

        if (normalizedName == normalizedQuery)
            return 0;

        if (normalizedName.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            return 1;

        if (normalizedName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            return 2;

        if (tokens.Count > 0 && normalizedName.StartsWith(tokens[0], StringComparison.OrdinalIgnoreCase))
            return 3;

        return 4;
    }

    /// <summary>The bonus a whole food gets, which outranks a branded one at equal match.</summary>
    private static int WholeFoodRank(string name, List<string> tokens)
    {
        var normalizedName = NormalizeSearchText(name);
        if (tokens.Count == 0)
            return 4;

        var normalizedQuery = string.Join(" ", tokens);
        if (normalizedName == normalizedQuery)
            return 0;

        var firstToken = tokens[0];
        if (normalizedName == $"{firstToken} raw" ||
            normalizedName.StartsWith($"{firstToken} raw ", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.StartsWith($"{firstToken} fresh", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (normalizedName.StartsWith($"{firstToken} ", StringComparison.OrdinalIgnoreCase))
            return 2;

        if (PreparedFoodWords.Any(word => normalizedName.Contains(word, StringComparison.OrdinalIgnoreCase)))
            return 4;

        return 3;
    }

    /// <summary>Scores a match found in a secondary field, ranked below the name.</summary>
    private static int SearchFieldRank(string searchableText, IReadOnlyList<string> tokens)
    {
        var normalized = NormalizeSearchText(searchableText);
        return tokens.All(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? 0
            : 1;
    }

    /// <summary>
    /// USDA data-type preference used by search ranking. Shared with the infrastructure filter so the
    /// ordering is defined once.
    /// </summary>
    public static int DataTypeRank(string? dataType)
    {
        return dataType?.Trim().ToUpperInvariant() switch
        {
            "FOUNDATION" => 0,
            "SR LEGACY" => 1,
            "SURVEY (FNDDS)" => 2,
            "BRANDED" => 3,
            _ => 4
        };
    }

    /// <summary>How many macros the entry reports, so a complete entry outranks a sparse one.</summary>
    private static int CountPrimaryMacros(FoodCatalogCandidate food)
    {
        return (food.Protein is > 0f ? 1 : 0) +
               (food.Carbs is > 0f ? 1 : 0) +
               (food.Fats is > 0f ? 1 : 0);
    }

    /// <summary>The key duplicates collapse by, so one food appears once.</summary>
    private static string BuildDedupKey(FoodCatalogCandidate food)
    {
        if (food.IsCustom)
            return food.UserFoodItemId is { } customId
                ? $"custom:{customId:N}"
                : $"custom-legacy:{food.Id.ToString(CultureInfo.InvariantCulture)}";

        // Canonical IDs are stable food identities. Equal names may have different
        // portions, nutrients, sources, or product barcodes.
        return $"catalog:{food.Id.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// The identity text a food is matched against: name plus every brand identifier. Shared with the
    /// infrastructure search filter, which ranks the same catalog shape.
    /// </summary>
    public static string BuildPrimarySearchableText(
        string? name, string? brandName, string? brandOwner, string? gtinUpc) =>
        string.Join(" ", name, brandName, brandOwner, gtinUpc);

    /// <summary>
    /// Primary text plus ingredients, for the lower-confidence match pass.
    /// </summary>
    public static string BuildFullSearchableText(string primaryText, string? ingredients) =>
        string.Join(" ", primaryText, ingredients);

    /// <summary>The generated regex that strips punctuation a search should ignore.</summary>
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SearchCleanupRegex();

    /// <summary>The generated regex that collapses runs of whitespace.</summary>
    [GeneratedRegex("\\s+")]
    private static partial Regex WhiteSpaceRegex();
}
