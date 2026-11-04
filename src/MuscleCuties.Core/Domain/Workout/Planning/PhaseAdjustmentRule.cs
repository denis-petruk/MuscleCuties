using System.Collections.ObjectModel;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum PhaseAdjustmentTier
{
    Unavailable,
    Maintain,
    Lighter,
    EasierDay
}

public enum PhaseAdjustmentPreviewStatus
{
    Unavailable,
    NoMeaningfulChange,
    ChangesOrChoicesAvailable
}

public enum PhaseAdjustmentField
{
    ExerciseSets,
    ContinuousMainMinutes
}

public enum PhaseAdjustmentChoice
{
    KeepCurrentSession,
    SkipFutureSession,
    SelectEligibleEasyCardioTemplate,
    SelectEligibleGentleTemplate
}

public sealed record PhaseAdjustmentExercise(
    Guid PrescriptionId,
    int ExerciseId,
    int AcceptedSets,
    bool IsOptional,
    bool IsAccessory);

/// <summary>
/// Projection of an accepted prescription. Optional/accessory flags must be stored
/// explicitly; continuous minutes must come from a supported time-based main block.
/// </summary>
public sealed record PhaseAdjustmentSession(
    Guid SessionId,
    ActivityModality Modality,
    SessionTrainingStyle TrainingStyle,
    IReadOnlyList<PhaseAdjustmentExercise> Exercises,
    decimal? ContinuousMainMinutes,
    IReadOnlyList<PhaseAdjustmentTemplateOption>? ExistingTemplateOptions = null,
    bool IsSkipped = false,
    string? SelectedReplacementTemplateId = null);

public sealed record PhaseAdjustmentTemplateOption(
    PhaseAdjustmentChoice Choice,
    string TemplateId,
    string? DisplayName = null);

public sealed record PhaseAdjustmentFieldChange(
    Guid SessionId,
    Guid? PrescriptionId,
    int? ExerciseId,
    PhaseAdjustmentField Field,
    decimal AcceptedValue,
    decimal ProposedValue);

public sealed record PhaseAdjustmentSessionChoices(
    Guid SessionId,
    IReadOnlyList<PhaseAdjustmentChoice> Choices);

public sealed record PhaseAdjustmentPreview(
    string PolicyVersion,
    PhaseAdjustmentTier Tier,
    PhaseAdjustmentPreviewStatus Status,
    IReadOnlyList<PhaseAdjustmentFieldChange> FieldChanges,
    IReadOnlyList<PhaseAdjustmentSessionChoices> SessionChoices,
    bool RequiresStrengthDurationRecalculation);

public sealed record PhaseAdjustmentChoiceSelection(
    Guid SessionId,
    PhaseAdjustmentChoice Choice,
    string? ExistingTemplateId = null);

public enum PhaseAdjustmentApplicationStatus
{
    InvalidInput,
    NotApplied,
    Applied
}

public sealed record PhaseAdjustmentApplication(
    PhaseAdjustmentApplicationStatus Status,
    IReadOnlyList<PhaseAdjustmentSession> Sessions,
    bool RequiresStrengthDurationRecalculation,
    string? LimitationCode);

/// <summary>
/// Produces deltas from accepted prescriptions only. Choices are invitations to
/// validate an existing template; this rule does not manufacture alternatives.
/// </summary>
public static class PhaseAdjustmentRule
{
    public const string PolicyId = "MC-PHASE-ADJUSTMENTS";
    public const string Version = "0.1.0";
    public const int EasierDayDiscomfortThreshold = 4;
    public const int EasierDayEnergyRating = 1;
    public const int LighterDiscomfortRating = 3;
    public const int LighterEnergyRating = 2;
    public const int MaintainMaximumDiscomfort = 2;
    public const int MaintainMinimumEnergy = 3;
    public const int OptionalAccessorySetReduction = 1;
    public const decimal ContinuousCardioMinuteReduction = 5m;
    public const decimal ContinuousCardioMinimumMainMinutes = 5m;

