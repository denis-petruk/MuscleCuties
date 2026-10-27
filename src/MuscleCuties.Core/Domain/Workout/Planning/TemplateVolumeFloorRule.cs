using System.Collections.Frozen;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record TemplateVolumeFloor(
    string TemplateId,
    int MinimumWorkingSets,
    int MinimumExercises,
    IReadOnlyDictionary<string, int> MinimumSetsBySlotId,
    string ReviewStatus = TemplateVolumeFloorRule.UnreviewedStatus);

public sealed record TemplateVolumeFloorEvaluationResult(
    bool IsAllowed,
    string? LimitationCode,
    string? ReasonDetail,
    int? MinimumWorkingSets,
    int? ActualWorkingSets);

/// <summary>
/// Clinically reviewed volume floors for strength templates.
/// Prohibits arbitrary exercise-dose limits and enforces reviewed template-specific set floors.
/// </summary>
public static class TemplateVolumeFloorRule
{
    // Limitation & Status Codes
    public const string TemplateVolumeFloorReviewMissing = "TemplateVolumeFloorReviewMissing";
    public const string VolumeBelowTemplateFloor = "VolumeBelowTemplateFloor";
    public const string ReviewedStatus = "ClinicallyReviewed";
    public const string UnreviewedStatus = "Unreviewed";

    // Slot Identifiers
    public const string KneeDominantSlotId = "knee-dominant";
    public const string HipDominantSlotId = "hip-dominant";
    public const string UpperPushSlotId = "upper-push";
    public const string UpperPullSlotId = "upper-pull";
    public const string TrunkSlotId = "trunk";
    public const string GoalSpecificAccessorySlotId = "goal-specific-accessory";

    // Template Identifiers
    public const string FullBody1DayTemplateId = "MC-FB-1D-A";
    public const string FullBody2DaySession1TemplateId = "MC-FB-2D-A";
    public const string FullBody2DaySession2TemplateId = "MC-FB-2D-B";
    public const string FullBody3DaySession1TemplateId = "MC-FB-3D-A";
    public const string FullBody3DaySession2TemplateId = "MC-FB-3D-B";
    public const string FullBody3DaySession3TemplateId = "MC-FB-3D-C";
    public const string UpperLower4DayUpper1TemplateId = "MC-UL-4D-UPPER-A";
    public const string UpperLower4DayLower1TemplateId = "MC-UL-4D-LOWER-A";
    public const string UpperLower4DayUpper2TemplateId = "MC-UL-4D-UPPER-B";
    public const string UpperLower4DayLower2TemplateId = "MC-UL-4D-LOWER-B";

    // Clinical Threshold Constants
    public const int FullBodyMinimumWorkingSets = 6;
    public const int FullBodyMinimumExercises = 3;
    public const int FullBodyMainSlotMinimumSets = 1;

    public const int UpperLowerMinimumWorkingSets = 4;
    public const int UpperLowerMinimumExercises = 2;
    public const int UpperLowerMainSlotMinimumSets = 2;

    public const int OptionalSlotMinimumSets = 0;

    public static readonly FrozenDictionary<string, TemplateVolumeFloor> StandardReviewedFloors =
        BuildStandardReviewedFloors();

    private static FrozenDictionary<string, TemplateVolumeFloor> BuildStandardReviewedFloors()
    {
        var floors = new Dictionary<string, TemplateVolumeFloor>(StringComparer.Ordinal)
        {
            [FullBody1DayTemplateId] = CreateFullBodyFloor(FullBody1DayTemplateId),
            [FullBody2DaySession1TemplateId] = CreateFullBodyFloor(FullBody2DaySession1TemplateId),
            [FullBody2DaySession2TemplateId] = CreateFullBodyFloor(FullBody2DaySession2TemplateId),
            [FullBody3DaySession1TemplateId] = CreateFullBodyFloor(FullBody3DaySession1TemplateId),
            [FullBody3DaySession2TemplateId] = CreateFullBodyFloor(FullBody3DaySession2TemplateId),
            [FullBody3DaySession3TemplateId] = CreateFullBodyFloor(FullBody3DaySession3TemplateId),
            [UpperLower4DayUpper1TemplateId] = CreateUpperFloor(UpperLower4DayUpper1TemplateId),
            [UpperLower4DayLower1TemplateId] = CreateLowerFloor(UpperLower4DayLower1TemplateId),
            [UpperLower4DayUpper2TemplateId] = CreateUpperFloor(UpperLower4DayUpper2TemplateId),
            [UpperLower4DayLower2TemplateId] = CreateLowerFloor(UpperLower4DayLower2TemplateId)
        };
        return floors.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static TemplateVolumeFloor CreateFullBodyFloor(string templateId) =>
        new(
            templateId,
            MinimumWorkingSets: FullBodyMinimumWorkingSets,
            MinimumExercises: FullBodyMinimumExercises,
            MinimumSetsBySlotId: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [KneeDominantSlotId] = FullBodyMainSlotMinimumSets,
                [HipDominantSlotId] = FullBodyMainSlotMinimumSets,
                [UpperPushSlotId] = FullBodyMainSlotMinimumSets,
                [UpperPullSlotId] = FullBodyMainSlotMinimumSets,
                [TrunkSlotId] = OptionalSlotMinimumSets,
                [GoalSpecificAccessorySlotId] = OptionalSlotMinimumSets
            }.ToFrozenDictionary(StringComparer.Ordinal),
            ReviewStatus: ReviewedStatus);

