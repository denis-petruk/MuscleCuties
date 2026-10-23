using System.Text.Json;

namespace MuscleCuties.Core.Services.Auth;

public static class ReceiptJsonHelper
{
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

    public static bool TryGetNonemptyString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return false;

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool TryString(JsonElement element, string name, out string value)
    {
        return TryGetNonemptyString(element, name, out value);
    }

    public static bool IsString(JsonElement element, string name, string expected)
    {
        return TryGetNonemptyString(element, name, out var value) &&
               string.Equals(value, expected, StringComparison.Ordinal);
    }

    public static bool TryGetInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    public static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out value);
    }

    public static bool TryInteger(JsonElement element, string name, out long value)
    {
        return TryGetInt64(element, name, out value);
    }
}
