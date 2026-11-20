using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Beta proposal 01a1f65a stored a week of ten sessions over four available days (four strength days
/// plus six companions), so the accept guard refused it with ProposalMixedScheduleInvalid. The
/// canonical projection now keeps the week inside the owner cap: the two features on a day are the
/// most a week can hold, and strength is what was over-scheduled.
/// </summary>
public sealed class CanonicalProjectionCapacityTests
{
    private const int OwnerMaximumActivitiesPerDay = 2;
    private const int StrengthActivity = (int)WorkoutActivityType.GeneralStrength;
    private const int SwimmingActivity = (int)WorkoutActivityType.Swimming;
    private const int YogaActivity = (int)WorkoutActivityType.Yoga;
    private const int WalkingActivity = (int)WorkoutActivityType.Walking;
    private const int RecoveryActivity = (int)WorkoutActivityType.GentleRecovery;
    private static readonly DateOnly LocalDate = new(2026, 11, 19);
    private static readonly DayOfWeek[] AvailableDays =
        [DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];

    [Fact]
    public void Canonical_week_never_exceeds_the_owner_cap_when_strength_days_and_companions_collide()
    {
        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(BetaPreview());

        var schedule = canonical.MixedActivitySchedule!;
        var layout = string.Join(", ", schedule.Sessions
            .GroupBy(session => session.Weekday)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}:{group.Count()}[{string.Join("/", group.Select(s => s.ActivityTypeId))}]"));

        Assert.True(schedule.Sessions.Count <= AvailableDays.Length * OwnerMaximumActivitiesPerDay,
            $"capacity: {layout}");
        Assert.All(schedule.Sessions.GroupBy(session => session.Weekday), group =>
            Assert.True(group.Count() <= OwnerMaximumActivitiesPerDay, $"per day: {layout}"));
        Assert.Equal(AvailableDays.Length * OwnerMaximumActivitiesPerDay, schedule.Sessions.Count);

        // Strength is what gives way, companions the person chose stay on the week.
        Assert.Equal(OwnerMaximumActivitiesPerDay, canonical.Sessions.Count);
        Assert.Contains(schedule.Sessions, session => session.ActivityTypeId == SwimmingActivity);
        Assert.Contains(schedule.Sessions, session => session.ActivityTypeId == YogaActivity);
        Assert.Contains(schedule.Sessions, session => session.ActivityTypeId == WalkingActivity);
        Assert.Contains(schedule.Sessions, session => session.ActivityTypeId == RecoveryActivity);

        // Nothing is dropped silently: the reduction is reported and must be acknowledged.
        Assert.Contains("StrengthSessionsExceedAvailableWeekdays", canonical.AdvisoryCodes);
        Assert.True(canonical.RequiresReducedVolumeAcknowledgment);

        // The stored week and the strength sessions handed to Apply agree.
        Assert.Equal(canonical.Sessions.Count, schedule.Sessions.Count(session =>
            session.ActivityTypeId == StrengthActivity));
    }

    [Fact]
    public void Canonical_week_keeps_every_strength_session_when_companions_leave_room()
    {
        var preview = BetaPreview() with { Sessions = StrengthSessions(2), GenerationV2 = Plan(2) };

        var canonical = GenerationPlanV2Mapper.ForCanonicalUse(preview);

        Assert.Equal(2, canonical.Sessions.Count);
        Assert.DoesNotContain("StrengthSessionsExceedAvailableWeekdays", canonical.AdvisoryCodes);
        Assert.False(canonical.RequiresReducedVolumeAcknowledgment);
    }

    private static StrengthPlanPreviewResponseV020 BetaPreview() =>
        new("MC-WORKOUT-POLICY", "0.2.0", "MC-PLANNING-0.4", "Feasible", 0,
            StrengthSessions(AvailableDays.Length), [], [], [],
            new MixedActivityScheduleV020(Companions(), [], []))
        {
            GenerationV2 = Plan(AvailableDays.Length),
            EffectiveActivityTypeId = StrengthActivity
        };

    private static IReadOnlyList<StrengthPlanPreviewSessionV020> StrengthSessions(int count) =>
        Enumerable.Range(0, count).Select(index => new StrengthPlanPreviewSessionV020(
            "MC-V2", "0.2.0", index, 60m,
            [new StrengthPlanSlotOutputV020("v2-KneeDominant-0", 0, true, 0, false, 101, "Squat",
                "Rule", "MC-V2", 3, 8, 12, 3, 150, [])],
            [], new Dictionary<string, string>(), AvailableDays[index])).ToArray();

    private static GenerationPlanV2 Plan(int strengthCount)
    {
        var strength = Enumerable.Range(0, strengthCount).Select(index => new GenerationSessionV2(
            index, AvailableDays[index], LocalDate, StrengthActivity, 5, 60m, [Exercise()])).ToArray();
        var companions = Companions().Select((session, index) => new GenerationSessionV2(
            strengthCount + index, session.Weekday, LocalDate, session.ActivityTypeId, 0,
            session.DurationMinutes, [])).ToArray();
        var all = strength.Concat(companions).ToArray();
        return new GenerationPlanV2("Feasible", "0.2.0", "1", "catalog", all.Length, "hash", [], [], [])
        {
            Sessions = all
        };
    }

    private static GenerationExerciseV2 Exercise() =>
        new(0, 101, "Squat", 0, 3, 8, 12, 3, 150, "CalibrationRequired", null, null, false, "Rule")
        {
            Pattern = nameof(MovementPattern.SquatPattern),
            SlotId = "v2-KneeDominant-0"
        };

    private static IReadOnlyList<MixedActivityScheduleSessionV020> Companions() =>
    [
        new(WalkingActivity, DayOfWeek.Monday, 30m, false, 2),
        new(RecoveryActivity, DayOfWeek.Thursday, 10m, false, 2),
        new(SwimmingActivity, DayOfWeek.Friday, 60m, false, 2),
        new(YogaActivity, DayOfWeek.Friday, 15m, false, 3),
        new(WalkingActivity, DayOfWeek.Saturday, 30m, false, 2),
        new(RecoveryActivity, DayOfWeek.Saturday, 10m, false, 3)
    ];
}
