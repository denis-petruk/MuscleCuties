using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

public sealed record StrengthPlanSessionDisplay(string Title, string Duration,
    ObservableCollection<StrengthPlanSlotDisplay> Slots)
{
    public static StrengthPlanSessionDisplay From(StrengthPlanPreviewSessionV020 session, bool hasOmittedMovements = false)
    {
        var title = LocalizedFormat.Format("Workout_Generation_SessionDayTitleFormat",
            session.PlannedWeekday is { } weekday
                ? UiTextCatalog.Get(StrengthPlanGenerationDisplayMapper.WeekdayLabelKey(weekday))
                : LocalizedFormat.Format("Workout_Generation_SessionTitleFormat",
                    session.SessionIndex.ToString(CultureInfo.CurrentCulture)));
        var duration = session.EstimatedMinutes is { } minutes
            ? UiTextCatalog.Get("Workout_Generation_EstimatedMinutesFormat")
                .Replace("{0:0}", minutes.ToString("0", CultureInfo.CurrentCulture), StringComparison.Ordinal)
            : UiTextCatalog.Get("Workout_Generation_EstimateUnavailable");
        var slots = session.Slots.Select(slot =>
        {
            var omitted = hasOmittedMovements && slot.IsRequired && slot.ExerciseId is null;
            var name = omitted
                ? UiTextCatalog.Get(OmittedMovementKey((StrengthMovementSlotDto)slot.MovementSlot))
                : string.IsNullOrWhiteSpace(slot.ExerciseName)
                ? UiTextCatalog.Get("Workout_Generation_ExercisePending")
                : UiTextCatalog.TranslateRuntimeText(slot.ExerciseName);
            var details = omitted
                ? UiTextCatalog.Get("Workout_Generation_MovementOmittedReason")
                : slot.Sets is { } sets && slot.MinimumRepetitions is { } minimum &&
                          slot.MaximumRepetitions is { } maximum
                ? LocalizedFormat.Format("Workout_Generation_SlotDetailsFormat",
                    sets.ToString(CultureInfo.CurrentCulture), minimum.ToString(CultureInfo.CurrentCulture),
                    maximum.ToString(CultureInfo.CurrentCulture))
                : UiTextCatalog.Get("Workout_Generation_PrescriptionPending");
            var rationale = ResolveSlotRationale(slot.SelectionReason);
            return new StrengthPlanSlotDisplay(name, details, slot.IsRequired) { Rationale = rationale };
        }).ToList();

        return new StrengthPlanSessionDisplay(title, duration,
            new ObservableCollection<StrengthPlanSlotDisplay>(slots));
    }

    private static string ResolveSlotRationale(string? selectionReason)
    {
        if (string.IsNullOrWhiteSpace(selectionReason))
            return string.Empty;

        if (UiTextCatalog.HasKey(selectionReason))
            return UiTextCatalog.Get(selectionReason);

        if (!selectionReason.Contains(' ', StringComparison.Ordinal))
        {
            var selectionReasonKey = $"Workout_Generation_SelectionReason_{selectionReason}";
            if (UiTextCatalog.HasKey(selectionReasonKey))
                return UiTextCatalog.Get(selectionReasonKey);
        }

        return selectionReason.Contains(' ', StringComparison.Ordinal)
            ? selectionReason
            : string.Empty;
    }

    private static string OmittedMovementKey(StrengthMovementSlotDto movement) => movement switch
    {
        StrengthMovementSlotDto.KneeDominant => "Workout_Generation_MovementKneeOmitted",
        StrengthMovementSlotDto.HipDominant => "Workout_Generation_MovementHipOmitted",
        StrengthMovementSlotDto.UpperPush => "Workout_Generation_MovementUpperPushOmitted",
        StrengthMovementSlotDto.UpperPull => "Workout_Generation_MovementUpperPullOmitted",
        StrengthMovementSlotDto.Trunk => "Workout_Generation_MovementTrunkOmitted",
        StrengthMovementSlotDto.GoalSpecificAccessory => "Workout_Generation_MovementAccessoryOmitted",
        _ => "Workout_Generation_MovementUnavailable"
    };
}
