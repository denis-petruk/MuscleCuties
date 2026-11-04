namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum StrengthWeekdayAssignmentStatus
{
    Assigned,
    Limited
}

public sealed record StrengthWeekdayAssignment(int SessionIndex, DayOfWeek Weekday);

public sealed record StrengthWeekdayAssignmentResult(
    StrengthWeekdayAssignmentStatus Status,
    IReadOnlyList<StrengthWeekdayAssignment> Assignments,
    string? LimitationCode);

/// <summary>Assigns strength sessions to available weekdays with even spacing.</summary>
public static class StrengthWeekdayAssignmentRule
{
    public const int MinimumSessionsPerWeek = 1;
    public const int TwoSessionsPerWeek = 2;
    public const int TwoDaysAvailable = 2;
    public const int SpacingDivisorForTwoSessions = 2;

    private const int FirstSessionIndex = 0;
    private const int MondayWeekOrderOffset = 1;
    private const string InvalidInputCode = "StrengthWeekdayAssignmentInvalid";
    private const string SessionCountExceedsAvailabilityCode = "StrengthSessionsExceedAvailableWeekdays";

    public static StrengthWeekdayAssignmentResult Assign(
        int sessionCount,
        IReadOnlySet<DayOfWeek>? availableWeekdays)
    {
        if (sessionCount is < MinimumSessionsPerWeek or > WorkoutPreferencesRule.MaximumDaysPerWeek ||
            availableWeekdays is null || availableWeekdays.Count == 0 ||
            availableWeekdays.Any(day => !Enum.IsDefined(day)))
            return Limited(InvalidInputCode);

        if (sessionCount > availableWeekdays.Count)
            return Limited(SessionCountExceedsAvailabilityCode);

        var orderedWeekdays = availableWeekdays.OrderBy(WeekdayOrder).ToArray();
        var selectedWeekdays = SelectEvenlySpaced(orderedWeekdays, sessionCount);
        var assignments = selectedWeekdays
            .Select((weekday, index) => new StrengthWeekdayAssignment(index, weekday))
            .ToArray();

        return new StrengthWeekdayAssignmentResult(
            StrengthWeekdayAssignmentStatus.Assigned, assignments, null);
    }

    /// <summary>
    /// Deterministic weekday assignment according to Section 6:
    /// - f = 1: d_1 when n = 2 else middle day d_{n/2}
    /// - f = 2: maximal index spacing (j - i) with circular distance tie-break
    /// - f > 2: spread evenly across available weekdays by index
    /// </summary>
    public static StrengthWeekdayAssignmentResult AssignBySpecification(
        int sessionCount,
        IReadOnlySet<DayOfWeek>? availableWeekdays)
    {
        if (sessionCount is < MinimumSessionsPerWeek or > WorkoutPreferencesRule.MaximumDaysPerWeek ||
            availableWeekdays is null || availableWeekdays.Count == 0 ||
            availableWeekdays.Any(day => !Enum.IsDefined(day)))
            return Limited(InvalidInputCode);

        if (sessionCount > availableWeekdays.Count)
            return Limited(SessionCountExceedsAvailabilityCode);

        var orderedWeekdays = availableWeekdays.OrderBy(WeekdayOrder).ToArray();
        var n = orderedWeekdays.Length;

        DayOfWeek[] selectedWeekdays;

        if (sessionCount == MinimumSessionsPerWeek)
        {
            var chosenIndex = n == TwoDaysAvailable ? FirstSessionIndex : n / SpacingDivisorForTwoSessions;
            selectedWeekdays = [orderedWeekdays[chosenIndex]];
        }
        else if (sessionCount == TwoSessionsPerWeek)
        {
            var bestI = FirstSessionIndex;
            var bestJ = n - 1;
            var maxIndexSpacing = -1;
            var maxCircularDistance = -1;

            for (var i = FirstSessionIndex; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var indexSpacing = j - i;
                    var orderI = WeekdayOrder(orderedWeekdays[i]);
                    var orderJ = WeekdayOrder(orderedWeekdays[j]);
                    var diff = Math.Abs(orderJ - orderI);
                    var circularDist = Math.Min(diff, WorkoutPreferencesRule.MaximumDaysPerWeek - diff);

                    if (indexSpacing > maxIndexSpacing ||
                        (indexSpacing == maxIndexSpacing && circularDist > maxCircularDistance))
                    {
                        maxIndexSpacing = indexSpacing;
                        maxCircularDistance = circularDist;
                        bestI = i;
                        bestJ = j;
                    }
                }
            }

            selectedWeekdays = [orderedWeekdays[bestI], orderedWeekdays[bestJ]];
        }
        else
        {
            selectedWeekdays = new DayOfWeek[sessionCount];
            var lastIndex = sessionCount - 1;
            var maxAvailableIndex = n - 1;

            for (var k = FirstSessionIndex; k < sessionCount; k++)
            {
                var index = (int)Math.Round((double)k * maxAvailableIndex / lastIndex, MidpointRounding.AwayFromZero);
                selectedWeekdays[k] = orderedWeekdays[index];
            }
        }

        var assignments = selectedWeekdays
            .Select((weekday, index) => new StrengthWeekdayAssignment(index, weekday))
            .ToArray();

        return new StrengthWeekdayAssignmentResult(
            StrengthWeekdayAssignmentStatus.Assigned, assignments, null);
    }

    private static DayOfWeek[] SelectEvenlySpaced(DayOfWeek[] orderedWeekdays, int sessionCount)
    {
        var current = new DayOfWeek[sessionCount];
        var best = Array.Empty<DayOfWeek>();
        var bestScore = long.MaxValue;

        void Search(int nextIndex, int selectedCount)
        {
            if (selectedCount == sessionCount)
            {
                var score = SpacingScore(current);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = current.ToArray();
                }

                return;
            }

            var remaining = sessionCount - selectedCount;
            for (var index = nextIndex; index <= orderedWeekdays.Length - remaining; index++)
            {
                current[selectedCount] = orderedWeekdays[index];
                Search(index + 1, selectedCount + 1);
            }
        }

        long SpacingScore(IReadOnlyList<DayOfWeek> selected)
        {
            var score = 0L;
            for (var index = FirstSessionIndex; index < selected.Count; index++)
            {
                var currentDay = WeekdayOrder(selected[index]);
                var nextDay = index == selected.Count - 1
                    ? WeekdayOrder(selected[FirstSessionIndex]) + WorkoutPreferencesRule.MaximumDaysPerWeek
                    : WeekdayOrder(selected[index + 1]);
                var spacingError = (nextDay - currentDay) * sessionCount -
                                   WorkoutPreferencesRule.MaximumDaysPerWeek;
                score += (long)spacingError * spacingError;
            }

            return score;
        }

        Search(FirstSessionIndex, FirstSessionIndex);
        return best;
    }

    private static int WeekdayOrder(DayOfWeek day) => day == DayOfWeek.Sunday
        ? WorkoutPreferencesRule.MaximumDaysPerWeek - 1
        : (int)day - MondayWeekOrderOffset;

    private static StrengthWeekdayAssignmentResult Limited(string limitationCode) =>
        new(StrengthWeekdayAssignmentStatus.Limited, Array.Empty<StrengthWeekdayAssignment>(), limitationCode);
}
