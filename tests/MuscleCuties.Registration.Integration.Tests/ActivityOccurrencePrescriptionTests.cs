using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

// Pure transport and immutable-snapshot checks; these do not open a database.
public sealed class ActivityOccurrencePrescriptionTests
{
    private const int OneWeeklySession = 1;
    private const decimal OriginalMinutes = ActivitySessionDurationRule.DemandingYogaMaximumMinutes;
    private const decimal ReducedMinutes = ActivitySessionDurationRule.DemandingYogaMinimumMinutes;

    [Fact]
    public void UnchangedOccurrenceUsesOriginalPrescriptionAndSurvivesSnapshotRoundTrip()
    {
        var original = Prescription(OriginalMinutes);
        var session = Session(OriginalMinutes);
        var decision = Decision(original);
        Assert.True(ActivityOccurrencePrescription.IsValid(session, decision));
        var validation = ActivityOccurrencePrescription.ValidationSession(session, decision);
        Assert.Equal(OriginalMinutes, validation.OccurrencePrescription!.MainMinutes);

        var snapshot = new PlannedPrescriptionSnapshot(Guid.NewGuid(), "Synthetic yoga", "Yoga",
            (int)WorkoutActivityType.Yoga, OriginalMinutes, [])
        {
            ActivityPrescriptionJson = ActivityOccurrencePrescription.Serialize(original)
        };
        var restored = PlannedPrescriptionSnapshot.Deserialize(snapshot.Serialize());
        Assert.NotNull(restored);
        Assert.Equal(snapshot.DayId, restored.DayId);
        Assert.Equal(OriginalMinutes,
            ActivityOccurrencePrescription.Deserialize(restored.ActivityPrescriptionJson)!.MainMinutes);
    }

    [Fact]
    public void ReducedOccurrenceRetainsItsDoseAcrossSerializationAndRejectsTampering()
    {
        var original = Prescription(OriginalMinutes);
        var reduced = original with { MainMinutes = ReducedMinutes };
        var restored = ActivityOccurrencePrescription.Deserialize(ActivityOccurrencePrescription.Serialize(reduced));
        var session = Session(ReducedMinutes) with { OccurrencePrescription = restored };
        var decision = Decision(original);
        Assert.True(ActivityOccurrencePrescription.IsValid(session, decision));
        Assert.Equal(ReducedMinutes,
            ActivityOccurrencePrescription.ValidationSession(session, decision).OccurrencePrescription!.MainMinutes);
        Assert.False(ActivityOccurrencePrescription.IsValid(session with { DurationMinutes = OriginalMinutes }, decision));
        Assert.False(ActivityOccurrencePrescription.IsValid(session with
        {
            OccurrencePrescription = reduced with { ActivityTypeId = (int)WorkoutActivityType.RockClimbing }
        }, decision));
        Assert.Throws<InvalidOperationException>(() => ActivityOccurrencePrescription.Deserialize("{"));
    }

    [Fact]
    public void HistoricalProposalsRemainCompatibleButPartialEvidenceFailsClosed()
    {
        var session = Session(OriginalMinutes);
        Assert.True(ActivityOccurrencePrescription.IsValid(session, Decision(null)));
        Assert.False(ActivityOccurrencePrescription.IsValid(session with
        {
            OccurrencePrescription = Prescription(ReducedMinutes)
        }, Decision(null)));
        Assert.False(ActivityOccurrencePrescription.IsValid(session,
            Decision(Prescription(OriginalMinutes) with { Intensity = "unknown-intensity" })));
    }

    private static ActivitySessionPrescriptionOutputV020 Prescription(decimal minutes) => new(
        (int)WorkoutActivityType.Yoga, nameof(SessionPurpose.Yoga), nameof(ActivityModality.Yoga),
        nameof(IntendedIntensity.Vigorous), nameof(SessionTrainingStyle.Demanding), OneWeeklySession,
        null, minutes, null, null, null, null, null, null, null, null, null, null, true, []);

    private static MixedActivityScheduleSessionV020 Session(decimal minutes) =>
        new((int)WorkoutActivityType.Yoga, DayOfWeek.Monday, minutes, true);

    private static MixedActivityScheduleDecisionV020 Decision(ActivitySessionPrescriptionOutputV020? original) =>
        new((int)WorkoutActivityType.Yoga, nameof(MixedActivityScheduleDisposition.Scheduled),
            nameof(ActivityPrescriptionStatus.Eligible), OneWeeklySession, OneWeeklySession, OneWeeklySession, [],
            OriginalPrescription: original);
}
