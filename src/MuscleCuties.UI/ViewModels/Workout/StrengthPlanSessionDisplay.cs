using System.Collections.ObjectModel;
using System.Globalization;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

public sealed record StrengthPlanSessionDisplay(string Title, string Duration,
    ObservableCollection<StrengthPlanSlotDisplay> Slots)
{
    public static StrengthPlanSessionDisplay From(StrengthPlanPreviewSessionV020 session, bool hasOmittedMovements = false)
    {
        var title = FormatLocalized("Workout_Generation_SessionDayTitleFormat",
            session.PlannedWeekday is { } weekday
                ? UiTextCatalog.Get(WeekdayLabelKey(weekday))
                : FormatLocalized("Workout_Generation_SessionTitleFormat",
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
                : slot.ExerciseName;
            var details = omitted
                ? UiTextCatalog.Get("Workout_Generation_MovementOmittedReason")
                : slot.Sets is { } sets && slot.MinimumRepetitions is { } minimum &&
                          slot.MaximumRepetitions is { } maximum
                ? FormatLocalized("Workout_Generation_SlotDetailsFormat",
                    sets.ToString(CultureInfo.CurrentCulture), minimum.ToString(CultureInfo.CurrentCulture),
                    maximum.ToString(CultureInfo.CurrentCulture))
                : UiTextCatalog.Get("Workout_Generation_PrescriptionPending");
            return new StrengthPlanSlotDisplay(name, details, slot.IsRequired);
        }).ToList();

        return new StrengthPlanSessionDisplay(title, duration,
            new ObservableCollection<StrengthPlanSlotDisplay>(slots));
    }

    private static string FormatLocalized(string key, params string[] arguments)
    {
        var formatted = UiTextCatalog.Get(key);
        for (var argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
            formatted = formatted.Replace($"{{{argumentIndex}}}", arguments[argumentIndex], StringComparison.Ordinal);
        return formatted;
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

    private static string WeekdayLabelKey(DayOfWeek weekday) => weekday switch
    {
        DayOfWeek.Monday => "Workout_Generation_DayMonday",
        DayOfWeek.Tuesday => "Workout_Generation_DayTuesday",
        DayOfWeek.Wednesday => "Workout_Generation_DayWednesday",
        DayOfWeek.Thursday => "Workout_Generation_DayThursday",
        DayOfWeek.Friday => "Workout_Generation_DayFriday",
        DayOfWeek.Saturday => "Workout_Generation_DaySaturday",
        DayOfWeek.Sunday => "Workout_Generation_DaySunday",
        _ => "Workout_Generation_UnknownDay"
    };
}
