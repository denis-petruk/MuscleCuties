using System.Text.RegularExpressions;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record YogaClassificationResult(
    bool IsDemanding,
    bool IsInconsistent,
    string? ReasonCode = null);

/// <summary>
/// Resolves yoga classification in one shared place before prescription, duration validation,
/// and hard-session accounting.
/// </summary>
public static class YogaClassificationRule
{
    public const string YogaClassificationInconsistentCode = "YogaClassificationInconsistent";

    private static readonly HashSet<string> DemandingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "power",
        "hot",
        "ashtanga",
        "rocket",
        "intense"
    };

    public static bool ContainsDemandingIndicator(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var span = text.AsSpan();
        var start = -1;
        for (var i = 0; i < span.Length; i++)
        {
            if (char.IsLetterOrDigit(span[i]))
            {
                if (start < 0)
                    start = i;
            }
            else
            {
                if (start >= 0)
                {
                    var token = span.Slice(start, i - start).ToString();
                    if (DemandingKeywords.Contains(token))
                        return true;
                    start = -1;
                }
            }
        }

        if (start >= 0)
        {
            var token = span.Slice(start).ToString();
            if (DemandingKeywords.Contains(token))
                return true;
        }

        return false;
    }

    public static bool ContainsDemandingIndicator(IEnumerable<string>? tags)
    {
        if (tags is null)
            return false;

        foreach (var tag in tags)
        {
            if (ContainsDemandingIndicator(tag))
                return true;
        }

        return false;
    }

    public static YogaClassificationResult Classify(
        bool? explicitClassification,
        string? activityName = null,
        IEnumerable<string>? tags = null)
    {
        var hasDemandingMetadata = ContainsDemandingIndicator(activityName) ||
                                  ContainsDemandingIndicator(tags);

        // If explicit classification conflicts with clearly demanding content metadata:
        if (explicitClassification == false && hasDemandingMetadata)
        {
            return new YogaClassificationResult(
                IsDemanding: true,
                IsInconsistent: true,
                ReasonCode: YogaClassificationInconsistentCode);
        }

        if (explicitClassification.HasValue)
        {
            return new YogaClassificationResult(
                IsDemanding: explicitClassification.Value,
                IsInconsistent: false);
        }

        // When classification is missing, default to demanding if indicators present, else gentle:
        return new YogaClassificationResult(
            IsDemanding: hasDemandingMetadata,
            IsInconsistent: false);
    }
}
