namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>
/// What the micronutrient totals in a daily overview actually cover. Independent food snapshots
/// contribute macros only; catalog and user-food meal entries carry the nutrient joins. A day can
/// therefore be complete, partial, entirely unavailable, or genuinely empty.
/// </summary>
public enum MicronutrientCoverageStatus
{
    KnownZero,
    Complete,
    Partial,
    Unavailable
}

public static class MicronutrientCoverageRule
{
    /// <summary>
    /// Classifies coverage from the number of logged items that carry nutrient data and the number
    /// that do not. Counts are item occurrences, not nutrients. A negative count is a programming
    /// error and must not be presented as a coverage state.
    /// </summary>
    public static MicronutrientCoverageStatus Classify(int coveredItemCount, int uncoveredItemCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coveredItemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(uncoveredItemCount);

        if (coveredItemCount == 0 && uncoveredItemCount == 0)
            return MicronutrientCoverageStatus.KnownZero;
        if (uncoveredItemCount == 0)
            return MicronutrientCoverageStatus.Complete;
        if (coveredItemCount == 0)
            return MicronutrientCoverageStatus.Unavailable;
        return MicronutrientCoverageStatus.Partial;
    }
}
