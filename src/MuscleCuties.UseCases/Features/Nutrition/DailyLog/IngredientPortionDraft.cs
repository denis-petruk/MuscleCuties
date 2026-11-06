using System.Globalization;

namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

/// <summary>
/// NM02: the editable portion text for one ingredient row. It owns the two rules the UI must not
/// paraphrase: an untouched draft keeps the stored quantity exactly, and an edited draft is parsed
/// predictably for the current culture without reinterpreting an ambiguous grouping separator.
/// </summary>
public static class IngredientPortionDraft
{
    /// <summary>Round-trip text, so re-opening a row never rounds its stored quantity.</summary>
    public static string Format(float grams) => Format(grams, CultureInfo.CurrentCulture);

    public static string Format(float grams, CultureInfo culture) =>
        grams.ToString("R", culture ?? CultureInfo.CurrentCulture);

    /// <summary>
    /// Resolves the draft to grams. <paramref name="originalText"/> is the text produced by
    /// <see cref="Format"/> when the row was opened; an unchanged draft returns
    /// <paramref name="originalGrams"/> untouched.
    /// </summary>
    public static bool TryParse(string? text, string? originalText, float originalGrams,
        CultureInfo culture, out float grams)
    {
        grams = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var candidate = text.Trim();
        if (!string.IsNullOrEmpty(originalText) &&
            string.Equals(candidate, originalText, StringComparison.Ordinal) &&
            IsUsable(originalGrams))
        {
            // Existing canonical data is valid by definition; an untouched row must not be rewritten.
            grams = originalGrams;
            return true;
        }

        // "1,234" and "1.234" read as a thousands grouping in some cultures and as a decimal in
        // others, so neither is assumed: the row asks for a clearer value instead.
        if (LooksLikeGroupedThousands(candidate))
            return false;

        var format = culture ?? CultureInfo.CurrentCulture;
        if (float.TryParse(candidate, NumberStyles.Float, format, out grams) && IsUsable(grams))
            return true;

        // Accept the single alternate decimal separator the person may have typed (12,5 in en-US,
        // 12.5 in ru-RU) when it is unambiguous for their culture.
        var decimalSeparator = format.NumberFormat.NumberDecimalSeparator;
        var alternateSeparator = decimalSeparator == "." ? "," : ".";
        if (!candidate.Contains(decimalSeparator, StringComparison.Ordinal) &&
            candidate.Contains(alternateSeparator, StringComparison.Ordinal))
        {
            var normalized = candidate.Replace(alternateSeparator, decimalSeparator, StringComparison.Ordinal);
            if (float.TryParse(normalized, NumberStyles.Float, format, out grams) && IsUsable(grams))
                return true;
        }

        grams = 0;
        return false;
    }

    private static bool IsUsable(float grams) => float.IsFinite(grams) && grams > 0;

    /// <summary>
    /// True for a value shaped like a grouped number: one to three leading digits followed by
    /// separator-delimited groups of exactly three digits.
    /// </summary>
    private static bool LooksLikeGroupedThousands(string value)
    {
        var start = value.Length > 0 && (value[0] == '+' || value[0] == '-') ? 1 : 0;
        var body = value[start..];
        if (body.Length == 0)
            return false;

        var parts = body.Split(['.', ',']);
        if (parts.Length < 2 || parts[0].Length is 0 or > 3)
            return false;

        for (var index = 1; index < parts.Length; index++)
        {
            if (parts[index].Length != 3)
                return false;
        }

        foreach (var part in parts)
        {
            foreach (var character in part)
            {
                if (character is < '0' or > '9')
                    return false;
            }
        }

        return true;
    }
}
