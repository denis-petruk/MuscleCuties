using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record StrengthStyleEvaluation(
    WorkoutActivityType Activity,
    bool IsFeasible,
    bool IsUnreduced,
    int OriginalPlannedWorkingSets,
    int ActualWorkingSets,
    decimal RetentionRatio,
    StrengthPlanPreviewResult PreviewResult);

public sealed record StrengthStyleSelectionResult(
    WorkoutActivityType? ChosenStyle,
    IReadOnlyList<WorkoutActivityType> FittingUnreducedStyles,
    IReadOnlyList<WorkoutActivityType> FittingReducedStyles,
    IReadOnlyList<StrengthStyleEvaluation> Evaluations,
    bool RequiresUserChoice,
    bool RequiresReducedVolumeAcknowledgment,
    string? LimitationCode = null);

/// <summary>
/// Evaluates selected strength styles according to Product Decision 3:
/// - Evaluates all selected strength styles.
/// - Prefers an unreduced safe fit; identifies multiple fitting styles if several fit so UI can prompt user choice.
/// - If none fits unreduced, evaluates safe reduced-volume options within selected styles,
///   preferring the option retaining highest proportion of original planned working sets,
///   breaking ties by lower demand: General Strength -> High Volume Strength -> High Intensity Strength.
/// - Prohibits switching to an unselected style as an implicit fallback.
/// </summary>
public static class StrengthStyleSelectionRule
{
    public const string NoStrengthStyleSelectedCode = "NoStrengthStyleSelected";
    public const string NoSafeStrengthStyleFitCode = "NoSafeStrengthStyleFit";
    public const string MultipleFittingStylesIdentifiedCode = "MultipleFittingStylesIdentified";

    public const int GeneralStrengthDemandRank = 1;
    public const int HighVolumeDemandRank = 2;
    public const int HighIntensityDemandRank = 3;

    public const decimal FullRetentionRatio = 1.0m;
    public const decimal ZeroRetentionRatio = 0.0m;
    public const int ZeroWorkingSets = 0;
    public const int SingleFittingStyleCount = 1;