    private const int FirstExerciseId = 1;
    private const int MinimumExerciseSetCount = 1;
    private const int NoCollectionItems = 0;
    private const int MissingItemIndex = -1;
    private const decimal NoContinuousMainMinutes = 0m;
    private const decimal MinimumContinuousMainMinutes = NoContinuousMainMinutes;
    public static PhaseAdjustmentTier SelectTier(PhaseBaseline? baseline,
        PhaseBaselineScale scale)
    {
        if (scale != PhaseBaselineScale.QuizMappedOneToFive || baseline is null ||
            baseline.Pain is < PhaseReviewRule.MinimumDiscomfortRating or
                > PhaseReviewRule.MaximumDiscomfortRating ||
            baseline.Energy is < PhaseReviewRule.MinimumEnergyRating or
                > PhaseReviewRule.MaximumEnergyRating)
            return PhaseAdjustmentTier.Unavailable;

        if (baseline.Pain >= PhaseAdjustmentRule.EasierDayDiscomfortThreshold ||
            baseline.Energy == PhaseAdjustmentRule.EasierDayEnergyRating)
            return PhaseAdjustmentTier.EasierDay;
        if (baseline.Pain == PhaseAdjustmentRule.LighterDiscomfortRating ||
            baseline.Energy == PhaseAdjustmentRule.LighterEnergyRating)
            return PhaseAdjustmentTier.Lighter;
        return baseline.Pain <= PhaseAdjustmentRule.MaintainMaximumDiscomfort &&
               baseline.Energy >= PhaseAdjustmentRule.MaintainMinimumEnergy
            ? PhaseAdjustmentTier.Maintain
            : PhaseAdjustmentTier.Unavailable;
    }

    public static PhaseAdjustmentPreview Preview(PhaseReviewGateResult? gate,
        PhaseBaseline? baseline, PhaseBaselineScale scale,
        IReadOnlyList<PhaseAdjustmentSession>? acceptedSessions)
    {
        var tier = SelectTier(baseline, scale);
        if (gate?.Status != PhaseReviewGateStatus.ReadyForAdjustmentPreview ||
            tier == PhaseAdjustmentTier.Unavailable || acceptedSessions is null ||
            acceptedSessions.Any(session => session is null || session.SessionId == Guid.Empty ||
                !Enum.IsDefined(session.Modality) || !Enum.IsDefined(session.TrainingStyle) ||
                session.Exercises is null ||
                session.Exercises.Any(exercise => exercise is null ||
                    exercise.PrescriptionId == Guid.Empty || exercise.ExerciseId < FirstExerciseId ||
                    exercise.AcceptedSets < MinimumExerciseSetCount) ||
                session.ExistingTemplateOptions?.Any(option => option is null ||
                    !Enum.IsDefined(option.Choice) ||
                    option.Choice is not (PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate or
                        PhaseAdjustmentChoice.SelectEligibleGentleTemplate) ||
                    string.IsNullOrWhiteSpace(option.TemplateId)) == true ||
                session.ExistingTemplateOptions?.Select(option => (option.Choice, option.TemplateId))
                    .Distinct().Count() != session.ExistingTemplateOptions?.Count ||
                session.Exercises.Select(exercise => exercise.PrescriptionId).Distinct().Count() !=
                session.Exercises.Count ||
                session.ContinuousMainMinutes is < NoContinuousMainMinutes) ||
            acceptedSessions.Select(session => session.SessionId).Distinct().Count() !=
            acceptedSessions.Count ||
            gate.EligibleSessionIds.Any(id => acceptedSessions.All(session => session.SessionId != id)))
            return new PhaseAdjustmentPreview(PhaseAdjustmentRule.Version,
                tier, PhaseAdjustmentPreviewStatus.Unavailable, [], [], false);

        var changes = new List<PhaseAdjustmentFieldChange>();
        var choices = new List<PhaseAdjustmentSessionChoices>();
        if (tier == PhaseAdjustmentTier.Maintain)
            return Result(tier, changes, choices);

        var selectedIds = gate.EligibleSessionIds.ToHashSet();
        foreach (var session in acceptedSessions.Where(session => selectedIds.Contains(session.SessionId)))
        {
            if (tier == PhaseAdjustmentTier.EasierDay)
            {
                if (session.TrainingStyle != SessionTrainingStyle.Gentle)
                    choices.Add(new PhaseAdjustmentSessionChoices(session.SessionId,
                        ChoicesWithExistingAlternative(session,
                            PhaseAdjustmentChoice.SelectEligibleGentleTemplate)));
                continue;
            }

            if (session.TrainingStyle == SessionTrainingStyle.Hiit)
            {
                choices.Add(new PhaseAdjustmentSessionChoices(session.SessionId,
                    ChoicesWithExistingAlternative(session,
                        PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate)));
                continue;
            }

            if (session.Modality == ActivityModality.Strength)
            {
                foreach (var exercise in session.Exercises.Where(exercise =>
                             exercise.IsOptional && exercise.IsAccessory &&
                             exercise.AcceptedSets > PhaseAdjustmentRule.OptionalAccessorySetReduction))
                    changes.Add(new PhaseAdjustmentFieldChange(session.SessionId,
                        exercise.PrescriptionId, exercise.ExerciseId,
                        PhaseAdjustmentField.ExerciseSets,
                        exercise.AcceptedSets,
                        exercise.AcceptedSets - PhaseAdjustmentRule.OptionalAccessorySetReduction));
                continue;
            }

            if (session.TrainingStyle != SessionTrainingStyle.Continuous ||
                !SupportsContinuousCardio(session.Modality) ||
                session.ContinuousMainMinutes is not { } acceptedMainMinutes)
                continue;

            var proposedMainMinutes = Math.Min(acceptedMainMinutes,
                Math.Max(PhaseAdjustmentRule.ContinuousCardioMinimumMainMinutes,
                    acceptedMainMinutes - PhaseAdjustmentRule.ContinuousCardioMinuteReduction));
            if (proposedMainMinutes < acceptedMainMinutes)
                changes.Add(new PhaseAdjustmentFieldChange(session.SessionId, null, null,
                    PhaseAdjustmentField.ContinuousMainMinutes,
                    acceptedMainMinutes, proposedMainMinutes));
        }

        return Result(tier, changes, choices);
    }

