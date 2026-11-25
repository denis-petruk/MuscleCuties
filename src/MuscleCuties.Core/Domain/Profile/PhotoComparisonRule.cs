namespace MuscleCuties.Core.Domain.Profile;

/// <summary>Compares two photos only when their scale and framing allow it, so a change is not an artefact.</summary>
public static class PhotoComparisonRule
{
    private const int MinimumStoredPhotoId = 1;

    /// <summary>True when two photos are recent enough and distinct enough to compare.</summary>
    public static bool CanCompare(
        int firstId,
        string? firstPath,
        bool firstAvailable,
        int secondId,
        string? secondPath,
        bool secondAvailable)
    {
        return firstId >= MinimumStoredPhotoId && secondId >= MinimumStoredPhotoId &&
               firstId != secondId && firstAvailable && secondAvailable &&
               !string.IsNullOrWhiteSpace(firstPath) && !string.IsNullOrWhiteSpace(secondPath);
    }
}
