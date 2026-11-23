namespace MuscleCuties.UI.Services.Localization;

/// <summary>
/// Format placeholders are positional. One substitution path keeps the generation mapper and the session
/// display producing identical text for the same key.
/// </summary>
public static class LocalizedFormat
{
    public static string Format(string key, params string[] arguments)
    {
        var formatted = UiTextCatalog.Get(key);
        for (var argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
            formatted = formatted.Replace($"{{{argumentIndex}}}", arguments[argumentIndex], StringComparison.Ordinal);
        return formatted;
    }
}
