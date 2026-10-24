namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum StrengthHighIntensityExerciseEvidenceStatus
{
    NotConfirmable,
    RequiresUserConfirmation,
    Confirmed
}

/// <summary>Separates trusted exercise evidence from the user's explicit readiness confirmation.</summary>
public static class StrengthHighIntensityExerciseEvidenceRule
{
    public const string TrustedEvidenceMissing = "HighIntensityMainNeedsFourExposuresKnownManageableLoad";
    public const string UserConfirmationRequired = "HighIntensityMainRequiresExplicitUserConfirmation";

    private const int MinimumPositiveExerciseId = 1;
    private const int NoCompletedExposures = 0;

    public static StrengthHighIntensityExerciseEvidenceStatus Evaluate(
        int exerciseId,
        int? completedExposureCount,
        bool hasTrustedManageableLoadEvidence,
        bool userConfirmedFamiliarity,
        bool userConfirmedManageableLoad)
    {
        if (exerciseId < MinimumPositiveExerciseId || completedExposureCount is null ||
            completedExposureCount < ActivityPrescriptionRule.StrengthHighIntensityRequiredExerciseExposures ||
            !hasTrustedManageableLoadEvidence)
            return StrengthHighIntensityExerciseEvidenceStatus.NotConfirmable;

        if (!userConfirmedFamiliarity || !userConfirmedManageableLoad)
            return StrengthHighIntensityExerciseEvidenceStatus.RequiresUserConfirmation;

        return StrengthHighIntensityExerciseEvidenceStatus.Confirmed;
    }

    public static bool IsConfirmableCandidate(
        int exerciseId,
        IReadOnlyDictionary<int, int> completedExerciseExposureCounts,
        IReadOnlySet<int> knownManageableLoadExerciseIds)
    {
        ArgumentNullException.ThrowIfNull(completedExerciseExposureCounts);
        ArgumentNullException.ThrowIfNull(knownManageableLoadExerciseIds);

        return Evaluate(exerciseId,
            completedExerciseExposureCounts.GetValueOrDefault(exerciseId, NoCompletedExposures),
            knownManageableLoadExerciseIds.Contains(exerciseId),
            userConfirmedFamiliarity: false,
            userConfirmedManageableLoad: false) != StrengthHighIntensityExerciseEvidenceStatus.NotConfirmable;
    }
}