    /// <summary>Applies an exact preview only after an explicit user confirmation.</summary>
    public static PhaseAdjustmentApplication Apply(PhaseReviewGateResult? gate,
        PhaseAdjustmentPreview? preview,
        IReadOnlyList<PhaseAdjustmentSession>? acceptedSessions,
        IReadOnlyList<PhaseAdjustmentChoiceSelection>? selections,
        bool applyChanges)
    {
        if (gate is null || gate.Status != PhaseReviewGateStatus.ReadyForAdjustmentPreview ||
            preview is null ||
            preview.PolicyVersion != PhaseAdjustmentRule.Version ||
            !Enum.IsDefined(preview.Tier) || !Enum.IsDefined(preview.Status) ||
            gate.EligibleSessionIds is null || preview.FieldChanges is null ||
            preview.SessionChoices is null || preview.SessionChoices.Any(item => item is null ||
                item.SessionId == Guid.Empty || item.Choices is null ||
                item.Choices.Any(choice => !Enum.IsDefined(choice))) ||
            preview.SessionChoices.Select(item => item.SessionId).Distinct().Count() !=
            preview.SessionChoices.Count ||
            preview.Status == PhaseAdjustmentPreviewStatus.Unavailable ||
            acceptedSessions is null || selections is null ||
            acceptedSessions.Any(session => session is null || session.SessionId == Guid.Empty ||
                session.Exercises is null || session.Exercises.Any(exercise => exercise is null ||
                    exercise.PrescriptionId == Guid.Empty || exercise.ExerciseId < FirstExerciseId ||
                    exercise.AcceptedSets < MinimumExerciseSetCount) || session.IsSkipped ||
                session.ExistingTemplateOptions?.Any(option => option is null ||
                    !Enum.IsDefined(option.Choice) || string.IsNullOrWhiteSpace(option.TemplateId)) == true ||
                !string.IsNullOrWhiteSpace(session.SelectedReplacementTemplateId)) ||
            acceptedSessions.Select(session => session.SessionId).Distinct().Count() !=
            acceptedSessions.Count ||
            selections.Any(selection => selection is null || selection.SessionId == Guid.Empty ||
                !Enum.IsDefined(selection.Choice)) ||
            selections.Select(selection => selection.SessionId).Distinct().Count() != selections.Count)
            return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                acceptedSessions ?? [], false, "PhaseAdjustmentApplicationInputInvalid");

