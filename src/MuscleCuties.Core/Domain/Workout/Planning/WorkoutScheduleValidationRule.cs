using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record WorkoutScheduleValidationSession(
    DayOfWeek Day,
    WorkoutActivityType Activity,
    bool IsHard,
    IReadOnlySet<string>? StrengthMuscleGroups = null);

/// <summary>Checks a proposed repeating week, including its Sunday-to-Monday boundary.</summary>
public static class WorkoutScheduleValidationRule
{
    public const string InvalidDay = "ScheduleInvalidDay";
    public const string TooManySessions = "ScheduleTooManySessionsOnDay";
    public const string DuplicateActivity = "ScheduleDuplicateActivityOnDay";
    public const string MultipleStrengthSessions = "ScheduleMultipleStrengthSessionsOnDay";
    public const string MultipleHardSessions = "ScheduleMultipleHardSessionsOnDay";
    public const string TooManyHardDays = "ScheduleTooManyHardDays";
    public const string StrengthMuscleMetadataMissing = "ScheduleStrengthMuscleMetadataMissing";
    public const string HiitConflict = "ScheduleHiitStrengthOrClimbingConflict";
    public const string ConsecutiveStrengthMuscleGroups = "ScheduleConsecutiveStrengthMuscleGroups";

    public const int MaximumActivitiesPerDay = MixedActivityWeekScheduleRule.MaximumSessionsPerDay;
    public const int MaximumStrengthSessionsPerDay = MixedActivityWeekScheduleRule.MaximumStrengthSessionsPerDay;
    public const int MaximumHardSessionsPerDay = 1;
    public const int MaximumHardDaysPerWeek = MixedActivityWeekScheduleRule.MaximumHardDaysPerWeek;
    public const int DaysInWeek = 7;
    public const int NextConsecutiveDayOffset = 1;
    public const int MinimumMuscleCount = 1;

    public static IReadOnlyList<string> Validate(
        IReadOnlyCollection<WorkoutScheduleValidationSession> sessions,
        bool requireStrengthMuscles = true)
    {
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        if (sessions.Any(session => !Enum.IsDefined(session.Day)))
            reasons.Add(InvalidDay);

        foreach (var day in sessions.GroupBy(session => session.Day))
        {
            if (day.Count() > MaximumActivitiesPerDay)
                reasons.Add(TooManySessions);

            if (day.Select(session => session.Activity).Distinct().Count() != day.Count())
                reasons.Add(DuplicateActivity);

            if (day.Count(session => IsStrength(session.Activity)) > MaximumStrengthSessionsPerDay)
                reasons.Add(MultipleStrengthSessions);

            if (day.Count(session => session.IsHard) > MaximumHardSessionsPerDay)
                reasons.Add(MultipleHardSessions);

            var hasHiit = day.Any(session => session.Activity == WorkoutActivityType.Hiit);
            var hasStrengthOrClimbing = day.Any(session =>
                IsStrength(session.Activity) || session.Activity == WorkoutActivityType.RockClimbing);

            if (hasHiit && hasStrengthOrClimbing)
                reasons.Add(HiitConflict);
        }

        if (sessions.Where(session => session.IsHard).Select(session => session.Day).Distinct().Count() >
            MaximumHardDaysPerWeek)
        {
            reasons.Add(TooManyHardDays);
        }

        if (requireStrengthMuscles && sessions.Any(session => IsStrength(session.Activity) &&
            (session.StrengthMuscleGroups is null || session.StrengthMuscleGroups.Count < MinimumMuscleCount)))
        {
            reasons.Add(StrengthMuscleMetadataMissing);
        }

        var strength = sessions
            .Where(session => IsStrength(session.Activity) && session.StrengthMuscleGroups is not null)
            .ToArray();

        foreach (var first in strength)
        {
            foreach (var second in strength)
            {
                if (((int)first.Day + NextConsecutiveDayOffset) % DaysInWeek == (int)second.Day &&
                    HasMuscleOverlap(first.StrengthMuscleGroups!, second.StrengthMuscleGroups!))
                {
                    reasons.Add(ConsecutiveStrengthMuscleGroups);
                }
            }
        }

        return reasons.OrderBy(reason => reason, StringComparer.Ordinal).ToArray();
    }

    private static bool HasMuscleOverlap(
        IReadOnlySet<string> firstMuscles,
        IReadOnlySet<string> secondMuscles)
    {
        return firstMuscles.Any(m1 =>
            secondMuscles.Any(m2 => string.Equals(m1, m2, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsStrength(WorkoutActivityType activity) => activity is
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
        WorkoutActivityType.StrengthHighIntensity;
}
