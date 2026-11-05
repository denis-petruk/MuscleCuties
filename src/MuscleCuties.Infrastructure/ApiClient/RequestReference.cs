namespace MuscleCuties.Infrastructure.ApiClient;

/// <summary>
/// G7: the server reference shown next to a failure is a bounded opaque token, not free text. The
/// repository server emits its trace identifier, but a synthetic or unexpected <c>requestId</c> could
/// contain a URL, a token or other noise, so anything that is not a short opaque reference is
/// omitted instead of rendered.
/// </summary>
public static class RequestReference
{
    /// <summary>The longest reference the screen keeps; longer values are not references at all.</summary>
    public const int MaximumLength = 64;

    /// <summary>
    /// True when <paramref name="value"/> is a bounded opaque reference that is safe to display.
    /// </summary>
    public static bool IsDisplayable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        if (candidate.Length is 0 or > MaximumLength)
            return false;

        foreach (var character in candidate)
        {
            var allowed = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or
                '.' or '-' or '_' or ':';
            if (!allowed)
                return false;
        }

        // A scheme separator or URL path never belongs to an opaque trace reference.
        if (candidate.Contains("//", StringComparison.Ordinal) ||
            candidate.Contains("..", StringComparison.Ordinal))
            return false;

        return true;
    }

    /// <summary>The trimmed reference, or null when it is not displayable.</summary>
    public static string? Sanitize(string? value) => IsDisplayable(value) ? value!.Trim() : null;
}
