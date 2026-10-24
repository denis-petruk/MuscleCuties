namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record StrengthWeekdayAssignmentV020(int SessionIndex, DayOfWeek Weekday);

public sealed record StrengthWeekdayAssignmentResultV020(
    string Status,
    IReadOnlyList<StrengthWeekdayAssignmentV020> Assignments,
    string? LimitationCode);

/// <summary>Maps a user's selected available weekdays to ordered strength proposal sessions.</summary>
public static class StrengthWeekdayAssignmentRuleV020
{
    private const int MinimumSessionsPerWeek = 1;
    private const int MaximumSessionsPerWeek = 7;
    private const int MondayWeekOrderOffset = 1;

    public static StrengthWeekdayAssignmentResultV020 Assign(
        int sessionCount,
        IReadOnlyList<DayOfWeek>? selectedWeekdays,
        IReadOnlySet<DayOfWeek>? availableWeekdays)
    {
        if (sessionCount is < MinimumSessionsPerWeek or > MaximumSessionsPerWeek ||
            selectedWeekdays is null || availableWeekdays is null ||
            selectedWeekdays.Count != sessionCount ||
            selectedWeekdays.Any(day => !Enum.IsDefined(day)) ||
            availableWeekdays.Any(day => !Enum.IsDefined(day)))
            return Limited("StrengthWeekdaySelectionInvalid");

        if (selectedWeekdays.Distinct().Count() != selectedWeekdays.Count)
            return Limited("StrengthWeekdaySelectionContainsDuplicates");

        if (selectedWeekdays.Any(day => !availableWeekdays.Contains(day)))
            return Limited("StrengthWeekdaySelectionIncludesUnavailableDay");

        var orderedDays = selectedWeekdays.OrderBy(WeekOrder).ToArray();
        return new StrengthWeekdayAssignmentResultV020("Assigned",
            orderedDays.Select((weekday, index) => new StrengthWeekdayAssignmentV020(index, weekday)).ToArray(),
            null);
    }

    private static StrengthWeekdayAssignmentResultV020 Limited(string limitationCode) =>
        new("Limited", [], limitationCode);

    private static int WeekOrder(DayOfWeek day) => day == DayOfWeek.Sunday
        ? MaximumSessionsPerWeek - 1
        : (int)day - MondayWeekOrderOffset;
}
