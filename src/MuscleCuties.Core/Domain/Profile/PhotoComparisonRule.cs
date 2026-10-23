namespace MuscleCuties.Core.Domain.Profile;

public static class PhotoComparisonRule
{
    private const int MinimumStoredPhotoId = 1;

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