    private static TemplateVolumeFloor CreateUpperFloor(string templateId) =>
        new(
            templateId,
            MinimumWorkingSets: UpperLowerMinimumWorkingSets,
            MinimumExercises: UpperLowerMinimumExercises,
            MinimumSetsBySlotId: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [UpperPushSlotId] = UpperLowerMainSlotMinimumSets,
                [UpperPullSlotId] = UpperLowerMainSlotMinimumSets,
                [TrunkSlotId] = OptionalSlotMinimumSets,
                [GoalSpecificAccessorySlotId] = OptionalSlotMinimumSets
            }.ToFrozenDictionary(StringComparer.Ordinal),
            ReviewStatus: ReviewedStatus);

    private static TemplateVolumeFloor CreateLowerFloor(string templateId) =>
        new(
            templateId,
            MinimumWorkingSets: UpperLowerMinimumWorkingSets,
            MinimumExercises: UpperLowerMinimumExercises,
            MinimumSetsBySlotId: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [KneeDominantSlotId] = UpperLowerMainSlotMinimumSets,
                [HipDominantSlotId] = UpperLowerMainSlotMinimumSets,
                [TrunkSlotId] = OptionalSlotMinimumSets,
                [GoalSpecificAccessorySlotId] = OptionalSlotMinimumSets
            }.ToFrozenDictionary(StringComparer.Ordinal),
            ReviewStatus: ReviewedStatus);

    private const int ZeroThreshold = 0;

    public static TemplateVolumeFloor? GetReviewedFloor(
        string? templateId,
        IReadOnlyDictionary<string, TemplateVolumeFloor>? customFloors = null)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        if (customFloors is not null && customFloors.TryGetValue(templateId, out var customFloor))
        {
            return customFloor.TemplateId == templateId &&
                   customFloor.ReviewStatus == ReviewedStatus &&
                   customFloor.MinimumWorkingSets > ZeroThreshold &&
                   customFloor.MinimumExercises > ZeroThreshold &&
                   customFloor.MinimumSetsBySlotId.Count > ZeroThreshold &&
                   customFloor.MinimumSetsBySlotId.All(pair => pair.Value >= ZeroThreshold)
                ? customFloor
                : null;
        }

        return StandardReviewedFloors.GetValueOrDefault(templateId);
    }

    public static bool HasReviewedFloor(
        string? templateId,
        IReadOnlyDictionary<string, TemplateVolumeFloor>? customFloors = null) =>
        GetReviewedFloor(templateId, customFloors) is not null;

    public static TemplateVolumeFloorEvaluationResult Evaluate(
        string templateId,
        int actualWorkingSets,
        IReadOnlyDictionary<string, int>? actualSetsBySlot = null,
        IReadOnlyDictionary<string, TemplateVolumeFloor>? customFloors = null)
    {
        var floor = GetReviewedFloor(templateId, customFloors);
        if (floor is null)
        {
            return new TemplateVolumeFloorEvaluationResult(
                IsAllowed: false,
                LimitationCode: TemplateVolumeFloorReviewMissing,
                ReasonDetail: $"Template '{templateId}' does not have a clinically reviewed volume floor.",
                MinimumWorkingSets: null,
                ActualWorkingSets: actualWorkingSets);
        }

        if (actualWorkingSets < floor.MinimumWorkingSets)
        {
            return new TemplateVolumeFloorEvaluationResult(
                IsAllowed: false,
                LimitationCode: VolumeBelowTemplateFloor,
                ReasonDetail: $"Working sets ({actualWorkingSets}) is below template floor ({floor.MinimumWorkingSets}) for '{templateId}'.",
                MinimumWorkingSets: floor.MinimumWorkingSets,
                ActualWorkingSets: actualWorkingSets);
        }

        if (actualSetsBySlot is not null)
        {
            foreach (var (slotId, minSets) in floor.MinimumSetsBySlotId)
            {
                var actualSets = actualSetsBySlot.GetValueOrDefault(slotId, OptionalSlotMinimumSets);
                if (actualSets < minSets)
                {
                    return new TemplateVolumeFloorEvaluationResult(
                        IsAllowed: false,
                        LimitationCode: VolumeBelowTemplateFloor,
                        ReasonDetail: $"Slot '{slotId}' working sets ({actualSets}) is below floor ({minSets}) for template '{templateId}'.",
                        MinimumWorkingSets: floor.MinimumWorkingSets,
                        ActualWorkingSets: actualWorkingSets);
                }
            }
        }

        return new TemplateVolumeFloorEvaluationResult(
            IsAllowed: true,
            LimitationCode: null,
            ReasonDetail: null,
            MinimumWorkingSets: floor.MinimumWorkingSets,
            ActualWorkingSets: actualWorkingSets);
    }
}
