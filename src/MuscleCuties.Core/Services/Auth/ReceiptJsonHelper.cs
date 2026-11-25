using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace MuscleCuties.Core.Services.Auth;

/// <summary>Reads provider receipts defensively, so a malformed payload is refused rather than trusted.</summary>
public static class ReceiptJsonHelper
{
    /// <summary>Whether the payload carries exactly the expected fields, so a substituted receipt is refused.</summary>
    public static bool HasExactFields(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!expected.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                return false;

        return seen.Count == expected.Length;
    }

    /// <summary>A required string field, answering false when it is missing or blank.</summary>
    public static bool TryGetNonemptyString(JsonElement element, string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>Whether a field is a JSON string rather than another type.</summary>
    public static bool IsString(JsonElement element, string name, string expected)
    {
        return TryGetNonemptyString(element, name, out var value) &&
               string.Equals(value, expected, StringComparison.Ordinal);
    }

    /// <summary>A required integer field, answering false when it is missing or of another type.</summary>
    public static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out value);
    }
}