    public static bool IsStrengthStyle(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or
        WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity;

    public static int GetDemandRank(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.GeneralStrength => GeneralStrengthDemandRank,
        WorkoutActivityType.HighVolumeStrength => HighVolumeDemandRank,
        WorkoutActivityType.StrengthHighIntensity => HighIntensityDemandRank,
        _ => int.MaxValue
    };

    public static StrengthStyleSelectionResult Select(
        StrengthPlanPreviewChoices? choices,
        StrengthPlanPreviewData? data)
    {
        if (choices is null || data is null)
        {
            return new StrengthStyleSelectionResult(
                ChosenStyle: null,
                FittingUnreducedStyles: Array.Empty<WorkoutActivityType>(),
                FittingReducedStyles: Array.Empty<WorkoutActivityType>(),
                Evaluations: Array.Empty<StrengthStyleEvaluation>(),
                RequiresUserChoice: false,
                RequiresReducedVolumeAcknowledgment: false,
                LimitationCode: StrengthPlanPreviewRule.InvalidInputCode);
        }

        // Selected strength styles: strictly from data.SelectedActivities (or choices.Activity if selected).
        // Never evaluate or fall back to an unselected style.
        var selectedStyles = (data.SelectedActivities ?? new HashSet<WorkoutActivityType>())
            .Where(IsStrengthStyle)
            .Distinct()
            .ToArray();

        if (selectedStyles.Length == 0)
        {
            return new StrengthStyleSelectionResult(
                ChosenStyle: null,
                FittingUnreducedStyles: Array.Empty<WorkoutActivityType>(),
                FittingReducedStyles: Array.Empty<WorkoutActivityType>(),
                Evaluations: Array.Empty<StrengthStyleEvaluation>(),
                RequiresUserChoice: false,
                RequiresReducedVolumeAcknowledgment: false,
                LimitationCode: NoStrengthStyleSelectedCode);
        }

        var evaluations = new List<StrengthStyleEvaluation>(selectedStyles.Length);

        foreach (var style in selectedStyles)
        {
            var styleChoices = choices with { Activity = style };
            var previewResult = StrengthPlanPreviewRule.EvaluateDirect(styleChoices, data);

            var isFeasible = previewResult.Status == StrengthPlanPreviewStatus.Feasible &&
                             previewResult.LimitationCodes.Count == 0;

            var hasOriginalSets = previewResult.OriginalPlannedWorkingSets.HasValue ||
                                  (previewResult.Sessions.Count > 0 &&
                                   previewResult.Sessions.All(session => session.OriginalPlannedWorkingSets.HasValue));
            var originalSets = previewResult.OriginalPlannedWorkingSets ??
                               (hasOriginalSets ? previewResult.Sessions.Sum(session => session.OriginalPlannedWorkingSets!.Value)
                                   : ZeroWorkingSets);

            var actualSets = previewResult.ActualWorkingSets ??
                             previewResult.Sessions.Sum(session => session.ActualWorkingSets ??
                                 session.Slots.Where(s => s.ExerciseId.HasValue).Sum(s => s.Sets.GetValueOrDefault()));

            var retentionRatio = originalSets > ZeroWorkingSets
                ? Math.Clamp((decimal)actualSets / originalSets, ZeroRetentionRatio, FullRetentionRatio)
                : ZeroRetentionRatio;

            var isUnreduced = isFeasible && hasOriginalSets && originalSets > ZeroWorkingSets &&
                              !previewResult.RequiresReducedVolumeAcknowledgment &&
                              actualSets >= originalSets;

            evaluations.Add(new StrengthStyleEvaluation(
                Activity: style,
                IsFeasible: isFeasible,
                IsUnreduced: isUnreduced,
                OriginalPlannedWorkingSets: originalSets,
                ActualWorkingSets: actualSets,
                RetentionRatio: retentionRatio,
                PreviewResult: previewResult));
        }

        var unreducedFits = evaluations.Where(e => e.IsUnreduced).ToArray();
        if (unreducedFits.Length > 0)
        {
            var fittingUnreducedStyles = unreducedFits.Select(e => e.Activity).ToArray();
            var requiresUserChoice = fittingUnreducedStyles.Length > SingleFittingStyleCount;

            // If the user already had a requested style that fits unreduced, preserve it;
            // otherwise break ties by lowest demand.
            var chosen = unreducedFits.Any(e => e.Activity == choices.Activity)
                ? choices.Activity
                : unreducedFits.OrderBy(e => GetDemandRank(e.Activity)).First().Activity;

            return new StrengthStyleSelectionResult(
                ChosenStyle: chosen,
                FittingUnreducedStyles: fittingUnreducedStyles,
                FittingReducedStyles: Array.Empty<WorkoutActivityType>(),
                Evaluations: evaluations,
                RequiresUserChoice: requiresUserChoice,
                RequiresReducedVolumeAcknowledgment: false,
                LimitationCode: null);
        }

        // None fits unreduced: evaluate safe reduced-volume options within selected styles.
        var reducedFits = evaluations
            .Where(e => e.IsFeasible && e.OriginalPlannedWorkingSets > ZeroWorkingSets &&
                        e.PreviewResult.RequiresReducedVolumeAcknowledgment)
            .ToArray();

        if (reducedFits.Length > 0)
        {
            var bestReduced = reducedFits
                .OrderByDescending(e => e.RetentionRatio)
                .ThenBy(e => GetDemandRank(e.Activity))
                .First();

            var fittingReducedStyles = reducedFits.Select(e => e.Activity).ToArray();

            return new StrengthStyleSelectionResult(
                ChosenStyle: bestReduced.Activity,
                FittingUnreducedStyles: Array.Empty<WorkoutActivityType>(),
                FittingReducedStyles: fittingReducedStyles,
                Evaluations: evaluations,
                RequiresUserChoice: false,
                RequiresReducedVolumeAcknowledgment: true,
                LimitationCode: null);
        }

        // None fit even with reduced volume
        return new StrengthStyleSelectionResult(
            ChosenStyle: null,
            FittingUnreducedStyles: Array.Empty<WorkoutActivityType>(),
            FittingReducedStyles: Array.Empty<WorkoutActivityType>(),
            Evaluations: evaluations,
            RequiresUserChoice: false,
            RequiresReducedVolumeAcknowledgment: false,
            LimitationCode: NoSafeStrengthStyleFitCode);
    }
}