        if (preview.Status == PhaseAdjustmentPreviewStatus.NoMeaningfulChange)
            return Application(PhaseAdjustmentApplicationStatus.NotApplied,
                acceptedSessions, false, "NoMeaningfulPhaseAdjustmentAvailable");
        if (!applyChanges)
            return Application(PhaseAdjustmentApplicationStatus.NotApplied,
                acceptedSessions, false, "UserDidNotApplyChanges");

        var sessionById = acceptedSessions.ToDictionary(session => session.SessionId);
        var changedSessions = new Dictionary<Guid, PhaseAdjustmentSession>();
        var changeKeys = new HashSet<(Guid SessionId, Guid? PrescriptionId, PhaseAdjustmentField Field)>();
        foreach (var change in preview.FieldChanges)
        {
            if (change is null || !Enum.IsDefined(change.Field) ||
                !sessionById.TryGetValue(change.SessionId, out var session) ||
                !gate.EligibleSessionIds.Contains(change.SessionId) ||
                !changeKeys.Add((change.SessionId, change.PrescriptionId, change.Field)))
                return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                    acceptedSessions, false, "PhaseAdjustmentFieldChangeInvalid");

            switch (change.Field)
            {
                case PhaseAdjustmentField.ExerciseSets:
                {
                    if (change.PrescriptionId is not { } prescriptionId || change.ExerciseId is null ||
                        change.ProposedValue != decimal.Truncate(change.ProposedValue) ||
                        change.ProposedValue < MinimumExerciseSetCount ||
                        change.ProposedValue >= change.AcceptedValue)
                        return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                            acceptedSessions, false, "PhaseAdjustmentSetChangeInvalid");
                    var current = changedSessions.GetValueOrDefault(session.SessionId, session);
                    var exerciseIndex = current.Exercises.ToList().FindIndex(exercise =>
                        exercise.PrescriptionId == prescriptionId &&
                        exercise.ExerciseId == change.ExerciseId &&
                        exercise.AcceptedSets == change.AcceptedValue);
                    if (exerciseIndex == MissingItemIndex)
                        return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                            acceptedSessions, false, "PhaseAdjustmentAcceptedPrescriptionChanged");
                    var updatedExercises = current.Exercises.ToArray();
                    updatedExercises[exerciseIndex] = updatedExercises[exerciseIndex] with
                    {
                        AcceptedSets = decimal.ToInt32(change.ProposedValue)
                    };
                    changedSessions[session.SessionId] = current with
                    {
                        Exercises = Array.AsReadOnly(updatedExercises)
                    };
                    break;
                }
                case PhaseAdjustmentField.ContinuousMainMinutes:
                {
                    var current = changedSessions.GetValueOrDefault(session.SessionId, session);
                    if (current.ContinuousMainMinutes != change.AcceptedValue ||
                        change.ProposedValue < MinimumContinuousMainMinutes ||
                        change.ProposedValue >= change.AcceptedValue)
                        return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                            acceptedSessions, false, "PhaseAdjustmentDurationChangeInvalid");
                    changedSessions[session.SessionId] = current with
                    {
                        ContinuousMainMinutes = change.ProposedValue
                    };
                    break;
                }
                default:
                    return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                        acceptedSessions, false, "PhaseAdjustmentFieldUnsupported");
            }
        }

        var previewChoices = preview.SessionChoices.ToDictionary(item => item.SessionId);
        if (selections.Any(selection => !previewChoices.ContainsKey(selection.SessionId)) ||
            previewChoices.Count != selections.Count)
            return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                acceptedSessions, false, "PhaseAdjustmentChoiceSelectionMissing");

        foreach (var selection in selections)
        {
            var offered = previewChoices[selection.SessionId];
            if (!offered.Choices.Contains(selection.Choice) ||
                !sessionById.TryGetValue(selection.SessionId, out var session) ||
                !gate.EligibleSessionIds.Contains(selection.SessionId) ||
                selection.Choice is not (PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate or
                    PhaseAdjustmentChoice.SelectEligibleGentleTemplate) &&
                !string.IsNullOrWhiteSpace(selection.ExistingTemplateId))
                return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                    acceptedSessions, false, "PhaseAdjustmentChoiceNotOffered");

            var updated = changedSessions.GetValueOrDefault(session.SessionId, session);
            if (selection.Choice == PhaseAdjustmentChoice.SkipFutureSession)
            {
                updated = updated with { IsSkipped = true };
            }
            else if (selection.Choice is PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate or
                         PhaseAdjustmentChoice.SelectEligibleGentleTemplate)
            {
                var templateId = selection.ExistingTemplateId;
                var isEligibleExistingTemplate = !string.IsNullOrWhiteSpace(templateId) &&
                    session.ExistingTemplateOptions?.Any(option => option.Choice == selection.Choice &&
                        string.Equals(option.TemplateId, templateId, StringComparison.Ordinal)) == true;
                if (!isEligibleExistingTemplate)
                    return Application(PhaseAdjustmentApplicationStatus.InvalidInput,
                        acceptedSessions, false, "PhaseAdjustmentAlternativeMustUseEligibleExistingTemplate");
                updated = updated with { SelectedReplacementTemplateId = templateId };
            }

            changedSessions[session.SessionId] = updated;
        }

        var result = acceptedSessions.Select(session =>
                changedSessions.GetValueOrDefault(session.SessionId, session))
            .ToArray();
        return Application(PhaseAdjustmentApplicationStatus.Applied,
            Array.AsReadOnly(result), preview.RequiresStrengthDurationRecalculation, null);
    }

    private static PhaseAdjustmentPreview Result(PhaseAdjustmentTier tier,
        List<PhaseAdjustmentFieldChange> changes,
        List<PhaseAdjustmentSessionChoices> choices) =>
        new(PhaseAdjustmentRule.Version, tier,
            changes.Count == NoCollectionItems && choices.Count == NoCollectionItems
                ? PhaseAdjustmentPreviewStatus.NoMeaningfulChange
                : PhaseAdjustmentPreviewStatus.ChangesOrChoicesAvailable,
            Array.AsReadOnly(changes.ToArray()), Array.AsReadOnly(choices.ToArray()),
            changes.Any(change => change.Field == PhaseAdjustmentField.ExerciseSets));

    private static PhaseAdjustmentApplication Application(
        PhaseAdjustmentApplicationStatus status,
        IReadOnlyList<PhaseAdjustmentSession> sessions,
        bool requiresStrengthDurationRecalculation,
        string? limitationCode) =>
        new(status, Array.AsReadOnly(sessions.ToArray()),
            requiresStrengthDurationRecalculation, limitationCode);

    private static bool SupportsContinuousCardio(ActivityModality modality) =>
        modality is ActivityModality.Walking or ActivityModality.Running or
            ActivityModality.Cycling or ActivityModality.Rowing or
            ActivityModality.Elliptical or ActivityModality.Swimming;

    private static ReadOnlyCollection<PhaseAdjustmentChoice> ChoicesWithExistingAlternative(
        PhaseAdjustmentSession session,
        PhaseAdjustmentChoice alternativeChoice)
    {
        var choices = new List<PhaseAdjustmentChoice>
        {
            PhaseAdjustmentChoice.KeepCurrentSession,
            PhaseAdjustmentChoice.SkipFutureSession
        };
        if (session.ExistingTemplateOptions?.Any(option => option.Choice == alternativeChoice &&
                !string.IsNullOrWhiteSpace(option.TemplateId)) == true)
            choices.Add(alternativeChoice);
        return Array.AsReadOnly(choices.ToArray());
    }
}
