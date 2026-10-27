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
/// Applies explicitly supplied template-specific volume floors without inferring missing limits.
/// </summary>
public static class TemplateVolumeFloorRule
{
    // Limitation & Status Codes
    public const string TemplateVolumeFloorReviewMissing = "TemplateVolumeFloorReviewMissing";
    public const string VolumeBelowTemplateFloor = "VolumeBelowTemplateFloor";
    public const string ReviewedStatus = "Configured";
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

    private const int ZeroThreshold = 0;
    private const int OptionalSlotMinimumSets = 0;

    public static TemplateVolumeFloor? GetReviewedFloor(
        string? templateId,
        IReadOnlyDictionary<string, TemplateVolumeFloor>? customFloors = null)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        if (customFloors is not null && customFloors.TryGetValue(templateId, out var customFloor))
        {
            return customFloor is not null && customFloor.TemplateId == templateId &&
                   customFloor.ReviewStatus == ReviewedStatus &&
                   customFloor.MinimumWorkingSets > ZeroThreshold &&
                   customFloor.MinimumExercises > ZeroThreshold &&
                   customFloor.MinimumSetsBySlotId is not null && customFloor.MinimumSetsBySlotId.Count > ZeroThreshold &&
                   customFloor.MinimumSetsBySlotId.All(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value >= ZeroThreshold)
                ? customFloor
                : null;
        }

        return null;
    }

    public static bool HasReviewedFloor(
        string? templateId,
        IReadOnlyDictionary<string, TemplateVolumeFloor>? customFloors = null) =>
        GetReviewedFloor(templateId, customFloors) is not null;

    public static TemplateVolumeFloorEvaluationResult EvaluateSelections(
        string templateId,
        IReadOnlyCollection<StrengthSlotSelection>? selections,
        IReadOnlyDictionary<string, TemplateVolumeFloor>? customFloors = null)
    {
        var floor = GetReviewedFloor(templateId, customFloors);
        if (floor is null)
            return Evaluate(templateId, ZeroThreshold, customFloors: customFloors);

        if (selections is null || selections.Any(slot => slot is null ||
                string.IsNullOrWhiteSpace(slot.SlotId) || slot.Sets.GetValueOrDefault() < ZeroThreshold) ||
            selections.Select(slot => slot.SlotId).Distinct(StringComparer.Ordinal).Count() != selections.Count)
            return new TemplateVolumeFloorEvaluationResult(false, VolumeBelowTemplateFloor,
                "Invalid or duplicate slot metadata.", floor.MinimumWorkingSets, null);

        var active = selections.Where(slot => slot.ExerciseId.HasValue && slot.Sets.GetValueOrDefault() > ZeroThreshold).ToArray();
        var setsBySlot = active.ToDictionary(slot => slot.SlotId, slot => slot.Sets.GetValueOrDefault(), StringComparer.Ordinal);
        var totalSetsLong = active.Sum(slot => (long)slot.Sets.GetValueOrDefault());
        if (totalSetsLong > int.MaxValue)
            return new TemplateVolumeFloorEvaluationResult(false, VolumeBelowTemplateFloor,
                "Working set total exceeds supported range.", floor.MinimumWorkingSets, null);
        var totalSets = (int)totalSetsLong;
        var result = Evaluate(templateId, totalSets, setsBySlot, customFloors);
        return result.IsAllowed && active.Length < floor.MinimumExercises
            ? new TemplateVolumeFloorEvaluationResult(false, VolumeBelowTemplateFloor,
                "Exercise count is below the template floor.", floor.MinimumWorkingSets, totalSets)
            : result;
    }
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
                ReasonDetail: $"Template '{templateId}' does not have a volume floor.",
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
